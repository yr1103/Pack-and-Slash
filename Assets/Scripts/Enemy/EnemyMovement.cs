using Unity.Profiling;
using UnityEngine;

/// <summary>
/// 공용 Flow Field를 따라 장애물을 우회해 플레이어로 이동 (XZ 평면, Y 고정), 주변 Enemy와 겹침을 Separation으로 감소
/// </summary>
public class EnemyMovement : MonoBehaviour
{
    private const int NEIGHBOR_BUFFER_SIZE = 16; // 자기 자신 포함

    private static readonly ProfilerMarker _separationMarker = new ProfilerMarker("EnemyMovement.Separation");
    // shortcut: 전체 Enemy 공유 고정 크기 버퍼라 초과 시 이웃 누락, 500마리 측정에서 떨림·누락 확인 시 버퍼 확장 또는 Spatial Hash Grid 전환
    private static readonly Collider[] _neighborColliders = new Collider[NEIGHBOR_BUFFER_SIZE];
    private static readonly Vector3[] _neighborPositions = new Vector3[NEIGHBOR_BUFFER_SIZE];

    [SerializeField, Min(0f)] private float _moveSpeed = 3f;
    [SerializeField, Min(0f)] private float _stopDistance = 1f; // 플레이어 반지름 0.4 + 큐브 반폭 0.5 + 여유
    [SerializeField, Min(0f)] private float _separationRadius = 1.2f; // 큐브 폭 1 + 여유
    [SerializeField, Min(0f)] private float _separationWeight = 1f; // Flow 방향(길이 1) 대비 Separation 비중
    [SerializeField] private LayerMask _enemyLayer; // Separation 이웃 탐색 대상
    [SerializeField, Min(0f)] private float _knockbackDeceleration = 20f; // 넉백 속도 초당 감소량 (임시값)
    [SerializeField, Min(0f)] private float _maxKnockbackSpeed = 10f; // 누적 넉백 속도 상한 (임시값)
    [SerializeField, Range(0f, 1f)] private float _knockbackResistance; // 넉백 감소율, 0 = 저항 없음 (보스 0.7 임시값)

    private FlowFieldManager _flowFieldManager; // Initialize로 주입, 미주입이면 정지
    private Transform _player;
    private Vector3 _lastDirection; // 보간 방향 0(장애물 모서리 파고듦)에서 고착 방지용
    private Vector3 _velocity;
    private Vector3 _knockbackVelocity; // Flow·Separation 이동과 별도로 더하는 XZ 넉백 속도

    /// <summary>현재 이동 속도 벡터 (정지 시 Vector3.zero, Animator 등 표시용)</summary>
    public Vector3 Velocity => _velocity;

    public FlowFieldManager FlowFieldManager => _flowFieldManager;
    public Transform Player => _player;

    // FlowField를 캐시하지 않고 매 프레임 조회 (초기화 순서·재계산과 무관하게 유효)
    private void Update()
    {
        if (_flowFieldManager == null || _flowFieldManager.FlowField == null)
        {
            _velocity = Vector3.zero;
            return;
        }

        CostField costField = _flowFieldManager.CostField;
        // 정지 거리 안 등 아래 조기 반환 경로에서도 밀려나도록 먼저 적용
        ApplyKnockback(costField);

        Vector2Int targetTile = _flowFieldManager.TargetTile;
        Vector3 position = transform.position;
        Vector3 playerPosition = _player.position;
        Vector3 direction = GetMoveDirection(costField, _flowFieldManager.FlowField,
            targetTile, position, playerPosition, _lastDirection, _stopDistance);

        if (direction == Vector3.zero)
        {
            _velocity = Vector3.zero;
            return;
        }

        Vector3 toPlayer = playerPosition - position;
        toPlayer.y = 0f;
        // 한 프레임 이동량이 (플레이어까지 거리 - 정지 거리) 이하면 어느 방향으로 가도 정지 거리 안으로 들어가지 않음
        float moveDistance = Mathf.Min(_moveSpeed * Time.deltaTime, toPlayer.magnitude - _stopDistance);

        // 격자 밖·목적지 타일은 플레이어 방향을 그대로 사용 (GetMoveDirection 분기와 동일 조건)
        Vector2Int tile = costField.WorldToTile(position);
        if (costField.IsInBounds(tile.x, tile.y) && tile != targetTile)
        {
            using (_separationMarker.Auto())
            {
                Vector3 separation = FindSeparation(position);
                direction = GetSeparatedDirection(costField, position, direction, separation, _separationWeight, moveDistance);
            }
        }

        _lastDirection = direction;

        transform.position = position + direction * moveDistance;
        transform.rotation = Quaternion.LookRotation(direction);
        _velocity = direction * _moveSpeed;
    }

