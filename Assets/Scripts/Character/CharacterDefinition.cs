using UnityEngine;

/// <summary>
/// 캐릭터별 선택 화면 표시 정보, 시작 무기 데이터, 캐릭터 특성·고유 아이템 장착·해제 효과 수치
/// </summary>
[CreateAssetMenu(fileName = "New Character Definition", menuName = "Data/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField, TextArea] private string _traitDescription; // 선택 화면 특성 표시 문구
    [SerializeField] private Sprite _portrait;
    [SerializeField] private string _startWeaponName; // 선택 화면 표시용
    [SerializeField] private WeaponDefinition _startWeapon; // 고유 무기 (주무기 재선정 우선순위 기준)
    [SerializeField] private InventoryData _startWeaponItem; // 시작 시 Inventory에 지급하는 무기 아이템 (_startWeapon과 같은 무기)
    [SerializeField] private InventoryData _uniqueItem; // 시작 시 Inventory에 지급하는 고유 아이템

    // 비율은 0.1 = 10%, 무기 효과는 _uniqueWeaponType 무기에만 적용
    [SerializeField] private WeaponType _uniqueWeaponType; // 특성·고유 아이템 효과 공통 대상 무기 종류

    // 캐릭터 특성 (고유 아이템 탈착과 무관하게 항상 적용)
    [SerializeField] private float _traitDamageBonus; // 공격력 보너스
    [SerializeField] private float _traitAccuracyBonus; // 정확도 보너스 (탄퍼짐 감소율)
    [SerializeField, Min(0)] private int _traitProjectileBonus; // 발사당 총알 추가 수
    [SerializeField] private float _traitMoveSpeedBonus; // 이동속도 보너스 (전역, 음수 = 감소)

    // 고유 아이템 장착 효과 (장착 중에만 적용)
    [SerializeField] private float _equippedAccuracyBonus; // 정확도 보너스 (탄퍼짐 감소율)
    [SerializeField] private bool _equippedUnlocksVestCells; // 조끼 전용 칸 해제
    [SerializeField, Min(0)] private int _equippedStorageBonus; // 보관함 한도 추가 수

    // 고유 아이템 해제 효과 (해제 중에만 적용)
    [SerializeField] private float _unequippedAttackSpeedBonus; // 공격속도 보너스
    [SerializeField, Min(0f)] private float _unequippedSpreadAngle; // 전체 확산각(도, 랜덤 탄퍼짐), 0이면 미적용
    [SerializeField] private float _unequippedMoveSpeedBonus; // 이동속도 보너스
    [SerializeField] private float _unequippedMaxHealthBonus; // 최대 체력 보너스 (음수 = 감소)

    public string DisplayName => _displayName;
    public string Description => _description;
    public string TraitDescription => _traitDescription;
    public Sprite Portrait => _portrait;
    public string StartWeaponName => _startWeaponName;
    public WeaponDefinition StartWeapon => _startWeapon;
    public InventoryData StartWeaponItem => _startWeaponItem;
    public InventoryData UniqueItem => _uniqueItem;
    public WeaponType UniqueWeaponType => _uniqueWeaponType;
    public float TraitDamageBonus => _traitDamageBonus;
    public float TraitAccuracyBonus => _traitAccuracyBonus;
    public int TraitProjectileBonus => _traitProjectileBonus;
    public float TraitMoveSpeedBonus => _traitMoveSpeedBonus;
    public float EquippedAccuracyBonus => _equippedAccuracyBonus;
    public bool EquippedUnlocksVestCells => _equippedUnlocksVestCells;
    public int EquippedStorageBonus => _equippedStorageBonus;
    public float UnequippedAttackSpeedBonus => _unequippedAttackSpeedBonus;
    public float UnequippedSpreadAngle => _unequippedSpreadAngle;
    public float UnequippedMoveSpeedBonus => _unequippedMoveSpeedBonus;
    public float UnequippedMaxHealthBonus => _unequippedMaxHealthBonus;
}
