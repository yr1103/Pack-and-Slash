using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory _inventory;
    [SerializeField] private InventoryCellUI[] _cellUIs;

    private Inventory.ItemInstance _selectedItem;

    // 레벨업 보상에서 선택된 아이템 전달
    public void SelectReward(InventoryData data)
    {
        _selectedItem = new Inventory.ItemInstance(data);
    }

    // 개별 셀 클릭 시 호출
    public void OnCellClicked(Vector2Int position)
    {
        if (_selectedItem == null)
            return;

        bool success = _inventory.TryPlace(_selectedItem, position, 0);

        if (!success)
        {
            Debug.Log("배치 불가능한 위치");
            return;
        }

        _selectedItem = null;
        RefreshGrid();
    }

    // 전체 격자 UI 갱신
    public void RefreshGrid()
    {
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                int index = y * 10 + x;

                _cellUIs[index].Refresh(_inventory.GetCell(x, y));
            }
        }
    }
}