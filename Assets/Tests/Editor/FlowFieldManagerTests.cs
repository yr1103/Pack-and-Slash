using NUnit.Framework;
using UnityEngine;

/// <summary>
/// FlowFieldManager 재계산 판단(타일 변경 여부·최소 간격 경계) 검증
/// </summary>
public class FlowFieldManagerTests
{
    private const float INTERVAL = 0.2f;
    private const float BELOW_INTERVAL = 0.1f;
    private const float ABOVE_INTERVAL = 5f; // 오래 정지한 뒤의 경과 시간

    private static readonly Vector2Int _lastTarget = new Vector2Int(3, 3);
    private static readonly Vector2Int _movedTile = new Vector2Int(4, 3);

    [Test]
    public void ShouldRecalculate_SameTile_IsFalse()
    {
        Assert.IsFalse(FlowFieldManager.ShouldRecalculate(_lastTarget, _lastTarget, ABOVE_INTERVAL, INTERVAL));
    }

    [Test]
    public void ShouldRecalculate_MovedBelowInterval_IsFalse()
    {
        Assert.IsFalse(FlowFieldManager.ShouldRecalculate(_movedTile, _lastTarget, BELOW_INTERVAL, INTERVAL));
    }

    [Test]
    public void ShouldRecalculate_MovedAtInterval_IsTrue()
    {
        Assert.IsTrue(FlowFieldManager.ShouldRecalculate(_movedTile, _lastTarget, INTERVAL, INTERVAL));
    }

    [Test]
    public void ShouldRecalculate_MovedAboveInterval_IsTrue()
    {
        Assert.IsTrue(FlowFieldManager.ShouldRecalculate(_movedTile, _lastTarget, ABOVE_INTERVAL, INTERVAL));
    }
}
