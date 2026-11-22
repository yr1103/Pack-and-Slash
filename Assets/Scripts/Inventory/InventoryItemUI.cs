using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// DragHandler = 드래그 시작, 드래그 하는 중, 드래그 종료 함수
// PointerClickHandler 이미지 클릭 시 사용하는 함수
public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    // 회전
    [SerializeField] private InputActionReference rotateAction;
    
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;

    private Inventory.ItemInstance item;
    private RectTransform rectTransform;
    private Vector3 originalPosition;
    
    private bool isSelected;

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

        if (!isSelected || item == null)
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
        item = target;
        RefreshPosition();
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        isSelected = true;

        Debug.Log("권총 선택 완료!");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;
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

        bool isInside = inventoryUI.TryGetCellPosition(
            eventData, out Vector2Int newPosition);

        Debug.Log(
            $"2. 그리드 좌표 변환: {isInside}, 좌표: {newPosition}"
        );

        if (!isInside)
        {
            rectTransform.position = originalPosition;
            return;
        }

        bool success = inventory.MoveItem(item, newPosition);

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
    
    // 현재 좌표 중심으로 이미지 위치해서 어색함.

    private void RefreshPosition()
    {
        Vector3 firstCell =
            inventoryUI.GetCellWorldPosition(item.Position);

        Vector3 secondCell;

        // 세로 방향
        if (item.Rotation % 2 == 0)
        {
            secondCell = inventoryUI.GetCellWorldPosition(
                item.Position + Vector2Int.up
            );
        }
        // 가로 방향
        else
        {
            secondCell = inventoryUI.GetCellWorldPosition(
                item.Position + Vector2Int.right
            );
        }

        // 두 칸의 중심
        rectTransform.position =
            (firstCell + secondCell) / 2f;

        // 이미지 회전
        rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, -item.Rotation * 90f);
    }

}


