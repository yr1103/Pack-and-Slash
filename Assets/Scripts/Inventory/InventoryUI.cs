using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 화면 좌표를 인벤토리 셀 좌표로 변환하는 역할
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private RectTransform itemLayer;

    [SerializeField] private float cellSize = 64f;
    [SerializeField] private float spacing = 2f;

    private const int Width = 10;
    private const int Height = 6;
    
    
    // 화면의 마우스 위치를 인벤토리 좌표로 변환
    public bool TryGetCellPosition(PointerEventData eventData, out Vector2Int cellPosition)
    {
        cellPosition = Vector2Int.zero;

        Vector2 localPoint;

        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );

        if (!success)
            return false;

        // Grid의 좌상단 기준 좌표로 변경
        float x = localPoint.x + gridRect.rect.width * gridRect.pivot.x;
        float y = gridRect.rect.height * (1f - gridRect.pivot.y)
                  - localPoint.y;

        // 실제 셀 간격
        float step = cellSize + spacing;

        int cellX = Mathf.FloorToInt(x / step);
        int cellY = Mathf.FloorToInt(y / step);

        // 10x6 범위인지 검사
        if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height)
            return false;

        // 셀 사이의 빈 간격 위인지 검사
        if (x - cellX * step >= cellSize || y - cellY * step >= cellSize)
            return false;

        cellPosition = new Vector2Int(cellX, cellY);
        return true;
    }

    // 셀 좌표를 UI 위치로 변환
    public Vector3 GetCellWorldPosition(Vector2Int cell)
    {
        float x = cell.x * (cellSize + spacing) + cellSize / 2f;
        float y = cell.y * (cellSize + spacing) + cellSize / 2f;

        Vector3 localPosition = new Vector3(
            -gridRect.rect.width * gridRect.pivot.x + x,
            gridRect.rect.height * (1f - gridRect.pivot.y) - y,
            0f
        );

        return gridRect.TransformPoint(localPosition);
    }
}
