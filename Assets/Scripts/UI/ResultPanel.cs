using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 세션 종료 시 결과 패널 표시 (결과 문구·통계), 타이틀 복귀 버튼 처리
/// </summary>
public class ResultPanel : MonoBehaviour
{
    private const string TITLE_SCENE_NAME = "TitleScene";
    private const string CLEAR_TEXT = "CLEAR";
    private const string GAME_OVER_TEXT = "GAME OVER";
    private const string EVOLVED_SUFFIX = " (Evolved)";
    private const string WEAPON_FORMAT = "{0} Lv.{1}";
    private const string EMPTY_WEAPON_LIST = "-";
    private const string LINE_BREAK = "\n";
    private const string VALUE_POSITION = "<pos=50%>"; // 항목명과 값 정렬 위치
    private const string STATS_FORMAT =
        "Survival Time" + VALUE_POSITION + "{0}" + LINE_BREAK +
        "Level" + VALUE_POSITION + "{1}" + LINE_BREAK +
        "Weapons" + VALUE_POSITION + "{2}" + LINE_BREAK +
        "Kills" + VALUE_POSITION + "{3}" + LINE_BREAK +
        "Evolved" + VALUE_POSITION + "{4}";

    [SerializeField] private GameSession _gameSession;
    [SerializeField] private PlayerExperience _playerExperience;
    [SerializeField] private WeaponLoadout _weaponLoadout;
    [SerializeField] private GameObject _panelRoot; // 이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨
    [SerializeField] private TMP_Text _resultTitleText;
    [SerializeField] private TMP_Text _statsText;
    [SerializeField] private Button _titleButton;

    private void Awake()
    {
        _panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _gameSession.Ended += HandleEnded;
        _titleButton.onClick.AddListener(HandleTitleButtonClicked);
    }

    private void OnDisable()
    {
        _gameSession.Ended -= HandleEnded;
        _titleButton.onClick.RemoveListener(HandleTitleButtonClicked);
    }

    /// <summary>무기마다 "종류 Lv.레벨" (진화면 " (Evolved)" 추가)을 줄바꿈으로 연결, 빈 목록이면 "-"</summary>
    public static string BuildWeaponList(IReadOnlyList<WeaponInstance> weapons)
    {
        if (weapons.Count == 0)
        {
            return EMPTY_WEAPON_LIST;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < weapons.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(LINE_BREAK);
            }

            WeaponInstance weapon = weapons[i];
            builder.AppendFormat(WEAPON_FORMAT, weapon.Definition.WeaponType, weapon.Level);
            if (weapon.IsEvolved)
            {
                builder.Append(EVOLVED_SUFFIX);
            }
        }

        return builder.ToString();
    }

    /// <summary>진화 무기 수</summary>
    public static int CountEvolved(IReadOnlyList<WeaponInstance> weapons)
    {
        int count = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].IsEvolved)
            {
                count++;
            }
        }

        return count;
    }

    private void HandleEnded(bool cleared)
    {
        IReadOnlyList<WeaponInstance> weapons = _weaponLoadout.Weapons;
        // 무기 목록 두 번째 줄부터도 값 열에 맞춰 정렬
        string weaponList = BuildWeaponList(weapons).Replace(LINE_BREAK, LINE_BREAK + VALUE_POSITION);

        _resultTitleText.text = cleared ? CLEAR_TEXT : GAME_OVER_TEXT;
        _statsText.text = string.Format(STATS_FORMAT,
            GameSession.FormatTime(_gameSession.ElapsedTime),
            _playerExperience.Level,
            weaponList,
            _gameSession.KillCount,
            CountEvolved(weapons));
        _panelRoot.SetActive(true);
    }

    private void HandleTitleButtonClicked()
    {
        SceneManager.LoadScene(TITLE_SCENE_NAME);
    }
}
