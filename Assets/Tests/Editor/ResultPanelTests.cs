using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 결과 패널 무기 목록 문자열(종류·레벨·진화 표시, 빈 목록)과 진화 무기 수 집계 검증
/// </summary>
public class ResultPanelTests
{
    private const string WEAPON_TYPE_FIELD = "_weaponType";
    private const int PISTOL_INSTANCE_ID = 1;
    private const int SHOTGUN_INSTANCE_ID = 2;
    private const int THIRD_INSTANCE_ID = 3;
    private const int PISTOL_LEVEL = 2;
    private const int SHOTGUN_LEVEL = 5;

    private WeaponDefinition _pistol;
    private WeaponDefinition _shotgun;

    [SetUp]
    public void SetUp()
    {
        _pistol = CreateDefinition(WeaponType.Pistol);
        _shotgun = CreateDefinition(WeaponType.Shotgun);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_pistol);
        Object.DestroyImmediate(_shotgun);
    }

    [Test]
    public void BuildWeaponList_SingleWeapon_ReturnsTypeAndLevel()
    {
        WeaponInstance pistol = new WeaponInstance(PISTOL_INSTANCE_ID, _pistol, PISTOL_LEVEL);

        Assert.AreEqual("Pistol Lv.2", ResultPanel.BuildWeaponList(new[] { pistol }));
    }

    [Test]
    public void BuildWeaponList_WithEvolved_AppendsSuffixAndSeparatesLines()
    {
        WeaponInstance pistol = new WeaponInstance(PISTOL_INSTANCE_ID, _pistol, PISTOL_LEVEL);
        WeaponInstance shotgun = new WeaponInstance(SHOTGUN_INSTANCE_ID, _shotgun, SHOTGUN_LEVEL, true);

        Assert.AreEqual("Pistol Lv.2\nShotgun Lv.5 (Evolved)", ResultPanel.BuildWeaponList(new[] { pistol, shotgun }));
    }

    [Test]
    public void BuildWeaponList_Empty_ReturnsDash()
    {
        Assert.AreEqual("-", ResultPanel.BuildWeaponList(Array.Empty<WeaponInstance>()));
    }

    [Test]
    public void CountEvolved_MixedList_CountsOnlyEvolved()
    {
        WeaponInstance[] weapons =
        {
            new WeaponInstance(PISTOL_INSTANCE_ID, _pistol, PISTOL_LEVEL, true),
            new WeaponInstance(SHOTGUN_INSTANCE_ID, _shotgun, SHOTGUN_LEVEL),
            new WeaponInstance(THIRD_INSTANCE_ID, _shotgun, SHOTGUN_LEVEL, true),
        };

        Assert.AreEqual(2, ResultPanel.CountEvolved(weapons));
    }

    private static WeaponDefinition CreateDefinition(WeaponType weaponType)
    {
        WeaponDefinition definition = ScriptableObject.CreateInstance<WeaponDefinition>();
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty(WEAPON_TYPE_FIELD).enumValueIndex = (int)weaponType;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }
}
