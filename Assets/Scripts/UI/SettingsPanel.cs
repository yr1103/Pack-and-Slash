using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 판넬 UI와 GameSettings 동기화 (열릴 때 현재 값 표시, 변경 즉시 반영, 닫힐 때 저장, 열고 닫기는 메뉴 스크립트 담당)
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider _volumeSlider; // 범위 0~1
    [SerializeField] private Toggle _hitFlashToggle;
    [SerializeField] private Toggle _damageNumberToggle;

    // 현재 값 표시는 알림 없이 갱신해 열 때마다 설정을 다시 기록하지 않음
    private void OnEnable()
    {
        _volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        _hitFlashToggle.SetIsOnWithoutNotify(GameSettings.HitFlashEnabled);
        _damageNumberToggle.SetIsOnWithoutNotify(GameSettings.DamageNumberEnabled);

        _volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
        _hitFlashToggle.onValueChanged.AddListener(HandleHitFlashChanged);
        _damageNumberToggle.onValueChanged.AddListener(HandleDamageNumberChanged);
    }

    private void OnDisable()
    {
        _volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
        _hitFlashToggle.onValueChanged.RemoveListener(HandleHitFlashChanged);
        _damageNumberToggle.onValueChanged.RemoveListener(HandleDamageNumberChanged);

        GameSettings.Save();
    }

    private void HandleVolumeChanged(float value)
    {
        GameSettings.Volume = value;
    }

    private void HandleHitFlashChanged(bool isOn)
    {
        GameSettings.HitFlashEnabled = isOn;
    }

    private void HandleDamageNumberChanged(bool isOn)
    {
        GameSettings.DamageNumberEnabled = isOn;
    }
}
