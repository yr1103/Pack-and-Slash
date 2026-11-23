using UnityEngine;

/// <summary>
/// 캐릭터별 선택 화면 표시 정보, 시작 무기 데이터, 고유 아이템 장착·해제 효과 수치
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

    // 고유 효과 (비율은 0.1 = 10%, 무기 효과는 _uniqueWeaponType 무기에만 적용)
    [SerializeField] private WeaponType _uniqueWeaponType; // 고유 효과 대상 무기 종류
    [SerializeField] private float _equippedDamageBonus; // 고유 아이템 장착 중 공격력 보너스
    [SerializeField] private float _unequippedAttackSpeedBonus; // 해제 시 공격속도 보너스
    [SerializeField, Min(0f)] private float _unequippedSpreadAngle; // 해제 시 전체 확산각(도, 랜덤 탄퍼짐), 0이면 미적용
    [SerializeField] private float _unequippedMoveSpeedBonus; // 해제 시 이동속도 보너스
    [SerializeField] private float _unequippedMaxHealthBonus; // 해제 시 최대 체력 보너스 (음수 = 감소)
    [SerializeField] private float _unequippedAccuracyBonus; // 해제 시 정확도 보너스 (탄퍼짐 감소율)
    [SerializeField] private float _baseMoveSpeedBonus; // 기본 특성 이동속도 보너스 (항상 적용)
    [SerializeField] private float _baseAccuracyBonus; // 기본 특성 정확도 보너스 (항상 적용)

    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Portrait => _portrait;
    public string StartWeaponName => _startWeaponName;
    public WeaponDefinition StartWeapon => _startWeapon;
    public InventoryData StartWeaponItem => _startWeaponItem;
    public InventoryData UniqueItem => _uniqueItem;
    public WeaponType UniqueWeaponType => _uniqueWeaponType;
    public float EquippedDamageBonus => _equippedDamageBonus;
    public float UnequippedAttackSpeedBonus => _unequippedAttackSpeedBonus;
    public float UnequippedSpreadAngle => _unequippedSpreadAngle;
    public float UnequippedMoveSpeedBonus => _unequippedMoveSpeedBonus;
    public float UnequippedMaxHealthBonus => _unequippedMaxHealthBonus;
    public float UnequippedAccuracyBonus => _unequippedAccuracyBonus;
    public float BaseMoveSpeedBonus => _baseMoveSpeedBonus;
    public float BaseAccuracyBonus => _baseAccuracyBonus;
}
