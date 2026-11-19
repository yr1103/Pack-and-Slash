using System;
using UnityEngine;

/// <summary>
/// 플레이어 체력·무적 게이트·Enemy 접촉 피해 처리 (사망 시 지정 컴포넌트 정지)
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    private const int CONTACT_BUFFER_SIZE = 4; // 첫 유효 Enemy 1마리만 필요
    private const string DEATH_LOG = "플레이어 사망";

    private static readonly Collider[] _contactColliders = new Collider[CONTACT_BUFFER_SIZE];

    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [SerializeField, Min(0f)] private float _invulnerabilityDuration = 0.5f; // 피해 후 무적 시간(초)
    [SerializeField, Min(0f)] private float _contactRadius = 0.6f; // Enemy 정지 거리 1.0 - 캡슐 반지름 0.5 = 표면 0.5 + 여유
    [SerializeField] private LayerMask _enemyLayer; // 접촉 검사 대상
    [SerializeField] private Behaviour[] _disableOnDeath; // 사망 시 정지할 이동·조준·사격 컴포넌트

    private Health _health;
    private DamageGate _damageGate;

    public float MaxHealth => _maxHealth;

    /// <summary>현재 체력 (체력바 표시용)</summary>
    public float CurrentHealth => _health.CurrentHealth;

    /// <summary>사망 상태 여부 (Animator 표시용)</summary>
    public bool IsDead => _health.IsDead;

    /// <inheritdoc/>
    public bool IsAlive => !_health.IsDead;

    /// <summary>무적 게이트를 통과해 피해가 적용될 때마다 발생 (사망하는 피해 포함)</summary>
    public event Action Damaged;

    /// <summary>사망 확정 시 1회 발생 (게임 오버 판정 구독 지점)</summary>
    public event Action Died;

    private void Awake()
    {
        _health = new Health(_maxHealth);
        _damageGate = new DamageGate(_invulnerabilityDuration);
    }

    private void Update()
    {
        _damageGate.Tick(Time.deltaTime);

        // 일시정지 중 사망하면 GameSession 종료 지연이 진행되지 않아 결과 판넬 없이 멈춤
        if (Time.timeScale == 0f)
        {
            return;
        }

        // 무적 중에는 접촉해도 피해가 없으므로 물리 검사 생략
        if (!IsAlive || !_damageGate.IsOpen)
        {
            return;
        }

        ApplyEnemyContactDamage();
    }

    // CharacterController 충돌과 무관하게 거리로 접촉 판정, 동시 접촉 시 처음 찾은 1마리만 적용
    private void ApplyEnemyContactDamage()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _contactRadius, _contactColliders, _enemyLayer);
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = _contactColliders[i].GetComponentInParent<Enemy>();
            if (enemy != null && enemy.IsAlive)
            {
                TakeDamage(enemy.ContactDamage);
                return;
            }
        }
    }

    /// <summary>무적 게이트 통과 시에만 체력 감소, 이번 피해로 처음 사망하면 사망 처리 (사망 상태면 무시)</summary>
    /// <param name="damage">피해량</param>
    public void TakeDamage(float damage)
    {
        if (!IsAlive || !_damageGate.TryPass())
        {
            return;
        }

        bool died = _health.ApplyDamage(damage);
        Damaged?.Invoke();
        if (died)
        {
            Die();
        }
    }

    private void Die()
    {
        for (int i = 0; i < _disableOnDeath.Length; i++)
        {
            _disableOnDeath[i].enabled = false;
        }

        Debug.Log(DEATH_LOG);
        Died?.Invoke();
    }
}
