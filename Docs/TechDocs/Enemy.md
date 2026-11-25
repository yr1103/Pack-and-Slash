# Enemy·스폰·피격·경험치 젬 (Enemy)

> 근거 : `Assets/Scripts/Enemy/Enemy.cs`, `Assets/Scripts/Enemy/EnemyPool.cs`, `Assets/Scripts/Enemy/EnemySpawner.cs`, `Assets/Scripts/Enemy/TempEnemySpawner.cs`, `Assets/Scripts/Enemy/EnemyHitFeedback.cs`, `Assets/Scripts/Enemy/{BossCharge,BossChargePattern,BossShootPattern,BossPatternSequencer,IBossPattern,EnemyMovement}.cs`, `Assets/Scripts/UI/GameSettings.cs`, `Assets/Scripts/UI/LevelUpPanel.cs`(`RequestChestReward`), `Assets/Scripts/GameFlow/GameSession.cs`(`HandleEnemyDied`, `_endFreezeDelay`), `Assets/Scripts/Player/PlayerHealth.cs`(`TakeDamage`), `Assets/Scripts/Weapon/Projectile/{Projectile,ProjectileHitDetector}.cs`, `Assets/Scripts/Combat/Health.cs`, `Assets/Scripts/Combat/DamageGate.cs`, `Assets/Scripts/Combat/ExperienceGem.cs`, `Assets/Scripts/Combat/ExperienceGemPool.cs`, `Assets/Scripts/Combat/{TreasureChest,TreasureChestSpawner}.cs`, `Assets/Scripts/Currency/Currency.cs`, `Assets/Tests/Editor/{HealthTests,DamageGateTests,ExperienceGemTests,EnemySpawnerTests,BossChargeTests,BossShootPatternTests,EnemyMovementTests}.cs`, `Assets/Prefabs/Enemy/*.prefab`, `Assets/Prefabs/Combat/{PFB_ExperienceGem,PFB_ExperienceGemElite,PFB_TreasureChest}.prefab`, `Assets/Prefabs/FX/PFB_DamageNumberComic.prefab`, `Assets/Materials/{MAT_ChargeWarning,MAT_BossBullet}.mat`, `Assets/Animations/Enemy/*`, `ProjectSettings/TagManager.asset`(레이어 번호), `Assets/Scenes/GameScene.unity`, `Assets/Scenes/TestMap.unity`, `Assets/Imports/Other/BTM_Assets/BTM_Items_Gems/Scripts/SimpleGemsAnim.cs`, `Assets/Imports/Fonts/DamageNumbersPro/Scripts/Internal/DamageNumber.cs`(`Spawn` 시그니처)
>
> 설계 원본 : `Docs/Spec/Enemy/MonsterSystem.md` (이 문서는 현재 구현 상태만 기술한다)
>
> 관련 문서 : Enemy 이동(Flow Field·Separation)은 `Docs/TechDocs/Map.md`, 표적 탐색·투사체는 `Docs/TechDocs/Weapon.md`, 처치 수·플레이어 체력·경험치는 `Docs/TechDocs/GameFlow.md`

## 목적 / 기능 범위

- 일반 몬스터의 체력·피격·사망 처리와 무기 표적 등록을 맡는다.
- 진행 시간에 따라 해금된 몬스터를 카메라 시야 바로 바깥에 일정 간격으로 생성하고, 프리팹별 풀로 재사용한다.
- 피격 시 재질 반짝임과 피해 숫자를 표시한다.
- 사망 시 경험치 젬을 떨어뜨리고, 젬은 플레이어 수집 반경에 들어오면 흩어졌다가 플레이어에게 흡수되어 경험치를 지급한다. 젬 Prefab은 Enemy마다 지정할 수 있다(강화몹 금색 젬).
- 보스·강화몹을 예정 시각에 1마리씩 시야 밖 격자 변 중점에 생성한다. 보스는 돌진·사격 패턴을 가지며, 최종 보스는 패턴을 순서대로 순환한다.
- 원형 대량 스폰 시각마다 플레이어 중심 원 위에 저속 몬스터를 한 번에 생성한다.
- 중간 보스가 죽으면 보물 상자를 떨어뜨리고, 플레이어가 획득 반경에 들어오면 보상 화면을 요청한다. 최종 보스가 죽으면 세션이 클리어된다(`Docs/TechDocs/GameFlow.md`).

| 범위 | 내용 |
|---|---|
| 포함 | Issue #8 : `Enemy`(체력·피격·사망·풀 반환), `EnemyPool`, `EnemySpawner`(시야 밖 생성·해금·상한), 피격 연출(`EnemyHitFeedback`), 경험치 젬·젬 풀, 공용 `Health`·`DamageGate`, 일반 몬스터 Prefab 3종 |
| 포함 | Issue #19 (M06~M12) : 보스·강화몹 스케줄(`EnemySpawner._bossSchedule`), 보스 패턴(`BossCharge`, `BossChargePattern`, `BossShootPattern`, `BossPatternSequencer`, `IBossPattern`), 보스 탄(`PFB_BossBullet`, 기존 `Projectile`·`ProjectilePool` 재사용), 보물 상자(`TreasureChest`, `TreasureChestSpawner`), 젬 Prefab별 풀, 원형 대량 스폰, 보스 4종·강화몹 3종·`PFB_EnemyGolem` Prefab |
| 포함 | 이슈 미등록 (브랜치 `art/fix-inven-ui`) : `EnemyMovement` 넉백(`AddKnockback`, `CalculateKnockbackVelocity`, 보스 넉백 저항), `EnemyPool`의 위치 지정 후 활성화(스폰 시 이전 위치 오피격 방지). 수치는 모두 임시값 |
| 제외 (후속) | 코드의 `shortcut` 주석 기준 : `EnemyManager`(static 활성 목록 이관, `Enemy.cs`). `EnemySpawner` 책임 분리(주의사항 l). 보물 상자 보상 내용(보상 화면 표시까지만 구현, `Docs/TechDocs/GameFlow.md`). M13(통합·Profiler·회귀 테스트). 그 밖의 후속 범위는 `Docs/Spec/Enemy/MonsterSystem.md` 참고 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `Enemy` (MonoBehaviour) | `ITargetable`·`IDamageable` 구현. 체력(`Health`) 소유, 피해 적용·사망 1회 처리, 사망 연출 시간(`_deathDuration`) 뒤 풀 반환. static 활성 목록 `ActiveEnemies`(무기 표적 후보), static 이벤트 `Died`, 인스턴스 이벤트 `Damaged`. 접촉 피해량 `ContactDamage`, 드롭 경험치 `ExperienceValue`, 드롭 젬 Prefab `GemPrefab`, 보물 상자 드롭 여부 `DropsTreasureChest`, 재화 지급량 `CurrencyReward`, 처치 시 클리어 여부 `ClearsSessionOnDeath` 노출 | `Health`, `EnemyMovement`·`Collider`(`Awake`에서 `GetComponent`), `Animator`(선택), `IObjectPool<Enemy>` |
| `EnemyPool` (MonoBehaviour) | 프리팹마다 `UnityEngine.Pool.ObjectPool<Enemy>`를 따로 둔다(`Dictionary<Enemy, ObjectPool<Enemy>>`). `Spawn(prefab, position, flowFieldManager, player)` : 풀에서 비활성 상태로 꺼내 위치 지정 → 활성화 → `Enemy.Initialize` 호출, 이어서 꺼낸 Enemy의 모든 `BossShootPattern`에 `_projectilePool`을 주입(`Initialize`). 프리팹의 풀은 그 프리팹의 첫 `Spawn` 때 생성 | `Enemy`, `FlowFieldManager`, `ProjectilePool`(`_projectilePool`), `BossShootPattern` |
| `EnemySpawner` (MonoBehaviour) | `GameSession.ElapsedTime` 기준 `_spawnInterval`마다 해금된 프리팹 중 균등 랜덤으로 `_spawnCountPerTick`마리를 시야 사다리꼴 바깥에 생성. 보스·강화몹은 `_bossSchedule` 순서대로 예정 시각에 1마리씩 시야 밖 격자 변 중점에 생성. 원형 대량 스폰 시각(`_ringSpawnTimes`)마다 플레이어 중심 원 위에 `_ringPrefab`을 일괄 생성. 동시 활성 상한 `_maxActiveEnemies`(일반·원형 스폰은 스케줄 길이만큼 예약 제외). 판정 함수 `IsUnlocked`, `HasCapacity`, `GetDistanceToBoundary`, `SelectBossSpawnIndex`, `GetRingPoint`(static) | `GameSession`, `EnemyPool`, `FlowFieldManager`(`CostField`), 플레이어 `Transform`, `Camera`, `Enemy.ActiveEnemies` |
| `BossCharge` (일반 클래스) | 보스 패턴 공용 상태 전환 `ChargeState`(`Chase` → `Warn` → `Charge` → `EndCharge`로 `Chase`). 범위 판정·경고 시간·쿨다운 타이머. `Tick(deltaTime, distanceToPlayer)`, `EndCharge()`, `Reset()` | 없음 |
| `IBossPattern` (interface) | 보스 패턴 계약. 이벤트 `Finished`(패턴 1회 실행 완료) 하나 | 없음 |
| `BossChargePattern` (MonoBehaviour, `IBossPattern`) | 돌진 패턴. 범위 안에 들어오면 방향을 고정해 경고선(LineRenderer) 표시, 경고 시간 후 고정 방향 직선 돌진, 거리 도달·이동 불가 타일에서 종료 | `Enemy`, `EnemyMovement`(`enabled`, `FlowFieldManager`, `Player`), `BossCharge`, `LineRenderer`, `CostField` |
| `BossShootPattern` (MonoBehaviour, `IBossPattern`) | 사격 패턴. 범위 안에 들어오면 예고 후 발사 순간의 플레이어 방향 기준으로 탄 n발 발사(1발 직선 / 360° 균등 / 부채꼴). 방향 계산 `GetSpreadDirection`(static) | `Enemy`, `EnemyMovement`(`enabled`, `Player`), `BossCharge`, `ProjectilePool`(`Initialize`로 주입), `Projectile`, `ProjectileSpawnContext` |
| `BossPatternSequencer` (MonoBehaviour) | `_patterns` 배열의 패턴 컴포넌트를 한 번에 하나만 켜고, `Finished` 후 `_interval`초 뒤 다음 패턴으로 순환 | `Enemy`, `IBossPattern`(`Behaviour`로 직렬화) |
| `TreasureChestSpawner` (MonoBehaviour) | `Enemy.Died` 구독. 모든 Enemy 사망 시 `Currency.Add(CurrencyReward)`로 재화를 지급한 뒤, `DropsTreasureChest`인 Enemy면 그 위치(높이 `_spawnHeight`)에 상자를 `Instantiate`하고 `Initialize`(상자에 `Currency` 전달). 풀 없음 | `TreasureChest`, `Enemy`, `Currency`, 플레이어 `Transform`, `LevelUpPanel` |
| `TreasureChest` (MonoBehaviour) | 매 프레임 플레이어가 획득 반경(`_pickupRadius`, XZ) 안이면 `Currency.Add(_currencyReward)`(기본 3, 필드 주석상 테스트값) → `LevelUpPanel.RequestChestReward()` 1회 후 `Destroy` | `ExperienceGem.IsInCollectRadius`(static), 플레이어 `Transform`, `LevelUpPanel`, `Currency` |
| `EnemyMovement` (MonoBehaviour, 넉백 부분만) | Flow Field 이동과 별도인 XZ 넉백 속도 `_knockbackVelocity` 소유. `AddKnockback(direction, strength)`로 누적, 매 `Update`에서 이동·선형 감속. 계산 함수 `CalculateKnockbackVelocity`(static). 이동 알고리즘(Flow Field·Separation)은 `Docs/TechDocs/Map.md` | `CostField`(`WorldToTile`, `IsInBounds`, `IsWalkable`). 호출자 `Projectile`(`Docs/TechDocs/Weapon.md`) |
| `TempEnemySpawner` (MonoBehaviour, 임시) | `TestMap` 전용. `Start`에서 `_spawnCount`마리를 자기 위치 기준 반경 `_spawnRadius` 원 위 랜덤 각도에 `EnemyPool`로 일괄 생성. 프리팹은 `_enemyPrefabs`에서 생성마다 균등 랜덤 | `EnemyPool`, `FlowFieldManager`, 플레이어 `Transform` |
| `EnemyHitFeedback` (MonoBehaviour) | `Enemy.Damaged` 구독. 피해마다 `_renderer.sharedMaterial`을 `_flashMaterial`로 `_flashDuration`초 교체, `DamageNumber.Spawn(위치, 피해량)`으로 피해 숫자 표시. 각각 `GameSettings` 설정이 꺼져 있으면 생략 | `Enemy`, `Renderer`, `Material`, `DamageNumbersPro.DamageNumber`, `GameSettings` |
| `Health` (일반 클래스) | 최대·현재 체력, `ApplyDamage`(0 미만으로 내려가지 않음, 처음 사망한 호출만 `true`), `Heal`(최대 체력 clamp, 사망 시 무시), `ResetToMax`. `Enemy`·`PlayerHealth`가 사용 | 없음 |
| `DamageGate` (일반 클래스) | 피해 1회 통과 후 무적 시간 동안 추가 통과를 막는 게이트(`TryPass`, `Tick`, `IsOpen`). 현재 `PlayerHealth`만 사용 | 없음 |
| `ExperienceGem` (MonoBehaviour) | 상태 `Idle` → `Scatter` → `Attract`. 도착 시 `PlayerExperience.AddExperience` 1회 후 풀 반환. 판정 함수 `IsInCollectRadius`, `GetScatterDirection`(static). 수명 없음 | `PlayerExperience`, `IObjectPool<ExperienceGem>` |
| `ExperienceGemPool` (MonoBehaviour) | `Enemy.Died` 구독. 사망 위치(높이는 `_spawnHeight`로 고정)에 젬을 꺼내 `Initialize` 호출. `ExperienceValue`가 0 이하면 생성하지 않음. 젬 Prefab마다 풀을 따로 둔다(`Dictionary<ExperienceGem, ObjectPool<ExperienceGem>>`, Enemy의 `GemPrefab` 또는 기본 `_gemPrefab`) | `ExperienceGem`, `PlayerExperience`, `Enemy` |

