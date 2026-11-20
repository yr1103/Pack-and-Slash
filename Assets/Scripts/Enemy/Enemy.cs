using System;
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
    private static readonly int _dieHash = Animator.StringToHash("Die");

    [SerializeField] private Transform _aimPoint; // 자식 AimPoint 빈 오브젝트
    [SerializeField] private Animator _animator; // 외형 자식의 Animator, 외형 없는 Enemy(임시 Cube)는 비워 둠
    [SerializeField, Min(1f)] private float _maxHealth = 30f;
    [SerializeField, Min(0f)] private float _deathDuration = 1f; // 사망 연출 후 풀 반환까지 시간(초)
    [SerializeField, Min(0f)] private float _contactDamage = 10f; // 플레이어 접촉 시 1회 피해량
    [SerializeField, Min(0)] private int _experienceValue = 1; // 사망 시 드롭할 젬의 경험치, 0이면 드롭 없음
    [SerializeField] private bool _dropsTreasureChest; // 사망 시 보물 상자 드롭 여부 (중간 보스만)

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

    public float ContactDamage => _contactDamage;
    public int ExperienceValue => _experienceValue;
    public bool DropsTreasureChest => _dropsTreasureChest;

    /// <summary>Enemy 사망 확정 시 1회 발생 (젬 드롭 등 구독 지점, 경험치는 직접 지급하지 않음)</summary>
    public static event Action<Enemy> Died;

    /// <summary>피해 적용 시마다 발생, 인자는 이번 피해량 (사망하는 피해 포함, 피격 연출·적중 사운드 구독 지점)</summary>
    public event Action<float> Damaged;

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

        bool isDeadByDamage = _health.ApplyDamage(damage);
        Damaged?.Invoke(damage);

        if (isDeadByDamage)
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

        // 재활성화 시 Animator가 기본 상태(Move)로 돌아가므로 OnEnable에서 되돌리지 않음
        if (_animator != null)
        {
            _animator.SetTrigger(_dieHash);
        }

        Died?.Invoke(this);
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
