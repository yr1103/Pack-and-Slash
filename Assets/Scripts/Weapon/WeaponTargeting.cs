using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 표적 탐색과 논리 조준 계산 (상태 없음, 후보는 호출부가 공급)
/// </summary>
public static class WeaponTargeting
{
    private const float HALF_RATIO = 0.5f;

    /// <summary>XZ 평면 기준 반경 안에서 공격 가능한 최근접 표적 1개 탐색</summary>
    /// <param name="origin">탐색 기준 위치 (플레이어 루트)</param>
    /// <param name="range">탐색 반경</param>
    /// <param name="candidates">표적 후보 목록 (후보가 없으면 null이 아닌 빈 목록, 파괴되지 않은 적만 담는 것은 공급하는 쪽이 보장)</param>
    /// <returns>최근접 표적 (거리가 같으면 SpawnId가 낮은 표적), 없으면 null</returns>
    public static ITargetable FindNearest(Vector3 origin, float range, IReadOnlyList<ITargetable> candidates)
    {
        // 제곱거리로 비교해 제곱근 계산 생략
        float rangeSqr = range * range;
        ITargetable nearest = null;
        float nearestSqrDistance = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            ITargetable candidate = candidates[i];
            if (!candidate.IsTargetable)
            {
                continue;
            }

            Vector3 offset = Vector3.ProjectOnPlane(candidate.AimPoint - origin, Vector3.up);
            float sqrDistance = offset.sqrMagnitude;

            // 반경 경계 포함 (거리 == range도 탐색 대상)
            if (sqrDistance > rangeSqr)
            {
                continue;
            }

            bool isCloser = nearest == null || sqrDistance < nearestSqrDistance;
            // 동일 거리면 먼저 생성된(SpawnId 낮은) 적 우선, 목록 순서와 무관
            bool winsTie = nearest != null && sqrDistance == nearestSqrDistance && candidate.SpawnId < nearest.SpawnId;
            if (isCloser || winsTie)
            {
                nearest = candidate;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearest;
    }

    /// <summary>총구에서 조준 위치로 향하는 XZ 평면 정규화 방향 (y = 0)</summary>
    /// <param name="muzzlePosition">총구 위치</param>
    /// <param name="aimPoint">표적 조준 위치</param>
    public static Vector3 GetAimDirection(Vector3 muzzlePosition, Vector3 aimPoint)
    {
        return Vector3.ProjectOnPlane(aimPoint - muzzlePosition, Vector3.up).normalized;
    }

    /// <summary>발사 기준 XZ 방향 (총구→표적, 총구와 표적이 겹치면 플레이어 중심→표적)</summary>
    /// <param name="muzzlePosition">총구 위치</param>
    /// <param name="playerPosition">플레이어 루트 위치</param>
    /// <param name="aimPoint">표적 조준 위치</param>
    /// <returns>정규화 방향, 둘 다 겹치면 영벡터</returns>
    public static Vector3 GetFireDirection(Vector3 muzzlePosition, Vector3 playerPosition, Vector3 aimPoint)
    {
        Vector3 muzzleDirection = GetAimDirection(muzzlePosition, aimPoint);
        // 적이 총구에 파고들면 총구 기준 방향이 사라지므로 플레이어 중심 기준으로 대체
        if (muzzleDirection != Vector3.zero)
        {
            return muzzleDirection;
        }

        return GetAimDirection(playerPosition, aimPoint);
    }

    /// <summary>부채꼴 안 index번째 탄의 XZ 평면 정규화 방향 (양 끝 포함 균등 간격, 1발이면 중앙)</summary>
    /// <param name="centerDirection">부채꼴 중심 방향</param>
    /// <param name="spreadAngle">부채꼴 전체 각도(도)</param>
    /// <param name="count">한 번에 발사하는 탄 수</param>
    /// <param name="index">0부터 count - 1까지의 탄 순번</param>
    public static Vector3 GetSpreadDirection(Vector3 centerDirection, float spreadAngle, int count, int index)
    {
        Vector3 planarCenter = Vector3.ProjectOnPlane(centerDirection, Vector3.up).normalized;
        // 1발 이하는 간격 계산 시 0으로 나누게 되므로 중앙 고정
        if (count <= 1)
        {
            return planarCenter;
        }

        float halfSpread = spreadAngle * HALF_RATIO;
        float angle = Mathf.Lerp(-halfSpread, halfSpread, (float)index / (count - 1));
        return Quaternion.AngleAxis(angle, Vector3.up) * planarCenter;
    }
}
