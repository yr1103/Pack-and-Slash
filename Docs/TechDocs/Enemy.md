# Enemy·스폰·피격·경험치 젬 (Enemy)

> 근거 : `Assets/Scripts/Enemy/Enemy.cs`, `Assets/Scripts/Enemy/EnemyPool.cs`, `Assets/Scripts/Enemy/EnemySpawner.cs`, `Assets/Scripts/Enemy/TempEnemySpawner.cs`, `Assets/Scripts/Enemy/EnemyHitFeedback.cs`, `Assets/Scripts/UI/GameSettings.cs`, `Assets/Scripts/Combat/Health.cs`, `Assets/Scripts/Combat/DamageGate.cs`, `Assets/Scripts/Combat/ExperienceGem.cs`, `Assets/Scripts/Combat/ExperienceGemPool.cs`, `Assets/Tests/Editor/{HealthTests,DamageGateTests,ExperienceGemTests,EnemySpawnerTests}.cs`, `Assets/Prefabs/Enemy/*.prefab`, `Assets/Prefabs/Combat/PFB_ExperienceGem.prefab`, `Assets/Prefabs/FX/PFB_DamageNumberComic.prefab`, `Assets/Animations/Enemy/*`, `Assets/Scenes/GameScene.unity`, `Assets/Scenes/TestMap.unity`, `Assets/Imports/Other/BTM_Assets/BTM_Items_Gems/Scripts/SimpleGemsAnim.cs`, `Assets/Imports/Fonts/DamageNumbersPro/Scripts/Internal/DamageNumber.cs`(`Spawn` 시그니처)
>
> 설계 원본 : `Docs/Spec/Enemy/MonsterSystem.md` (이 문서는 현재 구현 상태만 기술한다)
>
> 관련 문서 : Enemy 이동(Flow Field·Separation)은 `Docs/TechDocs/Map.md`, 표적 탐색·투사체는 `Docs/TechDocs/Weapon.md`, 처치 수·플레이어 체력·경험치는 `Docs/TechDocs/GameFlow.md`

## 목적 / 기능 범위

