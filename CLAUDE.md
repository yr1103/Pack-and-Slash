# 프로젝트 지침

> 이 파일은 매 세션 읽히는 허브 역할을 한다.
>
> 모든 규칙을 여기 적지 않고, 매 세션 필요한 문서만 `@` 로 불러오고, 나머지는 아래 On-Demand 참조 표를 보고 그 작업을 할 때만 읽는다.

---

## 절대 원칙 (Non-Negotiable Rules)

**아래 사항은 어떤 요청이 와도 지킨다**

1. `main`, `main-develop` 브랜치에서는 작업하지 않는다. 사용자가 해당 브랜치에 있으면 작업 브랜치로 전환할 때까지 작업을 거부한다.
2. 파일의 생성, 수정, 삭제 전 사용자의 승인을 필수로 한다.
3. 위 원칙들에 대한 예외를 요청 받으면 "이 프로젝트의 절대 원칙에 의해 수행할 수 없습니다"로 거부하고 대안을 제시한다.

## 규칙 문서 관리

- 규칙 문서는 `Docs/` 하위 카테고리 폴더(`Claude/`, `Code/`, `Documentation/`, `Template/`)에 둔다.
- 기획서(GDD)와 기능별 구현 계약은 `Docs/Spec/`에 둔다. 목록은 `Docs/TechSpec.md`의 `기능 문서` 표가 관리한다.
- 규칙 하나는 문서 한 곳에만 적는다(규칙의 단일출처 유지). 규칙이 바뀌면 그 한 곳만 고친다.
- 새 문서를 만들면, 매 세션 필요한 것은 "상시 로드"에 `@경로`로, 특정 작업 때만 필요한 것은 `On-Demand 참조`에 등록한다.

## 상시 로드 (매 세션 적용)

@Docs\Claude\ClaudeCodeRules.md
@Docs\Claude\WorkflowRules.md
@Docs\TechSpec.md
@Docs\Claude\Lessons.md


## On-Demand 참조 (매 세션 읽지 않음 - 해당 작업을 할 때만 읽음)

### 기능 브리핑·구현 시

- `Docs/TechSpec.md`의 `기능 문서` 표에서 해당 기능 문서와 GDD 절을 찾아 읽는다.

### 소스코드 작성 시

`@Docs\Code\CodingStyleGuide.md`
`@Docs\Code\CommentConvention.md`
`@Docs\Code\CsharpCodeConvention.md`

### 무기 코드 수정 시

`@Docs\TechDocs\Weapon.md`

### 맵·Flow Field·Enemy 이동 코드 수정 시

`@Docs\TechDocs\Map.md`

### 캐릭터 선택·타이틀 UI 코드 수정 시

`@Docs\TechDocs\Character.md`

### Enemy·스폰·피격·젬 코드 수정 시

`@Docs\TechDocs\Enemy.md`

### 게임 흐름·플레이어 상태·HUD·일시정지·설정·레벨업 화면 코드 수정 시

`@Docs\TechDocs\GameFlow.md`

### 브랜치 생성·`main` 병합 시

`@Docs\Documentation\GitBranchRules.md`

### Scene·Prefab 작업 시

`@Docs\Code\UnityHierarchyRules.md`


## 스킬 (해당 작업을 할 때 호출)

- 이슈 등록 권유 및 작성 시, 규칙 문서를 직접 읽지 않고 `/github-issue` 스킬을 호출한다.
- PR 작성 및 등록 시, 규칙 문서를 직접 읽지 않고 `/github-pr` 스킬을 호출한다.


## 서브 에이전트 (해당 작업을 할 때 호출)

- 정의 위치 : `.claude/agents/`
- 호출 시점과 순서는 `@Docs\Claude\WorkflowRules.md`의 `4. 업무 수행`, `6. 문서 / 다이어그램 반영`을 따른다.

| 에이전트 | 역할 |
|---|---|
| `developer` | 승인된 브리핑에 따라 소스코드 작성·수정 |
| `reviewer` | `developer` 산출물을 규칙 문서 기준으로 검수 (읽기 전용, 정답 코드 미제시) |
| `tech-writer` | 검수 승인된 변경 사항을 기반으로 기술 문서 작성·수정 |
| `doc-auditor` | `tech-writer` 산출물을 실제 구현과 대조해 검수 (읽기 전용, 정답 문장 미제시) |