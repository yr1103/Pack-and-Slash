using UnityEngine;

/// <summary>
/// 전체 Enemy가 공유하는 Integration Field·Flow Field 보유, 게임 시작 시 플레이어 타일 기준 계산
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

    [SerializeField] private MapGenerator _mapGenerator;
    [SerializeField] private Transform _player;
    [SerializeField] private bool _drawTarget;
    [SerializeField] private bool _drawIntegration; // 전체 칸을 그려 Scene 뷰 부하가 커서 기본값 off
    [SerializeField] private bool _drawFlow; // 전체 칸을 그려 Scene 뷰 부하가 커서 기본값 off

    private CostField _costField;
    private IntegrationField _integrationField;
    private FlowField _flowField; // 전체 Enemy 공유, Start 이후 유효(그 전에는 null)

    public FlowField FlowField => _flowField;

    // MapGenerator가 Awake에서 CostField를 만든 뒤 사용
    private void Start()
    {
        _costField = _mapGenerator.CostField;
        _integrationField = new IntegrationField(_costField);
        _flowField = new FlowField(_costField);

        // shortcut: 시작 시 1회만 계산, 작업 E(플레이어 타일 변경 감지·0.2초 갱신)에서 교체
        _integrationField.Calculate(_costField.WorldToTile(_player.position));
        _flowField.Calculate(_integrationField);
    }

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