- 일반 몬스터의 체력·피격·사망 처리와 무기 표적 등록을 맡는다.
- 진행 시간에 따라 해금된 몬스터를 카메라 시야 바로 바깥에 일정 간격으로 생성하고, 프리팹별 풀로 재사용한다.
- 피격 시 재질 반짝임과 피해 숫자를 표시한다.
- 사망 시 경험치 젬을 떨어뜨리고, 젬은 플레이어 수집 반경에 들어오면 흩어졌다가 플레이어에게 흡수되어 경험치를 지급한다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #8 : `Enemy`(체력·피격·사망·풀 반환), `EnemyPool`, `EnemySpawner`(시야 밖 생성·해금·상한), 피격 연출(`EnemyHitFeedback`), 경험치 젬·젬 풀, 공용 `Health`·`DamageGate`, 일반 몬스터 Prefab 3종 |
| 제외 (후속) | 코드의 `shortcut` 주석 기준 : `EnemyManager`(static 활성 목록 이관), 게임 시간 관리(GameState) 연동. 그 밖의 후속 범위는 `Docs/Spec/Enemy/MonsterSystem.md` 참고 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `Enemy` (MonoBehaviour) | `ITargetable`·`IDamageable` 구현. 체력(`Health`) 소유, 피해 적용·사망 1회 처리, 사망 연출 시간(`_deathDuration`) 뒤 풀 반환. static 활성 목록 `ActiveEnemies`(무기 표적 후보), static 이벤트 `Died`, 인스턴스 이벤트 `Damaged`. 접촉 피해량 `ContactDamage`, 드롭 경험치 `ExperienceValue` 노출 | `Health`, `EnemyMovement`·`Collider`(`Awake`에서 `GetComponent`), `Animator`(선택), `IObjectPool<Enemy>` |
| `EnemyPool` (MonoBehaviour) | 프리팹마다 `UnityEngine.Pool.ObjectPool<Enemy>`를 따로 둔다(`Dictionary<Enemy, ObjectPool<Enemy>>`). `Spawn(prefab, position, flowFieldManager, player)` : 풀에서 꺼내 위치 지정 후 `Enemy.Initialize` 호출. 프리팹의 풀은 그 프리팹의 첫 `Spawn` 때 생성 | `Enemy`, `FlowFieldManager` |
| `EnemySpawner` (MonoBehaviour) | `GameSession.ElapsedTime` 기준 `_spawnInterval`마다 해금된 프리팹 중 균등 랜덤으로 `_spawnCountPerTick`마리를 시야 사다리꼴 바깥에 생성. 동시 활성 상한 `_maxActiveEnemies`. 판정 함수 `IsUnlocked`, `HasCapacity`, `GetDistanceToBoundary`(static) | `GameSession`, `EnemyPool`, `FlowFieldManager`(`CostField`), 플레이어 `Transform`, `Camera`, `Enemy.ActiveEnemies` |
| `TempEnemySpawner` (MonoBehaviour, 임시) | `TestMap` 전용. `Start`에서 `_spawnCount`마리를 자기 위치 기준 반경 `_spawnRadius` 원 위 랜덤 각도에 `EnemyPool`로 일괄 생성. 프리팹은 `_enemyPrefabs`에서 생성마다 균등 랜덤 | `EnemyPool`, `FlowFieldManager`, 플레이어 `Transform` |
| `EnemyHitFeedback` (MonoBehaviour) | `Enemy.Damaged` 구독. 피해마다 `_renderer.sharedMaterial`을 `_flashMaterial`로 `_flashDuration`초 교체, `DamageNumber.Spawn(위치, 피해량)`으로 피해 숫자 표시. 각각 `GameSettings` 설정이 꺼져 있으면 생략 | `Enemy`, `Renderer`, `Material`, `DamageNumbersPro.DamageNumber`, `GameSettings` |
| `Health` (일반 클래스) | 최대·현재 체력, `ApplyDamage`(0 미만으로 내려가지 않음, 처음 사망한 호출만 `true`), `ResetToMax`. `Enemy`·`PlayerHealth`가 사용 | 없음 |
| `DamageGate` (일반 클래스) | 피해 1회 통과 후 무적 시간 동안 추가 통과를 막는 게이트(`TryPass`, `Tick`, `IsOpen`). 현재 `PlayerHealth`만 사용 | 없음 |
| `ExperienceGem` (MonoBehaviour) | 상태 `Idle` → `Scatter` → `Attract`. 도착 시 `PlayerExperience.AddExperience` 1회 후 풀 반환. 판정 함수 `IsInCollectRadius`, `GetScatterDirection`(static). 수명 없음 | `PlayerExperience`, `IObjectPool<ExperienceGem>` |
| `ExperienceGemPool` (MonoBehaviour) | `Enemy.Died` 구독. 사망 위치(높이는 `_spawnHeight`로 고정)에 젬을 꺼내 `Initialize` 호출. `ExperienceValue`가 0 이하면 생성하지 않음 | `ExperienceGem`, `PlayerExperience`, `Enemy` |

## API / 이벤트 / 데이터 계약

### 이벤트

| 이벤트 | 소유 | 인자 | 발생 시점 | 구독자 |
|---|---|---|---|---|
| `Enemy.Died` (static) | `Enemy` | 죽은 `Enemy` | 피해로 처음 사망한 `TakeDamage` 호출 안에서 1회. 이동·Collider 정지, 반환 타이머 시작, Animator `Die` 트리거 뒤에 발생 | `ExperienceGemPool.SpawnGem`, `GameSession.HandleEnemyDied` (구독 순서 지정 없음) |
| `Enemy.Damaged` (인스턴스) | `Enemy` | 이번 피해량(`float`) | `TakeDamage`에서 `Health.ApplyDamage` 직후, 사망 처리(`Die`) 전. 사망하는 피해도 포함 | `EnemyHitFeedback.OnDamaged` (`OnEnable` 구독, `OnDisable` 해제) |

- `Damaged`의 인자는 실제로 깎인 체력이 아니라 들어온 피해량이다. 남은 체력보다 큰 피해(오버킬)도 그대로 전달된다. 피해 숫자는 이 값을 표시한다 (사용자 결정).
- `Died`는 경험치를 직접 지급하지 않는다. 경험치는 젬이 플레이어에게 도착할 때 지급된다.

### `Enemy`

