using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 해금 시각 경계·활성 상한·시야 사다리꼴 경계까지 거리 계산 검증
/// </summary>
public class EnemySpawnerTests
{
    private const float B_UNLOCK_TIME = 180f;
    private const float C_UNLOCK_TIME = 360f;
    private const float JUST_BEFORE_B_UNLOCK = 179.9f;
    private const int MAX_ACTIVE_ENEMIES = 500;
    private const float TOLERANCE = 0.0001f;

    // 카메라가 +Z를 바라볼 때의 시야 사다리꼴 (가까운 변 z = -5 폭 12, 먼 변 z = 15 폭 32)
    private static readonly Vector2[] _trapezoid =
    {
        new Vector2(-6f, -5f), new Vector2(6f, -5f), new Vector2(16f, 15f), new Vector2(-16f, 15f)
    };

    [Test]
    public void IsUnlocked_JustBeforeUnlockTime_ReturnsFalse()
    {
        Assert.IsFalse(EnemySpawner.IsUnlocked(B_UNLOCK_TIME, JUST_BEFORE_B_UNLOCK));
    }

    [Test]
    public void IsUnlocked_AtUnlockTime_ReturnsTrue()
    {
        Assert.IsTrue(EnemySpawner.IsUnlocked(B_UNLOCK_TIME, B_UNLOCK_TIME));
        Assert.IsTrue(EnemySpawner.IsUnlocked(C_UNLOCK_TIME, C_UNLOCK_TIME));
    }

    [Test]
    public void IsUnlocked_BAtCUnlockTime_ReturnsTrue()
    {
        // 6:00 이후에도 먼저 해금된 B는 계속 후보
        Assert.IsTrue(EnemySpawner.IsUnlocked(B_UNLOCK_TIME, C_UNLOCK_TIME));
    }

    [Test]
    public void HasCapacity_BelowMax_ReturnsTrue()
    {
        Assert.IsTrue(EnemySpawner.HasCapacity(MAX_ACTIVE_ENEMIES - 1, MAX_ACTIVE_ENEMIES));
    }

    [Test]
    public void HasCapacity_AtMax_ReturnsFalse()
    {
        Assert.IsFalse(EnemySpawner.HasCapacity(MAX_ACTIVE_ENEMIES, MAX_ACTIVE_ENEMIES));
    }

    [Test]
    public void GetDistanceToBoundary_Forward_ReturnsFarEdgeDistance()
    {
        Assert.AreEqual(15f, EnemySpawner.GetDistanceToBoundary(Vector2.zero, Vector2.up, _trapezoid), TOLERANCE);
    }

    [Test]
    public void GetDistanceToBoundary_Backward_ReturnsNearEdgeDistance()
    {
        Assert.AreEqual(5f, EnemySpawner.GetDistanceToBoundary(Vector2.zero, Vector2.down, _trapezoid), TOLERANCE);
    }

    [Test]
    public void GetDistanceToBoundary_Side_ReturnsSlantedEdgeDistance()
    {
        // 오른쪽 빗변 (6, -5)→(16, 15)은 z = 0에서 x = 8.5
        Assert.AreEqual(8.5f, EnemySpawner.GetDistanceToBoundary(Vector2.zero, Vector2.right, _trapezoid), TOLERANCE);
        Assert.AreEqual(8.5f, EnemySpawner.GetDistanceToBoundary(Vector2.zero, Vector2.left, _trapezoid), TOLERANCE);
    }

    [Test]
    public void GetDistanceToBoundary_Diagonal_ResultLiesOnBoundary()
    {
        Vector2 direction = new Vector2(1f, 1f).normalized;

        float distance = EnemySpawner.GetDistanceToBoundary(Vector2.zero, direction, _trapezoid);
        Vector2 point = direction * distance;

        // 대각선은 빗변보다 먼 변(z = 15)에 먼저 닿음 → (15, 15)
        Assert.AreEqual(15f, point.x, TOLERANCE);
        Assert.AreEqual(15f, point.y, TOLERANCE);
    }
}
