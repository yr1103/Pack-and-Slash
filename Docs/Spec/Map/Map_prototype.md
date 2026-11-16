# 절차적 맵 생성 및 Flow Field 기반 Enemy 이동 시스템 구현 지시서

**진행 상태:** 결정 사항 확정 / 이슈 #7 범위 수정 후 착수 브리핑 예정

---

## 0. 확정된 결정 사항 (2026-11-16)

> 아래 결정이 본문과 충돌하면 이 절을 따른다.

| 항목 | 결정 |
|---|---|
| 구현 담당 | Cost / Integration / Flow Field, 갱신 정책, Separation 포함 전체를 AI Agent가 구현하고 개발자가 검수한다 (`TechSpec.md` §4) |
| 클래스 구성 | `MapGenerator` : 바닥·오브젝트 배치 / `CostField` : 격자 데이터·좌표 변환 (`MonsterMovement.md`의 맵 데이터 제공 역할 겸함) |
| 배치 오브젝트 | 나무·바위(장애물), 풀·꽃(장식). Agent가 Low Poly Nature Bundle에서 후보를 제안·배치하고 사용자 확인 후 확정 |
| 장애물 크기·회전 | 1×1 또는 2×2 점유. Capsule Collider 장애물은 Y축 자유 회전, Box Collider 장애물은 90° 단위 회전. 장식은 자유 회전 |
| 플레이어 시작 위치 | 맵 중앙 |
| 고립 영역 | 시작 타일에서 Flood Fill(4방향) 후 도달 불가능한 이동 가능 타일을 장애물(255)로 채운다 |
| 격자 원점 | 기존 바닥(100×100, 원점 중심)을 사용하므로 격자 원점은 (-50, -50). §2.1 좌표 변환식에 원점을 더한다 |
| 맵 경계 | 경계 벽 Collider 설치 (플레이어만 막음, `Default` Layer로 탄환 통과). 경계 밖 바닥은 표시하지 않음 |
| Enemy 스폰 | 화면이 플레이어 기준 앞 30·좌우 44까지 보여 격자 안에 화면 밖 공간이 부족하다. 격자 밖 여유 공간에 스폰하고, 격자 밖 Enemy는 Field 조회 없이 플레이어로 직선 이동한다 (§6.3의 맵 밖 이동 금지를 대체). 가장자리 2칸에는 장애물을 두지 않는다 |
| 장애물 Collider | 플레이어도 통과 불가 |
| Seed | 매번 랜덤 (고정 Seed 방식은 추후 확정) |
| 바닥 | 구현이 단순하고 비용이 낮은 방식 |
| 작업 Scene | `Assets/Scenes/TestMap.unity` |
| Enemy 이동 | `TempCubeEnemy`와 독립된 이동 컴포넌트를 만들어 부착 (실제 Enemy로 이관 가능하게) |
| 테스트 스폰 | 단순한 임시 스포너 |
| Separation | 작업 F로 포함 (`Map_prototype_separation.md`). §1.4의 Separation 제외 항목을 대체하며, Flocking은 계속 제외 |
| 장애물 모서리 파고듦 | Enemy가 장애물 모서리를 살짝 파고드는 것은 허용 |
| 경로 갱신 | 타일 변경 시 갱신 필요 표시, 실제 계산은 최대 0.2초에 1회 |
| 갱신 안정성 | 단일 스레드 동기 계산으로 처리, 이중 버퍼 미사용 |
| 플레이어 맵 밖·장애물 타일 | 이번 구현에서 고려하지 않음. 플레이어 이동 쪽 예외 처리는 추후 진행 |
| 최소 힙 | `PriorityQueue` 사용 불가 확인 (API 호환 수준 `NET_Standard_2_0`). `SortedSet`으로 대체 (`SortedSet_example.md` 참고) |

---

## 1. 프로젝트 개요

### 1.1. 구현 목적

게임 시작 시 100×100 크기의 평지 맵을 생성하고, 맵 위에 나무, 바위, 풀, 꽃 등의 프리팹을 절차적으로 배치한다.

