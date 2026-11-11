# 무기 시스템 (Weapon)

> 근거 : `Assets/Scripts/Weapon/*`, `Assets/Scripts/Player/*`, `Assets/Scripts/Enemy/TempCubeEnemy.cs`, `Assets/Tests/Editor/*`, 관련 Prefab·Scene·에셋 파일
>
> 설계 원본 : `Docs/Spec/Weapon/WeaponSystem.md` (이 문서는 현재 구현 상태만 기술한다)

## 목적 / 기능 범위

- 장착 무기마다 독립적으로 표적을 탐색하고, 쿨다운이 끝나면 비유도 투사체를 발사한다.
- 주무기 하나의 표적 방향으로 캐릭터 모델(몸통)을 회전시킨다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #5의 W01(무기 설정 SO·장착 인스턴스 분리), W02(최근접 적 탐색·조준 방향), W03(공통 투사체·풀·피격 판정·화면 경계 무효화·임시 Cube 적), W04(무기별 쿨다운·발사 루프), W05(샷건 기본 발사 : 부채꼴 다중 탄·탄 프리팹별 풀), W07 일부(주무기 몸통 회전) |
| 제외 (후속) | W06, W07 나머지 (예 : 주무기 변경 이벤트, 부유 무기 배치) |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `WeaponDefinition` (ScriptableObject) | 무기 종류별 공유 설정 (`WeaponType`, 발사 간격, 탄창, 재장전, 탐지 거리, 탄속, 최대 비행 거리·생존 시간, 피해, 화면 밖 무효화 여부, 발사 1회당 탄 수 `_projectilesPerShot`(기본 1, Min 1), 부채꼴 전체 각도 `_spreadAngle`(도, Min 0), 탄 프리팹 `_projectilePrefab`). 읽기 전용 프로퍼티만 노출 | `Projectile`(탄 프리팹 참조) |
| `WeaponInstance` | 장착한 무기 하나의 런타임 상태 (`InstanceId`, `Level`, `IsEvolved`, `ShotCooldown`, `CurrentAmmo`, `IsReloading`, `CurrentTarget`). 쿨다운 감소(`Tick`)·재설정(`ResetShotCooldown`)·잔탄 소모(`ConsumeAmmo`). 탄창·재장전 데이터(`ConsumeAmmo`, `IsReloading`, `MagazineCapacity`, `ReloadDuration`)는 현재 발사 루프에서 호출·사용하지 않는다. `IsReloading`은 값을 쓰는 코드가 없어 항상 `false`다. 연결은 W06(미니건 연사·재장전)에서 할 예정이다 | `WeaponDefinition`, `ITargetable` |
| `WeaponLoadout` (MonoBehaviour) | 장착 목록과 주무기(`Primary`) 소유. `Awake`에서 고유 무기 장착, `Equip`/`Unequip`, 주무기 선정(`SelectPrimary`) | `WeaponDefinition`, `WeaponInstance` |
| `WeaponTargeting` (static) | 상태 없는 계산. `FindNearest`(XZ 반경 내 최근접 표적), `GetAimDirection`(XZ 정규화 방향), `GetFireDirection`(발사 중심 방향, 총구 기준이 0이면 플레이어 중심 기준), `GetSpreadDirection`(부채꼴 안 n번째 탄 방향) | `ITargetable` |
| `WeaponAttackController` (MonoBehaviour) | 매 프레임 장착 무기별 쿨다운 감소 → 표적 탐색·`CurrentTarget` 기록 → 발사 | `WeaponLoadout`, `ProjectilePool`, `WeaponTargeting`, `TempCubeEnemy.ActiveEnemies`(임시 후보 공급) |
| `ProjectileSpawnContext` (readonly struct) | 발사 시점에 확정되는 값 묶음 (생성 위치, 방향, 탄속, 피해, 최대 거리·생존 시간, 화면 판정 여부, 고유 적중 한도) | 없음 |
| `ProjectilePool` (MonoBehaviour) | 탄 프리팹마다 `UnityEngine.Pool.ObjectPool<Projectile>`을 따로 두고(`Dictionary<Projectile, ObjectPool<Projectile>>`) 탄 생성·재사용. 프리팹의 풀은 그 프리팹의 첫 `Fire(prefab, context)` 호출 시 생성한다. 반환용 `Release` 메서드는 없다 | `Projectile`, 출력 카메라 |
| `Projectile` (MonoBehaviour) | 고정 방향 직진, 구간 SphereCast 적중·피해 적용, 화면 밖·최대 거리·최대 생존 시간 초과 시 `Launch`로 받은 `IObjectPool<Projectile>`(자신을 꺼낸 프리팹별 풀)에 직접 반환 | `ProjectileHitDetector`, `IObjectPool<Projectile>`, `IDamageable`, `Camera` |
| `ProjectileHitDetector` | 구간 SphereCast(최대 16개) 후 거리순 정렬, 같은 적 중복 제외, 죽은 적 제외, 고유 적중 한도 판정 | `IDamageable`, `Physics` |
| `IDamageable` | 피해 수신 계약 (`IsAlive`, `TakeDamage`) | 없음 |
| `ITargetable` | 탐색 대상 계약 (`AimPoint`, `IsTargetable`, `SpawnId`) | 없음 |
| `PlayerAimController` (MonoBehaviour) | `LateUpdate`에서 모델(`_visualRoot`)만 회전. 주무기 표적 → 이동 방향 → 정지 시 유지 | `WeaponLoadout`, `WeaponTargeting`, `CharacterController`(`RequireComponent`, `Awake` 캐싱) |
| `TempCubeEnemy` (MonoBehaviour, 임시) | 무기 검증용 적. `ITargetable`·`IDamageable` 구현, static 목록 `ActiveEnemies`에 활성화 시 등록·비활성화 시 제거, 피격 시 체력 감소·빨간색 표시, `_hitFlashDuration`(기본 1초, Min 0) 뒤 원래 색 복귀(재피격 시 남은 시간을 다시 채움, `Update`에서 `Time.deltaTime`으로 감소). 사망 시 노란색 유지. `OnEnable`에서 `SpawnId`·체력·색·남은 표시 시간 초기화. 사망 시 파괴·반환하지 않음 | 없음 |
| `PlayerMovement` | 이동만 담당. 이번 작업에서 회전 코드가 제거되었고 회전은 `PlayerAimController`가 맡는다. Move 액션의 `Enable`/`Disable`을 이 컴포넌트가 직접 맡는다 (`OnEnable`/`OnDisable`, 이 액션을 쓰는 컴포넌트가 하나뿐이라서) | `CharacterController`, `InputActionReference`(`_moveAction`) |

