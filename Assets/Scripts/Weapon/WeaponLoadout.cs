using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착 무기 목록과 주무기 소유 (시작 시 고유 무기 장착, 인벤토리 연결 전 임시 구현)
/// </summary>
public class WeaponLoadout : MonoBehaviour
{
    private const int START_LEVEL = 1;

    // shortcut: 카우보이 고유 무기(권총) 임시 지정, 캐릭터 데이터 추가 시 이전
    [SerializeField] private WeaponDefinition _signatureWeapon;

    private readonly List<WeaponInstance> _weapons = new List<WeaponInstance>();
    private int _nextInstanceId = 1;

    public WeaponDefinition SignatureWeapon => _signatureWeapon;

    /// <summary>현재 장착 중인 무기 인스턴스 목록</summary>
    public IReadOnlyList<WeaponInstance> Weapons => _weapons;

    /// <summary>몸통 조준 기준 주무기, 장착 무기가 없으면 null (주무기가 없을 때 장착하면 지정, 주무기 해제 시에만 재선정)</summary>
    public WeaponInstance Primary { get; private set; }

    /// <summary>Primary 값이 실제로 바뀐 직후 발생 (null로 바뀐 경우 포함, 같은 호출의 WeaponsChanged보다 먼저)</summary>
    public event Action PrimaryChanged;

    /// <summary>장착·해제가 성공할 때마다 Primary 갱신 후 발생</summary>
    public event Action WeaponsChanged;

    // 다른 컴포넌트가 Start·Update에서 장착 목록을 읽기 전에 고유 무기를 채움
    private void Awake()
    {
        Equip(_signatureWeapon);
    }

    /// <summary>새 인스턴스 ID를 발급해 레벨 1 무기를 장착 (주무기가 없으면 주무기로 지정)</summary>
    /// <returns>생성된 무기 인스턴스</returns>
    public WeaponInstance Equip(WeaponDefinition definition)
    {
        WeaponInstance weapon = new WeaponInstance(_nextInstanceId, definition, START_LEVEL);
        _nextInstanceId++;
        _weapons.Add(weapon);
        if (Primary == null)
        {
            Primary = weapon;
            PrimaryChanged?.Invoke();
        }

        WeaponsChanged?.Invoke();
        return weapon;
    }

    /// <summary>인스턴스 ID로 장착 해제 (주무기였으면 남은 무기 중 재선정)</summary>
    /// <returns>해당 ID가 목록에 있어 제거했으면 true</returns>
    public bool Unequip(int instanceId)
    {
        for (int i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i].InstanceId == instanceId)
            {
                WeaponInstance removed = _weapons[i];
                _weapons.RemoveAt(i);
                // 해제한 주무기는 목록에 없으므로 재선정 결과는 항상 다른 값
                if (removed == Primary)
                {
                    Primary = SelectPrimary(_weapons, _signatureWeapon);
                    PrimaryChanged?.Invoke();
                }

                WeaponsChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    /// <summary>주무기 선정 (진화 → 고유 무기 → 높은 레벨 → 낮은 InstanceId 순으로 우선)</summary>
    /// <param name="signatureWeapon">캐릭터 고유 무기 Definition</param>
    /// <returns>선정된 무기, 목록이 비어 있으면 null</returns>
    public static WeaponInstance SelectPrimary(IReadOnlyList<WeaponInstance> weapons, WeaponDefinition signatureWeapon)
    {
        WeaponInstance selected = null;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (selected == null || IsPreferred(weapons[i], selected, signatureWeapon))
            {
                selected = weapons[i];
            }
        }

        return selected;
    }

    // 앞 조건이 같을 때만 다음 조건으로 비교
    private static bool IsPreferred(WeaponInstance candidate, WeaponInstance current, WeaponDefinition signatureWeapon)
    {
        if (candidate.IsEvolved != current.IsEvolved)
        {
            return candidate.IsEvolved;
        }

        bool isCandidateSignature = candidate.Definition == signatureWeapon;
        bool isCurrentSignature = current.Definition == signatureWeapon;
        if (isCandidateSignature != isCurrentSignature)
        {
            return isCandidateSignature;
        }

        if (candidate.Level != current.Level)
        {
            return candidate.Level > current.Level;
        }

        return candidate.InstanceId < current.InstanceId;
    }
}
