using System;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// 게임 시작 시 Seed 기반으로 장애물·장식을 배치하고 Cost Field를 확정, 맵 경계 벽 생성
/// </summary>
public class MapGenerator : MonoBehaviour
{
    private const int MAP_WIDTH = 100;
    private const int MAP_HEIGHT = 100;
    private const float HALF = 0.5f;
    private const float FULL_ROTATION_DEGREES = 360f;
    private const float RIGHT_ANGLE_DEGREES = 90f;
    private const int RIGHT_ANGLE_STEP_COUNT = 4; // 0/90/180/270
    private const string OBSTACLE_LAYER_NAME = "Obstacle";
    private const string BOUNDARY_WALL_NAME_FORMAT = "BoundaryWall_{0:00}";
    private const string SEED_LOG_FORMAT = "[MapGenerator] Seed : {0}";
    private const string FILLED_LOG_FORMAT = "[MapGenerator] 고립 영역 채움 : {0}칸";

    private static readonly Vector3 _gridOrigin = new Vector3(-50f, 0f, -50f); // 기존 바닥(100×100, 원점 중심)의 좌하단
    private static readonly Vector3 _playerStartPosition = Vector3.zero;
    private static readonly Vector3 _halfTile = new Vector3(HALF, 0f, HALF);
    private static readonly Vector3 _obstacleGizmoSize = new Vector3(1f, 0.1f, 1f);
    private static readonly Color _boundaryGizmoColor = Color.yellow;
    private static readonly Color _obstacleGizmoColor = new Color(1f, 0f, 0f, 0.5f);
    private static readonly Color _safeZoneGizmoColor = Color.green;
    private static readonly ProfilerMarker _awakeMarker = new ProfilerMarker("MapGenerator.Awake");
    private static readonly ProfilerMarker _placeObstaclesMarker = new ProfilerMarker("MapGenerator.PlaceObstacles");
    private static readonly ProfilerMarker _placeDecorationsMarker = new ProfilerMarker("MapGenerator.PlaceDecorations");

    [SerializeField, Range(0f, 1f)] private float _obstacleChance = 0.05f; // 타일당 장애물 생성 확률
    [SerializeField, Range(0f, 1f)] private float _decorationChance = 0.15f; // 타일당 장식 생성 확률
    [SerializeField] private ObstacleEntry[] _obstacles;
    [SerializeField] private GameObject[] _decorations;
    [SerializeField] private int _safeZoneRadius = 5; // 시작 타일 기준 정사각형 반경(타일), 장애물 금지
    [SerializeField] private int _edgeClearWidth = 2; // 격자 가장자리에서 장애물 금지 폭(타일), 격자 밖 스폰 Enemy 진입로
    [SerializeField] private Transform _generatedRoot; // Environment/GeneratedMap
    [SerializeField] private float _boundaryWallHeight = 3f;
    [SerializeField] private float _boundaryWallThickness = 1f;
    [SerializeField] private bool _drawGizmos = true;

    private System.Random _random; // 같은 Seed면 같은 결과가 나오도록 모든 랜덤 판정에 이것만 사용
    private CostField _costField;
    private Vector2Int _startTile;
    private int _obstacleLayer;
    private int _boundaryWallCount;

    /// <summary>생성이 끝난 Cost Field (Awake 이후 유효, 에디터 비실행 시 null)</summary>
    public CostField CostField => _costField;

    // 다른 컴포넌트가 Start에서 CostField를 읽을 수 있도록 Awake에서 생성
    private void Awake()
    {
        using (_awakeMarker.Auto())
        {
            int seed = Environment.TickCount;
            Debug.LogFormat(SEED_LOG_FORMAT, seed);

            _random = new System.Random(seed);
            _costField = new CostField(MAP_WIDTH, MAP_HEIGHT, _gridOrigin);
            _startTile = _costField.WorldToTile(_playerStartPosition);
            _obstacleLayer = LayerMask.NameToLayer(OBSTACLE_LAYER_NAME);

            using (_placeObstaclesMarker.Auto())
            {
                PlaceObstacles();
            }

            int filledCount = _costField.FillUnreachable(_startTile);
            Debug.LogFormat(FILLED_LOG_FORMAT, filledCount);

            using (_placeDecorationsMarker.Auto())
            {
                PlaceDecorations();
            }

            CreateBoundaryWalls();
        }
    }

    private void PlaceObstacles()
    {
        for (int x = 0; x < MAP_WIDTH; x++)
        {
            for (int z = 0; z < MAP_HEIGHT; z++)
            {
                if (_random.NextDouble() >= _obstacleChance)
                {
                    continue;
                }

                ObstacleEntry entry = _obstacles[_random.Next(_obstacles.Length)];
                TryPlaceObstacle(entry, x, z);
            }
        }
    }

