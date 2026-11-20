using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Enemy 사망 시 그 위치에 경험치 젬 생성·재사용·반환 (젬 프리팹별 풀, 소진 시 ObjectPool 기본 동작대로 새로 생성)
/// </summary>
public class ExperienceGemPool : MonoBehaviour
{
    [SerializeField] private ExperienceGem _gemPrefab; // Enemy에 젬 프리팹이 지정되지 않았을 때 쓰는 기본 젬
    [SerializeField] private Transform _gemParent; // 생성 젬을 모을 Hierarchy 부모
    [SerializeField] private PlayerExperience _playerExperience; // 젬 수집 시 경험치 지급 대상
    [SerializeField, Min(1)] private int _defaultCapacity = 64; // 젬 프리팹별 풀마다 적용
    [SerializeField, Min(1)] private int _maxSize = 512; // 젬 프리팹별 풀마다 적용
    [SerializeField] private float _spawnHeight = 0.3f; // 젬 생성 높이(월드 Y)

    // 외형이 다른 젬끼리 섞여 재사용되지 않도록 프리팹마다 풀 분리
    private readonly Dictionary<ExperienceGem, ObjectPool<ExperienceGem>> _pools =
        new Dictionary<ExperienceGem, ObjectPool<ExperienceGem>>();

    private void OnEnable()
    {
        Enemy.Died += SpawnGem;
    }

    private void OnDisable()
    {
        Enemy.Died -= SpawnGem;
    }

    // 해당 젬 프리팹 첫 생성 시 풀 생성
    private void SpawnGem(Enemy enemy)
    {
        if (enemy.ExperienceValue <= 0)
        {
            return;
        }

        ExperienceGem prefab = enemy.GemPrefab != null ? enemy.GemPrefab : _gemPrefab;
        if (!_pools.TryGetValue(prefab, out ObjectPool<ExperienceGem> pool))
        {
            pool = CreatePool(prefab);
            _pools.Add(prefab, pool);
        }

        Vector3 position = enemy.transform.position;
        position.y = _spawnHeight;

        ExperienceGem gem = pool.Get();
        // 재사용 젬의 이전 위치·회전 제거
        gem.transform.SetPositionAndRotation(position, Quaternion.identity);
        gem.Initialize(_playerExperience, enemy.ExperienceValue, pool);
    }

    private static void ActivateGem(ExperienceGem gem)
    {
        gem.gameObject.SetActive(true);
    }

    private static void DeactivateGem(ExperienceGem gem)
    {
        gem.gameObject.SetActive(false);
    }

    // maxSize를 넘겨 반환된 젬은 보관하지 않고 파괴
    private static void DestroyGem(ExperienceGem gem)
    {
        Destroy(gem.gameObject);
    }

    private ObjectPool<ExperienceGem> CreatePool(ExperienceGem prefab)
    {
        return new ObjectPool<ExperienceGem>(() => Instantiate(prefab, _gemParent), ActivateGem, DeactivateGem,
            DestroyGem, collectionCheck: true, defaultCapacity: _defaultCapacity, maxSize: _maxSize);
    }
}
