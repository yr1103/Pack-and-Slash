using UnityEngine;

/// <summary>
/// 무기 종류별 공유 설정 데이터 (런타임 상태는 WeaponInstance가 보관)
/// </summary>
[CreateAssetMenu(fileName = "New Weapon Definition", menuName = "Data/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [SerializeField] private WeaponType _weaponType;
    [SerializeField] private float _fireInterval;
    [SerializeField] private int _magazineCapacity; // 0 = 탄창 없음 (권총·샷건)
    [SerializeField] private float _reloadDuration;
    [SerializeField, Min(0f)] private float _detectionRange;
    [SerializeField, Min(0f)] private float _projectileSpeed;
    [SerializeField, Min(0f)] private float _projectileMaxDistance; // 탄 비행 한계, 탐지 거리(DetectionRange)와 별개
    [SerializeField, Min(0f)] private float _projectileMaxLifetime;
    [SerializeField, Min(0f)] private float _projectileDamage;
    [SerializeField] private bool _disableOutsideGameCamera; // 출력 카메라 화면 밖이면 탄 반환·피해 무효 (권총·미니건 true)
    
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
}

/// <summary>무기 종류</summary>
public enum WeaponType
{
    Pistol,
    Shotgun,
    Minigun,
}