나무와 바위는 이동 불가능한 장애물로 처리하며, 풀, 꽃 등은 이동 가능한 장식 오브젝트로 처리한다.

생성된 맵의 장애물 정보를 바탕으로 Cost Field, Integration Field, Flow Field를 생성하고, 다수의 Enemy가 하나의 공통 Flow Field를 참조하여 장애물을 우회하면서 플레이어를 추적하도록 구현한다.

**이번 구현에서 가장 중요한 목표는 기능의 안정성, 코드 구조의 명확성, 다수 Enemy 환경에서의 계산 효율이다.**

### 1.2. 시스템 처리 순서

1. 맵 초기화
2. 단일 바닥 Mesh 생성 및 배치
3. 나무·풀 프리팹 절차적 배치
4. Cost Field 초기화 및 장애물 정보 확정
5. 플레이어 위치를 목적지로 Integration Field 계산
6. Integration Field 기반 Flow Field 생성
7. Enemy가 공통 Flow Field를 참조하여 이동
8. 플레이어의 타일 위치 변경 시 경로 필드 갱신

### 1.3. 구현 범위

| 항목 | 요구사항 |
|---|---|
| 맵 크기 | 100×100 Unity Unit |
| 맵 좌표 | XZ 평면 |
| 바닥 | 단일 Plane 또는 Mesh |
| 바닥 형태 | 단일 재질 및 형태 |
| 맵 생성 | 게임 시작 시 1회 |
| 나무 | 절차적 배치, 이동 불가능 |
| 풀 | 절차적 배치, 이동 가능 |
| 랜덤 생성 | Seed 기반 |
| Cost Field | 100×100 |
| Integration Field | 8방향 다익스트라 |
| Flow Field | 공용 이동 방향장 |
| Enemy | Flow Field 기반 플레이어 추적 |
| 목적지 갱신 | 타일 변경 감지 + 갱신 주기 제한 |
| 디버깅 | Gizmos 및 Unity Profiler |

### 1.4. 구현 제외 범위

다음 기능은 추후 확장 사항으로 유지한다.

- Chunk 기반 맵 생성 및 스트리밍
- 무한 맵 및 Local Flow Field
- Hierarchical Flow Field
- Job System / Burst
- Enemy Separation / Flocking
- 동적 장애물
- 지형별 이동 비용 차등 적용
- 보물 상자 및 이벤트 오브젝트 배치
- 저장 및 불러오기 시스템
- 복잡한 지형 생성 알고리즘

해당 기능들은 현재 구현에 필요하지 않으므로 임의로 추가하지 않는다.

---

## 2. 맵 생성 시스템

### 2.1. 기본 맵 구조

100×100 크기의 평평한 맵을 생성한다.

- 맵 범위: X = 0~100, Z = 0~100
- 논리적 타일 크기: 1×1 Unity Unit
- 논리적 타일 좌표: X = 0~99, Z = 0~99
- 전체 논리적 타일 수: 10,000
- 기본 바닥 높이: Y = 0

논리적 타일과 실제 렌더링 오브젝트는 분리한다.

맵 좌표 변환 기능을 제공한다.

**Tile → World**

```csharp
Vector3 TileToWorld(int x, int z)
{
    return new Vector3(x + 0.5f, 0f, z + 0.5f);
}
```

**World → Tile**

```csharp
Vector2Int WorldToTile(Vector3 position)
{
    return new Vector2Int(
        Mathf.FloorToInt(position.x),
        Mathf.FloorToInt(position.z)
    );
}
```

좌표 변환 로직을 여러 컴포넌트에 중복 작성하지 않는다.

### 2.2. 바닥 생성

바닥은 하나의 Plane 또는 단일 Mesh로 구성한다.

동일한 형태와 재질을 사용하기 때문에 바닥을 10,000개의 GameObject로 생성하지 않는다.

기본 Unity Plane 사용 시 다음 형태로 구현할 수 있다.

