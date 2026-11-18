using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Enemy 사망 시 그 위치에 경험치 젬 생성·재사용·반환 (소진 시 ObjectPool 기본 동작대로 새로 생성)
/// </summary>
public class ExperienceGemPool : MonoBehaviour
{
    [SerializeField] private ExperienceGem _gemPrefab;
    [SerializeField] private Transform _gemParent; // 생성 젬을 모을 Hierarchy 부모
    [SerializeField] private PlayerExperience _playerExperience; // 젬 수집 시 경험치 지급 대상
    [SerializeField, Min(1)] private int _defaultCapacity = 64;
    [SerializeField, Min(1)] private int _maxSize = 512;
    [SerializeField] private float _spawnHeight = 0.3f; // 젬 생성 높이(월드 Y)

    private ObjectPool<ExperienceGem> _pool;

    private void Awake()
    {
        CreatePool();
    }

    private void OnEnable()
    {
        Enemy.Died += SpawnGem;
    }

    private void OnDisable()
    {
        Enemy.Died -= SpawnGem;
    }

    private void SpawnGem(Enemy enemy)
    {
        if (enemy.ExperienceValue <= 0)
        {
            return;
        }

        Vector3 position = enemy.transform.position;
        position.y = _spawnHeight;

        ExperienceGem gem = _pool.Get();
        // 재사용 젬의 이전 위치·회전 제거
        gem.transform.SetPositionAndRotation(position, Quaternion.identity);
        gem.Initialize(_playerExperience, enemy.ExperienceValue, _pool);
    }

    private ExperienceGem CreateGem()
    {
        return Instantiate(_gemPrefab, _gemParent);
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

    private void CreatePool()
    {
        _pool = new ObjectPool<ExperienceGem>(CreateGem, ActivateGem, DeactivateGem, DestroyGem,
            collectionCheck: true, defaultCapacity: _defaultCapacity, maxSize: _maxSize);
    }
}
