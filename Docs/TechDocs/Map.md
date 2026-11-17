# 맵·Flow Field 시스템 (Map)

> 근거 : `Assets/Scripts/Map/*`, `Assets/Scripts/Enemy/EnemyMovement.cs`, `Assets/Scripts/Enemy/TempEnemySpawner.cs`, `Assets/Tests/Editor/*`, `Assets/Scenes/TestMap.unity`, `Assets/Prefabs/Enemy/PFB_TempCubeEnemy.prefab`, `ProjectSettings/*`
>
> 설계 원본 : `Docs/Spec/Map/Map_prototype.md`, `Docs/Spec/Map/Map_prototype_separation.md` (이 문서는 현재 구현 상태만 기술한다)

## 목적 / 기능 범위

- 게임 시작 시 100×100 타일 격자에 장애물·장식을 랜덤 배치하고, 타일별 이동 비용(Cost Field)을 확정한다.
- 플레이어 타일을 목적지로 누적 비용(Integration Field)과 이동 방향(Flow Field)을 계산하고, 전체 Enemy가 이 결과 하나를 공유한다.
- Enemy는 Flow Field를 따라 장애물을 우회해 플레이어로 이동하고, 주변 Enemy와 겹치지 않도록 Separation을 더한다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #7의 작업 A(맵 생성·Cost Field), B(Integration Field), C(Flow Field·Gizmos), D(Enemy 이동), E(목적지 갱신·성능 측정), F(Separation) |
| 제외 (후속) | Spatial Hash Grid(아래 `Spatial Hash Grid` 절), 고정 Seed, Chunk·Local Flow Field·Job System 등 `Map_prototype.md` §1.4 항목, 실제 Enemy 스폰·풀링 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `CostField` | 타일별 이동 비용(이동 가능 1, 장애물 255) 보관. 좌표 변환(`TileToWorld`, `WorldToTile`, `WorldToContinuousTile`), 범위·이동 가능 판정(`IsInBounds`, `IsWalkable`), 대각선 통과 판정(`CanPassDiagonal`), 장애물 지정(`SetObstacle`), 시작 타일에서 4방향으로 도달할 수 없는 타일 채움(`FillUnreachable`) | 없음 |
| `IntegrationField` | 목적지 타일까지 최소 누적 비용 계산 (8방향 다익스트라, 직선 1·대각선 1.414, 대각선은 `CanPassDiagonal`일 때만). 최소 힙 대신 `SortedSet<QueueEntry>` 사용. 마지막 목적지 `Target` | `CostField` |
| `FlowField` | 칸마다 8방향 중 "이웃 누적 비용 + 이동 비용"이 최소인 정규화 방향 계산. 목적지·장애물·도달 불가 칸은 `Vector2.zero`. 월드 위치 주변 4칸 방향의 쌍선형 보간(`GetInterpolatedDirection`) | `CostField`, `IntegrationField` |
| `FlowFieldManager` (MonoBehaviour) | `IntegrationField`·`FlowField`를 하나씩 소유하고 전체 Enemy에 공유. `Start`에서 1회 계산, `Update`에서 갱신 조건(`ShouldRecalculate`)을 만족하면 재계산. Profiler 구간 `FlowFieldManager.CalculateIntegration`, `FlowFieldManager.CalculateFlow`. Gizmos(목적지·누적 비용·방향) | `MapGenerator`(`_mapGenerator`), 플레이어 `Transform`(`_player`) |
| `MapGenerator` (MonoBehaviour) | `Awake`에서 `CostField` 생성 → 장애물 배치 → 고립 영역 채움 → 장식 배치 → 경계 벽 생성. 장애물마다 차단 Box 생성. Profiler 구간 `MapGenerator.Awake`, `MapGenerator.PlaceObstacles`, `MapGenerator.PlaceDecorations` | 장애물·장식 Prefab, `_generatedRoot` |
| `EnemyMovement` (MonoBehaviour) | 매 `Update`에 `FlowFieldManager`에서 Flow Field를 조회해 이동 방향 결정(`GetMoveDirection`), 주변 Enemy로 Separation 계산(`CalculateSeparation`)·합성(`GetSeparatedDirection`) 후 `transform.position` 직접 이동. `Velocity` 노출. Profiler 구간 `EnemyMovement.Separation` | `FlowFieldManager`, 플레이어 `Transform` (둘 다 `Initialize`로 주입), `Physics` |
| `TempEnemySpawner` (MonoBehaviour, 임시) | `Start`에서 `_spawnCount`마리를 자기 위치 기준 반경 `_spawnRadius` 원 위 랜덤 각도에 생성하고 `Initialize` 호출. 풀링·시간차 생성 없음 | `EnemyMovement` Prefab, `FlowFieldManager`, 플레이어 `Transform` |

