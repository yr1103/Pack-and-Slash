using UnityEngine;

/// <summary>
/// 최대·현재 체력 관리와 피해 적용, 사망 1회 판정 (Enemy·PlayerHealth가 사용)
/// </summary>
public class Health
{
    private float _maxHealth;
    private float _currentHealth;

    /// <summary>현재 체력을 최대 체력으로 채워 생성</summary>
    /// <param name="maxHealth">최대 체력 (0 이하면 생성 즉시 사망 상태)</param>
    public Health(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
    }

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;

    /// <summary>최대 체력 변경, 줄어들면 현재 체력을 새 최대치로 clamp만 하고 늘어나도 회복하지 않음</summary>
    public void SetMaxHealth(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = Mathf.Min(_currentHealth, maxHealth);
    }

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

    /// <summary>체력 회복 (최대 체력을 넘지 않음, 사망 상태면 무시)</summary>
    /// <param name="amount">회복량</param>
    public void Heal(float amount)
    {
        if (IsDead)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
    }

    /// <summary>현재 체력을 최대로 복구 (Enemy 풀 재사용 시 OnEnable에서 호출)</summary>
    public void ResetToMax()
    {
        _currentHealth = _maxHealth;
    }
}
