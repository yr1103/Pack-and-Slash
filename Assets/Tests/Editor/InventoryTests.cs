using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 다중 아이템 배치·이동·회전 시 충돌, 잠긴 셀·범위 밖 실패, 실패 시 상태 유지, Shape 원본 불변,
/// Inventory·Storage·Temporary 영역 간 이동 시 점유·카운트·회전·개수 변화 이벤트·실패 시 원본 유지,
/// 영역별 판매 해제·판매 불가·None 판매 실패·판매가 검증,
/// 영역 9개 조합 합성 시 재료 해제·결과 아이템 위치 유지·카운트·이벤트, 합성 불가 조건(최대 레벨·다른 Id·다른 Level·자기 자신·고유·None·null) 상태 유지 검증,
/// 무기 레벨 배율·패시브 인접 효과(상하좌우·대각선 제외·중복 1회·합산·회전·영역 이동·제거·고유 제외·상한)·Lv.3 효과 활성 검증
/// </summary>
public class InventoryTests
{
    private const string AWAKE_METHOD = "Awake";
    private const string IS_SELLABLE_FIELD = "_isSellable";
    private const int ROTATION_COUNT = 4;

    // 합성 테스트 데이터 (일반 무기·패시브 MaxLevel 5, 고유 아이템 MaxLevel 1)
    private const int MERGE_ITEM_ID = 1;
    private const int OTHER_MERGE_ITEM_ID = 2;
    private const int UNIQUE_ITEM_ID = 3;
    private const int MERGE_MAX_LEVEL = 5;
    private const int UNIQUE_MAX_LEVEL = 1;
    private const int MERGE_ROTATION = 1; // 결과 아이템 회전 유지 확인용 (Storage는 0으로 초기화)

    // 인접 효과 테스트 데이터 (모두 MaxLevel 5, 합성 대상끼리만 같은 Id)
    private const string ADJACENCY_EFFECT_TYPE_FIELD = "_adjacencyEffectType";
    private const float FLOAT_TOLERANCE = 0.0001f;
    private const int ADJACENCY_WEAPON_ID = 10;
    private const int LONG_WEAPON_ID = 11;
    private const int PASSIVE_ID = 12;
    private const int LONG_PASSIVE_ID = 13;
    private const int ESSENTIAL_ID = 14;

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

    // 합성 재료·대상 배치 (1칸 아이템, 서로 겹치지 않는 활성 셀·Temporary 좌표)
    private static readonly Vector2Int _mergeSourceCell = new Vector2Int(3, 1);
    private static readonly Vector2Int _mergeTargetCell = new Vector2Int(6, 4);
    private static readonly Vector2 _mergeTargetTemporaryPosition = new Vector2(30f, 40f);

    // 인접 효과 Shape·배치 (무기 1칸을 활성 영역 안쪽 (4,2)에 두어 상하좌우가 모두 활성 셀)
    private static readonly Vector2Int[] _singleCellShape = { new Vector2Int(0, 0) };
    private static readonly Vector2Int[] _verticalTwoCellShape = { new Vector2Int(0, 0), new Vector2Int(0, 1) };
    private static readonly Vector2Int _adjacencyWeaponCell = new Vector2Int(4, 2);

