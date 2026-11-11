using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착 무기별 쿨다운·표적 탐색·발사 처리 (무기마다 독립 판정)
/// </summary>
public class WeaponAttackController : MonoBehaviour
{
    // 첫 적 적중 후 반환, 미니건 진화 시 WeaponDefinition 필드로 이전
    private const int MAX_UNIQUE_TARGETS_HIT = 1;

    [SerializeField] private WeaponLoadout _loadout;
    [SerializeField] private ProjectilePool _pool;
    [SerializeField] private Transform _playerRoot; // 적 탐색 원점
    [SerializeField] private Transform _muzzle; // MuzzlePoint, 탄 생성·조준 기준

    private void Update()
    {
        // shortcut: 표적 후보를 임시 Cube 적 목록에서 공급, EnemyManager/Spatial Hash 연동 시 교체
        IReadOnlyList<ITargetable> candidates = TempCubeEnemy.ActiveEnemies;
        IReadOnlyList<WeaponInstance> weapons = _loadout.Weapons;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponInstance weapon = weapons[i];
            // shortcut: 일시정지 등 게임 시간 관리 연동 전 Time.deltaTime 직접 사용, GameState 도입 시 교체
            weapon.Tick(Time.deltaTime);

            WeaponDefinition definition = weapon.Definition;
            // 몸통 회전이 움직이는 표적을 따라가도록 쿨다운과 무관하게 매 프레임 갱신, 발사도 이 결과 사용
            ITargetable target = WeaponTargeting.FindNearest(_playerRoot.position, definition.DetectionRange, candidates);
            weapon.CurrentTarget = target;
            if (weapon.ShotCooldown > 0f)
            {
                continue;
            }

            // 표적이 없으면 쿨다운 0 유지, 타이머는 발사 성공 시점부터 다시 시작
            if (target == null)
            {
                continue;
            }

            Vector3 direction = WeaponTargeting.GetAimDirection(_muzzle.position, target.AimPoint);
            _pool.Fire(new ProjectileSpawnContext(_muzzle.position, direction, definition.ProjectileSpeed,
                definition.ProjectileDamage, definition.ProjectileMaxDistance, definition.ProjectileMaxLifetime,
                definition.DisableOutsideGameCamera, MAX_UNIQUE_TARGETS_HIT));
            weapon.ResetShotCooldown();
        }
    }
}
