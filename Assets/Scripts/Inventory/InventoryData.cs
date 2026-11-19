using System;
using UnityEngine;

/// <summary>
/// 아이템 하나 당 이름/아이콘/종류/모양/최대 레벨 정보 데이터
/// </summary>
[CreateAssetMenu(fileName = "New Inventory Datas", menuName = "Data/Inventory Datas")]
public class InventoryData : ScriptableObject
{
    [SerializeField] private int _id;
    [SerializeField] private string _itemName;
    [SerializeField] private Sprite _icon;
    [SerializeField] private string _description;
    [SerializeField] private ItemType _itemType;
    [SerializeField] private Vector2Int[] _itemShape; // 몇칸 차지하는지
    [SerializeField] private int _maxLevel;
    
    
    public int Id => _id;
    public string ItemName => _itemName;
    public Sprite Icon => _icon;
    public ItemType ItemType => _itemType;
    public Vector2Int[] ItemShape => _itemShape;
    public int MaxLevel => _maxLevel;

    public void SetData(int id, string itemName, Sprite icon, ItemType itemType, Vector2Int[] itemShape, int maxLevel)
    {
        _id = id;
        _itemName = itemName;
        _icon = icon;
        _itemType = itemType;
        _itemShape = itemShape;
        _maxLevel = maxLevel;
    }
}

public  enum ItemType
{
    Weapon,
    Passive,
    Expendables, // 소비
}
