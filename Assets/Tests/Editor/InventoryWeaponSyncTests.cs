using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Inventory.Changed 조작별 1회·실패 시 미발생, GetWeaponStats 레벨·인접·상한·미배치·null,
/// InventoryWeaponSync 배치 장착·이동/회전 시 같은 WeaponInstance 유지·Storage/Temporary/판매/합성 소멸 회수·재배치 재장착·합성 대상 레벨 갱신 검증
/// </summary>
public class InventoryWeaponSyncTests
{
    private const string AWAKE_METHOD = "Awake";
    private const string ADJACENCY_EFFECT_TYPE_FIELD = "_adjacencyEffectType";
    private const string START_WEAPON_FIELD = "_startWeapon";
    private const string DEFAULT_CHARACTER_FIELD = "_defaultCharacter";
    private const string SYNC_INVENTORY_FIELD = "_inventory";
    private const string SYNC_LOADOUT_FIELD = "_loadout";
    private const float FLOAT_TOLERANCE = 0.0001f;

    private const int WEAPON_ID = 1;
    private const int PASSIVE_ID = 2;
    private const int MAX_LEVEL = 5;
    private const float BASE_DAMAGE = 10f;
    private const float BASE_FIRE_INTERVAL = 1f;
    private const float BASE_SPREAD_ANGLE = 20f;
    private const float LEVEL3_SPREAD_REDUCTION = 0.2f;
    private const string LEVEL3_SPREAD_REDUCTION_FIELD = "_level3SpreadReduction";
    private const string LEVEL3_GUN_COUNT_FIELD = "_level3GunCount";

    private static readonly Vector2Int[] _singleCellShape = { new Vector2Int(0, 0) };

    // 무기를 활성 영역 안쪽 (4,2)에 두어 상하좌우가 모두 활성 셀
    private static readonly Vector2Int _weaponCell = new Vector2Int(4, 2);
    private static readonly Vector2Int _otherWeaponCell = new Vector2Int(6, 4);
    private static readonly Vector2Int _lockedCell = new Vector2Int(0, 0);

    private static readonly Vector2Int[] _neighborCells =
    {
        new Vector2Int(4, 1), new Vector2Int(4, 3), new Vector2Int(3, 2), new Vector2Int(5, 2),
    };

    private readonly List<Object> _createdObjects = new List<Object>();

    private Inventory _inventory;
    private WeaponLoadout _loadout;
    private WeaponDefinition _weaponDefinition;
    private InventoryData _weaponData;
    private int _changedCount;

    [SetUp]
    public void SetUp()
    {
        _weaponDefinition = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
        SerializedObject serializedDefinition = new SerializedObject(_weaponDefinition);
        serializedDefinition.FindProperty("_projectileDamage").floatValue = BASE_DAMAGE;
        serializedDefinition.FindProperty("_fireInterval").floatValue = BASE_FIRE_INTERVAL;
        serializedDefinition.FindProperty("_spreadAngle").floatValue = BASE_SPREAD_ANGLE;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

        _weaponData = Track(ScriptableObject.CreateInstance<InventoryData>());
        _weaponData.SetData(WEAPON_ID, "Weapon", null, ItemType.Weapon, string.Empty, _singleCellShape, MAX_LEVEL,
            _weaponDefinition);

        // EditMode에서는 AddComponent 시 Awake가 호출되지 않아 리플렉션으로 그리드 초기화
        GameObject inventoryObject = Track(new GameObject());
        _inventory = inventoryObject.AddComponent<Inventory>();
        typeof(Inventory)
            .GetMethod(AWAKE_METHOD, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_inventory, null);
        _inventory.Changed += () => _changedCount++;

        // 주무기 해제 시 재선정이 캐릭터의 고유 무기를 읽으므로 기본 캐릭터 주입
        CharacterSelection.Selected = null;
        CharacterDefinition character = Track(ScriptableObject.CreateInstance<CharacterDefinition>());
        SerializedObject serializedCharacter = new SerializedObject(character);
        serializedCharacter.FindProperty(START_WEAPON_FIELD).objectReferenceValue = _weaponDefinition;
        serializedCharacter.ApplyModifiedPropertiesWithoutUndo();

        GameObject loadoutObject = Track(new GameObject());
        _loadout = loadoutObject.AddComponent<WeaponLoadout>();
        SerializedObject serializedLoadout = new SerializedObject(_loadout);
        serializedLoadout.FindProperty(DEFAULT_CHARACTER_FIELD).objectReferenceValue = character;
        serializedLoadout.ApplyModifiedPropertiesWithoutUndo();

        // EditMode에서는 OnEnable이 호출되지 않아 Changed 구독을 직접 연결 (Start의 시작 아이템 지급은 UI가 필요해 제외)
        GameObject syncObject = Track(new GameObject());
        InventoryWeaponSync sync = syncObject.AddComponent<InventoryWeaponSync>();
        SerializedObject serializedSync = new SerializedObject(sync);
        serializedSync.FindProperty(SYNC_INVENTORY_FIELD).objectReferenceValue = _inventory;
        serializedSync.FindProperty(SYNC_LOADOUT_FIELD).objectReferenceValue = _loadout;
        serializedSync.ApplyModifiedPropertiesWithoutUndo();
        _inventory.Changed += sync.SyncWeapons;

        _changedCount = 0;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in _createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        _createdObjects.Clear();
    }

