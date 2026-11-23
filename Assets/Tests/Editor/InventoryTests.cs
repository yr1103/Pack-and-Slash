using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 다중 아이템 배치·이동·회전 시 충돌, 잠긴 셀·범위 밖 실패, 실패 시 상태 유지, Shape 원본 불변,
/// Inventory·Storage·Temporary 영역 간 이동 시 점유·카운트·회전·개수 변화 이벤트·실패 시 원본 유지,
/// 영역별 판매 해제·판매 불가·None 판매 실패·판매가 검증
/// </summary>
public class InventoryTests
{
    private const string AWAKE_METHOD = "Awake";
    private const string IS_SELLABLE_FIELD = "_isSellable";
    private const int ROTATION_COUNT = 4;

    // 기본 배치 (활성 영역 x 3~6, y 1~4)
    private static readonly Vector2Int _shotgunPosition = new Vector2Int(3, 1);
    private static readonly Vector2Int _minigunPosition = new Vector2Int(4, 1);
    private static readonly Vector2Int _pistolAPosition = new Vector2Int(4, 3);
    private static readonly Vector2Int _pistolBPosition = new Vector2Int(5, 3);

    // 기본 배치에서 아무도 점유하지 않는 활성 셀
    private static readonly Vector2Int[] _freeCells =
    {
        new Vector2Int(4, 1), new Vector2Int(5, 1), new Vector2Int(3, 4),
        new Vector2Int(6, 3), new Vector2Int(6, 4),
    };

    private static readonly Vector2 _temporaryPosition = new Vector2(10f, 20f);

    private GameObject _inventoryObject;
    private Inventory _inventory;
    private InventoryData _pistolData;
    private InventoryData _shotgunData;
    private InventoryData _minigunData;
    private InventoryData _probeData;
    private Inventory.ItemInstance _shotgun;
    private Inventory.ItemInstance _minigun;
    private Inventory.ItemInstance _pistolA;
    private Inventory.ItemInstance _pistolB;
    private Inventory.ItemInstance _probe;

