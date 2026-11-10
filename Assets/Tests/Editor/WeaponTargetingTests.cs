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
    private const int DEFAULT_SPAWN_ID = 0;
    private const int LOW_SPAWN_ID = 1;
    private const int HIGH_SPAWN_ID = 2;

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