| 멤버 | 내용 |
|---|---|
| `ActiveEnemies` (static, `IReadOnlyList<ITargetable>`) | `OnEnable`에서 추가, `OnDisable`에서 제거. 사망 연출 중인 Enemy도 반환 전까지 포함된다. `WeaponAttackController`가 표적 후보로, `EnemySpawner`가 상한 판정에 쓴다 (코드의 `shortcut` 주석 : `EnemyManager` 도입 시 이관) |
| `IsAlive` | `!체력.IsDead && isActiveAndEnabled` |
| `IsTargetable` | `IsAlive`와 같다 |
| `SpawnId` | `OnEnable`마다 static 카운터로 새로 발급. 풀 재사용 시에도 새 적으로 취급된다 |
| `AimPoint` | `_aimPoint.position` |
| `ContactDamage` | `_contactDamage` (플레이어 접촉 시 1회 피해량. 판정은 `PlayerHealth`, `Docs/TechDocs/GameFlow.md`) |
| `ExperienceValue` | `_experienceValue` (`Min(0)`). 0이면 젬을 드롭하지 않는다 |
| `TakeDamage(damage)` | `IsAlive`가 false면 무시. 체력 감소 → `Damaged` → 이번 피해로 처음 사망했으면 `Die` |
| `Initialize(flowFieldManager, player, pool)` | 반환할 풀 저장, `EnemyMovement.Initialize` 호출. `EnemyPool.Spawn`이 꺼낸 직후 호출한다 |

- 활성화(`OnEnable`) 시 `SpawnId` 발급, 체력 최대로 복구, 반환 타이머 0, `EnemyMovement`·`Collider` 켬, 목록 등록.
- 사망(`Die`) 시 `EnemyMovement`·`Collider`를 끄고 `_deathRemaining = _deathDuration`, `_animator`가 있으면 `Die` 트리거, `Died` 발생. Collider가 꺼지므로 탄이 맞지 않는다.
- 반환(`Despawn`) : `Update`에서 `_deathRemaining`을 `Time.deltaTime`만큼 줄여 0 이하가 되면 실행한다. 풀이 있으면 `_pool.Release(this)`, 풀이 없으면(Scene에 직접 배치해 `Initialize`를 받지 않은 Enemy) `SetActive(false)`만 한다.
- Animator는 재활성화 시 기본 상태(`Move`)로 돌아가므로 `OnEnable`에서 되돌리지 않는다 (코드 주석).

### `Health`

- 생성자에서 현재 체력 = 최대 체력. 최대 체력이 0 이하면 생성 즉시 사망 상태다.
- `ApplyDamage(damage)` : 이미 사망 상태면 `false`를 반환하고 무시한다. 아니면 `max(0, 현재 - 피해)`로 줄이고 사망 여부를 반환한다. 따라서 `true`는 처음 사망한 호출에서만 나온다.
- `IsDead` = 현재 체력 ≤ 0.

### `DamageGate`

- `TryPass()` : 열려 있으면(`남은 시간 ≤ 0`) 남은 시간을 무적 시간으로 채우고 `true`, 아니면 `false`.
- `Tick(deltaTime)` : 남은 시간을 줄인다 (0 미만으로 내려가지 않음). 경과 시간이 무적 시간과 같으면 다시 열린다.
- 무적 시간 0이면 매번 통과한다.

### `EnemySpawner`

- 생성 주기 : `GameSession.IsPlaying`이고 `ElapsedTime ≥ _nextSpawnTime`이면 `_nextSpawnTime += _spawnInterval` 후 1회 생성(`SpawnTick`). `_nextSpawnTime`은 0에서 시작한다. 한 `Update`에 최대 1회만 생성한다. 세션이 끝나면(`IsPlaying == false`) 생성을 멈춘다.
- 해금 : `IsUnlocked(unlockTime, elapsedTime)` = `elapsedTime ≥ unlockTime`. 매 생성마다 `_spawnTable`에서 해금된 프리팹을 모으고, 마리마다 그중 하나를 균등 랜덤으로 고른다. 해금된 프리팹이 없으면 생성하지 않는다.
- 상한 : 마리마다 `HasCapacity(Enemy.ActiveEnemies.Count, _maxActiveEnemies)`(= 활성 수 < 상한)를 확인하고, 상한이면 그 회차 생성을 끝낸다. 활성 수에는 사망 연출 중인 Enemy와 Scene에 직접 배치한 Enemy도 포함된다. 풀에 보관된(비활성) 개체는 포함되지 않는다.
- 위치 (`TryFindSpawnPosition`)
    1. 지면 높이 = 플레이어 Y. 카메라 뷰포트 네 모서리 광선을 이 높이의 수평면에 투사해 시야 사다리꼴의 XZ 꼭짓점을 구한다. 광선이 평면에 닿지 않으면 광선 위 `MAX_GROUND_DISTANCE`(200) 지점을 쓴다.
    2. 랜덤 방향마다 `GetDistanceToBoundary(플레이어 XZ, 방향, 꼭짓점)`로 경계까지 거리를 구한다.
    3. 거리가 0 이하(경계와 만나지 않음, 원점이 시야 밖)면 그 방향은 버리고 재시도한다.
    4. 생성 위치 = 플레이어 XZ + 방향 × (경계 거리 + `_spawnMargin`), Y = 지면 높이.
    5. 그 타일이 `CostField.IsWalkable`이면 확정, 아니면(격자 밖·장애물) 재시도한다.
    6. `_maxPositionAttempts`회 모두 실패하면 그 1마리만 건너뛴다.
