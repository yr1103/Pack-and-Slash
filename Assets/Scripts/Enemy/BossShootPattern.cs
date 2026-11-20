using System;
using UnityEngine;

/// <summary>
/// 보스 사격 패턴 : 범위 안 플레이어 감지 시 예고 후 발사 순간의 플레이어 방향으로 탄 발사 (Naga 직선탄·Bishop 방사탄·BlackKnight 직선·부채꼴탄 공용)
/// </summary>
public class BossShootPattern : MonoBehaviour, IBossPattern
{
    private const float FULL_CIRCLE_ANGLE = 360f;
    private const float HALF = 0.5f;

    [SerializeField] private Projectile _projectilePrefab; // hitMask Player·blockMask Obstacle인 보스 탄
    [SerializeField] private bool _stopWhileWarning = true; // true면 예고~발사 동안 추적 정지, false면 이동하며 발사
    [SerializeField, Min(0f)] private float _detectRange = 12f; // 예고 시작 XZ 거리, 수치미정
    [SerializeField, Min(0f)] private float _warnDuration = 0.6f; // 수치미정
    [SerializeField, Min(0f)] private float _cooldown = 2.5f; // 발사 후 재예고까지(초), 수치미정
    [SerializeField, Min(1)] private int _projectileCount = 1; // 수치미정
    [SerializeField, Range(0f, 360f)] private float _spreadAngle = 0f; // 360이면 균등 분할, 수치미정
    [SerializeField, Min(0f)] private float _projectileSpeed = 6f; // 수치미정
    [SerializeField, Min(0f)] private float _projectileDamage = 15f; // 수치미정
    [SerializeField, Min(0f)] private float _projectileMaxDistance = 30f; // 수치미정
    [SerializeField, Min(0f)] private float _projectileMaxLifetime = 10f; // 수치미정
    [SerializeField] private float _fireHeight = 1f; // 탄 생성 높이 (보스 위치 기준)

    private Enemy _enemy;
    private EnemyMovement _movement;
    private BossCharge _charge; // Warn = 예고, Charge = 발사 (진입 즉시 발사 후 EndCharge)
    private ProjectilePool _projectilePool;

    /// <summary>발사(정지 보스는 추적 복귀까지) 직후 발생</summary>
    public event Action Finished;

    private void Awake()
    {
        _charge = new BossCharge(_detectRange, _warnDuration, _cooldown);
        CacheComponents();
    }

    // 풀 재사용 시 추적 상태로 초기화 (EnemyMovement 재활성화는 Enemy.OnEnable 담당)
    private void OnEnable()
    {
        _charge.Reset();
    }

    private void Update()
    {
        // 사망 시 Enemy.Die가 끈 EnemyMovement를 다시 켜지 않도록 발사 처리 없이 중지
        if (!_enemy.IsAlive)
        {
            return;
        }

        Transform player = _movement.Player;
        if (_projectilePool == null || player == null)
        {
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        BossCharge.ChargeState previousState = _charge.State;
        _charge.Tick(Time.deltaTime, toPlayer.magnitude);

        if (previousState == BossCharge.ChargeState.Chase && _charge.State == BossCharge.ChargeState.Warn)
        {
            StartWarning(toPlayer);
        }
        else if (_charge.State == BossCharge.ChargeState.Charge)
        {
            Fire(toPlayer);
        }
    }

    /// <summary>중심 방향을 Y축 회전한 index번째 탄 방향 (XZ 정규화)</summary>
    /// <param name="centerDirection">중심 방향 (Y 성분 무시)</param>
    /// <param name="index">탄 번호 (0 ~ count-1)</param>
    /// <param name="count">탄 수, 1이면 중심 방향</param>
    /// <param name="spreadAngle">퍼짐 각도, 360이면 360/count 간격 균등 분할(index 0 = 중심), 그 외는 -spread/2 ~ +spread/2 양 끝 포함 균등</param>
    /// <returns>XZ 평면 정규화 방향</returns>
    public static Vector3 GetSpreadDirection(Vector3 centerDirection, int index, int count, float spreadAngle)
    {
        Vector3 flatCenter = new Vector3(centerDirection.x, 0f, centerDirection.z).normalized;
        if (count <= 1)
        {
            return flatCenter;
        }

        // 360이면 양 끝(0도·360도)이 겹치므로 count 등분
        float angle = spreadAngle >= FULL_CIRCLE_ANGLE
            ? FULL_CIRCLE_ANGLE / count * index
            : spreadAngle / (count - 1) * index - spreadAngle * HALF;
        return Quaternion.AngleAxis(angle, Vector3.up) * flatCenter;
    }

    // 정지 보스만 추적 정지·플레이어 방향 회전 (이동 보스는 회전을 EnemyMovement가 담당)
    private void StartWarning(Vector3 toPlayer)
    {
        if (!_stopWhileWarning)
        {
            return;
        }

        _movement.enabled = false;
        if (toPlayer != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(toPlayer);
        }
    }

    // 발사 순간의 플레이어 방향 기준 탄 발사 후 쿨다운 시작, 정지 보스는 추적 복귀
    private void Fire(Vector3 toPlayer)
    {
        // 플레이어와 같은 위치면 방향이 없으므로 현재 정면 사용
        Vector3 fireDirection = toPlayer == Vector3.zero ? transform.forward : toPlayer.normalized;
        Vector3 spawnPosition = transform.position + Vector3.up * _fireHeight;
        for (int i = 0; i < _projectileCount; i++)
        {
            Vector3 direction = GetSpreadDirection(fireDirection, i, _projectileCount, _spreadAngle);
            _projectilePool.Fire(_projectilePrefab, new ProjectileSpawnContext(spawnPosition, direction,
                _projectileSpeed, _projectileDamage, _projectileMaxDistance, _projectileMaxLifetime,
                requireCameraVisibility: false, maxUniqueTargetsHit: 1)); // 탄 1발은 대상 1명만 적중
        }

        _charge.EndCharge();
        if (_stopWhileWarning)
        {
            _movement.enabled = true;
        }

        Finished?.Invoke();
    }

    /// <summary>탄 풀 주입 (EnemyPool.Spawn에서 호출), 미주입이면 패턴 동작 안 함</summary>
    /// <param name="projectilePool">보스 탄을 꺼낼 풀</param>
    public void Initialize(ProjectilePool projectilePool)
    {
        _projectilePool = projectilePool;
    }

    private void CacheComponents()
    {
        _enemy = GetComponent<Enemy>();
        _movement = GetComponent<EnemyMovement>();
    }
}
