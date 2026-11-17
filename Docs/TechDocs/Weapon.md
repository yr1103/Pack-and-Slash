# 무기 시스템 (Weapon)

> 근거 : `Assets/Scripts/Weapon/*`, `Assets/Scripts/Player/*`, `Assets/Scripts/Enemy/TempCubeEnemy.cs`, `Assets/Tests/Editor/*`, 관련 Prefab·Scene·에셋 파일
>
> 설계 원본 : `Docs/Spec/Weapon/WeaponSystem.md` (이 문서는 현재 구현 상태만 기술한다)

## 목적 / 기능 범위

- 장착 무기마다 독립적으로 표적을 탐색하고, 쿨다운이 끝나면 비유도 투사체를 발사한다.
- 주무기 하나의 표적 방향으로 캐릭터 모델(몸통)을 회전시킨다.
- 주무기는 손 무기로, 나머지 장착 무기는 플레이어 주변 원형 슬롯의 부유 총기로 표시한다. 탄은 무기마다 자기 총구(손 무기 또는 부유 총기)에서 생성된다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #5의 W01(무기 설정 SO·장착 인스턴스 분리), W02(최근접 적 탐색·조준 방향), W03(공통 투사체·풀·피격 판정·화면 경계 무효화·임시 Cube 적), W04(무기별 쿨다운·발사 루프, W07에서 앞당긴 주무기 몸통 회전), W05(샷건 기본 발사 : 부채꼴 다중 탄·탄 프리팹별 풀), W06(미니건 기본 연사·재장전·랜덤 퍼짐), W07(부유 총기, 원형 배치, 주무기 변경 이벤트, 손 무기·Animator 교체, 고유 무기 기준 생김새) |
| 제외 (후속) | 주무기 수동 지정, 2중 원 배치·총기 수 상한, Animator 파라미터(달리기·사격) 연동 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `WeaponDefinition` (ScriptableObject) | 무기 종류별 공유 설정 (`WeaponType`, 발사 간격 `_fireInterval`(기본 `DEFAULT_FIRE_INTERVAL` = 1, `Min(MIN_FIRE_INTERVAL)` = 0.01. 0 이하이면 한 프레임 연속 발사 루프가 끝나지 않으므로 하한을 둠), 탄창, 재장전, 탐지 거리, 탄속, 최대 비행 거리·생존 시간, 피해, 화면 밖 무효화 여부, 발사 1회당 탄 수 `_projectilesPerShot`(기본 1, Min 1), 부채꼴 전체 각도 `_spreadAngle`(도, Min 0), 랜덤 퍼짐 여부 `_randomSpread`(true = 탄마다 ±SpreadAngle/2 범위 랜덤 각도, false = 균등 간격 부채꼴), 탄 프리팹 `_projectilePrefab`, 주무기가 아닐 때 띄우는 부유 외형 프리팹 `_floatingViewPrefab`). 읽기 전용 프로퍼티만 노출 | `Projectile`(탄 프리팹 참조), `FloatingWeaponView`(부유 외형 프리팹 참조) |
| `WeaponInstance` | 장착한 무기 하나의 런타임 상태 (`InstanceId`, `Level`, `IsEvolved`, `ShotCooldown`, `CurrentAmmo`, `IsReloading`, `ReloadRemaining`, `CurrentTarget`). 쿨다운·재장전 시간 감소(`Tick`), 다음 발사 예약(`ScheduleNextShot` : `ShotCooldown += FireInterval`), 넘긴 시간 버리기(`ClearOverdueShotCooldown`), 잔탄 소모와 재장전 시작(`ConsumeAmmo`). 이벤트 `AmmoChanged`, `ReloadStarted`, `ReloadFinished` | `WeaponDefinition`, `ITargetable` |
| `WeaponLoadout` (MonoBehaviour) | 장착 목록과 주무기(`Primary`) 소유. `Awake`에서 고유 무기 장착, `Equip`/`Unequip`, 주무기 선정(`SelectPrimary`). 고유 무기 읽기 프로퍼티 `SignatureWeapon`. 이벤트 `PrimaryChanged`, `WeaponsChanged` | `WeaponDefinition`, `WeaponInstance` |
| `WeaponTargeting` (static) | 상태 없는 계산. `FindNearest`(XZ 반경 내 최근접 표적), `GetAimDirection`(XZ 정규화 방향), `GetFireDirection`(발사 중심 방향, 총구 기준이 0이면 플레이어 중심 기준), `GetSpreadDirection`(부채꼴 안 n번째 탄 방향), `GetRandomSpreadDirection(centerDirection, spreadAngle, randomRatio)`(부채꼴 안 랜덤 위치 탄 방향, 비율은 호출부가 공급) | `ITargetable` |
| `WeaponAttackController` (MonoBehaviour) | 매 프레임 장착 무기별 쿨다운 감소 → 표적 탐색·`CurrentTarget` 기록 → 발사. 총구는 `_views.GetMuzzle(weapon)`으로 무기마다 받는다 (W06까지의 단일 `_muzzle` 필드는 제거됨) | `WeaponLoadout`, `ProjectilePool`, `WeaponViewController`(`_views`, 무기별 총구), `WeaponTargeting`, `TempCubeEnemy.ActiveEnemies`(임시 후보 공급) |
| `WeaponViewController` (MonoBehaviour) | 무기 외형 표시. 주무기 종류의 손 무기만 켜고 Animator Controller 교체, 주무기가 아닌 장착 무기마다 부유 총기(`FloatingWeaponView`) 생성·파괴·원형 슬롯 이동·조준, 무기별 총구 제공(`GetMuzzle`), 시작 시 고유 무기 종류로 생김새(Body·Head) 선택. 슬롯 각도 계산 `GetSlotAngle`(static) | `WeaponLoadout`(이벤트 구독), `FloatingWeaponView`, `Animator`, `WeaponDefinition.FloatingViewPrefab` |
| `FloatingWeaponView` (MonoBehaviour) | 부유 총기 하나의 외형. 총구 `Muzzle` 노출, 슬롯 위치로 `Vector3.MoveTowards` 이동(`MoveTowardsSlot`), 표적 방향(XZ)으로 `Quaternion.RotateTowards` 회전(`TurnTowards`, 표적이 없거나 방향이 영벡터면 현재 방향 유지). 속도·시간은 호출부가 공급 | `WeaponTargeting`, `ITargetable` |
| `TempLoadoutTester` (MonoBehaviour, 임시) | 인벤토리 연동 전 검증 도구. `Start`에서 `_extraWeapons`를 순서대로 `Equip`, 컨텍스트 메뉴 `Unequip Primary`로 현재 주무기 해제 | `WeaponLoadout` |
| `ProjectileSpawnContext` (readonly struct) | 발사 시점에 확정되는 값 묶음 (생성 위치, 방향, 탄속, 피해, 최대 거리·생존 시간, 화면 판정 여부, 고유 적중 한도) | 없음 |
| `ProjectilePool` (MonoBehaviour) | 탄 프리팹마다 `UnityEngine.Pool.ObjectPool<Projectile>`을 따로 두고(`Dictionary<Projectile, ObjectPool<Projectile>>`) 탄 생성·재사용. 프리팹의 풀은 그 프리팹의 첫 `Fire(prefab, context)` 호출 시 생성한다. 반환용 `Release` 메서드는 없다 | `Projectile`, 출력 카메라 |
| `Projectile` (MonoBehaviour) | 고정 방향 직진, 구간 SphereCast 적중·피해 적용, 장애물(`_blockMask`) 적중·화면 밖·최대 거리·최대 생존 시간 초과 시 `Launch`로 받은 `IObjectPool<Projectile>`(자신을 꺼낸 프리팹별 풀)에 직접 반환 | `ProjectileHitDetector`, `IObjectPool<Projectile>`, `IDamageable`, `Camera` |
| `ProjectileHitDetector` | 구간 SphereCast(최대 16개, `_hitMask`와 `_blockMask`를 합친 마스크) 후 거리순 정렬, 장애물 판정(`IsObstacle`), 같은 적 중복 제외, 죽은 적 제외, 고유 적중 한도 판정 | `IDamageable`, `Physics` |
| `IDamageable` | 피해 수신 계약 (`IsAlive`, `TakeDamage`) | 없음 |
| `ITargetable` | 탐색 대상 계약 (`AimPoint`, `IsTargetable`, `SpawnId`) | 없음 |
| `PlayerAimController` (MonoBehaviour) | `LateUpdate`에서 모델(`_visualRoot`)만 회전. 주무기 표적 → 이동 방향 → 정지 시 유지 | `WeaponLoadout`, `WeaponTargeting`, `CharacterController`(`RequireComponent`, `Awake` 캐싱) |
| `TempCubeEnemy` (MonoBehaviour, 임시) | 무기 검증용 적. `ITargetable`·`IDamageable` 구현, static 목록 `ActiveEnemies`에 활성화 시 등록·비활성화 시 제거, 피격 시 체력 감소·빨간색 표시, `_hitFlashDuration`(기본 1초, Min 0) 뒤 원래 색 복귀(재피격 시 남은 시간을 다시 채움, `Update`에서 `Time.deltaTime`으로 감소). 사망 시 노란색 유지. `OnEnable`에서 `SpawnId`·체력·색·남은 표시 시간 초기화. 사망 시 파괴·반환하지 않음 | 없음 |
| `PlayerMovement` | 이동만 담당. 이번 작업에서 회전 코드가 제거되었고 회전은 `PlayerAimController`가 맡는다. Move 액션의 `Enable`/`Disable`을 이 컴포넌트가 직접 맡는다 (`OnEnable`/`OnDisable`, 이 액션을 쓰는 컴포넌트가 하나뿐이라서) | `CharacterController`, `InputActionReference`(`_moveAction`) |

