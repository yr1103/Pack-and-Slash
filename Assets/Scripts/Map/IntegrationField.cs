using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CostField 기준 목적지 타일까지의 최소 누적 이동 비용 (8방향 다익스트라, Corner Cutting 금지)
/// </summary>
public class IntegrationField
{
    private const float STRAIGHT_MOVE_COST = 1f;     // 상하좌우 이동 비용
    private const float DIAGONAL_MOVE_COST = 1.414f; // 대각선 이동 비용

    private static readonly Vector2Int[] _straightDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };
    private static readonly Vector2Int[] _diagonalDirections =
    {
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    private readonly CostField _costField;
    private readonly float[,] _integration;
    private readonly SortedSet<QueueEntry> _frontier;
    private Vector2Int _target; // 마지막 Calculate 목적지 (호출 전에는 (0, 0))

    public Vector2Int Target => _target;

    /// <summary>누적 비용 배열과 탐색 큐를 한 번만 생성 (Calculate마다 재사용)</summary>
    public IntegrationField(CostField costField)
    {
        _costField = costField;
        _integration = new float[costField.Width, costField.Height];
        _frontier = new SortedSet<QueueEntry>(new QueueEntryComparer());
    }

    /// <summary>목적지 기준 누적 비용 재계산 (목적지가 이동 불가면 모든 칸 PositiveInfinity)</summary>
    public void Calculate(Vector2Int target)
    {
        _target = target;
        ResetIntegration();

        if (!_costField.IsWalkable(target.x, target.y))
        {
            return;
        }

        _integration[target.x, target.y] = 0f;
        _frontier.Add(new QueueEntry(0f, target.x, target.y));

        while (_frontier.Count > 0)
        {
            QueueEntry current = _frontier.Min;
            _frontier.Remove(current);

            // 같은 칸이 더 작은 비용으로 다시 추가된 뒤 남은 오래된 항목
            if (current.Cost > _integration[current.X, current.Z])
            {
                continue;
            }

            foreach (Vector2Int direction in _straightDirections)
            {
                Relax(current, direction, STRAIGHT_MOVE_COST);
            }

            foreach (Vector2Int direction in _diagonalDirections)
            {
                // 대각선 뚫기 금지 : 대각선을 이루는 두 직선 이웃이 모두 이동 가능해야 함
                if (_costField.CanPassDiagonal(current.X, current.Z, direction))
                {
                    Relax(current, direction, DIAGONAL_MOVE_COST);
                }
            }
        }
    }

    /// <summary>누적 비용 (범위 안 좌표만 허용, 도달 불가는 PositiveInfinity)</summary>
    public float GetIntegration(int x, int z)
    {
        return _integration[x, z];
    }

    private void Relax(QueueEntry current, Vector2Int direction, float moveCost)
    {
        int nextX = current.X + direction.x;
        int nextZ = current.Z + direction.y;
        if (!_costField.IsWalkable(nextX, nextZ))
        {
            return;
        }

        float nextCost = current.Cost + moveCost;
        if (nextCost < _integration[nextX, nextZ])
        {
            _integration[nextX, nextZ] = nextCost;
            _frontier.Add(new QueueEntry(nextCost, nextX, nextZ));
        }
    }

    private void ResetIntegration()
    {
        _frontier.Clear();
        for (int x = 0; x < _costField.Width; x++)
        {
            for (int z = 0; z < _costField.Height; z++)
            {
                _integration[x, z] = float.PositiveInfinity;
            }
        }
    }

    private readonly struct QueueEntry
    {
        public readonly float Cost;
        public readonly int X;
        public readonly int Z;

        public QueueEntry(float cost, int x, int z)
        {
            Cost = cost;
            X = x;
            Z = z;
        }
    }

    // SortedSet은 비교 결과 0을 중복으로 보고 추가하지 않으므로, 비용이 같으면 좌표로 순서를 정함
    private sealed class QueueEntryComparer : IComparer<QueueEntry>
    {
        public int Compare(QueueEntry left, QueueEntry right)
        {
            int costOrder = left.Cost.CompareTo(right.Cost);
            if (costOrder != 0)
            {
                return costOrder;
            }

            int xOrder = left.X.CompareTo(right.X);
            if (xOrder != 0)
            {
                return xOrder;
            }

            return left.Z.CompareTo(right.Z);
        }
    }
}
