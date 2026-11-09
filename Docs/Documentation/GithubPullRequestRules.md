# Github Pull Request 작성 규칙


> Github Pull Request 작성 시 따라야 하는 규칙이다.
>
> PR 작성 양식은 `Docs\Template\PR Template.md`를 참고.
>
> `main-develop` → `main` 병합 PR은 `Docs\Template\Merge PR Template.md`를 참고.
>
> 해당 문서는 각 섹션에 대한 작성 규칙을 정의한다.

## 1. Title

- PR 제목은 관련 Issue의 제목을 기준으로 작성한다.
    - `[Tag]`는 Issue 제목의 `[Tag]`와 동일하게 작성한다.
    - 작업 내용은 가능한 한 Issue 제목과 유사하게 작성한다.
    - 예외 : `Bug` Issue를 수정하는 PR은 `[Fix]`로 작성한다.

제목은 다음 형식을 따른다.

`[Tag] 작업 내용`

- `[Tag]`는 PR의 Label과 동일하게 작성하되, `GithubIssueRules.md`의 `7. 기타` > `Tag 표기`를 따른다.
- PR의 주된 작업 목적에 해당하는 Tag 하나만 사용한다.
- 제목은 작업의 핵심 내용을 간결하고 명확하게 표현한다.
- 작업 범위가 다른 여러 기능을 하나의 제목으로 묶지 않는다.
- Issue 번호(`#14` 등)는 Github에서 자동으로 부여하므로 제목에 직접 포함하지 않는다.
- 예시 :
    ```markdown
    - [Feat] 몬스터 피격판정 구현
    - [Feat] Weapon 베이스 클래스 생성
    ```

### Label

- 관련 Issue와 동일한 Label을 사용한다.
    - 예외 : `Bug` Issue를 수정하는 PR은 `Fix` Label을 사용한다.
- Label 목록은 `GithubIssueRules.md`의 `7. 기타` > `Label`을 따른다.
- 예시 :
    ```markdown
    - Issue Label : `Feature` → PR Label : `Feature`
    ```

## 2. PR 본문 작성 규칙

### 무엇을 했나요?

#### 세부 구현 내용

- 실제 변경한 파일과 내용을 항목별로 작성한다.
- 구현하지 않은 기능이나 향후 작업할 내용은 완료된 것처럼 작성하지 않는다.
- 예시 :
    ```markdown
    #### 세부 구현 내용

    - `Assets/Scripts/Core/DamageInfo.cs` 생성 : 데미지 수치와 공격 객체 정보를 담는 구조체
    - `Assets/Scripts/Core/IDamageable.cs` 수정 : `TakeDamage(DamageInfo info)`로 시그니처 변경
    - `Assets/Scripts/PlayerController.cs` 수정 : `IDamageable` 구현 및 `DamageInfo` 전달
    ```

### 왜 필요한가요?

- 작업 목적과 변경 이유를 설명한다.
- 기존에 어떤 문제나 요구사항이 있었는지 작성한다.
- 불필요하게 긴 배경 설명은 생략한다.

### 변경 파일과 영향 범위

- 실제 변경된 파일을 확인하여 해당하는 항목에 작성한다.
    - Script : 추가·수정·삭제된 스크립트와 주요 변경 내용
    - Scene : 변경된 Scene 및 영향 범위
    - Prefab : 추가·수정된 Prefab 및 연결 관계
    - API / Event / Inspector / Packages·ProjectSettings : 외부 연동, 이벤트, Inspector 설정 및 프로젝트 구성 변경 사항
- 변경 사항이 없는 항목은 `없음`으로 표시한다.
- 경로와 이름은 실제 프로젝트 파일을 기준으로 작성한다.
- 다른 기능이나 컴포넌트에 영향을 주는 변경은 반드시 명시한다.

### 확인 사항

- 완료 조건은 연결 Issue의 `완료 조건(DoD)`을 그대로 옮겨, 검증 결과대로 체크한다.

