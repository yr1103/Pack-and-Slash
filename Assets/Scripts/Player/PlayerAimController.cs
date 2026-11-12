using UnityEngine;

/// <summary>
/// 모델 몸통을 주무기 표적 방향, 표적이 없으면 이동 방향으로 회전 (발사는 회전 완료를 기다리지 않음)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAimController : MonoBehaviour
{
    [SerializeField] private Transform _visualRoot; // 회전할 모델 (CowboyDefault)
    [SerializeField] private WeaponLoadout _loadout;
    [SerializeField] private float _turnSpeed = 720f; // 초당 회전 각도(도)

    private CharacterController _characterController;

    private void Awake()
    {
        CacheComponents();
    }

    // 움직이는 표적·이동 방향을 따라가야 해서 매 프레임 폴링
    // WeaponAttackController.Update의 같은 프레임 표적 탐색 이후에 읽도록 LateUpdate 사용
    private void LateUpdate()
    {
        Vector3 lookDirection = GetLookDirection();
        // 표적도 이동도 없으면 LookRotation(zero) 경고 방지 및 마지막 방향 유지
        if (lookDirection == Vector3.zero)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
        // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
        _visualRoot.rotation = Quaternion.RotateTowards(_visualRoot.rotation, targetRotation, _turnSpeed * Time.deltaTime);
    }

    // 주무기 표적 우선, 없으면 이동 방향 (둘 다 없으면 zero)
    private Vector3 GetLookDirection()
    {
        WeaponInstance primary = _loadout.Primary;
        if (primary != null && primary.CurrentTarget != null)
        {
            return WeaponTargeting.GetAimDirection(_visualRoot.position, primary.CurrentTarget.AimPoint);
        }

        return Vector3.ProjectOnPlane(_characterController.velocity, Vector3.up);
    }

    private void CacheComponents()
    {
        _characterController = GetComponent<CharacterController>();
    }
}
