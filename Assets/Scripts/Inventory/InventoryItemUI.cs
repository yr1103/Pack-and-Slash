using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// DragHandler = 드래그 시작, 드래그 하는 중, 드래그 종료 함수
// PointerClickHandler 이미지 클릭 시 사용하는 함수
public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, ICanvasRaycastFilter,
    IPointerEnterHandler, IPointerExitHandler
{
    private const int ROTATION_COUNT = 4; // 90도 단위 회전 상태 수
    private const float ROTATION_STEP_ANGLE = 90f;

    private static readonly Vector2 _cellCenterOffset = new Vector2(0.5f, 0.5f); // 칸 좌표 → 칸 중심
    private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(); // 드롭 시 합성 대상 탐색용 재사용 버퍼

    // 회전
    [SerializeField] private InputActionReference rotateAction;

    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private SellConfirmPopup _sellPopup;
    [SerializeField] private ItemTooltipUI _tooltip; // 씬 배치 테스트 아이템은 미연결일 수 있음 (null이면 툴팁 없음)

    private Inventory.ItemInstance item;
    private RectTransform rectTransform;
    private Image _image;
    private Vector3 originalPosition;
    private Vector2 dragOffset; // 잡은 지점과 이미지 중심의 화면상 차이
    private int _grabbedShapeIndex; // 잡은 칸의 Shape 배열 인덱스 (회전과 무관하게 같은 칸)
    private bool hasGrabbedCell; // 셀 간격 위를 잡으면 false → 드래그 무효
    private int _dragRotation; // 드래그 중 미리보기 회전, 드롭 성공 시에만 데이터에 반영

    // 마지막으로 클릭한 아이템만 회전 입력을 받도록 전체 UI가 공유
    private static InventoryItemUI selectedItem;

    // 드래그 중에는 선택 아이템 대신 드래그 아이템만 회전 입력을 받도록 전체 UI가 공유
    private static InventoryItemUI _draggingItem;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
    }

    // --- 회전 ------------------------------------
    

    // 회전 액션은 아이템 UI 전체가 공유하므로 Enable·Disable은 InventoryUI가 담당, 여기서는 읽기만 함
    private void OnDisable()
    {
        // 드래그 중 비활성화되면 OnEndDrag가 오지 않을 수 있어 회전 입력이 막히지 않도록 해제
        if (_draggingItem == this)
            _draggingItem = null;

        // 판매로 비활성화된 아이템이 회전 입력 대상으로 남지 않도록 해제
        if (selectedItem == this)
            selectedItem = null;

        // 판매 등으로 비활성화되면 Exit가 오지 않으므로 직접 숨김
        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    private void Update()
    {
        if (rotateAction == null || rotateAction.action == null)
            return;

        if (!rotateAction.action.WasPressedThisFrame())
            return;

        // 판매 확인 대기 중인 아이템의 회전이 바뀌지 않도록 팝업이 열린 동안 회전 입력 무시
        if (_sellPopup.IsOpen)
            return;

        if (_draggingItem != null)
        {
            // 드래그 무효(셀 간격 잡음)는 드롭 시 위치만 되돌리므로 회전하지 않음
            if (_draggingItem == this && hasGrabbedCell)
                RotateDragPreview();

            return;
        }

        if (selectedItem != this || item == null)
            return;

        bool success = inventory.RotateItem(item);

        if (success)
        {
            // 회전으로 표시 크기가 바뀌어 Temporary 영역을 벗어날 수 있어 좌표 재보정
            // shortcut: Screen Space Overlay 전제(dragOffset과 동일)로 위치를 화면 좌표로 사용, Camera 캔버스 전환 시 카메라 전달 필요
            if (item.Location == ItemLocation.Temporary)
            {
                inventory.MoveToTemporary(item, inventoryUI.ClampToTemporary(
                    rectTransform.position, rectTransform.rect.size, item.Rotation, null), item.Rotation);
            }

            RefreshPosition();
            Debug.Log("아이템 회전 성공");
        }
    }

    // 데이터는 드롭 시 확정하므로 표시만 시계방향 90도 회전, 잡은 지점이 커서 아래에 남도록 커서 기준으로 회전
    private void RotateDragPreview()
    {
        Vector2 cursorPosition = (Vector2)rectTransform.position - dragOffset;

        _dragRotation = (_dragRotation + 1) % ROTATION_COUNT;

        // 화면 좌표(y 위쪽)에서 시계방향 90도 = (x, y) → (y, -x), 이미지 회전(-각도) 방향과 동일
        dragOffset = new Vector2(dragOffset.y, -dragOffset.x);
        rectTransform.position = cursorPosition + dragOffset;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_dragRotation * ROTATION_STEP_ANGLE);
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

        // 보관함은 축소 표시라 사각형 전체 판정
        if (item.Location == ItemLocation.Storage)
            return true;

        if (item.Location == ItemLocation.Temporary)
            return GetShapeIndexAt(screenPoint, eventCamera, false) >= 0;

        if (!inventoryUI.TryGetCellPosition(screenPoint, eventCamera, out Vector2Int cell))
            return false;

        return System.Array.IndexOf(inventory.GetRotatedShape(item, item.Rotation), cell - item.Position) >= 0;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        transform.SetAsLastSibling();
        selectedItem = this;

        Debug.Log($"{item?.Data?.ItemName} 선택 완료");
    }

    // 드래그 중에는 다른 아이템 위를 지나도 툴팁을 띄우지 않음
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null || item == null || eventData.dragging || _draggingItem != null)
            return;

        _tooltip.Show(item, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_tooltip != null)
            _tooltip.Hide(this);

        transform.SetAsLastSibling();
        originalPosition = rectTransform.position;
        hasGrabbedCell = false;
        _draggingItem = this;

        if (item != null)
            _dragRotation = item.Rotation;

        // Temporary·Storage는 그리드 밖이라 이미지 기준으로 잡은 칸을 구하고, 빈 칸을 잡아도 항상 꺼낼 수 있게 가장 가까운 칸 사용
        if (item != null && item.Location != ItemLocation.Inventory)
        {
            _grabbedShapeIndex = GetShapeIndexAt(eventData.position, eventData.pressEventCamera, true);
            hasGrabbedCell = true;

            // 보관함 축소 스케일을 원래대로 되돌리되, 잡은 지점이 커서 아래에 남도록 위치 보정
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 grabbedLocalPoint);
            rectTransform.localScale = Vector3.one;
            rectTransform.position += (Vector3)(eventData.position - (Vector2)rectTransform.TransformPoint(grabbedLocalPoint));
        }

        dragOffset = (Vector2)rectTransform.position - eventData.position;

        if (item != null && item.Location == ItemLocation.Inventory
            && inventoryUI.TryGetCellPosition(eventData, out Vector2Int grabbedCell))
        {
            _grabbedShapeIndex = System.Array.IndexOf(
                inventory.GetRotatedShape(item, item.Rotation), grabbedCell - item.Position);
            hasGrabbedCell = _grabbedShapeIndex >= 0;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position + dragOffset;
    }


    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("1. 드래그 종료");
        _draggingItem = null;

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

        // 합성 불가면 아래 영역별 처리로 그대로 진행
        if (TryMergeIntoItemUnderPointer(eventData))
            return;

        bool isInside = inventoryUI.TryGetCellPosition(
            eventData, out Vector2Int newPosition);

        // 마우스 아래 칸은 잡은 칸이므로, 미리보기 회전 기준 잡은 칸 오프셋을 빼야 item.Position 기준이 됨
        Vector2Int grabbedCellOffset = inventory.GetRotatedShape(item, _dragRotation)[_grabbedShapeIndex];
        Vector2Int targetPosition = newPosition - grabbedCellOffset;

        Debug.Log($"2. 그리드 좌표 변환: {isInside}, 좌표: {newPosition}, 배치 좌표: {targetPosition}");

        if (isInside)
        {
            bool success = inventory.TryPlace(item, targetPosition, _dragRotation);
            Debug.Log($"3. TryPlace 결과: {success}");
        }
        else if (inventoryUI.IsOverSellArea(eventData.position, eventData.pressEventCamera))
        {
            // 데이터는 팝업 확인 시에만 바뀌므로 아래 RefreshPosition으로 원위치·원회전 복귀 (미리보기 회전 버림), 판매 불가면 복귀만
            if (inventory.CanSell(item))
                _sellPopup.Open(item, this);
        }
        else if (inventoryUI.IsOverStorage(eventData.position, eventData.pressEventCamera))
        {
            inventory.TryStore(item);
        }
        else
        {
            // 드롭 시 이미지 중심 기준으로 Temporary 영역 안 가장 가까운 위치
            Vector2 temporaryPosition = inventoryUI.ClampToTemporary(
                eventData.position + dragOffset, rectTransform.rect.size, _dragRotation, eventData.pressEventCamera);
            inventory.MoveToTemporary(item, temporaryPosition, _dragRotation);
        }

        // 실패 시에도 데이터는 원래 영역·회전 상태 그대로라 다시 표시하면 원위치·원회전 복귀
        RefreshPosition();
    }

    // 포인터 아래 가장 위의 다른 아이템과 합성, 성공 시 재료(자신) UI 비활성화 (빈 칸은 IsRaycastLocationValid가 제외)
    private bool TryMergeIntoItemUnderPointer(PointerEventData eventData)
    {
        EventSystem.current.RaycastAll(eventData, _raycastResults);

        InventoryItemUI targetUI = null;

        foreach (RaycastResult result in _raycastResults)
        {
            InventoryItemUI hitItemUI = result.gameObject.GetComponent<InventoryItemUI>();

            if (hitItemUI != null && hitItemUI != this)
            {
                targetUI = hitItemUI;
                break;
            }
        }

        _raycastResults.Clear();

        if (targetUI == null)
            return false;

        // 합성 성공 시 재료·대상 레벨이 같으므로 재료 레벨로 기록 (대상 item은 미초기화일 수 있음)
        int previousLevel = item.Level;

        if (!inventory.TryMerge(item, targetUI.item))
            return false;

        string itemName = item.Data.ItemName;
        Debug.Log($"[InventoryMerge] {itemName} Lv.{previousLevel} + {itemName} Lv.{previousLevel} → {itemName} Lv.{targetUI.item.Level}");

        // shortcut: 결과 아이템 UI에 레벨 표시가 없어 갱신하지 않음, 레벨 표시 UI 추가 시 대상 UI 갱신 필요

        // 판매와 같은 방식, OnDisable이 툴팁·선택·드래그 상태 정리
        gameObject.SetActive(false);
        return true;
    }

    private void RefreshPosition()
    {
        if (item.Location == ItemLocation.Temporary)
        {
            rectTransform.position = inventoryUI.GetTemporaryWorldPosition(item.TemporaryPosition);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, -item.Rotation * 90f);
            rectTransform.localScale = Vector3.one;
            return;
        }

        if (item.Location == ItemLocation.Storage)
        {
            RefreshStoragePosition();
            return;
        }

        rectTransform.localScale = Vector3.one;

        // 정규화된 Shape는 최소가 (0,0)이므로 최대 좌표만 구하면 됨
        Vector2Int max = Vector2Int.zero;

        foreach (Vector2Int offset in inventory.GetRotatedShape(item, item.Rotation))
        {
            max = Vector2Int.Max(max, offset);
        }

        Vector3 firstCell = inventoryUI.GetCellWorldPosition(item.Position);
        Vector3 lastCell = inventoryUI.GetCellWorldPosition(item.Position + max);

        // 아이템 이미지 중심
        rectTransform.position = (firstCell + lastCell) / 2f;

        // 이미지 회전
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, -item.Rotation * 90f);
    }

    // 슬롯 중심에 회전 0으로 두고, 슬롯보다 크면 비율 유지 축소 (확대는 하지 않음)
    private void RefreshStoragePosition()
    {
        RectTransform slot = inventoryUI.GetStorageSlot(inventory.GetStorageIndex(item));
        Vector2 itemSize = rectTransform.rect.size;
        float scale = Mathf.Min(1f, Mathf.Min(slot.rect.width / itemSize.x, slot.rect.height / itemSize.y));

        rectTransform.position = slot.TransformPoint(slot.rect.center);
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    // 화면 좌표가 이미지의 어느 Shape 칸인지 찾아 Shape 배열 인덱스 반환 (회전 0·현재 회전 Shape는 같은 인덱스가 같은 칸)
    // 이미지 크기가 Shape × 셀 간격과 다를 수 있어 이미지를 회전 0 바운딩박스 칸 수로 나눠 비율로 계산
    private int GetShapeIndexAt(Vector2 screenPoint, Camera eventCamera, bool useNearest)
    {
        // 이미지 자체 로컬 좌표는 회전·스케일 이전 기준이라 회전 0 Shape와 바로 대응
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPoint, eventCamera, out Vector2 localPoint);

        Vector2Int[] shape = inventory.GetRotatedShape(item, 0);
        Vector2Int boundsSize = Vector2Int.zero;

        foreach (Vector2Int offset in shape)
        {
            boundsSize = Vector2Int.Max(boundsSize, offset + Vector2Int.one);
        }

        Rect rect = rectTransform.rect;

        // Shape y는 아래로 증가하므로 이미지 위쪽에서부터 계산
        Vector2 cellPoint = new Vector2(
            (localPoint.x - rect.xMin) / rect.width * boundsSize.x,
            (rect.yMax - localPoint.y) / rect.height * boundsSize.y);

        // 칸 중심이 가장 가까운 칸 = 점을 포함하는 칸 (포함 칸이 Shape에 없으면 가장 가까운 Shape 칸)
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < shape.Length; i++)
        {
            float distance = (cellPoint - ((Vector2)shape[i] + _cellCenterOffset)).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        if (!useNearest && Vector2Int.FloorToInt(cellPoint) != shape[nearestIndex])
            return -1;

        return nearestIndex;
    }

    /// <summary>런타임 생성 아이템용 의존성 주입, 아이콘·크기 설정 후 표시 (표시 위치를 데이터에서 읽으므로 영역 이동 후 호출)</summary>
    public void Initialize(Inventory.ItemInstance target, Inventory targetInventory, InventoryUI targetInventoryUI,
        SellConfirmPopup sellPopup, ItemTooltipUI tooltip)
    {
        inventory = targetInventory;
        inventoryUI = targetInventoryUI;
        _sellPopup = sellPopup;
        _tooltip = tooltip;

        _image.sprite = target.Data.Icon;

        // 회전은 localRotation으로 표시하므로 크기는 회전 0 기준
        Vector2 size = inventoryUI.GetItemSize(inventory.GetRotatedShape(target, 0));
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

        Initialize(target);
    }
}