## API / 이벤트 / 데이터 계약

### 이벤트 (`WeaponLoadout`)

모두 인자 없는 `Action`이다. 런타임 구독자는 `WeaponViewController`다 (`OnEnable` 구독, `OnDisable` 해제).

| 이벤트 | 발생 시점 |
|---|---|
| `PrimaryChanged` | `Primary` 값이 실제로 바뀐 직후. `Equip`에서 주무기가 `null`이라 새 무기를 지정했을 때, `Unequip`에서 해제한 무기가 주무기라 재선정했을 때 (`null`로 바뀐 경우 포함) |
| `WeaponsChanged` | `Equip` 성공 시, `Unequip`이 목록에서 제거했을 때 (`true` 반환). `Primary` 갱신 뒤에 발생한다 |

- 순서 : 한 호출에서 둘 다 발생하면 `PrimaryChanged` → `WeaponsChanged` 순이다.
- `Unequip`에 목록에 없는 ID를 넘기면(`false` 반환) 어느 이벤트도 발생하지 않는다.
- 주무기가 아닌 무기의 해제, 주무기가 이미 있을 때의 장착은 `WeaponsChanged`만 발생한다.
- `Awake`의 고유 무기 장착도 두 이벤트를 발생시키지만, 이 시점에 `WeaponViewController`가 구독했는지는 스크립트 실행 순서에 달려 있다 (실행 순서는 지정되지 않음, 미검증). 그래서 `WeaponViewController.Start`가 한 번 직접 반영한다 (아래 `호출 순서` 참고). 구독 후 이벤트로 이미 반영됐더라도 `Start`의 반영은 같은 결과를 다시 적용한다.

### 이벤트 (`WeaponInstance`)

모두 인자 없는 `Action`이다. `WeaponInstance` 이벤트를 구독하는 곳은 현재 테스트뿐이다.

| 이벤트 | 발생 시점 |
|---|---|
| `AmmoChanged` | `ConsumeAmmo`가 잔탄을 1 줄였을 때, 재장전 완료로 잔탄을 채웠을 때 |
| `ReloadStarted` | `ConsumeAmmo`로 잔탄이 0이 되어 재장전을 시작할 때 (같은 호출의 `AmmoChanged` 다음) |
| `ReloadFinished` | `Tick`에서 남은 재장전 시간이 0 이하가 되어 재장전이 끝날 때 (같은 호출의 `AmmoChanged` 다음) |

### 재장전

- 시작 : `ConsumeAmmo`로 잔탄이 0이 되면 즉시 시작한다 (`IsReloading = true`, `ReloadRemaining = ReloadDuration`). 잔탄이 남은 상태의 부분 재장전은 없다.
- 진행 : `Tick`에서 `ReloadRemaining`을 줄이고 0 이하가 되면 잔탄을 `MagazineCapacity`로 채운 뒤 종료한다. `WeaponAttackController`는 표적 탐색 전에 `Tick`을 호출하므로 표적 유무와 관계없이 진행된다.
- 탄창 없는 무기(`MagazineCapacity` <= 0) : `ConsumeAmmo`가 아무것도 하지 않아 재장전이 없다. `CurrentAmmo`는 생성자에서 대입한 `MagazineCapacity` 값(0 또는 음수)에서 바뀌지 않는다. 음수 용량을 막는 하한은 없다 (주의사항 y).
- 시간 : `Tick`에 호출부가 넘긴 `deltaTime`만 쓴다. `WeaponInstance`는 `Time`을 직접 읽지 않는다.
- 재장전 중에는 발사하지 않는다 (아래 `발사` 참고).

### 주무기 (`WeaponLoadout.Primary`)

- `Equip` : 주무기가 `null`일 때만 새로 장착한 무기를 주무기로 지정한다. 이미 주무기가 있으면 유지한다.
- `Unequip` : 해제한 무기가 주무기였을 때만 `SelectPrimary`로 남은 무기 중에서 다시 선정한다. 목록이 비면 `null`.
- `SelectPrimary` 우선순위 (앞 조건이 같을 때만 다음 조건 비교)
    1. 진화(`IsEvolved`) 무기
    2. 고유 무기 (`Definition == _signatureWeapon`)
    3. 높은 `Level`
    4. 낮은 `InstanceId` (먼저 장착)
- 수동 지정 API는 구현되지 않았다.

### 무기 외형 (`WeaponViewController`)

#### 손 무기·Animator 교체 (`PrimaryChanged`)

- `_handWeapons`에 등록한 항목만 켜고 끈다. 주무기의 `WeaponType`과 같은 항목의 `Weapon`을 켜고 나머지는 끈다. 켠 항목의 `AnimatorController`를 `_animator.runtimeAnimatorController`에 대입한다.
- 주무기가 `null`이면 등록된 손 무기를 모두 끄고, Animator Controller는 바꾸지 않는다 (직전 값 유지).
- 모델에 붙은 그 밖의 무기 오브젝트는 건드리지 않는다.
- 손 무기·Animator는 즉시 교체되고, 부유 총기는 아래 원형 배치 규칙에 따라 이동한다.

