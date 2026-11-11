using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 탄 프리팹별 투사체 생성·재사용·반환 (소진 시 ObjectPool 기본 동작대로 새로 생성)
/// </summary>
public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Camera _outputCamera; // 화면 밖 판정 기준 Game View 출력 카메라
    [SerializeField] private Transform _projectileParent; // Scene의 Gameplay/Projectiles
    [SerializeField, Min(1)] private int _defaultCapacity = 64; // 프리팹별 풀마다 적용
    [SerializeField, Min(1)] private int _maxSize = 512; // 프리팹별 풀마다 적용

    // 프리팹이 다른 탄끼리 섞여 재사용되지 않도록 프리팹마다 풀 분리
    private readonly Dictionary<Projectile, ObjectPool<Projectile>> _pools = new Dictionary<Projectile, ObjectPool<Projectile>>();

    /// <summary>프리팹의 풀에서 탄을 꺼내 Context로 초기화하고 발사 (해당 프리팹 첫 발사 시 풀 생성)</summary>
    /// <param name="prefab">발사할 탄 프리팹</param>
    /// <param name="context">발사 시점 값</param>
    public void Fire(Projectile prefab, ProjectileSpawnContext context)
    {
        if (!_pools.TryGetValue(prefab, out ObjectPool<Projectile> pool))
        {
            pool = CreatePool(prefab);
            _pools.Add(prefab, pool);
        }

        Projectile projectile = pool.Get();
        projectile.Launch(context, pool, _outputCamera);
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

    private ObjectPool<Projectile> CreatePool(Projectile prefab)
    {
        return new ObjectPool<Projectile>(() => Instantiate(prefab, _projectileParent), ActivateProjectile,
            DeactivateProjectile, DestroyProjectile, collectionCheck: true, defaultCapacity: _defaultCapacity,
            maxSize: _maxSize);
    }
}
