using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 검증용 임시 큐브 적 (표적 후보 등록·피격 표시, 사망 시 표적 제외만 처리)
/// </summary>
public class TempCubeEnemy : MonoBehaviour, ITargetable, IDamageable
{
    private const string HIT_LOG_FORMAT = "{0} 피격, 남은 체력 : {1}";

    private static readonly Color _hitColor = Color.red;
    // shortcut: 임시 static 목록, EnemyManager 도입 시 이관
    private static readonly List<ITargetable> _activeEnemies = new List<ITargetable>();

    [SerializeField] private Transform _aimPoint; // 자식 AimPoint 빈 오브젝트
    [SerializeField, Min(1f)] private float _maxHealth = 30f;

    private static int _nextSpawnId;
    private Renderer _renderer;
    private Material _materialInstance;
    private Color _originalColor;
    private float _currentHealth;

    /// <summary>활성화된 임시 적 전체 (무기 표적 후보로 공급)</summary>
    public static IReadOnlyList<ITargetable> ActiveEnemies => _activeEnemies;

    /// <inheritdoc/>
    public Vector3 AimPoint => _aimPoint.position;

    /// <inheritdoc/>
    public bool IsTargetable => _currentHealth > 0f && isActiveAndEnabled;

    /// <inheritdoc/>
    public int SpawnId { get; private set; }

    private void Awake()
    {
        CacheComponents();
    }

    // 풀 재사용 시에도 새 적으로 취급되도록 활성화마다 ID·체력·색 재설정
    private void OnEnable()
    {
        SpawnId = _nextSpawnId++;
        _currentHealth = _maxHealth;
        _materialInstance.color = _originalColor;
        _activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        _activeEnemies.Remove(this);
    }

    // renderer.material로 만든 인스턴스는 자동 해제되지 않음
    private void OnDestroy()
    {
        Destroy(_materialInstance);
    }

    /// <summary>체력 감소와 피격 색 표시 (이미 사망했으면 무시, 파괴·반환은 하지 않음)</summary>
    /// <param name="damage">피해량</param>
    public void TakeDamage(float damage)
    {
        if (!IsTargetable)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        _materialInstance.color = _hitColor;
        Debug.LogFormat(HIT_LOG_FORMAT, name, _currentHealth);
    }

    private void CacheComponents()
    {
        _renderer = GetComponent<Renderer>();
        // 공유 머티리얼 오염 방지용 개별 인스턴스
        _materialInstance = _renderer.material;
        _originalColor = _materialInstance.color;
    }
}