## API / 이벤트 / 데이터 계약

이벤트는 없다. 모든 공유는 참조 조회로 이루어진다.

### 좌표 (`CostField`)

- 격자 : 100×100 (`MapGenerator.MAP_WIDTH`·`MAP_HEIGHT`), 타일 1×1, 원점 `(-50, 0, -50)` (타일 (0, 0)의 좌하단 모서리, 기존 100×100 바닥의 좌하단).
- `TileToWorld(x, z)` = 원점 + `(x + 0.5, 0, z + 0.5)` (타일 중심).
- `WorldToTile(position)` = 원점 기준 `FloorToInt`. 격자 밖이면 범위 밖 값을 그대로 반환한다.
- `GetCost`, `SetObstacle`, `IntegrationField.GetIntegration`은 범위 안 좌표만 허용한다 (검사 없음). `IsWalkable`, `FlowField.GetDirection`은 범위 밖이면 각각 `false`, `Vector2.zero`.

### 공유 참조 (`FlowFieldManager`)

| 멤버 | 유효 시점 | 내용 |
|---|---|---|
| `FlowField` | `Start` 이후 (그 전에는 `null`) | 전체 Enemy가 공유하는 방향장 |
| `CostField` | `Start` 이후 | `MapGenerator.CostField`와 같은 인스턴스 |
| `TargetTile` | `Start` 이후 | `_integrationField.Target`. `Start` 전에는 `_integrationField`가 `null`이라 접근하면 예외. `FlowField != null` 확인 후 사용한다 |

- `EnemyMovement`는 `FlowField`를 캐시하지 않고 매 `Update`에 조회한다. `_flowFieldManager`가 `null`(미주입)이거나 `FlowField`가 `null`이면 `Velocity`를 0으로 두고 정지한다.

### 갱신 (`FlowFieldManager.ShouldRecalculate`)

- 조건 : 플레이어 타일 ≠ 마지막 목적지 **그리고** 마지막 계산 후 경과 시간 ≥ `_updateInterval` (같으면 재계산).
- 계산은 `Integration` → `Flow` 순서의 단일 스레드 동기 계산이다. 계산 중 Enemy가 읽지 않으므로 이중 버퍼를 쓰지 않는다.
- 목적지 타일이 이동 불가이면 `IntegrationField.Calculate`는 모든 칸을 무한대로 두고 끝나며, 그 결과 `FlowField`의 모든 칸이 `Vector2.zero`가 된다 (아래 `차단 Box` 도입 이유 참고).

### 맵 생성 (`MapGenerator`)

- Seed : `Awake`마다 `Environment.TickCount`. 콘솔에 `[MapGenerator] Seed : …`를 남긴다. 모든 랜덤 판정은 이 Seed의 `System.Random` 하나로 한다.
- 장애물 배치 : 모든 타일 (x, z)마다 `_obstacleChance` 확률로 `_obstacles` 중 하나를 골라 (x, z)를 좌하단으로 배치를 시도한다.
- 배치 금지 타일 : 가장자리 `_edgeClearWidth`칸, 시작 타일(월드 원점) 기준 정사각형 반경 `_safeZoneRadius`칸, 이미 이동 불가인 타일. 점유 타일 중 한 칸이라도 금지면 그 장애물은 건너뛴다.
- 고립 영역 : 장애물 배치 후 `FillUnreachable(시작 타일)`로 4방향 도달 불가 타일을 장애물(255)로 채운다. 채운 칸 수를 콘솔에 남긴다.
- 장식 : 이동 가능한 타일마다 `_decorationChance` 확률로 `_decorations` 중 하나를 자유 회전으로 타일 중심에 배치한다. Cost는 바꾸지 않는다.
- 생성 오브젝트(장애물·장식·차단 Box·경계 벽)는 모두 `_generatedRoot` 아래에 둔다.

