using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 경험치 추가 1회당 누적 증가·ExperienceGained 1회 발생(사운드 1회 약속 지점)·0 이하 무시 검증
/// </summary>
public class PlayerExperienceTests
{
    private const int FIRST_AMOUNT = 3;
    private const int SECOND_AMOUNT = 5;
    private const int NEGATIVE_AMOUNT = -2;

    private GameObject _playerObject;
    private PlayerExperience _playerExperience;
    private List<int> _gainedAmounts;

    [SetUp]
    public void SetUp()
    {
        _playerObject = new GameObject("PlayerExperienceTest");
        _playerExperience = _playerObject.AddComponent<PlayerExperience>();
        _gainedAmounts = new List<int>();
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
}