    // --- Inventory.Changed ------------------------------------

    [Test]
    public void Changed_PlaceItem_RaisedOnce_FailNotRaised()
    {
        Assert.IsFalse(_inventory.PlaceItem(CreateWeapon(), _lockedCell));
        Assert.AreEqual(0, _changedCount);

        Assert.IsTrue(_inventory.PlaceItem(CreateWeapon(), _weaponCell));
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_MoveItem_RaisedOnce_FailNotRaised()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);

        Assert.IsFalse(_inventory.MoveItem(weapon, _lockedCell));
        Assert.AreEqual(0, _changedCount);

        Assert.IsTrue(_inventory.MoveItem(weapon, _otherWeaponCell));
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_RotateItem_InventoryAndTemporaryOnce_StorageNotRaised()
    {
        Inventory.ItemInstance placed = PlaceWeapon(_weaponCell);
        Inventory.ItemInstance temporary = CreateWeapon();
        _inventory.MoveToTemporary(temporary, Vector2.zero, 0);
        Inventory.ItemInstance stored = CreateWeapon();
        _inventory.TryStore(stored);
        _changedCount = 0;

        Assert.IsFalse(_inventory.RotateItem(stored));
        Assert.AreEqual(0, _changedCount);

        Assert.IsTrue(_inventory.RotateItem(placed));
        Assert.AreEqual(1, _changedCount);

        Assert.IsTrue(_inventory.RotateItem(temporary));
        Assert.AreEqual(2, _changedCount);
    }

    [Test]
    public void Changed_TryPlace_RaisedOnce_FailNotRaised()
    {
        Inventory.ItemInstance weapon = CreateWeapon();
        _inventory.MoveToTemporary(weapon, Vector2.zero, 0);
        _changedCount = 0;

        Assert.IsFalse(_inventory.TryPlace(weapon, _lockedCell, 0));
        Assert.AreEqual(0, _changedCount);

        Assert.IsTrue(_inventory.TryPlace(weapon, _weaponCell, 0));
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_TryStore_MoveOnce_AlreadyStoredNotRaised()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);

        Assert.IsTrue(_inventory.TryStore(weapon));
        Assert.AreEqual(1, _changedCount);