#### 장애물 점유 크기와 회전 (`ObstacleEntry`)

| 필드 | 내용 |
|---|---|
| `_prefab` | 장애물 Prefab |
| `_tileSize` (`Vector2Int`, Min 1) | 점유 타일 수. x = 가로, y = Z 방향 세로 (회전 0° 기준) |
| `_isFreeRotation` | true = Y축 0~360° 자유 회전, false = 0/90/180/270° 중 하나 |

- 회전을 먼저 정한 뒤 점유 크기를 정한다. 90° 단위에서 90°·270°(회전 횟수가 홀수)이면 가로·세로를 교환한다. 자유 회전이면 교환하지 않는다.
- 배치 위치는 점유 타일들의 중심이다 (`TileToWorld(x, z) + ((가로-1)/2, 0, (세로-1)/2)`).
- **가로≠세로인 장애물은 `_isFreeRotation`을 반드시 false로 둔다.** 이유 : 점유 타일은 회전과 관계없이 축 정렬 직사각형으로 계산된다. 직사각형 모델을 자유 회전하면 대각선 방향으로 놓일 때 모델이 점유 타일 밖으로 나온다. 점유 타일 밖의 모델 부분은 Cost Field에서 이동 가능 칸이라 Enemy가 그 위를 지나간다. 이 규칙은 Inspector 입력을 검사하지 않으며 설정하는 쪽 책임이다 (코드 주석 "가로≠세로면 반드시 false").
- 장애물 모델과 그 하위 오브젝트 전체의 Layer는 `Obstacle`(8)로 바꾼다.

#### 차단 Box (`ObstacleBlocker_xx_zz`)

- 장애물마다 점유 타일 전체를 덮는 보이지 않는 축 정렬 `BoxCollider` 오브젝트를 하나 만든다. 이름의 `xx_zz`는 좌하단 타일 좌표다 (`ObstacleBlocker_{0:00}_{1:00}`).
- 범위 : 좌하단 타일 모서리 ~ 우상단 타일 모서리, 높이는 바닥부터 `_boundaryWallHeight` (경계 벽과 같은 값을 공유).
- Layer : `Default` (코드에서 지정하지 않은 새 GameObject 기본값). 탄환의 `_hitMask`(Enemy)·`_blockMask`(현재 없음)에 포함되지 않아 탄환은 통과한다.
- 경계 벽(`BoundaryWall_01`~`04`)과 같은 `CreateBlockingBox`로 만든다.
- 도입 이유 : 장애물 모델의 Collider는 타일보다 작다 (나무는 반지름 약 0.13의 Capsule, 바위는 낮은 Box. 브리핑 기준 수치이며 이 문서 작성 시 Prefab에서 재확인하지 않음). 차단 Box가 없으면 플레이어 중심이 장애물 타일 안으로 들어가거나 바위 위로 올라갈 수 있다. 그러면 Flow Field의 목적지(플레이어 타일)가 이동 불가 타일이 되어 `IntegrationField`가 모든 칸을 무한대로 두고, Flow가 전부 0이 되어 Enemy 추적이 멈춘다.
- 차단 Box는 플레이어(`CharacterController`)를 막기 위한 것이다. Enemy는 `transform.position`을 직접 바꾸므로 차단 Box·경계 벽에 막히지 않는다 (Enemy의 장애물 회피는 Flow Field가 담당).
- 탄환이 장애물을 통과하는 것은 이슈 #7에서 확정한 사항이다. 무기 쪽 상세는 `Docs/TechDocs/Weapon.md`의 `투사체 적중·반환` 참고.

#### 경계 벽 (`BoundaryWall_01`~`04`)

- 격자 바깥 네 변에 두께 `_boundaryWallThickness`, 높이 `_boundaryWallHeight`의 Box. 남·북 벽은 모서리까지 덮도록 두께만큼 길다. Layer `Default`라 탄환은 통과한다.

