using UnityEngine;

/// <summary>
/// 보스 돌진 패턴의 상태 전환 : 추적 중 범위 안·쿨다운 종료면 경고, 경고 시간 후 돌진, 돌진 종료 통지 시 추적 복귀·쿨다운 시작
/// </summary>
public class BossCharge
{
    /// <summary>돌진 패턴 상태 (Warn·Charge 중 EnemyMovement는 BossChargePattern이 끔)</summary>
    public enum ChargeState
    {
        Chase, // Flow Field 추적, 쿨다운 진행
        Warn, // 방향 고정 후 경고선 표시
        Charge  // 고정 방향 직선 돌진
    }

    private readonly float _range;
    private readonly float _warnDuration;
    private readonly float _cooldown;
    private float _warnRemaining;
    private float _cooldownRemaining;

    /// <summary>추적 상태·쿨다운 없음으로 생성</summary>
    /// <param name="range">경고 시작 XZ 거리</param>
    /// <param name="warnDuration">경고 시간(초)</param>
    /// <param name="cooldown">돌진 종료 후 재경고까지(초)</param>
    public BossCharge(float range, float warnDuration, float cooldown)
    {
        _range = range;
        _warnDuration = warnDuration;
        _cooldown = cooldown;
    }

    /// <summary>현재 상태 (Charge는 EndCharge 호출 전까지 유지)</summary>
    public ChargeState State { get; private set; }

    /// <summary>타이머 진행과 상태 전환 (매 프레임 호출)</summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    /// <param name="distanceToPlayer">플레이어까지 XZ 거리 (Chase에서만 사용)</param>
    public void Tick(float deltaTime, float distanceToPlayer)
    {
        if (State == ChargeState.Chase)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
            if (_cooldownRemaining <= 0f && distanceToPlayer <= _range)
            {
                State = ChargeState.Warn;
                _warnRemaining = _warnDuration;
            }

            return;
        }

        if (State == ChargeState.Warn)
        {
            _warnRemaining -= deltaTime;
            if (_warnRemaining <= 0f)
            {
                State = ChargeState.Charge;
            }
        }
    }

    /// <summary>돌진 종료 통지 (거리 도달·벽 정지 시), 추적 복귀 후 쿨다운 시작</summary>
    public void EndCharge()
    {
        State = ChargeState.Chase;
        _cooldownRemaining = _cooldown;
    }

    /// <summary>추적 상태·쿨다운 없음으로 초기화 (풀 재사용 시)</summary>
    public void Reset()
    {
        State = ChargeState.Chase;
        _warnRemaining = 0f;
        _cooldownRemaining = 0f;
    }
}
