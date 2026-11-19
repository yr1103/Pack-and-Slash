using System;
using UnityEngine;

/// <summary>
/// 플레이어 누적 경험치·레벨과 젬 수집 반경 관리 (경험치 추가 시 ExperienceGained, 레벨업 시 LevelChanged 통지)
/// </summary>
public class PlayerExperience : MonoBehaviour
{
    // shortcut: 임시 요구치 곡선 5 + 5×(레벨−1), 레벨 곡선 기획 확정 시 교체
    private const int BASE_REQUIRED_EXPERIENCE = 5;
    private const int REQUIRED_EXPERIENCE_PER_LEVEL = 5;

    [SerializeField, Min(0f)] private float _collectRadius = 0.4f; // 젬 수집 반경(XZ), 초기값 = CharacterController 반지름 (스펙 "초기 크기 = 플레이어 몸")

    private int _experience;
    private int _level = 1;
    private int _currentLevelExperience;

    public float CollectRadius => _collectRadius;
    public int Experience => _experience;
    public int Level => _level;
    public int CurrentLevelExperience => _currentLevelExperience; // 현재 레벨에서 모은 경험치 (레벨업 시 요구치만큼 차감)

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 경험치</summary>
    public int RequiredExperience => GetRequiredExperience(_level);

    /// <summary>경험치가 추가될 때마다 발생, 인자는 이번 추가량 (사운드·UI 구독 지점, 같은 추가의 LevelChanged 이후 발생)</summary>
    public event Action<int> ExperienceGained;

    /// <summary>레벨업 1회마다 발생, 인자는 새 레벨 (한 번에 여러 레벨이 오르면 레벨 수만큼 발생)</summary>
    public event Action<int> LevelChanged;

    /// <summary>해당 레벨에서 다음 레벨까지 필요한 경험치 (임시 곡선)</summary>
    /// <param name="level">1 이상의 레벨</param>
    public static int GetRequiredExperience(int level)
    {
        return BASE_REQUIRED_EXPERIENCE + REQUIRED_EXPERIENCE_PER_LEVEL * (level - 1);
    }

    /// <summary>누적·현재 레벨 경험치 증가, 요구치 이상이면 남는 경험치를 이월하며 레벨업 후 ExperienceGained 발생 (0 이하는 무시)</summary>
    /// <param name="amount">추가할 경험치</param>
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _experience += amount;
        _currentLevelExperience += amount;
        while (_currentLevelExperience >= RequiredExperience)
        {
            _currentLevelExperience -= RequiredExperience;
            _level++;
            LevelChanged?.Invoke(_level);
        }

        ExperienceGained?.Invoke(amount);
    }
}
