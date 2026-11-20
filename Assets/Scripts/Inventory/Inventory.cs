
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
    
    // ---이벤트 함수------------------------------------------------------------------------
    private void Awake()
    {
        Init();
    }
    // ------------------------------------------------------------------------------------
    
    // 실제 아이템 하나의 정보
    public class ItemInstance
    {
        public InventoryData Data;      // SO에서 생김새 가져오기
        public int Level;               // 아이템 레벨
        public Vector2Int Position;     // 위치 정보
        public int RotationIndex;       // 회전 정보 / 0도 회전 0 /  90 : 1 / 180 : 2 / 270 : 3

        // 생성자
        public ItemInstance(InventoryData data) 
        {
            Data = data;
            Level = 1;
            Position = Vector2Int.zero;
            RotationIndex = 0;
        }
    }

    // 아이템 회전 좌표 계산
    private Vector2Int[] GetRotatedShape(Vector2Int[] originalShape, int rotationIndex)
    {
        Vector2Int[] rotated = (Vector2Int[])originalShape.Clone();

        // 회전시 좌표 정규화
        for (int r = 0; r < rotationIndex % 4; r++)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;

            for (int i = 0; i < rotated.Length; i++)
            {
                int x = rotated[i].x;
                int y = rotated[i].y;

                // 좌하단 원점, 시계 방향 90도 처리
                rotated[i] = new Vector2Int(y, -x);
                // 최소값 받아와서
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

    // 배치 가능 여부 검증 : 아이템, 위치, 회전 정보
    private bool CanPlace(ItemInstance item, Vector2Int position, int rotationIndex)
    {
        Vector2Int[] shape = GetRotatedShape(item.Data.ItemShape, rotationIndex);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int target = position + offset;

            // 배열 범위 검사
            if (target.x < 0 || target.x >= Width || target.y < 0 || target.y >= Height)
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

    // 아이템 배치
    public bool TryPlace(
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

    // 특정 좌표의 셀을 조회
    public InventoryCell GetCell(int x, int y)
    {
        return _cells[x, y];
    }
    
    // 인벤토리 초기화
    private void Init()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // 초반 4x4 열린 상태로
                _cells[x, y].IsAvailable = 
                    x >= 3 && x <= 6 && y >= 1 && y <= 4;
            }
        }
    }
}
