using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 주무기 선정 우선순위(진화 → 고유 무기 → 높은 레벨 → 낮은 InstanceId)와 장착·해제 시 주무기 유지·재선정 검증
/// </summary>
public class WeaponLoadoutTests
{
    private const int LOW_LEVEL = 1;
    private const int HIGH_LEVEL = 3;
    private const int EARLIER_INSTANCE_ID = 1;
    private const int LATER_INSTANCE_ID = 2;
    private const string SIGNATURE_WEAPON_FIELD = "_signatureWeapon";

    private WeaponDefinition _signatureWeapon;
    private WeaponDefinition _otherWeapon;
    private GameObject _loadoutObject;
    private WeaponLoadout _loadout;

    [SetUp]
    public void SetUp()
    {
        _signatureWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        _otherWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();

        // EditMode에서는 AddComponent 시 Awake가 호출되지 않아 고유 무기 자동 장착 없이 빈 목록으로 시작
        _loadoutObject = new GameObject();
        _loadout = _loadoutObject.AddComponent<WeaponLoadout>();
        SerializedObject serializedLoadout = new SerializedObject(_loadout);
        serializedLoadout.FindProperty(SIGNATURE_WEAPON_FIELD).objectReferenceValue = _signatureWeapon;
        serializedLoadout.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_loadoutObject);
        Object.DestroyImmediate(_signatureWeapon);
        Object.DestroyImmediate(_otherWeapon);
    }

    // 각 테스트는 이겨야 할 무기를 목록 뒤에 두어 목록 순서로 우연히 통과하지 않게 함
    [Test]
    public void SelectPrimary_EvolvedBeatsSignatureAndLevel()
    {
        WeaponInstance signature = new WeaponInstance(EARLIER_INSTANCE_ID, _signatureWeapon, HIGH_LEVEL);
        WeaponInstance evolved = new WeaponInstance(LATER_INSTANCE_ID, _otherWeapon, LOW_LEVEL, true);

        Assert.AreSame(evolved, WeaponLoadout.SelectPrimary(new[] { signature, evolved }, _signatureWeapon));
    }

    [Test]
    public void SelectPrimary_SignatureBeatsLevel()
    {
        WeaponInstance highLevel = new WeaponInstance(EARLIER_INSTANCE_ID, _otherWeapon, HIGH_LEVEL);
        WeaponInstance signature = new WeaponInstance(LATER_INSTANCE_ID, _signatureWeapon, LOW_LEVEL);

        Assert.AreSame(signature, WeaponLoadout.SelectPrimary(new[] { highLevel, signature }, _signatureWeapon));
    }

    [Test]
    public void SelectPrimary_HigherLevelWins()
    {
        WeaponInstance lowLevel = new WeaponInstance(EARLIER_INSTANCE_ID, _otherWeapon, LOW_LEVEL);
        WeaponInstance highLevel = new WeaponInstance(LATER_INSTANCE_ID, _otherWeapon, HIGH_LEVEL);

        Assert.AreSame(highLevel, WeaponLoadout.SelectPrimary(new[] { lowLevel, highLevel }, _signatureWeapon));
    }

    [Test]
    public void SelectPrimary_Tie_LowerInstanceIdWins()
    {
        WeaponInstance laterId = new WeaponInstance(LATER_INSTANCE_ID, _otherWeapon, LOW_LEVEL);
        WeaponInstance earlierId = new WeaponInstance(EARLIER_INSTANCE_ID, _otherWeapon, LOW_LEVEL);

        Assert.AreSame(earlierId, WeaponLoadout.SelectPrimary(new[] { laterId, earlierId }, _signatureWeapon));
    }

    [Test]
    public void SelectPrimary_Empty_ReturnsNull()
    {
        Assert.IsNull(WeaponLoadout.SelectPrimary(Array.Empty<WeaponInstance>(), _signatureWeapon));
    }

    [Test]
    public void Equip_NoPrimary_BecomesPrimary()
    {
        WeaponInstance first = _loadout.Equip(_otherWeapon);

        Assert.AreSame(first, _loadout.Primary);
    }

    // 고유 무기는 재선정 우선순위가 높지만 장착만으로는 주무기를 빼앗지 않음
    [Test]
    public void Equip_HasPrimary_KeepsPrimary()
    {
        WeaponInstance first = _loadout.Equip(_otherWeapon);
        _loadout.Equip(_signatureWeapon);

        Assert.AreSame(first, _loadout.Primary);
    }

    [Test]
    public void Unequip_NonPrimary_KeepsPrimary()
    {
        WeaponInstance first = _loadout.Equip(_otherWeapon);
        WeaponInstance second = _loadout.Equip(_otherWeapon);

        _loadout.Unequip(second.InstanceId);

        Assert.AreSame(first, _loadout.Primary);
    }

    // 낮은 InstanceId 무기보다 고유 무기가 선정되어야 목록 순서가 아닌 우선순위 규칙을 따른 것
    [Test]
    public void Unequip_Primary_ReselectsByPriority()
    {
        WeaponInstance first = _loadout.Equip(_otherWeapon);
        _loadout.Equip(_otherWeapon);
        WeaponInstance signature = _loadout.Equip(_signatureWeapon);

        _loadout.Unequip(first.InstanceId);

        Assert.AreSame(signature, _loadout.Primary);
    }

    [Test]
    public void Unequip_LastWeapon_PrimaryIsNull()
    {
        WeaponInstance only = _loadout.Equip(_otherWeapon);

        _loadout.Unequip(only.InstanceId);

        Assert.IsNull(_loadout.Primary);
    }
}
