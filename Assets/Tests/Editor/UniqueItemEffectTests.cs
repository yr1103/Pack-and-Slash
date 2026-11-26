using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 캐릭터 특성·고유 아이템 장착·해제 무기 스탯(대상 무기만·현재 캐릭터만·반복 토글 누적 없음), 조끼 전용 칸·보관함 한도·해제 거부,
/// 패시브 Lv.3 효과 합산, PlayerStatSync 이동속도 상한·최대 체력 정책·경험치 소수 누적·수집 반경 배율 검증
/// </summary>
public class UniqueItemEffectTests
{
    private const string AWAKE_METHOD = "Awake";
    private const float FLOAT_TOLERANCE = 0.0001f;
    private const int MAX_LEVEL = 5;
    private const float BASE_DAMAGE = 10f;
    private const float BASE_FIRE_INTERVAL = 1f;
    private const float BASE_SPREAD_ANGLE = 20f;
    private const float BASE_MOVE_SPEED = 5f; // PlayerMovement 기본값
    private const float BASE_MAX_HEALTH = 100f; // PlayerHealth 기본값
    private const float BASE_COLLECT_RADIUS = 0.4f; // PlayerExperience 기본값

    private static readonly Vector2Int[] _singleCellShape = { new Vector2Int(0, 0) };
    private static readonly Vector2Int _weaponCell = new Vector2Int(4, 2);
    private static readonly Vector2Int _uniqueCell = new Vector2Int(6, 4);
    private static readonly Vector2Int _adjacentCell = new Vector2Int(4, 1);
    private static readonly Vector2Int _otherAdjacentCell = new Vector2Int(5, 2);

    // 조끼 장착 중에만 열리는 전용 칸 (Inventory._vestCells와 같은 좌표)
    private static readonly Vector2Int[] _vestCells =
    {
        new Vector2Int(4, 5), new Vector2Int(5, 5),
    };

