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

    /// <summary>무기 종류</summary>
    public WeaponType WeaponType => _weaponType;

    /// <summary>기본 발사 간격(초)</summary>
    public float FireInterval => _fireInterval;

    /// <summary>탄창 용량 (0이면 탄창 없는 무기)</summary>
    public int MagazineCapacity => _magazineCapacity;

    /// <summary>재장전 소요 시간(초)</summary>
    public float ReloadDuration => _reloadDuration;

    /// <summary>적 탐색 반경 (플레이어 루트 기준 XZ 평면)</summary>
    public float DetectionRange => _detectionRange;
}

/// <summary>
/// 무기 종류
/// </summary>
public enum WeaponType
{
    Pistol,
    Shotgun,
    Minigun,
}
