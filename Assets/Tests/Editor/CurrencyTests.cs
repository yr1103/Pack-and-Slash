using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 재화 추가 시 잔액 증가·Changed 1회(새 잔액 전달)·0 이하 무시, 차감 성공·잔액 부족 실패 시 잔액·이벤트 불변 검증
/// </summary>
public class CurrencyTests
{
    private const int ADD_AMOUNT = 5;
    private const int SPEND_AMOUNT = 3;
    private const int NEGATIVE_AMOUNT = -2;

    private GameObject _currencyObject;
    private Currency _currency;
    private List<int> _changedBalances;

    [SetUp]
    public void SetUp()
    {
        _currencyObject = new GameObject("CurrencyTest");
        _currency = _currencyObject.AddComponent<Currency>();
        _changedBalances = new List<int>();
        _currency.Changed += _changedBalances.Add;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_currencyObject);
    }

    [Test]
    public void Add_PositiveAmount_IncreasesBalanceAndRaisesEventOnce()
    {
        _currency.Add(ADD_AMOUNT);

        Assert.AreEqual(ADD_AMOUNT, _currency.Balance);
        CollectionAssert.AreEqual(new[] { ADD_AMOUNT }, _changedBalances);
    }

    [TestCase(0)]
    [TestCase(NEGATIVE_AMOUNT)]
    public void Add_ZeroOrNegative_IgnoresValueAndEvent(int amount)
    {
        _currency.Add(amount);

        Assert.AreEqual(0, _currency.Balance);
        Assert.IsEmpty(_changedBalances);
    }

    [Test]
    public void TrySpend_EnoughBalance_DecreasesBalanceAndRaisesEvent()
    {
        _currency.Add(ADD_AMOUNT);
        _changedBalances.Clear();

        Assert.IsTrue(_currency.TrySpend(SPEND_AMOUNT));

        Assert.AreEqual(ADD_AMOUNT - SPEND_AMOUNT, _currency.Balance);
        CollectionAssert.AreEqual(new[] { ADD_AMOUNT - SPEND_AMOUNT }, _changedBalances);
    }

    [TestCase(ADD_AMOUNT + 1)] // 잔액 부족
    [TestCase(0)]
    [TestCase(NEGATIVE_AMOUNT)]
    public void TrySpend_NotEnoughOrNonPositive_FailsAndKeepsState(int amount)
    {
        _currency.Add(ADD_AMOUNT);
        _changedBalances.Clear();

        Assert.IsFalse(_currency.TrySpend(amount));

        Assert.AreEqual(ADD_AMOUNT, _currency.Balance);
        Assert.IsEmpty(_changedBalances);
    }
}
