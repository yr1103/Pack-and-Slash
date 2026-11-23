using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// DragHandler = 드래그 시작, 드래그 하는 중, 드래그 종료 함수
// PointerClickHandler 이미지 클릭 시 사용하는 함수
public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, ICanvasRaycastFilter
{
    // 회전
    [SerializeField] private InputActionReference rotateAction;
    
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;

    private Inventory.ItemInstance item;
    private RectTransform rectTransform;
    private Vector3 originalPosition;
    private Vector2 dragOffset; // 잡은 지점과 이미지 중심의 화면상 차이
    private Vector2Int grabbedCellOffset; // 잡은 칸 - item.Position
    private bool hasGrabbedCell; // 셀 간격 위를 잡으면 false → 드래그 무효

    // 마지막으로 클릭한 아이템만 회전 입력을 받도록 전체 UI가 공유
    private static InventoryItemUI selectedItem;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    // --- 회전 ------------------------------------
    

    private void OnEnable()
    {
        rotateAction?.action?.Enable();
    }

    private void OnDisable()
    {
        rotateAction?.action?.Disable();
    }

    private void Update()
    {
        if (rotateAction == null || rotateAction.action == null)
            return;

        if (selectedItem != this || item == null)
            return;

        if (rotateAction.action.WasPressedThisFrame())
        {
            bool success = inventory.RotateItem(item);

            if (success)
            {
                RefreshPosition();
                Debug.Log("아이템 회전 성공");
            }
        }
    }
    
    
    //----------------------------------------

    // 표시할 실제 아이템 연결
    public void Initialize(Inventory.ItemInstance target)
    {
        rectTransform = GetComponent<RectTransform>();
        
        item = target;
        RefreshPosition();
    }
    
    // Image 사각형이 아닌 실제 점유 셀만 클릭 판정 (빈 칸 클릭은 아래 아이템으로 통과)
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (item == null)
            return true;

        if (!inventoryUI.TryGetCellPosition(screenPoint, eventCamera, out Vector2Int cell))
            return false;

        return System.Array.IndexOf(inventory.GetRotatedShape(item, item.Rotation), cell - item.Position) >= 0;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        selectedItem = this;

        Debug.Log($"{item?.Data?.ItemName} 선택 완료!");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.position;
        dragOffset = (Vector2)rectTransform.position - eventData.position;

        hasGrabbedCell = false;

        if (item != null && inventoryUI.TryGetCellPosition(eventData, out Vector2Int grabbedCell))
        {
            grabbedCellOffset = grabbedCell - item.Position;
            hasGrabbedCell = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position + dragOffset;
    }


    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("1. 드래그 종료");

        if (item == null)
        {
            Debug.LogError("아이템 데이터가 null");
            rectTransform.position = originalPosition;
            return;
        }

        if (!hasGrabbedCell)
        {
            rectTransform.position = originalPosition;
            return;
        }

        bool isInside = inventoryUI.TryGetCellPosition(
            eventData, out Vector2Int newPosition);

        // 마우스 아래 칸은 잡은 칸이므로, 잡은 칸 오프셋을 빼야 item.Position 기준이 됨
        Vector2Int targetPosition = newPosition - grabbedCellOffset;

        Debug.Log($"2. 그리드 좌표 변환: {isInside}, 좌표: {newPosition}, 배치 좌표: {targetPosition}"
        );

        if (!isInside)
        {
            rectTransform.position = originalPosition;
            return;
        }

        bool success = inventory.MoveItem(item, targetPosition);

        Debug.Log($"3. MoveItem 결과: {success}");

        if (success)
        {
            RefreshPosition();
        }
        else
        {
            rectTransform.position = originalPosition;
        }
    }
    
    private void RefreshPosition()
    {
        // 정규화된 Shape는 최소가 (0,0)이므로 최대 좌표만 구하면 바운딩 박스가 됨
        Vector2Int max = Vector2Int.zero;

        foreach (Vector2Int offset in inventory.GetRotatedShape(item, item.Rotation))
        {
            max = Vector2Int.Max(max, offset);
        }

        Vector3 firstCell = inventoryUI.GetCellWorldPosition(item.Position);
        Vector3 lastCell = inventoryUI.GetCellWorldPosition(item.Position + max);

        // 바운딩 박스의 중심
        rectTransform.position = (firstCell + lastCell) / 2f;

        // 이미지 회전
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, -item.Rotation * 90f);
    }

}


