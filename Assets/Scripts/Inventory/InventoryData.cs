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
    [SerializeField, TextArea] private string _description;
    [SerializeField] private ItemType _itemType;
    [SerializeField] private Vector2Int[] _itemShape; // 몇칸 차지하는지
    [SerializeField] private int _maxLevel;
    [SerializeField] private bool _isSellable = true; // 진화 무기는 false (판매 불가)

    // 무기 설정 참조하는 필드
    [SerializeField] private WeaponDefinition _weaponDefinition;

    // 툴팁 표시 전용 텍스트 (전투 수치와 연결하지 않음, 비어 있으면 툴팁에서 숨김)
    [SerializeField, TextArea] private string _equippedEffect; // 장착 효과
    [SerializeField, TextArea] private string _unequippedBuff; // 해제 시 버프
    [SerializeField, TextArea] private string _unequippedDebuff; // 해제 시 디버프


    public int Id => _id;
    public string ItemName => _itemName;
    public Sprite Icon => _icon;
    public string Description => _description;
    public ItemType ItemType => _itemType;
    public Vector2Int[] ItemShape => _itemShape;
    public int MaxLevel => _maxLevel;
    public bool IsSellable => _isSellable;

    public WeaponDefinition WeaponDefinition => _weaponDefinition;
    public string EquippedEffect => _equippedEffect;
    public string UnequippedBuff => _unequippedBuff;
    public string UnequippedDebuff => _unequippedDebuff;

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
    Essential, // 캐릭터 고유 아이템 (기존 SO 직렬화 정수값 보존을 위해 끝에 추가)
}