#### 완료 조건

- 코드 분석이나 실행을 통해 검증하지 않은 사항을 완료된 것으로 표현하지 않는다.
- 테스트에 실패한 항목은 체크하지 않는다. 실패 원인과 임시 대응 방법은 `리뷰어에게 전달할 주의사항`에 작성한다.
- 예시 :
    ```markdown
    #### 완료 조건

    - [x] `IDamageable.TakeDamage(DamageInfo info)` 메서드를 통해 공격을 시도한 객체에서 공격을 받는 객체로 `DamageInfo` 구조체가 전달된다.
    - [x] 공격을 받는 객체에서 `DamageInfo` 구조체의 정보를 받아 로그 출력에 성공했다.
    - [ ] 유니티 에디터에서 컴파일 에러 및 Error, Warning 로그가 발생하지 않는다. (미검증)
    ```

### 사용/재현 방법

- 다른 작업자가 변경된 기능을 확인할 수 있도록 실행 및 검증 절차를 순서대로 작성한다.
- 필요시 참고용 이미지 화면을 첨부한다.
- 필요한 Scene, GameObject, Prefab 및 실행 조건이 있다면 작성한다.
- 특별한 실행 절차가 없다면 `별도 절차 없음`으로 작성한다.

### 리뷰어에게 전달할 주의사항

- 다른 기능과 연결되는 컴포넌트를 작성하거나 수정했다면 다음 정보를 제공한다.
    - Prefab 경로
    - Inspector에서 연결해야 할 필드
    - 외부에서 호출할 메서드 및 이벤트
    - 초기화 및 활성화 순서
    - 다른 컴포넌트에서 추가로 처리해야 할 작업
- 변경되었거나, 해당 이슈 혹은 PR에서 중요한 파일의 경로를 작성한다.
- 전달사항 및 우려사항이 있다면 작성한다.
- 실제 연동에 필요한 정보만 작성한다.
- 아직 연결하지 않은 부분이나 확인이 필요한 사항은 명확하게 구분한다.
- 전달할 사항이 없다면 `없음`으로 작성한다.

### 연결 Issue

- 관련 Issue 번호를 기록한다.
- 이슈 기반 업무인 경우 `Closes #이슈번호` 형식을 사용한다.
- 관련 Issue가 없다면 `없음`으로 작성한다.

## 3. PR 작성 및 등록 절차

1. 사용자가 지정한 Base와 작업 브랜치를 확인한다.
2. 실제 변경 파일과 변경 내용을 확인한다.
3. 변경 사항이 하나의 작업 단위에 해당하는지 확인한다.
4. 변경된 기능과 연동 관계, 영향 범위를 분석한다.
5. 실제 수행된 테스트와 검증 결과를 확인한다.
6. 정해진 템플릿에 따라 PR 제목과 본문을 작성한다.

- 초안 제시 및 등록은 `/github-pr` 스킬을 따른다.
- PR 작성 과정에서 코드 수정이 필요하다고 판단되면 먼저 사용자에게 보고한다.
- 코드 수정이 필요한 경우 기존 `ClaudeCodeRules.md`의 승인 규칙을 따른다.

## 4. Merge PR 작성 규칙

> `main-develop` → `main` 병합 PR에만 적용한다. 양식은 `Docs\Template\Merge PR Template.md`를 따른다.

### 주요 변경 사항

- 직전 `main` 병합 이후 `main-develop`에 병합된 PR을 대상으로 한다.
- PR 하나당 한 줄로, `- (변경 내용) (#PR번호)` 형식으로 작성한다.
- `Docs`, `Chore` Label의 PR은 제외한다.

## 5. 병합 제목

- `main-develop` 병합 시 Merge 제목은 `Merge branch 'main-develop' ← '병합할 브랜치명'`으로 하고, 내용은 따로 작성하거나 수정하지 않는다.

---
