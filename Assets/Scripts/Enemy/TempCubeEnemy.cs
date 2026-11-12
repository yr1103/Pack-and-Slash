using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 검증용 임시 큐브 적 (표적 후보 등록·피격 표시, 사망 시 표적 제외만 처리)
/// </summary>
public class TempCubeEnemy : MonoBehaviour, ITargetable, IDamageable
{
    private const string HIT_LOG_FORMAT = "{0} 피격, 남은 체력 : {1}";

    private static readonly Color _hitColor = Color.red;
    private static readonly Color _deathColor = Color.yellow;
    // shortcut: 임시 static 목록, EnemyManager 도입 시 이관
    private static readonly List<ITargetable> _activeEnemies = new List<ITargetable>();

    [SerializeField] private Transform _aimPoint; // 자식 AimPoint 빈 오브젝트
    [SerializeField, Min(1f)] private float _maxHealth = 30f;
    [SerializeField, Min(0f)] private float _hitFlashDuration = 1f; // 피격 색 유지 시간(초)

    private static int _nextSpawnId;
    private Renderer _renderer;
    private Material _materialInstance;
    private Color _originalColor;
    private float _currentHealth;
    private float _hitFlashRemaining;

    /// <summary>활성화된 임시 적 전체 (무기 표적 후보로 공급)</summary>
    public static IReadOnlyList<ITargetable> ActiveEnemies => _activeEnemies;

    /// <inheritdoc/>
    public Vector3 AimPoint => _aimPoint.position;

    /// <inheritdoc/>
    public bool IsTargetable => IsAlive;

    /// <inheritdoc/>
    public int SpawnId { get; private set; }

    /// <inheritdoc/>
    public bool IsAlive => _currentHealth > 0f && isActiveAndEnabled;

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
        _hitFlashRemaining = 0f;
        _activeEnemies.Add(this);
    }

    private void Update()
    {
        if (_hitFlashRemaining <= 0f)
        {
            return;
        }

        // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
        _hitFlashRemaining -= Time.deltaTime;
        if (_hitFlashRemaining <= 0f)
        {
            _materialInstance.color = _originalColor;
        }
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

    /// <summary>체력 감소와 피격·사망 색 표시 (이미 사망했으면 무시, 파괴·반환은 하지 않음)</summary>
    /// <param name="damage">피해량</param>
    public void TakeDamage(float damage)
    {
        if (!IsTargetable)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        Debug.LogFormat(HIT_LOG_FORMAT, name, _currentHealth);

        // 사망 색이 피격 복귀로 덮이지 않도록 남은 표시 시간 제거
        if (!IsAlive)
        {
            _hitFlashRemaining = 0f;
            _materialInstance.color = _deathColor;
            return;
        }

        _hitFlashRemaining = _hitFlashDuration;
        _materialInstance.color = _hitColor;
    }

    private void CacheComponents()
    {
        _renderer = GetComponent<Renderer>();
        // 공유 머티리얼 오염 방지용 개별 인스턴스
        _materialInstance = _renderer.material;
        _originalColor = _materialInstance.color;
    }
}
