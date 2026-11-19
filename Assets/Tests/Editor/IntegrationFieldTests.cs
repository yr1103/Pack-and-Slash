using NUnit.Framework;
using UnityEngine;

/// <summary>
/// IntegrationField 누적 비용(직선·대각선)·장애물 우회·Corner Cutting 차단·도달 불가·재계산 초기화 검증
/// </summary>
public class IntegrationFieldTests
{
    private const int OPEN_MAP_SIZE = 5;
    private const int SMALL_MAP_SIZE = 3;
    private const int STRAIGHT_TILE_COUNT = 4;
    private const float DIAGONAL_COST = 1.414f; // IntegrationField의 대각선 이동 비용(√2 근사값)과 같은 값
    private const float DETOUR_COST = 6f; // 작은 맵에서 벽 위쪽으로 돌아가는 6칸 직선 이동
    private const float CORNER_DETOUR_COST = 2f; // 막힌 모서리 대신 직선 2칸
    private const float FLOAT_TOLERANCE = 0.0001f;

    private static readonly Vector2Int _cornerTile = new Vector2Int(0, 0);
    private static readonly Vector2Int _oppositeCornerTile = new Vector2Int(4, 4);
    private static readonly Vector2Int _diagonalTile = new Vector2Int(1, 1);
    private static readonly Vector2Int[] _detourWallTiles = { new Vector2Int(1, 0), new Vector2Int(1, 1) };
    private static readonly Vector2Int _detourGoalTile = new Vector2Int(2, 0);
    private static readonly Vector2Int _cornerBlockTile = new Vector2Int(1, 0);
    private static readonly Vector2Int[] _isolatingWallTiles = { new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1) };
    private static readonly Vector2Int _isolatedTile = new Vector2Int(2, 2);

    private CostField _openField;
    private CostField _smallField;

    [SetUp]
    public void SetUp()
    {
        _openField = new CostField(OPEN_MAP_SIZE, OPEN_MAP_SIZE, Vector3.zero);
        _smallField = new CostField(SMALL_MAP_SIZE, SMALL_MAP_SIZE, Vector3.zero);
    }

    [Test]
    public void Calculate_TargetCostIsZero()
    {
        IntegrationField integrationField = new IntegrationField(_openField);

        integrationField.Calculate(_cornerTile);

        Assert.AreEqual(0f, integrationField.GetIntegration(_cornerTile.x, _cornerTile.y));
        Assert.AreEqual(_cornerTile, integrationField.Target);
    }

    [Test]
    public void Calculate_OpenMap_StraightIsTileCountAndDiagonalIsDiagonalCost()
    {
        IntegrationField integrationField = new IntegrationField(_openField);

        integrationField.Calculate(_cornerTile);

        Assert.AreEqual(STRAIGHT_TILE_COUNT, integrationField.GetIntegration(STRAIGHT_TILE_COUNT, 0), FLOAT_TOLERANCE);
        Assert.AreEqual(STRAIGHT_TILE_COUNT, integrationField.GetIntegration(0, STRAIGHT_TILE_COUNT), FLOAT_TOLERANCE);
        Assert.AreEqual(DIAGONAL_COST, integrationField.GetIntegration(_diagonalTile.x, _diagonalTile.y), FLOAT_TOLERANCE);
    }

    [Test]
    public void Calculate_WallBetween_CostIsDetourLength()
    {
        foreach (Vector2Int wall in _detourWallTiles)
        {
            _smallField.SetObstacle(wall.x, wall.y);
        }
        IntegrationField integrationField = new IntegrationField(_smallField);

        integrationField.Calculate(_cornerTile);

        Assert.AreEqual(DETOUR_COST, integrationField.GetIntegration(_detourGoalTile.x, _detourGoalTile.y), FLOAT_TOLERANCE);
    }

    [Test]
    public void Calculate_OneStraightNeighborBlocked_DiagonalIsNotUsed()
    {
        _smallField.SetObstacle(_cornerBlockTile.x, _cornerBlockTile.y);
        IntegrationField integrationField = new IntegrationField(_smallField);

        integrationField.Calculate(_cornerTile);

        Assert.AreEqual(CORNER_DETOUR_COST, integrationField.GetIntegration(_diagonalTile.x, _diagonalTile.y), FLOAT_TOLERANCE);
    }

    [Test]
    public void Calculate_IsolatedTile_IsInfinity()
    {
        foreach (Vector2Int wall in _isolatingWallTiles)
        {
            _smallField.SetObstacle(wall.x, wall.y);
        }
        IntegrationField integrationField = new IntegrationField(_smallField);

        integrationField.Calculate(_cornerTile);

        Assert.IsTrue(float.IsPositiveInfinity(integrationField.GetIntegration(_isolatedTile.x, _isolatedTile.y)));
    }

    [Test]
    public void Calculate_TargetIsObstacle_AllTilesAreInfinity()
    {
        _smallField.SetObstacle(_cornerTile.x, _cornerTile.y);
        IntegrationField integrationField = new IntegrationField(_smallField);

        integrationField.Calculate(_cornerTile);

        for (int x = 0; x < SMALL_MAP_SIZE; x++)
        {
            for (int z = 0; z < SMALL_MAP_SIZE; z++)
            {
                Assert.IsTrue(float.IsPositiveInfinity(integrationField.GetIntegration(x, z)), $"({x}, {z})");
            }
        }
    }

    [Test]
    public void Calculate_CalledTwice_SecondResultMatchesFreshCalculation()
    {
        IntegrationField reusedField = new IntegrationField(_openField);
        IntegrationField freshField = new IntegrationField(_openField);

        reusedField.Calculate(_cornerTile);
        reusedField.Calculate(_oppositeCornerTile);
        freshField.Calculate(_oppositeCornerTile);

        for (int x = 0; x < OPEN_MAP_SIZE; x++)
        {
            for (int z = 0; z < OPEN_MAP_SIZE; z++)
            {
                Assert.AreEqual(freshField.GetIntegration(x, z), reusedField.GetIntegration(x, z), FLOAT_TOLERANCE, $"({x}, {z})");
            }
        }
    }
}
