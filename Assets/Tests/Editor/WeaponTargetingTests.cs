using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기 표적 탐색(XZ 최근접)과 논리 조준 방향 계산 검증
/// </summary>
public class WeaponTargetingTests
{
    private const float RANGE = 10f;
    private const float SHORT_RANGE = 5f;
    private const float LONG_RANGE = 20f;
    private const float TOLERANCE = 0.0001f;
    // Vector3.Angle은 acos 기반이라 0° 근처에서 float 오차가 약 0.02°까지 커짐
    private const float ANGLE_TOLERANCE = 0.05f;
    private const int DEFAULT_SPAWN_ID = 0;
    private const int LOW_SPAWN_ID = 1;
    private const int HIGH_SPAWN_ID = 2;
    private const float SPREAD_ANGLE = 45f;
    private const float NO_SPREAD = 0f;
    private const int SINGLE_SHOT = 1;
    private const int PELLET_COUNT = 5;
    private const float LEFT_EDGE_RATIO = 0f;
    private const float MIDDLE_RATIO = 0.5f;
    private const float RIGHT_EDGE_RATIO = 1f;
    private const float HALF_SPREAD_ANGLE = SPREAD_ANGLE / 2f;

    private static readonly Vector3 _origin = Vector3.zero;
    private static readonly Vector3 _frontFar = new Vector3(0f, 0f, 8f);
    private static readonly Vector3 _backNear = new Vector3(0f, 0f, -3f);
    private static readonly Vector3 _outOfRange = new Vector3(15f, 0f, 0f);
    private static readonly Vector3 _highNear = new Vector3(2f, 50f, 0f);
    private static readonly Vector3 _lowFar = new Vector3(4f, 0f, 0f);
    private static readonly Vector3 _tieRight = new Vector3(5f, 0f, 0f);
    private static readonly Vector3 _tieLeft = new Vector3(-5f, 0f, 0f);
    private static readonly Vector3 _rangeEdge = new Vector3(RANGE, 0f, 0f);
    private static readonly Vector3 _muzzle = new Vector3(0f, 1.5f, 0f);
    private static readonly Vector3 _aimPoint = new Vector3(3f, 0.2f, 4f);
    // _aimPoint와 XZ만 같고 높이는 다른 총구 (적이 총구에 파고든 상황)
    private static readonly Vector3 _overlappedMuzzle = new Vector3(3f, 1.5f, 4f);
    // 높이 성분과 길이가 있는 중심 방향으로 XZ 투영·정규화까지 확인
    private static readonly Vector3 _spreadCenter = new Vector3(3f, 2f, 4f);
    private static readonly float[] _expectedPelletAngles = { -22.5f, -11.25f, 0f, 11.25f, 22.5f };
    private static readonly float[] _randomRatios = { 0f, 0.1f, 0.37f, 0.5f, 0.82f, 1f };

    /// <summary>GameObject 없이 쓰는 테스트용 표적</summary>
    private class StubTarget : ITargetable
    {
        public Vector3 AimPoint { get; }
        public bool IsTargetable { get; }
        public int SpawnId { get; }

        public StubTarget(Vector3 aimPoint, bool isTargetable = true, int spawnId = DEFAULT_SPAWN_ID)
        {
            AimPoint = aimPoint;
            IsTargetable = isTargetable;
            SpawnId = spawnId;
        }
    }

    [Test]
    public void FindNearest_BackCloserThanFront_SelectsBack()
    {
        StubTarget front = new StubTarget(_frontFar);
        StubTarget back = new StubTarget(_backNear);

        ITargetable result = WeaponTargeting.FindNearest(_origin, RANGE, new List<ITargetable> { front, back });

        Assert.AreSame(back, result);
    }

    [Test]
    public void FindNearest_ExcludesOutOfRangeAndUntargetable()
    {
        StubTarget outOfRange = new StubTarget(_outOfRange);
        StubTarget dead = new StubTarget(_backNear, false);
        StubTarget valid = new StubTarget(_frontFar);

        ITargetable result = WeaponTargeting.FindNearest(_origin, RANGE, new List<ITargetable> { outOfRange, dead, valid });

        Assert.AreSame(valid, result);
    }

