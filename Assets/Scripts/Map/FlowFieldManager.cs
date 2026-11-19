using Unity.Profiling;
using UnityEngine;

/// <summary>
/// 전체 Enemy가 공유하는 Integration Field·Flow Field 보유, 플레이어 타일이 바뀌면 최소 간격을 두고 재계산
/// </summary>
public class FlowFieldManager : MonoBehaviour
{
    private const float FLOW_LINE_LENGTH = 0.4f; // 타일 중심에서 방향선 끝까지 거리

    private static readonly Vector3 _gizmoLift = new Vector3(0f, 0.05f, 0f); // 바닥과 겹쳐 가려지지 않도록 띄움
    private static readonly Vector3 _targetGizmoSize = Vector3.one;
    private static readonly Vector3 _integrationGizmoSize = new Vector3(1f, 0.05f, 1f);
    private static readonly Vector3 _flowTipGizmoSize = new Vector3(0.1f, 0.1f, 0.1f); // 방향선 끝 표시(선만으로는 방향 구분 불가)
    private static readonly Color _targetGizmoColor = Color.cyan;
    private static readonly Color _nearCostGizmoColor = new Color(0f, 1f, 0f, 0.4f);
    private static readonly Color _farCostGizmoColor = new Color(1f, 0f, 1f, 0.4f);
    private static readonly Color _flowGizmoColor = Color.white;
    private static readonly ProfilerMarker _integrationMarker = new ProfilerMarker("FlowFieldManager.CalculateIntegration");
    private static readonly ProfilerMarker _flowMarker = new ProfilerMarker("FlowFieldManager.CalculateFlow");

    [SerializeField] private MapGenerator _mapGenerator;
    [SerializeField] private Transform _player;
    [SerializeField, Min(0f)] private float _updateInterval = 0.2f; // 재계산 최소 간격(초)
    
    // Scene 뷰 Enemy 이동 경로 검증용 
    [SerializeField] private bool _drawTarget; // 플레이어 위치 청록색 박스로 표시
    [SerializeField] private bool _drawIntegration; // 근거리 초록 → 원거리 자홍, 도달 불가 칸 미표시 
    [SerializeField] private bool _drawFlow; // 흰 선의 끝 큐브 쪽이 Enemy 이동 방향

    private CostField _costField;
    private IntegrationField _integrationField;
    private FlowField _flowField; // 전체 Enemy 공유, Start 이후 유효(그 전에는 null)
    private float _lastCalculateTime;

    public FlowField FlowField => _flowField;
    public CostField CostField => _costField;

    /// <summary>Flow Field가 향하는 목적지 타일 (Start 이전 접근 불가, FlowField null 확인 후 사용)</summary>
    public Vector2Int TargetTile => _integrationField.Target;

    // MapGenerator가 Awake에서 CostField를 만든 뒤 사용
    private void Start()
    {
        _costField = _mapGenerator.CostField;
        _integrationField = new IntegrationField(_costField);
        _flowField = new FlowField(_costField);
        Calculate(_costField.WorldToTile(_player.position));
    }

    private void Update()
    {
        float elapsed = Time.time - _lastCalculateTime;
        Vector2Int playerTile = _costField.WorldToTile(_player.position);
        if (ShouldRecalculate(playerTile, _integrationField.Target, elapsed, _updateInterval))
        {
            Calculate(playerTile);
        }
    }

    /// <summary>재계산 필요 여부 : 플레이어 타일이 마지막 목적지와 다르고, 마지막 계산 후 간격 이상 지났을 때 true</summary>
    /// <param name="elapsed">마지막 계산 후 경과 시간(초)</param>
    /// <param name="interval">재계산 최소 간격(초), 경과 시간이 같으면 재계산</param>
    public static bool ShouldRecalculate(Vector2Int playerTile, Vector2Int lastTarget, float elapsed, float interval)
    {
        return playerTile != lastTarget && elapsed >= interval;
    }

    // 단일 스레드 동기 계산, 계산 중 Enemy가 읽지 않으므로 이중 버퍼 미사용
    private void Calculate(Vector2Int target)
    {
        using (_integrationMarker.Auto())
        {
            _integrationField.Calculate(target);
        }

        using (_flowMarker.Auto())
        {
            _flowField.Calculate(_integrationField);
        }

        _lastCalculateTime = Time.time;
    }

    // 확인 조건 : Scene·Game 뷰 Gizmos 버튼과 항목별 Inspector 토글(_drawTarget·_drawIntegration·_drawFlow)이 켜져 있을 때 표시
    private void OnDrawGizmos()
    {
        // 에디터 비실행 시에는 계산 결과가 없음
        if (_flowField == null)
        {
            return;
        }

        if (_drawIntegration)
        {
            DrawIntegrationGizmos();
        }

        if (_drawFlow)
        {
            DrawFlowGizmos();
        }

        if (_drawTarget)
        {
            Vector2Int target = _integrationField.Target;
            Gizmos.color = _targetGizmoColor;
            Gizmos.DrawWireCube(_costField.TileToWorld(target.x, target.y) + _gizmoLift, _targetGizmoSize);
        }
    }

    // 도달 가능한 칸만 누적 비용에 비례해 근거리 색 → 원거리 색으로 표시
    private void DrawIntegrationGizmos()
    {
        float maxCost = 0f;
        for (int x = 0; x < _costField.Width; x++)
        {
            for (int z = 0; z < _costField.Height; z++)
            {
                float cost = _integrationField.GetIntegration(x, z);
                if (!float.IsPositiveInfinity(cost))
                {
                    maxCost = Mathf.Max(maxCost, cost);
                }
            }
        }

        for (int x = 0; x < _costField.Width; x++)
        {
            for (int z = 0; z < _costField.Height; z++)
            {
                float cost = _integrationField.GetIntegration(x, z);
                if (float.IsPositiveInfinity(cost))
                {
                    continue;
                }

                // 목적지만 도달 가능하면 maxCost가 0이라 나눗셈 대신 InverseLerp로 0 처리
                Gizmos.color = Color.Lerp(_nearCostGizmoColor, _farCostGizmoColor, Mathf.InverseLerp(0f, maxCost, cost));
                Gizmos.DrawCube(_costField.TileToWorld(x, z), _integrationGizmoSize);
            }
        }
    }

    private void DrawFlowGizmos()
    {
        Gizmos.color = _flowGizmoColor;
        for (int x = 0; x < _costField.Width; x++)
        {
            for (int z = 0; z < _costField.Height; z++)
            {
                Vector2 direction = _flowField.GetDirection(x, z);
                // 방향 0 칸(목적지·장애물·도달 불가)은 선 없이 중심 큐브만 남아 방향 표시와 혼동되므로 생략
                if (direction == Vector2.zero)
                {
                    continue;
                }

                Vector3 start = _costField.TileToWorld(x, z) + _gizmoLift;
                Vector3 end = start + new Vector3(direction.x, 0f, direction.y) * FLOW_LINE_LENGTH;
                Gizmos.DrawLine(start, end);
                Gizmos.DrawCube(end, _flowTipGizmoSize);
            }
        }
    }
}