- `GetDistanceToBoundary` : 볼록 다각형의 각 변과 방향 광선의 교점 중 가장 먼 거리를 반환한다. 변과 평행이면 그 변은 건너뛴다. 교점이 없으면 0.

### `EnemyHitFeedback`

- `Awake`에서 `_renderer.sharedMaterial`을 원래 재질로 저장한다.
- 피해마다 (`OnDamaged`)
    - `GameSettings.HitFlashEnabled`이고 `_flashDuration > 0`이면 `sharedMaterial`을 `_flashMaterial`로 바꾸고 남은 시간을 `_flashDuration`으로 채운다. 반짝이는 중에 다시 맞으면 남은 시간만 갱신된다. 설정이 꺼져 있거나 `_flashDuration`이 0이면 반짝임을 생략한다.
    - `GameSettings.DamageNumberEnabled`이면 `_damageNumberPrefab.Spawn(_numberPoint.position, damage)`로 피해 숫자를 띄운다. 꺼져 있으면 생략한다.
    - 두 설정은 피해마다 읽으므로 설정 판넬에서 바꾸면 다음 피격부터 적용된다. 설정 값은 `GameSettings`의 static 캐시다 (`Docs/TechDocs/GameFlow.md`).
- `Update`에서 남은 시간을 `Time.deltaTime`만큼 줄이고 0 이하가 되면 원래 재질로 되돌린다.
- `OnDisable`(풀 반환 등)에서 반짝이는 중이었으면 원래 재질로 즉시 되돌린다. 재사용 시 반짝임 재질이 남지 않는다.

### `ExperienceGem`

| 상태 | 동작 | 다음 상태 |
|---|---|---|
| `Idle` | 매 프레임 `IsInCollectRadius(젬, 플레이어, PlayerExperience.CollectRadius)` 확인 | 반경 안이면 흩어짐 방향을 정하고 `Scatter` |
| `Scatter` | 시작 위치에서 흩어짐 방향으로 `_scatterDistance`만큼 `_scatterDuration`초 동안 이동 (2차 ease-out) | 진행률 1이면 `Attract` |
| `Attract` | `Vector3.MoveTowards`로 플레이어 위치를 향해 `_attractSpeed`로 이동. 진입 후 반경 변화와 무관하게 계속 | 플레이어까지 거리 ≤ `_arriveDistance`이면 `Collect` |

- `Collect` : `PlayerExperience.AddExperience(경험치)` 후 `_pool.Release(this)`. 반환으로 비활성화되어 `Update`가 다시 돌지 않으므로 중복 지급이 없다.
- `IsInCollectRadius` : XZ 거리(높이 무시), 경계 포함.
- `GetScatterDirection(젬, 플레이어, scatterAngle, randomRatio)` : 플레이어→젬 XZ 방향을 `Lerp(-scatterAngle, +scatterAngle, randomRatio)`도 회전한 정규화 방향. XZ가 겹치면(거리 제곱 ≤ 0.000001) `randomRatio × 360`도의 월드 방향. 호출부는 `Random.value`를 넘긴다.
- `Initialize(playerExperience, experienceValue, pool)` : 지급 대상·값·풀을 저장하고 상태를 `Idle`로 되돌린다.
- 수명이 없다. 수집되지 않은 젬은 반환되지 않고 남는다.

### `ExperienceGemPool`

