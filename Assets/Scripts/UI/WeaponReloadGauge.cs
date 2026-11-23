using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 옆 World Space 세로 재장전 게이지 (재장전 중인 무기가 있을 때만 표시, 빌보드는 부모 HealthBarCanvas가 처리)
/// </summary>
public class WeaponReloadGauge : MonoBehaviour
{
    [SerializeField] private WeaponLoadout _loadout;
    [SerializeField] private Image _fill; // Image Type = Filled, Fill Method = Vertical
    [SerializeField] private GameObject _visualRoot; // 표시/숨김 대상 (LateUpdate가 계속 돌도록 자기 자신이 아닌 자식)

    private void LateUpdate()
    {
        // 진행률은 매 프레임 변하므로 이벤트가 아닌 매 프레임 갱신
        WeaponInstance reloading = FindReloadingWeapon();
        _visualRoot.SetActive(reloading != null);
        if (reloading != null)
        {
            _fill.fillAmount = reloading.ReloadProgress;
        }
    }

    // shortcut: 여러 무기가 동시에 재장전하면 첫 무기만 표시, 무기별 게이지가 필요해지면 무기마다 게이지를 둔다
    private WeaponInstance FindReloadingWeapon()
    {
        foreach (WeaponInstance weapon in _loadout.Weapons)
        {
            if (weapon.IsReloading)
            {
                return weapon;
            }
        }

        return null;
    }
}
