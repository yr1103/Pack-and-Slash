using NUnit.Framework;

/// <summary>
/// 무기 공격력 범위(탄별 피해 ±비율) 계산 검증
/// </summary>
public class WeaponDamageVarianceTests
{
    private const float DAMAGE = 15f;
    private const float VARIANCE = 0.2f;
    private const float NO_VARIANCE = 0f;
    private const float MIN_RATIO = 0f;
    private const float MIDDLE_RATIO = 0.5f;
    private const float MAX_RATIO = 1f;
    private const float TOLERANCE = 0.0001f;

    [Test]
    public void GetRolledDamage_MinRatio_ReturnsLowerBound()
    {
        Assert.AreEqual(12f, WeaponDefinition.GetRolledDamage(DAMAGE, VARIANCE, MIN_RATIO), TOLERANCE);
    }

    [Test]
    public void GetRolledDamage_MaxRatio_ReturnsUpperBound()
    {
        Assert.AreEqual(18f, WeaponDefinition.GetRolledDamage(DAMAGE, VARIANCE, MAX_RATIO), TOLERANCE);
    }

    [Test]
    public void GetRolledDamage_MiddleRatio_ReturnsBaseDamage()
    {
        Assert.AreEqual(DAMAGE, WeaponDefinition.GetRolledDamage(DAMAGE, VARIANCE, MIDDLE_RATIO), TOLERANCE);
    }

    [TestCase(MIN_RATIO)]
    [TestCase(MAX_RATIO)]
    public void GetRolledDamage_NoVariance_ReturnsFixedDamage(float ratio)
    {
        Assert.AreEqual(DAMAGE, WeaponDefinition.GetRolledDamage(DAMAGE, NO_VARIANCE, ratio), TOLERANCE);
    }
}