```csharp
GameObject ground =
    GameObject.CreatePrimitive(PrimitiveType.Plane);

ground.transform.position = new Vector3(50f, 0f, 50f);
ground.transform.localScale = new Vector3(10f, 1f, 10f);
```

기존 프로젝트에 적절한 바닥 Mesh가 존재하면 이를 우선 활용한다.

바닥 Collider는 현재 플레이어 및 Enemy 이동 방식에 따라 필요한 경우에만 유지한다.

**논리적인 100×100 타일 정보는 단일 바닥 Mesh와 별개로 관리한다.**

### 2.3. Seed 기반 절차적 생성

나무와 풀의 배치는 Seed를 사용하여 생성한다.

동일한 Seed에서 동일한 위치에 오브젝트가 생성되도록 한다.

Inspector에서 다음 항목을 설정할 수 있도록 구현한다.

- 맵 생성 Seed
- 나무 생성 확률
- 풀 생성 확률
- 나무 Prefab
- 풀 Prefab

예시 초기값(적용할 prefab 확정 후 재설정 필요):

| 항목 | 초기값 |
|---|---:|
| Map Width | 100 |
| Map Height | 100 |
| Tree Spawn Chance | 5% |
| Grass Spawn Chance | 15% |

생성 확률은 기본값이며 Inspector에서 수정 가능하도록 한다.

한 타일에 나무와 풀을 중복 생성하지 않는다.

### 2.4. 나무 장애물

나무는 이동 불가능한 장애물이다.

단, **나무 Prefab은 사용자 선정 및 Agent 검증 완료 후 사용한다.**

작업 순서는 다음과 같다.

1. 사용자가 나무 Prefab 선정
2. Agent가 Prefab의 크기 및 Pivot 확인
3. 적절한 Scale 검토
4. Collider 크기 및 형태 확인
5. 점유 타일 범위 확인
6. Enemy 및 Player 충돌 가능성 검토
7. 검증 결과 사용자 보고
8. 사용자 승인 후 실제 생성 시스템에 적용

나무의 시각적 크기와 장애물 점유 범위는 구분한다.

잎사귀가 넓더라도 실제 이동을 막아야 하는 영역은 줄기 또는 지정한 충돌 범위를 기준으로 판단한다.

초기 구현에서는 한 타일에 대응하는 장애물 크기를 우선 사용한다.

나무가 배치된 타일은 Cost Field에서 이동 불가능하도록 처리한다.

### 2.5. 풀 프리팹

풀은 이동 가능한 장식 오브젝트이다.

- 나무가 배치되지 않은 타일에 생성
- 이동을 방해하지 않음
- Cost Field는 평지와 동일하게 1
- 불필요한 Collider는 제거하거나 이동에 영향을 주지 않도록 설정

다수의 풀 프리팹 생성이 성능에 영향을 준다면 기존 에셋의 렌더링 방식 및 인스턴싱 가능 여부를 검토한다.

단, 초기 단계에서 별도의 복잡한 렌더링 최적화 시스템을 구현하지 않는다.

### 2.6. 시작 위치 및 안전 영역

플레이어의 시작 위치에 나무가 생성되지 않도록 한다.

플레이어 시작 타일과 주변 일정 범위를 장애물 생성 제외 구역으로 설정한다.

시작 안전 영역은 Inspector에서 변경할 수 있도록 한다.

랜덤 장애물 생성으로 이동 불가능한 구역이 발생할 가능성이 있으므로, 생성된 맵의 이동 가능 여부를 검증할 수 있어야 한다.

초기 구현에서는 장애물 밀도를 낮게 유지하고, 고립 영역이 발견되면 경고 또는 단순 재생성 방식으로 대응한다.

---

## 3. Cost Field

### 3.1. 역할

맵의 모든 논리적 타일에 대한 이동 비용을 저장한다.

```csharp
private byte[,] _costs;
```

| 타일 | Cost | 의미 |
|---|---:|---|
| 바닥 | 1 | 이동 가능 |
| 풀 | 1 | 이동 가능 |
| 나무 | 255 | 이동 불가능 |

