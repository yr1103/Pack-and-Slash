using UnityEngine;

public class InventoryDragTest : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryData pistolData;
    [SerializeField] private InventoryItemUI pistolUI;

    private void Start()
    {
        // 1. 실제 권총 아이템 생성
        Inventory.ItemInstance pistol =
            new Inventory.ItemInstance(pistolData);

        // 2. 인벤토리 (4, 2)에 배치
        bool success = inventory.PlaceItem(
            pistol,
            new Vector2Int(4, 2)
        );

        if (!success)
        {
            Debug.LogError("권총 배치 실패");
            return;
        }

        // 3. UI 이미지와 실제 권총 연결
        pistolUI.Initialize(pistol);

        Debug.Log("권총 배치 성공");
    }
}