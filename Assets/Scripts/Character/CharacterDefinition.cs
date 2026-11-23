using UnityEngine;

/// <summary>
/// 캐릭터별 선택 화면 표시 정보와 시작 무기 데이터
/// </summary>
[CreateAssetMenu(fileName = "New Character Definition", menuName = "Data/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _portrait;
    [SerializeField] private string _startWeaponName; // 선택 화면 표시용
    [SerializeField] private WeaponDefinition _startWeapon; // 고유 무기 (주무기 재선정 우선순위 기준)
    [SerializeField] private InventoryData _startWeaponItem; // 시작 시 Inventory에 지급하는 무기 아이템 (_startWeapon과 같은 무기)
    [SerializeField] private InventoryData _uniqueItem; // 시작 시 Inventory에 지급하는 고유 아이템

    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Portrait => _portrait;
    public string StartWeaponName => _startWeaponName;
    public WeaponDefinition StartWeapon => _startWeapon;
    public InventoryData StartWeaponItem => _startWeaponItem;
    public InventoryData UniqueItem => _uniqueItem;
}