### 3.2. 필수 기능

- 100×100 Cost Field 초기화
- 나무 배치 결과 반영
- 특정 타일의 이동 비용 조회
- 타일 이동 가능 여부 확인
- 맵 범위 밖 좌표 처리

예상 인터페이스:

```csharp
public byte GetCost(int x, int z);
public bool IsWalkable(int x, int z);
```

Cost Field는 맵 생성 완료 후 확정한다.

이번 구현에서는 장애물이 움직이거나 파괴되지 않으므로 게임 진행 중 Cost Field를 반복 생성하지 않는다.

---

## 4. Integration Field

### 4.1. 역할

Cost Field를 기반으로 목적지까지의 최소 누적 이동 비용을 계산한다.

목적지는 플레이어가 위치한 타일이다.

```csharp
private float[,] _integration;
```

### 4.2. 이동 규칙

상하좌우 및 대각선 이동을 포함한 8방향 탐색을 사용한다.

| 이동 | 비용 |
|---|---:|
| 상하좌우 | 1 |
| 대각선 | 약 1.414 |

대각선 이동 시 Corner Cutting을 금지한다.

즉, 대각선으로 이동하려면 해당 대각선을 구성하는 두 직선 방향의 인접 타일이 모두 이동 가능해야 한다.

### 4.3. 다익스트라 구현

최소 힙 기반 다익스트라 알고리즘을 사용한다.

- 목적지 누적 비용을 0으로 설정
- 나머지 타일은 양의 무한대로 초기화
- 최소 누적 비용 타일부터 탐색
- 주변 8방향 이동 비용 갱신
- 장애물 타일 제외
- 도달 불가능한 타일은 무한대로 유지

기존 학습용 코드처럼 전체 타일을 매번 순회하여 최소 비용 타일을 찾는 방식은 사용하지 않는다.

가능하면 `PriorityQueue`를 사용한다.

프로젝트의 Unity 및 C# 런타임에서 지원되지 않는다면 단순한 최소 힙을 구현하거나 적절한 대안을 제안한다.

불필요한 외부 패키지 도입은 피한다.

---

## 5. Flow Field

### 5.1. 역할

Integration Field의 최소 누적 비용을 이용하여 각 타일에서 이동할 방향 벡터를 계산한다.

```csharp
private Vector2[,] _directions;
```

### 5.2. 방향 계산

모든 이동 가능한 타일을 순회하여 주변 8방향 중 최소 비용으로 목적지에 접근할 수 있는 방향을 선택한다.

```text
후보 이동 비용 =
인접 타일의 Integration 비용 + 해당 타일로 이동하는 비용
```

최적 방향은 정규화한 `Vector2`로 저장한다.

목적지, 장애물 및 도달 불가능한 타일은 `Vector2.zero`로 처리한다.

Integration Field와 동일한 대각선 이동 규칙과 Corner Cutting 방지 조건을 사용한다.

### 5.3. 공용 Flow Field

Flow Field는 전체 Enemy가 하나의 계산 결과를 공유한다.

Enemy별로 별도 Integration Field 또는 Flow Field를 생성하지 않는다.

공용 관리자 또는 기존 프로젝트의 적합한 시스템을 통해 참조를 전달한다.

---

## 6. Enemy Movement

### 6.1. 이동 방식

Enemy는 현재 위치의 Flow Field 방향을 조회하여 이동한다.

1. Enemy 월드 좌표 확인
2. 월드 좌표를 논리적 타일 좌표로 변환
3. Flow Field의 해당 타일 방향 조회
4. `Vector2`를 XZ 평면의 `Vector3`로 변환
5. 이동 속도 적용

이동 예시:

```csharp
Vector2Int tile = WorldToTile(transform.position);

Vector2 direction =
    _flowField.GetDirection(tile.x, tile.y);

Vector3 moveDirection =
    new Vector3(direction.x, 0f, direction.y);

transform.position +=
    moveDirection * _moveSpeed * Time.deltaTime;
```

