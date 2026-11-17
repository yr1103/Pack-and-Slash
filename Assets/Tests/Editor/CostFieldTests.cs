using NUnit.Framework;
using UnityEngine;

/// <summary>
/// CostField 좌표 변환(원점 반영)·범위 밖 처리·장애물 설정·고립 영역 채우기 검증
/// </summary>
public class CostFieldTests
{
    private const int MAP_SIZE = 100;
    private const int SMALL_MAP_SIZE = 5;
    private const int WALL_COLUMN = 3; // 작은 맵에서 이 열 전체를 막아 오른쪽 열을 고립
    private const byte WALKABLE_COST = 1;
    private const byte OBSTACLE_COST = 255;

    private static readonly Vector3 _mapOrigin = new Vector3(-50f, 0f, -50f);
    private static readonly Vector3 _firstTileCenter = new Vector3(-49.5f, 0f, -49.5f);
    private static readonly Vector2Int _worldOriginTile = new Vector2Int(50, 50);
    private static readonly Vector2Int[] _sampleTiles = { new Vector2Int(0, 0), new Vector2Int(50, 50), new Vector2Int(99, 99), new Vector2Int(12, 87) };
    private static readonly Vector2Int[] _outOfBoundsTiles = { new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(100, 0), new Vector2Int(0, 100) };
    private static readonly Vector2Int _obstacleTile = new Vector2Int(10, 20);

    private CostField _costField;

    [SetUp]
    public void SetUp()
    {
        _costField = new CostField(MAP_SIZE, MAP_SIZE, _mapOrigin);
    }

    [Test]
    public void TileToWorld_AddsOriginAndTileCenter()
    {
        Assert.AreEqual(_firstTileCenter, _costField.TileToWorld(0, 0));
    }

    [Test]
    public void WorldToTile_ReflectsOrigin()
    {
        Assert.AreEqual(_worldOriginTile, _costField.WorldToTile(Vector3.zero));
    }

    [Test]
    public void WorldToTile_RoundTripsTileToWorld()
    {
        foreach (Vector2Int tile in _sampleTiles)
        {
            Assert.AreEqual(tile, _costField.WorldToTile(_costField.TileToWorld(tile.x, tile.y)));
        }
    }

    [Test]
    public void IsWalkable_OutOfBounds_ReturnsFalse()
    {
        foreach (Vector2Int tile in _outOfBoundsTiles)
        {
            Assert.IsFalse(_costField.IsWalkable(tile.x, tile.y), tile.ToString());
        }
    }

    [Test]
    public void SetObstacle_MakesTileUnwalkableWithMaxCost()
    {
        Assert.AreEqual(WALKABLE_COST, _costField.GetCost(_obstacleTile.x, _obstacleTile.y));

        _costField.SetObstacle(_obstacleTile.x, _obstacleTile.y);

        Assert.IsFalse(_costField.IsWalkable(_obstacleTile.x, _obstacleTile.y));
        Assert.AreEqual(OBSTACLE_COST, _costField.GetCost(_obstacleTile.x, _obstacleTile.y));
    }

    [Test]
    public void FillUnreachable_FillsOnlyRegionCutOffFromStart()
    {
        CostField smallField = new CostField(SMALL_MAP_SIZE, SMALL_MAP_SIZE, Vector3.zero);
        for (int z = 0; z < SMALL_MAP_SIZE; z++)
        {
            smallField.SetObstacle(WALL_COLUMN, z);
        }

        int filledCount = smallField.FillUnreachable(Vector2Int.zero);

        Assert.AreEqual(SMALL_MAP_SIZE, filledCount);
        for (int x = 0; x < SMALL_MAP_SIZE; x++)
        {
            for (int z = 0; z < SMALL_MAP_SIZE; z++)
            {
                byte expected = x < WALL_COLUMN ? WALKABLE_COST : OBSTACLE_COST;
                Assert.AreEqual(expected, smallField.GetCost(x, z), $"({x}, {z})");
            }
        }
    }
}