## API / 이벤트 / 데이터 계약

- 이벤트 : 없음. 주무기 변경 이벤트는 구현되지 않았다 (W07 나머지).

### 주무기 (`WeaponLoadout.Primary`)

- `Equip` : 주무기가 `null`일 때만 새로 장착한 무기를 주무기로 지정한다. 이미 주무기가 있으면 유지한다.
- `Unequip` : 해제한 무기가 주무기였을 때만 `SelectPrimary`로 남은 무기 중에서 다시 선정한다. 목록이 비면 `null`.
- `SelectPrimary` 우선순위 (앞 조건이 같을 때만 다음 조건 비교)
    1. 진화(`IsEvolved`) 무기
    2. 고유 무기 (`Definition == _signatureWeapon`)
    3. 높은 `Level`
    4. 낮은 `InstanceId` (먼저 장착)
- 수동 지정 API는 구현되지 않았다.

### 표적 (`WeaponInstance.CurrentTarget`)

| 구분 | 위치 | 내용 |
|---|---|---|
| 쓰기 | `WeaponAttackController.Update` | 무기마다 쿨다운과 관계없이 매 프레임 `FindNearest` 결과를 기록 (없으면 `null`) |
| 읽기 | `PlayerAimController.LateUpdate` | 주무기의 `CurrentTarget`만 읽는다. `LateUpdate`라서 같은 프레임 `Update`의 탐색 결과를 읽는다 |

### 탐색 (`WeaponTargeting.FindNearest`)

- 원점 : 플레이어 루트 위치 (`_playerRoot`). 거리 : `AimPoint`와의 XZ 평면 거리 (높이 무시).
- `IsTargetable == false`인 후보는 제외한다. 거리가 반경과 같으면 포함한다.
- 거리가 같으면 `SpawnId`가 낮은 표적을 선택한다 (후보 목록 순서와 무관).

### 발사