실제 구현 시 기존 Enemy의 Rigidbody 및 이동 상태 로직과 충돌하지 않도록 한다.

### 6.2. 목적지 타일 내부 처리

플레이어와 Enemy가 동일한 타일에 있는 경우에는 플레이어의 실제 월드 좌표를 향해 이동하도록 한다.

이를 통해 목적지 타일에 진입하자마자 멈추는 문제를 방지한다.

### 6.3. 이동 관련 제한 사항

- 맵 범위 밖으로 이동하지 않도록 처리
- 도달 불가능한 타일에서는 안전하게 정지
- 이동 속도는 Inspector에서 조정
- Enemy별로 별도의 경로 탐색 수행 금지
- Enemy Separation 및 Flocking은 이번 구현에서 제외

---

## 7. Flow Field 갱신 시스템

### 7.1. 기본 원칙

Cost Field는 게임 시작 시 한 번 생성한다.

반면 Integration Field와 Flow Field는 플레이어의 위치 변경에 따라 갱신해야 한다.

다만 매 프레임 전체 맵을 재계산하지 않는다.

### 7.2. 갱신 조건

- 플레이어가 같은 타일에 있으면 재계산하지 않음
- 플레이어가 다른 타일로 이동하면 갱신 필요 상태로 변경
- 갱신 간격은 초기값 0.2초
- 갱신 시 최신 플레이어 타일을 목적지로 사용
- Integration Field 계산 완료 후 Flow Field 계산

갱신 간격은 Inspector에서 조정 가능하도록 한다.

### 7.3. 갱신 안정성

새 경로를 계산하는 동안 Enemy가 부분적으로 갱신된 데이터를 참조하지 않도록 한다.

재계산이 완료된 후 유효한 Flow Field를 사용하도록 관리한다.

기본 구현에서 멀티스레딩은 사용하지 않는다.

단일 스레드 동기 계산이 프레임에 영향을 주는 경우 Unity Profiler 결과를 바탕으로 개선안을 제시한다.

---

## 8. 클래스 및 데이터 구조

기존 프로젝트 구조를 확인한 후 적절하게 통합한다.

권장 책임 분리는 다음과 같다.

| 클래스 | 주요 역할 |
|---|---|
| `MapGenerator` | 바닥 및 절차적 오브젝트 배치 |
| `CostField` | 이동 비용 데이터 관리 |
| `IntegrationField` | 최소 누적 비용 계산 |
| `FlowField` | 이동 방향 계산 |
| `FlowFieldManager` | 경로 필드 갱신 관리 |
| `EnemyMovement` | 공용 방향장 기반 이동 |

불필요한 매니저 중복은 피한다.

Inspector에서 설정해야 하는 변수는 `[SerializeField] private`을 사용한다.

외부에서 조회가 필요한 데이터는 읽기 전용 프로퍼티 또는 명확한 조회 메서드를 제공한다.

이벤트가 적절한 경우 이벤트 기반으로 연결하되, 이번 구현 규모에서 필요하지 않은 복잡한 구조는 도입하지 않는다.

---

## 9. 디버깅 및 테스트

### 9.1. Gizmos

Scene 뷰에서 다음을 확인할 수 있도록 한다.

- 맵 경계
- 장애물 타일
- 플레이어 목적지 타일
- Integration Field 누적 비용
- Flow Field 방향 벡터

디버그 표시를 켜고 끌 수 있도록 한다.

### 9.2. 검증 항목