    [Test]
    public void FindNearest_TargetExactlyAtRange_IsIncluded()
    {
        StubTarget edge = new StubTarget(_rangeEdge);

        ITargetable result = WeaponTargeting.FindNearest(_origin, RANGE, new List<ITargetable> { edge });

        Assert.AreSame(edge, result);
    }

    [Test]
    public void FindNearest_SameDistance_SelectsLowerSpawnIdRegardlessOfOrder()
    {
        StubTarget later = new StubTarget(_tieRight, spawnId: HIGH_SPAWN_ID);
        StubTarget earlier = new StubTarget(_tieLeft, spawnId: LOW_SPAWN_ID);

        // 높은 ID를 목록 앞에 두어 목록 순서로 결정되지 않음을 확인
        ITargetable result = WeaponTargeting.FindNearest(_origin, RANGE, new List<ITargetable> { later, earlier });

        Assert.AreSame(earlier, result);
    }

    [Test]
    public void FindNearest_NoCandidates_ReturnsNull()
    {
        Assert.IsNull(WeaponTargeting.FindNearest(_origin, RANGE, new List<ITargetable>()));
    }

    [Test]
    public void FindNearest_AllInvalid_ReturnsNull()
    {
        List<ITargetable> candidates = new List<ITargetable>
        {
            new StubTarget(_outOfRange),
            new StubTarget(_backNear, false),
        };

        Assert.IsNull(WeaponTargeting.FindNearest(_origin, RANGE, candidates));
    }

    [Test]
    public void FindNearest_DifferentRangeWeapons_DecideTargetsIndependently()
    {
        WeaponDefinition shortWeapon = CreateDefinition(SHORT_RANGE);
        WeaponDefinition longWeapon = CreateDefinition(LONG_RANGE);
        StubTarget far = new StubTarget(_outOfRange);
        List<ITargetable> candidates = new List<ITargetable> { far };

        ITargetable shortResult = WeaponTargeting.FindNearest(_origin, shortWeapon.DetectionRange, candidates);
        ITargetable longResult = WeaponTargeting.FindNearest(_origin, longWeapon.DetectionRange, candidates);

        Assert.IsNull(shortResult);
        Assert.AreSame(far, longResult);

        Object.DestroyImmediate(shortWeapon);
        Object.DestroyImmediate(longWeapon);
    }

    [Test]
    public void FindNearest_IgnoresHeightDifference()
    {
        StubTarget high = new StubTarget(_highNear);
        StubTarget low = new StubTarget(_lowFar);

        ITargetable result = WeaponTargeting.FindNearest(_origin, RANGE, new List<ITargetable> { low, high });

        Assert.AreSame(high, result);
    }

    [Test]
    public void GetAimDirection_IsFlatAndNormalized()
    {
        Vector3 direction = WeaponTargeting.GetAimDirection(_muzzle, _aimPoint);

        Assert.AreEqual(0f, direction.y);
        Assert.AreEqual(1f, direction.magnitude, TOLERANCE);
    }

    [Test]
    public void GetFireDirection_MuzzleOverlapsTarget_UsesPlayerCenterDirection()
    {
        Vector3 expected = Vector3.ProjectOnPlane(_aimPoint - _origin, Vector3.up).normalized;

        Vector3 direction = WeaponTargeting.GetFireDirection(_overlappedMuzzle, _origin, _aimPoint);

        // Vector3.Angle은 영벡터에도 0을 반환하므로 성분으로 비교
        Assert.AreEqual(expected.x, direction.x, TOLERANCE);
        Assert.AreEqual(0f, direction.y, TOLERANCE);
        Assert.AreEqual(expected.z, direction.z, TOLERANCE);
    }

    [Test]
    public void GetSpreadDirection_SingleShot_ReturnsCenter()
    {
        Vector3 center = Vector3.ProjectOnPlane(_spreadCenter, Vector3.up).normalized;

        Vector3 direction = WeaponTargeting.GetSpreadDirection(_spreadCenter, SPREAD_ANGLE, SINGLE_SHOT, 0);

        Assert.AreEqual(0f, Vector3.Angle(center, direction), ANGLE_TOLERANCE);
    }