- 발사 순간 판정 : 같은 프레임에 탐색한 표적으로 발사한다. 탄은 이후 유도되지 않는다.
- 몸통 회전 완료를 기다리지 않는다.
- 쿨다운이 0이어도 표적이 없으면 발사하지 않고 쿨다운 0을 유지한다.
- 중심 방향 : `GetFireDirection(_muzzle.position, _playerRoot.position, target.AimPoint)`
    1. 총구→표적 `AimPoint`의 XZ 정규화 방향을 쓴다.
    2. 1이 영벡터(총구와 표적의 XZ 위치가 겹침)이면 플레이어 루트→표적 방향을 쓴다.
    3. 2도 영벡터이면 영벡터를 반환한다. 이 방향으로 만든 탄은 모두 `Projectile.Launch`에서 취소된다 (아래 `투사체 적중·반환` 참고).
- 부채꼴 : 발사 1회에 `ProjectilesPerShot`발을 만들고, 탄마다 `GetSpreadDirection(중심 방향, SpreadAngle, ProjectilesPerShot, 순번)`으로 방향을 정한다.
    - 각도는 `-SpreadAngle/2`부터 `+SpreadAngle/2`까지 양 끝을 포함한 균등 간격이다 (Y축 회전). 랜덤 요소는 없다.
    - 1발이면 중심 방향 그대로다. `SpreadAngle`이 0이면 모든 탄이 중심 방향이다.
    - `WeaponType`으로 분기하지 않는다. 권총은 1발·0°라서 기존과 같은 단발로 동작한다.
    - 모든 탄은 같은 위치(`_muzzle.position`)에서 같은 탄속·피해·거리·수명 값으로 생성된다.
- 탄 생성 : 탄마다 `_pool.Fire(definition.ProjectilePrefab, context)`를 호출한다.
- 쿨다운 : 탄 수와 관계없이 발사 1회당 `ResetShotCooldown()`을 1번 호출해 `FireInterval`로 재설정한다. 탄이 실제로 나갔는지는 확인하지 않으므로, 위 3번으로 탄이 모두 취소돼도 쿨다운은 재설정된다.
- 피해·한도 : 펠릿(탄)마다 개별 투사체라서 피해를 각각 적용한다. 고유 적중 한도는 `WeaponAttackController.MAX_UNIQUE_TARGETS_HIT = 1`이며 탄마다 첫 적 적중 후 반환된다. 코드 주석상 미니건 진화 시 `WeaponDefinition` 필드로 이전 예정이다.

### 투사체 적중·반환

- 피해 대상 판정 : 맞은 Collider의 부모 방향으로 `IDamageable`을 찾는다 (`GetComponentInParent`).
- 죽은 적 탄 통과 : `ProjectileHitDetector.TryRecord`가 `IsAlive == false`인 대상을 기록하지 않으므로 피해·한도 소모 없이 탄이 지나간다.
- 같은 적 중복 제외 : 발사마다 비우는 `HashSet<IDamageable>`에 기록된 적은 다시 맞지 않는다 (참조 비교).
- 화면 밖 무효화 (`RequireCameraVisibility == true`일 때)
    - 적중 지점이 출력 카메라 뷰포트 밖이면 피해 없이 즉시 반환한다.
    - 이동 후 위치가 뷰포트 밖이면 반환한다.
    - 뷰포트 판정 : `z > 0`이고 `x`, `y`가 0 이상 1 이하 (경계 포함).
- 그 밖의 반환 조건 : 누적 이동 거리 > `MaxDistance`, 경과 시간 > `MaxLifetime`, 고유 적중 한도 도달.
- `MaxDistance`를 넘는 구간의 적은 맞지 않도록 SphereCast 길이를 남은 거리로 제한한다.
- XZ 성분이 0인 방향으로 `Launch`하면 경고 로그를 남기고 즉시 반환한다 (탄마다 1번씩 기록).
- 충돌 마스크 : `PFB_PistolBullet`, `PFB_ShotgunPellet`의 `_hitMask`는 `Enemy` 레이어(6)만 검사한다.

## 호출 순서 또는 데이터 흐름

