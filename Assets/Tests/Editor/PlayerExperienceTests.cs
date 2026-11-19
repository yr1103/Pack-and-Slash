using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 경험치 추가 1회당 누적 증가·ExperienceGained 1회 발생(사운드 1회 약속 지점)·0 이하 무시, 레벨 요구치·레벨업 이월 검증
/// </summary>
public class PlayerExperienceTests
{
    private const int FIRST_AMOUNT = 3;
    private const int SECOND_AMOUNT = 5;
    private const int NEGATIVE_AMOUNT = -2;
    private const int MULTI_LEVEL_AMOUNT = 20;

    private GameObject _playerObject;
    private PlayerExperience _playerExperience;
    private List<int> _gainedAmounts;
    private List<int> _changedLevels;

    [SetUp]
    public void SetUp()
    {
        _playerObject = new GameObject("PlayerExperienceTest");
        _playerExperience = _playerObject.AddComponent<PlayerExperience>();
        _gainedAmounts = new List<int>();
        _changedLevels = new List<int>();
        _playerExperience.ExperienceGained += _gainedAmounts.Add;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_playerObject);
    }

    [Test]
    public void AddExperience_PositiveAmount_IncreasesExperienceByAmount()
    {
        _playerExperience.AddExperience(FIRST_AMOUNT);

        Assert.AreEqual(FIRST_AMOUNT, _playerExperience.Experience);
    }

    [Test]
    public void AddExperience_PositiveAmount_RaisesEventOnceWithAmount()
    {
        _playerExperience.AddExperience(FIRST_AMOUNT);

        CollectionAssert.AreEqual(new[] { FIRST_AMOUNT }, _gainedAmounts);
    }

    [Test]
    public void AddExperience_CalledTwice_AccumulatesAndRaisesEventPerCall()
    {
        _playerExperience.AddExperience(FIRST_AMOUNT);
        _playerExperience.AddExperience(SECOND_AMOUNT);

        Assert.AreEqual(FIRST_AMOUNT + SECOND_AMOUNT, _playerExperience.Experience);
        CollectionAssert.AreEqual(new[] { FIRST_AMOUNT, SECOND_AMOUNT }, _gainedAmounts);
    }

    [TestCase(0)]
    [TestCase(NEGATIVE_AMOUNT)]
    public void AddExperience_ZeroOrNegative_IgnoresValueAndEvent(int amount)
    {
        _playerExperience.AddExperience(FIRST_AMOUNT);
        _gainedAmounts.Clear();

        _playerExperience.AddExperience(amount);

        Assert.AreEqual(FIRST_AMOUNT, _playerExperience.Experience);
        Assert.IsEmpty(_gainedAmounts);
    }

    [TestCase(1, 5)]
    [TestCase(2, 10)]
    [TestCase(3, 15)]
    public void GetRequiredExperience_Level_ReturnsTemporaryCurve(int level, int expected)
    {
        Assert.AreEqual(expected, PlayerExperience.GetRequiredExperience(level));
    }

    [Test]
    public void AddExperience_ExactlyRequired_LevelsUpWithZeroRemainder()
    {
        _playerExperience.LevelChanged += _changedLevels.Add;

        _playerExperience.AddExperience(PlayerExperience.GetRequiredExperience(1));

        Assert.AreEqual(2, _playerExperience.Level);
        Assert.AreEqual(0, _playerExperience.CurrentLevelExperience);
        CollectionAssert.AreEqual(new[] { 2 }, _changedLevels);
    }

    [Test]
    public void AddExperience_LargeAmount_LevelsUpMultipleTimesAndCarriesOver()
    {
        _playerExperience.LevelChanged += _changedLevels.Add;

        // 레벨 1→2에 5, 2→3에 10 소모 후 잔여 5 (레벨 3 요구치 15 미만)
        _playerExperience.AddExperience(MULTI_LEVEL_AMOUNT);

        Assert.AreEqual(3, _playerExperience.Level);
        Assert.AreEqual(5, _playerExperience.CurrentLevelExperience);
        Assert.AreEqual(MULTI_LEVEL_AMOUNT, _playerExperience.Experience);
        CollectionAssert.AreEqual(new[] { 2, 3 }, _changedLevels);
    }

    [TestCase(0)]
    [TestCase(NEGATIVE_AMOUNT)]
    public void AddExperience_ZeroOrNegative_KeepsLevelAndCurrentLevelExperience(int amount)
    {
        _playerExperience.AddExperience(FIRST_AMOUNT);
        _playerExperience.LevelChanged += _changedLevels.Add;

        _playerExperience.AddExperience(amount);

        Assert.AreEqual(1, _playerExperience.Level);
        Assert.AreEqual(FIRST_AMOUNT, _playerExperience.CurrentLevelExperience);
        Assert.IsEmpty(_changedLevels);
    }
}
