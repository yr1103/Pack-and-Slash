using NUnit.Framework;
using UnityEngine;

/// <summary>
/// FlowField 직선·대각선 방향, 장애물 우회, Corner Cutting 차단, zero 칸(목적지·장애물·고립·격자 밖), 정규화, 보간 조회 검증
/// </summary>
public class FlowFieldTests
{
    private const int OPEN_MAP_SIZE = 5;
    private const int SMALL_MAP_SIZE = 3;
    private const float FLOAT_TOLERANCE = 0.0001f;
    private const float UNIT_LENGTH = 1f;

    private static readonly Vector2Int _cornerTile = new Vector2Int(0, 0);
    private static readonly Vector2Int _rightEdgeTile = new Vector2Int(4, 0);
    private static readonly Vector2Int _topEdgeTile = new Vector2Int(0, 4);
    private static readonly Vector2Int _centerTile = new Vector2Int(2, 2);
    private static readonly Vector2 _downLeftDirection = new Vector2(-1f, -1f).normalized;
    private static readonly Vector2Int[] _detourWallTiles = { new Vector2Int(1, 0), new Vector2Int(1, 1) };
    private static readonly Vector2Int _detourStartTile = new Vector2Int(2, 0);
    private static readonly Vector2Int _cornerBlockTile = new Vector2Int(1, 0);
    private static readonly Vector2Int _cornerStartTile = new Vector2Int(1, 1);
    private static readonly Vector2Int[] _isolatingWallTiles = { new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1) };
    private static readonly Vector2Int _isolatedTile = new Vector2Int(2, 2);
    private static readonly Vector2Int[] _outOfBoundsTiles =
    {
        new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(OPEN_MAP_SIZE, 0), new Vector2Int(0, OPEN_MAP_SIZE)
    };

    // 목적지 (0, 0) 기준 : (1, 0)은 왼쪽, (1, 1)은 왼쪽 아래 대각선
    private static readonly Vector3 _betweenLeftAndDiagonalPosition = new Vector3(1.5f, 0f, 1f); // (1, 0)·(1, 1) 중심 사이
    private static readonly Vector3 _betweenTargetAndLeftPosition = new Vector3(1f, 0f, 0.5f);   // (0, 0)·(1, 0) 중심 사이
    private static readonly Vector3 _gridCornerPosition = Vector3.zero; // 주변 4칸이 목적지와 격자 밖뿐

    private CostField _openField;
    private CostField _smallField;

    [SetUp]
    public void SetUp()
    {
        _openField = new CostField(OPEN_MAP_SIZE, OPEN_MAP_SIZE, Vector3.zero);
        _smallField = new CostField(SMALL_MAP_SIZE, SMALL_MAP_SIZE, Vector3.zero);
    }

    [Test]
    public void Calculate_OpenMap_StraightLinePointsToTarget()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        AssertDirection(Vector2.left, flowField.GetDirection(_rightEdgeTile.x, _rightEdgeTile.y));
        AssertDirection(Vector2.down, flowField.GetDirection(_topEdgeTile.x, _topEdgeTile.y));
    }

    [Test]
    public void Calculate_OpenMap_DiagonalLinePointsDiagonally()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        AssertDirection(_downLeftDirection, flowField.GetDirection(_centerTile.x, _centerTile.y));
    }

    [Test]
    public void Calculate_WallBetween_PointsAroundWall()
    {
        foreach (Vector2Int wall in _detourWallTiles)
        {
            _smallField.SetObstacle(wall.x, wall.y);
        }

        FlowField flowField = CalculateFlowField(_smallField, _cornerTile);

        AssertDirection(Vector2.up, flowField.GetDirection(_detourStartTile.x, _detourStartTile.y));
    }

    [Test]
    public void Calculate_OneStraightNeighborBlocked_DiagonalIsNotUsed()
    {
        _smallField.SetObstacle(_cornerBlockTile.x, _cornerBlockTile.y);

        FlowField flowField = CalculateFlowField(_smallField, _cornerTile);

        AssertDirection(Vector2.left, flowField.GetDirection(_cornerStartTile.x, _cornerStartTile.y));
    }

    [Test]
    public void Calculate_TargetAndObstacle_AreZero()
    {
        _smallField.SetObstacle(_cornerBlockTile.x, _cornerBlockTile.y);

        FlowField flowField = CalculateFlowField(_smallField, _cornerTile);

        Assert.AreEqual(Vector2.zero, flowField.GetDirection(_cornerTile.x, _cornerTile.y));
        Assert.AreEqual(Vector2.zero, flowField.GetDirection(_cornerBlockTile.x, _cornerBlockTile.y));
    }

    [Test]
    public void Calculate_IsolatedTile_IsZero()
    {
        foreach (Vector2Int wall in _isolatingWallTiles)
        {
            _smallField.SetObstacle(wall.x, wall.y);
        }

        FlowField flowField = CalculateFlowField(_smallField, _cornerTile);

        Assert.AreEqual(Vector2.zero, flowField.GetDirection(_isolatedTile.x, _isolatedTile.y));
    }

    [Test]
    public void Calculate_OpenMap_EveryNonTargetDirectionIsNormalized()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        for (int x = 0; x < OPEN_MAP_SIZE; x++)
        {
            for (int z = 0; z < OPEN_MAP_SIZE; z++)
            {
                if (x == _cornerTile.x && z == _cornerTile.y)
                {
                    continue;
                }

                Assert.AreEqual(UNIT_LENGTH, flowField.GetDirection(x, z).magnitude, FLOAT_TOLERANCE, $"({x}, {z})");
            }
        }
    }

    [Test]
    public void GetDirection_OutOfBounds_IsZero()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        foreach (Vector2Int tile in _outOfBoundsTiles)
        {
            Assert.AreEqual(Vector2.zero, flowField.GetDirection(tile.x, tile.y), $"({tile.x}, {tile.y})");
        }
    }

    [Test]
    public void GetInterpolatedDirection_BetweenTwoDirections_IsBlended()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        AssertDirection((Vector2.left + _downLeftDirection).normalized,
            flowField.GetInterpolatedDirection(_betweenLeftAndDiagonalPosition));
    }

    [Test]
    public void GetInterpolatedDirection_ZeroTileNeighbor_IsExcluded()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        AssertDirection(Vector2.left, flowField.GetInterpolatedDirection(_betweenTargetAndLeftPosition));
    }

    [Test]
    public void GetInterpolatedDirection_AllNeighborsZero_IsZero()
    {
        FlowField flowField = CalculateFlowField(_openField, _cornerTile);

        Assert.AreEqual(Vector2.zero, flowField.GetInterpolatedDirection(_gridCornerPosition));
    }

    private static FlowField CalculateFlowField(CostField costField, Vector2Int target)
    {
        IntegrationField integrationField = new IntegrationField(costField);
        integrationField.Calculate(target);

        FlowField flowField = new FlowField(costField);
        flowField.Calculate(integrationField);
        return flowField;
    }

    private static void AssertDirection(Vector2 expected, Vector2 actual)
    {
        Assert.AreEqual(expected.x, actual.x, FLOAT_TOLERANCE, $"expected {expected}, actual {actual}");
        Assert.AreEqual(expected.y, actual.y, FLOAT_TOLERANCE, $"expected {expected}, actual {actual}");
    }
}
