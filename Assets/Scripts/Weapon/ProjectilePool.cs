using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 투사체 생성·재사용·반환 (소진 시 ObjectPool 기본 동작대로 새로 생성)
/// </summary>
public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Projectile _projectilePrefab;
    [SerializeField] private Camera _outputCamera; // 화면 밖 판정 기준 Game View 출력 카메라
    [SerializeField] private Transform _projectileParent; // Scene의 Gameplay/Projectiles
    [SerializeField, Min(1)] private int _defaultCapacity = 64;
    [SerializeField, Min(1)] private int _maxSize = 512;

    private ObjectPool<Projectile> _pool;

    private void Awake()
    {
        InitPool();
    }

    /// <summary>풀에서 탄을 꺼내 Context로 초기화하고 발사</summary>
    /// <param name="context">발사 시점 값</param>
    public void Fire(ProjectileSpawnContext context)
    {
        Projectile projectile = _pool.Get();
        projectile.Launch(context, this, _outputCamera);
    }

    /// <summary>탄을 풀로 반환 (Projectile이 스스로 1회만 호출)</summary>
    /// <param name="projectile">반환할 탄</param>
    public void Release(Projectile projectile)
    {
        _pool.Release(projectile);
    }

    private Projectile CreateProjectile()
    {
        return Instantiate(_projectilePrefab, _projectileParent);
    }

    private static void ActivateProjectile(Projectile projectile)
    {
        projectile.gameObject.SetActive(true);
    }

    private static void DeactivateProjectile(Projectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    // maxSize를 넘겨 반환된 탄은 보관하지 않고 파괴
    private static void DestroyProjectile(Projectile projectile)
    {
        Destroy(projectile.gameObject);
    }

    private void InitPool()
    {
        _pool = new ObjectPool<Projectile>(CreateProjectile, ActivateProjectile, DeactivateProjectile, DestroyProjectile,
            collectionCheck: true, defaultCapacity: _defaultCapacity, maxSize: _maxSize);
    }
}
