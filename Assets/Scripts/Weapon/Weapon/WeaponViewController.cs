using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 외형 표시 (주무기 손 무기·Animator 교체, 비주무기 부유 총기 배치·조준, 무기별 총구 제공)
/// </summary>
public class WeaponViewController : MonoBehaviour
{
    private const float FULL_CIRCLE_DEGREES = 360f;
    private const int FIRST_FLOATING_SLOT = 1; // 슬롯 0은 주무기(손)

    [SerializeField] private WeaponLoadout _loadout;
    [SerializeField] private Transform _playerRoot; // 부유 슬롯 중심
    [SerializeField] private Transform _visualRoot; // 슬롯 0° 기준 (모델 정면)
    [SerializeField] private Animator _animator; // 모델(CowboyDefault)의 Animator
    [SerializeField] private HandWeaponEntry[] _handWeapons; // 등록한 손 무기만 켜고 끔
    [SerializeField] private AppearanceEntry[] _appearances; // 캐릭터별 생김새 (ApplyAppearance 참고)
    [SerializeField] private float _orbitRadius = 1.5f;
    [SerializeField] private float _orbitHeight = 1.2f;
    [SerializeField] private float _slotMoveSpeed = 6f; // 초당 슬롯 이동 거리
    [SerializeField] private float _floatingTurnSpeed = 720f; // 초당 부유 총기 회전 각도(도)

    // 무기별 부유 뷰 (주무기는 gun 1부터, 나머지는 gun 0부터 자루마다 1개)
    private readonly Dictionary<WeaponInstance, List<FloatingWeaponView>> _floatingViews =
        new Dictionary<WeaponInstance, List<FloatingWeaponView>>();
    private readonly List<WeaponInstance> _staleWeapons = new List<WeaponInstance>(); // 동기화 중 파괴할 뷰의 무기
    private HandWeaponEntry _activeHandWeapon; // 주무기 종류의 손 무기, 없으면 null

    private void OnEnable()
    {
        _loadout.PrimaryChanged += OnPrimaryChanged;
        _loadout.WeaponsChanged += SyncFloatingViews;
    }

    private void OnDisable()
    {
        _loadout.PrimaryChanged -= OnPrimaryChanged;
        _loadout.WeaponsChanged -= SyncFloatingViews;
    }

    // 장착 전(무기 0개)이어도 손 무기를 모두 끈 상태로 한 번 반영, 이후 장착은 이벤트로 반영
    private void Start()
    {
        ApplyAppearance();
        OnPrimaryChanged();
    }

