using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착 무기 인스턴스 임시 목록 (인벤토리 연결 전 테스트용)
/// </summary>
public class WeaponLoadout : MonoBehaviour
{
    private const int START_LEVEL = 1;

    private readonly List<WeaponInstance> _weapons = new List<WeaponInstance>();
    private int _nextInstanceId = 1;

    /// <summary>현재 장착 중인 무기 인스턴스 목록</summary>
    public IReadOnlyList<WeaponInstance> Weapons => _weapons;

    /// <summary>새 인스턴스 ID를 발급해 레벨 1 무기를 장착</summary>
    /// <returns>생성된 무기 인스턴스</returns>
    public WeaponInstance Equip(WeaponDefinition definition)
    {
        WeaponInstance weapon = new WeaponInstance(_nextInstanceId, definition, START_LEVEL);
        _nextInstanceId++;
        _weapons.Add(weapon);
        return weapon;
    }

    /// <summary>인스턴스 ID로 장착 해제</summary>
    /// <returns>해당 ID가 목록에 있어 제거했으면 true</returns>
    public bool Unequip(int instanceId)
    {
        for (int i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i].InstanceId == instanceId)
            {
                _weapons.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
}
