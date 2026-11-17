using UnityEngine;

/// <summary>
/// 주무기가 아닌 장착 무기의 부유 외형 (슬롯 이동·표적 조준, 속도와 시간은 호출부가 공급)
/// </summary>
public class FloatingWeaponView : MonoBehaviour
{
    [SerializeField] private Transform _muzzle; // 조준점

    public Transform Muzzle => _muzzle;

    /// <summary>슬롯 위치를 향해 최대 maxDistance만큼 이동</summary>
    /// <param name="slotPosition">목표 슬롯 월드 위치</param>
    /// <param name="maxDistance">이번 호출의 최대 이동 거리</param>
    public void MoveTowardsSlot(Vector3 slotPosition, float maxDistance)
    {
        transform.position = Vector3.MoveTowards(transform.position, slotPosition, maxDistance);
    }

    /// <summary>표적 방향(XZ)으로 최대 maxAngle만큼 회전 (표적이 없거나 위치가 겹치면 현재 방향 유지)</summary>
    /// <param name="target">이 무기의 현재 표적, 없으면 null</param>
    /// <param name="maxAngle">이번 호출의 최대 회전 각도(도)</param>
    public void TurnTowards(ITargetable target, float maxAngle)
    {
        if (target == null)
        {
            return;
        }

        Vector3 aimDirection = WeaponTargeting.GetAimDirection(transform.position, target.AimPoint);
        // 겹침으로 방향이 사라지면 LookRotation(zero) 경고 방지
        if (aimDirection == Vector3.zero)
        {
            return;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDirection), maxAngle);
    }
}