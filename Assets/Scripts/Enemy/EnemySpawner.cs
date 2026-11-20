using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 진행 시간에 따라 해금된 일반 몬스터를 일정 간격으로 카메라 시야 바로 바깥에 생성하고, 보스·강화몹은 예정 시각에 1마리씩 가장 가까운 시야 밖 격자 변 중점에 생성
/// 원형 대량 스폰 시각마다 1회, 플레이어 중심 원 위에 균등 각도로 몬스터를 한 번에 생성
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private const float FULL_CIRCLE_RADIANS = Mathf.PI * 2f;
    private const float MAX_GROUND_DISTANCE = 200f; // 광선이 지면에 닿지 않을 때 대신 쓰는 거리 (맵보다 충분히 커서 격자 밖 판정으로 재시도)
    private const int CENTER_DIVISOR = 2; // 격자 크기를 나눠 변 중점 타일 계산

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
    [SerializeField] private SpawnEntry[] _bossSchedule; // 보스·강화몹 프리팹과 등장 시각, 시각 오름차순으로 배치
    [SerializeField, Min(0.01f)] private float _spawnInterval = 1f; // 생성 간격(초)
    [SerializeField, Min(0)] private int _spawnCountPerTick = 2;
    [SerializeField, Min(0)] private int _maxActiveEnemies = 500; // 활성 Enemy 상한 (풀 보관 개체 제외)
    [SerializeField, Min(0f)] private float _spawnMargin = 2f; // 시야 경계 바깥 여유 거리
    [SerializeField, Min(1)] private int _maxPositionAttempts = 8; // 격자 밖·통행 불가 시 방향 재시도 횟수
    [SerializeField] private float[] _ringSpawnTimes; // 원형 대량 스폰 시각(초), 오름차순으로 배치
    [SerializeField] private Enemy _ringPrefab; // 원형 대량 스폰에 쓰는 저속 몬스터
    [SerializeField, Min(1)] private int _ringCount = 30; // 회차당 생성 수, 수치미정
    [SerializeField, Min(0f)] private float _ringRadius = 14f; // 플레이어 중심 원 반지름, 수치미정

    private readonly Vector2[] _groundCorners = new Vector2[_viewportCorners.Length];
    private readonly List<Enemy> _unlockedPrefabs = new List<Enemy>();
    private readonly List<Vector2> _bossCandidates = new List<Vector2>(); // 이동 가능한 격자 변 중점 (XZ)
    private float _nextSpawnTime;
    private int _nextBossIndex; // 다음에 생성할 _bossSchedule 항목
    private int _nextRingIndex; // 다음에 처리할 _ringSpawnTimes 항목

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
        if (!_gameSession.IsPlaying)
        {
            return;
        }

        float elapsedTime = _gameSession.ElapsedTime;
        TrySpawnNextBoss(elapsedTime);
        TrySpawnNextRing(elapsedTime);
        if (elapsedTime < _nextSpawnTime)
        {
            return;
        }

        _nextSpawnTime += _spawnInterval;
        SpawnTick(elapsedTime);
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

    /// <summary>보스·강화몹 생성 후보 선택 :시야 다각형 밖 후보 중 원점과 가장 가까운 후보</summary>
    /// <param name="origin">플레이어 XZ 위치</param>
    /// <param name="candidates">후보 XZ 위치</param>
    /// <param name="viewCorners">둘레 순으로 놓인 시야 볼록 다각형 꼭짓점</param>
    /// <returns>선택한 후보 인덱스, 모두 시야 안(경계 포함)이면 -1</returns>
    public static int SelectBossSpawnIndex(Vector2 origin, IReadOnlyList<Vector2> candidates, Vector2[] viewCorners)
    {
        int selectedIndex = -1;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float sqrDistance = (candidates[i] - origin).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance && !IsInsideConvex(candidates[i], viewCorners))
            {
                nearestSqrDistance = sqrDistance;
                selectedIndex = i;
            }
        }

        return selectedIndex;
    }

    /// <summary>원 위 균등 각도 지점 : index 0은 월드 +X 방향, 360/count 간격으로 월드 +X → +Z 순서 배치</summary>
    /// <param name="center">원 중심 XZ 위치</param>
    /// <param name="index">지점 순번 (0 ~ count - 1)</param>
    /// <param name="count">원 위 지점 수 (1 이상, 0이면 0으로 나눠 NaN)</param>
    /// <param name="radius">원 반지름</param>
    /// <returns>index번째 지점 XZ 위치</returns>
    public static Vector2 GetRingPoint(Vector2 center, int index, int count, float radius)
    {
        float angle = FULL_CIRCLE_RADIANS * index / count;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // 모든 변에 대해 같은 쪽(또는 변 위)이면 안쪽, 꼭짓점 둘레 방향(시계·반시계)과 무관
    private static bool IsInsideConvex(Vector2 point, Vector2[] corners)
    {
        bool hasPositive = false;
        bool hasNegative = false;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 start = corners[i];
            float side = Cross(corners[(i + 1) % corners.Length] - start, point - start);
            hasPositive |= side > 0f;
            hasNegative |= side < 0f;
        }

        return !(hasPositive && hasNegative);
    }

    // 예정 시각이 지난 다음 보스·강화몹을 생성, 위치를 못 찾으면 인덱스를 유지해 다음 프레임 재시도 (누락 방지)
    private void TrySpawnNextBoss(float elapsedTime)
    {
        if (_nextBossIndex >= _bossSchedule.Length
            || !IsUnlocked(_bossSchedule[_nextBossIndex].UnlockTime, elapsedTime))
        {
            return;
        }

        if (!TryFindBossSpawnPosition(out Vector3 position))
        {
            return;
        }

        _enemyPool.Spawn(_bossSchedule[_nextBossIndex].Prefab, position, _flowFieldManager, _player);
        _nextBossIndex++;
    }

    // 격자 4변 중점 중 시야 밖이면서 플레이어와 가장 가까운 곳
    private bool TryFindBossSpawnPosition(out Vector3 position)
    {
        float groundHeight = _player.position.y;
        UpdateGroundCorners(groundHeight);
        CollectBossCandidates();
        Vector2 origin = new Vector2(_player.position.x, _player.position.z);

        int index = SelectBossSpawnIndex(origin, _bossCandidates, _groundCorners);
        if (index == -1)
        {
            position = Vector3.zero;
            return false;
        }

        position = new Vector3(_bossCandidates[index].x, groundHeight, _bossCandidates[index].y);
        return true;
    }

    // 가장자리 행·열은 MapGenerator의 장애물 금지 폭 안이라 격자 안쪽에서 Enemy가 바로 진입 가능
    private void CollectBossCandidates()
    {
        _bossCandidates.Clear();
        CostField costField = _flowFieldManager.CostField;
        int centerX = costField.Width / CENTER_DIVISOR;
        int centerZ = costField.Height / CENTER_DIVISOR;
        AddBossCandidateIfWalkable(costField, centerX, costField.Height - 1);
        AddBossCandidateIfWalkable(costField, centerX, 0);
        AddBossCandidateIfWalkable(costField, 0, centerZ);
        AddBossCandidateIfWalkable(costField, costField.Width - 1, centerZ);
    }

    private void AddBossCandidateIfWalkable(CostField costField, int x, int z)
    {
        if (!costField.IsWalkable(x, z))
        {
            return;
        }

        Vector3 world = costField.TileToWorld(x, z);
        _bossCandidates.Add(new Vector2(world.x, world.z));
    }

    // 시각이 지난 다음 회차를 1회만 처리, 일부가 상한·장애물로 빠져도 재시도하지 않음
    private void TrySpawnNextRing(float elapsedTime)
    {
        if (_nextRingIndex >= _ringSpawnTimes.Length
            || !IsUnlocked(_ringSpawnTimes[_nextRingIndex], elapsedTime))
        {
            return;
        }

        _nextRingIndex++;
        SpawnRing();
    }

    // 그 순간 플레이어 XZ 위치 중심 원 위에 일괄 생성, 통행 불가·격자 밖 지점은 그 1마리만 건너뜀
    private void SpawnRing()
    {
        float groundHeight = _player.position.y;
        Vector2 center = new Vector2(_player.position.x, _player.position.z);
        CostField costField = _flowFieldManager.CostField;

        for (int i = 0; i < _ringCount; i++)
        {
            // 일반 스폰과 같은 보스·강화몹 예약 슬롯 제외 상한, 상한이면 이 회차 남은 생성 중단
            if (!HasCapacity(Enemy.ActiveEnemies.Count, _maxActiveEnemies - _bossSchedule.Length))
            {
                return;
            }

            Vector2 point = GetRingPoint(center, i, _ringCount, _ringRadius);
            Vector3 position = new Vector3(point.x, groundHeight, point.y);
            Vector2Int tile = costField.WorldToTile(position);
            if (!costField.IsWalkable(tile.x, tile.y))
            {
                continue;
            }

            _enemyPool.Spawn(_ringPrefab, position, _flowFieldManager, _player);
        }
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
            // 보스·강화몹 스케줄 전체 수만큼 슬롯을 항상 고정 예약 (등장 전·사망 후 포함), 보스·강화몹(상한 검사 없음)이 생겨도 전체 상한 초과 방지
            if (!HasCapacity(Enemy.ActiveEnemies.Count, _maxActiveEnemies - _bossSchedule.Length))
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
