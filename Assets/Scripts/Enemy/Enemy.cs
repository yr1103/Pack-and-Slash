using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Enemy 체력·피격·사망 처리와 무기 표적 등록 (사망 시 이동·충돌 정지, 일정 시간 후 풀 반환)
/// </summary>
public class Enemy : MonoBehaviour, ITargetable, IDamageable
{
    // shortcut: 임시 static 목록, EnemyManager 도입 시 이관
    private static readonly List<ITargetable> _activeEnemies = new List<ITargetable>();

    [SerializeField] private Transform _aimPoint; // 자식 AimPoint 빈 오브젝트
    [SerializeField, Min(1f)] private float _maxHealth = 30f;
    [SerializeField, Min(0f)] private float _deathDuration = 1f; // 사망 후 풀 반환까지 시간(초), M03에서 Death 클립 길이에 맞춤

    private static int _nextSpawnId;
    private Health _health;
    private EnemyMovement _movement;
    private Collider _collider;
    private IObjectPool<Enemy> _pool; // Initialize로 주입, 미주입(Scene 직접 배치)이면 사망 후 비활성화만
    private float _deathRemaining;

    /// <summary>활성화된 Enemy 전체 (무기 표적 후보로 공급, 사망 연출 중인 Enemy도 반환 전까지 포함)</summary>
    public static IReadOnlyList<ITargetable> ActiveEnemies => _activeEnemies;

    /// <inheritdoc/>
    public Vector3 AimPoint => _aimPoint.position;

    /// <inheritdoc/>
    public bool IsTargetable => IsAlive;

    /// <inheritdoc/>
    public int SpawnId { get; private set; }

    /// <inheritdoc/>
    public bool IsAlive => !_health.IsDead && isActiveAndEnabled;

    private void Awake()
    {
        _health = new Health(_maxHealth);
        CacheComponents();
    }

    // 풀 재사용 시에도 새 적으로 취급되도록 활성화마다 ID·체력·사망 상태 재설정
    private void OnEnable()
    {
        SpawnId = _nextSpawnId++;
        _health.ResetToMax();
        _deathRemaining = 0f;
        _movement.enabled = true;
        _collider.enabled = true;
        _activeEnemies.Add(this);
    }

    private void Update()
    {
        if (!_health.IsDead)
        {
            return;
        }

        // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
        _deathRemaining -= Time.deltaTime;
        if (_deathRemaining <= 0f)
        {
            Despawn();
        }
    }

    private void OnDisable()
    {
        _activeEnemies.Remove(this);
    }

    /// <summary>체력 감소, 이번 피해로 처음 사망하면 사망 처리 (이미 사망했거나 비활성이면 무시)</summary>
    /// <param name="damage">피해량</param>
    public void TakeDamage(float damage)
    {
        if (!IsAlive)
        {
            return;
        }

        if (_health.ApplyDamage(damage))
        {
            Die();
        }
    }

    // 사망 연출 동안 추적·피격을 멈추고 반환 타이머 시작
    private void Die()
    {
        _movement.enabled = false;
        _collider.enabled = false;
        _deathRemaining = _deathDuration;
    }

    // Release·SetActive(false) 후에는 Update가 돌지 않아 중복 반환 없음
    private void Despawn()
    {
        if (_pool == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _pool.Release(this);
    }

    /// <summary>풀에서 꺼낸 직후 호출해 이동에 필요한 참조와 반환할 풀 주입</summary>
    /// <param name="pool">반환할 풀 (이 Enemy를 꺼낸 프리팹별 풀)</param>
    public void Initialize(FlowFieldManager flowFieldManager, Transform player, IObjectPool<Enemy> pool)
    {
        _pool = pool;
        _movement.Initialize(flowFieldManager, player);
    }

    private void CacheComponents()
    {
        _movement = GetComponent<EnemyMovement>();
        _collider = GetComponent<Collider>();
    }
}
