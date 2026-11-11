using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 투사체 뷰포트·발사 방향 판정과 적중 탐지기의 중복·사망 대상 제외·한도·재사용 초기화 검증
/// </summary>
public class ProjectileTests
{
    private const float HIT_RADIUS = 0.1f;
    private const int MAX_UNIQUE_TARGETS = 2;
    private const float DIRECTION_TOLERANCE = 0.0001f;

    private static readonly Vector3 _slantedDirection = new Vector3(3f, 2f, 4f);
    private static readonly Vector3 _expectedPlanarDirection = new Vector3(0.6f, 0f, 0.8f);

    private ProjectileHitDetector _hitDetector;

    /// <summary>참조 식별·생존 여부만 필요한 테스트용 피해 대상</summary>
    private class StubDamageable : IDamageable
    {
        public bool IsAlive { get; set; } = true;

        public void TakeDamage(float damage)
        {
        }
    }

    [SetUp]
    public void SetUp()
    {
        _hitDetector = new ProjectileHitDetector(HIT_RADIUS, new LayerMask());
        _hitDetector.ResetForLaunch(MAX_UNIQUE_TARGETS);
    }

    [TestCase(0.5f, 0.5f, 1f, true)]
    [TestCase(0f, 0f, 1f, true)]
    [TestCase(1f, 1f, 1f, true)]
    [TestCase(-0.01f, 0.5f, 1f, false)]
    [TestCase(0.5f, 1.01f, 1f, false)]
    [TestCase(0.5f, 0.5f, 0f, false)]
    [TestCase(0.5f, 0.5f, -1f, false)]
    public void IsInsideViewport_Boundaries(float x, float y, float z, bool expected)
    {
        Assert.AreEqual(expected, Projectile.IsInsideViewport(new Vector3(x, y, z)));
    }

    // Launch가 발사를 취소하는 조건 (풀 없이 검증 가능한 진입점)
    [TestCase(0f, 0f, 0f)]
    [TestCase(0f, 5f, 0f)]
    public void TryGetPlanarDirection_NoXZComponent_ReturnsFalse(float x, float y, float z)
    {
        Assert.IsFalse(Projectile.TryGetPlanarDirection(new Vector3(x, y, z), out _));
    }

    [Test]
    public void TryGetPlanarDirection_Slanted_ProjectsAndNormalizes()
    {
        Assert.IsTrue(Projectile.TryGetPlanarDirection(_slantedDirection, out Vector3 planarDirection));
        Assert.AreEqual(0f, Vector3.Distance(_expectedPlanarDirection, planarDirection), DIRECTION_TOLERANCE);
    }

    [Test]
    public void TryRecord_SameTargetTwice_RecordsOnce()
    {
        StubDamageable target = new StubDamageable();

        Assert.IsTrue(_hitDetector.TryRecord(target));
        Assert.IsFalse(_hitDetector.TryRecord(target));
        Assert.IsFalse(_hitDetector.HasReachedLimit);
    }

    // TryGetNewTarget은 Physics Cast 결과가 필요해, 생존 판정이 들어 있는 TryRecord로 검증
    [Test]
    public void TryRecord_DeadTarget_NotRecorded()
    {
        StubDamageable target = new StubDamageable { IsAlive = false };

        Assert.IsFalse(_hitDetector.TryRecord(target));

        // 죽은 동안 기록되지 않았으므로 살아나면 처음 맞는 적으로 기록
        target.IsAlive = true;
        Assert.IsTrue(_hitDetector.TryRecord(target));
    }

    [Test]
    public void TryRecord_UniqueTargetsUpToLimit_ReachesLimit()
    {
        for (int i = 0; i < MAX_UNIQUE_TARGETS; i++)
        {
            Assert.IsFalse(_hitDetector.HasReachedLimit);
            _hitDetector.TryRecord(new StubDamageable());
        }

        Assert.IsTrue(_hitDetector.HasReachedLimit);
    }

    [Test]
    public void ResetForLaunch_Reused_ClearsHitRecord()
    {
        StubDamageable target = new StubDamageable();
        _hitDetector.TryRecord(target);

        // 풀에서 다시 꺼낸 상황 (Projectile.Launch가 호출하는 진입점)
        _hitDetector.ResetForLaunch(MAX_UNIQUE_TARGETS);

        Assert.IsTrue(_hitDetector.TryRecord(target));
    }
}