    // 무기·고유 아이템 칸과 겹치지 않는 패시브 배치 칸
    private static readonly Vector2Int[] _passiveCells =
    {
        new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4),
    };

    private readonly List<Object> _createdObjects = new List<Object>();

    private Inventory _inventory;
    private InventoryData _pistolData;
    private InventoryData _minigunData;
    private InventoryData _shotgunData;
    private InventoryData _hatData;
    private InventoryData _shakeData;
    private InventoryData _vestData;
    private CharacterDefinition _cowboy;
    private CharacterDefinition _heavyGunner;
    private CharacterDefinition _survivor;

    [SetUp]
    public void SetUp()
    {
        _pistolData = CreateWeaponData(1, WeaponType.Pistol);
        _minigunData = CreateWeaponData(2, WeaponType.Minigun);
        _shotgunData = CreateWeaponData(3, WeaponType.Shotgun);
        _hatData = CreateItemData(10, ItemType.Essential);
        _shakeData = CreateItemData(11, ItemType.Essential);
        _vestData = CreateItemData(12, ItemType.Essential);

        _cowboy = CreateCharacter(WeaponType.Pistol, _hatData,
            ("_traitDamageBonus", 0.1f), ("_unequippedAttackSpeedBonus", 0.6f), ("_unequippedSpreadAngle", 70f));
        _heavyGunner = CreateCharacter(WeaponType.Minigun, _shakeData,
            ("_traitAccuracyBonus", 0.1f), ("_traitMoveSpeedBonus", -0.1f), ("_equippedAccuracyBonus", 0.1f),
            ("_unequippedMoveSpeedBonus", -0.2f));
        _survivor = CreateCharacter(WeaponType.Shotgun, _vestData,
            ("_traitDamageBonus", 0.05f), ("_traitProjectileBonus", 1f), ("_equippedUnlocksVestCells", 1f),
            ("_equippedStorageBonus", 2f), ("_unequippedMoveSpeedBonus", 0.1f), ("_unequippedMaxHealthBonus", -0.6f));

        // EditMode에서는 AddComponent 시 Awake가 호출되지 않아 리플렉션으로 초기화
        _inventory = Track(new GameObject()).AddComponent<Inventory>();
        InvokeAwake(_inventory);
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

    // --- 고유 효과 무기 스탯 ------------------------------------

    [Test]
    public void Cowboy_HatEquipped_TraitDamageOnly()
    {
        _inventory.Character = _cowboy;
        Place(_hatData, _uniqueCell, 1);

        AssertStats(_inventory.GetWeaponStats(Place(_pistolData, _weaponCell, 1)), 11f, BASE_FIRE_INTERVAL,
            BASE_SPREAD_ANGLE, false);
    }

    [Test]
    public void Cowboy_HatUnequipped_TraitDamageAttackSpeedAndRandomSpread()
    {
        _inventory.Character = _cowboy;
        Inventory.ItemInstance hat = Place(_hatData, _uniqueCell, 1);
        _inventory.TryStore(hat);

        // 특성 공격력 유지, 간격 1 ÷ 1.6, 확산각 70° 랜덤
        AssertStats(_inventory.GetWeaponStats(Place(_pistolData, _weaponCell, 1)), 11f, 0.625f, 70f, true);
    }

    [Test]
    public void Cowboy_RepeatToggle_NoAccumulation()
    {
        _inventory.Character = _cowboy;
        Inventory.ItemInstance hat = Place(_hatData, _uniqueCell, 1);
        Inventory.ItemInstance pistol = Place(_pistolData, _weaponCell, 1);

        for (int i = 0; i < 3; i++)
        {
            _inventory.TryStore(hat);
            AssertStats(_inventory.GetWeaponStats(pistol), 11f, 0.625f, 70f, true);

            _inventory.TryPlace(hat, _uniqueCell, 0);
            AssertStats(_inventory.GetWeaponStats(pistol), 11f, BASE_FIRE_INTERVAL, BASE_SPREAD_ANGLE, false);
        }
    }

    [Test]
    public void Cowboy_NonTargetWeapon_NotApplied()
    {
        _inventory.Character = _cowboy;

        AssertStats(_inventory.GetWeaponStats(Place(_minigunData, _weaponCell, 1)), BASE_DAMAGE, BASE_FIRE_INTERVAL,
            BASE_SPREAD_ANGLE, false);
    }

    // 헤비거너 캐릭터에서 카우보이 모자가 해제 상태여도 권총에 카우보이 효과 없음
    [Test]
    public void OtherCharacter_UniqueEffectNotApplied()
    {
        _inventory.Character = _heavyGunner;
        Place(_shakeData, _uniqueCell, 1);

        AssertStats(_inventory.GetWeaponStats(Place(_pistolData, _weaponCell, 1)), BASE_DAMAGE, BASE_FIRE_INTERVAL,
            BASE_SPREAD_ANGLE, false);
    }

    [Test]
    public void NullCharacter_NoUniqueEffect()
    {
        Assert.IsFalse(_inventory.IsUniqueItemEquipped());
        AssertStats(_inventory.GetWeaponStats(Place(_pistolData, _weaponCell, 1)), BASE_DAMAGE, BASE_FIRE_INTERVAL,
            BASE_SPREAD_ANGLE, false);
    }

    [Test]
    public void HeavyGunner_AccuracyEquippedAndUnequipped()
    {
        _inventory.Character = _heavyGunner;
        Inventory.ItemInstance shake = Place(_shakeData, _uniqueCell, 1);
        Inventory.ItemInstance minigun = Place(_minigunData, _weaponCell, 1);

        // 특성 0.1 + 장착 0.1 → 20 × 0.8
        Assert.AreEqual(16f, _inventory.GetWeaponStats(minigun).SpreadAngle, FLOAT_TOLERANCE);

        // 특성 0.1만 → 20 × 0.9
        _inventory.TryStore(shake);
        Assert.AreEqual(18f, _inventory.GetWeaponStats(minigun).SpreadAngle, FLOAT_TOLERANCE);
    }

    [Test]
    public void HeavyGunner_AccuracyPlusAdjacentSpread_CappedAt70Percent()
    {
        _inventory.Character = _heavyGunner;
        Inventory.ItemInstance minigun = Place(_minigunData, _weaponCell, 1);
        InventoryData coolerData = CreatePassiveData(AdjacencyEffectType.Spread, Level3EffectType.None, 0f);
        Place(coolerData, _adjacentCell, MAX_LEVEL);
        Place(coolerData, _otherAdjacentCell, MAX_LEVEL);

        // 인접 0.5 × 2 + 특성 0.1 = 1.1 → 상한 0.7, 20 × 0.3
        Assert.AreEqual(6f, _inventory.GetWeaponStats(minigun).SpreadAngle, FLOAT_TOLERANCE);
    }

    // --- 겁쟁이 특성·조끼 ------------------------------------

    [Test]
    public void Survivor_Trait_ShotgunDamageAndExtraProjectile()
    {
        _inventory.Character = _survivor;
        Inventory.WeaponStats stats = _inventory.GetWeaponStats(Place(_shotgunData, _weaponCell, 1));

        // 조끼 해제 중에도 특성 적용, 10 × 1.05
        Assert.AreEqual(10.5f, stats.Damage, FLOAT_TOLERANCE);
        Assert.AreEqual(_shotgunData.WeaponDefinition.ProjectilesPerShot + 1, stats.ProjectilesPerShot);
    }

    [Test]
    public void Survivor_VestEquipped_VestCellsOpenStorage7_UnequippedLockedStorage5()
    {
        _inventory.Character = _survivor;
        Inventory.ItemInstance vest = Place(_vestData, _uniqueCell, 1);

        AssertVestCellsAvailable(true);
        Assert.AreEqual(7, _inventory.StorageCapacity);

        Assert.IsTrue(_inventory.TryStore(vest));
        AssertVestCellsAvailable(false);
        Assert.AreEqual(5, _inventory.StorageCapacity);
    }

    [Test]
    public void Survivor_VestCellOccupied_ReleaseRejectedNoChange()
    {
        _inventory.Character = _survivor;
        Inventory.ItemInstance vest = Place(_vestData, _uniqueCell, 1);
        Place(_pistolData, _vestCells[0], 1);

        Assert.IsFalse(_inventory.TryStore(vest));
        Assert.IsFalse(_inventory.RemoveItem(vest));
        Assert.IsFalse(_inventory.MoveToTemporary(vest, Vector2.zero, 0));
        Assert.IsFalse(_inventory.TrySell(vest));
        AssertVestReleaseUnchanged(vest, 0);
    }

    [Test]
    public void Survivor_ExtraStorageSlotOccupied_ReleaseRejectedNoChange()
    {
        _inventory.Character = _survivor;
        Inventory.ItemInstance vest = Place(_vestData, _uniqueCell, 1);

        // 6번째 슬롯까지 채움 (기본 한도 5 초과)
        for (int i = 0; i < 6; i++)
        {
            Assert.IsTrue(_inventory.TryStore(new Inventory.ItemInstance(_pistolData)));
        }

        Assert.IsFalse(_inventory.TryStore(vest));
        Assert.IsFalse(_inventory.RemoveItem(vest));
        Assert.IsFalse(_inventory.TrySell(vest));
        AssertVestReleaseUnchanged(vest, 6);
    }

    // 합성 재료로 장착 조끼가 빠져도 다른 해제 경로와 같은 거부 기준
    [Test]
    public void Survivor_MergeVestSourceWithVestCellOccupied_Rejected()
    {
        _inventory.Character = _survivor;
        Inventory.ItemInstance vest = Place(_vestData, _uniqueCell, 1);
        Place(_pistolData, _vestCells[0], 1);
        Inventory.ItemInstance storedVest = new Inventory.ItemInstance(_vestData);
        Assert.IsTrue(_inventory.TryStore(storedVest));

        Assert.IsFalse(_inventory.TryMerge(vest, storedVest));
        Assert.AreEqual(1, storedVest.Level);
        AssertVestReleaseUnchanged(vest, 1);
    }

    [Test]
    public void VestCells_NotExpansionCandidateUntilVestSold()
    {
        _inventory.Character = _survivor;

        foreach (Vector2Int cell in _vestCells)
        {
            Assert.IsFalse(_inventory.IsExpansionCandidate(cell));
            Assert.IsFalse(_inventory.TryUnlockCell(cell));
        }

        // 보관만 하면 예약 유지
        Inventory.ItemInstance vest = Place(_vestData, _uniqueCell, 1);
        Assert.IsTrue(_inventory.TryStore(vest));

        foreach (Vector2Int cell in _vestCells)
        {
            Assert.IsFalse(_inventory.IsExpansionCandidate(cell));
        }

        // 장착 상태에서 판매하면 전용 칸은 잠긴 채 확장 후보가 됨
        Assert.IsTrue(_inventory.TryPlace(vest, _uniqueCell, 0));
        Assert.IsTrue(_inventory.TrySell(vest));
        AssertVestCellsAvailable(false);

        foreach (Vector2Int cell in _vestCells)
        {
            Assert.IsTrue(_inventory.IsExpansionCandidate(cell));
            Assert.IsTrue(_inventory.TryUnlockCell(cell));
        }

        // 예약 해제 후 다른 조작에도 확장으로 연 칸 유지
        Place(_pistolData, _weaponCell, 1);
        AssertVestCellsAvailable(true);
    }

    // 조끼 캐릭터가 아니면 전용 칸은 일반 칸 (확장 후보, 연 뒤 유지, 점유해도 고유 아이템 해제 가능)
    [Test]
    public void NonVestCharacter_VestCellsActAsNormalCells()
    {
        _inventory.Character = _cowboy;
        Inventory.ItemInstance hat = Place(_hatData, _uniqueCell, 1);
        AssertVestCellsAvailable(false);

        foreach (Vector2Int cell in _vestCells)
        {
            Assert.IsTrue(_inventory.IsExpansionCandidate(cell));
            Assert.IsTrue(_inventory.TryUnlockCell(cell));
        }

        Place(_pistolData, _vestCells[0], 1);
        Assert.IsTrue(_inventory.TryStore(hat));
        Assert.IsTrue(_inventory.TryPlace(hat, _uniqueCell, 0));
        AssertVestCellsAvailable(true);
    }

    // --- 패시브 Lv.3 효과 ------------------------------------

    [Test]
    public void Level3Bonus_Level2Inactive_Level3To5Same()
    {
        InventoryData stockData = CreatePassiveData(AdjacencyEffectType.None, Level3EffectType.Experience, 0.1f);
        Inventory.ItemInstance stock = Place(stockData, _passiveCells[0], 2);
        Assert.AreEqual(0f, _inventory.GetLevel3Bonus(Level3EffectType.Experience), FLOAT_TOLERANCE);

        for (int level = 3; level <= MAX_LEVEL; level++)
        {
            stock.Level = level;
            Assert.AreEqual(0.1f, _inventory.GetLevel3Bonus(Level3EffectType.Experience), FLOAT_TOLERANCE);
        }

        Assert.AreEqual(0f, _inventory.GetLevel3Bonus(Level3EffectType.MoveSpeed), FLOAT_TOLERANCE);
    }

    [Test]
    public void Level3Bonus_TwoSummed_StoredRemoved()
    {
        InventoryData stockData = CreatePassiveData(AdjacencyEffectType.None, Level3EffectType.Experience, 0.1f);
        Place(stockData, _passiveCells[0], 3);
        Inventory.ItemInstance second = Place(stockData, _passiveCells[1], 3);
        Assert.AreEqual(0.2f, _inventory.GetLevel3Bonus(Level3EffectType.Experience), FLOAT_TOLERANCE);

        _inventory.TryStore(second);
        Assert.AreEqual(0.1f, _inventory.GetLevel3Bonus(Level3EffectType.Experience), FLOAT_TOLERANCE);
    }

    // --- PlayerStatSync ------------------------------------

    [Test]
    public void StatSync_MoveSpeedBonus_CappedAt50Percent()
    {
        PlayerParts player = CreatePlayer();
        _inventory.Character = _survivor;
        InventoryData beltData = CreatePassiveData(AdjacencyEffectType.None, Level3EffectType.MoveSpeed, 0.15f);

        // 벨트 0.15 × 3 + 조끼 해제 0.1 = 0.55 → 상한 0.5
        foreach (Vector2Int cell in _passiveCells)
        {
            Place(beltData, cell, 3);
        }

        Assert.AreEqual(BASE_MOVE_SPEED * 1.5f, player.Movement.MoveSpeed, FLOAT_TOLERANCE);
    }

    [Test]
    public void StatSync_HeavyGunnerMoveSpeed_BaseAndUnequipped()
    {
        PlayerParts player = CreatePlayer();
        _inventory.Character = _heavyGunner;
        Inventory.ItemInstance shake = Place(_shakeData, _uniqueCell, 1);
        Assert.AreEqual(BASE_MOVE_SPEED * 0.9f, player.Movement.MoveSpeed, FLOAT_TOLERANCE);

        _inventory.TryStore(shake);
        Assert.AreEqual(BASE_MOVE_SPEED * 0.7f, player.Movement.MoveSpeed, FLOAT_TOLERANCE);
    }

    // 감소 시 현재 체력 clamp, 증가 시 유지, 반복 토글해도 추가 증감 없음
    [Test]
    public void StatSync_SurvivorMaxHealth_ClampOnDecreaseKeepOnIncrease()
    {
        PlayerParts player = CreatePlayer();
        _inventory.Character = _survivor;
        Assert.AreEqual(BASE_MAX_HEALTH * 0.4f, player.Health.MaxHealth, FLOAT_TOLERANCE);
        Assert.AreEqual(BASE_MAX_HEALTH * 0.4f, player.Health.CurrentHealth, FLOAT_TOLERANCE);

        Inventory.ItemInstance vest = Place(_vestData, _uniqueCell, 1);

        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(BASE_MAX_HEALTH, player.Health.MaxHealth, FLOAT_TOLERANCE);
            Assert.AreEqual(BASE_MAX_HEALTH * 0.4f, player.Health.CurrentHealth, FLOAT_TOLERANCE);

            _inventory.TryStore(vest);
            Assert.AreEqual(BASE_MAX_HEALTH * 0.4f, player.Health.MaxHealth, FLOAT_TOLERANCE);
            Assert.AreEqual(BASE_MAX_HEALTH * 0.4f, player.Health.CurrentHealth, FLOAT_TOLERANCE);

            _inventory.TryPlace(vest, _uniqueCell, 0);
        }
    }

    [Test]
    public void Health_SetMaxHealth_ClampsOnlyWhenBelowCurrent()
    {
        Health health = new Health(100f);
        health.ApplyDamage(50f);

        health.SetMaxHealth(70f);
        Assert.AreEqual(50f, health.CurrentHealth, FLOAT_TOLERANCE);

        health.SetMaxHealth(40f);
        Assert.AreEqual(40f, health.CurrentHealth, FLOAT_TOLERANCE);

        health.SetMaxHealth(100f);
        Assert.AreEqual(40f, health.CurrentHealth, FLOAT_TOLERANCE);
    }

    [Test]
    public void StatSync_ExperienceMultiplier_AccumulatesFraction()
    {
        PlayerParts player = CreatePlayer();
        Place(CreatePassiveData(AdjacencyEffectType.None, Level3EffectType.Experience, 0.1f), _passiveCells[0], 3);

        // 5 × 1.1 = 5.5 → 5 지급·0.5 보관, 다음 5.5 + 0.5 = 6 지급
        player.Experience.AddExperience(5);
        Assert.AreEqual(5, player.Experience.Experience);

        player.Experience.AddExperience(5);
        Assert.AreEqual(11, player.Experience.Experience);
    }

    [Test]
    public void StatSync_CollectRadiusMultiplier_AppliedAndRemoved()
    {
        PlayerParts player = CreatePlayer();
        Inventory.ItemInstance cooler = Place(
            CreatePassiveData(AdjacencyEffectType.None, Level3EffectType.CollectRadius, 0.15f), _passiveCells[0], 3);
        Assert.AreEqual(BASE_COLLECT_RADIUS * 1.15f, player.Experience.CollectRadius, FLOAT_TOLERANCE);

        _inventory.TryStore(cooler);
        Assert.AreEqual(BASE_COLLECT_RADIUS, player.Experience.CollectRadius, FLOAT_TOLERANCE);
    }

    // --- 헬퍼 ------------------------------------

    private struct PlayerParts
    {
        public PlayerMovement Movement;
        public PlayerHealth Health;
        public PlayerExperience Experience;
    }

    // EditMode에서는 OnEnable이 호출되지 않아 Changed 구독을 직접 연결
    private PlayerParts CreatePlayer()
    {
        GameObject playerObject = Track(new GameObject());
        PlayerParts parts = new PlayerParts
        {
            Movement = playerObject.AddComponent<PlayerMovement>(),
            Health = playerObject.AddComponent<PlayerHealth>(),
            Experience = playerObject.AddComponent<PlayerExperience>(),
        };
        InvokeAwake(parts.Health);

        PlayerStatSync statSync = playerObject.AddComponent<PlayerStatSync>();
        SerializedObject serializedSync = new SerializedObject(statSync);
        serializedSync.FindProperty("_inventory").objectReferenceValue = _inventory;
        serializedSync.FindProperty("_movement").objectReferenceValue = parts.Movement;
        serializedSync.FindProperty("_health").objectReferenceValue = parts.Health;
        serializedSync.FindProperty("_experience").objectReferenceValue = parts.Experience;
        serializedSync.ApplyModifiedPropertiesWithoutUndo();
        _inventory.Changed += statSync.ApplyStats;
        statSync.ApplyStats();

        return parts;
    }

    private T Track<T>(T createdObject) where T : Object
    {
        _createdObjects.Add(createdObject);
        return createdObject;
    }

    private static void InvokeAwake(object target)
    {
        target.GetType()
            .GetMethod(AWAKE_METHOD, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(target, null);
    }

    private InventoryData CreateItemData(int id, ItemType itemType)
    {
        InventoryData data = Track(ScriptableObject.CreateInstance<InventoryData>());
        data.SetData(id, itemType.ToString(), null, itemType, string.Empty, _singleCellShape, MAX_LEVEL, null);
        return data;
    }

    private InventoryData CreateWeaponData(int id, WeaponType weaponType)
    {
        WeaponDefinition definition = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_weaponType").enumValueIndex = (int)weaponType;
        serializedDefinition.FindProperty("_projectileDamage").floatValue = BASE_DAMAGE;
        serializedDefinition.FindProperty("_fireInterval").floatValue = BASE_FIRE_INTERVAL;
        serializedDefinition.FindProperty("_spreadAngle").floatValue = BASE_SPREAD_ANGLE;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

        InventoryData data = Track(ScriptableObject.CreateInstance<InventoryData>());
        data.SetData(id, weaponType.ToString(), null, ItemType.Weapon, string.Empty, _singleCellShape, MAX_LEVEL,
            definition);
        return data;
    }

    private InventoryData CreatePassiveData(AdjacencyEffectType adjacencyType, Level3EffectType level3Type,
        float level3Value)
    {
        InventoryData data = CreateItemData(20, ItemType.Passive);
        SerializedObject serializedData = new SerializedObject(data);
        serializedData.FindProperty("_adjacencyEffectType").enumValueIndex = (int)adjacencyType;
        serializedData.FindProperty("_level3EffectType").enumValueIndex = (int)level3Type;
        serializedData.FindProperty("_level3EffectValue").floatValue = level3Value;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private CharacterDefinition CreateCharacter(WeaponType weaponType, InventoryData uniqueItem,
        params (string field, float value)[] bonuses)
    {
        CharacterDefinition character = Track(ScriptableObject.CreateInstance<CharacterDefinition>());
        SerializedObject serializedCharacter = new SerializedObject(character);
        serializedCharacter.FindProperty("_uniqueWeaponType").enumValueIndex = (int)weaponType;
        serializedCharacter.FindProperty("_uniqueItem").objectReferenceValue = uniqueItem;

        // int·bool 필드도 같은 목록으로 받기 위해 속성 타입별로 변환 (bool은 0이 아니면 true)
        foreach ((string field, float value) in bonuses)
        {
            SerializedProperty property = serializedCharacter.FindProperty(field);

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = (int)value;
                    break;

                case SerializedPropertyType.Boolean:
                    property.boolValue = value != 0f;
                    break;

                default:
                    property.floatValue = value;
                    break;
            }
        }

        serializedCharacter.ApplyModifiedPropertiesWithoutUndo();
        return character;
    }

    private Inventory.ItemInstance Place(InventoryData data, Vector2Int cell, int level)
    {
        Inventory.ItemInstance item = new Inventory.ItemInstance(data) { Level = level };
        Assert.IsTrue(_inventory.PlaceItem(item, cell));
        return item;
    }

    private void AssertVestCellsAvailable(bool isAvailable)
    {
        foreach (Vector2Int cell in _vestCells)
        {
            Assert.AreEqual(isAvailable, _inventory.IsCellAvailable(cell));
        }
    }

    // 해제 거부 후 조끼 장착·전용 칸·보관함 한도·보관 수가 그대로인지
    private void AssertVestReleaseUnchanged(Inventory.ItemInstance vest, int storageCount)
    {
        Assert.AreEqual(ItemLocation.Inventory, vest.Location);
        Assert.AreEqual(_uniqueCell, vest.Position);
        Assert.IsTrue(_inventory.IsUniqueItemEquipped());
        AssertVestCellsAvailable(true);
        Assert.AreEqual(7, _inventory.StorageCapacity);
        Assert.AreEqual(storageCount, _inventory.StorageCount);
    }

    private static void AssertStats(Inventory.WeaponStats stats, float damage, float fireInterval, float spreadAngle,
        bool randomSpread)
    {
        Assert.AreEqual(damage, stats.Damage, FLOAT_TOLERANCE);
        Assert.AreEqual(fireInterval, stats.FireInterval, FLOAT_TOLERANCE);
        Assert.AreEqual(spreadAngle, stats.SpreadAngle, FLOAT_TOLERANCE);
        Assert.AreEqual(randomSpread, stats.RandomSpread);
    }
}