        Assert.IsTrue(_inventory.TryStore(weapon));
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_MoveToTemporary_RaisedOnce()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);

        _inventory.MoveToTemporary(weapon, Vector2.zero, 0);

        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_TrySell_RaisedOnce_FailNotRaised()
    {
        Assert.IsFalse(_inventory.TrySell(CreateWeapon()));
        Assert.AreEqual(0, _changedCount);

        Assert.IsTrue(_inventory.TrySell(PlaceWeapon(_weaponCell)));
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_TryMerge_RaisedOnce_FailNotRaised()
    {
        Inventory.ItemInstance target = PlaceWeapon(_weaponCell);
        Inventory.ItemInstance source = PlaceWeapon(_otherWeaponCell);
        Inventory.ItemInstance higherLevel = CreateWeapon();
        higherLevel.Level = MAX_LEVEL;
        _inventory.MoveToTemporary(higherLevel, Vector2.zero, 0);
        _changedCount = 0;

        Assert.IsFalse(_inventory.TryMerge(higherLevel, target));
        Assert.AreEqual(0, _changedCount);

        Assert.IsTrue(_inventory.TryMerge(source, target));
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_RemoveItem_RaisedOnce_NotPlacedNotRaised()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);

        _inventory.RemoveItem(weapon);
        Assert.AreEqual(1, _changedCount);

        _inventory.RemoveItem(weapon);
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void Changed_Queries_NotRaised()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        Inventory.ItemInstance other = PlaceWeapon(_otherWeaponCell);

        _inventory.CanPlace(weapon, _weaponCell, 0);
        _inventory.CanMerge(other, weapon);
        _inventory.CanSell(weapon);
        _inventory.GetWeaponStats(weapon);
        _inventory.GetPlacedItems();

        Assert.AreEqual(0, _changedCount);
    }

    // --- GetWeaponStats ------------------------------------

    [Test]
    public void GetWeaponStats_UnplacedLevel1_ReturnsDefinitionValues()
    {
        Inventory.WeaponStats stats = _inventory.GetWeaponStats(CreateWeapon());

        AssertStats(stats, BASE_DAMAGE, BASE_FIRE_INTERVAL, BASE_SPREAD_ANGLE);
    }

    // 미배치 아이템은 옆 칸에 패시브가 있어도 인접 효과 0
    [Test]
    public void GetWeaponStats_UnplacedLevel3_AppliesOnlyLevelMultiplier()
    {
        PlacePassive(AdjacencyEffectType.Damage, _neighborCells[0], 1);
        Inventory.ItemInstance weapon = CreateWeapon();
        weapon.Level = 3;

        AssertStats(_inventory.GetWeaponStats(weapon), 12f, BASE_FIRE_INTERVAL, BASE_SPREAD_ANGLE);
    }

    [Test]
    public void GetWeaponStats_AdjacentDamage_NoCap()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        weapon.Level = 2;
        PlacePassive(AdjacencyEffectType.Damage, _neighborCells[0], MAX_LEVEL);
        PlacePassive(AdjacencyEffectType.Damage, _neighborCells[1], MAX_LEVEL);

        // 10 × 1.1 × (1 + 1.0)
        AssertStats(_inventory.GetWeaponStats(weapon), 22f, BASE_FIRE_INTERVAL, BASE_SPREAD_ANGLE);
    }

    [Test]
    public void GetWeaponStats_AdjacentFireInterval_CappedAtHalf()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        PlacePassive(AdjacencyEffectType.FireInterval, _neighborCells[0], 2);
        PlacePassive(AdjacencyEffectType.FireInterval, _neighborCells[1], 2);
        PlacePassive(AdjacencyEffectType.FireInterval, _neighborCells[2], 2);

        // 합 0.6 → 상한 0.5
        AssertStats(_inventory.GetWeaponStats(weapon), BASE_DAMAGE, 0.5f, BASE_SPREAD_ANGLE);
    }

    [Test]
    public void GetWeaponStats_AdjacentSpread_CappedAt70Percent()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        PlacePassive(AdjacencyEffectType.Spread, _neighborCells[0], MAX_LEVEL);
        PlacePassive(AdjacencyEffectType.Spread, _neighborCells[1], MAX_LEVEL);

        // 합 1.0 → 상한 0.7, 20 × 0.3
        AssertStats(_inventory.GetWeaponStats(weapon), BASE_DAMAGE, BASE_FIRE_INTERVAL, 6f);
    }

    [Test]
    public void GetWeaponStats_Level3SpreadReduction_AppliedFromLevel3()
    {
        SetLevel3SpreadReduction(LEVEL3_SPREAD_REDUCTION);
        Inventory.ItemInstance weapon = CreateWeapon();
        weapon.Level = 2;
        Assert.AreEqual(BASE_SPREAD_ANGLE, _inventory.GetWeaponStats(weapon).SpreadAngle, FLOAT_TOLERANCE);

        weapon.Level = 3;

        // 20 × (1 - 0.2)
        Assert.AreEqual(16f, _inventory.GetWeaponStats(weapon).SpreadAngle, FLOAT_TOLERANCE);
    }

    [Test]
    public void GetWeaponStats_Level3SpreadReductionWithAdjacent_CappedAt70Percent()
    {
        SetLevel3SpreadReduction(LEVEL3_SPREAD_REDUCTION);
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        weapon.Level = 3;
        PlacePassive(AdjacencyEffectType.Spread, _neighborCells[0], MAX_LEVEL);
        PlacePassive(AdjacencyEffectType.Spread, _neighborCells[1], 2);

        // 인접 0.5 + 0.2 + Lv.3 0.2 = 0.9 → 상한 0.7, 20 × 0.3
        Assert.AreEqual(6f, _inventory.GetWeaponStats(weapon).SpreadAngle, FLOAT_TOLERANCE);
    }

    [Test]
    public void GetWeaponStats_Level3GunCount_AppliedFromLevel3()
    {
        SerializedObject serializedDefinition = new SerializedObject(_weaponDefinition);
        serializedDefinition.FindProperty(LEVEL3_GUN_COUNT_FIELD).intValue = 2;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        Inventory.ItemInstance weapon = CreateWeapon();
        weapon.Level = 2;
        Assert.AreEqual(1, _inventory.GetWeaponStats(weapon).GunCount);

        weapon.Level = 3;
        Assert.AreEqual(2, _inventory.GetWeaponStats(weapon).GunCount);
    }

    [Test]
    public void GetWeaponStats_DefaultLevel3GunCount_StaysOneAtLevel3()
    {
        Inventory.ItemInstance weapon = CreateWeapon();
        weapon.Level = 3;

        Assert.AreEqual(1, _inventory.GetWeaponStats(weapon).GunCount);
    }

    [Test]
    public void GetWeaponStats_NullOrNoDefinition_ReturnsZero()
    {
        InventoryData passiveData = CreatePassiveData(AdjacencyEffectType.Damage);

        AssertStats(_inventory.GetWeaponStats(null), 0f, 0f, 0f);
        AssertStats(_inventory.GetWeaponStats(new Inventory.ItemInstance(null)), 0f, 0f, 0f);
        AssertStats(_inventory.GetWeaponStats(new Inventory.ItemInstance(passiveData)), 0f, 0f, 0f);
    }

    // --- InventoryWeaponSync ------------------------------------

    [Test]
    public void Sync_PlaceWeapon_EquipsWithLevelAndStats()
    {
        Inventory.ItemInstance weapon = CreateWeapon();
        weapon.Level = 2;

        _inventory.PlaceItem(weapon, _weaponCell);

        Assert.AreEqual(1, _loadout.Weapons.Count);
        WeaponInstance equipped = _loadout.Weapons[0];
        Assert.AreSame(_weaponDefinition, equipped.Definition);
        Assert.AreEqual(2, equipped.Level);
        Assert.AreEqual(11f, equipped.Damage, FLOAT_TOLERANCE);
        Assert.AreSame(equipped, _loadout.Primary);
    }

    [Test]
    public void Sync_PlacePassive_NotEquipped()
    {
        PlacePassive(AdjacencyEffectType.Damage, _weaponCell, 1);

        Assert.AreEqual(0, _loadout.Weapons.Count);
    }

    [Test]
    public void Sync_MoveAndRotate_KeepsSameWeaponInstance()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        WeaponInstance equipped = _loadout.Weapons[0];

        _inventory.MoveItem(weapon, _otherWeaponCell);
        _inventory.RotateItem(weapon);
        _inventory.TryPlace(weapon, _weaponCell, 0);

        Assert.AreEqual(1, _loadout.Weapons.Count);
        Assert.AreSame(equipped, _loadout.Weapons[0]);
    }

    [Test]
    public void Sync_AdjacentPassiveChanged_UpdatesStatsOnSameInstance()
    {
        PlaceWeapon(_weaponCell);
        WeaponInstance equipped = _loadout.Weapons[0];

        Inventory.ItemInstance passive = PlacePassive(AdjacencyEffectType.Damage, _neighborCells[0], 1);
        Assert.AreSame(equipped, _loadout.Weapons[0]);
        Assert.AreEqual(11f, equipped.Damage, FLOAT_TOLERANCE);

        _inventory.TryStore(passive);
        Assert.AreEqual(BASE_DAMAGE, equipped.Damage, FLOAT_TOLERANCE);
    }

    [Test]
    public void Sync_StoreThenPlaceAgain_UnequipsThenEquipsNewInstance()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);
        WeaponInstance first = _loadout.Weapons[0];

        _inventory.TryStore(weapon);
        Assert.AreEqual(0, _loadout.Weapons.Count);
        Assert.IsNull(_loadout.Primary);

        _inventory.TryPlace(weapon, _weaponCell, 0);
        Assert.AreEqual(1, _loadout.Weapons.Count);
        Assert.AreNotSame(first, _loadout.Weapons[0]);
    }

    [Test]
    public void Sync_MoveToTemporary_Unequips()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);

        _inventory.MoveToTemporary(weapon, Vector2.zero, 0);

        Assert.AreEqual(0, _loadout.Weapons.Count);
    }

    [Test]
    public void Sync_Sell_Unequips()
    {
        Inventory.ItemInstance weapon = PlaceWeapon(_weaponCell);

        _inventory.TrySell(weapon);

        Assert.AreEqual(0, _loadout.Weapons.Count);
    }

    [Test]
    public void Sync_Merge_UnequipsSourceAndUpdatesTargetLevel()
    {
        Inventory.ItemInstance target = PlaceWeapon(_weaponCell);
        WeaponInstance targetWeapon = _loadout.Weapons[0];
        Inventory.ItemInstance source = PlaceWeapon(_otherWeaponCell);

        _inventory.TryMerge(source, target);

        Assert.AreEqual(1, _loadout.Weapons.Count);
        Assert.AreSame(targetWeapon, _loadout.Weapons[0]);
        Assert.AreEqual(2, targetWeapon.Level);
        Assert.AreEqual(11f, targetWeapon.Damage, FLOAT_TOLERANCE);
    }

    // --- 헬퍼 ------------------------------------

    private T Track<T>(T createdObject) where T : Object
    {
        _createdObjects.Add(createdObject);
        return createdObject;
    }

    private Inventory.ItemInstance CreateWeapon()
    {
        return new Inventory.ItemInstance(_weaponData);
    }

    private Inventory.ItemInstance PlaceWeapon(Vector2Int cell)
    {
        Inventory.ItemInstance weapon = CreateWeapon();
        Assert.IsTrue(_inventory.PlaceItem(weapon, cell));
        _changedCount = 0;
        return weapon;
    }

    // 효과 종류는 SetData에 없어 직렬화 필드로 설정
    private InventoryData CreatePassiveData(AdjacencyEffectType effectType)
    {
        InventoryData data = Track(ScriptableObject.CreateInstance<InventoryData>());
        data.SetData(PASSIVE_ID, "Passive", null, ItemType.Passive, string.Empty, _singleCellShape, MAX_LEVEL, null);
        SerializedObject serializedData = new SerializedObject(data);
        serializedData.FindProperty(ADJACENCY_EFFECT_TYPE_FIELD).enumValueIndex = (int)effectType;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private void SetLevel3SpreadReduction(float reduction)
    {
        SerializedObject serializedDefinition = new SerializedObject(_weaponDefinition);
        serializedDefinition.FindProperty(LEVEL3_SPREAD_REDUCTION_FIELD).floatValue = reduction;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
    }

    private Inventory.ItemInstance PlacePassive(AdjacencyEffectType effectType, Vector2Int cell, int level)
    {
        Inventory.ItemInstance passive = new Inventory.ItemInstance(CreatePassiveData(effectType)) { Level = level };
        Assert.IsTrue(_inventory.PlaceItem(passive, cell));
        _changedCount = 0;
        return passive;
    }

    private static void AssertStats(Inventory.WeaponStats stats, float damage, float fireInterval, float spreadAngle)
    {
        Assert.AreEqual(damage, stats.Damage, FLOAT_TOLERANCE);
        Assert.AreEqual(fireInterval, stats.FireInterval, FLOAT_TOLERANCE);
        Assert.AreEqual(spreadAngle, stats.SpreadAngle, FLOAT_TOLERANCE);
    }
}
