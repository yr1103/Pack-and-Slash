using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 해금 시각 경계·활성 상한·시야 사다리꼴 경계까지 거리 계산·보스 생성 후보 선택·원형 스폰 지점 검증
/// </summary>
public class EnemySpawnerTests
{
    private const float B_UNLOCK_TIME = 180f;
    private const float C_UNLOCK_TIME = 360f;
    private const float JUST_BEFORE_B_UNLOCK = 179.9f;
    private const int MAX_ACTIVE_ENEMIES = 500;
    private const float TOLERANCE = 0.0001f;
    private const int RING_COUNT = 30;
    private const float RING_RADIUS = 14f;
    private const float ANGLE_TOLERANCE = 0.01f; // SignedAngle 내부 acos 오차 허용

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

    [Test]
    public void GetDistanceToBoundary_OutsideOriginFacingAway_ReturnsZero()
    {
        // 사다리꼴 아래(z = -10)에서 뒤쪽을 보면 경계와 만나지 않음 → 스폰 시도 실패 처리 조건
        Vector2 outsideOrigin = new Vector2(0f, -10f);

        Assert.AreEqual(0f, EnemySpawner.GetDistanceToBoundary(outsideOrigin, Vector2.down, _trapezoid), TOLERANCE);
    }

    [Test]
    public void SelectBossSpawnIndex_AllOutside_ReturnsNearest()
    {
        Vector2[] candidates = { new Vector2(30f, 0f), new Vector2(0f, -8f) };

        Assert.AreEqual(1, EnemySpawner.SelectBossSpawnIndex(Vector2.zero, candidates, _trapezoid));
    }

    [Test]
    public void SelectBossSpawnIndex_NearestInside_ReturnsNextNearestOutside()
    {
        // (0, 10)은 시야 안, (0, -20)은 (40, 0)보다 가까운 시야 밖 후보
        Vector2[] candidates = { new Vector2(0f, 10f), new Vector2(40f, 0f), new Vector2(0f, -20f) };

        Assert.AreEqual(2, EnemySpawner.SelectBossSpawnIndex(Vector2.zero, candidates, _trapezoid));
    }

    [Test]
    public void SelectBossSpawnIndex_AllInside_ReturnsMinusOne()
    {
        Vector2[] candidates = { new Vector2(0f, 10f), new Vector2(5f, 0f) };

        Assert.AreEqual(-1, EnemySpawner.SelectBossSpawnIndex(Vector2.zero, candidates, _trapezoid));
    }

    [Test]
    public void SelectBossSpawnIndex_OnBoundary_TreatedAsInside()
    {
        // (0, -5)는 가까운 변 위 → 화면 가장자리라 제외
        Vector2[] candidates = { new Vector2(0f, -5f), new Vector2(0f, -20f) };

        Assert.AreEqual(1, EnemySpawner.SelectBossSpawnIndex(Vector2.zero, candidates, _trapezoid));
    }

    [Test]
    public void GetRingPoint_AllPoints_LieAtRadiusFromCenter()
    {
        Vector2 center = new Vector2(3f, -2f);

        for (int i = 0; i < RING_COUNT; i++)
        {
            Vector2 point = EnemySpawner.GetRingPoint(center, i, RING_COUNT, RING_RADIUS);
            Assert.AreEqual(RING_RADIUS, Vector2.Distance(center, point), TOLERANCE);
        }
    }

    [Test]
    public void GetRingPoint_AdjacentPoints_SeparatedByEqualAngle()
    {
        float expectedAngle = 360f / RING_COUNT;

        for (int i = 0; i < RING_COUNT; i++)
        {
            Vector2 current = EnemySpawner.GetRingPoint(Vector2.zero, i, RING_COUNT, RING_RADIUS);
            Vector2 next = EnemySpawner.GetRingPoint(Vector2.zero, (i + 1) % RING_COUNT, RING_COUNT, RING_RADIUS);
            Assert.AreEqual(expectedAngle, Vector2.SignedAngle(current, next), ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetRingPoint_IndexZero_ReturnsCenterPlusRadiusOnX()
    {
        Vector2 center = new Vector2(3f, -2f);

        Vector2 point = EnemySpawner.GetRingPoint(center, 0, RING_COUNT, RING_RADIUS);

        Assert.AreEqual(center.x + RING_RADIUS, point.x, TOLERANCE);
        Assert.AreEqual(center.y, point.y, TOLERANCE);
    }
}