    private GameObject _inventoryObject;
    private Inventory _inventory;
    private InventoryData _pistolData;
    private InventoryData _shotgunData;
    private InventoryData _minigunData;
    private InventoryData _probeData;
    private InventoryData _mergeData;
    private InventoryData _otherMergeData;
    private InventoryData _uniqueData;
    private InventoryData _adjacencyWeaponData;
    private InventoryData _longWeaponData;
    private InventoryData _passiveData;
    private InventoryData _longPassiveData;
    private InventoryData _essentialData;
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
        _mergeData = CreateMergeData(MERGE_ITEM_ID, "MergeItem", MERGE_MAX_LEVEL);
        _otherMergeData = CreateMergeData(OTHER_MERGE_ITEM_ID, "OtherMergeItem", MERGE_MAX_LEVEL);
        _uniqueData = CreateMergeData(UNIQUE_ITEM_ID, "UniqueItem", UNIQUE_MAX_LEVEL);
        _adjacencyWeaponData = CreateAdjacencyData(ADJACENCY_WEAPON_ID, ItemType.Weapon,
            AdjacencyEffectType.None, _singleCellShape);
        _longWeaponData = CreateAdjacencyData(LONG_WEAPON_ID, ItemType.Weapon,
            AdjacencyEffectType.None, _verticalTwoCellShape);
        _passiveData = CreateAdjacencyData(PASSIVE_ID, ItemType.Passive,
            AdjacencyEffectType.Damage, _singleCellShape);
        _longPassiveData = CreateAdjacencyData(LONG_PASSIVE_ID, ItemType.Passive,
            AdjacencyEffectType.Damage, _verticalTwoCellShape);
        // 효과 종류를 지정해도 고유 아이템은 주지도 받지도 않아야 함
        _essentialData = CreateAdjacencyData(ESSENTIAL_ID, ItemType.Essential,
            AdjacencyEffectType.Damage, _singleCellShape);

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
        Object.DestroyImmediate(_mergeData);
        Object.DestroyImmediate(_otherMergeData);
        Object.DestroyImmediate(_uniqueData);
        Object.DestroyImmediate(_adjacencyWeaponData);
        Object.DestroyImmediate(_longWeaponData);
        Object.DestroyImmediate(_passiveData);
        Object.DestroyImmediate(_longPassiveData);
        Object.DestroyImmediate(_essentialData);
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

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void TryMerge_SameIdAndLevel_IncreasesTargetLevel(int level)
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_mergeData) { Level = level };
        Inventory.ItemInstance target = new Inventory.ItemInstance(_mergeData) { Level = level };
        Inventory.ItemInstance bystander = new Inventory.ItemInstance(_mergeData) { Level = level };
        PutItem(source, ItemLocation.Inventory, _mergeSourceCell, _temporaryPosition);
        PutItem(target, ItemLocation.Inventory, _mergeTargetCell, _mergeTargetTemporaryPosition);

        Assert.IsTrue(_inventory.CanMerge(source, target));
        Assert.IsTrue(_inventory.TryMerge(source, target));

        Assert.AreEqual(level + 1, target.Level);
        Assert.AreEqual(ItemLocation.None, source.Location);
        // 레벨은 인스턴스에만 반영, 같은 데이터를 공유하는 다른 인스턴스·데이터 원본 불변
        Assert.AreEqual(level, bystander.Level);
        Assert.AreSame(_mergeData, target.Data);
        Assert.AreEqual(MERGE_ITEM_ID, _mergeData.Id);
        Assert.AreEqual(MERGE_MAX_LEVEL, _mergeData.MaxLevel);
    }

    [TestCase(ItemLocation.Inventory, ItemLocation.Inventory)]
    [TestCase(ItemLocation.Inventory, ItemLocation.Storage)]
    [TestCase(ItemLocation.Inventory, ItemLocation.Temporary)]
    [TestCase(ItemLocation.Storage, ItemLocation.Inventory)]
    [TestCase(ItemLocation.Storage, ItemLocation.Storage)]
    [TestCase(ItemLocation.Storage, ItemLocation.Temporary)]
    [TestCase(ItemLocation.Temporary, ItemLocation.Inventory)]
    [TestCase(ItemLocation.Temporary, ItemLocation.Storage)]
    [TestCase(ItemLocation.Temporary, ItemLocation.Temporary)]
    public void TryMerge_BetweenLocations_ReleasesSourceAndKeepsTarget(
        ItemLocation sourceLocation, ItemLocation targetLocation)
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_mergeData);
        Inventory.ItemInstance target = new Inventory.ItemInstance(_mergeData);
        PutItem(target, targetLocation, _mergeTargetCell, _mergeTargetTemporaryPosition);
        PutItem(source, sourceLocation, _mergeSourceCell, _temporaryPosition);

        Vector2Int targetPosition = target.Position;
        int targetRotation = target.Rotation;
        Vector2 targetTemporaryPosition = target.TemporaryPosition;
        int targetStorageIndex = _inventory.GetStorageIndex(target);
        int sourceStorageIndex = _inventory.GetStorageIndex(source);
        int expectedStorageCount = _inventory.StorageCount - (sourceLocation == ItemLocation.Storage ? 1 : 0);
        int expectedTemporaryCount = _inventory.TemporaryCount - (sourceLocation == ItemLocation.Temporary ? 1 : 0);
        int storageEventCount = 0;
        int temporaryEventCount = 0;
        _inventory.StorageCountChanged += () => storageEventCount++;
        _inventory.TemporaryCountChanged += () => temporaryEventCount++;

        Assert.IsTrue(_inventory.TryMerge(source, target));

        // 재료 해제
        Assert.AreEqual(ItemLocation.None, source.Location);
        Assert.AreEqual(-1, _inventory.GetStorageIndex(source));
        if (sourceLocation == ItemLocation.Inventory)
            AssertFree(_mergeSourceCell);
        if (sourceLocation == ItemLocation.Storage)
            Assert.IsNull(_inventory.GetStorageItem(sourceStorageIndex));

        // 결과 아이템 유지
        Assert.AreEqual(2, target.Level);
        Assert.AreEqual(targetLocation, target.Location);
        Assert.AreEqual(targetPosition, target.Position);
        Assert.AreEqual(targetRotation, target.Rotation);
        Assert.AreEqual(targetTemporaryPosition, target.TemporaryPosition);
        Assert.AreEqual(targetStorageIndex, _inventory.GetStorageIndex(target));
        if (targetLocation == ItemLocation.Inventory)
            AssertOccupied(_mergeTargetCell);

        // 카운트·개수 변화 이벤트는 재료 출발 영역 기준
        Assert.AreEqual(expectedStorageCount, _inventory.StorageCount);
        Assert.AreEqual(expectedTemporaryCount, _inventory.TemporaryCount);
        Assert.AreEqual(sourceLocation == ItemLocation.Storage ? 1 : 0, storageEventCount);
        Assert.AreEqual(sourceLocation == ItemLocation.Temporary ? 1 : 0, temporaryEventCount);
    }

    [Test]
    public void TryMerge_MaxLevel_FailsAndKeepsState()
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_mergeData) { Level = MERGE_MAX_LEVEL };
        Inventory.ItemInstance target = new Inventory.ItemInstance(_mergeData) { Level = MERGE_MAX_LEVEL };
        PutMergePair(source, target);

        AssertMergeFailsAndKeepsState(source, target);
    }

    [Test]
    public void TryMerge_DifferentId_FailsAndKeepsState()
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_mergeData);
        Inventory.ItemInstance target = new Inventory.ItemInstance(_otherMergeData);
        PutMergePair(source, target);

        AssertMergeFailsAndKeepsState(source, target);
    }

    [Test]
    public void TryMerge_DifferentLevel_FailsAndKeepsState()
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_mergeData) { Level = 1 };
        Inventory.ItemInstance target = new Inventory.ItemInstance(_mergeData) { Level = 2 };
        PutMergePair(source, target);

        AssertMergeFailsAndKeepsState(source, target);
    }

    [Test]
    public void TryMerge_Self_FailsAndKeepsState()
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_mergeData);
        PutItem(source, ItemLocation.Inventory, _mergeSourceCell, _temporaryPosition);

        AssertMergeFailsAndKeepsState(source, source);
    }

    // 진화 무기·고유 아이템처럼 MaxLevel 1인 데이터
    [Test]
    public void TryMerge_MaxLevelOne_FailsAndKeepsState()
    {
        Inventory.ItemInstance source = new Inventory.ItemInstance(_uniqueData);
        Inventory.ItemInstance target = new Inventory.ItemInstance(_uniqueData);
        PutMergePair(source, target);

        AssertMergeFailsAndKeepsState(source, target);
        Assert.AreEqual(UNIQUE_MAX_LEVEL, _uniqueData.MaxLevel);
    }

    [Test]
    public void TryMerge_LocationNone_FailsAndKeepsState()
    {
        Inventory.ItemInstance placed = new Inventory.ItemInstance(_mergeData);
        Inventory.ItemInstance unplaced = new Inventory.ItemInstance(_mergeData);
        PutItem(placed, ItemLocation.Inventory, _mergeSourceCell, _temporaryPosition);

        AssertMergeFailsAndKeepsState(unplaced, placed);
        AssertMergeFailsAndKeepsState(placed, unplaced);
    }

    [Test]
    public void TryMerge_NullItemOrData_Fails()
    {
        Inventory.ItemInstance placed = new Inventory.ItemInstance(_mergeData);
        Inventory.ItemInstance nullData = new Inventory.ItemInstance(_mergeData);
        PutMergePair(placed, nullData);
        // 배치 후 Data만 제거해 Location None이 아닌 Data null 검사만 실패 원인이 되도록 함
        nullData.Data = null;

        Assert.IsFalse(_inventory.TryMerge(null, placed));
        Assert.IsFalse(_inventory.TryMerge(placed, null));
        AssertMergeFailsAndKeepsState(nullData, placed);
        AssertMergeFailsAndKeepsState(placed, nullData);

        Assert.AreEqual(1, placed.Level);
        Assert.AreEqual(ItemLocation.Inventory, placed.Location);
        AssertOccupied(_mergeSourceCell);
    }

    [TestCase(1, 1.0f)]
    [TestCase(2, 1.1f)]
    [TestCase(3, 1.2f)]
    [TestCase(4, 1.3f)]
    [TestCase(5, 1.4f)]
    public void GetWeaponLevelMultiplier_ReturnsLinearMultiplier(int level, float expected)
    {
        Assert.AreEqual(expected, Inventory.GetWeaponLevelMultiplier(level), FLOAT_TOLERANCE);
    }

    [TestCase(1, 0.1f)]
    [TestCase(2, 0.2f)]
    [TestCase(3, 0.3f)]
    [TestCase(4, 0.4f)]
    [TestCase(5, 0.5f)]
    public void GetPassiveAdjacencyBonus_ReturnsLevelTimesTenPercent(int level, float expected)
    {
        Assert.AreEqual(expected, Inventory.GetPassiveAdjacencyBonus(level), FLOAT_TOLERANCE);
    }

    // 무기는 배치 여부와 무관하게 레벨로만 판정 (미배치 상태로 확인)
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    [TestCase(5, true)]
    public void IsLevel3EffectActive_Weapon_ActiveFromLevel3(int level, bool expected)
    {
        Inventory.ItemInstance weapon = new Inventory.ItemInstance(_adjacencyWeaponData) { Level = level };

        Assert.AreEqual(expected, _inventory.IsLevel3EffectActive(weapon));
    }

    // 인접 무기 없이 Inventory에 배치만 되어 있어도 활성
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    [TestCase(5, true)]
    public void IsLevel3EffectActive_PlacedPassive_ActiveFromLevel3(int level, bool expected)
    {
        Inventory.ItemInstance passive = PlaceAdjacencyItem(_passiveData, _adjacencyWeaponCell, level);

        Assert.AreEqual(expected, _inventory.IsLevel3EffectActive(passive));
    }

    [Test]
    public void IsLevel3EffectActive_PassiveInStorage_Inactive()
    {
        Inventory.ItemInstance passive = new Inventory.ItemInstance(_passiveData) { Level = 3 };
        Assert.IsTrue(_inventory.TryStore(passive));

        Assert.IsFalse(_inventory.IsLevel3EffectActive(passive));
    }

    [TestCase(0, 1)]
    [TestCase(0, -1)]
    [TestCase(-1, 0)]
    [TestCase(1, 0)]
    public void GetAdjacencyBonus_PassiveOrthogonal_Applies(int offsetX, int offsetY)
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        Inventory.ItemInstance passive = PlaceAdjacencyItem(_passiveData,
            _adjacencyWeaponCell + new Vector2Int(offsetX, offsetY), 1);

        Assert.IsTrue(_inventory.GetAdjacentItems(weapon).Contains(passive));
        Assert.AreEqual(0.1f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage), FLOAT_TOLERANCE);
        // 패시브 효과 종류와 다른 수치에는 반영되지 않음
        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.FireInterval));
    }

    [TestCase(1, 1)]
    [TestCase(1, -1)]
    [TestCase(-1, 1)]
    [TestCase(-1, -1)]
    public void GetAdjacencyBonus_PassiveDiagonal_NotApplied(int offsetX, int offsetY)
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        Inventory.ItemInstance passive = PlaceAdjacencyItem(_passiveData,
            _adjacencyWeaponCell + new Vector2Int(offsetX, offsetY), 1);

        Assert.IsFalse(_inventory.GetAdjacentItems(weapon).Contains(passive));
        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage));
    }

    // 세로 2칸 무기 (4,2),(4,3)과 세로 2칸 패시브 (5,2),(5,3)이 두 셀로 맞닿음
    [Test]
    public void GetAdjacencyBonus_SamePassiveTouchingMultipleCells_CountedOnce()
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_longWeaponData, _adjacencyWeaponCell, 1);
        PlaceAdjacencyItem(_longPassiveData, new Vector2Int(5, 2), 1);

        Assert.AreEqual(1, _inventory.GetAdjacentItems(weapon).Count);
        Assert.AreEqual(0.1f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage), FLOAT_TOLERANCE);
    }

    [Test]
    public void GetAdjacencyBonus_TwoPassiveInstances_Summed()
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 2), 1);
        PlaceAdjacencyItem(_passiveData, new Vector2Int(5, 2), 2);

        Assert.AreEqual(0.3f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage), FLOAT_TOLERANCE);
    }

    // 세로 무기 (3,1),(3,2)는 (3,3) 패시브와 맞닿고, 가로 회전 (3,1),(4,1)은 떨어짐
    [Test]
    public void GetAdjacencyBonus_AfterRotation_UsesRotatedCells()
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_longWeaponData, new Vector2Int(3, 1), 1);
        PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 3), 1);
        Assert.AreEqual(0.1f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage), FLOAT_TOLERANCE);

        Assert.IsTrue(_inventory.RotateItem(weapon));

        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage));
    }

    [TestCase(ItemLocation.Storage)]
    [TestCase(ItemLocation.Temporary)]
    public void GetAdjacencyBonus_PassiveMovedOutOfInventory_NotApplied(ItemLocation location)
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        Inventory.ItemInstance passive = PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 2), 1);
        Assert.AreEqual(0.1f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage), FLOAT_TOLERANCE);

        PutItem(passive, location, _mergeSourceCell, _temporaryPosition);

        Assert.IsFalse(_inventory.GetAdjacentItems(weapon).Contains(passive));
        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage));
    }

    [Test]
    public void GetAdjacencyBonus_PassiveSold_Released()
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        Inventory.ItemInstance passive = PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 2), 1);

        Assert.IsTrue(_inventory.TrySell(passive));

        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage));
    }

    // 고유 (3,2)가 무기 (4,2)에 주지 않고, 패시브 (3,3)도 고유에 주지 않음
    [Test]
    public void GetAdjacencyBonus_EssentialItem_NeitherGivesNorReceives()
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        Inventory.ItemInstance essential = PlaceAdjacencyItem(_essentialData, new Vector2Int(3, 2), 1);
        PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 3), 1);

        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage));
        Assert.AreEqual(0f, _inventory.GetAdjacencyBonus(essential, AdjacencyEffectType.Damage));
    }

    [Test]
    public void TryMerge_ReachLevel3_AppliesLevelBonusAndLevel3Effect()
    {
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 2);
        Inventory.ItemInstance passive = PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 2), 2);
        Inventory.ItemInstance weaponSource = new Inventory.ItemInstance(_adjacencyWeaponData) { Level = 2 };
        Inventory.ItemInstance passiveSource = new Inventory.ItemInstance(_passiveData) { Level = 2 };
        Assert.IsTrue(_inventory.TryStore(weaponSource));
        Assert.IsTrue(_inventory.TryStore(passiveSource));
        Assert.IsFalse(_inventory.IsLevel3EffectActive(weapon));
        Assert.IsFalse(_inventory.IsLevel3EffectActive(passive));

        Assert.IsTrue(_inventory.TryMerge(weaponSource, weapon));
        Assert.IsTrue(_inventory.TryMerge(passiveSource, passive));

        Assert.AreEqual(1.2f, Inventory.GetWeaponLevelMultiplier(weapon.Level), FLOAT_TOLERANCE);
        Assert.AreEqual(0.3f, _inventory.GetAdjacencyBonus(weapon, AdjacencyEffectType.Damage), FLOAT_TOLERANCE);
        Assert.IsTrue(_inventory.IsLevel3EffectActive(weapon));
        Assert.IsTrue(_inventory.IsLevel3EffectActive(passive));
    }

    // Lv.5 패시브 2개 = 합계 1.0
    [TestCase(AdjacencyEffectType.FireInterval, 0.5f)]
    [TestCase(AdjacencyEffectType.Spread, 0.7f)]
    [TestCase(AdjacencyEffectType.Damage, 1.0f)]
    public void GetEffectiveAdjacencyBonus_AppliesCapPerEffect(AdjacencyEffectType effectType, float expected)
    {
        SetAdjacencyEffectType(_passiveData, effectType);
        Inventory.ItemInstance weapon = PlaceAdjacencyItem(_adjacencyWeaponData, _adjacencyWeaponCell, 1);
        PlaceAdjacencyItem(_passiveData, new Vector2Int(3, 2), 5);
        PlaceAdjacencyItem(_passiveData, new Vector2Int(5, 2), 5);

        Assert.AreEqual(1.0f, _inventory.GetAdjacencyBonus(weapon, effectType), FLOAT_TOLERANCE);
        Assert.AreEqual(expected, _inventory.GetEffectiveAdjacencyBonus(weapon, effectType), FLOAT_TOLERANCE);
    }

    private Inventory.ItemInstance PlaceAdjacencyItem(InventoryData data, Vector2Int cell, int level)
    {
        Inventory.ItemInstance item = new Inventory.ItemInstance(data) { Level = level };
        Assert.IsTrue(_inventory.PlaceItem(item, cell));
        return item;
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

    // 합성 판정에 Id·MaxLevel이 필요해 기존 CreateData(id 0, maxLevel 1)와 분리, Shape는 1칸
    private static InventoryData CreateMergeData(int id, string itemName, int maxLevel)
    {
        InventoryData data = ScriptableObject.CreateInstance<InventoryData>();
        data.SetData(id, itemName, null, ItemType.Weapon, string.Empty, new[] { new Vector2Int(0, 0) }, maxLevel, null);
        return data;
    }

    // 인접 판정에 종류·효과·Shape가 필요하고, 효과 종류는 SetData에 없어 직렬화 필드로 설정
    private static InventoryData CreateAdjacencyData(int id, ItemType itemType, AdjacencyEffectType effectType,
        Vector2Int[] shape)
    {
        InventoryData data = ScriptableObject.CreateInstance<InventoryData>();
        data.SetData(id, string.Empty, null, itemType, string.Empty, shape, MERGE_MAX_LEVEL, null);
        SetAdjacencyEffectType(data, effectType);
        return data;
    }

    private static void SetAdjacencyEffectType(InventoryData data, AdjacencyEffectType effectType)
    {
        SerializedObject serializedData = new SerializedObject(data);
        serializedData.FindProperty(ADJACENCY_EFFECT_TYPE_FIELD).enumValueIndex = (int)effectType;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
    }

    // None은 배치하지 않음, Inventory·Temporary는 MERGE_ROTATION으로 배치
    private void PutItem(Inventory.ItemInstance item, ItemLocation location, Vector2Int cell, Vector2 temporaryPosition)
    {
        switch (location)
        {
            case ItemLocation.Inventory:
                Assert.IsTrue(_inventory.TryPlace(item, cell, MERGE_ROTATION));
                break;

            case ItemLocation.Storage:
                Assert.IsTrue(_inventory.TryStore(item));
                break;

            case ItemLocation.Temporary:
                _inventory.MoveToTemporary(item, temporaryPosition, MERGE_ROTATION);
                break;
        }
    }

    // 실패 검증용 기본 배치 : 재료 Inventory, 대상 Storage
    private void PutMergePair(Inventory.ItemInstance source, Inventory.ItemInstance target)
    {
        PutItem(source, ItemLocation.Inventory, _mergeSourceCell, _temporaryPosition);
        PutItem(target, ItemLocation.Storage, _mergeTargetCell, _mergeTargetTemporaryPosition);
    }

    private void AssertMergeFailsAndKeepsState(Inventory.ItemInstance source, Inventory.ItemInstance target)
    {
        int sourceLevel = source.Level;
        int targetLevel = target.Level;
        ItemLocation sourceLocation = source.Location;
        ItemLocation targetLocation = target.Location;
        int storageCount = _inventory.StorageCount;
        int temporaryCount = _inventory.TemporaryCount;
        int eventCount = 0;
        _inventory.StorageCountChanged += () => eventCount++;
        _inventory.TemporaryCountChanged += () => eventCount++;

        Assert.IsFalse(_inventory.CanMerge(source, target));
        Assert.IsFalse(_inventory.TryMerge(source, target));

        Assert.AreEqual(sourceLevel, source.Level);
        Assert.AreEqual(targetLevel, target.Level);
        Assert.AreEqual(sourceLocation, source.Location);
        Assert.AreEqual(targetLocation, target.Location);
        Assert.AreEqual(storageCount, _inventory.StorageCount);
        Assert.AreEqual(temporaryCount, _inventory.TemporaryCount);
        Assert.AreEqual(0, eventCount);

        // 1칸 Shape라 Position이 곧 점유 셀
        if (source.IsPlaced)
            AssertOccupied(source.Position);
        if (target.IsPlaced)
            AssertOccupied(target.Position);
    }

    // SetData 시그니처를 바꾸지 않도록 직렬화 필드로 설정
    private static void SetSellable(InventoryData data, bool isSellable)
    {
        SerializedObject serializedData = new SerializedObject(data);
        serializedData.FindProperty(IS_SELLABLE_FIELD).boolValue = isSellable;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
    }
}
