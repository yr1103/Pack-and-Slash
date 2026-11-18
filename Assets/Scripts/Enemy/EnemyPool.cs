using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Enemy 프리팹별 생성·재사용·반환 (소진 시 ObjectPool 기본 동작대로 새로 생성)
/// </summary>
public class EnemyPool : MonoBehaviour
{
    [SerializeField] private Transform _enemyParent; // 생성 Enemy를 모을 Hierarchy 부모
    [SerializeField, Min(1)] private int _defaultCapacity = 64; // 프리팹별 풀마다 적용
    [SerializeField, Min(1)] private int _maxSize = 512; // 프리팹별 풀마다 적용

    // 종류가 다른 Enemy끼리 섞여 재사용되지 않도록 프리팹마다 풀 분리
    private readonly Dictionary<Enemy, ObjectPool<Enemy>> _pools = new Dictionary<Enemy, ObjectPool<Enemy>>();

    /// <summary>프리팹의 풀에서 Enemy를 꺼내 위치 지정 후 초기화 (해당 프리팹 첫 생성 시 풀 생성)</summary>
    /// <param name="prefab">생성할 Enemy 프리팹</param>
    /// <param name="position">생성 위치 (월드 좌표)</param>
    /// <returns>활성화·초기화된 Enemy</returns>
    public Enemy Spawn(Enemy prefab, Vector3 position, FlowFieldManager flowFieldManager, Transform player)
    {
        if (!_pools.TryGetValue(prefab, out ObjectPool<Enemy> pool))
        {
            pool = CreatePool(prefab);
            _pools.Add(prefab, pool);
        }

        Enemy enemy = pool.Get();
        // 재사용 Enemy의 이전 위치·회전 제거
        enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
        enemy.Initialize(flowFieldManager, player, pool);
        return enemy;
    }

    private static void ActivateEnemy(Enemy enemy)
    {
        enemy.gameObject.SetActive(true);
    }

    private static void DeactivateEnemy(Enemy enemy)
    {
        enemy.gameObject.SetActive(false);
    }

    // maxSize를 넘겨 반환된 Enemy는 보관하지 않고 파괴
    private static void DestroyEnemy(Enemy enemy)
    {
        Destroy(enemy.gameObject);
    }

    private ObjectPool<Enemy> CreatePool(Enemy prefab)
    {
        return new ObjectPool<Enemy>(() => Instantiate(prefab, _enemyParent), ActivateEnemy,
            DeactivateEnemy, DestroyEnemy, collectionCheck: true, defaultCapacity: _defaultCapacity,
            maxSize: _maxSize);
    }
}
