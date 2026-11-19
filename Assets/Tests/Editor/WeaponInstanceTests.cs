using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 같은 WeaponDefinition을 쓰는 인스턴스끼리 런타임 상태를 공유하지 않는지 검증
/// </summary>
public class WeaponInstanceTests
{
    private const float FIRE_INTERVAL = 0.5f;
    private const int MAGAZINE_CAPACITY = 30;
    private const float TICK_DELTA_TIME = 0.1f;
    private const float RELOAD_DURATION = 2f;
    private const float OVERDUE_TICK_DELTA_TIME = 0.7f; // 발사 간격보다 긴 프레임
    private const float TOLERANCE = 0.0001f;
    private const string DEFAULT_CHARACTER_FIELD = "_defaultCharacter";

    private WeaponDefinition _definition;

    [SetUp]
    public void SetUp()
    {
        _definition = ScriptableObject.CreateInstance<WeaponDefinition>();

        // 런타임 API에 설정 메서드를 두지 않기 위해 직렬화 필드에 직접 값 주입
        SerializedObject serializedDefinition = new SerializedObject(_definition);
        serializedDefinition.FindProperty("_weaponType").enumValueIndex = (int)WeaponType.Minigun;
        serializedDefinition.FindProperty("_fireInterval").floatValue = FIRE_INTERVAL;
        serializedDefinition.FindProperty("_magazineCapacity").intValue = MAGAZINE_CAPACITY;
        serializedDefinition.FindProperty("_reloadDuration").floatValue = RELOAD_DURATION;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_definition);
    }

    [Test]
    public void ConsumeAmmo_OnlyChangesOwnInstance()
    {
        WeaponInstance first = new WeaponInstance(1, _definition, 1);
        WeaponInstance second = new WeaponInstance(2, _definition, 1);

        first.ConsumeAmmo();

        Assert.AreEqual(MAGAZINE_CAPACITY - 1, first.CurrentAmmo);
        Assert.AreEqual(MAGAZINE_CAPACITY, second.CurrentAmmo);
    }

    [Test]
    public void ShotCooldown_OnlyChangesOwnInstance()
    {
        WeaponInstance first = new WeaponInstance(1, _definition, 1);
        WeaponInstance second = new WeaponInstance(2, _definition, 1);
        first.ScheduleNextShot();
        second.ScheduleNextShot();

        first.Tick(TICK_DELTA_TIME);

        Assert.AreEqual(FIRE_INTERVAL - TICK_DELTA_TIME, first.ShotCooldown, TOLERANCE);
        Assert.AreEqual(FIRE_INTERVAL, second.ShotCooldown);
    }

    [Test]
    public void ConsumeAmmo_LastRound_StartsReload()
    {
        WeaponInstance weapon = new WeaponInstance(1, _definition, 1);
        int reloadStartedCount = 0;
        weapon.ReloadStarted += () => reloadStartedCount++;

        ConsumeWholeMagazine(weapon);

        Assert.AreEqual(0, weapon.CurrentAmmo);
        Assert.IsTrue(weapon.IsReloading);
        Assert.AreEqual(RELOAD_DURATION, weapon.ReloadRemaining, TOLERANCE);
        Assert.AreEqual(1, reloadStartedCount);
    }

    [Test]
    public void Tick_ShorterThanReloadDuration_KeepsReloading()
    {
        WeaponInstance weapon = new WeaponInstance(1, _definition, 1);
        ConsumeWholeMagazine(weapon);

        weapon.Tick(RELOAD_DURATION - TICK_DELTA_TIME);

        Assert.IsTrue(weapon.IsReloading);
        Assert.AreEqual(0, weapon.CurrentAmmo);
        Assert.AreEqual(TICK_DELTA_TIME, weapon.ReloadRemaining, TOLERANCE);
    }

    [Test]
    public void Tick_ReloadDurationElapsed_RefillsAndFinishes()
    {
        WeaponInstance weapon = new WeaponInstance(1, _definition, 1);
        ConsumeWholeMagazine(weapon);
        int ammoChangedCount = 0;
        int reloadFinishedCount = 0;
        weapon.AmmoChanged += () => ammoChangedCount++;
        weapon.ReloadFinished += () => reloadFinishedCount++;

        weapon.Tick(RELOAD_DURATION);

        Assert.IsFalse(weapon.IsReloading);
        Assert.AreEqual(MAGAZINE_CAPACITY, weapon.CurrentAmmo);
        Assert.AreEqual(0f, weapon.ReloadRemaining);
        Assert.AreEqual(1, ammoChangedCount);
        Assert.AreEqual(1, reloadFinishedCount);
    }

    [Test]
    public void ConsumeAmmo_NoMagazine_DoesNotReload()
    {
        SerializedObject serializedDefinition = new SerializedObject(_definition);
        serializedDefinition.FindProperty("_magazineCapacity").intValue = 0;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        WeaponInstance weapon = new WeaponInstance(1, _definition, 1);

        weapon.ConsumeAmmo();

        Assert.IsFalse(weapon.IsReloading);
        Assert.AreEqual(0, weapon.CurrentAmmo);
    }

    [Test]
    public void ScheduleNextShot_AfterOverdueTick_CarriesOverOverdueTime()
    {
        WeaponInstance weapon = new WeaponInstance(1, _definition, 1);

        weapon.Tick(OVERDUE_TICK_DELTA_TIME);
        Assert.AreEqual(-OVERDUE_TICK_DELTA_TIME, weapon.ShotCooldown, TOLERANCE);

        weapon.ScheduleNextShot();
        Assert.AreEqual(FIRE_INTERVAL - OVERDUE_TICK_DELTA_TIME, weapon.ShotCooldown, TOLERANCE);
    }

    [Test]
    public void ClearOverdueShotCooldown_ResetsOnlyNegativeCooldown()
    {
        WeaponInstance weapon = new WeaponInstance(1, _definition, 1);

        weapon.Tick(OVERDUE_TICK_DELTA_TIME);
        weapon.ClearOverdueShotCooldown();
        Assert.AreEqual(0f, weapon.ShotCooldown);

        weapon.ScheduleNextShot();
        weapon.ClearOverdueShotCooldown();
        Assert.AreEqual(FIRE_INTERVAL, weapon.ShotCooldown, TOLERANCE);
    }

    [Test]
    public void Loadout_SameDefinitionTwice_GetsDistinctIdsAndUnequipRemoves()
    {
        GameObject loadoutObject = new GameObject("WeaponLoadoutTest");
        WeaponLoadout loadout = loadoutObject.AddComponent<WeaponLoadout>();
        // 주무기 해제 시 재선정이 캐릭터의 고유 무기를 읽으므로 기본 캐릭터 주입 (고유 무기는 비워 둠)
        CharacterDefinition character = ScriptableObject.CreateInstance<CharacterDefinition>();
        SerializedObject serializedLoadout = new SerializedObject(loadout);
        serializedLoadout.FindProperty(DEFAULT_CHARACTER_FIELD).objectReferenceValue = character;
        serializedLoadout.ApplyModifiedPropertiesWithoutUndo();

        WeaponInstance first = loadout.Equip(_definition);
        WeaponInstance second = loadout.Equip(_definition);

        Assert.AreNotEqual(first.InstanceId, second.InstanceId);
        Assert.IsTrue(loadout.Unequip(first.InstanceId));
        Assert.IsFalse(loadout.Unequip(first.InstanceId));
        Assert.AreEqual(1, loadout.Weapons.Count);
        Assert.AreSame(second, loadout.Weapons[0]);

        Object.DestroyImmediate(loadoutObject);
        Object.DestroyImmediate(character);
    }

    private static void ConsumeWholeMagazine(WeaponInstance weapon)
    {
        for (int i = 0; i < MAGAZINE_CAPACITY; i++)
        {
            weapon.ConsumeAmmo();
        }
    }
}
