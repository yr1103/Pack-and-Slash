using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 진행 시간에 따라 해금된 일반 몬스터를 일정 간격으로 카메라 시야 바로 바깥에 생성
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private const float FULL_CIRCLE_RADIANS = Mathf.PI * 2f;
    private const float MAX_GROUND_DISTANCE = 200f; // 광선이 지면에 닿지 않을 때 대신 쓰는 거리 (맵보다 충분히 커서 격자 밖 판정으로 재시도)

    // 순서대로 이어 볼록 사다리꼴이 되도록 뷰포트 모서리를 둘레 순으로 배치
    private static readonly Vector2[] _viewportCorners =
    {
        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
    };

    [SerializeField] private GameSession _gameSession;
    [SerializeField] private EnemyPool _enemyPool;
    [SerializeField] private FlowFieldManager _flowFieldManager;
    [SerializeField] private Transform _player;
    [SerializeField] private Camera _camera; // Game View 카메라, 시야 사다리꼴 계산용
    [SerializeField] private SpawnEntry[] _spawnTable; // 해금된 항목 중 균등 랜덤 선택
    [SerializeField, Min(0.01f)] private float _spawnInterval = 1f; // 생성 간격(초)
    [SerializeField, Min(0)] private int _spawnCountPerTick = 2;
    [SerializeField, Min(0)] private int _maxActiveEnemies = 500; // 활성 Enemy 상한 (풀 보관 개체 제외)
    [SerializeField, Min(0f)] private float _spawnMargin = 2f; // 시야 경계 바깥 여유 거리
    [SerializeField, Min(1)] private int _maxPositionAttempts = 8; // 격자 밖·통행 불가 시 방향 재시도 횟수

    private readonly Vector2[] _groundCorners = new Vector2[_viewportCorners.Length];
    private readonly List<Enemy> _unlockedPrefabs = new List<Enemy>();
    private float _nextSpawnTime;

    /// <summary>스폰 테이블 항목 (Enemy 프리팹과 해금 시각)</summary>
    [Serializable]
    private struct SpawnEntry
    {
        [SerializeField] private Enemy _prefab;
        [SerializeField, Min(0f)] private float _unlockTime; // 해금 시각(초), 이 시각 이상부터 생성

        public Enemy Prefab => _prefab;
        public float UnlockTime => _unlockTime;
    }

    // 생성 주기를 GameSession 생존 시간에 맞춰 종료·일시정지 시 함께 멈춤
    private void Update()
    {
        if (!_gameSession.IsPlaying || _gameSession.ElapsedTime < _nextSpawnTime)
        {
            return;
        }

        _nextSpawnTime += _spawnInterval;
        SpawnTick(_gameSession.ElapsedTime);
    }

    /// <summary>해금 판정 (경과 시간이 해금 시각 이상이면 해금)</summary>
    public static bool IsUnlocked(float unlockTime, float elapsedTime)
    {
        return elapsedTime >= unlockTime;
    }

    /// <summary>상한 판정 (활성 수가 상한 미만이면 생성 가능)</summary>
    public static bool HasCapacity(int activeCount, int maxActiveEnemies)
    {
        return activeCount < maxActiveEnemies;
    }

    /// <summary>XZ 평면에서 원점부터 볼록 다각형 경계까지 방향을 따라간 거리</summary>
    /// <param name="origin">시작점 (다각형 안쪽)</param>
    /// <param name="direction">정규화된 방향</param>
    /// <param name="corners">둘레 순으로 놓인 볼록 다각형 꼭짓점</param>
    /// <returns>경계까지 거리, 경계와 만나지 않으면 0</returns>
    public static float GetDistanceToBoundary(Vector2 origin, Vector2 direction, Vector2[] corners)
    {
        float farthest = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 start = corners[i];
            Vector2 edge = corners[(i + 1) % corners.Length] - start;
            float denominator = Cross(direction, edge);
            // 변과 평행하면 교점 없음
            if (Mathf.Approximately(denominator, 0f))
            {
                continue;
            }

            Vector2 toStart = start - origin;
            float distance = Cross(toStart, edge) / denominator;
            float edgeRatio = Cross(toStart, direction) / denominator;
            // 원점이 안쪽이면 앞쪽 교점은 하나, 원점이 바깥이어도 나가는 교점(가장 먼 쪽)을 사용
            if (distance >= 0f && edgeRatio >= 0f && edgeRatio <= 1f && distance > farthest)
            {
                farthest = distance;
            }
        }

        return farthest;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void SpawnTick(float elapsedTime)
    {
        CollectUnlockedPrefabs(elapsedTime);
        if (_unlockedPrefabs.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _spawnCountPerTick; i++)
        {
            if (!HasCapacity(Enemy.ActiveEnemies.Count, _maxActiveEnemies))
            {
                return;
            }

            // 위치를 못 찾으면 이 1마리만 건너뜀
            if (!TryFindSpawnPosition(out Vector3 position))
            {
                continue;
            }

            Enemy prefab = _unlockedPrefabs[Random.Range(0, _unlockedPrefabs.Count)];
            _enemyPool.Spawn(prefab, position, _flowFieldManager, _player);
        }
    }

    private void CollectUnlockedPrefabs(float elapsedTime)
    {
        _unlockedPrefabs.Clear();
        for (int i = 0; i < _spawnTable.Length; i++)
        {
            if (IsUnlocked(_spawnTable[i].UnlockTime, elapsedTime))
            {
                _unlockedPrefabs.Add(_spawnTable[i].Prefab);
            }
        }
    }

    // 시야 사다리꼴 경계 + 여유 거리 위치를 랜덤 방향으로 찾고, 경계 미교차·격자 밖·통행 불가면 다른 방향으로 재시도
    private bool TryFindSpawnPosition(out Vector3 position)
    {
        float groundHeight = _player.position.y;
        UpdateGroundCorners(groundHeight);
        Vector2 origin = new Vector2(_player.position.x, _player.position.z);
        CostField costField = _flowFieldManager.CostField;

        for (int attempt = 0; attempt < _maxPositionAttempts; attempt++)
        {
            float angle = Random.Range(0f, FULL_CIRCLE_RADIANS);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float boundaryDistance = GetDistanceToBoundary(origin, direction, _groundCorners);
            // 경계와 만나지 않는 방향(원점이 시야 밖)은 생성 위치가 화면 안일 수 있어 다른 방향으로 재시도
            if (boundaryDistance <= 0f)
            {
                continue;
            }

            Vector2 point = origin + direction * (boundaryDistance + _spawnMargin);

            position = new Vector3(point.x, groundHeight, point.y);
            Vector2Int tile = costField.WorldToTile(position);
            if (costField.IsWalkable(tile.x, tile.y))
            {
                return true;
            }
        }

        position = Vector3.zero;
        return false;
    }

    // 카메라 뷰포트 네 모서리 광선을 지면 평면(y = groundHeight)에 투사해 시야 사다리꼴 XZ 꼭짓점 갱신
    private void UpdateGroundCorners(float groundHeight)
    {
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        for (int i = 0; i < _viewportCorners.Length; i++)
        {
            Ray ray = _camera.ViewportPointToRay(_viewportCorners[i]);
            // 수평선 위를 향한 광선은 지면에 닿지 않으므로 최대 거리 지점으로 대체
            Vector3 hitPoint = ground.Raycast(ray, out float enter)
                ? ray.GetPoint(enter)
                : ray.GetPoint(MAX_GROUND_DISTANCE);
            _groundCorners[i] = new Vector2(hitPoint.x, hitPoint.z);
        }
    }
}
