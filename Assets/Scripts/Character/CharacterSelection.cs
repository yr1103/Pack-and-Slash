/// <summary>
/// 타이틀에서 고른 캐릭터를 GameScene으로 전달 (선택 없이 GameScene 단독 실행 시 null)
/// </summary>
public static class CharacterSelection
{
    /// <summary>타이틀 캐릭터 선택 결과, 미선택이면 null</summary>
    public static CharacterDefinition Selected { get; set; }
}