    [Test]
    public void GetSpreadDirection_FivePellets_EvenlySpacedIncludingEdges()
    {
        Vector3 center = Vector3.ProjectOnPlane(_spreadCenter, Vector3.up).normalized;

        for (int i = 0; i < PELLET_COUNT; i++)
        {
            Vector3 direction = WeaponTargeting.GetSpreadDirection(_spreadCenter, SPREAD_ANGLE, PELLET_COUNT, i);

            Assert.AreEqual(_expectedPelletAngles[i], Vector3.SignedAngle(center, direction, Vector3.up), ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetSpreadDirection_ZeroSpread_AllCenter()
    {
        Vector3 center = Vector3.ProjectOnPlane(_spreadCenter, Vector3.up).normalized;

        for (int i = 0; i < PELLET_COUNT; i++)
        {
            Vector3 direction = WeaponTargeting.GetSpreadDirection(_spreadCenter, NO_SPREAD, PELLET_COUNT, i);

            Assert.AreEqual(0f, Vector3.Angle(center, direction), ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetSpreadDirection_IsFlatAndNormalized()
    {
        for (int i = 0; i < PELLET_COUNT; i++)
        {
            Vector3 direction = WeaponTargeting.GetSpreadDirection(_spreadCenter, SPREAD_ANGLE, PELLET_COUNT, i);

            Assert.AreEqual(0f, direction.y, TOLERANCE);
            Assert.AreEqual(1f, direction.magnitude, TOLERANCE);
        }
    }

    [Test]
    public void GetRandomSpreadDirection_RatioEdgesAndMiddle_MapToSpreadEdgesAndCenter()
    {
        Vector3 center = Vector3.ProjectOnPlane(_spreadCenter, Vector3.up).normalized;

        Vector3 left = WeaponTargeting.GetRandomSpreadDirection(_spreadCenter, SPREAD_ANGLE, LEFT_EDGE_RATIO);
        Vector3 middle = WeaponTargeting.GetRandomSpreadDirection(_spreadCenter, SPREAD_ANGLE, MIDDLE_RATIO);
        Vector3 right = WeaponTargeting.GetRandomSpreadDirection(_spreadCenter, SPREAD_ANGLE, RIGHT_EDGE_RATIO);

        Assert.AreEqual(-HALF_SPREAD_ANGLE, Vector3.SignedAngle(center, left, Vector3.up), ANGLE_TOLERANCE);
        Assert.AreEqual(0f, Vector3.SignedAngle(center, middle, Vector3.up), ANGLE_TOLERANCE);
        Assert.AreEqual(HALF_SPREAD_ANGLE, Vector3.SignedAngle(center, right, Vector3.up), ANGLE_TOLERANCE);
    }

    [Test]
    public void GetRandomSpreadDirection_AnyRatio_StaysWithinSpread()
    {
        Vector3 center = Vector3.ProjectOnPlane(_spreadCenter, Vector3.up).normalized;

        for (int i = 0; i < _randomRatios.Length; i++)
        {
            Vector3 direction = WeaponTargeting.GetRandomSpreadDirection(_spreadCenter, SPREAD_ANGLE, _randomRatios[i]);

            Assert.LessOrEqual(Vector3.Angle(center, direction), HALF_SPREAD_ANGLE + ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetRandomSpreadDirection_ZeroSpread_ReturnsCenter()
    {
        Vector3 center = Vector3.ProjectOnPlane(_spreadCenter, Vector3.up).normalized;

        for (int i = 0; i < _randomRatios.Length; i++)
        {
            Vector3 direction = WeaponTargeting.GetRandomSpreadDirection(_spreadCenter, NO_SPREAD, _randomRatios[i]);

            Assert.AreEqual(0f, Vector3.Angle(center, direction), ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetRandomSpreadDirection_IsFlatAndNormalized()
    {
        for (int i = 0; i < _randomRatios.Length; i++)
        {
            Vector3 direction = WeaponTargeting.GetRandomSpreadDirection(_spreadCenter, SPREAD_ANGLE, _randomRatios[i]);

            Assert.AreEqual(0f, direction.y, TOLERANCE);
            Assert.AreEqual(1f, direction.magnitude, TOLERANCE);
        }
    }

    private static WeaponDefinition CreateDefinition(float detectionRange)
    {
        WeaponDefinition definition = ScriptableObject.CreateInstance<WeaponDefinition>();

        // 런타임 API에 설정 메서드를 두지 않기 위해 직렬화 필드에 직접 값 주입
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_detectionRange").floatValue = detectionRange;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

        return definition;
    }
}