    [SetUp]
    public void SetUp()
    {
        _pistolData = CreateData("Pistol", new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) });
        _shotgunData = CreateData("Shotgun",
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) });
        _minigunData = CreateData("Minigun",
            new[] { new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) });
        // 1칸 아이템으로 CanPlace를 시도해 셀 점유 여부를 public API만으로 확인
        _probeData = CreateData("Probe", new[] { new Vector2Int(0, 0) });

        // EditMode에서는 AddComponent 시 Awake가 호출되지 않아 리플렉션으로 그리드 초기화
        _inventoryObject = new GameObject();
        _inventory = _inventoryObject.AddComponent<Inventory>();
        typeof(Inventory)
            .GetMethod(AWAKE_METHOD, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_inventory, null);

        _shotgun = new Inventory.ItemInstance(_shotgunData);
        _minigun = new Inventory.ItemInstance(_minigunData);
        _pistolA = new Inventory.ItemInstance(_pistolData);
        _pistolB = new Inventory.ItemInstance(_pistolData);
        _probe = new Inventory.ItemInstance(_probeData);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_inventoryObject);
        Object.DestroyImmediate(_pistolData);
        Object.DestroyImmediate(_shotgunData);
        Object.DestroyImmediate(_minigunData);
        Object.DestroyImmediate(_probeData);
    }

    [Test]
    public void PlaceItem_DefaultLayout_AllSucceedAndOccupyCells()
    {
        PlaceDefaultLayout();

        Assert.AreNotSame(_pistolA, _pistolB);
        AssertOccupied(new Vector2Int(3, 1), new Vector2Int(3, 2), new Vector2Int(3, 3)); // 샷건
        AssertOccupied(new Vector2Int(6, 1), new Vector2Int(4, 2), new Vector2Int(5, 2), new Vector2Int(6, 2)); // 미니건
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4)); // 권총A
        AssertOccupied(new Vector2Int(5, 3), new Vector2Int(5, 4)); // 권총B
        AssertFree(_freeCells);
    }

    [Test]
    public void PlaceItem_OnOccupiedCell_Fails()
    {
        PlaceDefaultLayout();
        Inventory.ItemInstance pistolC = new Inventory.ItemInstance(_pistolData);

        Assert.IsFalse(_inventory.PlaceItem(pistolC, _pistolBPosition));
        Assert.IsFalse(pistolC.IsPlaced);
        AssertFree(_freeCells);
    }

    [Test]
    public void MoveItem_OnOtherItemCell_FailsAndKeepsState()
    {
        PlaceDefaultLayout();

        Assert.IsFalse(_inventory.MoveItem(_pistolA, _pistolBPosition));

        Assert.AreEqual(_pistolAPosition, _pistolA.Position);
        Assert.AreEqual(0, _pistolA.Rotation);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
        // 권총A 기준으로도 막혀 있으면 권총B가 그대로 점유 중
        Assert.IsFalse(_inventory.CanPlace(_pistolA, _pistolBPosition, 0));
        AssertFree(_freeCells);
    }

    // (3,4)가 비어 있어 샷건을 1칸 올리면 자기 셀 (3,2),(3,3)과 겹침
    [Test]
    public void MoveItem_OverlappingOwnCells_Succeeds()
    {
        PlaceDefaultLayout();
        Vector2Int newPosition = new Vector2Int(3, 2);

        Assert.IsTrue(_inventory.MoveItem(_shotgun, newPosition));

        Assert.AreEqual(newPosition, _shotgun.Position);
        AssertOccupied(new Vector2Int(3, 2), new Vector2Int(3, 3), new Vector2Int(3, 4));
        AssertFree(new Vector2Int(3, 1));
    }

    [TestCase(0, 0)] // 잠긴 셀
    [TestCase(9, 5)] // 배열 끝 잠긴 셀, 두 번째 칸 (9,6)은 범위 밖
    [TestCase(-1, 0)] // 배열 범위 밖
    public void MoveItem_ToLockedOrOutOfRange_FailsAndKeepsState(int x, int y)
    {
        PlaceDefaultLayout();

        Assert.IsFalse(_inventory.MoveItem(_pistolA, new Vector2Int(x, y)));

        Assert.AreEqual(_pistolAPosition, _pistolA.Position);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
    }

    [Test]
    public void RotateItem_Pistol_ShapeMatchesEachRotation()
    {
        AssertRotationCycle(_pistolA, new[]
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
        });
    }

    [Test]
    public void RotateItem_Shotgun_ShapeMatchesEachRotation()
    {
        AssertRotationCycle(_shotgun, new[]
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
        });
    }

    [Test]
    public void RotateItem_Minigun_ShapeMatchesEachRotation()
    {
        AssertRotationCycle(_minigun, new[]
        {
            new[] { new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(1, 2), new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
            new[] { new Vector2Int(0, 1), new Vector2Int(2, 0), new Vector2Int(1, 0), new Vector2Int(0, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 2), new Vector2Int(1, 1), new Vector2Int(1, 0) },
        });
    }

    // 권총A 가로 회전 시 (4,3),(5,3) 중 (5,3)을 권총B가 점유
    [Test]
    public void RotateItem_CollidesWithOtherItem_FailsAndKeepsState()
    {
        PlaceDefaultLayout();

        Assert.IsFalse(_inventory.RotateItem(_pistolA));

        Assert.AreEqual(0, _pistolA.Rotation);
        Assert.AreEqual(_pistolAPosition, _pistolA.Position);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
        Assert.IsFalse(_inventory.CanPlace(_pistolA, _pistolBPosition, 0));
    }

    // 활성 영역이 배열 가장자리에서 떨어져 있어 배치된 아이템의 회전은 배열 범위 밖에 닿지 않으므로 잠긴 셀만 검증
    [Test]
    public void RotateItem_IntoLockedCell_FailsAndKeepsState()
    {
        Vector2Int position = new Vector2Int(5, 1);
        Assert.IsTrue(_inventory.PlaceItem(_shotgun, position));

        // 가로 회전 시 (7,1)이 잠긴 셀
        Assert.IsFalse(_inventory.RotateItem(_shotgun));

        Assert.AreEqual(0, _shotgun.Rotation);
        Assert.AreEqual(position, _shotgun.Position);
        AssertOccupied(new Vector2Int(5, 1), new Vector2Int(5, 2), new Vector2Int(5, 3));
        AssertFree(new Vector2Int(6, 1));
    }

    [Test]
    public void MoveAndRotate_DoesNotModifyItemShapeAsset()
    {
        Vector2Int[] originalShape = (Vector2Int[])_minigunData.ItemShape.Clone();
        Assert.IsTrue(_inventory.PlaceItem(_minigun, new Vector2Int(3, 1)));

        Assert.IsTrue(_inventory.RotateItem(_minigun));
        Assert.IsTrue(_inventory.MoveItem(_minigun, new Vector2Int(4, 2)));
        Assert.IsTrue(_inventory.RotateItem(_minigun));

        CollectionAssert.AreEqual(originalShape, _minigunData.ItemShape);
    }

    // 같은 InventoryData를 공유하는 두 권총이 회전 상태를 공유하지 않아야 함
    [Test]
    public void RotateItem_OneItem_DoesNotAffectOtherItemRotation()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, new Vector2Int(3, 1)));
        Assert.IsTrue(_inventory.PlaceItem(_pistolB, new Vector2Int(5, 1)));

        Assert.IsTrue(_inventory.RotateItem(_pistolA));

        Assert.AreEqual(1, _pistolA.Rotation);
        Assert.AreEqual(0, _pistolB.Rotation);
        AssertOccupied(new Vector2Int(5, 1), new Vector2Int(5, 2));
    }

    [Test]
    public void MoveToTemporary_FromInventory_ReleasesCellsAndCounts()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));
        int eventCount = 0;
        _inventory.TemporaryCountChanged += () => eventCount++;

        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.AreEqual(ItemLocation.Temporary, _pistolA.Location);
        Assert.IsFalse(_pistolA.IsPlaced);
        Assert.AreEqual(_temporaryPosition, _pistolA.TemporaryPosition);
        Assert.AreEqual(1, _inventory.TemporaryCount);
        Assert.AreEqual(1, eventCount);
        Assert.IsTrue(_inventory.PlaceItem(_pistolB, _pistolAPosition));
    }

    // Temporary 내 좌표 이동은 카운트·이벤트 변화 없음
    [Test]
    public void MoveToTemporary_WithinTemporary_UpdatesPositionOnly()
    {
        _inventory.MoveToTemporary(_pistolA, Vector2.zero, 0);
        int eventCount = 0;
        _inventory.TemporaryCountChanged += () => eventCount++;

        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.AreEqual(_temporaryPosition, _pistolA.TemporaryPosition);
        Assert.AreEqual(1, _inventory.TemporaryCount);
        Assert.AreEqual(0, eventCount);
    }

    [Test]
    public void TryPlace_FromTemporary_Succeeds()
    {
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);
        int eventCount = 0;
        _inventory.TemporaryCountChanged += () => eventCount++;

        Assert.IsTrue(_inventory.TryPlace(_pistolA, _pistolAPosition, 0));

        Assert.AreEqual(ItemLocation.Inventory, _pistolA.Location);
        Assert.AreEqual(_pistolAPosition, _pistolA.Position);
        Assert.AreEqual(0, _inventory.TemporaryCount);
        Assert.AreEqual(1, eventCount);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
    }

    [Test]
    public void TryPlace_FromTemporaryOnOccupiedCell_FailsAndKeepsState()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolB, _pistolBPosition));
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.IsFalse(_inventory.TryPlace(_pistolA, _pistolBPosition, 0));

        Assert.AreEqual(ItemLocation.Temporary, _pistolA.Location);
        Assert.AreEqual(_temporaryPosition, _pistolA.TemporaryPosition);
        Assert.AreEqual(1, _inventory.TemporaryCount);
        AssertOccupied(new Vector2Int(5, 3), new Vector2Int(5, 4));
        Assert.IsFalse(_inventory.CanPlace(_pistolA, _pistolBPosition, 0));
    }

    // 가로 회전 상태로 보관 시 회전 셀 해제 후 회전 0
    [Test]
    public void TryStore_FromInventory_ReleasesCellsAndResetsRotation()
    {
        Vector2Int position = new Vector2Int(3, 1);
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, position));
        Assert.IsTrue(_inventory.RotateItem(_pistolA));

        Assert.IsTrue(_inventory.TryStore(_pistolA));

        Assert.AreEqual(ItemLocation.Storage, _pistolA.Location);
        Assert.AreEqual(0, _pistolA.Rotation);
        Assert.AreEqual(1, _inventory.StorageCount);
        Assert.AreSame(_pistolA, _inventory.GetStorageItem(_inventory.GetStorageIndex(_pistolA)));
        Assert.AreEqual(0, _inventory.TemporaryCount);
        AssertFree(new Vector2Int(3, 1), new Vector2Int(4, 1));
    }

    [Test]
    public void TryStore_WhenFull_FailsAndKeepsState()
    {
        FillStorage();
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));
        _inventory.MoveToTemporary(_shotgun, _temporaryPosition, 0);

        Assert.IsFalse(_inventory.TryStore(_pistolA));
        Assert.IsFalse(_inventory.TryStore(_shotgun));

        Assert.AreEqual(ItemLocation.Inventory, _pistolA.Location);
        Assert.AreEqual(_pistolAPosition, _pistolA.Position);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
        Assert.AreEqual(ItemLocation.Temporary, _shotgun.Location);
        Assert.AreEqual(_temporaryPosition, _shotgun.TemporaryPosition);
        Assert.AreEqual(1, _inventory.TemporaryCount);
        Assert.AreEqual(Inventory.STORAGE_CAPACITY, _inventory.StorageCount);
    }

    [Test]
    public void MoveToTemporary_NotCountedInStorage()
    {
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.AreEqual(0, _inventory.StorageCount);
        Assert.AreEqual(-1, _inventory.GetStorageIndex(_pistolA));
    }

    [Test]
    public void MoveToTemporary_FromStorage_ReleasesSlot()
    {
        Assert.IsTrue(_inventory.TryStore(_pistolA));
        int slotIndex = _inventory.GetStorageIndex(_pistolA);

        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.AreEqual(ItemLocation.Temporary, _pistolA.Location);
        Assert.AreEqual(0, _inventory.StorageCount);
        Assert.IsNull(_inventory.GetStorageItem(slotIndex));
        Assert.AreEqual(1, _inventory.TemporaryCount);
    }

    [Test]
    public void TryPlace_FromStorage_ReleasesSlotAndOccupiesCells()
    {
        Assert.IsTrue(_inventory.TryStore(_pistolA));

        Assert.IsTrue(_inventory.TryPlace(_pistolA, _pistolAPosition, 0));

        Assert.AreEqual(ItemLocation.Inventory, _pistolA.Location);
        Assert.AreEqual(0, _inventory.StorageCount);
        Assert.AreEqual(0, _inventory.TemporaryCount);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
    }

    // 권총A의 이전 Position에서 가로 회전 시 (5,3)을 권총B가 점유하지만 Temporary는 검사하지 않음
    [Test]
    public void RotateItem_Temporary_SucceedsWithoutCollisionCheck()
    {
        PlaceDefaultLayout();
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.IsTrue(_inventory.RotateItem(_pistolA));

        Assert.AreEqual(1, _pistolA.Rotation);
        Assert.AreEqual(ItemLocation.Temporary, _pistolA.Location);
        AssertOccupied(new Vector2Int(5, 3), new Vector2Int(5, 4));
    }

    [Test]
    public void RotateItem_Storage_Fails()
    {
        Assert.IsTrue(_inventory.TryStore(_pistolA));

        Assert.IsFalse(_inventory.RotateItem(_pistolA));

        Assert.AreEqual(0, _pistolA.Rotation);
        Assert.AreEqual(ItemLocation.Storage, _pistolA.Location);
    }

    [Test]
    public void TryPlace_FromTemporaryWithRotation_AppliesRotation()
    {
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);
        Vector2Int position = new Vector2Int(3, 1);

        Assert.IsTrue(_inventory.TryPlace(_pistolA, position, 1));

        Assert.AreEqual(ItemLocation.Inventory, _pistolA.Location);
        Assert.AreEqual(position, _pistolA.Position);
        Assert.AreEqual(1, _pistolA.Rotation);
        AssertOccupied(new Vector2Int(3, 1), new Vector2Int(4, 1));
        AssertFree(new Vector2Int(3, 2));
    }

    // 세로(회전 0)로는 (4,3),(4,4)가 비어 있지만 가로 회전 시 (5,3)을 권총B가 점유
    [Test]
    public void TryPlace_FromTemporaryWithRotationCollides_FailsAndKeepsState()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolB, _pistolBPosition));
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.IsFalse(_inventory.TryPlace(_pistolA, _pistolAPosition, 1));

        Assert.AreEqual(ItemLocation.Temporary, _pistolA.Location);
        Assert.AreEqual(0, _pistolA.Rotation);
        Assert.AreEqual(_temporaryPosition, _pistolA.TemporaryPosition);
        Assert.AreEqual(1, _inventory.TemporaryCount);
        AssertFree(new Vector2Int(4, 3), new Vector2Int(4, 4));
    }

    [Test]
    public void TryPlace_WithinInventoryWithRotation_MovesAndRotates()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, new Vector2Int(3, 1)));
        Vector2Int newPosition = new Vector2Int(4, 1);

        Assert.IsTrue(_inventory.TryPlace(_pistolA, newPosition, 1));

        Assert.AreEqual(newPosition, _pistolA.Position);
        Assert.AreEqual(1, _pistolA.Rotation);
        AssertOccupied(new Vector2Int(4, 1), new Vector2Int(5, 1));
        AssertFree(new Vector2Int(3, 1), new Vector2Int(3, 2));
    }

    // 가로 회전으로 (5,4),(6,4)에 놓으면 (5,4)를 권총B가 점유
    [Test]
    public void TryPlace_WithinInventoryWithRotationCollides_FailsAndKeepsState()
    {
        PlaceDefaultLayout();

        Assert.IsFalse(_inventory.TryPlace(_pistolA, new Vector2Int(5, 4), 1));

        Assert.AreEqual(ItemLocation.Inventory, _pistolA.Location);
        Assert.AreEqual(_pistolAPosition, _pistolA.Position);
        Assert.AreEqual(0, _pistolA.Rotation);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
        AssertFree(_freeCells);
    }

    // 출발 셀은 기존 회전(세로) 기준으로 해제되어야 함
    [Test]
    public void MoveToTemporary_FromInventoryWithRotation_StoresRotationAndReleasesCells()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, new Vector2Int(3, 1)));

        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 1);

        Assert.AreEqual(ItemLocation.Temporary, _pistolA.Location);
        Assert.AreEqual(1, _pistolA.Rotation);
        AssertFree(new Vector2Int(3, 1), new Vector2Int(3, 2));
    }

    [Test]
    public void StorageCountChanged_TryStoreSucceeds_RaisedOnce()
    {
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;

        Assert.IsTrue(_inventory.TryStore(_pistolA));

        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void StorageCountChanged_FromStorageToTemporary_RaisedOnce()
    {
        Assert.IsTrue(_inventory.TryStore(_pistolA));
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;

        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);

        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void StorageCountChanged_FromStorageToInventory_RaisedOnce()
    {
        Assert.IsTrue(_inventory.TryStore(_pistolA));
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;

        Assert.IsTrue(_inventory.TryPlace(_pistolA, _pistolAPosition, 0));

        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void StorageCountChanged_TryStoreFails_NotRaised()
    {
        FillStorage();
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;

        Assert.IsFalse(_inventory.TryStore(_pistolA));

        Assert.AreEqual(0, eventCount);
    }

    // 구독자가 전환 중간 상태(Location None)를 보지 않아야 함
    [Test]
    public void TemporaryCountChanged_LeavingTemporary_RaisedAfterArrival()
    {
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);
        ItemLocation locationAtEvent = ItemLocation.None;
        _inventory.TemporaryCountChanged += () => locationAtEvent = _pistolA.Location;

        Assert.IsTrue(_inventory.TryPlace(_pistolA, _pistolAPosition, 0));

        Assert.AreEqual(ItemLocation.Inventory, locationAtEvent);
    }

    [Test]
    public void TrySell_FromInventory_ReleasesCellsAndSetsNone()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));

        Assert.IsTrue(_inventory.TrySell(_pistolA));

        Assert.AreEqual(ItemLocation.None, _pistolA.Location);
        AssertFree(new Vector2Int(4, 3), new Vector2Int(4, 4));
    }

    [Test]
    public void TrySell_FromStorage_ReleasesSlotAndRaisesStorageEvent()
    {
        Assert.IsTrue(_inventory.TryStore(_pistolA));
        int slotIndex = _inventory.GetStorageIndex(_pistolA);
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;

        Assert.IsTrue(_inventory.TrySell(_pistolA));

        Assert.AreEqual(ItemLocation.None, _pistolA.Location);
        Assert.AreEqual(0, _inventory.StorageCount);
        Assert.IsNull(_inventory.GetStorageItem(slotIndex));
        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void TrySell_FromTemporary_DecreasesCountAndRaisesTemporaryEvent()
    {
        _inventory.MoveToTemporary(_pistolA, _temporaryPosition, 0);
        int eventCount = 0;
        _inventory.TemporaryCountChanged += () => eventCount++;

        Assert.IsTrue(_inventory.TrySell(_pistolA));

        Assert.AreEqual(ItemLocation.None, _pistolA.Location);
        Assert.AreEqual(0, _inventory.TemporaryCount);
        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void TrySell_FromInventory_RaisesNoCountEvents()
    {
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));
        int eventCount = 0;
        _inventory.TemporaryCountChanged += () => eventCount++;
        _inventory.StorageCountChanged += () => eventCount++;

        Assert.IsTrue(_inventory.TrySell(_pistolA));

        Assert.AreEqual(0, eventCount);
    }

    // 진화 무기처럼 판매 불가로 설정된 데이터
    [Test]
    public void TrySell_NotSellable_FailsAndKeepsState()
    {
        SetSellable(_pistolData, false);
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));
        _inventory.MoveToTemporary(_pistolB, _temporaryPosition, 0);

        Assert.IsFalse(_inventory.CanSell(_pistolA));
        Assert.IsFalse(_inventory.TrySell(_pistolA));
        Assert.IsFalse(_inventory.TrySell(_pistolB));

        Assert.AreEqual(ItemLocation.Inventory, _pistolA.Location);
        AssertOccupied(new Vector2Int(4, 3), new Vector2Int(4, 4));
        Assert.AreEqual(ItemLocation.Temporary, _pistolB.Location);
        Assert.AreEqual(1, _inventory.TemporaryCount);
    }

    // 생성 직후 또는 이미 판매된 아이템
    [Test]
    public void TrySell_LocationNone_Fails()
    {
        Assert.IsFalse(_inventory.CanSell(_pistolA));
        Assert.IsFalse(_inventory.TrySell(_pistolA));

        Assert.IsTrue(_inventory.TryStore(_pistolB));
        Assert.IsTrue(_inventory.TrySell(_pistolB));
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;

        Assert.IsFalse(_inventory.TrySell(_pistolB));
        Assert.AreEqual(0, eventCount);
    }

    [TestCase(1, 1)]
    [TestCase(3, 3)]
    public void GetSellPrice_ReturnsLevelTimesPricePerLevel(int level, int expectedPrice)
    {
        _pistolA.Level = level;

        Assert.AreEqual(expectedPrice, _inventory.GetSellPrice(_pistolA));
    }

    // 보상 후보처럼 ItemInstance 없이 데이터·레벨로 조회
    [Test]
    public void GetSellPrice_DataAndLevel_ReturnsLevelTimesPricePerLevel()
    {
        Assert.AreEqual(3, _inventory.GetSellPrice(_pistolData, 3));
        Assert.AreEqual(0, _inventory.GetSellPrice((InventoryData)null, 1));
    }

    private void FillStorage()
    {
        for (int i = 0; i < Inventory.STORAGE_CAPACITY; i++)
        {
            Assert.IsTrue(_inventory.TryStore(new Inventory.ItemInstance(_probeData)));
        }
    }

    private void PlaceDefaultLayout()
    {
        Assert.IsTrue(_inventory.PlaceItem(_shotgun, _shotgunPosition));
        Assert.IsTrue(_inventory.PlaceItem(_minigun, _minigunPosition));
        Assert.IsTrue(_inventory.PlaceItem(_pistolA, _pistolAPosition));
        Assert.IsTrue(_inventory.PlaceItem(_pistolB, _pistolBPosition));
    }

    // 빈 활성 영역 (3,1)에 단독 배치 후 0→90→180→270→0 회전하며 Shape 확인
    private void AssertRotationCycle(Inventory.ItemInstance item, Vector2Int[][] expectedShapes)
    {
        Assert.IsTrue(_inventory.PlaceItem(item, new Vector2Int(3, 1)));
        CollectionAssert.AreEquivalent(expectedShapes[0], _inventory.GetRotatedShape(item, item.Rotation));

        for (int i = 1; i <= ROTATION_COUNT; i++)
        {
            Assert.IsTrue(_inventory.RotateItem(item));
            Assert.AreEqual(i % ROTATION_COUNT, item.Rotation);
            CollectionAssert.AreEquivalent(expectedShapes[i % ROTATION_COUNT],
                _inventory.GetRotatedShape(item, item.Rotation));
        }
    }

    private void AssertOccupied(params Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells)
        {
            Assert.IsFalse(_inventory.CanPlace(_probe, cell, 0), $"{cell} 비어 있음");
        }
    }

    private void AssertFree(params Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells)
        {
            Assert.IsTrue(_inventory.CanPlace(_probe, cell, 0), $"{cell} 점유됨");
        }
    }

    // 테스트에 필요한 값은 이름·Shape뿐이라 나머지는 기본값 (maxLevel 1)
    private static InventoryData CreateData(string itemName, Vector2Int[] shape)
    {
        InventoryData data = ScriptableObject.CreateInstance<InventoryData>();
        data.SetData(0, itemName, null, ItemType.Weapon, string.Empty, shape, 1, null);
        return data;
    }

    // SetData 시그니처를 바꾸지 않도록 직렬화 필드로 설정
    private static void SetSellable(InventoryData data, bool isSellable)
    {
        SerializedObject serializedData = new SerializedObject(data);
        serializedData.FindProperty(IS_SELLABLE_FIELD).boolValue = isSellable;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
    }
}