- `Awake`에서 풀 생성, `OnEnable`/`OnDisable`에서 `Enemy.Died` 구독/해제.
- `SpawnGem(enemy)` : `enemy.ExperienceValue ≤ 0`이면 무시. 위치 = Enemy 위치(Y는 `_spawnHeight`), 회전 초기화 후 `Initialize(_playerExperience, enemy.ExperienceValue, _pool)`.

### 풀 공통 (`EnemyPool`, `ExperienceGemPool`)

- `ObjectPool` 기본 동작 : 비어 있으면 새로 생성, `_maxSize`를 넘겨 반환된 개체는 `Destroy`. `collectionCheck: true`.
- 꺼낼 때 `SetActive(true)`, 반환 시 `SetActive(false)`.
- `EnemyPool`의 `_defaultCapacity`·`_maxSize`는 프리팹별 풀마다 각각 적용된다.

## 호출 순서 또는 데이터 흐름

```
[매 프레임 Update] EnemySpawner
  IsPlaying && ElapsedTime ≥ _nextSpawnTime
  └─ _nextSpawnTime += _spawnInterval → SpawnTick(ElapsedTime)
       ├─ CollectUnlockedPrefabs (UnlockTime ≤ ElapsedTime)
       └─ for _spawnCountPerTick
            ├─ HasCapacity(ActiveEnemies.Count, _maxActiveEnemies) 아니면 종료
            ├─ TryFindSpawnPosition 실패 → 이 1마리 건너뜀
            └─ EnemyPool.Spawn(랜덤 해금 프리팹, 위치, FlowFieldManager, 플레이어)
                 ├─ (프리팹 풀 없으면 생성) → pool.Get()
                 │    → (새 개체면 Instantiate → Awake → OnEnable) / (재사용이면 SetActive(true) → OnEnable)
                 │      OnEnable : SpawnId 발급, 체력 복구, 이동·Collider 켬, ActiveEnemies 등록
                 ├─ SetPositionAndRotation(위치, identity)
                 └─ Enemy.Initialize(FlowFieldManager, 플레이어, pool) → EnemyMovement.Initialize

[피격] Projectile 적중 → IDamageable.TakeDamage(피해)   (Weapon.md `투사체 적중·반환`)
Enemy.TakeDamage
  ├─ IsAlive 아니면 무시
  ├─ Health.ApplyDamage(피해)
  ├─ Damaged(피해) → EnemyHitFeedback : 재질 교체, 피해 숫자 Spawn (각각 GameSettings가 켜져 있을 때만)
  └─ 처음 사망이면 Die()
       ├─ EnemyMovement·Collider 끔, _deathRemaining = _deathDuration
       ├─ Animator Die 트리거 (있으면)
       └─ Died(this)
            ├─ ExperienceGemPool.SpawnGem → 젬 Get → Initialize (Idle)
            └─ GameSession.HandleEnemyDied → IsPlaying이면 KillCount++

[매 프레임 Update] Enemy (사망 상태만)
  _deathRemaining -= deltaTime → 0 이하면 Despawn
    ├─ 풀 있음 → pool.Release → SetActive(false) → OnDisable : ActiveEnemies 제거, EnemyHitFeedback 재질 복원
    └─ 풀 없음 → SetActive(false)

[매 프레임 Update] ExperienceGem
  Idle → (수집 반경 안) → Scatter → (진행률 1) → Attract → (도착) → AddExperience → pool.Release
```

## Scene·Prefab·Inspector 사용 방법

### 일반 몬스터 Prefab (`Assets/Prefabs/Enemy/PFB_EnemyMushroom|Cactus|Bat.prefab`)

```
PFB_Enemy…                    (Layer : Enemy(6), CapsuleCollider, EnemyMovement, Enemy, EnemyHitFeedback)
├─ AimPoint                   (로컬 위치 (0, 0.6, 0), Enemy._aimPoint·EnemyHitFeedback._numberPoint)
└─ (외형 모델 Prefab 인스턴스) (Animator, Renderer)
```

| Prefab | 외형 모델 | `_maxHealth` | `EnemyMovement._moveSpeed` | Animator Controller |
|---|---|---|---|---|
| `PFB_EnemyMushroom` | `MushroomAngryPADefault` | 30 | 2.5 | `EnemyMushroom.controller` |
| `PFB_EnemyCactus` | `CactusPADefault` | 45 | 2 | `EnemyCactus.overrideController` |
| `PFB_EnemyBat` | `BatPADefault` | 20 | 4 | `EnemyBat.overrideController` |

