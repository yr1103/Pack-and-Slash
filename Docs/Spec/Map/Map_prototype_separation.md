## 추가 작업 F — Enemy Separation 및 겹침 방지

### 구현 목적

Flow Field를 공유하는 Enemy들이 동일한 방향으로 이동하면서 서로 겹치는 현상을 줄인다.

전체 Flocking 알고리즘 대신 Separation 행동만 적용한다.

### 구현 요구사항

1. Flow Field 기반 Enemy 이동이 정상적으로 작동하는지 먼저 검증한다.
2. Enemy 주변 일정 거리 내에 다른 Enemy가 있는지 확인한다.
3. 주변 Enemy와의 거리 및 방향을 기반으로 Separation 벡터를 계산한다.
4. Flow Field 방향과 Separation 방향을 가중 합산하여 최종 이동 방향을 계산한다.
5. Separation 반경 및 가중치는 Inspector에서 조절할 수 있도록 한다.
6. Separation으로 인해 나무 장애물을 통과하거나 이동 불가능한 타일에 진입하지 않도록 처리한다.
7. 주변 Enemy가 없는 경우 기존 Flow Field 방향을 그대로 사용한다.

### 성능 요구사항

- 모든 Enemy를 매 프레임 전수 비교하는 O(N²) 구조를 최종 구현으로 사용하지 않는다.
- 초기 테스트에서 주변 개체 탐색 성능을 측정한다.
- Enemy 수 증가로 인한 부담이 확인되면 Spatial Hash Grid를 적용한다.
- Spatial Grid의 셀 크기는 Enemy의 크기와 Separation 반경을 기준으로 결정한다.
- 필요하지 않은 Job System, Burst 및 전체 Flocking 알고리즘은 추가하지 않는다.

> 측정 결과와 Spatial Hash Grid 미적용 결정은 `Docs/TechDocs/Map.md`를 따른다.

### 검증 항목

- [x] Enemy들이 서로 겹치는 현상이 감소하는가?
- [x] 다수 Enemy가 플레이어를 자연스럽게 포위하는가?
- [x] 장애물 근처에서도 이동이 안정적인가?
- [x] Separation이 Flow Field 추적을 방해하지 않는가?
- [x] Enemy 수가 증가해도 프레임이 안정적인가? (500마리 측정, Grid 미적용 결정)
- [ ] 기존 Enemy 이동 및 넉백 로직과 충돌하지 않는가? (넉백 미구현으로 해당 없음, 넉백 구현 시 확인)

### 구현 시점

기본 Flow Field 및 Enemy Movement 검증이 완료된 후 별도 작업으로 착수한다.

Separation을 도입하기 전후의 이동 결과와 성능을 비교하여 사용자에게 보고한다.