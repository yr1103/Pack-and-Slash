using UnityEngine;

/// <summary>
/// IntegrationField 기준 칸마다 목적지로 향하는 정규화 이동 방향 (8방향, Corner Cutting 금지)
/// </summary>
public class FlowField
{
    // IntegrationField와 같은 값이어야 최소 비용 방향이 누적 비용 계산과 일치함
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
    private readonly Vector2[,] _directions;

    /// <summary>방향 배열을 한 번만 생성 (Calculate마다 재사용)</summary>
    public FlowField(CostField costField)
    {
        _costField = costField;
        _directions = new Vector2[costField.Width, costField.Height];
    }

    /// <summary>누적 비용 기준 모든 칸의 이동 방향 재계산 (목적지·장애물·도달 불가 칸은 Vector2.zero)</summary>
    /// <param name="integrationField">같은 CostField로 만들고 Calculate를 마친 Integration Field</param>
    public void Calculate(IntegrationField integrationField)
    {
        for (int x = 0; x < _costField.Width; x++)
        {
            for (int z = 0; z < _costField.Height; z++)
            {
                _directions[x, z] = FindDirection(integrationField, x, z);
            }
        }
    }

    /// <summary>타일의 정규화 이동 방향 (격자 밖이면 Vector2.zero)</summary>
    public Vector2 GetDirection(int x, int z)
    {
        return _costField.IsInBounds(x, z) ? _directions[x, z] : Vector2.zero;
    }

    // 8방향 중 "이웃 누적 비용 + 이동 비용"이 최소인 방향, 동률이면 먼저 확인한 방향 유지
    private Vector2 FindDirection(IntegrationField integrationField, int x, int z)
    {
        // 장애물·도달 불가 칸은 누적 비용이 PositiveInfinity
        bool isTarget = x == integrationField.Target.x && z == integrationField.Target.y;
        if (isTarget || float.IsPositiveInfinity(integrationField.GetIntegration(x, z)))
        {
            return Vector2.zero;
        }

        float bestCost = float.PositiveInfinity;
        Vector2Int bestDirection = Vector2Int.zero;

        foreach (Vector2Int direction in _straightDirections)
        {
            float candidateCost = GetCandidateCost(integrationField, x, z, direction, STRAIGHT_MOVE_COST);
            if (candidateCost < bestCost)
            {
                bestCost = candidateCost;
                bestDirection = direction;
            }
        }

        foreach (Vector2Int direction in _diagonalDirections)
        {
            if (!_costField.CanPassDiagonal(x, z, direction))
            {
                continue;
            }

            float candidateCost = GetCandidateCost(integrationField, x, z, direction, DIAGONAL_MOVE_COST);
            if (candidateCost < bestCost)
            {
                bestCost = candidateCost;
                bestDirection = direction;
            }
        }

        return ((Vector2)bestDirection).normalized;
    }

    // 이웃이 격자 밖이거나 장애물이면 PositiveInfinity
    private float GetCandidateCost(IntegrationField integrationField, int x, int z, Vector2Int direction, float moveCost)
    {
        int nextX = x + direction.x;
        int nextZ = z + direction.y;
        if (!_costField.IsWalkable(nextX, nextZ))
        {
            return float.PositiveInfinity;
        }

        return integrationField.GetIntegration(nextX, nextZ) + moveCost;
    }
}
