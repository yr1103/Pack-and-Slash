using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 고정 방향 직진 투사체 (이동, 적중 피해 적용, 장애물·화면·거리·수명 경계 반환)
/// </summary>
public class Projectile : MonoBehaviour
{
    private const string ZERO_DIRECTION_WARNING_FORMAT = "투사체 방향 {0}의 XZ 성분이 0이라 발사 취소";

    [SerializeField, Min(0f)] private float _hitRadius = 0.1f;
    [SerializeField] private LayerMask _hitMask; // 플레이어 탄은 Enemy 레이어만 검사해 자기 피격 방지
    [SerializeField] private LayerMask _blockMask; // 닿으면 탄이 막혀 반환되는 장애물 레이어 (_hitMask와 겹치면 장애물로 처리)

    private ProjectileHitDetector _hitDetector;
    private ProjectileSpawnContext _context;
    private IObjectPool<Projectile> _pool;
    private Camera _outputCamera;
    private Vector3 _direction;
    private float _traveledDistance;
    private float _elapsedTime;
    private bool _isActive;

    private void Awake()
    {
        _hitDetector = new ProjectileHitDetector(_hitRadius, _hitMask, _blockMask);
    }

    private void Update()
    {
        if (!_isActive)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        float stepDistance = _context.Speed * deltaTime;
        Vector3 startPosition = transform.position;

        // MaxDistance 너머의 적은 맞지 않도록 남은 거리까지만 검사
        CastAndHit(startPosition, Mathf.Min(stepDistance, _context.MaxDistance - _traveledDistance));
        // 이번 구간 적중으로 이미 반환됐으면 이동 생략
        if (!_isActive)
        {
            return;
        }

        transform.position = startPosition + _direction * stepDistance;
        _traveledDistance += stepDistance;
        _elapsedTime += deltaTime;

        if (IsOutOfBounds())
        {
            Despawn();
        }
    }

    /// <summary>풀에서 꺼낸 직후 호출, 이전 발사의 상태를 모두 초기화하고 발사 (방향을 정할 수 없으면 경고 후 즉시 반환)</summary>
    /// <param name="context">발사 시점 값 (Direction은 비정규 입력 허용, 여기서 XZ 정규화)</param>
    /// <param name="pool">반환할 풀 (이 탄을 꺼낸 프리팹별 풀)</param>
    /// <param name="outputCamera">화면 판정 기준 출력 카메라</param>
    public void Launch(ProjectileSpawnContext context, IObjectPool<Projectile> pool, Camera outputCamera)
    {
        _context = context;
        _pool = pool;
        _outputCamera = outputCamera;
        _traveledDistance = 0f;
        _elapsedTime = 0f;
        _isActive = true;

        if (!TryGetPlanarDirection(context.Direction, out _direction))
        {
            Debug.LogWarningFormat(ZERO_DIRECTION_WARNING_FORMAT, context.Direction);
            Despawn();
            return;
        }

        // 재사용 탄이 이전 발사에서 맞힌 적을 다시 못 맞히는 문제 방지
        _hitDetector.ResetForLaunch(context.MaxUniqueTargetsHit);
        transform.SetPositionAndRotation(context.SpawnPosition, Quaternion.LookRotation(_direction));
    }

    /// <summary>방향을 XZ 평면에 투영 후 정규화</summary>
    /// <returns>투영 길이가 0에 가까워 방향을 정할 수 없으면 false</returns>
    public static bool TryGetPlanarDirection(Vector3 direction, out Vector3 planarDirection)
    {
        // normalized는 길이가 매우 작으면 영벡터를 반환
        planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        return planarDirection != Vector3.zero;
    }

    /// <summary>뷰포트 좌표가 카메라 렌더 화면 안인지 판정 (경계 0·1 포함, 카메라 뒤쪽 제외)</summary>
    /// <param name="viewportPoint">Camera.WorldToViewportPoint 결과</param>
    public static bool IsInsideViewport(Vector3 viewportPoint)
    {
        return viewportPoint.z > 0f
            && viewportPoint.x >= 0f && viewportPoint.x <= 1f
            && viewportPoint.y >= 0f && viewportPoint.y <= 1f;
    }

    private void CastAndHit(Vector3 startPosition, float castDistance)
    {
        int hitCount = _hitDetector.Cast(startPosition, _direction, castDistance);

        for (int i = 0; i < hitCount; i++)
        {
            // Cast 결과가 가까운 순이라 여기서 반환하면 장애물 뒤의 적은 맞지 않음
            if (_hitDetector.IsObstacle(i))
            {
                Despawn();
                return;
            }

            if (!_hitDetector.TryGetNewTarget(i, out IDamageable target, out float hitDistance))
            {
                continue;
            }

            // 프레임 사이 화면 밖으로 나간 뒤의 적중 차단
            if (_context.RequireCameraVisibility && !IsVisible(startPosition + _direction * hitDistance))
            {
                Despawn();
                return;
            }

            target.TakeDamage(_context.Damage);
            if (_hitDetector.HasReachedLimit)
            {
                Despawn();
                return;
            }
        }
    }

    private bool IsOutOfBounds()
    {
        if (_context.RequireCameraVisibility && !IsVisible(transform.position))
        {
            return true;
        }

        return _traveledDistance > _context.MaxDistance || _elapsedTime > _context.MaxLifetime;
    }

    private bool IsVisible(Vector3 worldPosition)
    {
        return IsInsideViewport(_outputCamera.WorldToViewportPoint(worldPosition));
    }

    private void Despawn()
    {
        // 반환 경로가 여러 곳이라 중복 Release 방지
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _pool.Release(this);
    }
}