    // 움직이는 표적·몸통 회전을 따라가야 해서 매 프레임 갱신, WeaponAttackController.Update의 표적 탐색 이후에 읽도록 LateUpdate 사용
    private void LateUpdate()
    {
        IReadOnlyList<WeaponInstance> weapons = _loadout.Weapons;
        int totalGunCount = GetTotalGunCount();
        float maxDistance = _slotMoveSpeed * Time.deltaTime;
        float maxAngle = _floatingTurnSpeed * Time.deltaTime;
        int slotIndex = FIRST_FLOATING_SLOT;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponInstance weapon = weapons[i];
            List<FloatingWeaponView> views = _floatingViews[weapon];
            for (int j = 0; j < views.Count; j++)
            {
                views[j].MoveTowardsSlot(GetSlotPosition(slotIndex, totalGunCount), maxDistance);
                views[j].TurnTowards(weapon.CurrentTarget, maxAngle);
                slotIndex++;
            }
        }
    }

    /// <summary>슬롯의 모델 정면 기준 Y 회전 각도(도), 총 수만큼 원을 균등 분할</summary>
    /// <param name="slotIndex">0 = 주무기, 1부터 부유 총기</param>
    /// <param name="gunCount">전체 총기 수 (모든 장착 무기 GunCount 합, 1 이상)</param>
    public static float GetSlotAngle(int slotIndex, int gunCount)
    {
        return slotIndex * FULL_CIRCLE_DEGREES / gunCount;
    }

    /// <summary>무기 자루의 탄 생성 기준 총구 (주무기 gun 0이면 활성 손 무기, 아니면 부유 총기)</summary>
    /// <param name="weapon">현재 장착 중인 무기</param>
    /// <param name="gunIndex">0 ~ weapon.GunCount - 1</param>
    public Transform GetMuzzle(WeaponInstance weapon, int gunIndex)
    {
        if (weapon != _loadout.Primary)
        {
            return _floatingViews[weapon][gunIndex].Muzzle;
        }

        return gunIndex == 0 ? _activeHandWeapon.Muzzle : _floatingViews[weapon][gunIndex - 1].Muzzle;
    }

    private void OnPrimaryChanged()
    {
        ApplyHandWeapon(_loadout.Primary);
        SyncFloatingViews();
    }

    // 주무기가 없으면 손 무기를 모두 끄고 Animator Controller는 유지
    private void ApplyHandWeapon(WeaponInstance primary)
    {
        _activeHandWeapon = null;
        for (int i = 0; i < _handWeapons.Length; i++)
        {
            HandWeaponEntry entry = _handWeapons[i];
            bool isActive = primary != null && entry.WeaponType == primary.Definition.WeaponType;
            entry.Weapon.SetActive(isActive);
            if (isActive)
            {
                _activeHandWeapon = entry;
                _animator.runtimeAnimatorController = entry.AnimatorController;
            }
        }
    }

    // 장착 무기마다 부유 뷰 목록을 유지 (주무기는 GunCount - 1개, 나머지는 GunCount개), 기존 뷰는 재사용하고 부족분만 슬롯에 즉시 생성
    // shortcut: 풀링 없이 생성·파괴, 장착·레벨 변경은 인벤토리 확정 시점에만 발생. 잦아지면 풀링
    private void SyncFloatingViews()
    {
        IReadOnlyList<WeaponInstance> weapons = _loadout.Weapons;
        int totalGunCount = GetTotalGunCount();
        _staleWeapons.Clear();
        _staleWeapons.AddRange(_floatingViews.Keys);
        int slotIndex = FIRST_FLOATING_SLOT;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponInstance weapon = weapons[i];
            _staleWeapons.Remove(weapon);
            if (!_floatingViews.TryGetValue(weapon, out List<FloatingWeaponView> views))
            {
                views = new List<FloatingWeaponView>();
                _floatingViews.Add(weapon, views);
            }

            int viewCount = GetFloatingViewCount(weapon);
            while (views.Count > viewCount)
            {
                Destroy(views[views.Count - 1].gameObject);
                views.RemoveAt(views.Count - 1);
            }

            while (views.Count < viewCount)
            {
                // 모델 아래에 두면 몸통 회전에 끌려가므로 회전하지 않는 이 오브젝트 아래에 생성
                views.Add(Instantiate(weapon.Definition.FloatingViewPrefab,
                    GetSlotPosition(slotIndex + views.Count, totalGunCount), _visualRoot.rotation, transform));
            }

            slotIndex += viewCount;
        }

        for (int i = 0; i < _staleWeapons.Count; i++)
        {
            List<FloatingWeaponView> views = _floatingViews[_staleWeapons[i]];
            for (int j = 0; j < views.Count; j++)
            {
                Destroy(views[j].gameObject);
            }

            _floatingViews.Remove(_staleWeapons[i]);
        }
    }

    // 주무기 gun 0은 손 무기라 부유 뷰에서 제외
    private int GetFloatingViewCount(WeaponInstance weapon)
    {
        return weapon == _loadout.Primary ? weapon.GunCount - 1 : weapon.GunCount;
    }

    private int GetTotalGunCount()
    {
        IReadOnlyList<WeaponInstance> weapons = _loadout.Weapons;
        int total = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            total += weapons[i].GunCount;
        }

        return total;
    }

    private Vector3 GetSlotPosition(int slotIndex, int gunCount)
    {
        float angle = _visualRoot.eulerAngles.y + GetSlotAngle(slotIndex, gunCount);
        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
        return _playerRoot.position + direction * _orbitRadius + Vector3.up * _orbitHeight;
    }

    // 캐릭터 기준으로 생김새를 시작 시 한 번 정함 (주무기 승계 시 유지)
    private void ApplyAppearance()
    {
        CharacterDefinition character = _loadout.Character;
        for (int i = 0; i < _appearances.Length; i++)
        {
            AppearanceEntry entry = _appearances[i];
            bool isActive = entry.Character == character;
            entry.Body.SetActive(isActive);
            entry.Head.SetActive(isActive);
        }
    }

    /// <summary>무기 종류별 손 무기 오브젝트·총구·Animator Controller</summary>
    [Serializable]
    private class HandWeaponEntry
    {
        [SerializeField] private WeaponType _weaponType;
        [SerializeField] private GameObject _weapon; // 모델 손에 붙은 무기 오브젝트
        [SerializeField] private Transform _muzzle;
        [SerializeField] private RuntimeAnimatorController _animatorController;

        public WeaponType WeaponType => _weaponType;
        public GameObject Weapon => _weapon;
        public Transform Muzzle => _muzzle;
        public RuntimeAnimatorController AnimatorController => _animatorController;
    }

    /// <summary>캐릭터별 생김새 (Body·Head)</summary>
    [Serializable]
    private class AppearanceEntry
    {
        [SerializeField] private CharacterDefinition _character;
        [SerializeField] private GameObject _body;
        [SerializeField] private GameObject _head;

        public CharacterDefinition Character => _character;
        public GameObject Body => _body;
        public GameObject Head => _head;
    }
}