- 보스 패턴은 보스 Prefab 루트에 컴포넌트로 붙는다. 패턴 하나만 쓰는 보스(Salamander·Naga·Bishop)는 그 패턴이 항상 켜져 있고, 최종 보스(BlackKnight)만 `BossPatternSequencer`가 켜고 끈다.

## API / 이벤트 / 데이터 계약

### 이벤트

| 이벤트 | 소유 | 인자 | 발생 시점 | 구독자 |
|---|---|---|---|---|
| `Enemy.Died` (static) | `Enemy` | 죽은 `Enemy` | 피해로 처음 사망한 `TakeDamage` 호출 안에서 1회. 이동·Collider 정지, 반환 타이머 시작, Animator `Die` 트리거 뒤에 발생 | `ExperienceGemPool.SpawnGem`, `GameSession.HandleEnemyDied`, `TreasureChestSpawner.SpawnChest` (구독 순서 지정 없음) |
| `Enemy.Damaged` (인스턴스) | `Enemy` | 이번 피해량(`float`) | `TakeDamage`에서 `Health.ApplyDamage` 직후, 사망 처리(`Die`) 전. 사망하는 피해도 포함 | `EnemyHitFeedback.OnDamaged` (`OnEnable` 구독, `OnDisable` 해제) |
| `IBossPattern.Finished` (인스턴스) | `BossChargePattern`, `BossShootPattern` | 없음 | 돌진 패턴 : 돌진 종료(거리 도달·이동 불가 타일) 후 `EnemyMovement`를 다시 켠 직후. 사격 패턴 : 발사·쿨다운 시작(정지 보스는 `EnemyMovement` 재활성화) 직후. 사망으로 중지되면 발생하지 않는다 | `BossPatternSequencer.HandlePatternFinished` (현재 패턴에만 구독, 완료 또는 `OnDisable` 시 해제). Salamander·Naga·Bishop은 구독자 없음 |

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
| `GemPrefab` | `_gemPrefab`. 드롭할 젬 Prefab. 비어 있으면 `ExperienceGemPool._gemPrefab`(기본 젬)을 쓴다 (Issue #19) |
| `DropsTreasureChest` | `_dropsTreasureChest`. true면 사망 시 `TreasureChestSpawner`가 보물 상자를 생성한다 (중간 보스만, 필드 주석) |
| `CurrencyReward` | `_currencyReward` (`Min(0)`, 기본 0). 사망 시 `TreasureChestSpawner`가 지급하는 특수 재화량. 0이면 지급 없음 (엘리트만, 필드 주석) |
| `ClearsSessionOnDeath` | `_clearsSessionOnDeath`. true면 사망 시 `GameSession`이 클리어로 종료한다 (최종 보스만, 필드 주석. `Docs/TechDocs/GameFlow.md`) |
| `TakeDamage(damage)` | `IsAlive`가 false면 무시. 체력 감소 → `Damaged` → 이번 피해로 처음 사망했으면 `Die` |
| `Initialize(flowFieldManager, player, pool)` | 반환할 풀 저장, `EnemyMovement.Initialize` 호출. `EnemyPool.Spawn`이 꺼낸 직후 호출한다 |

- 활성화(`OnEnable`) 시 `SpawnId` 발급, 체력 최대로 복구, 반환 타이머 0, `EnemyMovement`·`Collider` 켬, 목록 등록.
- 사망(`Die`) 시 `EnemyMovement`·`Collider`를 끄고 `_deathRemaining = _deathDuration`, `_animator`가 있으면 `Die` 트리거, `Died` 발생. Collider가 꺼지므로 탄이 맞지 않는다.
- 반환(`Despawn`) : `Update`에서 `_deathRemaining`을 `Time.deltaTime`만큼 줄여 0 이하가 되면 실행한다. 풀이 있으면 `_pool.Release(this)`, 풀이 없으면(Scene에 직접 배치해 `Initialize`를 받지 않은 Enemy) `SetActive(false)`만 한다.
- Animator는 재활성화 시 기본 상태(`Move`)로 돌아가므로 `OnEnable`에서 되돌리지 않는다 (코드 주석).

### `Health`

- 생성자에서 현재 체력 = 최대 체력. 최대 체력이 0 이하면 생성 즉시 사망 상태다.
- `ApplyDamage(damage)` : 이미 사망 상태면 `false`를 반환하고 무시한다. 아니면 `max(0, 현재 - 피해)`로 줄이고 사망 여부를 반환한다. 따라서 `true`는 처음 사망한 호출에서만 나온다.
- `Heal(amount)` : 사망 상태면 무시한다. 아니면 `min(최대, 현재 + 회복량)`으로 늘린다. 호출부는 `PlayerHealth.Heal`(`GameFlow.md`)이다.
- `IsDead` = 현재 체력 ≤ 0.

### `DamageGate`

- `TryPass()` : 열려 있으면(`남은 시간 ≤ 0`) 남은 시간을 무적 시간으로 채우고 `true`, 아니면 `false`.
- `Tick(deltaTime)` : 남은 시간을 줄인다 (0 미만으로 내려가지 않음). 경과 시간이 무적 시간과 같으면 다시 열린다.
- 무적 시간 0이면 매번 통과한다.

### `EnemySpawner`

- `Update` 순서 : `GameSession.IsPlaying`일 때만 아래를 순서대로 실행한다. 세션이 끝나면(`IsPlaying == false`) 세 가지 생성 모두 멈춘다.
    1. `TrySpawnNextBoss(ElapsedTime)` (보스·강화몹, 아래)
    2. `TrySpawnNextRing(ElapsedTime)` (원형 대량 스폰, 아래)
    3. 일반 스폰 : `ElapsedTime ≥ _nextSpawnTime`이면 `_nextSpawnTime += _spawnInterval` 후 1회 생성(`SpawnTick`)
- 생성 주기 (일반) : `_nextSpawnTime`은 0에서 시작한다. 한 `Update`에 최대 1회만 생성한다.
- 해금 : `IsUnlocked(unlockTime, elapsedTime)` = `elapsedTime ≥ unlockTime`. 매 생성마다 `_spawnTable`에서 해금된 프리팹을 모으고, 마리마다 그중 하나를 균등 랜덤으로 고른다. 해금된 프리팹이 없으면 생성하지 않는다.
- 상한 (일반) : 마리마다 `HasCapacity(Enemy.ActiveEnemies.Count, _maxActiveEnemies - _bossSchedule.Length)`(= 활성 수 < 상한 − 스케줄 길이)를 확인하고, 상한이면 그 회차 생성을 끝낸다. 스케줄 길이만큼의 슬롯은 보스·강화몹이 등장하기 전이나 죽은 뒤에도 항상 예약된다 (코드 주석, Issue #19). 활성 수에는 사망 연출 중인 Enemy와 Scene에 직접 배치한 Enemy도 포함된다. 풀에 보관된(비활성) 개체는 포함되지 않는다.
- 위치 (`TryFindSpawnPosition`)
    1. 지면 높이 = 플레이어 Y. 카메라 뷰포트 네 모서리 광선을 이 높이의 수평면에 투사해 시야 사다리꼴의 XZ 꼭짓점을 구한다. 광선이 평면에 닿지 않으면 광선 위 `MAX_GROUND_DISTANCE`(200) 지점을 쓴다.
    2. 랜덤 방향마다 `GetDistanceToBoundary(플레이어 XZ, 방향, 꼭짓점)`로 경계까지 거리를 구한다.
    3. 거리가 0 이하(경계와 만나지 않음, 원점이 시야 밖)면 그 방향은 버리고 재시도한다.
    4. 생성 위치 = 플레이어 XZ + 방향 × (경계 거리 + `_spawnMargin`), Y = 지면 높이.
    5. 그 타일이 `CostField.IsWalkable`이면 확정, 아니면(격자 밖·장애물) 재시도한다.
    6. `_maxPositionAttempts`회 모두 실패하면 그 1마리만 건너뛴다.
- `GetDistanceToBoundary` : 볼록 다각형의 각 변과 방향 광선의 교점 중 가장 먼 거리를 반환한다. 변과 평행이면 그 변은 건너뛴다. 교점이 없으면 0.

#### 보스·강화몹 스케줄 (`_bossSchedule`, Issue #19)

- `_bossSchedule`은 `SpawnEntry[]`(프리팹, 등장 시각 `_unlockTime`)이다. 보스와 강화몹을 함께 담으며, 시각 오름차순으로 배치해야 한다 (필드 주석, 정렬 검사 없음). 필드 이름은 강화몹도 담지만 그대로 둔다 (사용자 결정, 주의사항 l).
- `TrySpawnNextBoss` : 다음 항목 인덱스 `_nextBossIndex` 하나만 본다. `IsUnlocked(그 항목 시각, ElapsedTime)`이 true면 위치를 찾고, 찾으면 `EnemyPool.Spawn` 후 인덱스를 1 올린다. 각 항목은 1회만 생성된다.
- 위치를 못 찾으면 인덱스를 유지하고 다음 프레임에 다시 시도한다 (누락 방지, 코드 주석). 따라서 앞 항목이 생성되어야 다음 항목을 볼 수 있다 (순서 큐, 주의사항 m). 한 `Update`에 최대 1마리만 생성한다.
- 상한 검사를 하지 않는다. 보스·강화몹은 활성 수와 관계없이 생성된다. 대신 일반·원형 스폰이 스케줄 길이만큼 슬롯을 예약한다.
- 이전 보스의 생존 여부와 관계없이 시각이 되면 다음 항목을 생성한다.
- 위치 (`TryFindBossSpawnPosition`)
    1. 지면 높이 = 플레이어 Y. 일반 스폰과 같은 방식으로 시야 사다리꼴 꼭짓점을 갱신한다.
    2. 후보 (`CollectBossCandidates`) : `CostField` 격자 4변의 중점 타일 (가운데 x, 위 끝 행) · (가운데 x, 0행) · (0열, 가운데 z) · (오른쪽 끝 열, 가운데 z). 가운데 = 폭·높이 ÷ 2(정수 나눗셈). `IsWalkable`인 타일만 넣고, 위치는 `TileToWorld`(타일 중심)의 XZ다. 가장자리 행·열은 `MapGenerator`의 장애물 금지 폭 안이라 격자 안쪽에서 바로 진입할 수 있다 (코드 주석, `Docs/TechDocs/Map.md`의 `배치 금지 타일`).
    3. `SelectBossSpawnIndex(플레이어 XZ, 후보, 꼭짓점)` : 시야 다각형 밖인 후보 중 플레이어와 가장 가까운 후보의 인덱스. 다각형 경계 위는 안쪽으로 본다. 모두 시야 안이면 -1 → 위치 없음.
- 생성 위치는 격자 안쪽 가장자리 타일이다. 격자 바깥에서 걸어 들어오는 연출은 없다.

#### 원형 대량 스폰 (`_ringSpawnTimes`, Issue #19)

- `TrySpawnNextRing` : 다음 회차 인덱스 `_nextRingIndex`의 시각(`_ringSpawnTimes`, 오름차순 배치, 필드 주석)이 지나면 인덱스를 먼저 1 올리고 `SpawnRing`을 1회 실행한다. 일부가 상한·장애물로 빠져도 그 회차를 다시 시도하지 않는다. 한 `Update`에 최대 1회차.
- `SpawnRing` : 그 순간 플레이어 XZ를 중심으로, 같은 프레임에 `_ringCount`마리를 `_ringPrefab`으로 생성한다 (분할 생성 없음).
    - 마리마다 `HasCapacity(활성 수, _maxActiveEnemies - _bossSchedule.Length)`를 확인하고, 상한이면 그 회차의 남은 생성을 멈춘다.
    - 위치 = `GetRingPoint(중심, i, _ringCount, _ringRadius)`, Y = 플레이어 Y. 그 타일이 `IsWalkable`이 아니면(장애물·격자 밖) 그 1마리만 건너뛴다.
    - 카메라 시야 조건은 없다. 반지름이 작으면 화면 안에 생성될 수 있다.
- `GetRingPoint(center, index, count, radius)` : 각도 = 2π × index / count. index 0은 월드 +X 방향이고 +X → +Z 순서로 배치된다. count가 0이면 0으로 나눠 NaN이다 (summary 주석, `_ringCount`는 `Min(1)`).

### 보스 패턴 (Issue #19)

#### `BossCharge` (공용 상태 전환)

| 상태 | `Tick(deltaTime, distanceToPlayer)` 동작 | 다음 상태 |
|---|---|---|
| `Chase` | 쿨다운을 `deltaTime`만큼 줄인다 (0 미만 없음) | 쿨다운 ≤ 0이고 플레이어까지 XZ 거리 ≤ 범위면 `Warn` (경고 시간 채움) |
| `Warn` | 경고 시간을 줄인다 | ≤ 0이면 `Charge` |
| `Charge` | 아무것도 하지 않는다 | 호출부가 `EndCharge()`를 부를 때까지 유지 |

- `EndCharge()` : `Chase`로 돌아가고 쿨다운을 채운다. `Reset()` : `Chase`, 경고·쿨다운 0.
- 범위·경고 시간·쿨다운은 생성자로 받는다. 두 패턴 모두 `Awake`에서 Inspector 값으로 한 번 만든다 (Play 중 Inspector 값을 바꿔도 반영되지 않음, 코드 확인).
- 사격 패턴은 `Warn` = 예고, `Charge` = 발사로 쓴다 (필드 주석).

#### `BossChargePattern` (돌진)

- `Update` 전제 : `Enemy.IsAlive`가 false면 경고선만 끄고 아무것도 하지 않는다 (돌진 종료 처리를 하지 않아 `Enemy.Die`가 끈 `EnemyMovement`를 다시 켜지 않음, 코드 주석). `EnemyMovement.FlowFieldManager`·`CostField`·`Player` 중 하나라도 `null`이면 아무것도 하지 않는다.
- `Chase` → `Warn` 진입 프레임 (`StartWarning`)
    - 돌진 방향 = 그 순간 보스→플레이어 XZ 정규화 방향(스냅샷). 같은 위치면 현재 정면.
    - `EnemyMovement.enabled = false` (추적 정지), 보스를 돌진 방향으로 회전.
    - 경고선 : `_warningLine`의 정점 0 = 보스 위치 + 위로 `_warningLineHeight`, 정점 1 = 정점 0 + 방향 × `_chargeDistance`. `enabled = true`.
- `Charge` 동안 매 프레임 (`MoveCharge`)
    - 진입 프레임에 경고선을 끈다.
    - 이동량 = min(`_chargeSpeed` × deltaTime, 남은 돌진 거리). 다음 위치의 타일이 `IsWalkable`이 아니면(장애물·격자 밖) 이동하지 않고 종료한다. 아니면 `transform.position`을 직접 옮기고, 누적 거리가 `_chargeDistance`에 닿으면 종료한다.
    - 돌진 중 플레이어를 다시 추적하지 않는다.
- 종료 (`EndCharge`) : `BossCharge.EndCharge()`(쿨다운 시작) → `EnemyMovement.enabled = true` → `Finished`.
- 돌진 피해는 별도 판정이 없다. 기존 접촉 피해(`PlayerHealth`의 접촉 판정, `ContactDamage`)가 그대로 적용된다 (`Docs/TechDocs/GameFlow.md`).
- `_chargeSpeed`는 `Min(0.01)`이다. 0이면 돌진이 끝나지 않는다 (필드 주석).
- `OnEnable` : `BossCharge.Reset()`, 경고선 끔. `EnemyMovement` 재활성화는 `Enemy.OnEnable`이 맡는다 (코드 주석).

#### `BossShootPattern` (사격)

- `Update` 전제 : `Enemy.IsAlive`가 false면 아무것도 하지 않는다. `_projectilePool`(미주입) 또는 `EnemyMovement.Player`가 `null`이면 아무것도 하지 않는다.
- `Chase` → `Warn` 진입 프레임 (`StartWarning`) : `_stopWhileWarning`이 true일 때만 `EnemyMovement.enabled = false`, 플레이어 방향으로 회전. false면 아무것도 하지 않고 이동을 계속한다 (회전은 `EnemyMovement`가 맡음). 경고선 등 예고 표시는 없다.
- `Charge` 진입 프레임 (`Fire`)
    - 중심 방향 = 발사 순간의 보스→플레이어 XZ 방향 (같은 위치면 현재 정면). 예고 시작 때의 방향이 아니다.
    - 생성 위치 = 보스 위치 + 위로 `_fireHeight`. `_projectileCount`발마다 `GetSpreadDirection(중심, i, 개수, _spreadAngle)` 방향으로 `ProjectilePool.Fire(_projectilePrefab, context)`. context : 탄속 `_projectileSpeed`, 피해 `_projectileDamage`, 최대 거리 `_projectileMaxDistance`, 최대 생존 시간 `_projectileMaxLifetime`, `requireCameraVisibility: false`, `maxUniqueTargetsHit: 1`.
    - 이어서 `BossCharge.EndCharge()`(쿨다운 시작), `_stopWhileWarning`이면 `EnemyMovement.enabled = true`, `Finished`.
- `GetSpreadDirection(centerDirection, index, count, spreadAngle)` (static) : 중심 방향을 XZ로 평탄화·정규화한 뒤 Y축 회전한다.
    - count ≤ 1 : 중심 방향.
    - spreadAngle ≥ 360 : `360 / count × index`도 (index 0 = 중심, 균등 분할).
    - 그 밖 : `spreadAngle / (count − 1) × index − spreadAngle / 2`도 (양 끝 포함 균등 부채꼴).
    - `_spreadAngle`은 `Range(0, 360)`이다.
- 무기 쪽 `WeaponTargeting.GetSpreadDirection`과는 별개의 함수다 (360° 처리 방식이 다름, `Docs/TechDocs/Weapon.md` 주의사항 j).
- `Initialize(projectilePool)` : `EnemyPool.Spawn`이 매 생성마다 호출한다. 미주입이면 패턴이 동작하지 않는다.
- `OnEnable` : `BossCharge.Reset()`.

#### 보스 탄 적중

- 탄은 무기와 같은 `Projectile`·`ProjectilePool`을 쓴다 (`Docs/TechDocs/Weapon.md`의 `투사체 적중·반환`). `PFB_BossBullet`의 `_hitMask`가 Player(7), `_blockMask`가 Obstacle(8)이라 플레이어만 맞고 장애물 레이어에 막힌다.
- `PFB_Player` 루트는 Layer Player(7)이고 `CharacterController`와 `PlayerHealth`를 가진다. 탄이 맞은 Collider의 부모 방향 `IDamageable`이 `PlayerHealth`이므로 `PlayerHealth.TakeDamage`를 거치며, 접촉 피해와 같은 `DamageGate`(무적 시간)를 공유한다 (`Docs/TechDocs/GameFlow.md`의 `PlayerHealth`).
- 위 경로는 코드·Prefab으로 확인했다. Play 확인 항목별 결과는 이 문서 작성 입력에 포함되지 않았다.

#### `BossPatternSequencer` (최종 보스)

- `_patterns` : `Behaviour[]`. 인터페이스는 Inspector 직렬화가 안 되므로 `Behaviour`로 받는다 (코드 주석). 실행 순서대로 배치하며 같은 컴포넌트를 여러 번 넣을 수 있다.
- `OnValidate` : `IBossPattern`이 아닌 원소는 경고 로그(`_patterns[i]의 {타입}은(는) IBossPattern이 아니라 비움`)를 남기고 `null`로 비운다. 편집 시점 검사이며 실행 중 검사는 없다.
- `OnEnable` : 모든 패턴 `enabled = false` → 인덱스 0 → `StartPattern`. 풀 재사용 시에도 첫 패턴부터 다시 시작한다.
- `StartPattern` : 현재 패턴의 `Finished`를 구독하고 `enabled = true`.
- `HandlePatternFinished` : 구독 해제, 그 패턴 `enabled = false`, 대기 시간 = `_interval`.
- `Update` : 패턴 실행 중이거나 Enemy가 죽었으면 아무것도 하지 않는다. 대기 중이면 대기 시간을 줄이고 0 이하에서 다음 인덱스(배열 끝이면 0)로 `StartPattern`. 대기 중에는 모든 패턴이 꺼져 있어 `EnemyMovement` 추적만 한다.
- `OnDisable` : 현재 패턴 구독만 해제한다 (같은 컴포넌트 중복 배치 시 구독이 쌓이지 않도록 현재 패턴만 구독, 코드 주석).
- 패턴이 다시 켜질 때 그 패턴의 `OnEnable`이 `BossCharge.Reset()`을 하므로 쿨다운이 0으로 돌아간다. 그래서 시퀀서 아래에서는 각 패턴의 `_cooldown`이 다음 실행을 늦추지 않고, 패턴 사이 간격은 `_interval`과 플레이어가 범위에 들어오기까지의 시간으로 정해진다 (코드 확인, Play 미검증).
- `_patterns`가 비어 있거나 `null` 원소가 있으면 `OnEnable`·`StartPattern`에서 예외가 난다 (`null` 검사 없음).
- `Awake`에서 `GetComponent<Enemy>()`로 Enemy를 찾는다 (reviewer 권고가 있었으나 사용자 결정으로 유지).

#### 사망·풀 재사용

- 사망 : `Enemy.Die`가 `EnemyMovement`를 끈다. 각 패턴은 `IsAlive == false`를 보고 멈추며(돌진 패턴은 경고선도 끔) `EnemyMovement`를 다시 켜지 않는다. 시퀀서도 다음 패턴으로 넘어가지 않는다.
- 재사용 : `Enemy.OnEnable`이 `EnemyMovement`를 켜고, 패턴 `OnEnable`이 상태를 `Chase`로 되돌리며, 시퀀서 `OnEnable`이 첫 패턴부터 다시 시작한다. 같은 GameObject의 `OnEnable` 사이 순서는 지정되지 않았다.

### 보물 상자 (`TreasureChestSpawner`, `TreasureChest`, Issue #19)

- `TreasureChestSpawner` : `OnEnable`/`OnDisable`에서 `Enemy.Died` 구독/해제. `SpawnChest(enemy)` : 먼저 `_currency.Add(enemy.CurrencyReward)`로 재화를 지급한다 (`Currency.Add`는 0 이하를 무시하므로 일반 몬스터·보스는 변화 없음). 이어서 `DropsTreasureChest`가 false면 끝낸다. 위치 = Enemy 위치(Y는 `_spawnHeight`), 회전 없음, 부모 `_chestParent`로 `Instantiate` 후 `Initialize(_player, _levelUpPanel, _currency)`.
- `Died`는 첫 사망 시 1회만 발생하므로 엘리트 재화도 1회만 지급된다. 메서드 이름 `SpawnChest`는 재화 지급을 포함하지만 그대로 둔다 (사용자 결정).
- 풀을 쓰지 않는다. 한 판 최대 3개(중간 보스 수)라서다 (코드 주석).
- `TreasureChest.Update` : `_player`가 `null`(미초기화, Scene 직접 배치 등)이면 판정하지 않는다. `ExperienceGem.IsInCollectRadius(상자 위치, 플레이어 위치, _pickupRadius)`(XZ 거리, 경계 포함)가 true면 `_currency.Add(_currencyReward)` → `_levelUpPanel.RequestChestReward()` → `Destroy(gameObject)`.
- 중복 요청 방지 플래그가 없다. 요청 경로가 이 `Update` 하나뿐이고, `Destroy`는 그 프레임이 끝난 뒤 적용되어 다음 `Update`가 오지 않기 때문이다 (코드 주석).
- 상자에는 수명이 없다. 획득 전까지 남는다.
- 보상 화면의 회차 처리(대기 중인 레벨업 우선, 카드 5장)는 `Docs/TechDocs/GameFlow.md`의 `LevelUpPanel`.

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

- `OnEnable`/`OnDisable`에서 `Enemy.Died` 구독/해제.
- 젬 Prefab마다 풀을 따로 둔다 (`Dictionary<ExperienceGem, ObjectPool<ExperienceGem>>`, 외형이 다른 젬끼리 섞여 재사용되지 않도록, 코드 주석). 각 Prefab의 풀은 그 Prefab의 첫 생성 때 만든다 (Issue #19 이전의 `Awake` 단일 풀 생성은 없어짐).
- `SpawnGem(enemy)` : `enemy.ExperienceValue ≤ 0`이면 무시. Prefab = `enemy.GemPrefab`이 있으면 그것, 없으면 `_gemPrefab`(기본 젬). 위치 = Enemy 위치(Y는 `_spawnHeight`), 회전 초기화 후 `Initialize(_playerExperience, enemy.ExperienceValue, 그 Prefab의 풀)`.

### 넉백 (`EnemyMovement`)

| 필드 (임시값) | 내용 | Prefab 값 |
|---|---|---|
| `_knockbackDeceleration` | 넉백 속도의 초당 감소량 (`Min(0)`) | 코드 기본값 20. 보스 4종 Prefab 20 |
| `_maxKnockbackSpeed` | 누적 넉백 속도 크기 상한 (`Min(0)`) | 코드 기본값 10. 보스 4종 Prefab 10 |
| `_knockbackResistance` | 넉백 감소율 (`Range(0, 1)`, 0 = 저항 없음, 1 = 넉백 무효) | 코드 기본값 0. `PFB_BossSalamander`·`PFB_BossNaga`·`PFB_BossBishop`·`PFB_BossBlackKnight` 0.7 |

- 일반 몬스터·강화몹 Prefab에는 세 필드의 직렬화 값이 없어 코드 기본값(20 / 10 / 0)을 쓴다 (Prefab에 키 없음 확인).
- `AddKnockback(direction, strength)` : `_knockbackVelocity = CalculateKnockbackVelocity(현재, direction, strength, _knockbackResistance, _maxKnockbackSpeed)`.
- `CalculateKnockbackVelocity(current, direction, strength, resistance, maxSpeed)` = `ClampMagnitude(current + XZ 정규화(direction) × strength × (1 − resistance), maxSpeed)`. 방향의 Y 성분은 버리며, XZ가 0이면 현재 속도를 그대로 둔다(상한만 적용). 적중마다 더하므로 샷건 펠릿이 여러 발 맞으면 누적되고, 크기는 상한으로만 제한된다 (점감 없음). 보스 저항은 더하기 전 강도에 곱한다.
- 적용 (`Update` → `ApplyKnockback`, Flow 이동보다 먼저) : 정지 거리 안 등 조기 반환 경로에서도 밀려나도록 먼저 적용한다 (코드 주석). 속도가 0이면 건너뛴다.
    1. 다음 위치 = 현재 위치 + 넉백 속도 × `Time.deltaTime`.
    2. 현재 타일이 격자 안이고 다음 위치 타일이 이동 불가(`!IsWalkable`)면 넉백 속도를 0으로 하고 끝낸다 (이번 프레임 이동 없음). 격자 밖에서는 기존 이동처럼 타일 검사를 생략한다.
    3. 그 밖에는 다음 위치로 이동하고 넉백 속도를 `MoveTowards(…, 0, _knockbackDeceleration × deltaTime)`로 선형 감속한다.
- 넉백 이동은 Flow·Separation 이동(`direction × moveDistance`)과 별도로 위치에 더해진다. Flow 방향·`_velocity`는 바꾸지 않는다.
- 일시정지 : `Time.deltaTime` 기준이라 `timeScale` 0이면 이동·감속이 모두 멈춘다 (코드 주석).
- `FlowFieldManager`·`FlowField`가 없으면 `Update`가 먼저 반환해 넉백도 적용되지 않는다. `EnemyMovement`가 꺼진 동안(사망, 보스 돌진·사격 패턴 중)에는 `Update`가 돌지 않아 넉백이 멈춘 채 남고, 다시 켜지면 이어서 적용된다 (코드 기준, Play 미검증).
- 초기화 : `Initialize`에서 `_knockbackVelocity`를 0으로 한다 (풀 재사용 시 이전 생애의 넉백 제거, 코드 주석).
- 한계 (코드의 `shortcut` 주석) : 중심점의 다음 타일만 검사해 ㄱ자 모서리 대각 통과·몸체 일부 벽 겹침이 가능하다. 문제되면 몸체 반폭 기준 검사로 교체한다.

### 풀 공통 (`EnemyPool`, `ExperienceGemPool`)

- `ObjectPool` 기본 동작 : 비어 있으면 새로 생성, `_maxSize`를 넘겨 반환된 개체는 `Destroy`. `collectionCheck: true`.
- 반환 시 `SetActive(false)`. `ExperienceGemPool`은 꺼낼 때(`actionOnGet`) `SetActive(true)`.
- `EnemyPool` 꺼내기 : `actionOnGet`이 없다. 새 개체는 `Instantiate` 직후 `SetActive(false)`로 비활성 생성하고(활성 프리팹이라 생성 순간 부모 원점에서 활성화·OnEnable이 실행되지만 같은 호출에서 바로 비활성화해 물리 판정 전에 해제), `Spawn`이 `SetPositionAndRotation(위치, identity)` → `SetActive(true)` 순서로 위치를 먼저 정한 뒤 활성화한다. 프로젝트의 Auto Sync Transforms가 꺼져 있어, 먼저 켜면 같은 프레임 물리 판정이 이전 위치(사망 지점 등)에 남아 오피격이 생기기 때문이다 (`ProjectSettings/DynamicsManager.asset`의 `m_AutoSyncTransforms: 0`).
- 두 풀 모두 `_defaultCapacity`·`_maxSize`는 프리팹별 풀마다 각각 적용된다.
- `EnemyPool.Spawn`은 `Enemy.Initialize` 뒤 `GetComponents<BossShootPattern>(재사용 List)`로 사격 패턴을 모아 `Initialize(_projectilePool)`을 호출한다. 스폰마다 리스트를 새로 할당하지 않는다 (코드 주석). 사격 패턴이 없는 Enemy는 빈 목록이다.

## 호출 순서 또는 데이터 흐름

```
[매 프레임 Update] EnemySpawner   (IsPlaying일 때만)
  ├─ TrySpawnNextBoss(ElapsedTime)
  │    _bossSchedule[_nextBossIndex] 시각 지남?
  │    └─ TryFindBossSpawnPosition : 격자 4변 중점(통행 가능) 중 시야 밖 + 플레이어와 가장 가까운 곳
  │         ├─ 실패 → 인덱스 유지, 다음 프레임 재시도
  │         └─ 성공 → EnemyPool.Spawn(항목 프리팹, …) → _nextBossIndex++   (상한 검사 없음)
  ├─ TrySpawnNextRing(ElapsedTime)
  │    _ringSpawnTimes[_nextRingIndex] 지남? → _nextRingIndex++ → SpawnRing
  │    └─ for _ringCount (같은 프레임)
  │         ├─ HasCapacity(ActiveEnemies.Count, _maxActiveEnemies - _bossSchedule.Length) 아니면 종료
  │         ├─ GetRingPoint 타일이 통행 불가 → 이 1마리 건너뜀
  │         └─ EnemyPool.Spawn(_ringPrefab, …)
  └─ ElapsedTime ≥ _nextSpawnTime
     └─ _nextSpawnTime += _spawnInterval → SpawnTick(ElapsedTime)
          ├─ CollectUnlockedPrefabs (UnlockTime ≤ ElapsedTime)
          └─ for _spawnCountPerTick
               ├─ HasCapacity(ActiveEnemies.Count, _maxActiveEnemies - _bossSchedule.Length) 아니면 종료
               ├─ TryFindSpawnPosition 실패 → 이 1마리 건너뜀
               └─ EnemyPool.Spawn(랜덤 해금 프리팹, 위치, FlowFieldManager, 플레이어)

EnemyPool.Spawn(prefab, 위치, FlowFieldManager, 플레이어)
  ├─ (프리팹 풀 없으면 생성) → pool.Get()   (비활성 개체 반환. 새 개체면 Instantiate(Awake·OnEnable) 직후 SetActive(false))
  ├─ SetPositionAndRotation(위치, identity)   (활성화 전, Auto Sync Transforms 꺼짐 대비)
  ├─ SetActive(true) → OnEnable
  │      Enemy.OnEnable : SpawnId 발급, 체력 복구, 이동·Collider 켬, ActiveEnemies 등록
  │      보스 패턴 OnEnable : BossCharge.Reset (돌진 패턴은 경고선 끔), 시퀀서는 첫 패턴만 켬
  ├─ Enemy.Initialize(FlowFieldManager, 플레이어, pool) → EnemyMovement.Initialize (넉백 속도 0)
  └─ GetComponents<BossShootPattern> → 각 Initialize(_projectilePool)

[매 프레임 Update] 보스 패턴 (켜진 것만, IsAlive일 때만)
  BossCharge.Tick(deltaTime, 플레이어 XZ 거리)
  ├─ Chase → Warn : 돌진 = 방향 스냅샷·EnemyMovement 끔·경고선 켬 / 사격 = (_stopWhileWarning이면) EnemyMovement 끔
  ├─ Warn → Charge
  │    ├─ 돌진 : 경고선 끔 → 매 프레임 직선 이동 → (거리 도달 또는 다음 타일 통행 불가) EndCharge
  │    └─ 사격 : 발사 순간 플레이어 방향으로 ProjectilePool.Fire × _projectileCount → EndCharge
  └─ EndCharge : 쿨다운 시작 → EnemyMovement 켬 (사격은 _stopWhileWarning일 때만) → Finished
       └─ (BlackKnight) BossPatternSequencer.HandlePatternFinished : 패턴 끔 → _interval 대기 → 다음 패턴 켬 (순환)

[보스 탄 적중] Projectile → PlayerHealth.TakeDamage → DamageGate.TryPass (접촉 피해와 공유)   (GameFlow.md)

[피격] Projectile 적중 → IDamageable.TakeDamage(피해)   (Weapon.md `투사체 적중·반환`)
  └─ (탄 넉백 > 0 && 피해 후 IsAlive) EnemyMovement.AddKnockback(탄 진행 방향, 넉백) → 다음 Update의 ApplyKnockback에서 이동·감속
Enemy.TakeDamage
  ├─ IsAlive 아니면 무시
  ├─ Health.ApplyDamage(피해)
  ├─ Damaged(피해) → EnemyHitFeedback : 재질 교체, 피해 숫자 Spawn (각각 GameSettings가 켜져 있을 때만)
  └─ 처음 사망이면 Die()
       ├─ EnemyMovement·Collider 끔, _deathRemaining = _deathDuration
       ├─ Animator Die 트리거 (있으면)
       └─ Died(this)
            ├─ ExperienceGemPool.SpawnGem → (ExperienceValue > 0) 젬 Prefab(GemPrefab 또는 기본) 풀 Get → Initialize (Idle)
            ├─ GameSession.HandleEnemyDied → IsPlaying이면 KillCount++, ClearsSessionOnDeath면 End(Cleared)
            └─ TreasureChestSpawner.SpawnChest → Currency.Add(CurrencyReward) (0 이하 무시) → DropsTreasureChest면 상자 Instantiate → Initialize(…, Currency)
       (보스 패턴·시퀀서는 이후 자기 Update에서 IsAlive false를 보고 정지)

[매 프레임 Update] TreasureChest
  플레이어가 _pickupRadius(XZ) 안 → Currency.Add(_currencyReward) → LevelUpPanel.RequestChestReward → Destroy   (보상 화면은 GameFlow.md)

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
- Issue #19 Override Controller : `EnemySalamander`, `EnemyNaga`, `EnemyBishop`, `EnemyBlackKnight`(보스), `EnemyElitePlant`, `EnemyEliteDragon`, `EnemyEliteOrc`(강화몹), `EnemyGolem`. 모두 `EnemyMushroom.controller`를 원본으로 `Move`·`Die` 클립 2개를 교체한다. 보스 패턴(경고·돌진·사격) 전용 애니메이션 상태는 없다.

### 보스 Prefab (`Assets/Prefabs/Enemy/PFB_Boss*.prefab`, Issue #19)

```
PFB_BossSalamander            (Layer Enemy(6), CapsuleCollider, EnemyMovement, Enemy, EnemyHitFeedback, BossChargePattern)
├─ AimPoint                   (로컬 위치 (0, 1.2, 0))
├─ SalamanderPADefault        (외형 모델 Prefab 인스턴스, Animator, Renderer)
└─ ChargeWarningLine          (LineRenderer : 꺼짐, MAT_ChargeWarning, 폭 0.6, World Space, 정점 2개)

PFB_BossNaga / PFB_BossBishop (Layer Enemy(6), CapsuleCollider, EnemyMovement, Enemy, EnemyHitFeedback, BossShootPattern)
├─ AimPoint                   (로컬 위치 (0, 1.2, 0))
└─ NagaWizardPADefault / BishopKnightPADefault

PFB_BossBlackKnight           (Layer Enemy(6), CapsuleCollider, EnemyMovement, Enemy, EnemyHitFeedback,
│                              BossChargePattern, BossShootPattern ×2, BossPatternSequencer)
├─ AimPoint                   (로컬 위치 (0, 1.2, 0))
├─ BlackKnightPADefault
└─ ChargeWarningLine          (Salamander와 같은 설정)
```

- 자식 순서는 Salamander·BlackKnight만 Transform 순서로 확인했다. Naga·Bishop은 자식 구성만 확인했다.

| Prefab | `_maxHealth` | `_moveSpeed` | `_contactDamage` | `_dropsTreasureChest` | `_clearsSessionOnDeath` | Animator Controller |
|---|---|---|---|---|---|---|
| `PFB_BossSalamander` | 300 | 5 | 20 | 1 | 0 | `EnemySalamander.overrideController` |
| `PFB_BossNaga` | 600 | 2.5 | 20 | 1 | 0 | `EnemyNaga.overrideController` |
| `PFB_BossBishop` | 900 | 2.5 | 20 | 1 | 0 | `EnemyBishop.overrideController` |
| `PFB_BossBlackKnight` | 1200 | 4.5 | 20 | 0 | 1 | `EnemyBlackKnight.overrideController` |

보스 4종 공통 값

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `CapsuleCollider` | 반지름 / 높이 / 중심 | 1 / 2.5 / (0, 1.25, 0), 트리거 아님 (루트) |
| `EnemyMovement` | `_stopDistance` / `_separationRadius` / `_separationWeight` / `_enemyLayer` | 1.5 / 2 / 1 / Enemy |
| `Enemy` | `_deathDuration` | 2 |
| | `_experienceValue` | 0 (젬 미드롭) |
| | `_gemPrefab` | Prefab에 직렬화된 값 없음 (코드 기본 `null`) |
| `EnemyHitFeedback` | 연결·값 | 일반 몬스터 3종과 같다 (`MAT_HitFlash`, `PFB_DamageNumberComic`, `_numberPoint` = `AimPoint`, `_flashDuration` 0.1) |

보스 패턴 값 (Prefab 파일 기준, 사용자가 Play 후 조정한 값 포함)

| Prefab · 컴포넌트 | 값 |
|---|---|
| Salamander `BossChargePattern` | `_chargeRange` 8, `_warnDuration` 1, `_chargeSpeed` 15, `_chargeDistance` 10, `_cooldown` 2, `_warningLineHeight` 0.1 |
| Naga `BossShootPattern` | `_stopWhileWarning` 0 (이동하며 발사), `_detectRange` 12, `_warnDuration` 0.6, `_cooldown` 1.5, `_projectileCount` 1, `_spreadAngle` 0, `_projectileSpeed` 6, `_projectileDamage` 15, `_projectileMaxDistance` 30, `_projectileMaxLifetime` 10, `_fireHeight` 1 |
| Bishop `BossShootPattern` | `_stopWhileWarning` 1 (정지 후 발사), `_detectRange` 12, `_warnDuration` 0.5, `_cooldown` 3, `_projectileCount` 8, `_spreadAngle` 360, `_projectileSpeed` 5, `_projectileDamage` 15, `_projectileMaxDistance` 30, `_projectileMaxLifetime` 10, `_fireHeight` 1 |
| BlackKnight `BossChargePattern` | `_chargeRange` 8, `_warnDuration` 0.8, `_chargeSpeed` 18, `_chargeDistance` 12, `_cooldown` 2, `_warningLineHeight` 0.1 |
| BlackKnight `BossShootPattern` (직선) | `_stopWhileWarning` 0, `_detectRange` 12, `_warnDuration` 0.6, `_cooldown` 2.5, `_projectileCount` 1, `_spreadAngle` 0, `_projectileSpeed` 8, `_projectileDamage` 15, `_projectileMaxDistance` 30, `_projectileMaxLifetime` 10, `_fireHeight` 1 |
| BlackKnight `BossShootPattern` (부채꼴) | `_stopWhileWarning` 0, `_detectRange` 12, `_warnDuration` 0.6, `_cooldown` 2.5, `_projectileCount` 3, `_spreadAngle` 40, `_projectileSpeed` 6, `_projectileDamage` 15, `_projectileMaxDistance` 30, `_projectileMaxLifetime` 10, `_fireHeight` 1 |
| BlackKnight `BossPatternSequencer` | `_patterns` = [돌진, 직선, 돌진, 부채꼴] (돌진은 같은 `BossChargePattern` 컴포넌트 2회 배치), `_interval` 1.5 |

- 모든 `BossShootPattern`의 `_projectilePrefab`은 `PFB_BossBullet`이다. 사격 값은 코드 주석상 수치미정이다.
- 돌진 패턴의 `_warningLine`은 자식 `ChargeWarningLine`의 LineRenderer다. 필드 주석의 전제(`useWorldSpace` true, `positionCount` 2, 평소 `enabled` false)를 Prefab이 충족한다.
- BlackKnight의 패턴 컴포넌트는 Prefab에서 모두 켜진 상태(`m_Enabled` 1)로 저장되어 있다. 활성화 시 `BossPatternSequencer.OnEnable`이 모두 끄고 첫 패턴만 켠다.
- 시퀀서 아래의 패턴 `_cooldown`은 다음 실행을 늦추지 않는다 (`BossPatternSequencer` 절 참고, Play 미검증).
- BlackKnight 사격 패턴의 `_detectRange`(12) 안에 플레이어가 들어올 때까지 그 패턴이 끝나지 않으므로 다음 패턴으로 넘어가지 않는다 (코드 확인).

### 강화몹 Prefab (`Assets/Prefabs/Enemy/PFB_EnemyElitePlant|Dragon|Orc.prefab`, Issue #19)

- 구성은 일반 몬스터와 같다 (루트 Layer Enemy(6), `CapsuleCollider`, `EnemyMovement`, `Enemy`, `EnemyHitFeedback`, 자식 `AimPoint`·외형 모델). 패턴 컴포넌트는 없다.

| Prefab | 외형 모델 (스케일) | `_maxHealth` | `_moveSpeed` | `_contactDamage` | Animator Controller | `_bossSchedule` 시각 |
|---|---|---|---|---|---|---|
| `PFB_EnemyElitePlant` | `MonsterPlantPADefault` (0.75) | 150 | 1.5 | 15 | `EnemyElitePlant.overrideController` | 180 (3:00) |
| `PFB_EnemyEliteDragon` | `DragonPADefault` (0.85) | 300 | 1.7 | 20 | `EnemyEliteDragon.overrideController` | 480 (8:00) |
| `PFB_EnemyEliteOrc` | `OrcPADefault` (스케일 오버라이드 없음) | 500 | 1.8 | 25 | `EnemyEliteOrc.overrideController` | 780 (13:00) |

| 컴포넌트 | 필드 | 3종 공통 값 |
|---|---|---|
| `CapsuleCollider` | 반지름 / 높이 / 중심 | 0.8 / 2 / (0, 1, 0) |
| `AimPoint` | 로컬 위치 | (0, 1, 0) |
| `EnemyMovement` | `_stopDistance` / `_separationRadius` / `_separationWeight` / `_enemyLayer` | 1.35 / 1.6 / 1 / Enemy |
| `Enemy` | `_deathDuration` | 1 |
| | `_experienceValue` | 10 |
| | `_gemPrefab` | `PFB_ExperienceGemElite` (금색 젬) |
| | `_dropsTreasureChest` / `_clearsSessionOnDeath` | 0 / 0 |
| | `_currencyReward` | 1 (Issue #37) |
| `EnemyHitFeedback` | 연결·값 | 일반 몬스터 3종과 같다 |

- `_currencyReward`는 강화몹 3종만 1이다. 다른 Enemy Prefab은 직렬화 값이 없어 코드 기본값 0이다.

### `Assets/Prefabs/Enemy/PFB_EnemyGolem.prefab` (원형 대량 스폰용, Issue #19)

- 구성은 일반 몬스터와 같다. 외형 `GolemPADefault`(스케일 0.85), Animator `EnemyGolem.overrideController`.
- `CapsuleCollider` 0.75 / 1.6 / (0, 0.8, 0), `AimPoint` (0, 0.8, 0).
- `EnemyMovement` : `_moveSpeed` 1.3, `_stopDistance` 1.3, `_separationRadius` 1.2, `_separationWeight` 0.1, `_enemyLayer` Enemy.
- `Enemy` : `_maxHealth` 40, `_deathDuration` 1, `_contactDamage` 10, `_experienceValue` 1, `_gemPrefab` 비어 있음(기본 젬), `_dropsTreasureChest`·`_clearsSessionOnDeath` 0.
- `_spawnTable`에 없고 `EnemySpawner._ringPrefab`으로만 생성된다.

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

### `Assets/Prefabs/Combat/PFB_ExperienceGemElite.prefab` (강화몹 금색 젬, Issue #19)

- `PFB_ExperienceGem`과 같은 구성·값이다 (`ExperienceGem` 값 60 / 0.25 / 1.2 / 10 / 0.1, `Ruby2` 스케일 0.3, `isFloating`·`isScaling` 0).
- 차이는 `Ruby2`의 재질 오버라이드 하나다 : `m_Materials[0]` = `Assets/Imports/Other/BTM_Assets/BTM_Items_Gems/Materials/BaseMaterial_Yellow.mat`.
- 강화몹 3종의 `Enemy._gemPrefab`이 이 Prefab을 가리킨다. `ExperienceGemPool`은 기본 젬과 다른 풀로 관리한다.

### `Assets/Prefabs/Enemy/PFB_BossBullet.prefab` (Issue #19)

```
PFB_BossBullet                (Layer Default, Projectile)
└─ Visual                     (MeshFilter Unity 내장 메시 fileID 10207, MeshRenderer MAT_BossBullet · 그림자 끔, 로컬 스케일 0.5)
```

| 필드 | 값 |
|---|---|
| `_hitRadius` | 0.3 |
| `_hitMask` | Player(7) |
| `_blockMask` | Obstacle(8) |

- 탄속·피해·거리·수명은 Prefab이 아니라 발사하는 `BossShootPattern`의 값이다.
- 무기 탄 3종(`_blockMask` Nothing)과 달리 장애물(Layer Obstacle)에 막힌다. 장애물 모델은 `MapGenerator`가 Layer Obstacle로 바꾸고, 차단 Box·경계 벽은 Layer Default라 막지 않는다 (`Docs/TechDocs/Map.md`).
- 내장 메시 fileID 10207의 종류(Sphere 여부)는 파일에서 이름으로 확인할 수 없었다.

### `Assets/Prefabs/Combat/PFB_TreasureChest.prefab` (Issue #19)

```
PFB_TreasureChest             (Layer Default, TreasureChest)
└─ Silver_Chest               (Assets/Imports/Other/Low Poly Toon Chests/Prefabs/Silver_Chest 인스턴스, 로컬 위치 Y 0.57)
```

- `TreasureChest._pickupRadius` 1.5 (XZ 거리, 필드 주석상 수치 미정).
- Collider가 없다. 획득은 거리 판정만 쓴다.
- 모델의 로컬 Y 0.57은 지면 위로 올리는 보정값이다 (브리핑 기준 목적, 수치는 Prefab에서 확인).

### 재질 (`Assets/Materials/`, Issue #19)

| 파일 | 사용처 | `_BaseColor` |
|---|---|---|
| `MAT_ChargeWarning.mat` | 보스 돌진 경고선 (`ChargeWarningLine`의 LineRenderer) | (1, 0.1, 0.1) 빨강 |
| `MAT_BossBullet.mat` | `PFB_BossBullet/Visual` | (0.6, 0.1, 1) 보라 |

- 두 재질 모두 `_Surface` 0(불투명)이다. 셰이더 이름은 GUID만 확인했다.

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
├─ (플레이어 Prefab 인스턴스)
├─ Enemies                    (EnemyPool._enemyParent)
├─ Gems                       (ExperienceGemPool._gemParent)
├─ Projectiles
└─ Chests                     (TreasureChestSpawner._chestParent, Issue #19)
Managers
├─ GameSession
├─ ProjectilePool
├─ MapGenerator
├─ FlowFieldManager
├─ EnemyPool                  (EnemyPool)
├─ ExperienceGemPool          (ExperienceGemPool)
├─ EnemySpawner               (EnemySpawner)
└─ TreasureChestSpawner       (TreasureChestSpawner, Issue #19)
```

- `Gameplay`·`Managers`의 자식 순서는 위 목록대로 Transform 순서로 확인했다 (Issue #19 문서 작성 시).

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `EnemyPool` | `_enemyParent` | `Gameplay/Enemies` |
| | `_defaultCapacity` / `_maxSize` | 64 / 512 (프리팹별 풀마다) |
| | `_projectilePool` | `Managers/ProjectilePool` (보스 사격 패턴에 주입, Issue #19) |
| `ExperienceGemPool` | `_gemPrefab` | `PFB_ExperienceGem` (Enemy에 젬 Prefab이 없을 때 쓰는 기본 젬) |
| | `_gemParent` | `Gameplay/Gems` |
| | `_playerExperience` | 플레이어 Prefab 인스턴스의 `PlayerExperience` |
| | `_defaultCapacity` / `_maxSize` | 64 / 512 (젬 Prefab별 풀마다) |
| | `_spawnHeight` | 0.3 |
| `TreasureChestSpawner` | `_chestPrefab` | `PFB_TreasureChest` |
| | `_currency` | `Currency` 오브젝트의 `Currency` (Issue #37) |
| | `_chestParent` | `Gameplay/Chests` |
| | `_player` | 플레이어 Prefab 인스턴스 |
| | `_levelUpPanel` | `UI/HUDCanvas`의 `LevelUpPanel` |
| | `_spawnHeight` | 0 |
| `EnemySpawner` | `_gameSession` | `Managers/GameSession` |
| | `_enemyPool` | `Managers/EnemyPool` |
| | `_flowFieldManager` | `Managers/FlowFieldManager` |
| | `_player` | 플레이어 Prefab 인스턴스 |
| | `_camera` | `MainCamera`의 Camera |
| | `_spawnTable` | `PFB_EnemyMushroom` 0초, `PFB_EnemyCactus` 180초, `PFB_EnemyBat` 360초 |
| | `_spawnInterval` | 1 |
| | `_spawnCountPerTick` | 2 |
| | `_maxActiveEnemies` | 500 (일반·원형 스폰 실제 상한은 500 − 스케줄 7 = 493) |
| | `_spawnMargin` | 2 |
| | `_maxPositionAttempts` | 8 |
| | `_bossSchedule` (7개, 순서대로) | `PFB_EnemyElitePlant` 180, `PFB_BossSalamander` 300, `PFB_EnemyEliteDragon` 480, `PFB_BossNaga` 600, `PFB_EnemyEliteOrc` 780, `PFB_BossBishop` 900, `PFB_BossBlackKnight` 1200 (초) |
| | `_ringSpawnTimes` | 240, 480, 720, 960 (초) |
| | `_ringPrefab` | `PFB_EnemyGolem` |
| | `_ringCount` / `_ringRadius` | 50 / 25 (코드 기본값 30 / 14, 필드 주석상 수치미정) |

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
| l | `EnemySpawner`의 책임이 늘었다 (일반 스폰, 보스·강화몹 스케줄과 위치 선택, 원형 대량 스폰). `_bossSchedule`·`TrySpawnNextBoss`·`SelectBossSpawnIndex` 등의 이름은 강화몹도 다루지만 그대로 둔다 (사용자 결정) | 후속 리팩토링 이슈 |
| m | `_bossSchedule`은 순서 큐다. 앞 항목의 위치를 못 찾으면(4변 중점이 모두 시야 안이거나 통행 불가) 그 항목이 생성될 때까지 뒤 항목도 시각이 지나도 생성되지 않는다. 시각이 오름차순이 아니면 늦은 시각의 앞 항목이 뒤 항목을 막는다 (정렬 검사 없음). 같은 프레임에 여러 항목의 시각이 지나도 한 프레임에 1마리씩 생성된다 | 참고 |
| n | Play 중 Project 창의 Prefab 에셋 값을 바꾸면 Play 종료 후에도 에셋에 남는다. 보스·패턴 수치를 Play 중 시험할 때는 Hierarchy의 생성된 인스턴스를 수정한다. 또 `BossCharge`는 `Awake`에서 한 번 만들어지므로 Play 중 인스턴스의 범위·경고 시간·쿨다운을 바꿔도 이미 만든 개체에는 반영되지 않는다 (코드 확인) | 참고 |
| o | 원형 대량 스폰은 한 프레임에 최대 `_ringCount`(GameScene 50)마리를 생성한다. 첫 회차에는 `PFB_EnemyGolem` 풀이 비어 있어 모두 `Instantiate`된다 (풀은 첫 `Spawn` 때 만들어지고 미리 채우지 않음). 프레임 비용은 측정하지 않았다 | M13 Profiler 측정 대상 |
| p | `PFB_BossBlackKnight`의 사망 연출 `_deathDuration`(2초)이 `GameSession._endFreezeDelay`(1.5초)보다 길다. 최종 보스 처치 클리어 시 1.5초 뒤 `Time.timeScale` 0으로 정지하므로 사망 애니메이션이 끝나기 전에 멈출 수 있다 (코드·값 기준, 실제 표시는 미검증) | 사망 연출·종료 대기 조정 시 |
| q | 보스 관련 Inspector 연결 누락 시 `null` 검사가 없다. 돌진 패턴 `_warningLine`이 비면 `OnEnable`에서, 사격 패턴 `_projectilePrefab`이 비면 첫 발사의 `ProjectilePool` `Dictionary` 조회에서, 시퀀서 `_patterns`가 비거나 `null` 원소가 있으면 `OnEnable`에서, `TreasureChestSpawner`의 `_chestPrefab`·`_levelUpPanel`이 비면 상자 생성·획득 시 예외가 난다. `TreasureChestSpawner._currency`가 비면 모든 Enemy 사망 시 `SpawnChest` 첫 줄(`Add`)에서 예외가 나고 상자도 생성되지 않는다. `EnemyPool._projectilePool`이 비면 예외 없이 사격 패턴이 동작하지 않는다 | 보스 추가 시 연결 필수 |
| r | 원형 대량 스폰에는 카메라 시야 조건이 없다. `_ringRadius`가 화면 반경보다 작으면 화면 안에 생성된다 | 수치 확정 시 |
| s | 넉백은 중심점의 다음 타일만 검사한다 (코드의 `shortcut` 주석). ㄱ자 모서리 대각 통과, 몸체 일부가 벽과 겹치는 상황이 가능하다. 넉백 위치 이동에는 Separation·정지 거리 판정이 없어 플레이어 쪽으로 밀리는 방향이면 정지 거리 안으로 들어갈 수 있다 (코드 기준 추정, 미검증) | 문제되면 몸체 반폭 기준 검사로 교체 |
| t | 넉백 누적은 상한(`_maxKnockbackSpeed`)만 있고 점감은 없다. 보스 저항은 펠릿마다 강도에 곱해진다. 수치(감속 20, 상한 10, 보스 저항 0.7, 샷건 Lv.3 강도 6)는 모두 임시값이다 | 밸런스 조정 시 |

### 검증 방법

- EditMode 테스트 (`Assets/Tests/Editor/`)
    - `HealthTests` : 치명적이지 않은 피해 시 체력 감소, 체력이 정확히 0 또는 그 아래(오버킬)로 내려가면 사망을 1회만 보고, 사망 후 피해 무시, 사망 후 `ResetToMax`로 최대 체력 복구
    - `DamageGateTests` : 같은 프레임 반복 시 1회만 통과, 무적 중 차단, 무적 후 재통과, 무적 시간과 정확히 같은 경과 시 재통과, 무적 시간 0이면 항상 통과
    - `ExperienceGemTests` : 수집 반경 안·밖·경계(포함)·높이 차 무시, 흩어짐 방향 평면·정규화, 비율 양 끝·중간이 각도 양 끝·반대 방향에 대응, 임의 비율이 각도 범위 안, 플레이어와 겹칠 때 평면·정규화 방향
    - `EnemySpawnerTests` : 해금 시각 직전 false·해금 시각 true·이후 항목 해금 시각에 이전 항목 true, 상한 미만 true·상한 false, 경계 거리 앞·뒤·옆(기울어진 변)·대각선(결과가 경계 위), 원점이 시야 밖이고 경계 반대 방향이면 0
- 테스트 실행 결과 : EditMode 테스트 161/161 통과 (2026-11-18, RP 단계 기준).
- Play 모드 (AI agent) : 스폰 후보 200회 샘플이 전부 화면 밖·통행 가능 칸, 처치 수 증가, 풀에서 재사용된 Enemy 16/16이 `Move` 상태.
- Play 모드 (사용자) : 몬스터 3종 이동·피격·사망 애니메이션, 젬 드롭·흡수, 해금 시각 변경, 피격 반짝임·데미지 숫자 확인.
- Issue #19 EditMode 테스트 (추가분)
    - `EnemySpawnerTests` : `SelectBossSpawnIndex` 모두 시야 밖이면 가장 가까운 후보, 가장 가까운 후보가 시야 안이면 다음으로 가까운 시야 밖 후보, 모두 시야 안이면 -1, 경계 위는 안쪽으로 취급. `GetRingPoint` 모든 지점이 중심에서 반지름 거리, 이웃 지점 사이 각도 균등, index 0은 중심 + 반지름 × (+X)
    - `BossChargeTests` : 범위 밖이면 `Chase` 유지, 범위 안이면 `Warn`, 경고 시간 경과 시 `Charge`, `EndCharge` 후 `Chase` 복귀·쿨다운 중 재경고 없음, 쿨다운 후 재경고
    - `BossShootPatternTests` : `GetSpreadDirection` 1발 중심, 8발 360° 균등(index 0 = 중심), 3발 40° = −20°/0°/+20°, 기울어진 입력은 평탄화·정규화
- 브랜치 `art/fix-inven-ui` EditMode 테스트 (추가분, 메서드 존재만 확인) : `EnemyMovementTests.CalculateKnockbackVelocity_Resistance_ScalesStrengthAndIgnoresY`(저항 배율·Y 무시), `CalculateKnockbackVelocity_Accumulated_ClampedToMaxSpeed`(누적 상한). 실행 결과·Play 모드 확인 결과(넉백, 스폰 오피격 방지)는 이 문서 작성 입력에 포함되지 않았다 (미검증). `ApplyKnockback`(이동·감속·이동 불가 타일 종료)을 검증하는 EditMode 테스트는 없다.
- Issue #19 테스트 실행 결과·Play 모드 확인 항목별 결과는 이 문서 작성 입력에 포함되지 않았다 (미검증으로 표기). `TreasureChest`·`TreasureChestSpawner`·`BossPatternSequencer`·`BossChargePattern`·`BossShootPattern`의 MonoBehaviour 동작(이동·발사·순환·상자 획득)을 검증하는 EditMode 테스트는 없다.

## 관련 Issue/PR

- Issue #8 (몬스터 프로토타입)
- PR : 미정
- Issue #19 (몬스터 최종 M06~M12 : 보스 스케줄, Salamander 돌진, Naga·Bishop 사격, 보물 상자·보상 요청, BlackKnight 시퀀서·처치 클리어, 강화몹·금색 젬, 원형 대량 스폰). 브랜치 `feat/monster-final`. PR : 미정
- Issue #7 (맵 프로토타입, Enemy 이동 `EnemyMovement`·`TempEnemySpawner` 최초 도입. `Docs/TechDocs/Map.md`)
- 이슈 미등록 (브랜치 `art/fix-inven-ui` : `EnemyMovement` 넉백, `EnemyPool` 위치 지정 후 활성화). PR : 미정
