using NUnit.Framework;

/// <summary>
/// 체력 피해 감소·사망 1회 판정·사망 후 피해 무시·초기화 복구 검증
/// </summary>
public class HealthTests
{
    private const float MAX_HEALTH = 30f;
    private const float DAMAGE = 10f;
    private const float EXACT_LETHAL_DAMAGE = 30f; // 체력이 정확히 0
    private const float OVERKILL_DAMAGE = 45f; // 체력이 0 미만으로 계산되는 피해
    private const float TOLERANCE = 0.0001f;

    private Health _health;

    [SetUp]
    public void SetUp()
    {
        _health = new Health(MAX_HEALTH);
    }

    [Test]
    public void ApplyDamage_NonLethal_ReducesCurrentHealth()
    {
        Assert.IsFalse(_health.ApplyDamage(DAMAGE));
        Assert.AreEqual(MAX_HEALTH - DAMAGE, _health.CurrentHealth, TOLERANCE);
        Assert.IsFalse(_health.IsDead);
    }

    [TestCase(EXACT_LETHAL_DAMAGE)]
    [TestCase(OVERKILL_DAMAGE)]
    public void ApplyDamage_ReachesZeroOrBelow_ReportsDeathOnce(float lethalDamage)
    {
        Assert.IsTrue(_health.ApplyDamage(lethalDamage));
        Assert.IsTrue(_health.IsDead);
        Assert.AreEqual(0f, _health.CurrentHealth, TOLERANCE);

        Assert.IsFalse(_health.ApplyDamage(lethalDamage));
    }

    [Test]
    public void ApplyDamage_AfterDeath_Ignored()
    {
        _health.ApplyDamage(EXACT_LETHAL_DAMAGE);

        Assert.IsFalse(_health.ApplyDamage(DAMAGE));
        Assert.IsTrue(_health.IsDead);
        Assert.AreEqual(0f, _health.CurrentHealth, TOLERANCE);
    }

    [Test]
    public void ResetToMax_AfterDeath_RestoresMaxHealth()
    {
        _health.ApplyDamage(EXACT_LETHAL_DAMAGE);

        // 풀에서 다시 꺼낸 상황 (Enemy.OnEnable이 호출하는 진입점)
        _health.ResetToMax();

        Assert.IsFalse(_health.IsDead);
        Assert.AreEqual(MAX_HEALTH, _health.CurrentHealth, TOLERANCE);
        // 재사용 후에도 사망 판정이 다시 1회 발생
        Assert.IsTrue(_health.ApplyDamage(EXACT_LETHAL_DAMAGE));
    }
}
