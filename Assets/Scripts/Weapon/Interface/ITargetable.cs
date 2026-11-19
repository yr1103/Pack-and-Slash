using UnityEngine;

/// <summary>
/// 무기가 탐색·조준할 수 있는 표적 계약
/// </summary>
public interface ITargetable
{
    /// <summary>조준 위치 (월드 좌표)</summary>
    Vector3 AimPoint { get; }

    /// <summary>살아 있고 공격 가능한 상태 여부</summary>
    bool IsTargetable { get; }

    /// <summary>생성 순으로 증가하는 고유 ID (낮을수록 먼저 생성)</summary>
    int SpawnId { get; }
}
