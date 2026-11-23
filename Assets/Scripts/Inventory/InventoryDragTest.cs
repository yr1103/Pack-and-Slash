using System;
using UnityEngine;

// 다중 아이템 배치 테스트 전용, 테스트 종료 후 삭제
public class InventoryDragTest : MonoBehaviour
{
    [Serializable]
    private class TestItem
    {
        public InventoryData data;
        public InventoryItemUI ui;
        public Vector2Int position;
    }

    [SerializeField] private Inventory _inventory;
    [SerializeField] private TestItem[] _testItems;

    private void Start()
    {
        foreach (TestItem testItem in _testItems)
        {
            Inventory.ItemInstance item =
                new Inventory.ItemInstance(testItem.data);

            if (!_inventory.PlaceItem(item, testItem.position))
            {
                Debug.LogError($"{testItem.data?.ItemName} 배치 실패: {testItem.position}");
                continue;
            }

            // UI 이미지와 실제 아이템 연결
            testItem.ui.Initialize(item);
        }
    }
}
