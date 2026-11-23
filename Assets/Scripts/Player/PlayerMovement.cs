using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Move 입력을 월드 XZ 평면 이동으로 변환 (회전은 PlayerAimController가 모델에 적용)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private float _moveSpeed = 5f; // 기본 이동속도 (배율 적용 전)

    private CharacterController _characterController;
    private float _moveSpeedMultiplier = 1f;

    /// <summary>배율이 적용된 현재 이동속도</summary>
    public float MoveSpeed => _moveSpeed * _moveSpeedMultiplier;

    /// <summary>이동속도 배율 설정 (기본 _moveSpeed는 유지, 매번 기본값 기준으로 적용)</summary>
    public void SetMoveSpeedMultiplier(float multiplier)
    {
        _moveSpeedMultiplier = multiplier;
    }

    private void Awake()
    {
        CacheComponents();
    }

    // 이 액션을 사용하는 컴포넌트가 이것 하나뿐이라 직접 Enable/Disable 소유
    private void OnEnable()
    {
        _moveAction.action.Enable();
    }

    private void OnDisable()
    {
        _moveAction.action.Disable();
    }

    private void Update()
    {
        Vector2 moveInput = _moveAction.action.ReadValue<Vector2>();
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        // 입력이 없어도 중력 적용을 위해 매 프레임 호출
        _characterController.SimpleMove(moveDirection * MoveSpeed);
    }

    private void CacheComponents()
    {
        _characterController = GetComponent<CharacterController>();
    }
}
