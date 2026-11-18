using UnityEngine;

public class Inventory : MonoBehaviour
{

    public class ItemInstance
    {
        public string Name;     // 아이템 이름
        public int Level;       // 아이템 레벨
    }
    
    public class InventoryCell
    {
        public bool IsAvailable;         // 열린 상태인지
        public ItemInstance hasEquipped; // 장비 착용되어있는지
    }

}
