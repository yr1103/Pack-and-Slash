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
    [SerializeField] private Sprite[] _levelIcons; // 인덱스 0 = Lv.2, 1 = Lv.3 ... (비어 있으면 _icon 사용)
    [SerializeField, TextArea] private string _description;
    [SerializeField] private ItemType _itemType;
    [SerializeField] private Vector2Int[] _itemShape; // 몇칸 차지하는지
    [SerializeField] private int _maxLevel;
    [SerializeField] private bool _isSellable = true; // 진화 무기는 false (판매 불가)
    [SerializeField] private AdjacencyEffectType _adjacencyEffectType; // 패시브가 맞닿은 무기에 주는 효과 종류 (수치는 레벨 기준 공통)
    [SerializeField] private Level3EffectType _level3EffectType; // 패시브 Lv.3 이상 Inventory 배치 시 플레이어 효과 종류 (인접 무관)
    [SerializeField] private float _level3EffectValue; // Lv.3 효과 수치 (0.1 = 10%, Lv.3~5 동일)
    [SerializeField] private ExpendableEffectType _expendableEffectType; // 소비 아이템 선택 시 즉시 적용 효과 종류
    [SerializeField] private float _expendableEffectValue; // 소비 효과 수치 (Heal = 회복량, ExpandCell = 확장 칸 수)

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
    public AdjacencyEffectType AdjacencyEffectType => _adjacencyEffectType;
    public Level3EffectType Level3EffectType => _level3EffectType;
    public float Level3EffectValue => _level3EffectValue;
    public ExpendableEffectType ExpendableEffectType => _expendableEffectType;
    public float ExpendableEffectValue => _expendableEffectValue;

    public WeaponDefinition WeaponDefinition => _weaponDefinition;
    public string EquippedEffect => _equippedEffect;
    public string UnequippedBuff => _unequippedBuff;
    public string UnequippedDebuff => _unequippedDebuff;

    /// <summary>레벨별 아이콘, Lv.1 또는 해당 레벨 아이콘이 없으면 기본 아이콘</summary>
    public Sprite GetIcon(int level)
    {
        int index = level - 2;
        if (_levelIcons != null && index >= 0 && index < _levelIcons.Length && _levelIcons[index] != null)
            return _levelIcons[index];

        return _icon;
    }

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

/// <summary>
/// 패시브 인접 효과 종류 (SO 직렬화 정수값 보존을 위해 추가 시 끝에 둘 것)
/// </summary>
public enum AdjacencyEffectType
{
    None,
    Damage, // 공격력 증가
    FireInterval, // 발사 간격 감소
    Spread, // 탄퍼짐 감소
}

/// <summary>
/// 패시브 Lv.3 효과 종류 (SO 직렬화 정수값 보존을 위해 추가 시 끝에 둘 것)
/// </summary>
public enum Level3EffectType
{
    None,
    Experience, // 경험치 획득량 증가
    MoveSpeed, // 이동속도 증가
    CollectRadius, // 경험치 젬 수집 반경 증가
}

/// <summary>
/// 소비 아이템 효과 종류 (SO 직렬화 정수값 보존을 위해 추가 시 끝에 둘 것)
/// </summary>
public enum ExpendableEffectType
{
    None,
    Heal, // 체력 회복
    ExpandCell, // Inventory 칸 확장
}
