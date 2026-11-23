using UnityEngine;

public class Inventory : MonoBehaviour
{
    private const int Width = 10;
    private const int Height = 6;

    private InventoryCell[,] cells = new InventoryCell[Width, Height];

    // 실제 아이템 하나
    public class ItemInstance
    {
        public InventoryData Data;
        public int Level = 1;
        public Vector2Int Position;
        public int Rotation;
        public bool IsPlaced;

        public ItemInstance(InventoryData data)
        {
            Data = data;
        }
    }

    // 인벤토리 한 칸
    public class InventoryCell
    {
        public bool IsAvailable;
        public ItemInstance hasEquipped;
    }

    private void Awake()
    {
        InitializeGrid();
    }

    // 1. 60칸 생성 및 중앙 4x4 활성화
    private void InitializeGrid()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                cells[x, y] = new InventoryCell();

                cells[x, y].IsAvailable =
                    x >= 3 && x <= 6 &&
                    y >= 1 && y <= 4;
            }
        }
    }

    // 2. 회전된 Shape 좌표 계산
    /// <summary>회전 적용 후 최소 좌표가 (0,0)이 되도록 정규화한 Shape (UI 표시 위치 계산에도 사용)</summary>
    public Vector2Int[] GetRotatedShape(
        ItemInstance item, int rotation)
    {
        Vector2Int[] original = item.Data.ItemShape;
        Vector2Int[] result = new Vector2Int[original.Length];

        for (int i = 0; i < original.Length; i++)
        {
            Vector2Int pos = original[i];

            for (int r = 0; r < rotation; r++)
            {
                pos = new Vector2Int(-pos.y, pos.x);
            }

            result[i] = pos;
        }

        // 음수 좌표 정규화
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (Vector2Int pos in result)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.y < minY) minY = pos.y;
        }

        for (int i = 0; i < result.Length; i++)
        {
            result[i] -= new Vector2Int(minX, minY);
        }

        return result;
    }

    // 3. 배치 가능한지 검사
    public bool CanPlace(
        ItemInstance item, Vector2Int position, int rotation)
    {
        if (item == null || item.Data == null ||
            item.Data.ItemShape == null ||
            item.Data.ItemShape.Length == 0)
            return false;

        Vector2Int[] shape = GetRotatedShape(item, rotation);

        foreach (Vector2Int offset in shape)
        {
            int x = position.x + offset.x;
            int y = position.y + offset.y;

            // 배열 범위 검사
            if (x < 0 || x >= Width ||
                y < 0 || y >= Height)
                return false;

            // 잠금 셀 검사
            if (!cells[x, y].IsAvailable)
                return false;

            // 다른 아이템이 점유 중인지 검사
            ItemInstance occupant = cells[x, y].hasEquipped;

            if (occupant != null && occupant != item)
                return false;
        }

        return true;
    }

    // 4. 실제 셀 점유
    private void FillCells(ItemInstance item)
    {
        Vector2Int[] shape =
            GetRotatedShape(item, item.Rotation);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int pos = item.Position + offset;
            cells[pos.x, pos.y].hasEquipped = item;
        }
    }

    // 5. 아이템 최초 배치
    public bool PlaceItem(ItemInstance item, Vector2Int position)
    {
        if (item == null || item.IsPlaced)
            return false;

        if (!CanPlace(item, position, item.Rotation))
            return false;

        item.Position = position;
        item.IsPlaced = true;

        FillCells(item);
        return true;
    }

    // 6. 기존 점유 셀 해제
    public void RemoveItem(ItemInstance item)
    {
        if (item == null || !item.IsPlaced)
            return;

        Vector2Int[] shape =
            GetRotatedShape(item, item.Rotation);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int pos = item.Position + offset;
            cells[pos.x, pos.y].hasEquipped = null;
        }

        item.IsPlaced = false;
    }

    // 7. 아이템 이동
    public bool MoveItem(ItemInstance item, Vector2Int newPosition)
    {
        if (item == null || !item.IsPlaced)
            return false;

        if (!CanPlace(item, newPosition, item.Rotation))
            return false;

        RemoveItem(item);

        item.Position = newPosition;
        item.IsPlaced = true;
        FillCells(item);

        return true;
    }

    // 8. 90도 회전
    public bool RotateItem(ItemInstance item)
    {
        if (item == null || !item.IsPlaced)
            return false;

        int nextRotation = (item.Rotation + 1) % 4;

        if (!CanPlace(item, item.Position, nextRotation))
            return false;

        RemoveItem(item);

        item.Rotation = nextRotation;
        item.IsPlaced = true;
        FillCells(item);

        return true;
    }
}
