using UnityEngine;

/// <summary>
/// Flow Field 이동 검증용 임시 스포너 (시작 시 격자 밖 원 위에 일괄 생성, 풀링·시간차 생성 없음)
/// </summary>
public class TempEnemySpawner : MonoBehaviour
{
    private const float FULL_CIRCLE_RADIANS = Mathf.PI * 2f;

    [SerializeField] private EnemyMovement _enemyPrefab;
    [SerializeField] private FlowFieldManager _flowFieldManager;
    [SerializeField] private Transform _player;
    [SerializeField] private Transform _enemyParent; // 생성 Enemy를 모을 Hierarchy 부모
    [SerializeField, Min(0)] private int _spawnCount = 20;
    [SerializeField, Min(0f)] private float _spawnRadius = 75f; // 격자 모서리까지 거리(약 70.7)보다 커야 격자 밖에서 생성

    private void Start()
    {
        for (int i = 0; i < _spawnCount; i++)
        {
            float angle = Random.Range(0f, FULL_CIRCLE_RADIANS);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _spawnRadius;

            EnemyMovement enemy = Instantiate(_enemyPrefab, transform.position + offset, Quaternion.identity, _enemyParent);
            enemy.Initialize(_flowFieldManager, _player);
        }
    }
}