- [ ] 100×100 맵 정상 생성
- [ ] 단일 바닥 Mesh 사용
- [ ] 동일 Seed에서 동일한 나무·풀 배치
- [ ] 나무 Prefab 크기와 Collider 검증
- [ ] 나무를 장애물로 인식
- [ ] 풀을 이동 가능한 장식으로 처리
- [ ] 플레이어 시작 위치의 이동 가능 여부 확보
- [ ] Cost Field 정상 생성
- [ ] 8방향 Integration Field 계산
- [ ] Corner Cutting 방지
- [ ] Flow Field 방향 계산
- [ ] Enemy가 장애물을 우회하여 플레이어 추적
- [ ] 여러 Enemy가 하나의 Flow Field 공유
- [ ] 플레이어 타일 변경 시 경로 갱신
- [ ] 동일 타일에서 불필요한 갱신 미발생
- [ ] 맵 밖 및 도달 불가능한 타일 안전 처리
- [ ] Unity Console 오류 없음

### 9.3. 성능 검증

Unity Profiler를 사용하여 다음을 측정한다.

- 최초 맵 생성 시간
- 나무 및 풀 배치 시간
- Integration Field 계산 시간
- Flow Field 계산 시간
- 경로 갱신 시 프레임 영향
- Enemy 10/100/500마리 이동 성능

측정 결과를 근거로 최적화 필요 여부를 판단한다.

---

## 10. AI Agent 작업 절차

### 10.1. 사전 분석

작업 시작 전 반드시 프로젝트를 확인한다.

- Unity 및 C# 버전
- 기존 맵과 Enemy 구현
- 기존 플레이어 이동 방식
- Rigidbody 및 Collider 구성
- 현재 사용 중인 Prefab과 Scene
- 코딩 컨벤션 및 작업 승인 규칙

기존 코드와 충돌하는 변경을 임의로 진행하지 않는다.

### 10.2. 단계별 작업

**작업 A — 맵 생성 및 Cost Field**

- 단일 바닥 Mesh 구성
- Seed 기반 맵 생성
- 나무 및 풀 배치
- Prefab 검증
- Cost Field 생성 및 테스트

**작업 B — Integration Field**

- 최소 힙 기반 다익스트라 구현
- 8방향 비용 적용
- 장애물 및 Corner Cutting 처리
- 누적 비용 검증

**작업 C — Flow Field**

- 방향 벡터 계산
- 방향장 공유
- Gizmos 시각화

**작업 D — Enemy 이동**

- 월드/타일 좌표 변환
- 공용 Flow Field 조회
- 실제 이동 적용
- 기존 이동 상태 및 충돌 시스템 검증

**작업 E — 목적지 갱신 및 성능 확인**

- 플레이어 타일 변경 감지
- 경로 재계산 주기 적용
- 다수 Enemy 테스트
- Unity Profiler 측정

각 단계는 사용자 승인 후 진행하며, 구현 완료 시 변경 파일과 테스트 결과를 보고한다.

### 10.3. 작업 결과 보고

각 작업 완료 후 다음 내용을 포함한다.

1. 구현한 기능
2. 변경된 Script / Scene / Prefab
3. Inspector 연결이 필요한 항목
4. 테스트 수행 결과
5. 알려진 문제 및 제한 사항
6. 다음 작업에 필요한 변경 사항

---

## 11. 완료 기준

다음 조건을 모두 만족하면 이번 시스템의 기본 구현을 완료한 것으로 판단한다.

1. 게임 시작 시 100×100 단일 바닥 맵 생성
2. Seed에 따라 나무와 풀이 재현 가능하게 배치
3. 나무와 풀의 이동 가능 여부가 Cost Field에 반영
4. Integration Field가 플레이어까지의 최소 누적 비용 계산
5. Flow Field가 장애물을 고려한 이동 방향 생성
6. Enemy가 공용 방향장을 참조하여 플레이어 추적
7. 장애물 우회 및 대각선 모서리 통과 방지
8. 플레이어 이동에 따라 경로 데이터 갱신
9. 여러 Enemy에서 정상 동작 및 성능 검증
10. 주요 데이터를 Scene 뷰에서 시각적으로 확인 가능

**이번 구현은 100×100 맵에서 안정적으로 동작하는 Flow Field 시스템 구축까지 진행한다.**

Chunk Streaming, Local Flow Field, Job System 및 기타 대규모 최적화는 필요성이 확인될 때 별도 작업으로 진행한다.