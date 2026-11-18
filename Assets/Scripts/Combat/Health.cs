using UnityEngine;

/// <summary>
/// 최대·현재 체력 관리와 피해 적용, 사망 1회 판정 (Enemy·플레이어 공용)
/// </summary>
public class Health
{
    private readonly float _maxHealth;
    private float _currentHealth;

    public Health(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
    }

    public float CurrentHealth => _currentHealth;

    /// <summary>현재 체력이 0 이하인 사망 상태 여부</summary>
    public bool IsDead => _currentHealth <= 0f;

    /// <summary>체력 감소 (0 미만으로 내려가지 않음, 이미 사망 상태면 무시)</summary>
    /// <param name="damage">피해량</param>
    /// <returns>이번 피해로 처음 사망했으면 true (사망 처리를 1회만 하도록 호출부가 사용)</returns>
    public bool ApplyDamage(float damage)
    {
        if (IsDead)
        {
            return false;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        return IsDead;
    }

    /// <summary>현재 체력을 최대로 복구 (풀 재사용·부활 시 호출)</summary>
    public void ResetToMax()
    {
        _currentHealth = _maxHealth;
    }
}
