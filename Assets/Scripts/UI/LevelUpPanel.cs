using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업·보물 상자 보상 요청 시 일시정지하고 보상 화면 표시, 완료 버튼은 표시 중인 회차만 처리하고 다음 회차는 레벨업(카드 3장) 우선 후 보물 상자(카드 5장), 모두 끝나면 닫으며 재개
/// </summary>
public class LevelUpPanel : MonoBehaviour
{
    [SerializeField] private GameSession _gameSession;
    [SerializeField] private PlayerExperience _playerExperience;
    [SerializeField] private GameObject _panelRoot; // 이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨
    [SerializeField] private Button _completeButton;
    [SerializeField] private GameObject[] _rewardCards;
    [SerializeField, Min(1)] private int _levelUpCardCount = 3; // 레벨업 회차 표시 카드 수
    [SerializeField, Min(1)] private int _chestCardCount = 5; // 보물 상자 회차 표시 카드 수

    private int _pendingCount; // 아직 처리하지 않은 레벨업 횟수
    private int _pendingChestCount; // 아직 처리하지 않은 보물 상자 보상 횟수
    private bool _isShowingChest; // 현재 표시 중인 회차가 보물 상자인지 (완료 시 차감 대상)

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

    /// <summary>보물 상자 보상 1회 대기열에 추가 (화면이 닫혀 있으면 열고, 대기 중인 레벨업이 있으면 그 뒤에 표시)</summary>
    public void RequestChestReward()
    {
        _pendingChestCount++;
        if (!IsOpen)
        {
            Open();
        }
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
        RefreshRewardCards();
        _panelRoot.SetActive(true);
    }

    // 표시 중인 회차 종류의 횟수만 차감 (표시 중 도착한 다른 종류 요청이 잘못 차감되지 않도록), 남은 회차가 있으면 다음 회차 표시
    private void HandleCompleteClicked()
    {
        if (_isShowingChest)
        {
            _pendingChestCount--;
        }
        else
        {
            _pendingCount--;
        }

        if (_pendingCount == 0 && _pendingChestCount == 0)
        {
            _panelRoot.SetActive(false);
            _gameSession.Resume();
            return;
        }

        RefreshRewardCards();
    }

    // 다음 회차 종류(레벨업 우선)를 정하고 그 개수만큼 카드 활성화
    private void RefreshRewardCards()
    {
        _isShowingChest = _pendingCount == 0;
        int cardCount = _isShowingChest ? _chestCardCount : _levelUpCardCount;
        for (int i = 0; i < _rewardCards.Length; i++)
        {
            _rewardCards[i].SetActive(i < cardCount);
        }
    }
}
