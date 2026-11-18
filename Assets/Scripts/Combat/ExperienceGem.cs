using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 경험치 젬 수집 흐름 (대기 → 수집 반경 진입 시 흩어짐 → 플레이어 중심으로 흡수 → 도착 시 XP 1회 지급 후 풀 반환)
/// </summary>
public class ExperienceGem : MonoBehaviour
{
    private const float FULL_CIRCLE_ANGLE = 360f;
    private const float OVERLAP_SQR_DISTANCE = 0.000001f; // XZ 거리 제곱이 이 값 이하면 방향을 정할 수 없는 겹침으로 판정

    private enum State
    {
        Idle, // 수집 반경 진입 대기
        Scatter, // 플레이어 반대쪽으로 짧게 감속 이동
        Attract, // 플레이어 중심으로 이동 (진입 후 반경 변화와 무관하게 계속)
    }

    [SerializeField, Min(0f)] private float _scatterAngle = 60f; // 플레이어 반대 방향 기준 좌우 최대 회전 각도(도)
    [SerializeField, Min(0.01f)] private float _scatterDuration = 0.15f; // 흩어짐 이동 시간(초)
    [SerializeField, Min(0f)] private float _scatterDistance = 0.6f; // 흩어짐 이동 거리
    [SerializeField, Min(0f)] private float _attractSpeed = 10f; // 흡수 이동 속도(m/s)
    [SerializeField, Min(0f)] private float _arriveDistance = 0.1f; // 플레이어 중심까지 이 거리 이하면 도착

    private PlayerExperience _playerExperience;
    private IObjectPool<ExperienceGem> _pool;
    private int _experienceValue;
    private State _state;
    private Vector3 _scatterStart;
    private Vector3 _scatterDirection;
    private float _scatterElapsed;

    private void Update()
    {
        switch (_state)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Scatter:
                UpdateScatter();
                break;
            case State.Attract:
                UpdateAttract();
                break;
        }
    }

    private void UpdateIdle()
    {
        Vector3 playerPosition = _playerExperience.transform.position;
        if (!IsInCollectRadius(transform.position, playerPosition, _playerExperience.CollectRadius))
        {
            return;
        }

        _scatterStart = transform.position;
        _scatterDirection = GetScatterDirection(_scatterStart, playerPosition, _scatterAngle, Random.value);
        _scatterElapsed = 0f;
        _state = State.Scatter;
    }

    private void UpdateScatter()
    {
        // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
        _scatterElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_scatterElapsed / _scatterDuration);
        // ease-out (2차) : 처음 빠르게 튕긴 뒤 감속
        float easedProgress = 1f - (1f - progress) * (1f - progress);
        transform.position = _scatterStart + _scatterDirection * (_scatterDistance * easedProgress);

        if (progress >= 1f)
        {
            _state = State.Attract;
        }
    }

    private void UpdateAttract()
    {
        Vector3 playerPosition = _playerExperience.transform.position;
        // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
        transform.position = Vector3.MoveTowards(transform.position, playerPosition, _attractSpeed * Time.deltaTime);

        if ((playerPosition - transform.position).sqrMagnitude <= _arriveDistance * _arriveDistance)
        {
            Collect();
        }
    }

    // 지급 직후 반환(비활성화)되어 Update가 다시 돌지 않으므로 중복 지급 없음
    private void Collect()
    {
        _playerExperience.AddExperience(_experienceValue);
        _pool.Release(this);
    }

    /// <summary>젬이 수집 반경 안인지 판정 (XZ 거리, 높이 무시, 경계 포함)</summary>
    public static bool IsInCollectRadius(Vector3 gemPosition, Vector3 playerPosition, float collectRadius)
    {
        Vector3 offset = Vector3.ProjectOnPlane(gemPosition - playerPosition, Vector3.up);
        return offset.sqrMagnitude <= collectRadius * collectRadius;
    }

    /// <summary>흩어짐 XZ 정규화 방향 (플레이어→젬 방향을 ±scatterAngle 안에서 회전, XZ가 겹치면 전 방향 중 하나)</summary>
    /// <param name="scatterAngle">좌우 최대 회전 각도(도)</param>
    /// <param name="randomRatio">0 = -scatterAngle, 0.5 = 플레이어 반대 방향, 1 = +scatterAngle, 겹침이면 0~1 = 월드 정면 기준 0~360도 (호출부가 Random.value 등으로 공급)</param>
    public static Vector3 GetScatterDirection(Vector3 gemPosition, Vector3 playerPosition, float scatterAngle, float randomRatio)
    {
        Vector3 awayDirection = Vector3.ProjectOnPlane(gemPosition - playerPosition, Vector3.up);
        if (awayDirection.sqrMagnitude <= OVERLAP_SQR_DISTANCE)
        {
            return Quaternion.AngleAxis(randomRatio * FULL_CIRCLE_ANGLE, Vector3.up) * Vector3.forward;
        }

        float angle = Mathf.Lerp(-scatterAngle, scatterAngle, randomRatio);
        return Quaternion.AngleAxis(angle, Vector3.up) * awayDirection.normalized;
    }

    /// <summary>풀에서 꺼낸 직후 호출해 지급 대상·값·반환할 풀 주입 후 대기 상태로 시작</summary>
    /// <param name="experienceValue">도착 시 지급할 경험치</param>
    public void Initialize(PlayerExperience playerExperience, int experienceValue, IObjectPool<ExperienceGem> pool)
    {
        _playerExperience = playerExperience;
        _experienceValue = experienceValue;
        _pool = pool;
        // 풀 재사용 시 이전 수집 진행 상태 제거
        _state = State.Idle;
        _scatterElapsed = 0f;
    }
}