3종 공통 값

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `CapsuleCollider` | 반지름 / 높이 / 중심 | 0.5 / 1.2 / (0, 0.6, 0), 트리거 아님 (루트에 있음, `Map.md` 주의사항 c의 전제 충족) |
| `EnemyMovement` | `_stopDistance` / `_separationRadius` / `_separationWeight` / `_enemyLayer` | 1 / 1.2 / 1 / Enemy |
| `Enemy` | `_aimPoint` | 자식 `AimPoint` |
| | `_animator` | 외형 모델의 Animator |
| | `_deathDuration` | 1 |
| | `_contactDamage` | 10 |
| | `_experienceValue` | 1 |
| `EnemyHitFeedback` | `_enemy` | 같은 루트의 `Enemy` |
| | `_renderer` | 외형 모델의 Renderer |
| | `_flashMaterial` | `Assets/Materials/MAT_HitFlash.mat` |
| | `_damageNumberPrefab` | `PFB_DamageNumberComic` |
| | `_numberPoint` | 자식 `AimPoint` |
| | `_flashDuration` | 0.1 |

- 외형 모델 이름은 Prefab 인스턴스의 `m_Name` 오버라이드 값이다. 원본 모델 Prefab은 `Assets/Imports/`(Git 추적 제외) 아래에 있다.

### Animator (`Assets/Animations/Enemy/`)

- `EnemyMushroom.controller` : Base Layer 하나. 기본 상태 `Move`, 트리거 파라미터 `Die`가 켜지면 `Die` 상태로 전이한다.
- `EnemyCactus.overrideController`, `EnemyBat.overrideController` : `EnemyMushroom.controller`를 원본으로 `Move`·`Die` 클립 2개를 각 몬스터 클립으로 교체한다.
- `Enemy.Die()`가 `Die` 트리거를 건다. 파라미터는 `Die` 하나뿐이라 `Move` 상태는 이동·정지와 관계없이 재생된다.

### `Assets/Prefabs/Enemy/PFB_TempCubeEnemy.prefab` (임시)

- 루트 : Layer Enemy(6), `BoxCollider`(크기 1), `EnemyMovement`(`_moveSpeed` 3), `Enemy`(`_maxHealth` 30, `_deathDuration` 1, `_animator` 비어 있음). 자식 `AimPoint`. `EnemyHitFeedback` 없음.
- `Enemy`의 `_contactDamage`·`_experienceValue`는 Prefab에 직렬화된 값이 없어 코드 기본값(10, 1)이 적용된다 (Play 확인됨).
- 이전 `TempCubeEnemy` 컴포넌트(스크립트 삭제됨)를 `Enemy`로 대체했다. Scene 배치 인스턴스 3개(`PFB_TempCubeEnemy_01`~`03`)는 `GameScene`에서 삭제됐고 `TestMap`에만 남아 있다 (주의사항 참고).

### `Assets/Prefabs/Combat/PFB_ExperienceGem.prefab`

```
PFB_ExperienceGem             (ExperienceGem)
└─ Ruby2                      (BTM Items Gems 모델 Prefab 인스턴스, 로컬 스케일 0.3, SimpleGemsAnim)
```

| 필드 | Prefab 값 (코드 기본값) |
|---|---|
| `_scatterAngle` | 60 (60) |
| `_scatterDuration` | 0.25 (0.15) |
| `_scatterDistance` | 1.2 (0.6) |
| `_attractSpeed` | 10 (10) |
| `_arriveDistance` | 0.1 (0.1) |

- `Ruby2`의 `SimpleGemsAnim`은 `isFloating`·`isScaling`을 0(끔)으로 오버라이드했다. 이유 : `SimpleGemsAnim.Start`가 월드 위치(`transform.position`)와 크기를 한 번만 캐시하고, 켜져 있으면 매 프레임 그 캐시 기준으로 위치·크기를 덮어쓴다. 젬은 풀에서 재사용되며 이동(흩어짐·흡수)하므로 위치·크기가 틀어진다.
- `isRotating` 등 다른 `SimpleGemsAnim` 값은 오버라이드하지 않았다 (원본 모델 Prefab 값, 이 문서에서 확인하지 않음).

### `Assets/Prefabs/FX/PFB_DamageNumberComic.prefab`

