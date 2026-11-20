using UnityEngine;

/// <summary>
/// 보물 상자 드롭 대상 Enemy 사망 시 그 위치에 보물 상자 생성
/// </summary>
public class TreasureChestSpawner : MonoBehaviour
{
    [SerializeField] private TreasureChest _chestPrefab;
    [SerializeField] private Transform _chestParent; // 생성 상자를 모을 Hierarchy 부모
    [SerializeField] private Transform _player; // 상자 획득 판정 대상
    [SerializeField] private LevelUpPanel _levelUpPanel; // 상자 획득 시 보상 요청 대상
    [SerializeField] private float _spawnHeight; // 상자 생성 높이(월드 Y)

    private void OnEnable()
    {
        Enemy.Died += SpawnChest;
    }

    private void OnDisable()
    {
        Enemy.Died -= SpawnChest;
    }

    // 한 판 최대 3개(중간 보스 수)라 풀 없이 생성·파괴
    private void SpawnChest(Enemy enemy)
    {
        if (!enemy.DropsTreasureChest)
        {
            return;
        }

        Vector3 position = enemy.transform.position;
        position.y = _spawnHeight;

        TreasureChest chest = Instantiate(_chestPrefab, position, Quaternion.identity, _chestParent);
        chest.Initialize(_player, _levelUpPanel);
    }
}
