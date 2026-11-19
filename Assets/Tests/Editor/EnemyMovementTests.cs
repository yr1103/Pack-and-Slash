using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EnemyMovement 이동 방향 결정 분기(정지 거리·격자 밖·목적지 타일·일반 타일·방향 0)와 Separation 계산·장애물 진입 방지 검증
/// </summary>
public class EnemyMovementTests
{
    private const int MAP_SIZE = 3;
    private const float FLOAT_TOLERANCE = 0.0001f;
    private const float NO_STOP_DISTANCE = 0f; // 정지 거리 외 분기 확인용
    private const float STOP_DISTANCE_OVER_DETOUR = 2.5f; // _detourStartPosition에서 플레이어까지 거리 2보다 큼
    private const float SEPARATION_RADIUS = 1f;
    private const float STRONG_SEPARATION_WEIGHT = 10f; // Flow 방향보다 Separation이 우세하도록
    private const float NEXT_MOVE_DISTANCE = 0.5f; // _nearWallPosition에서 왼쪽 이동 시 벽 타일 (1, 0) 진입

    // 목적지 (0, 0), 벽 (1, 0)·(1, 1) → (2, 0)은 플레이어 방향(왼쪽)이 아닌 위로 우회
    private static readonly Vector2Int _targetTile = new Vector2Int(0, 0);
    private static readonly Vector2Int[] _wallTiles = { new Vector2Int(1, 0), new Vector2Int(1, 1) };
    private static readonly Vector3 _playerPosition = new Vector3(0.5f, 0f, 0.5f);
    private static readonly Vector3 _outsideHighPosition = new Vector3(10f, 2f, 0.5f); // Y가 달라도 방향 Y는 0
    private static readonly Vector3 _sameTileLeftPosition = new Vector3(0.2f, 0f, 0.5f);
    private static readonly Vector3 _sameTileRightPlayerPosition = new Vector3(0.8f, 0f, 0.5f);
    private static readonly Vector3 _detourStartPosition = new Vector3(2.5f, 0f, 0.5f);
    private static readonly Vector3 _wallPosition = new Vector3(1.5f, 0f, 1.5f);
    private static readonly Vector3 _unusedPreviousDirection = Vector3.back; // 다른 분기에서 쓰이지 않음을 확인
    private static readonly Vector3 _rightNeighborPosition = new Vector3(2.8f, 0f, 0.5f); // _detourStartPosition 오른쪽 0.3
    private static readonly Vector3 _nearWallPosition = new Vector3(2.1f, 0f, 0.5f); // 타일 (2, 0), 왼쪽이 벽

    private CostField _costField;
    private FlowField _flowField;

    [SetUp]
    public void SetUp()
    {
        _costField = new CostField(MAP_SIZE, MAP_SIZE, Vector3.zero);
        foreach (Vector2Int wall in _wallTiles)
        {
            _costField.SetObstacle(wall.x, wall.y);
        }

        IntegrationField integrationField = new IntegrationField(_costField);
        integrationField.Calculate(_targetTile);

        _flowField = new FlowField(_costField);
        _flowField.Calculate(integrationField);
    }

    [Test]
    public void GetMoveDirection_OutsideGrid_PointsToPlayerOnXZ()
    {
        Vector3 direction = GetMoveDirection(_outsideHighPosition, _playerPosition, _unusedPreviousDirection);

        AssertDirection(Vector3.left, direction);
    }

    [Test]
    public void GetMoveDirection_TargetTile_PointsToPlayerPosition()
    {
        Vector3 direction = GetMoveDirection(_sameTileLeftPosition, _sameTileRightPlayerPosition, _unusedPreviousDirection);

        AssertDirection(Vector3.right, direction);
    }

    [Test]
    public void GetMoveDirection_NormalTile_FollowsFlowField()
    {
        Vector3 direction = GetMoveDirection(_detourStartPosition, _playerPosition, _unusedPreviousDirection);

        AssertDirection(Vector3.forward, direction);
    }

    [Test]
    public void GetMoveDirection_ZeroFlowTile_KeepsPreviousDirection()
    {
        Vector3 direction = GetMoveDirection(_wallPosition, _playerPosition, Vector3.right);

        AssertDirection(Vector3.right, direction);
    }

    [Test]
    public void GetMoveDirection_ZeroFlowTileWithoutPrevious_IsZero()
    {
        Vector3 direction = GetMoveDirection(_wallPosition, _playerPosition, Vector3.zero);

        Assert.AreEqual(Vector3.zero, direction);
    }

    [Test]
    public void GetMoveDirection_WithinStopDistance_IsZeroWithoutPrevious()
    {
        Vector3 direction = EnemyMovement.GetMoveDirection(_costField, _flowField, _targetTile,
            _detourStartPosition, _playerPosition, _unusedPreviousDirection, STOP_DISTANCE_OVER_DETOUR);

        Assert.AreEqual(Vector3.zero, direction);
    }

    [Test]
    public void CalculateSeparation_NoNeighbor_IsZero()
    {
        Vector3 separation = EnemyMovement.CalculateSeparation(_detourStartPosition, new Vector3[0], 0, SEPARATION_RADIUS);

        Assert.AreEqual(Vector3.zero, separation);
    }

    [Test]
    public void CalculateSeparation_OneNeighbor_PointsAwayFromNeighbor()
    {
        Vector3[] neighbors = { _rightNeighborPosition };

        Vector3 separation = EnemyMovement.CalculateSeparation(_detourStartPosition, neighbors, neighbors.Length, SEPARATION_RADIUS);

        AssertDirection(Vector3.left, separation.normalized);
    }

    [Test]
    public void CalculateSeparation_ZeroDistanceNeighbor_IsExcluded()
    {
        Vector3[] neighbors = { _detourStartPosition };

        Vector3 separation = EnemyMovement.CalculateSeparation(_detourStartPosition, neighbors, neighbors.Length, SEPARATION_RADIUS);

        Assert.AreEqual(Vector3.zero, separation);
    }

    [Test]
    public void GetSeparatedDirection_NextTileBlocked_KeepsFlowDirection()
    {
        Vector3 direction = EnemyMovement.GetSeparatedDirection(_costField, _nearWallPosition, Vector3.forward,
            Vector3.left, STRONG_SEPARATION_WEIGHT, NEXT_MOVE_DISTANCE);

        AssertDirection(Vector3.forward, direction);
    }

    private Vector3 GetMoveDirection(Vector3 enemyPosition, Vector3 playerPosition, Vector3 previousDirection)
    {
        return EnemyMovement.GetMoveDirection(_costField, _flowField, _targetTile, enemyPosition, playerPosition,
            previousDirection, NO_STOP_DISTANCE);
    }

    private static void AssertDirection(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, FLOAT_TOLERANCE, $"expected {expected}, actual {actual}");
        Assert.AreEqual(expected.y, actual.y, FLOAT_TOLERANCE, $"expected {expected}, actual {actual}");
        Assert.AreEqual(expected.z, actual.z, FLOAT_TOLERANCE, $"expected {expected}, actual {actual}");
    }
}