    // (x, z)를 좌하단으로 TileSize×TileSize 칸을 점유, 한 칸이라도 불가하면 건너뜀
    private void TryPlaceObstacle(ObstacleEntry entry, int x, int z)
    {
        int size = entry.TileSize;
        for (int dx = 0; dx < size; dx++)
        {
            for (int dz = 0; dz < size; dz++)
            {
                if (!CanPlaceObstacle(x + dx, z + dz))
                {
                    return;
                }
            }
        }

        for (int dx = 0; dx < size; dx++)
        {
            for (int dz = 0; dz < size; dz++)
            {
                _costField.SetObstacle(x + dx, z + dz);
            }
        }

        float centerOffset = (size - 1) * HALF; // 점유 칸들의 중심
        Vector3 position = _costField.TileToWorld(x, z) + new Vector3(centerOffset, 0f, centerOffset);
        float angle = entry.IsFreeRotation
            ? (float)(_random.NextDouble() * FULL_ROTATION_DEGREES)
            : _random.Next(RIGHT_ANGLE_STEP_COUNT) * RIGHT_ANGLE_DEGREES;

        GameObject obstacle = Instantiate(entry.Prefab, position, Quaternion.Euler(0f, angle, 0f), _generatedRoot);
        // 탄환 막힘·플레이어 충돌 판정에 Layer를 쓰므로 하위 Collider까지 지정
        foreach (Transform child in obstacle.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = _obstacleLayer;
        }
    }

    private bool CanPlaceObstacle(int x, int z)
    {
        bool isInsideEdge = x >= _edgeClearWidth && x < MAP_WIDTH - _edgeClearWidth
                            && z >= _edgeClearWidth && z < MAP_HEIGHT - _edgeClearWidth;
        bool isInSafeZone = Mathf.Abs(x - _startTile.x) <= _safeZoneRadius
                            && Mathf.Abs(z - _startTile.y) <= _safeZoneRadius;
        return isInsideEdge && !isInSafeZone && _costField.IsWalkable(x, z);
    }

    // 장애물·고립 영역 채움 칸은 IsWalkable이 false라 장식과 겹치지 않음
    private void PlaceDecorations()
    {
        for (int x = 0; x < MAP_WIDTH; x++)
        {
            for (int z = 0; z < MAP_HEIGHT; z++)
            {
                if (!_costField.IsWalkable(x, z) || _random.NextDouble() >= _decorationChance)
                {
                    continue;
                }

                GameObject prefab = _decorations[_random.Next(_decorations.Length)];
                float angle = (float)(_random.NextDouble() * FULL_ROTATION_DEGREES);
                Instantiate(prefab, _costField.TileToWorld(x, z), Quaternion.Euler(0f, angle, 0f), _generatedRoot);
            }
        }
    }

    // 플레이어만 막는 Collider, Default Layer라 탄환은 통과
    private void CreateBoundaryWalls()
    {
        float left = _gridOrigin.x;
        float right = left + MAP_WIDTH;
        float bottom = _gridOrigin.z;
        float top = bottom + MAP_HEIGHT;
        float floor = _gridOrigin.y;
        float ceiling = floor + _boundaryWallHeight;
        float thickness = _boundaryWallThickness;

        // 남·북 벽이 모서리까지 덮도록 두께만큼 길게 생성
        CreateBoundaryWall(new Vector3(left - thickness, floor, bottom - thickness), new Vector3(right + thickness, ceiling, bottom));
        CreateBoundaryWall(new Vector3(left - thickness, floor, top), new Vector3(right + thickness, ceiling, top + thickness));
        CreateBoundaryWall(new Vector3(left - thickness, floor, bottom), new Vector3(left, ceiling, top));
        CreateBoundaryWall(new Vector3(right, floor, bottom), new Vector3(right + thickness, ceiling, top));
    }

    private void CreateBoundaryWall(Vector3 min, Vector3 max)
    {
        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);

        _boundaryWallCount++;
        GameObject wall = new GameObject(string.Format(BOUNDARY_WALL_NAME_FORMAT, _boundaryWallCount));
        wall.transform.SetParent(_generatedRoot, false);
        wall.transform.position = bounds.center;
        wall.AddComponent<BoxCollider>().size = bounds.size;
    }

    // 10,000칸 대상이라 장애물 칸만 그림, CostField가 없으면(에디터 비실행) 경계만 그림
    private void OnDrawGizmos()
    {
        if (!_drawGizmos)
        {
            return;
        }

        Vector3 mapSize = new Vector3(MAP_WIDTH, 0f, MAP_HEIGHT);
        Gizmos.color = _boundaryGizmoColor;
        Gizmos.DrawWireCube(_gridOrigin + mapSize * HALF, mapSize);

        if (_costField == null)
        {
            return;
        }

        Gizmos.color = _obstacleGizmoColor;
        for (int x = 0; x < MAP_WIDTH; x++)
        {
            for (int z = 0; z < MAP_HEIGHT; z++)
            {
                if (!_costField.IsWalkable(x, z))
                {
                    Gizmos.DrawCube(_costField.TileToWorld(x, z), _obstacleGizmoSize);
                }
            }
        }

        Bounds safeZone = new Bounds();
        safeZone.SetMinMax(
            _costField.TileToWorld(_startTile.x - _safeZoneRadius, _startTile.y - _safeZoneRadius) - _halfTile,
            _costField.TileToWorld(_startTile.x + _safeZoneRadius, _startTile.y + _safeZoneRadius) + _halfTile);
        Gizmos.color = _safeZoneGizmoColor;
        Gizmos.DrawWireCube(safeZone.center, safeZone.size);
    }

    /// <summary>장애물 Prefab과 점유 크기·회전 방식</summary>
    [Serializable]
    private class ObstacleEntry
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Range(1, 2)] private int _tileSize = 1; // 1이면 1×1, 2면 2×2 점유
        [SerializeField] private bool _isFreeRotation; // true면 Y축 자유 회전(Capsule Collider), false면 90° 단위(Box Collider)

        public GameObject Prefab => _prefab;
        public int TileSize => _tileSize;
        public bool IsFreeRotation => _isFreeRotation;
    }
}
