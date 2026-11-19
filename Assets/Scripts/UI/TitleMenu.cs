using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 타이틀 메뉴의 설정 판넬 열고 닫기, 게임 종료 확인창 처리 (ESC로 열린 창을 하나씩 닫음)
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [SerializeField] private Button _settingsButton;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private Button _settingsCloseButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private GameObject _quitDialog;
    [SerializeField] private Button _quitYesButton;
    [SerializeField] private Button _quitNoButton;
    [SerializeField] private InputActionReference _cancelAction; // UI/Cancel

    // Scene마다 이 액션을 쓰는 컴포넌트가 메뉴 스크립트 하나뿐이라 직접 Enable/Disable 소유
    private void OnEnable()
    {
        _settingsButton.onClick.AddListener(OpenSettings);
        _settingsCloseButton.onClick.AddListener(CloseSettings);
        _quitButton.onClick.AddListener(OpenQuitDialog);
        _quitYesButton.onClick.AddListener(Quit);
        _quitNoButton.onClick.AddListener(CloseQuitDialog);
        _cancelAction.action.performed += HandleCancelPerformed;
        _cancelAction.action.Enable();
    }

    private void OnDisable()
    {
        _settingsButton.onClick.RemoveListener(OpenSettings);
        _settingsCloseButton.onClick.RemoveListener(CloseSettings);
        _quitButton.onClick.RemoveListener(OpenQuitDialog);
        _quitYesButton.onClick.RemoveListener(Quit);
        _quitNoButton.onClick.RemoveListener(CloseQuitDialog);
        _cancelAction.action.performed -= HandleCancelPerformed;
        _cancelAction.action.Disable();
    }

    // 가장 위에 열린 창(확인창 → 설정 판넬) 하나만 닫고, 열린 창이 없으면 무시
    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (_quitDialog.activeSelf)
        {
            CloseQuitDialog();
        }
        else if (_settingsPanel.activeSelf)
        {
            CloseSettings();
        }
    }

    private void OpenSettings()
    {
        _settingsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        _settingsPanel.SetActive(false);
    }

    private void OpenQuitDialog()
    {
        _quitDialog.SetActive(true);
    }

    private void CloseQuitDialog()
    {
        _quitDialog.SetActive(false);
    }

    // 에디터에서는 Application.Quit이 무시되어 Play 모드가 유지됨
    private void Quit()
    {
        Application.Quit();
    }
}
