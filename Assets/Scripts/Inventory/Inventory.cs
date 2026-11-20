
using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private const int Width = 10;
    private const int Height = 6;
    
    // 각 셀 정보(구조체)
    // 깊은 복사 할 일 없고 GC 부담 적음
    public struct InventoryCell
    {
        public bool IsAvailable;
        public ItemInstance Occupant;
    }

    // 10x6 배열 생성
    private InventoryCell[,] _cells = new InventoryCell[Width, Height];
    
    // ------------------------------------------------------------------------------------
    
    private void Awake()
    {
        Init();
    }
    
    // ------------------------------------------------------------------------------------
    
    // 실제 아이템 하나의 정보
    public class ItemInstance
    {
        public InventoryData Data;
        public int Level;
        public Vector2Int Position;
        public int RotationIndex; 

        public ItemInstance(InventoryData data)
        {
            Data = data;
            Level = 1;
            Position = Vector2Int.zero;
            RotationIndex = 0;
        }
    }

    // 2. 아이템 회전 좌표 계산
    private Vector2Int[] GetRotatedShape(
        Vector2Int[] originalShape,
        int rotationIndex)
    {
        Vector2Int[] rotated =
            (Vector2Int[])originalShape.Clone();

        for (int r = 0; r < rotationIndex % 4; r++)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;

            for (int i = 0; i < rotated.Length; i++)
            {
                int x = rotated[i].x;
                int y = rotated[i].y;

                // 좌하단 원점, 시계 방향 90도
                rotated[i] = new Vector2Int(y, -x);

                minX = Mathf.Min(minX, rotated[i].x);
                minY = Mathf.Min(minY, rotated[i].y);
            }

            // 회전 후 좌표를 0부터 시작하도록 보정
            for (int i = 0; i < rotated.Length; i++)
            {
                rotated[i] -= new Vector2Int(minX, minY);
            }
        }

        return rotated;
    }

    // 3. 배치 가능 여부 검사
    private bool CanPlace(
        ItemInstance item,
        Vector2Int position,
        int rotationIndex)
    {
        Vector2Int[] shape =
            GetRotatedShape(item.Data.ItemShape, rotationIndex);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int target = position + offset;

            // 배열 범위 검사
            if (target.x < 0 || target.x >= Width ||
                target.y < 0 || target.y >= Height)
            {
                return false;
            }

            InventoryCell cell = _cells[target.x, target.y];

            // 잠긴 칸 검사
            if (!cell.IsAvailable)
                return false;

            // 다른 아이템 점유 검사
            if (cell.Occupant != null)
                return false;
        }

        return true;
    }

    // 4. 아이템 배치
    private bool TryPlace(
        ItemInstance item,
        Vector2Int position,
        int rotationIndex)
    {
        if (!CanPlace(item, position, rotationIndex))
            return false;

        Vector2Int[] shape =
            GetRotatedShape(item.Data.ItemShape, rotationIndex);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int target = position + offset;

            _cells[target.x, target.y].Occupant = item;
        }

        item.Position = position;
        item.RotationIndex = rotationIndex % 4;

        return true;
    }
    
    
    // 인벤토리 초기화
    private void Init()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _cells[x, y].IsAvailable =
                    x >= 3 && x <= 6 &&
                    y >= 1 && y <= 4;
            }
        }
    }
}