### Enemy 이동 (`EnemyMovement`)

#### 이동 방향 (`GetMoveDirection`)

순서대로 판정한다.

1. 플레이어까지 XZ 거리 ≤ `_stopDistance` → `Vector3.zero` (정지).
2. 현재 타일이 격자 밖이거나 목적지 타일 → 플레이어 방향(XZ 정규화). 격자 밖 Enemy는 Field를 조회하지 않고 직진한다.
3. 그 밖 → `FlowField.GetInterpolatedDirection` (주변 4개 타일 중심 방향의 쌍선형 보간 후 정규화). 방향 0 타일(장애물·목적지·격자 밖)은 합에 기여하지 않는다.
4. 3의 결과가 0이면 직전 이동 방향(`_lastDirection`)을 유지한다 (장애물 모서리 파고듦에서 고착 방지). 직전 방향도 없으면 0.

#### 이동 거리

- `min(_moveSpeed × Time.deltaTime, 플레이어까지 XZ 거리 - _stopDistance)`. 한 프레임에 정지 거리 안으로 들어가지 않는다.
- 이동 후 `transform.rotation`을 이동 방향으로 바꾸고, `Velocity = 방향 × _moveSpeed`. 정지 시 `Velocity`는 0이다.

#### Separation

- 적용 조건 : 이동 방향이 0이 아니고(정지 거리 밖), 현재 타일이 격자 안이며 목적지 타일이 아닐 때만. 정지 거리 안·격자 밖·목적지 타일에서는 적용하지 않는다.
- 이웃 탐색 (`FindSeparation`) : `Physics.OverlapSphereNonAlloc(현재 위치, _separationRadius, 공유 버퍼, _enemyLayer)`. 버퍼는 전체 Enemy가 공유하는 static 배열 16칸(자기 자신 포함)이다. 초과하면 이웃이 누락된다 (코드의 `shortcut` 주석).
- 계산 (`CalculateSeparation`) : 반경 안 이웃마다 `(이웃→자신 XZ 단위 방향) × (1 - 거리 / 반경)`을 합산한다. 반경 경계에서 0, 최근접에서 1이며 정규화하지 않는다.
    - 거리 0인 이웃은 제외한다. 버퍼에 들어온 자기 자신이 이 경우다.
    - 거리 ≥ 반경인 이웃도 제외한다. `OverlapSphere`가 Collider 표면 기준이라 중심이 반경 밖인 이웃도 들어오기 때문이다.
- 합성 (`GetSeparatedDirection`) : `(Flow 방향 + Separation × _separationWeight)`를 정규화한다. 결과가 0이거나, 그 방향으로 이번 프레임 이동 거리만큼 간 위치의 타일이 이동 불가이면 Flow 방향만 쓴다.
- Profiler 구간 `EnemyMovement.Separation`이 이웃 탐색과 합성을 감싼다.

## 호출 순서 또는 데이터 흐름

```
[Awake] MapGenerator
  ├─ Seed = Environment.TickCount, CostField 생성 (100×100, 원점 (-50, 0, -50))
  ├─ PlaceObstacles()              타일마다 확률 판정 → TryPlaceObstacle
  │    ├─ 회전 결정 (자유 / 90° 단위, 90°·270°면 가로·세로 교환)
  │    ├─ 점유 타일 전부 배치 가능 확인 → SetObstacle
  │    ├─ 장애물 Instantiate (하위 전체 Layer = Obstacle)
  │    └─ 차단 Box 생성 (ObstacleBlocker_xx_zz)
  ├─ FillUnreachable(시작 타일)     고립 영역 → 255
  ├─ PlaceDecorations()
  └─ CreateBoundaryWalls()          BoundaryWall_01~04

[Start] (실행 순서 지정 없음)
FlowFieldManager.Start
  ├─ _costField = _mapGenerator.CostField   (Awake에서 생성되어 있음)
  ├─ IntegrationField·FlowField 생성
  └─ Calculate(플레이어 타일)                Integration → Flow
TempEnemySpawner.Start (임시)
  └─ _spawnCount마리 Instantiate → enemy.Initialize(FlowFieldManager, 플레이어)

[매 프레임 Update] FlowFieldManager
  ShouldRecalculate(플레이어 타일, 마지막 목적지, 경과 시간, _updateInterval)
  → true면 Calculate(플레이어 타일)

[매 프레임 Update] EnemyMovement (Enemy마다)
  ├─ _flowFieldManager == null || FlowField == null → Velocity 0, 정지
  ├─ 방향 = GetMoveDirection(...)             0이면 Velocity 0, 정지
  ├─ 이동 거리 = min(속도 × deltaTime, 거리 - 정지 거리)
  ├─ (격자 안 && 목적지 타일 아님)
  │    └─ [EnemyMovement.Separation]
  │         ├─ FindSeparation → OverlapSphereNonAlloc → CalculateSeparation
  │         └─ 방향 = GetSeparatedDirection(...)
  └─ position += 방향 × 이동 거리, rotation = LookRotation(방향)
```