    // Time.deltaTime 기준이라 일시정지(timeScale 0) 중에는 이동·감속 모두 정지
    private void ApplyKnockback(CostField costField)
    {
        if (_knockbackVelocity == Vector3.zero)
        {
            return;
        }

        Vector3 position = transform.position;
        Vector3 nextPosition = position + _knockbackVelocity * Time.deltaTime;
        // 격자 밖에서는 기존 이동처럼 타일 검사 생략, 격자 안에서 이동 불가 타일로 밀리면 넉백 종료
        // shortcut: 중심점의 다음 타일만 검사해 ㄱ자 모서리 대각 통과·몸체 일부 벽 겹침 가능, 문제되면 몸체 반폭 기준 검사로 교체
        Vector2Int tile = costField.WorldToTile(position);
        Vector2Int nextTile = costField.WorldToTile(nextPosition);
        if (costField.IsInBounds(tile.x, tile.y) && !costField.IsWalkable(nextTile.x, nextTile.y))
        {
            _knockbackVelocity = Vector3.zero;
            return;
        }

        transform.position = nextPosition;
        _knockbackVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, _knockbackDeceleration * Time.deltaTime);
    }

    /// <summary>넉백 속도를 더함 (저항만큼 감소, 누적 크기는 상한으로 자름)</summary>
    /// <param name="direction">밀어낼 방향 (XZ 평면 투영 후 정규화, 0이면 무시)</param>
    /// <param name="strength">넉백 속도 (초당 거리)</param>
    public void AddKnockback(Vector3 direction, float strength)
    {
        _knockbackVelocity = CalculateKnockbackVelocity(_knockbackVelocity, direction, strength,
            _knockbackResistance, _maxKnockbackSpeed);
    }

    /// <summary>현재 넉백 속도에 방향 × 강도 × (1 - 저항)을 더하고 크기를 상한으로 자름</summary>
    /// <param name="direction">XZ 평면 투영 후 정규화, 0이면 현재 속도 유지</param>
    /// <param name="resistance">0 = 저항 없음, 1 = 넉백 무효</param>
    /// <returns>XZ 평면 넉백 속도 (크기 maxSpeed 이하)</returns>
    public static Vector3 CalculateKnockbackVelocity(Vector3 current, Vector3 direction, float strength,
        float resistance, float maxSpeed)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        Vector3 added = current + planarDirection * (strength * (1f - resistance));
        return Vector3.ClampMagnitude(added, maxSpeed);
    }

    // 물리 탐색으로 주변 Enemy 위치를 모아 Separation 벡터 계산
    private Vector3 FindSeparation(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, _separationRadius, _neighborColliders, _enemyLayer);
        for (int i = 0; i < count; i++)
        {
            _neighborPositions[i] = _neighborColliders[i].transform.position;
        }

        return CalculateSeparation(position, _neighborPositions, count, _separationRadius);
    }

    /// <summary>
    /// 이동 방향 결정 : 정지 거리 안이면 정지, 격자 밖·목적지 타일은 플레이어 방향, 일반 타일은 주변 Flow 방향 보간, 보간 결과가 0이면 직전 방향
    /// </summary>
    /// <param name="targetTile">Flow Field의 목적지 타일</param>
    /// <param name="previousDirection">직전 이동 방향 (XZ 정규화 방향, 최초면 Vector3.zero)</param>
    /// <param name="stopDistance">플레이어까지 XZ 거리가 이 값 이하면 정지</param>
    /// <returns>XZ 평면 정규화 방향 (정지 거리 안이거나, 보간 결과가 0이고 previousDirection이 zero면 Vector3.zero)</returns>
    public static Vector3 GetMoveDirection(CostField costField, FlowField flowField, Vector2Int targetTile,
        Vector3 enemyPosition, Vector3 playerPosition, Vector3 previousDirection, float stopDistance)
    {
        Vector3 toPlayer = playerPosition - enemyPosition;
        toPlayer.y = 0f;
        if (toPlayer.magnitude <= stopDistance)
        {
            return Vector3.zero;
        }

        Vector2Int tile = costField.WorldToTile(enemyPosition);
        if (!costField.IsInBounds(tile.x, tile.y) || tile == targetTile)
        {
            return toPlayer.normalized;
        }

        Vector2 flowDirection = flowField.GetInterpolatedDirection(enemyPosition);
        if (flowDirection == Vector2.zero)
        {
            return previousDirection;
        }

        return new Vector3(flowDirection.x, 0f, flowDirection.y);
    }

    /// <summary>
    /// Separation 벡터 계산 : 반경 안 이웃에서 멀어지는 XZ 방향을 가까울수록 크게(반경 경계 0 ~ 최근접 1) 합산
    /// </summary>
    /// <param name="neighborPositions">이웃 위치 (자기 자신이 포함돼도 거리 0이라 제외)</param>
    /// <param name="neighborCount">neighborPositions 중 유효한 앞쪽 개수</param>
    /// <returns>합산 벡터 (정규화하지 않음, 이웃이 없으면 Vector3.zero)</returns>
    public static Vector3 CalculateSeparation(Vector3 enemyPosition, Vector3[] neighborPositions, int neighborCount, float radius)
    {
        Vector3 separation = Vector3.zero;
        for (int i = 0; i < neighborCount; i++)
        {
            Vector3 away = enemyPosition - neighborPositions[i];
            away.y = 0f;
            float distance = away.magnitude;
            // 거리 0은 방향을 정할 수 없고, OverlapSphere는 Collider 표면 기준이라 중심이 반경 밖인 이웃도 들어옴
            if (distance <= 0f || distance >= radius)
            {
                continue;
            }

            separation += away / distance * (1f - distance / radius);
        }

        return separation;
    }

    /// <summary>
    /// 최종 방향 결정 : (Flow 방향 + Separation × 가중치) 정규화, 그 방향으로 이동한 위치가 이동 불가 타일이거나 합성 결과가 0이면 Flow 방향
    /// </summary>
    /// <param name="flowDirection">GetMoveDirection 결과 (XZ 정규화 방향)</param>
    /// <param name="moveDistance">이번 프레임 이동 거리 (다음 위치 판정용)</param>
    /// <returns>XZ 평면 정규화 방향</returns>
    public static Vector3 GetSeparatedDirection(CostField costField, Vector3 enemyPosition, Vector3 flowDirection,
        Vector3 separation, float weight, float moveDistance)
    {
        Vector3 combined = (flowDirection + separation * weight).normalized;
        if (combined == Vector3.zero)
        {
            return flowDirection;
        }

        // shortcut: 다음 위치 타일만 확인해 ㄱ자 모서리 대각 통과 미처리, 모서리 끼임 확인 시 CanPassDiagonal 판정 추가
        Vector2Int nextTile = costField.WorldToTile(enemyPosition + combined * moveDistance);
        if (!costField.IsWalkable(nextTile.x, nextTile.y))
        {
            return flowDirection;
        }

        return combined;
    }

    /// <summary>생성 직후 호출해 공용 Flow Field 관리자와 플레이어 참조 주입</summary>
    public void Initialize(FlowFieldManager flowFieldManager, Transform player)
    {
        _flowFieldManager = flowFieldManager;
        _player = player;
        _knockbackVelocity = Vector3.zero; // 풀 재사용 시 이전 생애의 넉백 제거
    }
}
