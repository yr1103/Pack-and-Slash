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
    private const float EXPERIENCE_ROUNDING_TOLERANCE = 0.0001f; // 소수 누적의 float 오차로 정수 직전에서 내림되는 것 방지

    [SerializeField, Min(0f)] private float _collectRadius = 0.4f; // 기본 젬 수집 반경(XZ, 배율 적용 전), 초기값 = CharacterController 반지름 (스펙 "초기 크기 = 플레이어 몸")

    private int _experience;
    private int _level = 1;
    private int _currentLevelExperience;
    private float _experienceMultiplier = 1f;
    private float _experienceRemainder; // 배율 적용 후 지급하지 못한 소수 경험치 (정수가 되면 지급)
    private float _collectRadiusMultiplier = 1f;

    /// <summary>배율이 적용된 현재 젬 수집 반경</summary>
    public float CollectRadius => _collectRadius * _collectRadiusMultiplier;
    public int Experience => _experience;
    public int Level => _level;
    public int CurrentLevelExperience => _currentLevelExperience; // 현재 레벨에서 모은 경험치 (레벨업 시 요구치만큼 차감)

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 경험치</summary>
    public int RequiredExperience => GetRequiredExperience(_level);

    /// <summary>경험치가 추가될 때마다 발생, 인자는 배율 적용 후 이번 추가량 (사운드·UI 구독 지점, 같은 추가의 LevelChanged 이후 발생)</summary>
    public event Action<int> ExperienceGained;

    /// <summary>레벨업 1회마다 발생, 인자는 새 레벨 (한 번에 여러 레벨이 오르면 레벨 수만큼 발생)</summary>
    public event Action<int> LevelChanged;

    /// <summary>해당 레벨에서 다음 레벨까지 필요한 경험치 (임시 곡선)</summary>
    /// <param name="level">1 이상의 레벨</param>
    public static int GetRequiredExperience(int level)
    {
        return BASE_REQUIRED_EXPERIENCE + REQUIRED_EXPERIENCE_PER_LEVEL * (level - 1);
    }

    /// <summary>경험치 획득량 배율 설정 (기본 1, 다음 AddExperience부터 적용)</summary>
    public void SetExperienceMultiplier(float multiplier)
    {
        _experienceMultiplier = multiplier;
    }

    /// <summary>젬 수집 반경 배율 설정 (기본 _collectRadius는 유지)</summary>
    public void SetCollectRadiusMultiplier(float multiplier)
    {
        _collectRadiusMultiplier = multiplier;
    }

    /// <summary>amount × 배율의 정수 부분만큼 누적·현재 레벨 경험치 증가 (소수는 누적 후 정수가 되면 지급), 요구치 이상이면 이월하며 레벨업 후 ExperienceGained 발생 (0 이하는 무시)</summary>
    /// <param name="amount">배율 적용 전 경험치</param>
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        float scaledAmount = amount * _experienceMultiplier + _experienceRemainder;
        int grantedAmount = Mathf.FloorToInt(scaledAmount + EXPERIENCE_ROUNDING_TOLERANCE);
        _experienceRemainder = Mathf.Max(0f, scaledAmount - grantedAmount);
        if (grantedAmount <= 0)
        {
            return;
        }

        _experience += grantedAmount;
        _currentLevelExperience += grantedAmount;
        while (_currentLevelExperience >= RequiredExperience)
        {
            _currentLevelExperience -= RequiredExperience;
            _level++;
            LevelChanged?.Invoke(_level);
        }

        ExperienceGained?.Invoke(grantedAmount);
    }
}
