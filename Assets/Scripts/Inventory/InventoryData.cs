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

    // 무기 설정 참조하는 필드
    [SerializeField] private WeaponDefinition _weaponDefinition;
    
    
    public int Id => _id;
    public string ItemName => _itemName;
    public Sprite Icon => _icon;
    public string Description => _description;
    public ItemType ItemType => _itemType;
    public Vector2Int[] ItemShape => _itemShape;
    public int MaxLevel => _maxLevel;

    public WeaponDefinition WeaponDefinition => _weaponDefinition;
    
    public void SetData(int id, string itemName, Sprite icon, ItemType itemType, string description, Vector2Int[] itemShape, int maxLevel, WeaponDefinition weaponDefinition)
    {
        _id = id;
        _itemName = itemName;
        _icon = icon;
        _description = description;
        _itemType = itemType;
        _itemShape = itemShape;
        _maxLevel = maxLevel;
        _weaponDefinition = weaponDefinition;
    }
}

public  enum ItemType
{
    Weapon,
    Passive,
    Expendables, // 소비
}
