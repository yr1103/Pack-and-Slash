using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD 표시 (생존 시간·처치 수는 값이 바뀔 때만, 경험치 바·레벨은 PlayerExperience 이벤트로, 재화는 Currency.Changed 이벤트로 갱신)
/// </summary>
public class GameHUD : MonoBehaviour
{
    private const string LEVEL_FORMAT = "LV {0}";

    [SerializeField] private GameSession _gameSession;
    [SerializeField] private PlayerExperience _playerExperience;
    [SerializeField] private Currency _currency;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _killCountText;
    [SerializeField] private TMP_Text _currencyText;
    [SerializeField] private Image _experienceFill; // Image Type = Filled

    // 첫 Update에서 반드시 갱신되도록 나올 수 없는 값으로 시작
    private int _shownSeconds = -1;
    private int _shownKillCount = -1;

    private void OnEnable()
    {
        _playerExperience.ExperienceGained += HandleExperienceGained;
        _currency.Changed += RefreshCurrency;
    }

    private void Start()
    {
        RefreshCurrency(_currency.Balance);
        RefreshExperience();
    }

    // 시간은 매 프레임 흐르므로 폴링, 텍스트 할당은 표시 값이 바뀔 때만
    private void Update()
    {
        int seconds = Mathf.FloorToInt(_gameSession.ElapsedTime);
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _timerText.text = GameSession.FormatTime(_gameSession.ElapsedTime);
        }

        // GameSession에 처치 수 변경 이벤트가 없어 폴링
        int killCount = _gameSession.KillCount;
        if (killCount != _shownKillCount)
        {
            _shownKillCount = killCount;
            _killCountText.text = killCount.ToString();
        }
    }

    private void OnDisable()
    {
        _playerExperience.ExperienceGained -= HandleExperienceGained;
        _currency.Changed -= RefreshCurrency;
    }

    // 레벨업 시에도 LevelChanged 이후 발생하므로 이 이벤트만으로 경험치 바·레벨 갱신
    private void HandleExperienceGained(int amount)
    {
        RefreshExperience();
    }

    private void RefreshExperience()
    {
        _experienceFill.fillAmount = (float)_playerExperience.CurrentLevelExperience / _playerExperience.RequiredExperience;
        _levelText.text = string.Format(LEVEL_FORMAT, _playerExperience.Level);
    }

    private void RefreshCurrency(int balance)
    {
        _currencyText.text = balance.ToString();
    }
}
