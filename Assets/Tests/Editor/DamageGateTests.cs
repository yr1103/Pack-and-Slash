using NUnit.Framework;

/// <summary>
/// 무적 게이트의 같은 프레임 중복 차단·무적시간 중 차단·경과 후 재통과·경계 시점 검증
/// </summary>
public class DamageGateTests
{
    private const float INVULNERABILITY_DURATION = 0.5f;
    private const float HALF_DURATION = 0.25f;
    private const float AFTER_DURATION = 0.6f;
    private const int SAME_FRAME_ATTEMPTS = 10;

    private DamageGate _gate;

    [SetUp]
    public void SetUp()
    {
        _gate = new DamageGate(INVULNERABILITY_DURATION);
    }

    [Test]
    public void TryPass_SameFrameRepeated_PassesOnce()
    {
        int passCount = 0;
        for (int i = 0; i < SAME_FRAME_ATTEMPTS; i++)
        {
            if (_gate.TryPass())
            {
                passCount++;
            }
        }

        Assert.AreEqual(1, passCount);
    }

    [Test]
    public void TryPass_DuringInvulnerability_Blocked()
    {
        _gate.TryPass();
        _gate.Tick(HALF_DURATION);

        Assert.IsFalse(_gate.IsOpen);
        Assert.IsFalse(_gate.TryPass());
    }

    [Test]
    public void TryPass_AfterInvulnerability_PassesAgain()
    {
        _gate.TryPass();
        _gate.Tick(AFTER_DURATION);

        Assert.IsTrue(_gate.IsOpen);
        Assert.IsTrue(_gate.TryPass());
        // 재통과로 무적시간이 다시 시작
        Assert.IsFalse(_gate.TryPass());
    }

    [Test]
    public void TryPass_ExactlyDurationElapsed_PassesAgain()
    {
        _gate.TryPass();
        _gate.Tick(INVULNERABILITY_DURATION);

        // 문서 주석 계약 : 무적시간 이상 경과하면 열림
        Assert.IsTrue(_gate.IsOpen);
        Assert.IsTrue(_gate.TryPass());
    }

    [Test]
    public void TryPass_ZeroDuration_AlwaysPasses()
    {
        DamageGate gate = new DamageGate(0f);

        Assert.IsTrue(gate.TryPass());
        Assert.IsTrue(gate.TryPass());
    }
}
