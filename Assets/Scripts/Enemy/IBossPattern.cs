using System;

/// <summary>
/// BossPatternSequencer가 순서대로 켜고 끄는 보스 패턴 계약
/// </summary>
public interface IBossPattern
{
    /// <summary>패턴 1회 실행(돌진 종료·발사) 완료 시 발생</summary>
    event Action Finished;
}