```
[Awake]
WeaponLoadout.Awake → Equip(_signatureWeapon) → Primary 지정
PlayerAimController.Awake → CharacterController 캐싱
(ProjectilePool은 Awake에서 풀을 만들지 않는다. 프리팹별 풀은 첫 Fire 시 생성)

[매 프레임 Update] WeaponAttackController
for 장착 무기마다
  ├─ weapon.Tick(Time.deltaTime)                   쿨다운 감소
  ├─ FindNearest(플레이어 루트, DetectionRange, TempCubeEnemy.ActiveEnemies)
  ├─ weapon.CurrentTarget = 탐색 결과
  ├─ 쿨다운 > 0 → 다음 무기
  ├─ 표적 없음 → 다음 무기 (쿨다운 0 유지)
  ├─ 중심 방향 = GetFireDirection(총구, 플레이어 루트, 표적 AimPoint)
  ├─ for j = 0 .. ProjectilesPerShot - 1
  │    ├─ 방향 = GetSpreadDirection(중심 방향, SpreadAngle, ProjectilesPerShot, j)
  │    └─ ProjectilePool.Fire(ProjectilePrefab, SpawnContext)
  │         → (프리팹의 풀이 없으면 생성) → pool.Get() → Projectile.Launch(context, pool, 카메라)
  │           (XZ 방향 0이면 경고 후 반환)
  └─ weapon.ResetShotCooldown()                    발사 1회당 1번, 항상 실행

[매 프레임 Update] Projectile (활성 탄마다)
  ├─ 이번 이동 구간 SphereCast → 가까운 순으로 적중 처리 (화면 밖 적중이면 반환)
  ├─ 한도 도달 → _pool.Release(this)   (Launch로 받은 프리팹별 풀)
  └─ 이동 → 화면 밖·최대 거리·최대 생존 시간 초과 → _pool.Release(this)

[매 프레임 LateUpdate] PlayerAimController
  주무기 CurrentTarget 있음 → 표적 AimPoint 방향 (XZ)
  없으면 → CharacterController.velocity의 XZ 성분 (이동 방향)
  둘 다 0 → 회전하지 않음 (현재 방향 유지)
  → _visualRoot를 RotateTowards(_turnSpeed × Time.deltaTime)로 회전
```

- 주무기가 없을 때(`Primary == null`)도 이동 방향 회전은 동작한다.
- 회전 대상은 모델(`_visualRoot`)뿐이며 플레이어 루트는 회전하지 않는다.

## Scene·Prefab·Inspector 사용 방법

### `Assets/Prefabs/Player/PFB_Player.prefab`

```
PFB_Player                    (Layer : Player, CharacterController, PlayerMovement, PlayerAimController)
├─ CowboyDefault              (모델 Prefab 인스턴스, 회전 대상)
│  └─ … / hand_r / PistolDefault
│                          └─ MuzzlePoint   (추가 오브젝트, 로컬 위치 (0, 0.15, 0.47))
└─ Weapon                     (WeaponLoadout, WeaponAttackController)
```

| 컴포넌트 | 필드 | 연결 값 |
|---|---|---|
| `PlayerAimController` (루트) | `_visualRoot` | `CowboyDefault` |
| | `_loadout` | `Weapon`의 `WeaponLoadout` |
| | `_turnSpeed` | 720 (초당 각도) |
| `WeaponLoadout` (Weapon) | `_signatureWeapon` | `SO_Pistol` |
| `WeaponAttackController` (Weapon) | `_loadout` | `Weapon`의 `WeaponLoadout` |
| | `_pool` | Prefab에서는 비어 있음. `GameScene`의 Prefab 인스턴스 오버라이드로 `Managers/ProjectilePool` 연결 |
| | `_playerRoot` | `PFB_Player` 루트 |
| | `_muzzle` | `MuzzlePoint` |

- `CowboyDefault` 모델 Prefab에서 `PistolDefault`는 기본 비활성이고 `HuntingRifleDefault`는 기본 활성이다. `PFB_Player`는 모델 내부 오브젝트 2개의 활성 상태를 오버라이드(하나 활성, 하나 비활성)하는데, 이것이 각각 `PistolDefault` 활성·`HuntingRifleDefault` 비활성인지는 fileID 대응을 확인하지 못했다 (미검증, Unity 에디터에서 확인).
- `GameScene`의 `PFB_Player` 인스턴스는 모델 내부 오브젝트 2개를 추가로 비활성 오버라이드한다. 대상 오브젝트 이름은 확인하지 못했다 (미검증).

### `Assets/Scenes/GameScene.unity`

| 오브젝트 | 컴포넌트 | 필드 값 |
|---|---|---|
| `Managers/ProjectilePool` | `ProjectilePool` | `_outputCamera` = `MainCamera`의 Camera, `_projectileParent` = `Gameplay/Projectiles`, `_defaultCapacity` = 64, `_maxSize` = 512 (두 값은 프리팹별 풀마다 각각 적용) |

