using UnityEngine;

/// <summary>
/// Inventory 상태(캐릭터 특성, 고유 아이템 장착·해제, 패시브 Lv.3 효과)로 플레이어 이동속도·최대 체력·경험치·젬 수집 반경 배율을 갱신
/// </summary>
public class PlayerStatSync : MonoBehaviour
{
    private const float MAX_MOVE_SPEED_BONUS = 0.5f; // 이동속도 보너스 합계 상한 (테스트 상한, 하한 없음)

    [SerializeField] private Inventory _inventory;
    [SerializeField] private PlayerMovement _movement;
    [SerializeField] private PlayerHealth _health;
    [SerializeField] private PlayerExperience _experience;

    private void OnEnable()
    {
        _inventory.Changed += ApplyStats;
    }

    private void OnDisable()
    {
        _inventory.Changed -= ApplyStats;
    }

    // PlayerHealth.Awake의 Health 생성 이후 적용, InventoryWeaponSync.Start와 순서가 바뀌어도 Changed로 다시 계산되어 결과 동일
    private void Start()
    {
        ApplyStats();
    }

    /// <summary>모든 배율을 기본값 기준으로 재계산해 적용 (누적 없음, Character가 null이면 특성·고유 효과 없이 Lv.3 효과만)</summary>
    public void ApplyStats()
    {
        CharacterDefinition character = _inventory.Character;
        float moveSpeedBonus = _inventory.GetLevel3Bonus(Level3EffectType.MoveSpeed);
        float maxHealthBonus = 0f;

        if (character != null)
        {
            moveSpeedBonus += character.TraitMoveSpeedBonus;

            if (!_inventory.IsUniqueItemEquipped())
            {
                moveSpeedBonus += character.UnequippedMoveSpeedBonus;
                maxHealthBonus = character.UnequippedMaxHealthBonus;
            }
        }

        _movement.SetMoveSpeedMultiplier(Mathf.Max(0f, 1f + Mathf.Min(moveSpeedBonus, MAX_MOVE_SPEED_BONUS)));
        _health.SetMaxHealthMultiplier(1f + maxHealthBonus);
        _experience.SetExperienceMultiplier(1f + _inventory.GetLevel3Bonus(Level3EffectType.Experience));
        _experience.SetCollectRadiusMultiplier(1f + _inventory.GetLevel3Bonus(Level3EffectType.CollectRadius));
    }
}
