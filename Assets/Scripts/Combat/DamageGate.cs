using UnityEngine;

/// <summary>
/// 피해 1회 통과 후 무적시간 동안 추가 피해를 막는 게이트 (무적시간 0이면 매번 통과)
/// </summary>
public class DamageGate
{
    private readonly float _invulnerabilityDuration;
    private float _remaining;

    public DamageGate(float invulnerabilityDuration)
    {
        _invulnerabilityDuration = invulnerabilityDuration;
    }

    /// <summary>피해를 통과시킬 수 있는 상태 여부 (마지막 통과 후 무적시간 이상 경과하면 true)</summary>
    public bool IsOpen => _remaining <= 0f;

    /// <summary>무적 남은 시간 감소 (매 프레임 호출)</summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Tick(float deltaTime)
    {
        _remaining = Mathf.Max(0f, _remaining - deltaTime);
    }

    /// <summary>피해 통과 시도, 통과하면 무적시간 시작</summary>
    /// <returns>열려 있어 통과했으면 true, 무적 중이면 false</returns>
    public bool TryPass()
    {
        if (!IsOpen)
        {
            return false;
        }

        _remaining = _invulnerabilityDuration;
        return true;
    }
}
