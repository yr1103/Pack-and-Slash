using UnityEngine;

/// <summary>
/// 이동 방향(모델 정면 기준)과 주무기 사격 여부를 매 프레임 Animator 파라미터로 설정
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimator : MonoBehaviour
{
    private const float STOP_SPEED_THRESHOLD = 0.1f; // 이 속도(m/s) 이하는 정지로 판정

    private static readonly int _moveXHash = Animator.StringToHash("MoveX");
    private static readonly int _moveZHash = Animator.StringToHash("MoveZ");
    private static readonly int _isShootingHash = Animator.StringToHash("IsShooting");

    [SerializeField] private Animator _animator; // CowboyDefault의 Animator
    [SerializeField] private Transform _visualRoot; // 몸통 회전 대상 모델 (CowboyDefault)
    [SerializeField] private WeaponLoadout _loadout;

    private CharacterController _characterController;

    private void Awake()
    {
        CacheComponents();
    }

    // Animator는 Update 이후·LateUpdate 이전에 평가되므로 Update에서 설정해야 같은 프레임에 반영 (표적·몸통 회전은 최대 1프레임 이전 값)
    // 주무기 변경 시 Controller가 교체되어 파라미터가 초기화되므로 변경 여부와 무관하게 매 프레임 설정
    private void Update()
    {
        Vector2 moveDirection = GetLocalMoveDirection(_characterController.velocity, _visualRoot.rotation, STOP_SPEED_THRESHOLD);
        _animator.SetFloat(_moveXHash, moveDirection.x);
        _animator.SetFloat(_moveZHash, moveDirection.y);
        _animator.SetBool(_isShootingHash, IsShooting());
    }

    /// <summary>월드 속도를 모델 정면 기준 로컬 이동 방향으로 변환 (Y 성분 무시)</summary>
    /// <param name="stopThreshold">이 속도 이하는 정지로 판정</param>
    /// <returns>x=로컬 오른쪽, y=로컬 정면인 길이 1 벡터, 정지면 zero</returns>
    public static Vector2 GetLocalMoveDirection(Vector3 worldVelocity, Quaternion modelRotation, float stopThreshold)
    {
        Vector3 flatVelocity = Vector3.ProjectOnPlane(worldVelocity, Vector3.up);
        if (flatVelocity.sqrMagnitude <= stopThreshold * stopThreshold)
        {
            return Vector2.zero;
        }

        Vector3 localVelocity = Quaternion.Inverse(modelRotation) * flatVelocity;
        return new Vector2(localVelocity.x, localVelocity.z).normalized;
    }

    // 재장전 중에는 발사하지 않으므로 사격 자세 해제
    private bool IsShooting()
    {
        WeaponInstance primary = _loadout.Primary;
        return primary != null && primary.CurrentTarget != null && !primary.IsReloading;
    }

    private void CacheComponents()
    {
        _characterController = GetComponent<CharacterController>();
    }
}
