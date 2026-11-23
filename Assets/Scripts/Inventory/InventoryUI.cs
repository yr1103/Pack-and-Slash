using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 좌표↔인벤토리 셀 좌표 변환, Temporary 영역 좌표 보정, Storage·판매 영역 판정
/// 회전 InputAction 활성화의 단일 소유자
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private RectTransform itemLayer;

    [SerializeField] private float cellSize = 64f;
    [SerializeField] private float spacing = 2f;

    [SerializeField] private RectTransform _temporaryArea; // LevelUpPanel 하단 자유 배치 영역
    [SerializeField] private RectTransform _storagePanel;
    [SerializeField] private RectTransform[] _storageSlots; // 인덱스 = Inventory 보관함 슬롯 인덱스
    [SerializeField] private RectTransform _sellArea; // 드롭 시 판매 확인 팝업 표시
    [SerializeField] private InputActionReference _rotateAction; // 아이템 UI 전체가 공유하는 회전 액션

    private const int Width = 10;
    private const int Height = 6;

    // 아이템 UI 하나가 비활성화(판매)돼도 공유 액션이 꺼지지 않도록 패널 수명 기준으로 활성화 소유
    private void OnEnable()
    {
        _rotateAction.action.Enable();
    }

    private void OnDisable()
    {
        _rotateAction.action.Disable();
    }

    // 화면의 마우스 위치를 인벤토리 좌표로 변환
    public bool TryGetCellPosition(PointerEventData eventData, out Vector2Int cellPosition)
    {
        return TryGetCellPosition(eventData.position, eventData.pressEventCamera, out cellPosition);
    }

    // 레이캐스트 필터처럼 PointerEventData 없이 화면 좌표만 있는 경우용
    public bool TryGetCellPosition(Vector2 screenPosition, Camera eventCamera, out Vector2Int cellPosition)
    {
        cellPosition = Vector2Int.zero;

        Vector2 localPoint;

        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                screenPosition,
                eventCamera,
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

    /// <summary>화면 좌표가 보관함 패널 위인지 판정</summary>
    public bool IsOverStorage(Vector2 screenPosition, Camera eventCamera)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_storagePanel, screenPosition, eventCamera);
    }

    /// <summary>화면 좌표가 판매 영역 위인지 판정</summary>
    public bool IsOverSellArea(Vector2 screenPosition, Camera eventCamera)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_sellArea, screenPosition, eventCamera);
    }

    /// <summary>아이템 전체가 Temporary 영역 안에 들어오는 가장 가까운 위치 계산</summary>
    /// <param name="screenPosition">아이템 이미지 중심의 화면 좌표</param>
    /// <param name="itemSize">회전 0 기준 아이템 이미지 크기</param>
    /// <returns>Temporary 영역 기준 로컬 좌표</returns>
    public Vector2 ClampToTemporary(Vector2 screenPosition, Vector2 itemSize, int rotation, Camera eventCamera)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _temporaryArea, screenPosition, eventCamera, out Vector2 localPoint);

        // 90·270도(홀수 회전)면 표시 가로·세로가 바뀜
        if (rotation % 2 != 0)
            itemSize = new Vector2(itemSize.y, itemSize.x);

        Rect area = _temporaryArea.rect;

        return new Vector2(
            ClampAxis(localPoint.x, area.xMin, area.xMax, itemSize.x),
            ClampAxis(localPoint.y, area.yMin, area.yMax, itemSize.y));
    }

    /// <summary>Temporary 영역 로컬 좌표를 UI 위치로 변환</summary>
    public Vector3 GetTemporaryWorldPosition(Vector2 temporaryPosition)
    {
        return _temporaryArea.TransformPoint(temporaryPosition);
    }

    /// <summary>보관함 슬롯 표시 위치·크기 기준</summary>
    public RectTransform GetStorageSlot(int index)
    {
        return _storageSlots[index];
    }

    // 아이템 중심을 영역 안으로 제한, 아이템이 영역보다 크면 그 축은 영역 중앙
    private static float ClampAxis(float center, float min, float max, float size)
    {
        if (size > max - min)
            return (min + max) / 2f;

        float halfSize = size / 2f;
        return Mathf.Clamp(center, min + halfSize, max - halfSize);
    }
}
