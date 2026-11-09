# Github Issue 작성 규칙

> Github Issue 작성 시 따라야 하는 규칙이다.
>
> 이슈 작성 양식은 `@Docs\Template\Issue Template.md`를 참고.
>
> 해당 문서는 각 섹션에 대한 작성 규칙을 정의한다.

## 1. Title

- 제목은 `[Tag] 작업 내용` 형식으로 작성한다.
- `[Tag]`는 Issue의 Label과 동일하게 작성하되, `7. 기타` > `Tag 표기`를 따른다.
- `[Tag]`를 제외한 작업 내용을 20자 이내로 적는다.
- 상세한 키워드가 아닌 `기능` 단위로 적는다.
- 예시 :
    ```markdown
    - [Feat] 플레이어, 이동 구현
    - [Feat] 몬스터 피격판정 구현
    - [Feat] Weapon 베이스 클래스 생성
    ```

## 2. 배경 / 목적

- 이슈 작성 시, 해당 섹션에 기재할 내용을 사용자에게 질문하며, 해당 조건이 충족되지 않을 시 이슈 작성을 거부한다.
- 리스트로 작성하며, 불필요한 표현을 제거해 간결하게 작성한다.
- 예시 :
    ```markdown
    ## 배경 / 목적

    - 플레이어 피격 판정 시, 데미지 수치와 공격판정에 필요한 데이터 동시 전달 필요
    - `DamageInfo` 구조체를 전달하는 방식으로 피격 판정 개선
    ```

## 3. 목표 (To-be)

- 작업이 완료 되었을 때의 예상 동작 혹은 결과를 기재한다.
- 리스트로 작성하며, 불필요한 표현을 제거해 간결하게 작성한다.
- 예시 :
    ```markdown
    ## 목표 (To-be)
    
    - 플레이어와 몬스터간 공격판정 시, `IDamageable` 인터페이스의 `TakeDamage(DamageInfo info)`를 통해 공격 판정 개선
    - `DamageInfo` 구조체가 데미지를 주는 객체로부터 받는 객체로 전달
    ```


## 4. 작업 내용

- 예상되는 프로젝트의 생성/수정/삭제에 대한 변경 사항을 기재한다.
- 리스트로 작성하며, 특정 경로의 파일에 어떤 작업이 이루어지는지 기재한다.
- 예시 :
    ```markdown
    ## 작업 내용

    - `Assets/Scripts/PlayerController.cs` 수정
    - `Assets/Scripts/Core/IDamageable.cs` 수정
    - `Assets/Scripts/Core/DamageInfo.cs` 생성
    - `Assets/Prefabs/Player/PFB_Player.prefab` 수정
    ```

## 5. 완료 조건(DoD)

- 작업을 완료처리 하기 전의 체크리스트 확인 목적으로 작성한다.
- `3. 목표 (To-be)` 및 `4. 작업 내용`의 내용과, 유니티 에디터에서의 정상 구동 여부를 체크리스트로 둔다.
- 예시 :
    ```markdown
    ## 완료 조건(DoD)

    - [ ] `PlayerController` 객체와 `MonsterController` 객체가 `IDamageable` 인터페이스를 통해서만 데미지 판정을 주고받는다.
    - [ ] `IDamageable.TakeDamage(DamageInfo info)` 메서드를 통해 공격을 시도한 객체에서 공격을 받는 객체로 `DamageInfo` 구조체가 전달된다.
    - [ ] 공격을 받는 객체에서 `DamageInfo` 구조체의 정보를 받아 로그 출력에 성공했다.
    - [ ] 유니티 에디터에서 컴파일 에러 및 Error, Warning 로그가 발생하지 않는다.
    ```

## 6. 특이사항

- 아래의 사항에 해당되지 않으면 이 섹션은 기재하지 않는다.
    1. 해당 이슈가 진행되기 위한 선행 조건이 있는 경우
    2. `Github Issues`에 등록된 다른 Issue와 연관되어 있는 경우
    3. 해당 이슈를 통해 해소된 블로커 및 이슈가 있는 경우
- `Github Issues`에 등록된 Issue를 언급하는 경우, 태그를 걸어둔다.


## 7. 기타

### Label

- Label은 아래 항목 중 하나를 채택하여 걸어둔다.

| Label | 용도 |
|---|---|
| Bug | 버그 발생 시, 버그 상황 설명 및 재현 과정 기록 |
| Fix | 버그 수정 작업 |
| Docs | 문서 관련 작업을 수행 |
| Feature | 새로운 기능을 추가 |
| Art | 프로젝트의 디자인, 에셋, 이미지, 로고 등과 관련된 작업 |
| Refactor | 기능 동작을 유지하는 코드 구조 개선 |
| Chore | 기타 유지보수 작업, 설정 및 패키지 정리 |
| Test | 테스트 작성 및 검증 작업 |
| Merge | main-develop → main 개발 단계 병합 |

### Tag 표기

- Issue, PR 제목과 커밋 메시지에 직접 작성하는 `[Tag]`는 `Feature` 대신 `Feat`으로 적는다.
- `Feature` 외의 Label은 이름 그대로 `[Tag]`로 적는다. (예 : `[Fix]`, `[Merge]`)
- Label은 위 표의 이름(`Feature`) 그대로 걸어둔다.

    
