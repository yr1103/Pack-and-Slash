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
        first.ResetShotCooldown();
        second.ResetShotCooldown();

        first.Tick(TICK_DELTA_TIME);

        Assert.AreEqual(FIRE_INTERVAL - TICK_DELTA_TIME, first.ShotCooldown, 0.0001f);
        Assert.AreEqual(FIRE_INTERVAL, second.ShotCooldown);
    }

    [Test]
    public void Loadout_SameDefinitionTwice_GetsDistinctIdsAndUnequipRemoves()
    {
        GameObject loadoutObject = new GameObject("WeaponLoadoutTest");
        WeaponLoadout loadout = loadoutObject.AddComponent<WeaponLoadout>();

        WeaponInstance first = loadout.Equip(_definition);
        WeaponInstance second = loadout.Equip(_definition);

        Assert.AreNotEqual(first.InstanceId, second.InstanceId);
        Assert.IsTrue(loadout.Unequip(first.InstanceId));
        Assert.IsFalse(loadout.Unequip(first.InstanceId));
        Assert.AreEqual(1, loadout.Weapons.Count);
        Assert.AreSame(second, loadout.Weapons[0]);

        Object.DestroyImmediate(loadoutObject);
    }
}
