using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 표적 탐색과 논리 조준 계산 (상태 없음, 후보는 호출부가 공급)
/// </summary>
public static class WeaponTargeting
{
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
}
