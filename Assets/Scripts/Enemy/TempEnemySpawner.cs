using UnityEngine;

/// <summary>
/// Flow Field 이동 검증용 임시 스포너 (시작 시 격자 밖 원 위에 풀에서 일괄 생성, 시간차 생성 없음)
/// </summary>
public class TempEnemySpawner : MonoBehaviour
{
    private const float FULL_CIRCLE_RADIANS = Mathf.PI * 2f;

    [SerializeField] private Enemy[] _enemyPrefabs; // 생성마다 균등 랜덤 선택
    [SerializeField] private EnemyPool _enemyPool;
    [SerializeField] private FlowFieldManager _flowFieldManager;
    [SerializeField] private Transform _player;
    [SerializeField, Min(0)] private int _spawnCount = 20;
    [SerializeField, Min(0f)] private float _spawnRadius = 75f; // 격자 모서리까지 거리(약 70.7)보다 커야 격자 밖에서 생성

    private void Start()
    {
        for (int i = 0; i < _spawnCount; i++)
        {
            float angle = Random.Range(0f, FULL_CIRCLE_RADIANS);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _spawnRadius;

            Enemy prefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Length)];

            _enemyPool.Spawn(prefab, transform.position + offset, _flowFieldManager, _player);
        }
    }
}