- DamageNumbersPro의 `DamageNumber` 컴포넌트 Prefab. Comic 3D 프리셋(`Assets/Imports/Fonts/DamageNumbersPro/Demo/Prefabs/3D/Comic.prefab`)의 복사본이다.

| 필드 | 값 |
|---|---|
| `lifetime` | 0.8 |
| `durationFadeOut` | 0.3 |
| `enablePooling` / `poolSize` | 1 / 200 |
| `enableNumber` | 1 (숫자 표시) |
| `enableLeftText` | 0 (`leftText` "Boom" 끔) |
| `enableRightText` / `enableTopText` / `enableBottomText` | 0 / 0 / 0 |

### `Assets/Scenes/GameScene.unity`

```
Gameplay
├─ Enemies                    (EnemyPool._enemyParent)
├─ Gems                       (ExperienceGemPool._gemParent)
└─ Projectiles
Managers
├─ GameSession
├─ ProjectilePool
├─ MapGenerator
├─ FlowFieldManager
├─ EnemyPool                  (EnemyPool)
├─ ExperienceGemPool          (ExperienceGemPool)
└─ EnemySpawner               (EnemySpawner)
```

- `Managers`의 자식 순서는 위 목록 중 `GameSession`·`ProjectilePool`·`MapGenerator`·`FlowFieldManager`·`EnemyPool`까지만 Transform 순서로 확인했다. 나머지 두 오브젝트의 순서는 확인하지 않았다.

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `EnemyPool` | `_enemyParent` | `Gameplay/Enemies` |
| | `_defaultCapacity` / `_maxSize` | 64 / 512 (프리팹별 풀마다) |
| `ExperienceGemPool` | `_gemPrefab` | `PFB_ExperienceGem` |
| | `_gemParent` | `Gameplay/Gems` |
| | `_playerExperience` | 플레이어 Prefab 인스턴스의 `PlayerExperience` |
| | `_defaultCapacity` / `_maxSize` | 64 / 512 |
| | `_spawnHeight` | 0.3 |
| `EnemySpawner` | `_gameSession` | `Managers/GameSession` |
| | `_enemyPool` | `Managers/EnemyPool` |
| | `_flowFieldManager` | `Managers/FlowFieldManager` |
| | `_player` | 플레이어 Prefab 인스턴스 |
| | `_camera` | `MainCamera`의 Camera |
| | `_spawnTable` | `PFB_EnemyMushroom` 0초, `PFB_EnemyCactus` 180초, `PFB_EnemyBat` 360초 |
| | `_spawnInterval` | 1 |
| | `_spawnCountPerTick` | 2 |
| | `_maxActiveEnemies` | 500 |
| | `_spawnMargin` | 2 |
| | `_maxPositionAttempts` | 8 |

### `Assets/Scenes/TestMap.unity`

- `GameSession`·`EnemySpawner`가 없다. 대신 `Managers/TempEnemySpawner`가 `Start`에서 1회 일괄 생성한다. `EnemyPool`·`ExperienceGemPool`은 `GameScene`과 같은 방식으로 `Managers` 아래에 있다. 값은 `Docs/TechDocs/Map.md`의 `TestMap.unity` 절 참고.

## 예외·주의사항·검증 방법

### 주의사항·알려진 한계

