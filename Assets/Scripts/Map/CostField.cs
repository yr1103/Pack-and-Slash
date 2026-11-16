using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 격자의 타일별 이동 비용(이동 가능 1, 장애물 255)과 타일·월드 좌표 변환
/// </summary>
public class CostField
{
    private const byte WALKABLE_COST = 1;
    private const byte OBSTACLE_COST = 255;
    private const float TILE_CENTER_OFFSET = 0.5f; // 타일 좌하단 모서리에서 중심까지 거리

    private static readonly Vector2Int[] _fourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private readonly int _width;
    private readonly int _height;
    private readonly Vector3 _origin; // 타일 (0, 0)의 좌하단 모서리 월드 좌표
    private readonly byte[,] _costs;

    public int Width => _width;
    public int Height => _height;

    /// <summary>모든 타일을 이동 가능(1)으로 초기화</summary>
    /// <param name="origin">타일 (0, 0)의 좌하단 모서리 월드 좌표</param>
    public CostField(int width, int height, Vector3 origin)
    {
        _width = width;
        _height = height;
        _origin = origin;
        _costs = new byte[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                _costs[x, z] = WALKABLE_COST;
            }
        }
    }

    /// <summary>타일 중심의 월드 좌표</summary>
    public Vector3 TileToWorld(int x, int z)
    {
        return _origin + new Vector3(x + TILE_CENTER_OFFSET, 0f, z + TILE_CENTER_OFFSET);
    }

    /// <summary>월드 좌표가 속한 타일 좌표 (격자 밖이면 범위 밖 값 반환)</summary>
    public Vector2Int WorldToTile(Vector3 position)
    {
        Vector3 local = position - _origin;
        return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.z));
    }

    /// <summary>월드 좌표의 연속 타일 좌표 (타일 중심이 정수, 타일 사이 보간용)</summary>
    public Vector2 WorldToContinuousTile(Vector3 position)
    {
        Vector3 local = position - _origin;
        return new Vector2(local.x - TILE_CENTER_OFFSET, local.z - TILE_CENTER_OFFSET);
    }

    /// <summary>격자 범위 안의 타일인지 확인</summary>
    public bool IsInBounds(int x, int z)
    {
        return x >= 0 && x < _width && z >= 0 && z < _height;
    }

    /// <summary>타일 이동 비용 (범위 안 좌표만 허용)</summary>
    public byte GetCost(int x, int z)
    {
        return _costs[x, z];
    }

    /// <summary>이동 가능한 타일인지 확인 (범위 밖은 false)</summary>
    public bool IsWalkable(int x, int z)
    {
        return IsInBounds(x, z) && _costs[x, z] != OBSTACLE_COST;
    }

    /// <summary>Corner Cutting 금지 판정 : 대각선을 이루는 두 직선 이웃이 모두 이동 가능한지 (도착 칸은 따로 확인)</summary>
    /// <param name="direction">대각선 방향 (각 성분이 1 또는 -1)</param>
    public bool CanPassDiagonal(int x, int z, Vector2Int direction)
    {
        return IsWalkable(x + direction.x, z) && IsWalkable(x, z + direction.y);
    }

    /// <summary>타일을 장애물(255)로 설정 (범위 안 좌표만 허용)</summary>
    public void SetObstacle(int x, int z)
    {
        _costs[x, z] = OBSTACLE_COST;
    }

    /// <summary>시작 타일에서 4방향으로 도달할 수 없는 이동 가능 타일을 장애물로 채움</summary>
    /// <param name="start">이동 가능한 타일이어야 함 (막혀 있으면 모든 이동 가능 타일이 채워짐)</param>
    /// <returns>장애물로 바꾼 타일 수</returns>
    public int FillUnreachable(Vector2Int start)
    {
        bool[,] isReached = new bool[_width, _height];
        Queue<Vector2Int> frontier = new Queue<Vector2Int>();

        if (IsWalkable(start.x, start.y))
        {
            isReached[start.x, start.y] = true;
            frontier.Enqueue(start);
        }

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            foreach (Vector2Int direction in _fourDirections)
            {
                Vector2Int next = current + direction;
                if (IsWalkable(next.x, next.y) && !isReached[next.x, next.y])
                {
                    isReached[next.x, next.y] = true;
                    frontier.Enqueue(next);
                }
            }
        }

        int filledCount = 0;
        for (int x = 0; x < _width; x++)
        {
            for (int z = 0; z < _height; z++)
            {
                if (_costs[x, z] != OBSTACLE_COST && !isReached[x, z])
                {
                    _costs[x, z] = OBSTACLE_COST;
                    filledCount++;
                }
            }
        }

        return filledCount;
    }
}
