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
    [SerializeField] private bool _disableOutsideGameCamera; // 출력 카메라 화면 밖이면 탄 반환·피해 무효 (권총·미니건 true)
    [SerializeField, Min(1)] private int _projectilesPerShot = 1;
    [SerializeField, Min(0f)] private float _spreadAngle; // 부채꼴 전체 각도(도), 양 끝 탄 포함
    [SerializeField] private bool _randomSpread; // true = 탄마다 ±SpreadAngle/2 랜덤 각도, false = 균등 간격 부채꼴
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
    public bool DisableOutsideGameCamera => _disableOutsideGameCamera;
    public int ProjectilesPerShot => _projectilesPerShot;
    public float SpreadAngle => _spreadAngle;
    public bool RandomSpread => _randomSpread;
    public Projectile ProjectilePrefab => _projectilePrefab;
    public FloatingWeaponView FloatingViewPrefab => _floatingViewPrefab;
}

/// <summary>무기 종류</summary>
public enum WeaponType
{
    Pistol,
    Shotgun,
    Minigun,
}
