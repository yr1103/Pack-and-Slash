using NUnit.Framework;

/// <summary>
/// 보스 돌진 상태 전환의 범위 판정·경고 시간 경과·돌진 종료 후 복귀·쿨다운 검증
/// </summary>
public class BossChargeTests
{
    private const float RANGE = 8f;
    private const float WARN_DURATION = 1f;
    private const float COOLDOWN = 4f;
    private const float IN_RANGE = 5f;
    private const float OUT_OF_RANGE = 10f;
    private const float SHORT_TIME = 0.5f;

    private BossCharge _charge;

    [SetUp]
    public void SetUp()
    {
        _charge = new BossCharge(RANGE, WARN_DURATION, COOLDOWN);
    }

    [Test]
    public void Tick_OutOfRange_StaysChase()
    {
        _charge.Tick(SHORT_TIME, OUT_OF_RANGE);

        Assert.AreEqual(BossCharge.ChargeState.Chase, _charge.State);
    }

    [Test]
    public void Tick_InRange_StartsWarn()
    {
        _charge.Tick(SHORT_TIME, IN_RANGE);

        Assert.AreEqual(BossCharge.ChargeState.Warn, _charge.State);
    }

    [Test]
    public void Tick_WarnDurationElapsed_StartsCharge()
    {
        _charge.Tick(SHORT_TIME, IN_RANGE);
        _charge.Tick(SHORT_TIME, OUT_OF_RANGE);
        // 경고 중에는 범위와 무관하게 진행
        Assert.AreEqual(BossCharge.ChargeState.Warn, _charge.State);

        _charge.Tick(SHORT_TIME, OUT_OF_RANGE);

        Assert.AreEqual(BossCharge.ChargeState.Charge, _charge.State);
    }

    [Test]
    public void EndCharge_ReturnsToChase_NoRestartDuringCooldown()
    {
        _charge.Tick(SHORT_TIME, IN_RANGE);
        _charge.Tick(WARN_DURATION, IN_RANGE);
        _charge.EndCharge();

        Assert.AreEqual(BossCharge.ChargeState.Chase, _charge.State);

        _charge.Tick(COOLDOWN - SHORT_TIME, IN_RANGE);

        Assert.AreEqual(BossCharge.ChargeState.Chase, _charge.State);
    }

    [Test]
    public void Tick_AfterCooldown_StartsWarnAgain()
    {
        _charge.Tick(SHORT_TIME, IN_RANGE);
        _charge.Tick(WARN_DURATION, IN_RANGE);
        _charge.EndCharge();
        _charge.Tick(COOLDOWN, IN_RANGE);

        Assert.AreEqual(BossCharge.ChargeState.Warn, _charge.State);
    }
}
