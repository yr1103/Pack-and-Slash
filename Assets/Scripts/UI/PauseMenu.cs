using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전투 중 ESC 일시정지 메뉴 (설정 판넬을 열며 일시정지, 닫으며 재개, 타이틀 복귀 확인창 처리)
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private const string TITLE_SCENE_NAME = "TitleScene";

    [SerializeField] private GameSession _gameSession;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private Button _settingsCloseButton;
    [SerializeField] private Button _returnTitleButton;
    [SerializeField] private GameObject _returnDialog;
    [SerializeField] private Button _returnYesButton;
    [SerializeField] private Button _returnNoButton;
    [SerializeField] private InputActionReference _cancelAction; // UI/Cancel

    // Scene마다 이 액션을 쓰는 컴포넌트가 메뉴 스크립트 하나뿐이라 직접 Enable/Disable 소유
    private void OnEnable()
    {
        _settingsCloseButton.onClick.AddListener(CloseSettingsAndResume);
        _returnTitleButton.onClick.AddListener(OpenReturnDialog);
        _returnYesButton.onClick.AddListener(ReturnToTitle);
        _returnNoButton.onClick.AddListener(CloseReturnDialog);
        _cancelAction.action.performed += HandleCancelPerformed;
        _cancelAction.action.Enable();
    }

    private void OnDisable()
    {
        _settingsCloseButton.onClick.RemoveListener(CloseSettingsAndResume);
        _returnTitleButton.onClick.RemoveListener(OpenReturnDialog);
        _returnYesButton.onClick.RemoveListener(ReturnToTitle);
        _returnNoButton.onClick.RemoveListener(CloseReturnDialog);
        _cancelAction.action.performed -= HandleCancelPerformed;
        _cancelAction.action.Disable();
    }

    // 종료 연출·결과 판넬 중에는 무시, 가장 위에 열린 창(확인창 → 설정 판넬)부터 닫고 열린 창이 없으면 일시정지
    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (!_gameSession.IsPlaying)
        {
            return;
        }

        if (_returnDialog.activeSelf)
        {
            CloseReturnDialog();
        }
        else if (_settingsPanel.activeSelf)
        {
            CloseSettingsAndResume();
        }
        else
        {
            _gameSession.Pause();
            _settingsPanel.SetActive(true);
        }
    }

    private void CloseSettingsAndResume()
    {
        _settingsPanel.SetActive(false);
        _gameSession.Resume();
    }

    private void OpenReturnDialog()
    {
        _returnDialog.SetActive(true);
    }

    private void CloseReturnDialog()
    {
        _returnDialog.SetActive(false);
    }

    // timeScale 복구는 Scene 전환 시 GameSession.OnDestroy가 담당
    private void ReturnToTitle()
    {
        SceneManager.LoadScene(TITLE_SCENE_NAME);
    }
}
