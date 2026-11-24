using UnityEngine;

/// <summary>
/// 플레이어가 획득 반경에 들어오면 보물 상자 보상을 1회 요청하고 파괴
/// </summary>
public class TreasureChest : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _pickupRadius = 1.5f; // 획득 반경 (XZ 거리, 수치 미정)

    private Transform _player;
    private LevelUpPanel _levelUpPanel;

    private void Update()
    {
        // 미초기화(Scene 직접 배치 등)면 판정하지 않음
        if (_player == null)
        {
            return;
        }

        if (!ExperienceGem.IsInCollectRadius(transform.position, _player.position, _pickupRadius))
        {
            return;
        }

        // 요청 경로는 이 Update뿐이고 Destroy는 이번 프레임 이후 적용되어 다음 Update가 오지 않으므로 중복 요청 없음
        _levelUpPanel.RequestChestReward();
        Destroy(gameObject);
    }

    /// <summary>생성 직후 호출해 획득 판정 대상과 보상 요청 대상 주입</summary>
    public void Initialize(Transform player, LevelUpPanel levelUpPanel)
    {
        _player = player;
        _levelUpPanel = levelUpPanel;
    }
}
