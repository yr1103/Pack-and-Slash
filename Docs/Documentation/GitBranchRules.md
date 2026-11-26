# Git Branch 규칙

> 작업 브랜치 생성 시 따라야 하는 규칙

---

## 1. 브랜치 구조

| 브랜치 | 역할 |
|---|---|
| `main` | 큰 단위 개발이 마무리된 결과만 보관 |
| `main-develop` | 개발 통합 브랜치. 모든 작업 브랜치의 분기·PR 기준 |
| 작업 브랜치 | Issue 단위 작업 |

## 2. 작업 브랜치 생성

- 작업 브랜치는 `main-develop`을 기준으로 생성한다.
- 가능하면 Issue의 `Development → Create a branch` 기능을 사용해 Issue와 Branch를 연결한다.
    - 이 기능을 사용할 때 기준 브랜치를 `main-develop`으로 지정한다. (기본값은 `main`)
- 개설 주체는 `WorkflowRules.md`의 `4. 업무 수행`을 따른다.

## 3. 브랜치 이름

- 형식 : `type/작업내용`
- 예시 : `feat/monster-movement`, `fix/tower-targeting`, `docs/unity-convention`
- 작업 내용은 가능한 한 짧고 명확하게 작성하며, 여러 단어는 `-`로 구분한다.

| Type | 의미 |
|---|---|
| feat | 기능 구현 |
| fix | 버그 수정 |
| refactor | 리팩토링 |
| docs | 문서 작업 |
| test | 테스트 |
| chore | 기타 유지보수 작업, 설정 및 패키지 정리 |
| art | 디자인, 에셋, 이미지 작업 |

## 4. `main-develop` → `main` 병합

- 큰 단위 개발 단계가 마무리되었을 때만 병합한다.
    - 예 : 최소 단위 프로토타입, UI 및 최소 단위 게임 플레이, 폴리싱
- 목적 : 병합 실수 시 복구 지점 확보, `main`에 불필요한 내용 포함 방지
- 병합 시점은 사용자가 결정한다.
- `Merge` Label로 Issue를 등록한 뒤 PR로 진행한다. PR 양식은 `Docs/Template/Merge PR Template.md`를 따른다.

### 제외 파일

- 아래 파일·폴더는 `main`에 포함하지 않는다. (`main-develop` 전용)

| 대상 | 이유 |
|---|---|
| `.codex/`, `.agents/`, `AGENTS.md` | Codex 작업 설정 (`cd6c437`에서 정리) |
| `.idea/`, `.mcp.json` | 로컬 IDE·MCP 설정 (`.gitignore` 처리) |
| `Assets/Scenes/*/LightingData.asset`, `ReflectionProbe-*.exr` (Scene 하위 폴더) | Generate Lighting 부산물 |
| `Assets/TutorialInfo/`, `Assets/Readme.asset` | URP 템플릿 안내 파일 |
| `Assets/_Recovery/` | 크래시 복구 Scene |

- Scene의 `m_LightingDataAsset` 참조는 `main` 기본값을 유지한다.
- 새로 제외할 파일이 생기면 이 표에 추가한다.

### 병합 전 확인

1. 직전 `main` 병합 이후 변경 파일을 확인한다. : `git diff --name-status origin/main...origin/main-develop`
2. 변경 목록에 제외 파일이 있으면, `main`을 기준으로 만든 작업 브랜치(`chore/merge-develop-to-main`)에서 병합하고 제외 파일을 정리한다. 없으면 바로 PR로 진행한다.
3. 삭제·복원할 파일은 실행 전에 파일 단위 목록으로 승인받는다.
4. 제외 처리 내용은 Merge PR의 `리뷰어에게 전달할 주의사항`에 적는다.