- 탄 프리팹은 `ProjectilePool`이 아닌 무기 SO의 `_projectilePrefab`에 연결한다.

### `Assets/Prefabs/Weapon/PFB_PistolBullet.prefab`

- 루트 : `Projectile` (`_hitRadius` = 0.1, `_hitMask` = Enemy).
- 자식 : `SM_Bullet_05` (표시 전용, 로컬 스케일 5).

### `Assets/Prefabs/Weapon/PFB_ShotgunPellet.prefab`

- 루트 : `Projectile` (`_hitRadius` = 0.1, `_hitMask` = Enemy).
- 자식 : `SM_Bullet_09` (표시 전용, 로컬 스케일 5, 로컬 회전 X 90°).

### `Assets/Data/Weapon/SO_Pistol.asset` (임시값)

| 필드 | 값 |
|---|---|
| `_weaponType` | Pistol |
| `_fireInterval` | 1.5 |
| `_magazineCapacity` / `_reloadDuration` | 0 / 0 (탄창 없음) |
| `_detectionRange` | 15 |
| `_projectileSpeed` | 15 |
| `_projectileMaxDistance` | 40 |
| `_projectileMaxLifetime` | 5 |
| `_projectileDamage` | 10 |
| `_disableOutsideGameCamera` | true |
| `_projectilesPerShot` | 1 |
| `_spreadAngle` | 0 |
| `_projectilePrefab` | `PFB_PistolBullet` |

- 탐지 거리·탄속·비행 거리·생존 시간·피해는 밸런스 미확정 임시값이다 (`WeaponSystem.md` §11).

### `Assets/Data/Weapon/SO_Shotgun.asset` (임시값)

| 필드 | 값 |
|---|---|
| `_weaponType` | Shotgun |
| `_fireInterval` | 2.5 |
| `_magazineCapacity` / `_reloadDuration` | 0 / 0 (탄창 없음) |
| `_detectionRange` | 7 |
| `_projectileSpeed` | 25 |
| `_projectileMaxDistance` | 8 |
| `_projectileMaxLifetime` | 1 |
| `_projectileDamage` | 5 (펠릿 1발당) |
| `_disableOutsideGameCamera` | false |
| `_projectilesPerShot` | 5 |
| `_spreadAngle` | 45 |
| `_projectilePrefab` | `PFB_ShotgunPellet` |

- `_fireInterval` 2.5와 `_disableOutsideGameCamera` false는 `WeaponSystem.md` §11 표기와 같다. 그 밖의 값은 밸런스 미확정 임시값이다 (§11 "실험 후 결정").
- `SO_Shotgun`은 어느 Prefab·Scene에도 기본 장착되어 있지 않다. 고유 무기(`_signatureWeapon`)는 `SO_Pistol`이다.

## 예외·주의사항·검증 방법

### 주의사항·후속 작업

