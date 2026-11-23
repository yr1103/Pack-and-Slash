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
    [SerializeField] private WeaponViewController _views; // 무기별 총구(탄 생성·조준 기준) 제공

    private void Update()
    {
        // shortcut: 표적 후보를 Enemy static 활성 목록에서 공급, EnemyManager/Spatial Hash 연동 시 교체
        IReadOnlyList<ITargetable> candidates = Enemy.ActiveEnemies;
        IReadOnlyList<WeaponInstance> weapons = _loadout.Weapons;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponInstance weapon = weapons[i];
            weapon.Tick(Time.deltaTime);

            WeaponDefinition definition = weapon.Definition;
            // 몸통 회전이 움직이는 표적을 따라가도록 쿨다운과 무관하게 매 프레임 갱신, 발사도 이 결과 사용
            ITargetable target = WeaponTargeting.FindNearest(_playerRoot.position, definition.DetectionRange, candidates);
            weapon.CurrentTarget = target;
            Transform muzzle = _views.GetMuzzle(weapon);

            // 넘긴 쿨다운만큼 한 프레임에 여러 발 발사, FPS와 무관하게 연사 속도 유지
            while (target != null && !weapon.IsReloading && weapon.ShotCooldown <= 0f)
            {
                float overshoot = -weapon.ShotCooldown; // 발사 예정 시각을 넘긴 시간(초)
                Vector3 centerDirection = WeaponTargeting.GetFireDirection(muzzle.position, _playerRoot.position, target.AimPoint);
                int projectileCount = definition.ProjectilesPerShot;
                for (int j = 0; j < projectileCount; j++)
                {
                    Vector3 direction = definition.RandomSpread
                        ? WeaponTargeting.GetRandomSpreadDirection(centerDirection, weapon.SpreadAngle, Random.value)
                        : WeaponTargeting.GetSpreadDirection(centerDirection, weapon.SpreadAngle, projectileCount, j);
                    // shortcut: 넘긴 시간만큼 탄을 앞당겨 생성해 탄 간격을 고르게 함, 긴 프레임 끊김 시 총구~생성 위치 사이 적은 판정 누락. 누락이 문제되면 생성 구간 SphereCast 추가
                    Vector3 spawnPosition = muzzle.position + direction * definition.ProjectileSpeed * overshoot;
                    _pool.Fire(definition.ProjectilePrefab, new ProjectileSpawnContext(spawnPosition, direction,
                        definition.ProjectileSpeed, weapon.Damage, definition.ProjectileMaxDistance,
                        definition.ProjectileMaxLifetime, definition.DisableOutsideGameCamera, MAX_UNIQUE_TARGETS_HIT));
                }

                // 탄 수와 무관하게 발사 1회 = 1발 소모·쿨다운 1회 (진화로 탄 수가 늘면 소모량 재결정)
                weapon.ConsumeAmmo();
                weapon.ScheduleNextShot();
            }

            // 표적이 없거나 재장전 중이면 넘긴 시간을 버림, 타이머는 발사 가능 시점부터 다시 시작 (재장전 후 몰아 쏘기 방지)
            if (target == null || weapon.IsReloading)
            {
                weapon.ClearOverdueShotCooldown();
            }
        }
    }
}
