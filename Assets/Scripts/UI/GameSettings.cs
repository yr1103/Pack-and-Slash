using UnityEngine;

/// <summary>
/// 게임 설정(볼륨·피격 효과·데미지 숫자) 보관, PlayerPrefs 저장·로드 (값은 정적 필드에 캐시해 읽음)
/// </summary>
public static class GameSettings
{
    private const string VOLUME_KEY = "Settings.Volume";
    private const string HIT_FLASH_KEY = "Settings.HitFlash";
    private const string DAMAGE_NUMBER_KEY = "Settings.DamageNumber";
    private const float DEFAULT_VOLUME = 1f;
    private const int TRUE_VALUE = 1; // bool은 PlayerPrefs에 int 0/1로 저장

    private static float _volume = DEFAULT_VOLUME;
    private static bool _hitFlashEnabled = true;
    private static bool _damageNumberEnabled = true;

    /// <summary>전체 볼륨 (0~1, 변경 즉시 AudioListener에 적용)</summary>
    public static float Volume
    {
        get => _volume;
        set
        {
            _volume = Mathf.Clamp01(value);
            AudioListener.volume = _volume;
            PlayerPrefs.SetFloat(VOLUME_KEY, _volume);
        }
    }

    /// <summary>적 피격 반짝임 표시 여부</summary>
    public static bool HitFlashEnabled
    {
        get => _hitFlashEnabled;
        set
        {
            _hitFlashEnabled = value;
            PlayerPrefs.SetInt(HIT_FLASH_KEY, ToInt(value));
        }
    }

    /// <summary>피해 숫자 표시 여부</summary>
    public static bool DamageNumberEnabled
    {
        get => _damageNumberEnabled;
        set
        {
            _damageNumberEnabled = value;
            PlayerPrefs.SetInt(DAMAGE_NUMBER_KEY, ToInt(value));
        }
    }

    /// <summary>변경된 설정을 디스크에 기록 (설정 판넬을 닫을 때 호출)</summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static int ToInt(bool value)
    {
        return value ? TRUE_VALUE : 0;
    }

    // 첫 Scene 로드 전에 저장값을 캐시하고 볼륨 적용 (피격마다 PlayerPrefs를 읽지 않도록)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        _volume = PlayerPrefs.GetFloat(VOLUME_KEY, DEFAULT_VOLUME);
        _hitFlashEnabled = PlayerPrefs.GetInt(HIT_FLASH_KEY, TRUE_VALUE) == TRUE_VALUE;
        _damageNumberEnabled = PlayerPrefs.GetInt(DAMAGE_NUMBER_KEY, TRUE_VALUE) == TRUE_VALUE;
        AudioListener.volume = _volume;
    }
}