| # | 내용 | 후속 시점 |
|---|---|---|
| a | 장착 무기 전체가 `WeaponAttackController._muzzle` 하나를 함께 쓴다. 무기별 총구 위치가 없다. 샷건도 손 권총(`PistolDefault`)의 `MuzzlePoint`에서 발사된다 | W06·W07 (부유 무기 배치) |
| b | `CurrentTarget`은 다음 `Update`에서 덮어쓸 때까지 이전 값을 유지한다. `PlayerAimController`는 `ITargetable` 타입(interface)으로 `null`을 비교하므로 Unity의 파괴된 오브젝트 판정(`UnityEngine.Object`의 `==`)이 적용되지 않는다. 표적이 파괴된 뒤 읽으면 `AimPoint` 접근에서 예외가 날 수 있다 (재현 미검증) | 적 사망·풀 반환 구현 시 |
| c | 무기마다 매 프레임 후보 전체를 탐색한다. 간격 탐색·이벤트 방식은 측정 근거가 없어 채택하지 않았다 | 실제 적 연동 시 Profiler 측정 |
| d | 고유 무기를 `WeaponLoadout._signatureWeapon`에 카우보이 권총으로 임시 지정 (코드의 `shortcut` 주석) | 캐릭터 데이터 추가 시 이전 |
| e | `WeaponAttackController`, `Projectile`, `PlayerAimController`, `TempCubeEnemy`가 `Time.deltaTime`을 직접 쓴다. 일시정지와 연동되어 있지 않다 | GameFlow(GameState) 연동 시 교체 |
| f | 손 권총 `MuzzlePoint` 위치 (0, 0.15, 0.47)는 애니메이션이 없는 상태 기준이다. W05 Play 모드에서 애니메이션 재생 중 `MuzzlePoint`가 움직여 탄이 가끔 빗나가는 것을 사용자가 관찰했다 (발사 로직은 정상 동작) | 애니메이션(idle/running/피격) 적용 후 Play 모드로 조정 |
| g | `TempCubeEnemy.TakeDamage`는 `IsAlive`가 아닌 `IsTargetable`로 사망 여부를 검사한다. 현재는 `IsTargetable => IsAlive`라 결과가 같지만, 두 값의 의미가 달라지면 결과도 달라진다 | 실제 적 구현 시 |
| h | W03 이월 항목 (아래 표) | 각 항목 참고 |
| i | 펠릿마다 개별 피해라서 가까운 적 하나에 펠릿이 여러 발 맞으면 피해가 그만큼 누적된다 (현재 임시값 기준 최대 5발 × 5). 근접 피해 상한은 구현되지 않았다 | W08에서 검토 |
| j | `_spreadAngle`이 360이면 양 끝 탄의 각도가 -180°·+180°라서 같은 방향으로 겹친다 | 밸런스 조정 시 |
| k | `PFB_ShotgunPellet`의 탄 외형(`SM_Bullet_09`, 스케일 5)은 Play 확인 후 다시 검토하기로 했다 | Play 확인 후 재검토 |
| l | 손 무기 외형은 장착 무기에 따라 자동으로 바뀌지 않는다. W05 Play 확인은 손 무기를 `ShotgunDefault`로 수동 전환해 진행했다 | W07 |
| m | 탄은 총구 위치(`_muzzle.position`)에서 생성되고, 중심 방향은 총구→표적 `AimPoint`의 XZ 방향이다 (코드 확인). 플레이어 몸과 총구 사이에 낀 적이 맞는지는 확인하지 않았다 (미검증) | 실제 적 연동 시 사용자 Play 검증 후 AI agent Play 모드 검증으로 2차 확인 |
| n | `PlayerAimController.GetLookDirection`은 여전히 `GetAimDirection(_visualRoot.position, AimPoint)`를 쓴다. 표적과 `_visualRoot`의 XZ 위치가 겹치면 영벡터가 되어 `LateUpdate`에서 회전하지 않는다 (이동 방향으로 대체하지 않음). 겹침 시 기대 동작은 정해지지 않았다 | 미정 |
| o | SO의 `_projectilePrefab`이 비어 있으면(`null`) 그 무기의 첫 발사 때 `Dictionary`의 `null` 키 예외가 난다. `null` 검사는 없다 (의도) | 무기 SO 작성 시 연결 필수 |
| p | `ProjectilePool`의 `_defaultCapacity`·`_maxSize`는 프리팹별 풀마다 각각 적용된다. 전체 탄 상한은 `_maxSize` × 프리팹 수다 | 참고 |
| q | `TempCubeEnemy._hitFlashDuration`이 0이면 피격 색(빨강)이 원래 색으로 돌아오지 않는다. 임시 도구라 그대로 둔다 (사용자 결정) | 실제 적 구현 시 임시 적과 함께 정리 |

### W03 이월 항목

