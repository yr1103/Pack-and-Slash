using System;
using UnityEngine;

/// <summary>
/// 보스 돌진 패턴 : 범위 안 플레이어 방향을 고정해 경고선 표시 후 직선 돌진, 거리 도달·이동 불가 타일에서 추적 복귀
/// </summary>
public class BossChargePattern : MonoBehaviour, IBossPattern
{
    [SerializeField] private LineRenderer _warningLine; // 자식, useWorldSpace true·positionCount 2·평소 enabled false
    [SerializeField, Min(0f)] private float _chargeRange = 8f; // 경고 시작 XZ 거리
    [SerializeField, Min(0f)] private float _warnDuration = 1f; 
    [SerializeField, Min(0.01f)] private float _chargeSpeed = 15f; // 0이면 돌진이 끝나지 않음
    [SerializeField, Min(0f)] private float _chargeDistance = 10f; 
    [SerializeField, Min(0f)] private float _cooldown = 4f; // 돌진 종료 후 재경고까지(초)
    [SerializeField] private float _warningLineHeight = 0.1f; // 바닥에 묻히지 않게 띄우는 높이

    private Enemy _enemy;
    private EnemyMovement _movement;
    private BossCharge _charge;
    private Vector3 _chargeDirection; // 경고 시작 순간의 플레이어 방향 (XZ 정규화)
    private float _chargedDistance;

    /// <summary>돌진 종료(추적 복귀) 직후 발생</summary>
    public event Action Finished;

    private void Awake()
    {
        _charge = new BossCharge(_chargeRange, _warnDuration, _cooldown);
        CacheComponents();
    }

    // 풀 재사용 시 추적 상태로 초기화 (EnemyMovement 재활성화는 Enemy.OnEnable 담당)
    private void OnEnable()
    {
        _charge.Reset();
        _warningLine.enabled = false;
    }

    private void Update()
    {
        // 사망 시 Enemy.Die가 끈 EnemyMovement를 다시 켜지 않도록 돌진 종료 처리 없이 중지
        if (!_enemy.IsAlive)
        {
            if (_warningLine.enabled)
            {
                _warningLine.enabled = false;
            }

            return;
        }

        FlowFieldManager flowFieldManager = _movement.FlowFieldManager;
        Transform player = _movement.Player;
        if (flowFieldManager == null || flowFieldManager.CostField == null || player == null)
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
            if (previousState == BossCharge.ChargeState.Warn)
            {
                _warningLine.enabled = false;
            }

            MoveCharge(flowFieldManager.CostField);
        }
    }

    // 방향 고정, 추적 정지, 경고선 표시
    private void StartWarning(Vector3 toPlayer)
    {
        // 플레이어와 같은 위치면 방향이 없으므로 현재 정면 사용
        _chargeDirection = toPlayer == Vector3.zero ? transform.forward : toPlayer.normalized;
        _chargedDistance = 0f;
        _movement.enabled = false;
        transform.rotation = Quaternion.LookRotation(_chargeDirection);

        Vector3 start = transform.position + Vector3.up * _warningLineHeight;
        _warningLine.SetPosition(0, start); // 경고선 시작 정점
        _warningLine.SetPosition(1, start + _chargeDirection * _chargeDistance); // 경고선 끝 정점
        _warningLine.enabled = true;
    }

    // 고정 방향 직선 이동, 다음 위치가 이동 불가(격자 밖 포함)거나 돌진 거리 도달 시 종료
    private void MoveCharge(CostField costField)
    {
        float step = Mathf.Min(_chargeSpeed * Time.deltaTime, _chargeDistance - _chargedDistance);
        Vector3 nextPosition = transform.position + _chargeDirection * step;
        Vector2Int nextTile = costField.WorldToTile(nextPosition);
        if (!costField.IsWalkable(nextTile.x, nextTile.y))
        {
            EndCharge();
            return;
        }

        transform.position = nextPosition;
        _chargedDistance += step;
        if (_chargedDistance >= _chargeDistance)
        {
            EndCharge();
        }
    }

    private void EndCharge()
    {
        _charge.EndCharge();
        _movement.enabled = true;
        Finished?.Invoke();
    }

    private void CacheComponents()
    {
        _enemy = GetComponent<Enemy>();
        _movement = GetComponent<EnemyMovement>();
    }
}
