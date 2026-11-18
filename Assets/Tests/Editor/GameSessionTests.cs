using NUnit.Framework;

/// <summary>
/// 클리어 판정 경계와 생존 시간 표시 형식(mm:ss, 소수점 버림) 검증
/// </summary>
public class GameSessionTests
{
    private const float CLEAR_TIME = 1380f;
    private const float JUST_BEFORE_CLEAR_TIME = 1379.9f;
    private const float JUST_BEFORE_ONE_MINUTE = 59.9f;

    [Test]
    public void IsCleared_JustBeforeClearTime_ReturnsFalse()
    {
        Assert.IsFalse(GameSession.IsCleared(JUST_BEFORE_CLEAR_TIME, CLEAR_TIME));
    }

    [Test]
    public void IsCleared_AtClearTime_ReturnsTrue()
    {
        Assert.IsTrue(GameSession.IsCleared(CLEAR_TIME, CLEAR_TIME));
    }

    [Test]
    public void FormatTime_Zero_ReturnsZeroMinutesZeroSeconds()
    {
        Assert.AreEqual("00:00", GameSession.FormatTime(0f));
    }

    [Test]
    public void FormatTime_FractionalSeconds_TruncatesFraction()
    {
        Assert.AreEqual("00:59", GameSession.FormatTime(JUST_BEFORE_ONE_MINUTE));
    }

    [Test]
    public void FormatTime_ClearTime_ReturnsTwentyThreeMinutes()
    {
        Assert.AreEqual("23:00", GameSession.FormatTime(CLEAR_TIME));
    }
}
