using UnityEngine;

/// <summary>
/// 공용 Flow Field를 따라 장애물을 우회해 플레이어로 이동 (XZ 평면, Y 고정)
/// </summary>
public class EnemyMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _moveSpeed = 3f;
    [SerializeField, Min(0f)] private float _stopDistance = 1f; // 플레이어 반지름 0.4 + 큐브 반폭 0.5 + 여유

    private FlowFieldManager _flowFieldManager; // Initialize로 주입, 미주입이면 정지
    private Transform _player;
    private Vector3 _lastDirection; // 보간 방향 0(장애물 모서리 파고듦)에서 고착 방지용
    private Vector3 _velocity;

    /// <summary>현재 이동 속도 벡터 (정지 시 Vector3.zero, Animator 등 표시용)</summary>
    public Vector3 Velocity => _velocity;

    // FlowField를 캐시하지 않고 매 프레임 조회 (초기화 순서·재계산과 무관하게 유효)
    private void Update()
    {
        if (_flowFieldManager == null || _flowFieldManager.FlowField == null)
        {
            _velocity = Vector3.zero;
            return;
        }

        Vector3 position = transform.position;
        Vector3 playerPosition = _player.position;
        Vector3 direction = GetMoveDirection(_flowFieldManager.CostField, _flowFieldManager.FlowField,
            _flowFieldManager.TargetTile, position, playerPosition, _lastDirection, _stopDistance);

        if (direction == Vector3.zero)
        {
            _velocity = Vector3.zero;
            return;
        }

        _lastDirection = direction;

        Vector3 toPlayer = playerPosition - position;
        toPlayer.y = 0f;
        // 한 프레임 이동량이 (플레이어까지 거리 - 정지 거리) 이하면 어느 방향으로 가도 정지 거리 안으로 들어가지 않음
        // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
        float moveDistance = Mathf.Min(_moveSpeed * Time.deltaTime, toPlayer.magnitude - _stopDistance);

        transform.position = position + direction * moveDistance;
        transform.rotation = Quaternion.LookRotation(direction);
        _velocity = direction * _moveSpeed;
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

    /// <summary>생성 직후 호출해 공용 Flow Field 관리자와 플레이어 참조 주입</summary>
    public void Initialize(FlowFieldManager flowFieldManager, Transform player)
    {
        _flowFieldManager = flowFieldManager;
        _player = player;
    }
}
