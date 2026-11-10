using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Move 입력을 월드 XZ 평면 이동과 이동 방향 회전으로 변환
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 720f; // 초당 회전 각도(도)

    private CharacterController _characterController;

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
        _characterController.SimpleMove(moveDirection * _moveSpeed);

        RotateTowards(moveDirection);
    }

    private void RotateTowards(Vector3 moveDirection)
    {
        // 입력이 없을 때 LookRotation(zero) 경고 방지 및 마지막 방향 유지
        if (moveDirection == Vector3.zero)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
    }

    private void CacheComponents()
    {
        _characterController = GetComponent<CharacterController>();
    }
}
