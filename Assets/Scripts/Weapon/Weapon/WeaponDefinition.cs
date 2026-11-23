using UnityEngine;

/// <summary>
/// 무기 종류별 공유 설정 데이터 (런타임 상태는 WeaponInstance가 보관)
/// </summary>
[CreateAssetMenu(fileName = "New Weapon Definition", menuName = "Data/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    private const float MIN_FIRE_INTERVAL = 0.01f;
    private const float DEFAULT_FIRE_INTERVAL = 1f;

    [SerializeField] private WeaponType _weaponType;
    [SerializeField, Min(MIN_FIRE_INTERVAL)] private float _fireInterval = DEFAULT_FIRE_INTERVAL; // 0 이하면 한 프레임 연속 발사 루프가 끝나지 않아 하한 지정
    [SerializeField] private int _magazineCapacity; // 0 = 탄창 없음 (권총·샷건)
    [SerializeField] private float _reloadDuration;
    [SerializeField, Min(0f)] private float _detectionRange;
    [SerializeField, Min(0f)] private float _projectileSpeed;
    [SerializeField, Min(0f)] private float _projectileMaxDistance; // 탄 비행 한계, 탐지 거리(DetectionRange)와 별개
    [SerializeField, Min(0f)] private float _projectileMaxLifetime;
    [SerializeField, Min(0f)] private float _projectileDamage;
    [SerializeField, Range(0f, 1f)] private float _damageVariance; // 최종 공격력 기준 탄별 피해 ±비율, 0 = 고정
    [SerializeField] private bool _disableOutsideGameCamera; // 출력 카메라 화면 밖이면 탄 반환·피해 무효 (권총·미니건 true)
    [SerializeField, Min(1)] private int _pierceCount = 1; // 탄 하나가 피해를 주는 서로 다른 적 최대 수, 1 = 관통 없음
    [SerializeField, Min(1)] private int _projectilesPerShot = 1;
    [SerializeField, Min(0f)] private float _spreadAngle; // 부채꼴 전체 각도(도), 양 끝 탄 포함
    [SerializeField] private bool _randomSpread; // true = 탄마다 ±SpreadAngle/2 랜덤 각도, false = 균등 간격 부채꼴
    [SerializeField, Range(0f, 1f)] private float _level3SpreadReduction; // Lv.3 이상 탄퍼짐 감소율, 0 = 효과 없음
    [SerializeField, Min(1)] private int _level3GunCount = 1; // Lv.3 이상 이 인스턴스의 총기 자루 수, 1 = 효과 없음
    [SerializeField, Min(0f)] private float _level3KnockbackStrength; // Lv.3 이상 적중 시 넉백 속도(초당 거리), 0 = 효과 없음
    [SerializeField] private Projectile _projectilePrefab; // 무기별 탄 외형, 프리팹마다 풀 분리
    [SerializeField] private FloatingWeaponView _floatingViewPrefab; // 주무기가 아닐 때 플레이어 주변에 띄우는 외형

    public WeaponType WeaponType => _weaponType;
    public float FireInterval => _fireInterval;
    public int MagazineCapacity => _magazineCapacity;
    public float ReloadDuration => _reloadDuration;
    public float DetectionRange => _detectionRange;
    public float ProjectileSpeed => _projectileSpeed;
    public float ProjectileMaxDistance => _projectileMaxDistance;
    public float ProjectileMaxLifetime => _projectileMaxLifetime;
    public float ProjectileDamage => _projectileDamage;
    public float DamageVariance => _damageVariance;
    public bool DisableOutsideGameCamera => _disableOutsideGameCamera;
    public int PierceCount => _pierceCount;
    public int ProjectilesPerShot => _projectilesPerShot;
    public float SpreadAngle => _spreadAngle;
    public bool RandomSpread => _randomSpread;
    public float Level3SpreadReduction => _level3SpreadReduction;
    public int Level3GunCount => _level3GunCount;
    public float Level3KnockbackStrength => _level3KnockbackStrength;
    public Projectile ProjectilePrefab => _projectilePrefab;
    public FloatingWeaponView FloatingViewPrefab => _floatingViewPrefab;

    /// <summary>
    /// 탄 1발의 피해량 (damage × (1 - variance) ~ damage × (1 + variance) 구간)
    /// </summary>
    /// <param name="randomRatio">0 = 최소, 1 = 최대 (발사 시 Random.value, 툴팁 범위 표시 시 0·1)</param>
    public static float GetRolledDamage(float damage, float variance, float randomRatio)
    {
        return damage * Mathf.Lerp(1f - variance, 1f + variance, randomRatio);
    }
}

/// <summary>무기 종류</summary>
public enum WeaponType
{
    Pistol,
    Shotgun,
    Minigun,
}
