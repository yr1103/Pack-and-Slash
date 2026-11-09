---
name: github-pr
description: Github Pull Request 작성 및 등록 절차. 작업 브랜치의 Push가 끝난 뒤 PR 작성/등록이 필요할 때, 또는 사용자가 PR 작성/등록을 요청할 때 호출한다.
---

# Github Pull Request 등록

> 이 스킬은 **절차와 멘트 규격**만 정의한다.
>
> 작성 규칙과 양식의 원본은 아래 문서이며, 이 스킬에 규칙을 복사하지 않는다. (규칙 단일출처 유지)
>
> - 작성 규칙 : `Docs/Documentation/GithubPullRequestRules.md`
> - Tag / Label 규칙 : `Docs/Documentation/GithubIssueRules.md`의 `7. 기타`
> - 작성 양식 : `Docs/Template/PR Template.md`
> - 작성 양식 (`main` 병합) : `Docs/Template/Merge PR Template.md`
> - 상위 흐름 : `Docs/Codex/WorkflowRules.md`의 `## 8. PR`

---

## 절차

각 단계는 이전 단계가 완료되어야 진행한다. 단계를 건너뛰지 않는다.

### 1. 진입 확인

- 현재 작업 브랜치와 원격 Push 여부를 확인한다.
    - Push되지 않은 커밋이 있으면 PR 작성을 진행하지 않고 사용자에게 보고한다.
- 연결된 Issue 번호를 확인한다. 불분명하면 사용자에게 질문한다.
- 사용자가 Base 브랜치를 지정하지 않았으면 `[멘트 A] Base 확인` 규격으로 질문한다.
- Git 명령은 읽기 전용(`git status`, `git log`, `git diff`, `git branch` 등)만 사용한다. Commit, Push, 병합, 브랜치 전환은 수행하지 않는다.

### 2. 규칙 로드

- 작성 전 아래 문서를 읽는다.
    - `Docs/Documentation/GithubPullRequestRules.md` : 전체
    - `Docs/Documentation/GithubIssueRules.md` : `7. 기타` 섹션
    - 양식 코드 블록 : 1단계에서 확인한 Base에 따라 하나만 읽는다.
        - Base가 `main` : `Docs/Template/Merge PR Template.md`
        - 그 외 : `Docs/Template/PR Template.md`
- 이전에 읽은 기억에 의존하지 않고, 매 호출마다 다시 읽는다.

### 3. 작성 준비

- `GithubPullRequestRules.md`의 `3. PR 작성 및 등록 절차`에 따라 변경 내용과 검증 결과를 확인한다.
- 연결된 Issue가 있으면 Issue 본문을 조회해 제목, Label, 완료 조건(DoD)을 확인한다.
    ```
    gh issue view <번호> --json title,labels,body
    ```
- Base가 `main`이면 `주요 변경 사항`에 넣을 PR을 수집한다.
    1. 직전 `main` 병합 시점을 확인한다.
        ```
        gh pr list --base main --state merged --limit 1 --json mergedAt
        ```
    2. 그 이후 `main-develop`에 병합된 PR을 조회한다. 1번 결과가 비어 있으면 `--search` 조건 없이 전체를 조회한다.
        ```
        gh pr list --base main-develop --state merged --search "merged:><mergedAt>" --json number,title,labels
        ```
    3. 대상과 제외 기준은 `GithubPullRequestRules.md`의 `4. Merge PR 작성 규칙`을 따른다.

### 4. 초안 작성 및 승인 요청

- 2단계에서 읽은 규칙과 양식에 따라 초안을 작성한다.
- `[멘트 B] 초안 승인 요청` 규격으로 제시하고, 사용자의 승인을 기다린다.
- 수정 요청이 있으면 해당 부분만 고쳐 다시 `[멘트 B]`로 제시한다.

### 5. 등록

- 승인을 받은 후에만 등록한다.
- 양식의 `# [Tag] 작업 내용` 줄은 Title로 사용하고, 본문에서는 제외한다.
- 본문은 스크래치패드 경로에 임시 파일로 저장한 뒤 `gh` CLI로 등록한다.
    ```
    gh pr create --base "<Base>" --head "<작업 브랜치>" --title "<Title>" --body-file "<임시 파일 경로>" --label "<Label>"
    ```
- 등록 후 `[멘트 C] 등록 완료` 규격으로 보고한다.
- 병합과 브랜치 파기는 사용자가 진행한다. 병합 완료를 전달받으면 `WorkflowRules.md`의 `10. Issue Close`로 진행한다.

---

## 멘트 규격

> 필요한 정보만 간결하게 전달한다. 규격 외의 설명은 덧붙이지 않는다.

### [멘트 A] Base 확인

```markdown
❓ **Base 브랜치 확인**
- 작업 브랜치 : (브랜치명)
- 예상 Base : (브랜치명) (판단 근거 한 줄)

이 Base로 PR을 작성할까요?
```

### [멘트 B] 초안 승인 요청

```markdown
📝 **PR 초안 : (Title)**
- Base ← 작업 브랜치 : (Base) ← (작업 브랜치)
- Label : (Label)
- 연결 Issue : (Closes #번호 / 없음)

(2단계에서 읽은 양식에 따른 본문)

이대로 등록할까요? 수정할 부분이 있으면 말씀해 주세요.
```

### [멘트 C] 등록 완료

```markdown
✅ **PR 등록 완료** : #(번호) (Title)
- 링크 : (PR URL)

병합이 완료되면 알려주세요.
```
