using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 월드 속도 → 모델 정면 기준 로컬 이동 방향 변환 검증
/// </summary>
public class PlayerAnimatorTests
{
    private const float THRESHOLD = 0.1f;
    private const float SPEED = 5f;
    private const float BELOW_THRESHOLD_SPEED = 0.05f;
    private const float TURN_RIGHT_ANGLE = 90f;
    private const float TOLERANCE = 0.0001f;

    private static readonly Quaternion _turnedRight = Quaternion.Euler(0f, TURN_RIGHT_ANGLE, 0f);

    [Test]
    public void GetLocalMoveDirection_WorldForward_ReturnsForward()
    {
        AssertDirection(Vector2.up, Vector3.forward * SPEED, Quaternion.identity);
    }

    [Test]
    public void GetLocalMoveDirection_WorldBackward_ReturnsBackward()
    {
        AssertDirection(Vector2.down, Vector3.back * SPEED, Quaternion.identity);
    }

    [Test]
    public void GetLocalMoveDirection_WorldRight_ReturnsRight()
    {
        AssertDirection(Vector2.right, Vector3.right * SPEED, Quaternion.identity);
    }

    [Test]
    public void GetLocalMoveDirection_ModelTurnedRightWorldForward_ReturnsLeft()
    {
        // 모델이 월드 +X를 보고 있으면 월드 +Z 이동은 모델 기준 왼쪽
        AssertDirection(Vector2.left, Vector3.forward * SPEED, _turnedRight);
    }

    [Test]
    public void GetLocalMoveDirection_ModelTurnedRightWorldRight_ReturnsForward()
    {
        AssertDirection(Vector2.up, Vector3.right * SPEED, _turnedRight);
    }

    [Test]
    public void GetLocalMoveDirection_BelowThreshold_ReturnsZero()
    {
        AssertDirection(Vector2.zero, Vector3.forward * BELOW_THRESHOLD_SPEED, Quaternion.identity);
    }

    [Test]
    public void GetLocalMoveDirection_VerticalOnly_ReturnsZero()
    {
        AssertDirection(Vector2.zero, Vector3.down * SPEED, Quaternion.identity);
    }

    [Test]
    public void GetLocalMoveDirection_ForwardWithVertical_ReturnsForward()
    {
        AssertDirection(Vector2.up, (Vector3.forward + Vector3.down) * SPEED, Quaternion.identity);
    }

    private static void AssertDirection(Vector2 expected, Vector3 worldVelocity, Quaternion modelRotation)
    {
        Vector2 result = PlayerAnimator.GetLocalMoveDirection(worldVelocity, modelRotation, THRESHOLD);

        Assert.AreEqual(expected.x, result.x, TOLERANCE);
        Assert.AreEqual(expected.y, result.y, TOLERANCE);
    }
}
