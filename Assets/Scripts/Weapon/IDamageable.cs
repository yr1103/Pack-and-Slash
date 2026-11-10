/// <summary>
/// 투사체 등에게 피해를 받을 수 있는 대상 계약
/// </summary>
public interface IDamageable
{
    /// <summary>피해 적용 (사망·무시 여부는 구현체가 결정)</summary>
    /// <param name="damage">피해량</param>
    void TakeDamage(float damage);
}
