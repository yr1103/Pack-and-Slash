# Roadmap

> 기능 단위 작업 순서와 단계별 결정 사항. 각 단계의 구체 내용은 해당 단계 착수 브리핑에서 결정한다.
> 관련 GDD : §16

---

## 1. 담당

- **개발자 직접 구현 :** 인벤토리 (`Inventory.md`), Flow Field / Vector Field · Spatial Hash (`MonsterMovement.md`)
- **AI Agent :** 아래 작업 순서의 각 단계. 개발자가 위 핵심 기능을 구현하는 동안 순서대로 진행한다.
- 보스 이펙트 및 추가 효과는 AI Agent가 진행한다.

## 2. 작업 순서

| # | 단계 | 담당 | 해당 단계 브리핑에서 정할 사항 |
|---|---|---|---|
| 1 | Scene 생성 + `MapGridProvider` | Agent | 씬 구성, 3D 탑다운 카메라, Low Poly Nature Bundle 기반 맵 배치, Flow Field가 받을 맵 데이터 형식(**임시 형식으로 작성, 개발자 확정 후 수정 필수**) |
| 2 | UI 작업 | Agent | 작업 범위, UI 프레임워크 |
| 3 | 플레이어 이동 | Agent | 이동 처리 방식, Input Action 구성 |
| 4 | 무기 구조(무기 추가 가능) + 기본 enemy 1종 + 임시 이동 | Agent | 무기 데이터·동작 분리 방식, Enemy 생명주기 구조, 임시 추적 이동 |
| 5 | 디버그 레벨업 키 | Agent | 키 입력 시 **플레이어 레벨** 상승, 입력 액션 구성, 빌드 포함 여부 |
| 6 | 무기 3종 | Agent | 무기별 파라미터 |
| 7 | 기본 enemy 5종 | Agent | 종류별 차이, 스폰 규칙, lerp 이동 몬스터의 장애물 처리 |
| 8 | 무기 Lv.3 효과 | Agent | 넉백 이동 상태 처리 |
| 9 | 패시브 아이템 | Agent | 임시 장착 목록 기준 시너지 처리 |
| 10 | 무기 진화 | Agent | 진화 조건 검사 위치, 패시브 소모/유지 |
| 11 | 경험치·레벨업 효과 | Agent | 일시정지 방식, 연속 레벨업 처리 |
| 12 | 레벨업 UI 보상 선택 (3중 2택) | Agent | 인벤토리 API 연결 시점 |
| 13 | 보스 enemy(중간 보스 3종 + 20분 최종 보스) + 이펙트 + 보물상자 보상 (5중 3택) | Agent | 최종 보스 탄환 궤적(15분 보스와 구분), 보물상자 접촉 획득, 돌진 이동 상태 처리, 보물상자 보상의 판매·리롤 규칙 |
| 병행 | 인벤토리, Flow Field / Vector Field, Spatial Hash | 개발자 | 내부 설계 전부 |

- 무기·패시브 레벨을 올리는 디버그 수단은 보류한다. (필요 시 별도 브리핑)
- 시스템 간 경계 계약은 `MonsterMovement.md`, `Inventory.md`를 따른다.

**확정 사항** (Issue #3에서 확정)

- 1단계 탑다운 카메라 : Cinemachine (`CinemachineCamera` + `CinemachineFollow`, 월드 고정 오프셋·고정 각도)
- 3단계 이동 처리 : 플레이어 루트의 `CharacterController.SimpleMove`, Move 입력은 월드 XZ 평면 기준
- 3단계 Input Action : 기존 `InputSystem_Actions`의 `Player/Move`를 수정 없이 사용, `InputActionReference`로 참조