- `FlowFieldManager.Start`와 `TempEnemySpawner.Start`의 순서는 정해져 있지 않다. 스포너가 먼저여도 Enemy는 `FlowField`가 `null`인 동안 정지하고, 이후 프레임부터 이동한다.
- `FlowFieldManager.Update`와 `EnemyMovement.Update`의 순서도 정해져 있지 않다. 같은 프레임에 갱신 전 결과를 읽을 수 있다 (최대 1프레임 지연, Play 영향 미검증).

## Scene·Prefab·Inspector 사용 방법

> 아래 값은 `Assets/Scenes/TestMap.unity`, `PFB_TempCubeEnemy.prefab`에 저장된 값이다. 코드의 필드 기본값과 다르면 Scene·Prefab 값이 실제 동작 기준이다.

### `Assets/Scenes/TestMap.unity`

```
Environment
└─ GeneratedMap               (_generatedRoot, 런타임 생성 오브젝트의 부모)
Gameplay
└─ Enemies                    (_enemyParent, Scene 배치 PFB_TempCubeEnemy 3개 포함)
Managers
├─ MapGenerator               (MapGenerator)
├─ FlowFieldManager           (FlowFieldManager)
└─ TempEnemySpawner           (TempEnemySpawner, 로컬 위치 (0, 0.482284, 0))
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `MapGenerator` | `_obstacleChance` | 0.01 (코드 기본값 0.05) |
| | `_decorationChance` | 0.03 (코드 기본값 0.15) |
| | `_obstacles` | 아래 표 (4항목) |
| | `_safeZoneRadius` | 5 |
| | `_edgeClearWidth` | 2 |
| | `_generatedRoot` | `Environment/GeneratedMap` |
| | `_boundaryWallHeight` | 3 (차단 Box 높이로도 사용) |
| | `_boundaryWallThickness` | 1 |
| | `_drawGizmos` | true |
| `FlowFieldManager` | `_mapGenerator` | `Managers/MapGenerator` |
| | `_player` | 플레이어 Prefab 인스턴스 |
| | `_updateInterval` | 0.2 |
| | `_drawTarget` / `_drawIntegration` / `_drawFlow` | false / false / false |
| `TempEnemySpawner` (임시) | `_enemyPrefab` | `PFB_TempCubeEnemy` |
| | `_flowFieldManager` | `Managers/FlowFieldManager` |
| | `_player` | 플레이어 Prefab 인스턴스 |
| | `_enemyParent` | `Gameplay/Enemies` |
| | `_spawnCount` | 20 (성능 측정 시 500으로 변경해 측정) |
| | `_spawnRadius` | 75 (격자 모서리까지 약 70.7보다 커서 격자 밖에 생성) |

`_obstacles`

| 장애물 | `_tileSize` | `_isFreeRotation` |
|---|---|---|
| 나무 1 | (1, 1) | true |
| 나무 2 | (1, 1) | true |
| `Rock_Blocks_m_06` (`With_Box_Colliders`) | (2, 2) | true |
| `Rock_Wall_01` (`With_Box_Colliders`) | (5, 2) | false (90° 단위) |

- 바위 2종은 Low Poly Nature Bundle의 `Rocks/Rock Assets/Prefabs/With_Box_Colliders/1 Color/` 아래 Prefab이다 (`.meta` guid로 확인). 나무 2종의 Prefab 이름은 확인하지 못했다 (미검증, `Assets/Imports/`가 Git 추적 대상이 아니라 검색에서 빠짐).
- `_decorations`의 항목은 이 문서에서 다루지 않는다.

### `Assets/Prefabs/Enemy/PFB_TempCubeEnemy.prefab`

- 루트 : Layer `Enemy`(6), `BoxCollider`(크기 1, 트리거 아님), `TempCubeEnemy`, `EnemyMovement`. 자식 `AimPoint`.
- `EnemyMovement`의 Collider는 같은 루트 오브젝트에 있다 (자기 자신 제외 조건의 전제, 주의사항 c).

| `EnemyMovement` 필드 | 값 |
|---|---|
| `_moveSpeed` | 3 |
| `_stopDistance` | 1 (플레이어 반지름 0.4 + 큐브 반폭 0.5 + 여유) |
| `_separationRadius` | 1.2 (큐브 폭 1 + 여유) |
| `_separationWeight` | 1 |
| `_enemyLayer` | Enemy |

### Gizmos

- `MapGenerator._drawGizmos` : 격자 경계(노랑 와이어)는 항상, 이동 불가 칸(반투명 빨강)·시작 안전 영역(초록 와이어)은 Play 중에만 표시한다.
- `FlowFieldManager` : Play 중 `_drawTarget`(목적지 청록 박스), `_drawIntegration`(근거리 초록 → 원거리 자홍, 도달 불가 칸 미표시), `_drawFlow`(흰 선, 끝 큐브 쪽이 이동 방향)를 각각 켠다. Scene·Game 뷰의 Gizmos 버튼도 켜져 있어야 한다.

## 예외·주의사항·검증 방법

### 성능 측정

> 메인 세션이 에디터 Play 모드에서 Unity Profiler로 측정한 값이다 (이 문서 작성 시 재측정하지 않음). Unity 창이 포커스를 잃은 상태에서 측정하기 위해 Play 중 `Application.runInBackground = true`로 두고 측정했다 (프로젝트 설정 `runInBackground`는 0).

작업 E (경로 갱신·이동)

| 항목 | 값 |
|---|---|
| 갱신 1회 `FlowFieldManager.CalculateIntegration` | 약 14.5~16.4ms, GC 약 504KB (`SortedSet` 노드 할당) |
| 갱신 1회 `FlowFieldManager.CalculateFlow` | 약 3~4ms |
| `EnemyMovement` 500마리 (Separation 도입 전) | 1.1ms |
| 맵 생성 (`MapGenerator.Awake`) | 약 25ms |

작업 F (Separation, Enemy 503마리, 게임 시간 20초, 도입 전 → 후)

| 항목 | 도입 전 | 도입 후 |
|---|---|---|
| XZ 거리 1 미만인 Enemy 쌍 수 | 5602 | 1290 |
| 최근접 Enemy 거리 평균 | 0.126 | 0.602 |
| `EnemyMovement.Separation` 평균 (최대) | - | 3.48ms (5.63ms) |
| `BehaviourUpdate` | 1.09ms | 5.03ms |

### Spatial Hash Grid

- 판단 기준(500마리에서 `EnemyMovement.Separation` 1ms 이상)을 넘었지만 적용하지 않았다 (사용자 결정).
- 맵·캐릭터·무기 등 모든 기능을 적용한 뒤 성능 문제가 생기면 검토한다. 이슈는 등록하지 않았다.

### 주의사항·알려진 한계

| # | 내용 | 후속 시점 |
|---|---|---|
| a | `GetSeparatedDirection`은 다음 위치의 타일만 확인한다. 장애물이 ㄱ자로 놓인 모서리를 대각선으로 통과하는 경우는 처리하지 않는다 (코드의 `shortcut` 주석). 실제로 끼이는지는 미검증 | 모서리 끼임 확인 시 `CanPassDiagonal` 판정 추가 |
| b | Separation 이웃 버퍼가 전체 Enemy 공유 16칸이다. 반경 안 Collider가 16개를 넘으면 이웃이 누락된다 (코드의 `shortcut` 주석) | 떨림·누락 확인 시 버퍼 확장 또는 Spatial Hash Grid |
| c | 자기 자신 제외는 "거리 0" 조건에 기대므로, Collider가 `EnemyMovement`와 같은 Transform에 있다는 전제가 필요하다. 실제 적 Prefab에서 Collider를 오프셋 자식에 두면 자기 Collider를 이웃으로 보고 스스로를 밀어낸다 | 실제 적 Prefab 작성 시 |
| d | 밀집 시 Separation 합은 최대 이웃 15 × 1 = 15까지 커질 수 있어 Flow 방향(길이 1)을 압도할 수 있다 (정규화 전 합산). 다음 타일이 이동 불가일 때만 Flow로 되돌린다 | 밀집 이동 문제 확인 시 |
| e | `Rock_Wall_01`은 90° 단위로 회전해도 양 끝이 점유 타일 밖으로 약 0.15 돌출된다 (브리핑 기준 수치, 이 문서 작성 시 재측정하지 않음) | 문제가 보이면 점유 크기·스케일 조정 |
| f | 이웃 위치는 Physics 쪽 Transform 기준이라 최대 약 0.02초 늦을 수 있다 (`DynamicsManager` `m_AutoSyncTransforms` 0, `Fixed Timestep` 0.02, Enemy는 `transform.position`으로 이동) | 참고 |
| g | 갱신 비용 개선(`SortedSet` GC 등)은 이슈 #7 범위 밖 별도 작업이다 (`IntegrationField.Relax`의 TODO 주석) | 별도 작업 |
| h | 성능 측정 때 Enemy가 503마리였다 (`_spawnCount` 500). 원인은 확인하지 않았다. 참고로 `TestMap`의 `Gameplay/Enemies`에 Scene 배치 `PFB_TempCubeEnemy` 3개가 있어 500 + 3일 가능성이 있다 (추정, 미검증). 이 3개는 `Initialize`를 받지 않아 정지한다 | 미정 |
| i | `EnemyMovement`, `FlowFieldManager`는 `Time.deltaTime`·`Time.time`을 직접 쓴다. 일시정지와 연동되어 있지 않다 (코드의 `shortcut` 주석) | GameState 도입 시 교체 |
| j | `_isFreeRotation`의 "가로≠세로면 false" 규칙은 코드가 검사하지 않는다 (`장애물 점유 크기와 회전` 절) | 참고 |

### 검증 방법

- EditMode 테스트 (`Assets/Tests/Editor/`)
    - `CostFieldTests` : 원점·타일 중심 변환, 원점 반영, 왕복 변환, 범위 밖 이동 불가, 장애물 지정, 고립 영역만 채움
    - `IntegrationFieldTests` : 목적지 0, 직선·대각선 비용, 벽 우회 비용, 직선 이웃 하나가 막히면 대각선 미사용, 고립 칸 무한대, 목적지가 장애물이면 전부 무한대, 두 번 계산 시 새 계산과 같은 결과
    - `FlowFieldTests` : 직선·대각선 방향, 벽 우회, 대각선 금지, 목적지·장애물 0, 고립 칸 0, 모든 방향 정규화, 범위 밖 0, 보간 혼합, 방향 0 이웃 제외, 이웃 전부 0이면 0
    - `FlowFieldManagerTests` : 같은 타일이면 재계산 안 함, 간격 미만 안 함, 간격과 같으면·초과면 재계산
    - `EnemyMovementTests` : 격자 밖 플레이어 직진(높이 무시), 목적지 타일 플레이어 위치 직진, 일반 타일 Flow 우회, 방향 0이면 직전 방향 유지·직전 방향 없으면 0, 정지 거리 안 0, Separation 이웃 없음 0·한 이웃 반대 방향·거리 0 이웃 제외, 다음 타일이 막히면 Flow 방향 유지
- Play 모드 : 사용자 확인 결과는 이 문서 작성 입력에 포함되지 않았다 (미검증으로 표기). Separation의 겹침 감소는 위 `성능 측정`의 쌍 수·최근접 거리로 측정했다.

## 관련 Issue/PR

- Issue #7 (맵 프로토타입, 작업 A~F)