| 항목 | 코드에서 확인한 사실 |
|---|---|
| 설계서 §6.2 `ProjectileSpawnContext` 개념 필드와 차이 | 실제 필드는 `SpawnPosition`, `Direction`, `Speed`, `Damage`, `MaxDistance`, `MaxLifetime`, `RequireCameraVisibility`, `MaxUniqueTargetsHit` 8개다. 설계서의 `owner`, `weaponInstanceId`, `knockbackData`, `shotId`는 없다 |
| 설계서 §7 `shotId` 대신 `_isActive` | `Projectile.Despawn`은 `_isActive`가 false면 반환하지 않아 중복 `Release`를 막는다. `shotId`는 사용하지 않는다 |
| `Direction` 주석과 실제 동작 불일치 | `ProjectileSpawnContext.Direction` 주석은 "XZ 평면 정규화 이동 방향"이지만, `Projectile.Launch`는 비정규 입력을 허용하고 `TryGetPlanarDirection`으로 직접 XZ 투영·정규화한다. 현재 호출부(`WeaponAttackController`)는 `GetFireDirection`으로 중심 방향을 구하고, 탄마다 `GetSpreadDirection` 결과를 `Direction`으로 넘긴다. 총구→표적과 플레이어 중심→표적의 XZ 성분이 모두 0이면 `GetFireDirection`이 영벡터를 반환하고, `GetSpreadDirection`도 영벡터를 그대로 반환해 `Direction`에 영벡터가 들어간다. 이때 `Projectile.Launch`는 경고 로그를 남기고 탄을 바로 반환한다 |
| 적 풀링 시 `HashSet` 참조 기준 중복 판정 위험 | `ProjectileHitDetector`는 `HashSet<IDamageable>`에 참조로 기록한다. 탄이 비행하는 동안 같은 적 오브젝트가 풀에서 재사용되면 새 적이어도 같은 참조라 맞지 않는다 (재현 미검증) |
| SphereCast 시작점이 이미 Collider와 겹친 경우 | `ProjectileHitDetector.Cast`는 `Physics.SphereCastNonAlloc`을 이동 구간 시작점에서 호출한다. 시작점이 이미 Collider와 겹쳐 있어도 `SphereCastNonAlloc`은 그 Collider를 distance 0으로 검출함을 실제 Scene에서 실측했다 (W05 메인 세션 확인). 총구가 적과 겹친 상태의 피격도 사용자 Play 모드에서 정상 확인했다 |

### 기타 예외 처리

- 표적 후보 목록이 비면 `FindNearest`는 `null`을 반환한다. 후보 목록 자체가 `null`이면 안 된다 (공급하는 쪽 책임).
- `ObjectPool`이 비면 새로 생성한다. `_maxSize`(512)를 넘겨 반환된 탄은 파괴한다.
- `TempCubeEnemy`는 사망 후에도 활성 상태로 남아 `ActiveEnemies`에 있다. `IsTargetable == false`이므로 탐색에서 제외되고, `IsAlive == false`이므로 탄이 통과한다.

### 검증 방법

- EditMode 테스트 (`Assets/Tests/Editor/`)
    - `WeaponInstanceTests` : 인스턴스 간 잔탄·쿨다운 독립, 같은 Definition 두 번 장착 시 ID 구분·해제
    - `WeaponTargetingTests` : 후방 적, 범위·공격 불가 제외, 경계 포함, 동일 거리 `SpawnId` 우선, 빈 목록, 무기별 독립 반경, 높이 무시, 조준 방향 평면·정규화, 총구가 표적과 겹칠 때 플레이어 중심 방향 사용(`GetFireDirection`), 부채꼴 1발 중앙·5발 양 끝 포함 균등 간격·퍼짐 0이면 모두 중앙·평면·정규화(`GetSpreadDirection`)
    - `ProjectileTests` : 뷰포트 경계, XZ 성분 0 방향, 비스듬한 방향 정규화, 중복 기록 1회, 죽은 적 미기록, 고유 적중 한도, 재사용 시 기록 초기화
    - `WeaponLoadoutTests` : 주무기 우선순위 4단계, 빈 목록, 장착 시 지정·유지, 해제 시 유지·재선정·`null`
- 결과 : 컴파일 오류 0, EditMode 41/41 통과 (W05 메인 세션 확인 기준, W05에서 `WeaponTargetingTests` 5개 추가).
- Play 모드 : 조준·발사·죽은 적 탄 통과·몸통 회전이 정상임을 사용자가 확인했다. 화면 밖 무효화, 풀 한도 초과, 항목 b의 위험 상황, 항목 h 중 적 풀링 시 `HashSet` 중복 판정 위험은 Play 모드로 확인하지 않았다 (미검증). 항목 h 중 SphereCast 시작점 겹침은 W05 메인 세션 실측과 사용자 Play 확인이 있다 (W03 이월 항목 표 참고).
- Play 모드 (W05) : 사용자가 다음을 확인했다. 권총 회귀 없음, 샷건 부채꼴 발사·짧은 거리 소멸·탄마다 첫 적 적중 후 반환, 총구가 적과 겹친 상태의 피격, 손 무기를 `ShotgunDefault`로 수동 전환한 상태의 발사. 애니메이션 중 탄이 가끔 빗나가는 현상은 항목 f 참고. 항목 m은 미검증이다.

## 관련 Issue/PR

- Issue #5 (무기 시스템 W01~W07)
- #3
