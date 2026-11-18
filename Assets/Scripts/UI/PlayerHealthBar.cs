using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 머리 위 World Space 체력바 (피해 시 갱신, 카메라와 같은 회전으로 빌보드)
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private Image _fill; // Image Type = Filled
    [SerializeField] private Camera _camera;

    private void OnEnable()
    {
        _playerHealth.Damaged += Refresh;
    }

    // PlayerHealth.Awake에서 체력이 초기화된 뒤 읽도록 Start에서 갱신
    private void Start()
    {
        Refresh();
    }

    // 플레이어 이동·회전 후 카메라 방향에 맞추도록 LateUpdate에서 처리
    private void LateUpdate()
    {
        transform.rotation = _camera.transform.rotation;
    }

    private void OnDisable()
    {
        _playerHealth.Damaged -= Refresh;
    }

    private void Refresh()
    {
        _fill.fillAmount = _playerHealth.CurrentHealth / _playerHealth.MaxHealth;
    }
}