| # | 내용 | 후속 시점 |
|---|---|---|
| a | `ActiveEnemies`는 static 목록이다. 사망 연출 중인 Enemy도 반환 전까지 포함되므로 `EnemySpawner` 상한 판정과 무기 후보 순회에 들어간다 (무기 탐색은 `IsTargetable == false`로 제외) | `EnemyManager` 도입 시 이관 |
| b | `TestMap`의 Scene 배치 `PFB_TempCubeEnemy` 3개(`GameScene`에서는 삭제됨)는 `Initialize`를 받지 않아 이동하지 않고(`FlowFieldManager` 미주입), 사망 시 풀 대신 `SetActive(false)`만 한다. 이들도 `ActiveEnemies`·처치 수·젬 드롭(경험치 1)에 포함된다 | 임시 적 정리 시 |
| c | (해결) `Enemy`, `EnemyHitFeedback`, `ExperienceGem`, `PlayerHealth`, `GameSession`이 `Time.deltaTime`을 직접 쓰던 일시정지 미연동 `shortcut` 주석은 Issue #13에서 삭제됐다. 일시정지는 `GameSession`이 `Time.timeScale` 0으로 구현하므로(`Docs/TechDocs/GameFlow.md`) 사망 연출·반짝임 복원·젬 이동이 일시정지와 함께 멈추는 것이 의도된 동작이다 | 해결 (#13) |
| d | 젬에는 수명이 없다. 수집되지 않은 젬은 계속 활성 상태로 남고 매 프레임 수집 반경을 검사한다 | 젬 수가 문제되면 수명·합치기 검토 |
| e | 젬 높이는 `_spawnHeight`(월드 Y 0.3) 고정이다. Enemy가 다른 높이에서 죽어도 젬은 이 높이에 생성된다 | 지면 높이가 달라지면 |
| f | `EnemyHitFeedback`은 `Renderer.sharedMaterial` 하나만 교체한다. 외형에 Renderer가 여러 개면 연결한 하나만 반짝인다 | 외형 추가 시 |
| g | 피해 숫자는 들어온 피해량(오버킬 포함)을 표시한다. 실제로 깎인 체력과 다를 수 있다 (사용자 결정) | 참고 |
| h | `EnemySpawner`의 `_nextSpawnTime`은 0에서 시작하므로 첫 생성은 세션 시작 직후다. 한 `Update`에 1회만 생성하므로 프레임이 `_spawnInterval`보다 길게 끊기면 밀린 회차는 이후 프레임에 1회씩 생성된다 | 참고 |
| i | 생성 위치 재시도가 모두 실패하면 그 1마리는 조용히 건너뛴다 (로그 없음) | 참고 |
| j | 프리팹·Inspector 연결 누락 시 `null` 검사가 없다. `Enemy._aimPoint`가 비면 `AimPoint` 접근에서, `EnemyHitFeedback`의 `_renderer`·`_damageNumberPrefab`·`_numberPoint`가 비면 `Awake`·피격 시 예외가 난다. `Enemy` 루트에 `EnemyMovement`·`Collider`가 없으면 `OnEnable`에서 예외가 난다 | 몬스터 추가 시 연결 필수 |
| k | `EnemyPool`은 `_maxSize`를 넘겨 반환된 Enemy를 `Destroy`한다. 그 Enemy를 `CurrentTarget`으로 가진 무기가 있으면 `Weapon.md` 주의사항 b의 상황이 될 수 있다 (재현 미검증) | 참고 |

### 검증 방법

- EditMode 테스트 (`Assets/Tests/Editor/`)
    - `HealthTests` : 치명적이지 않은 피해 시 체력 감소, 체력이 정확히 0 또는 그 아래(오버킬)로 내려가면 사망을 1회만 보고, 사망 후 피해 무시, 사망 후 `ResetToMax`로 최대 체력 복구
    - `DamageGateTests` : 같은 프레임 반복 시 1회만 통과, 무적 중 차단, 무적 후 재통과, 무적 시간과 정확히 같은 경과 시 재통과, 무적 시간 0이면 항상 통과
    - `ExperienceGemTests` : 수집 반경 안·밖·경계(포함)·높이 차 무시, 흩어짐 방향 평면·정규화, 비율 양 끝·중간이 각도 양 끝·반대 방향에 대응, 임의 비율이 각도 범위 안, 플레이어와 겹칠 때 평면·정규화 방향
    - `EnemySpawnerTests` : 해금 시각 직전 false·해금 시각 true·이후 항목 해금 시각에 이전 항목 true, 상한 미만 true·상한 false, 경계 거리 앞·뒤·옆(기울어진 변)·대각선(결과가 경계 위), 원점이 시야 밖이고 경계 반대 방향이면 0
- 테스트 실행 결과 : EditMode 테스트 161/161 통과 (2026-11-18, RP 단계 기준).
- Play 모드 (AI agent) : 스폰 후보 200회 샘플이 전부 화면 밖·통행 가능 칸, 처치 수 증가, 풀에서 재사용된 Enemy 16/16이 `Move` 상태.
- Play 모드 (사용자) : 몬스터 3종 이동·피격·사망 애니메이션, 젬 드롭·흡수, 해금 시각 변경, 피격 반짝임·데미지 숫자 확인.

## 관련 Issue/PR

- Issue #8 (몬스터 프로토타입)
- PR : 미정
- Issue #7 (맵 프로토타입, Enemy 이동 `EnemyMovement`·`TempEnemySpawner` 최초 도입. `Docs/TechDocs/Map.md`)
