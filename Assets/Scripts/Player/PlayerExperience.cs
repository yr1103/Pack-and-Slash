using System;
using UnityEngine;

/// <summary>
/// 플레이어 누적 경험치와 젬 수집 반경 관리 (경험치 추가 시 ExperienceGained 통지)
/// </summary>
public class PlayerExperience : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _collectRadius = 0.4f; // 젬 수집 반경(XZ), 초기값 = CharacterController 반지름 (스펙 "초기 크기 = 플레이어 몸")

    private int _experience;

    public float CollectRadius => _collectRadius;
    public int Experience => _experience;

    /// <summary>경험치가 추가될 때마다 발생, 인자는 이번 추가량 (사운드·레벨업·UI 구독 지점)</summary>
    public event Action<int> ExperienceGained;

    /// <summary>누적 경험치 증가 후 ExperienceGained 발생 (0 이하는 무시)</summary>
    /// <param name="amount">추가할 경험치</param>
    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _experience += amount;
        ExperienceGained?.Invoke(amount);
    }
}
