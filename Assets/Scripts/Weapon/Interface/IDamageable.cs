/// <summary>
/// 투사체 등에게 피해를 받을 수 있는 대상 계약
/// </summary>
public interface IDamageable
{
    /// <summary>살아 있어 피해를 받을 수 있는 상태 여부 (false면 투사체가 기록 없이 통과)</summary>
    bool IsAlive { get; }

    /// <summary>피해 적용 (사망·무시 여부는 구현체가 결정)</summary>
    /// <param name="damage">피해량</param>
    void TakeDamage(float damage);
}