#### 원형 배치 (부유 총기)

- 대상 : 장착 목록 중 주무기가 아닌 무기마다 `FloatingWeaponView` 1개. 주무기는 슬롯 0(손)이고 부유 총기는 슬롯 1부터다.
- 슬롯 번호 : 장착 목록(`Weapons`) 순서대로 주무기를 건너뛰며 1, 2, … 를 부여한다.
- 슬롯 각도 : `GetSlotAngle(i, N) = i × 360 / N` (N = 장착 무기 수, 주무기 포함). 0°는 `_visualRoot`(모델)의 현재 Y 회전이다. 모델이 회전하면 슬롯도 따라 회전한다.
- 슬롯 위치 : `_playerRoot.position + (Y축으로 (_visualRoot Y + 슬롯 각도)만큼 회전한 +Z 방향) × _orbitRadius + 위쪽 × _orbitHeight`. 중심은 플레이어 루트이고 모델이 아니다.
- 이동 : 매 `LateUpdate`에 `Vector3.MoveTowards`로 최대 `_slotMoveSpeed × Time.deltaTime`만큼 슬롯을 향해 이동한다 (거리/초 방식, 보간 시간 방식이 아님).
- 새 뷰 : `SyncFloatingViews`에서 무기의 `FloatingViewPrefab`을 슬롯 위치에, 회전은 `_visualRoot.rotation`으로, 부모는 `WeaponViewController`가 붙은 오브젝트(`Weapon`)로 `Instantiate`한다. 생성 즉시 슬롯 위치에 있으므로 이동 없이 나타난다.
- 제거 : 장착 목록에 없거나 주무기가 된 무기(승계)의 뷰는 `Destroy`한다. 남은 부유 총기는 뷰를 유지한 채 새 슬롯으로 이동한다.
- 조준 : 매 `LateUpdate`에 각 부유 총기를 그 무기의 `CurrentTarget` 방향(XZ)으로 최대 `_floatingTurnSpeed × Time.deltaTime`도 회전한다. 표적이 없으면 현재 방향을 유지한다. 몸통 회전과는 독립이다.
- `SyncFloatingViews`는 `PrimaryChanged`(손 무기 반영 뒤)와 `WeaponsChanged` 양쪽에서 호출된다.

#### 무기별 총구 (`GetMuzzle(weapon)`)

- 주무기면 현재 켜진 손 무기 항목의 `Muzzle`, 아니면 그 무기 부유 총기의 `Muzzle`을 반환한다.
- 전달하는 무기는 현재 장착 중이어야 한다 (사용 조건은 주의사항 참고).

#### 생김새 (`ApplyAppearance`)

- `_loadout.SignatureWeapon.WeaponType`(고유 무기 종류)과 같은 `_appearances` 항목의 Body·Head를 켜고, 나머지 항목의 Body·Head를 끈다.
- `Start`에서 한 번만 실행한다. 주무기가 승계되어도 생김새는 바뀌지 않는다.

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
- 발사 조건 : 표적 있음, 재장전 중 아님, `ShotCooldown` <= 0. 조건이 맞는 동안 `while`로 반복 발사한다.
    - `ShotCooldown`은 `Tick`에서 음수까지 내려가고, 발사마다 `ScheduleNextShot`으로 `FireInterval`을 더한다. 넘긴 시간이 다음 발사로 이월되므로 한 프레임에 여러 번 발사할 수 있고, 연사 속도가 FPS와 무관하다.
    - 표적이 없거나 재장전 중이면 루프 뒤에 `ClearOverdueShotCooldown`으로 음수 쿨다운만 0으로 되돌린다 (양수는 유지). 다시 쏠 수 있게 된 뒤 넘긴 시간만큼 몰아 쏘지 않는다.
- 총구 : 무기마다 `_views.GetMuzzle(weapon)`으로 받는다. 주무기는 손 무기 총구, 나머지는 부유 총기 총구다 (`무기 외형` 절 참고).
- 중심 방향 : `GetFireDirection(muzzle.position, _playerRoot.position, target.AimPoint)`
    1. 총구→표적 `AimPoint`의 XZ 정규화 방향을 쓴다.
    2. 1이 영벡터(총구와 표적의 XZ 위치가 겹침)이면 플레이어 루트→표적 방향을 쓴다.
    3. 2도 영벡터이면 영벡터를 반환한다. 이 방향으로 만든 탄은 모두 `Projectile.Launch`에서 취소된다 (아래 `투사체 적중·반환` 참고).
- 탄 방향 : 발사 1회에 `ProjectilesPerShot`발을 만든다. `RandomSpread`가 true면 탄마다 `GetRandomSpreadDirection(중심 방향, SpreadAngle, Random.value)`, false면 `GetSpreadDirection(중심 방향, SpreadAngle, ProjectilesPerShot, 순번)`으로 방향을 정한다.
    - 균등 (`GetSpreadDirection`) : 각도는 `-SpreadAngle/2`부터 `+SpreadAngle/2`까지 양 끝을 포함한 균등 간격이다 (Y축 회전). 1발이면 중심 방향 그대로다.
    - 랜덤 (`GetRandomSpreadDirection`) : `randomRatio` 0 = `-SpreadAngle/2`, 1 = `+SpreadAngle/2` 사이를 `Mathf.Lerp`로 보간한 각도다 (범위 밖 비율은 끝으로 고정). 탄 수가 1발이어도 랜덤 각도가 적용된다.
    - `SpreadAngle`이 0이면 두 방식 모두 모든 탄이 중심 방향이다.
    - `WeaponType`으로 분기하지 않는다. 권총은 1발·0°·균등이라서 기존과 같은 단발로 동작한다.
    - 모든 탄은 같은 탄속·피해·거리·수명 값으로 생성된다.
