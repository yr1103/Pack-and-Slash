# Tech Specification

**버전:** v2.3 (2026-11-10)
**기준 기획서:** `Docs/Spec/GDD.md`

> 매 세션 읽는 허브 문서다. 기술 환경·담당 경계·기능 문서 목록·우선순위만 둔다.
> 게임 규칙의 원본은 GDD, 구현 계약의 원본은 `Docs/Spec/`의 기능 문서다. 기능 작업 시 해당 기능 문서와 그 문서가 가리키는 GDD 절만 읽는다.

---

## 1. 프로젝트 제원

- **프로젝트명:** 미정
- **장르:** 탑다운 자동 사격 생존 액션 / 격자 인벤토리 로그라이트
- **개발:** 1인 / 핵심 개발 목표 14일 / 과제 기간 2026-11-06 ~ 2026-11-24
- **목표:** 직접 구현한 자료구조·알고리즘의 동작 및 성능 검증, 컨텍스트 기반 AI 협업 워크플로 실습

## 2. Unity 환경

- **Unity 버전:** `6000.3.9f1`
- **렌더 파이프라인:** URP
- **빌드 플랫폼:** Windows
- **개발 도구:** Rider / Claude Code / Codex
- **플레이 조작:** WASD 이동, 마우스 기반 인벤토리 및 Storage 조작

## 3. 입력 시스템

- **New Input System만 사용.** 레거시 `InputManager` 사용 금지
- 입력 기능 구현 시 `InputActionReference`를 사용해 `InputAction` 참조
- 여러 컴포넌트가 같은 액션을 공유할 경우, 중복 Enable/Disable 및 소유권은 구현 전 확인
- 필수 입력 범위: 플레이어 이동, 아이템 선택·드래그·드롭, 아이템 회전, 판매·리롤·완료 UI
- 구체적인 입력 액션 에셋 구성과 회전 단축키는 별도 브리핑에서 결정

---

## 4. 담당 경계

> **핵심 알고리즘의 자료구조 및 내부 설계는 개발자가 직접 결정한다.** AI Agent는 명시적 승인 없이 알고리즘을 구현·수정하거나 내부 구조를 확정하지 않는다. (예외 : `Docs/Spec/Map/Map_prototype.md` 범위는 개발자 승인에 따라 AI Agent가 구현한다)

| 영역 | 개발자 직접 설계·구현 | AI Agent 구현 지원 가능 |
|---|---|---|
| 몬스터 이동 | 검수 (`Docs/Spec/Map/Map_prototype.md` 범위는 AI Agent가 구현) | Flow Field / Vector Field, Spatial Hash, Separation의 자료구조·갱신 정책·이웃 탐색·이동 방향 계산 (`Map_prototype.md` 범위), Enemy 체력·피격·사망·스폰, 기본 공격, 보스 패턴, 시각 효과, 디버그 UI |
| 인벤토리 | 셀 상태·점유 표현, 좌표·90도 회전, 범위/잠금/충돌 판정, 인접 탐색, 합성 가능 여부, 확장 가능 칸 판정 | 보상 UI, Storage UI·상태 흐름, 아이템 외형·툴팁, 드래그 입력 연결, 판매·리롤·수치 적용 |
| 그 외 | 알고리즘 성능 실험 및 근거 정리 | 캐릭터 선택/데이터, 플레이어 이동, 일반 전투, 경험치 보석, 무기 발사·패시브 수치·진화 효과, 보상 생성, 재화, 테스트 케이스, 통합 검증, 성능 측정 도구, 문서화 |

- 담당 영역을 넘는 수정이 필요하면 구현 전에 승인받는다.
- 개발자가 구현한 알고리즘을 AI가 임의로 재작성하거나 성능 최적화를 이유로 교체하지 않는다.

---

## 5. 기능 문서 (On-Demand)

| 기능 | 문서 | 관련 GDD 절 |
|---|---|---|
| 게임 흐름 (상태·일시정지·연속 레벨업) | `Docs/Spec/GameFlow.md` | §2, §9.1, §9.5 |
| 몬스터 이동 | `Docs/Spec/MonsterMovement.md` | §12 |
| 맵 생성·Flow Field (작업 지시서, Separation·최소 힙 예시 포함) | `Docs/Spec/Map/Map_prototype.md` | §12 |
| 인벤토리 (장착 격자·Storage·합성·확장) | `Docs/Spec/Inventory.md` | §7, §8, §10 |
| 보상·판매·리롤·재화 | `Docs/Spec/Reward.md` | §9.2 ~ §9.4, §11 |
| 전투 (경험치·무기·패시브·진화·캐릭터) | `Docs/Spec/Combat.md` | §2.1, §3 ~ §6 |
| 무기 상세 (조준·발사·투사체·배치·주무기 승계·고유 아이템 탈착) | `Docs/Spec/Weapon/WeaponSystem.md` | §3 ~ §6 |
| 무기 작업 지시서 (W00 ~ W13) | `Docs/Spec/Weapon/WeaponAgentTasks/README_weapon.md` | - |
| 보스 | `Docs/Spec/Boss.md` | §13 |
| 작업 순서 (로드맵) | `Docs/Spec/Roadmap.md` | §16 |

- 기능 문서는 `데이터 / 계약 / 테스트 기준 / 미정 파라미터` 순서로 작성한다. (`Roadmap.md`는 작업 순서 문서, `WeaponSystem.md`는 상세 설계서, `README_weapon.md`·`Map_prototype.md`는 작업 지시서로 예외)
- 무기 관련 내용이 `Combat.md`·GDD와 충돌하면 `WeaponSystem.md`를 따른다.
- 데이터 표는 **개념적 필드**이며 클래스명·구조체명·저장 형식을 강제하지 않는다.
- `미정 파라미터`는 AI Agent가 임의로 확정하지 않는다.

---

## 6. 공통 미정 사항

- **3D 탑다운 뷰로 확정.** 맵은 Low Poly Nature Bundle(Unity Asset Store) 에셋으로 구성한다.
- 충돌·이동 처리(Transform/Rigidbody 등), 폴더·씬·프리팹 구조 : 기능 착수 전 브리핑에서 결정
- 인터페이스/API 이름과 데이터 저장 형식 : 착수 브리핑에서 승인 후 확정
- 아이템 효과·공격 동작은 데이터와 동작 시스템 분리를 우선 검토한다. ScriptableObject 채택 여부는 브리핑에서 결정

---

## 7. 구현 우선순위

> **알고리즘 학습과 검증 > 필수 게임 루프 통합 > 콘텐츠·연출 확장**
