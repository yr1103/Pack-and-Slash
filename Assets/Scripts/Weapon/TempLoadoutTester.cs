using UnityEngine;

/// <summary>
/// 임시 검증 도구 : 시작 시 추가 무기 장착, 컨텍스트 메뉴로 주무기 해제 (인벤토리 연동 전 부유 총기·주무기 승계 확인용)
/// </summary>
public class TempLoadoutTester : MonoBehaviour
{
    [SerializeField] private WeaponLoadout _loadout;
    [SerializeField] private WeaponDefinition[] _extraWeapons; // 시작 시 순서대로 장착

    private void Start()
    {
        for (int i = 0; i < _extraWeapons.Length; i++)
        {
            _loadout.Equip(_extraWeapons[i]);
        }
    }

    [ContextMenu("Unequip Primary")]
    private void UnequipPrimary()
    {
        WeaponInstance primary = _loadout.Primary;
        if (primary == null)
        {
            return;
        }

        _loadout.Unequip(primary.InstanceId);
    }
}