- 생성 위치 : `총구 위치 + 탄 방향 × ProjectileSpeed × overshoot` (`overshoot = -ShotCooldown`, 발사 예정 시각을 넘긴 시간). 넘긴 시간만큼 탄을 앞당겨 생성해 연사 탄 간격을 고르게 한다. 넘긴 시간이 0이면 총구 위치다.
- 탄 생성 : 탄마다 `_pool.Fire(definition.ProjectilePrefab, context)`를 호출한다.
- 잔탄·쿨다운 : 탄 수와 관계없이 발사 1회당 `ConsumeAmmo()`와 `ScheduleNextShot()`을 각각 1번 호출한다. 탄이 실제로 나갔는지는 확인하지 않으므로, 위 3번으로 탄이 모두 취소돼도 잔탄 소모와 쿨다운 예약은 실행된다.
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
- 충돌 마스크 : `PFB_PistolBullet`, `PFB_ShotgunPellet`, `PFB_MinigunBullet`의 `_hitMask`는 `Enemy` 레이어(6)만 검사한다.
- 장애물 막힘 (`_blockMask`)
    - 동작 : `_blockMask`에 포함된 레이어의 Collider에 닿으면 피해 없이 즉시 반환한다. SphereCast 결과를 가까운 순으로 처리하므로 장애물 뒤의 적은 맞지 않는다. `_hitMask`와 겹치는 레이어는 장애물로 처리한다.
    - 현재 설정 : 탄 Prefab 3종 모두 `_blockMask` = Nothing(0)이다. 따라서 탄은 장애물·차단 Box·경계 벽을 모두 통과한다 (이슈 #7 결정. 맵 쪽 상세는 `Docs/TechDocs/Map.md`).
    - 막힘 기능(`ProjectileHitDetector.IsObstacle`과 `Projectile`의 분기)은 코드에 유지한다. 초반 설계에서 적용한 기능이고 코드 양이 크지 않으며, 맵·캐릭터·무기 추가 시 쓸 수 있어서다 (사용자 판단).
    - `Projectile.cs`의 `_blockMask` 주석은 "닿으면 탄이 막혀 반환되는 장애물 레이어"라고 설명하지만, 현재 Prefab에는 지정된 레이어가 없다.

## 호출 순서 또는 데이터 흐름

```
[Awake · OnEnable]   (오브젝트가 켜질 때 컴포넌트마다 Awake → OnEnable을 이어서 호출한다.
                      컴포넌트 사이 순서는 지정되지 않아, 아래 두 블록 중 어느 쪽이 먼저인지는 정해져 있지 않다)
WeaponLoadout.Awake → Equip(_signatureWeapon) → Primary 지정
PlayerAimController.Awake → CharacterController 캐싱
(ProjectilePool은 Awake에서 풀을 만들지 않는다. 프리팹별 풀은 첫 Fire 시 생성)

WeaponViewController.OnEnable
  _loadout.PrimaryChanged += OnPrimaryChanged
  _loadout.WeaponsChanged += SyncFloatingViews
  (OnDisable에서 해제)

[Start] WeaponViewController
  ├─ ApplyAppearance()                              고유 무기 종류로 Body·Head 선택 (한 번만)
  └─ OnPrimaryChanged()
       ├─ ApplyHandWeapon(Primary)                  손 무기 켜고 끔, Animator Controller 교체
       └─ SyncFloatingViews()                       부유 총기 생성·파괴
[Start] TempLoadoutTester (임시)
  _extraWeapons 순서대로 Equip → 이벤트로 WeaponViewController 갱신
  (두 Start의 실행 순서는 지정되지 않음. 어느 순서든 결과 상태는 같다)

[이벤트] WeaponLoadout.Equip / Unequip
  ├─ (Primary가 바뀐 경우) PrimaryChanged → ApplyHandWeapon → SyncFloatingViews
  └─ WeaponsChanged → SyncFloatingViews

[매 프레임 Update] WeaponAttackController
for 장착 무기마다
  ├─ weapon.Tick(Time.deltaTime)                   쿨다운 감소 (음수까지), 재장전 중이면 남은 시간 감소 → 0 이하면 잔탄 채움·재장전 종료
  ├─ FindNearest(플레이어 루트, DetectionRange, TempCubeEnemy.ActiveEnemies)
  ├─ weapon.CurrentTarget = 탐색 결과
  ├─ 총구 = _views.GetMuzzle(weapon)                주무기 = 손 무기 총구, 그 외 = 부유 총기 총구
  ├─ while (표적 있음 && 재장전 중 아님 && 쿨다운 <= 0)
  │    ├─ overshoot = -쿨다운
  │    ├─ 중심 방향 = GetFireDirection(총구.position, 플레이어 루트, 표적 AimPoint)
  │    ├─ for j = 0 .. ProjectilesPerShot - 1
  │    │    ├─ 방향 = RandomSpread ? GetRandomSpreadDirection(중심 방향, SpreadAngle, Random.value)
  │    │    │                     : GetSpreadDirection(중심 방향, SpreadAngle, ProjectilesPerShot, j)
  │    │    ├─ 생성 위치 = 총구 + 방향 × ProjectileSpeed × overshoot
  │    │    └─ ProjectilePool.Fire(ProjectilePrefab, SpawnContext)
  │    │         → (프리팹의 풀이 없으면 생성) → pool.Get() → Projectile.Launch(context, pool, 카메라)
  │    │           (XZ 방향 0이면 경고 후 반환)
  │    ├─ weapon.ConsumeAmmo()                     발사 1회당 1번 (탄창 무기만 소모, 0발이면 재장전 시작)
  │    └─ weapon.ScheduleNextShot()                쿨다운 += FireInterval
  └─ 표적 없음 || 재장전 중 → weapon.ClearOverdueShotCooldown()   음수 쿨다운만 0으로

[매 프레임 Update] Projectile (활성 탄마다)
  ├─ 이번 이동 구간 SphereCast → 가까운 순으로 적중 처리 (장애물(_blockMask)이면 반환, 화면 밖 적중이면 반환)
  ├─ 한도 도달 → _pool.Release(this)   (Launch로 받은 프리팹별 풀)
  └─ 이동 → 화면 밖·최대 거리·최대 생존 시간 초과 → _pool.Release(this)

[매 프레임 LateUpdate] PlayerAimController
  주무기 CurrentTarget 있음 → 표적 AimPoint 방향 (XZ)
  없으면 → CharacterController.velocity의 XZ 성분 (이동 방향)
  둘 다 0 → 회전하지 않음 (현재 방향 유지)
  → _visualRoot를 RotateTowards(_turnSpeed × Time.deltaTime)로 회전

[매 프레임 LateUpdate] WeaponViewController
for 장착 무기마다 (주무기 제외, 슬롯 번호 1부터)
  ├─ view.MoveTowardsSlot(슬롯 위치, _slotMoveSpeed × Time.deltaTime)
  └─ view.TurnTowards(weapon.CurrentTarget, _floatingTurnSpeed × Time.deltaTime)
(PlayerAimController.LateUpdate와의 실행 순서는 지정되지 않음, 주의사항 참고)
```

- 주무기가 없을 때(`Primary == null`)도 이동 방향 회전은 동작한다.
- 회전 대상은 모델(`_visualRoot`)뿐이며 플레이어 루트는 회전하지 않는다.

## Scene·Prefab·Inspector 사용 방법

### `Assets/Prefabs/Player/PFB_Player.prefab`

```
PFB_Player                    (Layer : Player, CharacterController, PlayerMovement, PlayerAimController)
├─ CowboyDefault              (모델 Prefab 인스턴스, 회전 대상, Animator)
│  ├─ … / PistolDefault
│  │      └─ MuzzlePoint      (추가 오브젝트, 로컬 위치 (0, 0.15, 0.47))
│  ├─ … / ShotgunDefault
│  │      └─ MuzzlePoint      (추가 오브젝트, 로컬 위치 (0, 0.031, 0.928))
│  └─ … / MiniGunDefault
│         └─ MuzzlePoint      (추가 오브젝트, 로컬 위치 (0, 0, 1.066))
└─ Weapon                     (WeaponLoadout, WeaponAttackController, WeaponViewController, TempLoadoutTester)
   └─ PFB_Floating…(Clone)    (런타임 생성 부유 총기, Prefab에는 없음)
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
| | `_views` | `Weapon`의 `WeaponViewController` |
| `WeaponViewController` (Weapon) | `_loadout` | `Weapon`의 `WeaponLoadout` |
| | `_playerRoot` | `PFB_Player` 루트 |
| | `_visualRoot` | `CowboyDefault` |
| | `_animator` | `CowboyDefault`의 Animator |
| | `_handWeapons` | 아래 표 (3항목) |
| | `_appearances` | 아래 표 (3항목) |
| | `_orbitRadius` | 1.5 (임시값) |
| | `_orbitHeight` | 1.2 (임시값) |
| | `_slotMoveSpeed` | 6 (초당 거리, 임시값) |
| | `_floatingTurnSpeed` | 720 (초당 각도, 임시값) |
| `TempLoadoutTester` (Weapon, 임시) | `_loadout` | `Weapon`의 `WeaponLoadout` |
| | `_extraWeapons` | `SO_Shotgun`, `SO_Minigun` (이 순서) |

`_handWeapons`

| `_weaponType` | `_weapon` | `_muzzle` | `_animatorController` |
|---|---|---|---|
| Pistol | `PistolDefault` | `PistolDefault/MuzzlePoint` | `PistolAnimator` |
| Shotgun | `ShotgunDefault` | `ShotgunDefault/MuzzlePoint` | `ShotGunAnimator` |
| Minigun | `MiniGunDefault` | `MiniGunDefault/MuzzlePoint` | `MiniGunAnimator` |

- Animator Controller 경로 : `Assets/Imports/Player/BattleRoyaleHeroesPBR/Animator/`.

`_appearances`

| `_signatureType` | `_body` | `_head` | 캐릭터 |
|---|---|---|---|
| Pistol | `Body10` | `Head10` | Cowboy |
| Shotgun | `Body06` | `Head06` | Chicken |
| Minigun | `Body08` | `Head08` | Salaryman |

- 캐릭터 이름은 구현 브리핑 기준이다. 오브젝트 이름은 `CowboyDefault` 모델 Prefab에서 확인했다.
- 손 무기(`PistolDefault`·`ShotgunDefault`·`MiniGunDefault`)의 활성 상태는 `WeaponViewController.Start`에서 주무기 기준으로 스크립트가 정한다. Prefab에 저장된 활성 상태는 런타임 결과에 영향을 주지 않는다. 참고로 `PFB_Player`는 `PistolDefault` 활성(1) 오버라이드를 갖고 있다 (`_handWeapons` Pistol 항목과 같은 fileID). `CowboyDefault` 모델 Prefab에서 `ShotgunDefault`·`MiniGunDefault`는 기본 비활성이다.
- `PFB_Player`의 또 다른 활성 상태 오버라이드(비활성 0) 1개가 `HuntingRifleDefault`인지는 fileID 대응을 확인하지 못했다 (미검증, Unity 에디터에서 확인). `HuntingRifleDefault`는 `_handWeapons`에 없으므로 스크립트가 제어하지 않는다.
- Body·Head 활성 상태도 `Start`에서 `_appearances`에 등록된 6개만 스크립트가 정한다.
- `GameScene`의 `PFB_Player` 인스턴스는 모델 내부 오브젝트 2개를 추가로 비활성 오버라이드한다. 대상 오브젝트 이름은 확인하지 못했다 (미검증).

### `Assets/Scenes/GameScene.unity`

| 오브젝트 | 컴포넌트 | 필드 값 |
|---|---|---|
| `Managers/ProjectilePool` | `ProjectilePool` | `_outputCamera` = `MainCamera`의 Camera, `_projectileParent` = `Gameplay/Projectiles`, `_defaultCapacity` = 64, `_maxSize` = 512 (두 값은 프리팹별 풀마다 각각 적용) |

- 탄 프리팹은 `ProjectilePool`이 아닌 무기 SO의 `_projectilePrefab`에 연결한다.

### `Assets/Prefabs/Weapon/PFB_PistolBullet.prefab`

- 루트 : `Projectile` (`_hitRadius` = 0.1, `_hitMask` = Enemy, `_blockMask` = Nothing).
- 자식 : `SM_Bullet_05` (표시 전용, 로컬 스케일 5).

### `Assets/Prefabs/Weapon/PFB_ShotgunPellet.prefab`

- 루트 : `Projectile` (`_hitRadius` = 0.1, `_hitMask` = Enemy, `_blockMask` = Nothing).
- 자식 : `SM_Bullet_09` (표시 전용, 로컬 스케일 5, 로컬 회전 X 90°).

### `Assets/Prefabs/Weapon/PFB_MinigunBullet.prefab`

- `PFB_PistolBullet`을 복제해 만들었다 (루트 GameObject·Transform·`Projectile`의 fileID가 같음).
- 루트 : `Projectile` (`_hitRadius` = 0.1, `_hitMask` = Enemy, `_blockMask` = Nothing).
- 자식 : `SM_Bullet_06` (표시 전용, 로컬 스케일 5, 로컬 회전 X 90°).

### 부유 총기 Prefab (`Assets/Prefabs/Weapon/PFB_FloatingPistol|Shotgun|Minigun.prefab`)

```
PFB_Floating…                 (FloatingWeaponView, _muzzle = MuzzlePoint)
├─ (무기 모델 Prefab 인스턴스) (표시 전용, 로컬 위치·회전 0)
└─ MuzzlePoint                (총구, 로컬 회전 0)
```

| Prefab | 모델 | `MuzzlePoint` 로컬 위치 |
|---|---|---|
| `PFB_FloatingPistol` | `PistolDefault` | (0, 0.15, 0.47) |
| `PFB_FloatingShotgun` | `ShotgunDefault` | (0, 0.031, 0.928) |
| `PFB_FloatingMinigun` | `MiniGunDefault` | (0, 0, 1.066) |

- `PFB_Player`의 손 무기와 같은 무기 모델 Prefab을 쓰고, `MuzzlePoint` 로컬 위치도 같은 값이다.
- 런타임에 `WeaponViewController`가 `Weapon` 아래에 생성하므로 이름에 `(Clone)` 접미사가 붙는다 (주의사항 참고).

### `Assets/Data/Weapon/SO_Pistol.asset` (임시값)

| 필드 | 값 |
|---|---|
| `_weaponType` | Pistol |
| `_fireInterval` | 1.5 |
| `_magazineCapacity` / `_reloadDuration` | 0 / 0 (탄창 없음) |
| `_detectionRange` | 15 |
| `_projectileSpeed` | 22 |
| `_projectileMaxDistance` | 40 |
| `_projectileMaxLifetime` | 5 |
| `_projectileDamage` | 15 |
| `_disableOutsideGameCamera` | true |
| `_projectilesPerShot` | 1 |
| `_spreadAngle` | 0 |
| `_randomSpread` | false |
| `_projectilePrefab` | `PFB_PistolBullet` |
| `_floatingViewPrefab` | `PFB_FloatingPistol` |

- 탐지 거리·탄속·비행 거리·생존 시간·피해는 밸런스 미확정 임시값이다 (`WeaponSystem.md` §11).

### `Assets/Data/Weapon/SO_Shotgun.asset` (임시값)

| 필드 | 값 |
|---|---|
| `_weaponType` | Shotgun |
| `_fireInterval` | 2.5 |
| `_magazineCapacity` / `_reloadDuration` | 0 / 0 (탄창 없음) |
| `_detectionRange` | 9 |
| `_projectileSpeed` | 30 |
| `_projectileMaxDistance` | 10 |
| `_projectileMaxLifetime` | 1 |
| `_projectileDamage` | 5 (펠릿 1발당) |
| `_disableOutsideGameCamera` | false |
| `_projectilesPerShot` | 5 |
| `_spreadAngle` | 35 |
| `_randomSpread` | false |
| `_projectilePrefab` | `PFB_ShotgunPellet` |
| `_floatingViewPrefab` | `PFB_FloatingShotgun` |

- `_fireInterval` 2.5와 `_disableOutsideGameCamera` false는 `WeaponSystem.md` §11 표기와 같다. 그 밖의 값은 밸런스 미확정 임시값이다 (§11 "실험 후 결정").
- `SO_Shotgun`은 어느 Prefab·Scene에도 기본 장착되어 있지 않다. 고유 무기(`_signatureWeapon`)는 `SO_Pistol`이다.

### `Assets/Data/Weapon/SO_Minigun.asset` (임시값)

| 필드 | 값 |
|---|---|
| `_weaponType` | Minigun |
| `_fireInterval` | 0.15 |
| `_magazineCapacity` / `_reloadDuration` | 150 / 5 |
| `_detectionRange` | 20 |
| `_projectileSpeed` | 20 |
| `_projectileMaxDistance` | 40 |
| `_projectileMaxLifetime` | 5 |
| `_projectileDamage` | 3 |
| `_disableOutsideGameCamera` | true |
| `_projectilesPerShot` | 1 |
| `_spreadAngle` | 18 |
| `_randomSpread` | true |
| `_projectilePrefab` | `PFB_MinigunBullet` |
| `_floatingViewPrefab` | `PFB_FloatingMinigun` |

- `_reloadDuration` 5와 `_disableOutsideGameCamera` true는 `WeaponSystem.md` §11 표기와 같다. §11의 `magazineCapacity`는 100발이고 이 SO는 150이다. 그 밖의 값은 밸런스 미확정 임시값이다 (§11 "실험 후 결정").

## 예외·주의사항·검증 방법

### 주의사항·후속 작업

| # | 내용 | 후속 시점 |
|---|---|---|
| a | (해결) 장착 무기 전체가 `WeaponAttackController._muzzle` 하나를 함께 쓰던 문제. W07에서 `_muzzle`을 제거하고 `WeaponViewController.GetMuzzle(weapon)`으로 무기별 총구(손 무기 또는 부유 총기)를 쓴다 | 해결 (W07) |
| b | `CurrentTarget`은 다음 `Update`에서 덮어쓸 때까지 이전 값을 유지한다. `PlayerAimController`는 `ITargetable` 타입(interface)으로 `null`을 비교하므로 Unity의 파괴된 오브젝트 판정(`UnityEngine.Object`의 `==`)이 적용되지 않는다. 표적이 파괴된 뒤 읽으면 `AimPoint` 접근에서 예외가 날 수 있다 (재현 미검증) | 적 사망·풀 반환 구현 시 |
| c | 무기마다 매 프레임 후보 전체를 탐색한다. 간격 탐색·이벤트 방식은 측정 근거가 없어 채택하지 않았다 | 실제 적 연동 시 Profiler 측정 |
| d | 고유 무기를 `WeaponLoadout._signatureWeapon`에 카우보이 권총으로 임시 지정 (코드의 `shortcut` 주석) | 캐릭터 데이터 추가 시 이전 |
| e | `WeaponAttackController`, `Projectile`, `PlayerAimController`, `TempCubeEnemy`가 `Time.deltaTime`을 직접 쓴다. 일시정지와 연동되어 있지 않다 | GameFlow(GameState) 연동 시 교체 |
| f | 손 무기 `MuzzlePoint` 로컬 위치 3종 `PistolDefault` (0, 0.15, 0.47), `ShotgunDefault` (0, 0.031, 0.928), `MiniGunDefault` (0, 0, 1.066)은 애니메이션이 없는 모델 메시 기준으로 총신 끝을 산출한 값이다. W05 Play 모드에서 애니메이션 재생 중 `MuzzlePoint`가 움직여 탄이 가끔 빗나가는 것을 사용자가 관찰했다 (발사 로직은 정상 동작) | 애니메이션(idle/running/피격) 적용 후 Play 모드로 조정 |
| g | `TempCubeEnemy.TakeDamage`는 `IsAlive`가 아닌 `IsTargetable`로 사망 여부를 검사한다. 현재는 `IsTargetable => IsAlive`라 결과가 같지만, 두 값의 의미가 달라지면 결과도 달라진다 | 실제 적 구현 시 |
| h | W03 이월 항목 (아래 표) | 각 항목 참고 |
| i | 펠릿마다 개별 피해라서 가까운 적 하나에 펠릿이 여러 발 맞으면 피해가 그만큼 누적된다 (현재 임시값 기준 최대 5발 × 5). 근접 피해 상한은 구현되지 않았다 | W08에서 검토 |
| j | `_spreadAngle`이 360이면 양 끝 탄의 각도가 -180°·+180°라서 같은 방향으로 겹친다 | 밸런스 조정 시 |
| k | `PFB_ShotgunPellet`의 탄 외형(`SM_Bullet_09`, 스케일 5)은 Play 확인 후 다시 검토하기로 했다 | Play 확인 후 재검토 |
| l | (해결) 손 무기 외형이 장착 무기에 따라 자동으로 바뀌지 않던 문제 (W05 Play 확인은 손 무기를 `ShotgunDefault`로 수동 전환해 진행). W07에서 `WeaponViewController`가 주무기 종류의 손 무기를 켜고 Animator Controller를 교체한다 | 해결 (W07) |
| m | 탄은 총구 위치(`_muzzle.position`)에서 탄 방향으로 넘어간 시간 × 탄속만큼 앞당긴 위치에서 생성되고(발사 절, 주의사항 r 참고), 중심 방향은 총구→표적 `AimPoint`의 XZ 방향이고, 총구와 표적의 XZ 위치가 겹치면 플레이어 루트→표적 방향이다 (코드 확인, 발사 절 `중심 방향` 참고). 플레이어 몸과 총구 사이에 낀 적이 맞는지는 확인하지 않았다 (미검증) | 실제 적 연동 시 사용자 Play 검증 후 AI agent Play 모드 검증으로 2차 확인 |
| n | `PlayerAimController.GetLookDirection`은 여전히 `GetAimDirection(_visualRoot.position, AimPoint)`를 쓴다. 표적과 `_visualRoot`의 XZ 위치가 겹치면 영벡터가 되어 `LateUpdate`에서 회전하지 않는다 (이동 방향으로 대체하지 않음). 겹침 시 기대 동작은 정해지지 않았다 | 미정 |
| o | SO의 `_projectilePrefab`이 비어 있으면(`null`) 그 무기의 첫 발사 때 `Dictionary`의 `null` 키 예외가 난다. `null` 검사는 없다 (의도) | 무기 SO 작성 시 연결 필수 |
| p | `ProjectilePool`의 `_defaultCapacity`·`_maxSize`는 프리팹별 풀마다 각각 적용된다. 전체 탄 상한은 `_maxSize` × 프리팹 수다 | 참고 |
| q | `TempCubeEnemy._hitFlashDuration`이 0이면 피격 색(빨강)이 원래 색으로 돌아오지 않는다. 임시 도구라 그대로 둔다 (사용자 결정) | 실제 적 구현 시 임시 적과 함께 정리 |
| r | 넘긴 시간만큼 앞당겨 생성한 탄은 총구 기준 사거리가 `ProjectileSpeed × overshoot`만큼 늘어난다. `Projectile`이 생성 위치부터 이동 거리·경과 시간을 세기 때문이다. 또 긴 프레임 끊김 시 총구~생성 위치 구간의 적은 판정하지 않는다 (코드의 `shortcut` 주석) | 누락이 문제되면 생성 구간 SphereCast 추가 |
| s | `ReloadDuration`이 0이어도 재장전이 시작된 프레임에는 더 발사하지 않는다. 재장전은 다음 프레임 `Tick`에서 끝나므로 최소 1프레임 발사가 멈춘다 | 참고 |
| t | 표적이 없다가 다시 찾은 첫 프레임에도, `ClearOverdueShotCooldown`으로 0이 된 쿨다운이 그 프레임 `Tick`에서 `-deltaTime`이 되어 생성 위치가 `deltaTime`만큼 앞당겨진다 | 참고 |
| u | `_fireInterval`의 `Min(0.01)` 하한은 Inspector 입력만 막는다. 코드나 `SerializedObject`로 넣은 값은 막지 않는다 | 참고 |
| v | 모자 탈착 등 런타임 퍼짐 변경은 SO 값(`_spreadAngle`)을 바꾸지 않고, 인스턴스별 합성값으로 처리할 예정이다 (미구현) | W12 |
| w | 탄 수와 관계없이 발사 1회 = 잔탄 1발 소모다 (코드 주석) | 진화 탄 수 증가(W10) 시 재결정 |
| x | `GetSpreadDirection`과 `GetRandomSpreadDirection`은 보간 비율만 다르고 계산이 중복된다. 그대로 둔다 (사용자 결정) | W08(퍼짐 배율 도입) 시 재판단 |
| y | `_magazineCapacity`, `_reloadDuration`에는 하한(`Min`)이 없다 | 미정 |
| z1 | 생김새를 고유 무기 종류(`WeaponType`)로 정하는 임시 구조다 (코드의 `shortcut` 주석). 캐릭터가 아닌 무기 종류에 묶여 있고, 외형 표시 컴포넌트가 생김새까지 맡는다 (reviewer 단일 책임 권고) | 캐릭터 데이터 추가 시 캐릭터 쪽으로 이전 |
| z2 | 부유 총기는 풀링 없이 `Instantiate`·`Destroy`한다 (코드의 `shortcut` 주석, 장착 변경은 인벤토리 확정 시점에만 발생한다는 전제) | 장착 변경이 잦아지면 풀링 |
| z3 | `WeaponViewController`가 꺼진 동안(`OnDisable`로 구독 해제) 장착이 바뀌면 다시 켜질 때 재동기화하지 않는다. `GetMuzzle`은 `WeaponAttackController.Update`가 매 프레임 부르므로 꺼진 동안에도 호출된다. 이때 새로 장착한 부유 무기는 뷰가 없어 `KeyNotFoundException`이 나고(다시 켜진 뒤 `LateUpdate`도 같음), 주무기만 바뀐 경우는 예외 없이 이전 주무기의 손 총구(`_activeHandWeapon`)를 돌려준다. 현재 이 컴포넌트를 끄는 흐름은 없다 | 끄는 흐름 도입 시 |
| z4 | 주무기 변경을 수반하는 `Equip`/`Unequip` 1회에 `SyncFloatingViews`가 2회 실행된다 (`PrimaryChanged`, `WeaponsChanged` 각각). 두 번째 실행은 변경이 없어 결과는 같다 | 참고 |
| z5 | `WeaponViewController.LateUpdate`와 `PlayerAimController.LateUpdate`의 실행 순서가 지정되지 않았다. 슬롯 각도가 `_visualRoot` 회전을 기준으로 하므로, 몸통 회전보다 먼저 실행되면 슬롯 목표 위치가 최대 1프레임 늦을 수 있다 | 문제가 보이면 실행 순서 지정 |
| z6 | Inspector·SO 연결 누락 시 `null` 검사 없이 예외·오동작이 난다. `_handWeapons`에 주무기 `WeaponType` 항목이 없으면 `GetMuzzle`에서 `NullReferenceException`. `_appearances`에 고유 무기 종류 항목이 없으면 등록된 Body·Head가 모두 꺼진다. SO의 `_floatingViewPrefab`이 비면 `Instantiate`에서 예외 | 무기·캐릭터 추가 시 연결 필수 |
| z7 | 런타임 부유 총기 오브젝트 이름에 `(Clone)` 접미사가 붙는다. `UnityHierarchyRules.md` 이름 규칙을 런타임 생성 오브젝트에도 적용하는지는 정해지지 않았다 | 미정 |
| z8 | `TempLoadoutTester`는 `PFB_Player`에 붙은 임시 검증 도구다 (`_extraWeapons` = `SO_Shotgun`, `SO_Minigun`). 이 Prefab을 쓰는 모든 Scene에서 시작 시 무기 3개가 장착된다 | 인벤토리 연동 시 제거 |
| z9 | Animator 파라미터(달리기·사격) 연동이 없다. 교체한 Animator Controller의 기본 상태만 재생한다 | 애니메이션 연동 시 |
| z10 | W07 reviewer 권고 중 코드에 반영하지 않은 항목 (기록만) : `WeaponViewController.Start` 주석이 실행 순서를 단정함, 이름 `gunCount`·`_views`의 의미 명확성, `WeaponLoadoutTests` 요약 주석, 이벤트 순서(`PrimaryChanged` → `WeaponsChanged`)와 `Unequip` 성공 시 `WeaponsChanged` 발생을 검증하는 테스트 부재 | 미정 |

### W03 이월 항목

| 항목 | 코드에서 확인한 사실 |
|---|---|
| 설계서 §6.2 `ProjectileSpawnContext` 개념 필드와 차이 | 실제 필드는 `SpawnPosition`, `Direction`, `Speed`, `Damage`, `MaxDistance`, `MaxLifetime`, `RequireCameraVisibility`, `MaxUniqueTargetsHit` 8개다. 설계서의 `owner`, `weaponInstanceId`, `knockbackData`, `shotId`는 없다 |
| 설계서 §7 `shotId` 대신 `_isActive` | `Projectile.Despawn`은 `_isActive`가 false면 반환하지 않아 중복 `Release`를 막는다. `shotId`는 사용하지 않는다 |
| `Direction` 주석과 실제 동작 불일치 | `ProjectileSpawnContext.Direction` 주석은 "XZ 평면 정규화 이동 방향"이지만, `Projectile.Launch`는 비정규 입력을 허용하고 `TryGetPlanarDirection`으로 직접 XZ 투영·정규화한다. 현재 호출부(`WeaponAttackController`)는 `GetFireDirection`으로 중심 방향을 구하고, 탄마다 `RandomSpread`가 true면 `GetRandomSpreadDirection`, false면 `GetSpreadDirection` 결과를 `Direction`으로 넘긴다. 총구→표적과 플레이어 중심→표적의 XZ 성분이 모두 0이면 `GetFireDirection`이 영벡터를 반환하고, 두 함수 모두 영벡터를 그대로 반환해 `Direction`에 영벡터가 들어간다. 이때 `Projectile.Launch`는 경고 로그를 남기고 탄을 바로 반환한다 |
| 적 풀링 시 `HashSet` 참조 기준 중복 판정 위험 | `ProjectileHitDetector`는 `HashSet<IDamageable>`에 참조로 기록한다. 탄이 비행하는 동안 같은 적 오브젝트가 풀에서 재사용되면 새 적이어도 같은 참조라 맞지 않는다 (재현 미검증) |
| SphereCast 시작점이 이미 Collider와 겹친 경우 | `ProjectileHitDetector.Cast`는 `Physics.SphereCastNonAlloc`을 이동 구간 시작점에서 호출한다. 시작점이 이미 Collider와 겹쳐 있어도 `SphereCastNonAlloc`은 그 Collider를 distance 0으로 검출함을 실제 Scene에서 실측했다 (W05 메인 세션 확인). 총구가 적과 겹친 상태의 피격도 사용자 Play 모드에서 정상 확인했다 |

### 기타 예외 처리

- 표적 후보 목록이 비면 `FindNearest`는 `null`을 반환한다. 후보 목록 자체가 `null`이면 안 된다 (공급하는 쪽 책임).
- `ObjectPool`이 비면 새로 생성한다. `_maxSize`(512)를 넘겨 반환된 탄은 파괴한다.
- `TempCubeEnemy`는 사망 후에도 활성 상태로 남아 `ActiveEnemies`에 있다. `IsTargetable == false`이므로 탐색에서 제외되고, `IsAlive == false`이므로 탄이 통과한다.

### 검증 방법

- EditMode 테스트 (`Assets/Tests/Editor/`)
    - `WeaponInstanceTests` : 인스턴스 간 잔탄·쿨다운 독립, 같은 Definition 두 번 장착 시 ID 구분·해제, 마지막 탄 소모 시 재장전 시작(`ConsumeAmmo_LastRound_StartsReload`), 재장전 시간 전 유지(`Tick_ShorterThanReloadDuration_KeepsReloading`), 재장전 시간 경과 시 잔탄 채움·종료(`Tick_ReloadDurationElapsed_RefillsAndFinishes`), 탄창 없는 무기 재장전 없음(`ConsumeAmmo_NoMagazine_DoesNotReload`), 넘긴 시간 이월(`ScheduleNextShot_AfterOverdueTick_CarriesOverOverdueTime`), 음수 쿨다운만 0으로(`ClearOverdueShotCooldown_ResetsOnlyNegativeCooldown`)
    - `WeaponTargetingTests` : 후방 적, 범위·공격 불가 제외, 경계 포함, 동일 거리 `SpawnId` 우선, 빈 목록, 무기별 독립 반경, 높이 무시, 조준 방향 평면·정규화, 총구가 표적과 겹칠 때 플레이어 중심 방향 사용(`GetFireDirection`), 부채꼴 1발 중앙·5발 양 끝 포함 균등 간격·퍼짐 0이면 모두 중앙·평면·정규화(`GetSpreadDirection`), 비율 양 끝·중간이 퍼짐 양 끝·중앙에 대응(`GetRandomSpreadDirection_RatioEdgesAndMiddle_MapToSpreadEdgesAndCenter`), 임의 비율이 퍼짐 범위 안(`GetRandomSpreadDirection_AnyRatio_StaysWithinSpread`), 퍼짐 0이면 중앙(`GetRandomSpreadDirection_ZeroSpread_ReturnsCenter`), 평면·정규화(`GetRandomSpreadDirection_IsFlatAndNormalized`)
    - `ProjectileTests` : 뷰포트 경계, XZ 성분 0 방향, 비스듬한 방향 정규화, 중복 기록 1회, 죽은 적 미기록, 고유 적중 한도, 재사용 시 기록 초기화
    - `WeaponLoadoutTests` : 주무기 우선순위 4단계, 빈 목록, 장착 시 지정·유지, 해제 시 유지·재선정·`null`, 이벤트 (주무기 없을 때 장착 시 `PrimaryChanged` 1회(`Equip_NoPrimary_RaisesPrimaryChangedOnce`), 주무기 있을 때 장착 시 `WeaponsChanged`만(`Equip_HasPrimary_RaisesOnlyWeaponsChanged`), 비주무기 해제 시 `PrimaryChanged` 없음(`Unequip_NonPrimary_DoesNotRaisePrimaryChanged`), 주무기 해제 시 `PrimaryChanged` 1회(`Unequip_Primary_RaisesPrimaryChangedOnce`), 없는 ID 해제 시 이벤트 없음(`Unequip_UnknownId_RaisesNoEvents`))
    - `WeaponViewSlotTests` : 슬롯 각도가 총 수만큼 원을 균등 분할하고 슬롯 0이 0°인지 (`GetSlotAngle_DividesCircleEvenly`, N = 1~4의 10개 케이스)
- 결과 : EditMode 66/66 통과 (W07 메인 세션 확인 기준, W07에서 `WeaponLoadoutTests` 이벤트 5개·`WeaponViewSlotTests` 10개 추가. W06 기준은 51/51, W05 기준은 41/41).
- Play 모드 : 조준·발사·죽은 적 탄 통과·몸통 회전이 정상임을 사용자가 확인했다. 화면 밖 무효화, 풀 한도 초과, 항목 b의 위험 상황, 항목 h 중 적 풀링 시 `HashSet` 중복 판정 위험은 Play 모드로 확인하지 않았다 (미검증). 항목 h 중 SphereCast 시작점 겹침은 W05 메인 세션 실측과 사용자 Play 확인이 있다 (W03 이월 항목 표 참고).
- Play 모드 (W05) : 사용자가 다음을 확인했다. 권총 회귀 없음, 샷건 부채꼴 발사·짧은 거리 소멸·탄마다 첫 적 적중 후 반환, 총구가 적과 겹친 상태의 피격, 손 무기를 `ShotgunDefault`로 수동 전환한 상태의 발사. 애니메이션 중 탄이 가끔 빗나가는 현상은 항목 f 참고. 항목 m은 미검증이다.
- Play 모드 (W06) : 사용자가 Play 모드로 확인한 뒤 검수를 승인했다 (SO 값 조정 포함).
- Play 모드 (W07) : 사용자가 다음을 확인한 뒤 검수를 승인했다. 원형 배치, 부유 총기의 독립 조준·발사, 주무기 해제 시 승계와 손 무기·Animator 교체, 고유 무기를 `SO_Shotgun`/`SO_Minigun`으로 바꿨을 때 생김새(Chicken/Salaryman) 변경, 이동 정상.
- 렌더 확인 (W07, 메인 세션) : 부유 총기 3종이 +Z 정면을 향하고 총구가 총신 끝에 있음, `MiniGunAnimator` 정지 자세에서 두 손 파지. 달리기 자세의 파지는 미검증이다.
- W07 미검증 : 주의사항 z3(꺼진 동안 장착 변경), z5(LateUpdate 순서로 인한 1프레임 지연), z6(연결 누락 시 예외)은 Play 모드로 재현하지 않았다.

## 관련 Issue/PR

- Issue #5 (무기 시스템 W01~W07)
- #3
