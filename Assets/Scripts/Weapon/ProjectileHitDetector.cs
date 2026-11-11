using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체 구간 SphereCast 적중 후보 탐지 (같은 적 중복 제외, 고유 적중 한도 판정)
/// </summary>
public class ProjectileHitDetector
{
    // 한 구간에서 받는 최대 Collider 수
    // SphereCastNonAlloc은 결과 순서를 보장하지 않아, 초과 시 어떤 Collider가 빠질지 알 수 없음 (가장 가까운 적이 빠질 수 있음)
    private const int MAX_CAST_HITS = 16;

    private static readonly IComparer<RaycastHit> _distanceComparer =
        Comparer<RaycastHit>.Create((left, right) => left.distance.CompareTo(right.distance));

    private readonly RaycastHit[] _castHits = new RaycastHit[MAX_CAST_HITS];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly float _hitRadius;
    private readonly LayerMask _hitMask;
    private int _maxUniqueTargetsHit;

    /// <summary>이번 발사의 고유 적중 수가 한도에 도달했는지 여부</summary>
    public bool HasReachedLimit => _hitTargets.Count >= _maxUniqueTargetsHit;

    /// <param name="hitRadius">SphereCast 반지름</param>
    /// <param name="hitMask">검사할 레이어</param>
    public ProjectileHitDetector(float hitRadius, LayerMask hitMask)
    {
        _hitRadius = hitRadius;
        _hitMask = hitMask;
    }

    /// <summary>발사마다 호출, 이전 발사의 적중 기록 초기화</summary>
    /// <param name="maxUniqueTargetsHit">이번 발사의 고유 적중 한도</param>
    public void ResetForLaunch(int maxUniqueTargetsHit)
    {
        _hitTargets.Clear();
        _maxUniqueTargetsHit = maxUniqueTargetsHit;
    }

    /// <summary>구간 SphereCast 후 가까운 순으로 정렬 (결과는 다음 Cast 전까지 유효)</summary>
    /// <returns>검출된 Collider 수</returns>
    public int Cast(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.SphereCastNonAlloc(origin, _hitRadius, direction, _castHits, distance,
            _hitMask, QueryTriggerInteraction.Ignore);
        // NonAlloc 결과는 순서 보장이 없어 가까운 적부터 처리하도록 정렬
        System.Array.Sort(_castHits, 0, hitCount, _distanceComparer);
        return hitCount;
    }

    /// <summary>Cast 결과 index번째가 처음 맞는 피해 대상이면 기록 후 반환</summary>
    /// <param name="hitDistance">Cast 시작점부터 적중 지점까지 거리</param>
    /// <returns>IDamageable이 없는 Collider이거나 살아 있지 않거나 이미 맞힌 적이면 false</returns>
    public bool TryGetNewTarget(int index, out IDamageable target, out float hitDistance)
    {
        hitDistance = _castHits[index].distance;
        target = _castHits[index].collider.GetComponentInParent<IDamageable>();
        return target != null && TryRecord(target);
    }

    /// <summary>처음 맞는 적이면 기록 (한 적의 Collider 여러 개·연속 프레임 접촉을 1회로 제한, 참조 기준)</summary>
    /// <returns>살아 있지 않거나 이미 기록된 적이면 false</returns>
    public bool TryRecord(IDamageable target)
    {
        // 죽은 적은 기록하지 않아 한도를 소모하지 않고 탄이 통과
        return target.IsAlive && _hitTargets.Add(target);
    }
}
