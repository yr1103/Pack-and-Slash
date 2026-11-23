using System;
using UnityEngine;

/// <summary>
/// 특수 재화 단일 잔액 보유, 잔액이 바뀔 때만 Changed 발생
/// </summary>
public class Currency : MonoBehaviour
{
    private int _balance;

    public int Balance => _balance;

    /// <summary>잔액이 바뀐 뒤 새 잔액 전달</summary>
    public event Action<int> Changed;

    /// <summary>잔액 증가 (0 이하는 무시)</summary>
    public void Add(int amount)
    {
        if (amount <= 0)
            return;

        _balance += amount;
        Changed?.Invoke(_balance);
    }

    /// <summary>잔액이 충분할 때만 차감</summary>
    /// <returns>0 이하 또는 잔액 부족이면 false, 잔액·이벤트 변화 없음</returns>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > _balance)
            return false;

        _balance -= amount;
        Changed?.Invoke(_balance);
        return true;
    }
}
