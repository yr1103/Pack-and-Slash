using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 시 일시정지하고 보상 화면 표시, 쌓인 레벨업 횟수만큼 완료 버튼으로 넘긴 뒤 닫으며 재개
/// </summary>
public class LevelUpPanel : MonoBehaviour
{
    [SerializeField] private GameSession _gameSession;
    [SerializeField] private PlayerExperience _playerExperience;
    [SerializeField] private GameObject _panelRoot; // 이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨
    [SerializeField] private Button _completeButton;
    [SerializeField] private GameObject[] _rewardCards;
    // shortcut: 3(레벨업)/5(보물상자) 레이아웃 확인용 임시 값, 보상·보물상자 구현 시 호출부에서 개수 지정으로 교체
    [SerializeField] private int _previewCardCount = 3;

    private int _pendingCount; // 아직 처리하지 않은 레벨업 횟수

    /// <summary>레벨업 화면 표시 여부 (열려 있는 동안 ESC 일시정지 무시용)</summary>
    public bool IsOpen => _panelRoot.activeSelf;

    private void Awake()
    {
        _panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _playerExperience.LevelChanged += HandleLevelChanged;
        _completeButton.onClick.AddListener(HandleCompleteClicked);
    }

    private void OnDisable()
    {
        _playerExperience.LevelChanged -= HandleLevelChanged;
        _completeButton.onClick.RemoveListener(HandleCompleteClicked);
    }

    // 한 번에 여러 레벨이 오르면 이벤트가 연속 발생하므로 횟수만 쌓고 화면은 한 번만 연다
    private void HandleLevelChanged(int level)
    {
        _pendingCount++;
        if (!IsOpen)
        {
            Open();
        }
    }

    private void Open()
    {
        _gameSession.Pause();
        for (int i = 0; i < _rewardCards.Length; i++)
        {
            _rewardCards[i].SetActive(i < _previewCardCount);
        }

        _panelRoot.SetActive(true);
    }

    // 남은 레벨업이 있으면 화면 유지 (다음 회차, 현재는 갱신할 보상 내용 없음)
    private void HandleCompleteClicked()
    {
        _pendingCount--;
        if (_pendingCount == 0)
        {
            _panelRoot.SetActive(false);
            _gameSession.Resume();
        }
    }
}
