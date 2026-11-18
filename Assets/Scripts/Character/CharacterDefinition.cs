using UnityEngine;

/// <summary>
/// 캐릭터별 선택 화면 표시 정보와 시작 무기 데이터
/// </summary>
[CreateAssetMenu(fileName = "New Character Definition", menuName = "Data/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _portrait;
    [SerializeField] private string _startWeaponName; // 선택 화면 표시용
    [SerializeField] private WeaponDefinition _startWeapon; // 시작 시 장착하는 고유 무기

    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Portrait => _portrait;
    public string StartWeaponName => _startWeaponName;
    public WeaponDefinition StartWeapon => _startWeapon;
}
