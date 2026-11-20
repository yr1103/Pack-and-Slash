# 게임 흐름·플레이어 상태·HUD (GameFlow)

> 근거 : `Assets/Scripts/GameFlow/GameSession.cs`, `Assets/Scripts/Player/PlayerHealth.cs`, `Assets/Scripts/Player/PlayerExperience.cs`, `Assets/Scripts/UI/GameHUD.cs`, `Assets/Scripts/UI/PlayerHealthBar.cs`, `Assets/Scripts/UI/ResultPanel.cs`, `Assets/Scripts/UI/{GameSettings,SettingsPanel,PauseMenu,LevelUpPanel}.cs`, `Assets/Scripts/Combat/TreasureChest.cs`(`RequestChestReward` 호출부), `Assets/Scripts/Enemy/Enemy.cs`(`ClearsSessionOnDeath`), `Assets/Scripts/Combat/Health.cs`, `Assets/Scripts/Combat/DamageGate.cs`, `Assets/Tests/Editor/{PlayerExperienceTests,GameSessionTests,ResultPanelTests}.cs`, `Assets/Prefabs/Player/PFB_Player.prefab`, `Assets/Prefabs/UI/{PFB_SettingsPanel,PFB_LevelUpPanel,PFB_RewardCard}.prefab`, `Assets/InputSystem_Actions.inputactions`, `Assets/Scenes/GameScene.unity`, `Assets/Scenes/TestMap.unity`, `Assets/Sprites/UI/*.png.meta`, `Assets/Sprites/Inventory/**/*.png.meta`, `Assets/Imports/UI/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/{Slide Bar/F_UI_BlueBar,Menu Icons/F_UI_MenuIcons_Tile Map 16x16}.png.meta`, `.gitignore`
>
> 설계 원본 : `Docs/Spec/GameFlow.md` (이 문서는 현재 구현 상태만 기술한다)
>
> 관련 문서 : Enemy 사망 이벤트·경험치 젬은 `Docs/TechDocs/Enemy.md`, 무기·플레이어 애니메이션은 `Docs/TechDocs/Weapon.md`, 타이틀 메뉴(`TitleMenu`)는 `Docs/TechDocs/Character.md`

## 목적 / 기능 범위

- 전투 한 판(세션)의 진행 상태·생존 시간·처치 수를 관리하고, 클리어(생존 시간 도달 또는 최종 보스 처치)·게임 오버(플레이어 사망)를 판정한다.
- 종료 후 일정 시간 뒤 전투를 정지(`Time.timeScale` 0)하고 결과 패널을 표시한다. 결과 패널에서 타이틀로 돌아간다.
- 전투 중 ESC로 일시정지(`Time.timeScale` 0)하며 설정 판넬을 열고, 닫으면 재개한다. 설정 판넬에서 확인창을 거쳐 타이틀로 돌아갈 수 있다.
- 레벨업마다 일시정지하고 레벨업 화면(인벤토리·보상 카드 표시)을 열며, 쌓인 레벨업 횟수만큼 완료 버튼을 누르면 닫고 재개한다. 보물 상자 보상 요청도 같은 화면을 쓴다(카드 5장, 대기 중인 레벨업 다음 순서). 보상 선택·리롤·인벤토리 배치는 표시만 있고 동작하지 않는다.
- 설정(볼륨·피격 효과·데미지 숫자)을 `PlayerPrefs`에 저장하고 게임 시작 시 불러온다.
- 플레이어 체력·무적 시간·Enemy 접촉 피해, 경험치·레벨을 관리한다.
- 생존 시간·처치 수·경험치 바·레벨·재화(고정 0)를 HUD로, 플레이어 체력을 머리 위 체력바로 표시한다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #8 : `GameSession`(상태·생존 시간·처치 수·종료 판정·전투 정지), `PlayerHealth`, `PlayerExperience`(임시 레벨 곡선), `GameHUD`, `PlayerHealthBar`, `ResultPanel` |
| 포함 | Issue #13 : `GameSession.Pause`/`Resume`, `PauseMenu`(ESC 일시정지·타이틀 복귀 확인창), `SettingsPanel`·`PFB_SettingsPanel`, `GameSettings`(저장·로드), `PlayerHealth` 일시정지 중 접촉 판정 생략 |
| 포함 | Issue #17 : `LevelUpPanel`(레벨업 횟수 누적·일시정지·완료 버튼으로 재개), `PFB_LevelUpPanel`·`PFB_RewardCard`(표시 전용 레이아웃), `PauseMenu`가 레벨업 화면 중 ESC 무시, 경험치 바 스프라이트 교체 |
| 포함 | Issue #19 : `GameSession`의 최종 보스 처치 클리어(`Enemy.ClearsSessionOnDeath`), `LevelUpPanel`의 보물 상자 보상 요청(`RequestChestReward`)·회차 종류별 카드 수(레벨업 3장, 보물 상자 5장). 상자 생성·획득 판정은 `Docs/TechDocs/Enemy.md` |
| 제외 (후속) | 보상 선택·획득(보물 상자 보상 포함), 리롤 동작, 인벤토리 배치·저장고, 재화 획득, 다시하기 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `GameSession` (MonoBehaviour) | 상태 `SessionState`(`Playing`·`Cleared`·`GameOver`), `ElapsedTime`, `KillCount` 소유. 생존 시간이 `_clearTime` 이상이거나 `ClearsSessionOnDeath`인 Enemy(최종 보스)가 죽으면 클리어, 플레이어 사망 시 게임 오버. 종료 후 `_endFreezeDelay`초 뒤 `Time.timeScale = 0`과 `Ended(bool)` 발생. 일시정지 `Pause`·재개 `Resume`(진행 중일 때만). `Time.timeScale`을 바꾸는 유일한 클래스. 판정·표시 함수 `IsCleared`, `FormatTime`(static) | `PlayerHealth`(`Died`), `Enemy`(`Died`, static) |
| `PlayerHealth` (MonoBehaviour) | `IDamageable` 구현. `Health`·`DamageGate` 소유. 매 프레임 `Physics.OverlapSphereNonAlloc`으로 Enemy 접촉 판정 후 피해 (`Time.timeScale` 0이면 생략). 이벤트 `Damaged`, `Died`. 사망 시 `_disableOnDeath` 컴포넌트를 끔. `MaxHealth`, `CurrentHealth`, `IsDead`, `IsAlive` 노출 | `Health`, `DamageGate`, `Enemy`(`ContactDamage`, `IsAlive`), `Physics` |
| `PlayerExperience` (MonoBehaviour) | 누적 경험치 `Experience`, `Level`, 현재 레벨 경험치 `CurrentLevelExperience`, 요구치 `RequiredExperience`, 젬 수집 반경 `CollectRadius`. `AddExperience`로 증가·다중 레벨업·이월. 이벤트 `LevelChanged`, `ExperienceGained`. 요구치 함수 `GetRequiredExperience`(static, 임시 곡선) | 없음 |
| `GameHUD` (MonoBehaviour) | 생존 시간·처치 수는 매 프레임 폴링해 값이 바뀔 때만 텍스트 갱신. 경험치 바·레벨은 `ExperienceGained` 구독으로 갱신. 재화는 "0" 고정 | `GameSession`, `PlayerExperience`, TMP(`TMP_Text`), `Image` |
| `PlayerHealthBar` (MonoBehaviour) | 플레이어 머리 위 World Space 체력바. `PlayerHealth.Damaged`로 채움 비율 갱신, `LateUpdate`에서 카메라와 같은 회전(빌보드) | `PlayerHealth`, `Image`, `Camera` |
| `ResultPanel` (MonoBehaviour) | `GameSession.Ended` 구독. 결과 문구(`CLEAR`/`GAME OVER`)와 통계(생존 시간·레벨·무기 목록·처치 수·진화 무기 수)를 영문으로 표시. 타이틀 버튼으로 `TitleScene` 로드. 문자열 함수 `BuildWeaponList`, `CountEvolved`(static) | `GameSession`, `PlayerExperience`, `WeaponLoadout`, `SceneManager`, TMP, `Button` |
| `PauseMenu` (MonoBehaviour) | ESC(`UI/Cancel`) 입력으로 일시정지하며 설정 판넬을 열고, 닫으며 재개. 설정 판넬의 타이틀 복귀 버튼·확인창(예/아니오) 처리. 입력 액션을 직접 `Enable`/`Disable`. 레벨업 화면이 열려 있으면 ESC 무시 | `GameSession`(`IsPlaying`, `Pause`, `Resume`), `LevelUpPanel`(`IsOpen`), `Button`, `InputActionReference`, `SceneManager` |
| `LevelUpPanel` (MonoBehaviour) | `PlayerExperience.LevelChanged`마다 레벨업 대기 횟수를, `RequestChestReward()`마다 보물 상자 대기 횟수를 1 늘리고, 화면이 닫혀 있으면 `GameSession.Pause()` 후 화면을 연다. 회차 종류는 레벨업 우선이며 종류에 따라 보상 카드 수(3/5)를 정한다. 완료 버튼마다 표시 중인 종류의 대기 횟수를 1 줄이고 둘 다 0이면 화면을 닫고 `Resume()`. 표시 여부 `IsOpen` 노출 | `GameSession`(`Pause`, `Resume`), `PlayerExperience`(`LevelChanged`), `Button`. 호출자 `TreasureChest`(`Docs/TechDocs/Enemy.md`) |
| `SettingsPanel` (MonoBehaviour) | 설정 판넬 UI(볼륨 슬라이더, 피격 효과·데미지 숫자 토글)와 `GameSettings` 동기화. 켜질 때 현재 값 표시, 변경 즉시 반영, 꺼질 때 저장. 열고 닫기는 메뉴 스크립트(`PauseMenu`, `TitleMenu`)가 담당 | `GameSettings`, `Slider`, `Toggle` |
| `GameSettings` (static class) | 설정 값 `Volume`(0~1), `HitFlashEnabled`, `DamageNumberEnabled`를 static 필드에 캐시하고 `PlayerPrefs`에 기록. `Save`로 디스크 기록. 첫 Scene 로드 전 자동 로드 | `PlayerPrefs`, `AudioListener` |

- `Health`·`DamageGate`의 계약은 `Docs/TechDocs/Enemy.md` 참고.
- `GameSettings`의 피격 효과·데미지 숫자 값은 `EnemyHitFeedback`이 읽는다 (`Docs/TechDocs/Enemy.md`). 타이틀의 설정 판넬은 `TitleMenu`가 연다 (`Docs/TechDocs/Character.md`).

## API / 이벤트 / 데이터 계약

### 이벤트

| 이벤트 | 소유 | 인자 | 발생 시점 | 구독자 |
|---|---|---|---|---|
| `GameSession.Ended` | `GameSession` | 클리어 여부(`bool`) | 종료(`End`) 후 `_endFreezeDelay`초 뒤, `Time.timeScale = 0` 직후 1회 | `ResultPanel.HandleEnded` |
| `PlayerHealth.Damaged` | `PlayerHealth` | 없음 | 무적 게이트를 통과해 피해가 적용될 때마다 (사망하는 피해 포함, `Died`보다 먼저) | `PlayerHealthBar.Refresh`, `PlayerAnimator.PlayHit`(`Weapon.md`) |
| `PlayerHealth.Died` | `PlayerHealth` | 없음 | 처음 사망한 피해에서 1회. `_disableOnDeath`를 끈 뒤 발생 | `GameSession.HandlePlayerDied` |
| `PlayerExperience.LevelChanged` | `PlayerExperience` | 새 레벨(`int`) | 레벨업 1회마다. 한 번에 여러 레벨이 오르면 레벨 수만큼 발생 | `LevelUpPanel.HandleLevelChanged` |
| `PlayerExperience.ExperienceGained` | `PlayerExperience` | 이번 추가량(`int`) | `AddExperience`(양수)마다 1회. 같은 호출의 `LevelChanged`가 모두 끝난 뒤 발생 | `GameHUD.HandleExperienceGained` |

- 구독 위치 : `GameSession`(`Enemy.Died`, `PlayerHealth.Died`), `GameHUD`, `PlayerHealthBar`, `ResultPanel`, `LevelUpPanel` 모두 `OnEnable` 구독, `OnDisable` 해제.

### `GameSession`

| 멤버 | 내용 |
|---|---|
| `State` / `IsPlaying` | 시작 값 `Playing`. `IsPlaying`은 생존 시간 누적·`EnemySpawner` 생성 허용 조건 |
| `ElapsedTime` | `Playing` 동안만 `Update`에서 `Time.deltaTime`을 누적한다. 종료 즉시 멈춘다 |
| `KillCount` | `Enemy.Died`마다 `Playing`일 때만 1 증가. 종료 후 사망 연출 대기 중에 죽은 적은 세지 않는다 |
| 최종 보스 처치 클리어 (`HandleEnemyDied`) | 처치 수 처리 뒤 `enemy.ClearsSessionOnDeath`이면 `End(Cleared)`. `End`가 `Playing`일 때만 처리하므로 종료 후에 죽어도 상태는 바뀌지 않는다. 처치한 보스도 `Playing` 중이면 처치 수에 포함된다 (Issue #19) |
| `IsCleared(elapsed, clearTime)` | `elapsed ≥ clearTime` |
| `FormatTime(seconds)` | `"mm:ss"`. 소수점 버림, 분은 두 자리 이상 (예 : 1380 → `"23:00"`) |
| `Pause()` | `IsPlaying`이면 `Time.timeScale = 0`. 아니면 아무것도 하지 않는다 |
| `Resume()` | `IsPlaying`이면 `Time.timeScale = 1`. 아니면 아무것도 하지 않는다. 종료 후 전투 정지(`timeScale` 0)를 풀지 않기 위해서다 (코드 주석) |

- 종료 (`End(state)`) : `Playing`일 때 첫 종료만 처리한다. 상태를 바꿔 생존 시간·스폰을 즉시 멈추고, 코루틴으로 `WaitForSeconds(_endFreezeDelay)` 뒤 `Time.timeScale = 0`, `Ended(state == Cleared)`.
- 클리어와 게임 오버가 겹치면 먼저 `End`를 호출한 쪽 상태가 유지된다.
- `Time.timeScale` 소유 : `Awake`와 `OnDestroy`에서 1로 복구한다. 정지 상태로 Scene을 다시 불러오거나 `GameSession`이 없는 Scene(타이틀)으로 이동해도 정지가 남지 않게 하기 위해서다 (코드 주석). 그 밖에는 `Pause`·`Resume`·종료 코루틴만 `timeScale`을 바꾼다. 다른 클래스는 `Time.timeScale`을 바꾸지 않는다.
- 일시정지는 상태(`State`)를 바꾸지 않는다. 일시정지 중에도 `IsPlaying`은 true다. `Time.deltaTime`이 0이 되어 생존 시간 누적과 `Time.deltaTime`·`Time.time`을 쓰는 이동·쿨다운·연출이 함께 멈춘다.
- `Pause`·`Resume`이 `IsPlaying` 조건이므로 종료 후 `_endFreezeDelay` 대기 중에는 일시정지되지 않는다. 이 동안은 `timeScale`이 1이라 Enemy 이동·사망 연출·플레이어 사망 애니메이션이 계속 재생된다.

### `PlayerHealth`

- 접촉 판정 (`Update`) : `DamageGate.Tick(Time.deltaTime)` 후, `Time.timeScale`이 0이면 판정을 생략한다. 일시정지 중 사망하면 `GameSession` 종료 대기(`WaitForSeconds`, scaled time)가 진행되지 않아 결과 판넬 없이 멈추기 때문이다 (코드 주석). 이어서 살아 있고 게이트가 열려 있을 때만 `OverlapSphereNonAlloc(플레이어 위치, _contactRadius, 공유 버퍼 4칸, _enemyLayer)`를 실행한다. 찾은 Collider의 부모 방향 `Enemy` 중 `IsAlive`인 첫 Enemy의 `ContactDamage`로 `TakeDamage`하고 끝낸다 (동시 접촉 시 1마리만 적용). `CharacterController` 충돌과 무관한 거리 판정이다.
- `TakeDamage(damage)` : 사망 상태이거나 `DamageGate.TryPass()`가 실패하면 무시. 통과하면 `Health.ApplyDamage` → `Damaged` → 처음 사망이면 `Die`.
- `Die` : `_disableOnDeath`의 모든 `Behaviour.enabled = false`, 콘솔 로그 "플레이어 사망", `Died`.
- 접촉 외 경로(`IDamageable.TakeDamage` 직접 호출)도 같은 무적 게이트를 거친다. `timeScale` 0 생략은 접촉 판정(`Update`)에만 있고 `TakeDamage`에는 없다.

### `PlayerExperience`

- `GetRequiredExperience(level)` = `5 + 5 × (level − 1)` (코드의 `shortcut` 주석 : 임시 곡선, 레벨 곡선 기획 확정 시 교체). 레벨 1 → 5, 2 → 10, 3 → 15.
- `AddExperience(amount)` : 0 이하면 무시(값·이벤트 모두 없음). 양수면 `Experience`와 `CurrentLevelExperience`에 더한 뒤, `CurrentLevelExperience ≥ RequiredExperience`인 동안 요구치를 빼고 레벨을 올리며 `LevelChanged(새 레벨)`를 발생시킨다. 남는 경험치는 다음 레벨로 이월된다. 마지막에 `ExperienceGained(amount)`.
- `CollectRadius` : `_collectRadius`(XZ). 사용처는 `ExperienceGem`(`Enemy.md`).

### `GameHUD`

- `Start` : 재화 텍스트 "0", 경험치 바·레벨 1회 갱신.
- `Update` : `FloorToInt(ElapsedTime)`이 바뀌면 `FormatTime`으로 타이머 갱신. `KillCount`가 바뀌면 처치 수 갱신 (`GameSession`에 처치 수 변경 이벤트가 없어 폴링, 코드 주석).
- 경험치 바 : `fillAmount = CurrentLevelExperience / RequiredExperience`, 레벨 텍스트 `"LV {레벨}"`. `ExperienceGained`만 구독한다. 레벨업 시에도 `LevelChanged` 뒤에 `ExperienceGained`가 발생하므로 이것만으로 레벨까지 갱신된다.

### `PlayerHealthBar`

- `Start`에서 1회, `Damaged`마다 `fillAmount = CurrentHealth / MaxHealth`. `PlayerHealth.Awake`에서 체력이 만들어진 뒤 읽도록 첫 갱신을 `Start`에서 한다.
- `LateUpdate`에서 `transform.rotation = _camera.transform.rotation`.

### `ResultPanel`

- `Awake`에서 `_panelRoot.SetActive(false)`.
- `HandleEnded(cleared)` : 제목 `CLEAR`/`GAME OVER`, 통계를 아래 형식으로 채운 뒤 `_panelRoot`를 켠다. 항목명과 값은 TMP `<pos=50%>` 태그로 정렬한다.

| 항목명 | 값 |
|---|---|
| `Survival Time` | `GameSession.FormatTime(ElapsedTime)` |
| `Level` | `PlayerExperience.Level` |
| `Weapons` | `BuildWeaponList(WeaponLoadout.Weapons)` (둘째 줄부터도 값 열에 정렬) |
| `Kills` | `GameSession.KillCount` |
| `Evolved` | `CountEvolved(WeaponLoadout.Weapons)` |

- `BuildWeaponList` : 무기마다 `"{WeaponType} Lv.{Level}"`, 진화면 `" (Evolved)"`를 붙여 줄바꿈으로 연결한다. 빈 목록이면 `"-"`.
- `CountEvolved` : `IsEvolved`인 무기 수.
- 타이틀 버튼 : `SceneManager.LoadScene("TitleScene")`. `timeScale`은 `GameSession.OnDestroy`가 1로 복구한다.
- `_panelRoot`는 이 컴포넌트가 붙은 오브젝트와 달라야 한다. 같으면 `Awake`에서 자기 자신을 꺼 `OnEnable` 구독이 일어나지 않는다 (코드 주석 "이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨").

### `PauseMenu`

- 구독 (`OnEnable`) : 설정 판넬 닫기 버튼 → `CloseSettingsAndResume`, 타이틀 복귀 버튼 → `OpenReturnDialog`, 확인창 예 → `ReturnToTitle`, 아니오 → `CloseReturnDialog`를 `onClick.AddListener`로 구독한다. `_cancelAction.action.performed`를 구독하고 액션을 `Enable`한다. `OnDisable`에서 모두 해제하고 액션을 `Disable`한다.
- 입력 : `_cancelAction`은 `Assets/InputSystem_Actions.inputactions`의 `UI/Cancel` 액션 참조다 (필드 주석). Scene마다 이 액션을 쓰는 컴포넌트가 메뉴 스크립트 하나뿐이라 메뉴 스크립트가 `Enable`/`Disable`을 직접 소유한다 (코드 주석). `EventSystem`의 UI 입력 모듈과는 별개다.
- ESC (`HandleCancelPerformed`) 분기 순서

| 순서 | 조건 | 동작 |
|---|---|---|
| 1 | `!_gameSession.IsPlaying` (종료 연출·결과 패널 중) 또는 `_levelUpPanel.IsOpen` (레벨업 화면 중) | 무시 |
| 2 | 확인창(`_returnDialog`)이 켜져 있음 | 확인창만 닫음 (일시정지 유지) |
| 3 | 설정 판넬(`_settingsPanel`)이 켜져 있음 | 판넬을 끄고 `GameSession.Resume()` |
| 4 | 열린 창 없음 | `GameSession.Pause()` 후 판넬을 켬 |

- 설정 판넬의 닫기 버튼도 3과 같은 `CloseSettingsAndResume`이다.
- 타이틀 복귀 : 판넬의 타이틀 복귀 버튼 → 확인창 켬. 예 → `SceneManager.LoadScene("TitleScene")`. `timeScale` 복구는 Scene 전환 시 `GameSession.OnDestroy`가 담당한다 (코드 주석). 아니오 → 확인창만 닫는다.
- 확인창은 별도 스크립트 없이 `PauseMenu`가 버튼을 처리한다.

### `LevelUpPanel`

| 멤버 | 내용 |
|---|---|
| `IsOpen` | `_panelRoot.activeSelf`. `PauseMenu`가 ESC 무시 조건으로 읽는다 |
| `RequestChestReward()` | 보물 상자 보상 1회를 대기열에 추가한다 (`_pendingChestCount++`). 화면이 닫혀 있을 때만 `Open`. 호출부는 `TreasureChest.Update` 하나다 (Issue #19) |
| `_pendingCount` (private) | 아직 처리하지 않은 레벨업 횟수 |
| `_pendingChestCount` (private) | 아직 처리하지 않은 보물 상자 보상 횟수 |
| `_isShowingChest` (private) | 현재 표시 중인 회차가 보물 상자인지. 완료 시 어느 대기 횟수를 줄일지 정한다 |
| `_levelUpCardCount` (Inspector, `Min(1)`) | 레벨업 회차에 켤 보상 카드 수. 기본값·`GameScene` 값 3 |
| `_chestCardCount` (Inspector, `Min(1)`) | 보물 상자 회차에 켤 보상 카드 수. 기본값·`GameScene` 값 5 |

- Issue #17의 `_previewCardCount` 필드와 그 `shortcut` 주석(3/5 레이아웃 확인용 임시 값)은 Issue #19에서 제거되고 위 두 필드로 바뀌었다.
- `Awake`에서 `_panelRoot.SetActive(false)`.
- 구독 (`OnEnable`) : `_playerExperience.LevelChanged += HandleLevelChanged`, `_completeButton.onClick.AddListener(HandleCompleteClicked)`. `OnDisable`에서 해제.
- `HandleLevelChanged(level)` : `_pendingCount++`. 화면이 닫혀 있을 때만 `Open`. 한 번에 여러 레벨이 올라 이벤트가 연속 발생해도 횟수만 쌓고 화면은 한 번만 연다 (코드 주석). 인자 `level`은 쓰지 않는다.
- `Open` : `_gameSession.Pause()` → `RefreshRewardCards()` → `_panelRoot.SetActive(true)`.
- `RefreshRewardCards` : 다음 회차 종류를 정한다. `_isShowingChest = (_pendingCount == 0)`이므로 레벨업 대기가 남아 있으면 항상 레벨업 회차가 먼저다. `_rewardCards[i]`를 `i < 카드 수`(보물 상자 `_chestCardCount`, 레벨업 `_levelUpCardCount`)이면 켜고 나머지는 끈다.
- `HandleCompleteClicked` : `_isShowingChest`이면 `_pendingChestCount--`, 아니면 `_pendingCount--`. 표시 중인 종류만 줄이므로 표시 중에 도착한 다른 종류의 요청이 잘못 차감되지 않는다 (코드 주석). 두 횟수가 모두 0이면 `_panelRoot.SetActive(false)` 후 `_gameSession.Resume()`. 아니면 `RefreshRewardCards()`로 다음 회차를 표시한다. 따라서 마지막 회차를 완료하기 전에는 재개하지 않는다.
- 화면이 열린 동안 들어온 요청은 횟수만 늘고 표시를 바꾸지 않는다. 예 : 보물 상자 회차(5장) 표시 중 레벨업하면 그 회차는 보물 상자로 유지되고, 완료하면 `_pendingChestCount`가 줄어든 뒤 다음 회차는 레벨업(3장)이다.
- 보상 카드 내용은 회차가 바뀌어도 갱신되지 않는다. 켜지는 카드 수만 바뀐다.
- `Pause`·`Resume`은 `GameSession`의 `IsPlaying` 조건을 따른다. 종료 대기 중 레벨업의 처리는 주의사항 n 참고.
- `_panelRoot`는 이 컴포넌트가 붙은 오브젝트와 달라야 한다 (필드 주석 "이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨"). 이유는 `ResultPanel`과 같다.

### `SettingsPanel`

- `OnEnable` : 슬라이더·토글에 `GameSettings`의 현재 값을 `SetValueWithoutNotify`·`SetIsOnWithoutNotify`로 표시한 뒤(열 때마다 설정을 다시 기록하지 않기 위해서, 코드 주석) `onValueChanged`를 구독한다.
- 값 변경 : 볼륨 → `GameSettings.Volume`, 피격 효과 토글 → `GameSettings.HitFlashEnabled`, 데미지 숫자 토글 → `GameSettings.DamageNumberEnabled`에 즉시 대입한다.
- `OnDisable` : 구독 해제 후 `GameSettings.Save()`. 판넬을 닫을 때뿐 아니라 판넬이 켜진 채 Scene이 전환될 때도 실행된다.
- 판넬을 열고 닫는 코드는 없다. `PauseMenu`(GameScene), `TitleMenu`(TitleScene)가 판넬 루트를 `SetActive`한다.

### `GameSettings`

| 멤버 | PlayerPrefs 키 | 기본값 | 내용 |
|---|---|---|---|
| `Volume` | `"Settings.Volume"` (float) | 1 | setter : `Mathf.Clamp01`로 제한 → 캐시 → `AudioListener.volume` 적용 → `PlayerPrefs.SetFloat` |
| `HitFlashEnabled` | `"Settings.HitFlash"` (int 0/1) | true | setter : 캐시 → `PlayerPrefs.SetInt` |
| `DamageNumberEnabled` | `"Settings.DamageNumber"` (int 0/1) | true | setter : 캐시 → `PlayerPrefs.SetInt` |

- getter는 static 필드 캐시만 읽는다. 피격마다 `PlayerPrefs`를 읽지 않기 위해서다 (코드 주석).
- 로드 (`Load`, private) : `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`로 첫 Scene 로드 전에 세 값을 읽어 캐시하고 `AudioListener.volume`에 볼륨을 적용한다. 읽은 볼륨은 `Clamp01`을 거치지 않는다.
- bool은 1이면 true, 그 외 값은 false로 읽는다.
- `Save()` : `PlayerPrefs.Save()`로 변경된 설정을 디스크에 기록한다. 호출부는 `SettingsPanel.OnDisable` 하나다. 코드 주석은 "설정 판넬을 닫을 때 호출"이지만 실제로는 Scene 전환으로 판넬이 꺼질 때도 호출된다 (위 `SettingsPanel` 참고).

## 호출 순서 또는 데이터 흐름

```
[첫 Scene 로드 전] GameSettings.Load → PlayerPrefs 세 값 캐시, AudioListener.volume = 볼륨

[Awake] GameSession → Time.timeScale = 1
[Awake] PlayerHealth → Health(_maxHealth), DamageGate(_invulnerabilityDuration)
[Awake] ResultPanel → _panelRoot 끔   LevelUpPanel → _panelRoot 끔
[OnEnable] GameSession : Enemy.Died += HandleEnemyDied, PlayerHealth.Died += HandlePlayerDied
           GameHUD : ExperienceGained += …   PlayerHealthBar : Damaged += Refresh   ResultPanel : Ended += HandleEnded
           PauseMenu : 버튼 onClick 4개 구독, UI/Cancel performed += HandleCancelPerformed, 액션 Enable
           LevelUpPanel : LevelChanged += HandleLevelChanged, 완료 버튼 onClick 구독
[Start] GameHUD → 재화 "0", 경험치 바·레벨   PlayerHealthBar → 체력바

[매 프레임 Update]
GameSession (Playing만)  ElapsedTime += deltaTime → IsCleared면 End(Cleared)
PlayerHealth             DamageGate.Tick → (timeScale 0이면 끝) → (살아 있고 게이트 열림) OverlapSphere → 첫 살아 있는 Enemy → TakeDamage(ContactDamage)
                           → Health.ApplyDamage → Damaged (체력바·Hit 애니메이션) → 처음 사망이면 Die
                              → _disableOnDeath 끔 → Died → GameSession.End(GameOver)
GameHUD                  타이머·처치 수 폴링 (바뀔 때만 텍스트 갱신)

[Enemy 사망] Enemy.Died → GameSession.HandleEnemyDied   (젬·상자 드롭은 Enemy.md)
  ├─ Playing이면 KillCount++
  └─ ClearsSessionOnDeath(최종 보스)면 End(Cleared)
[젬 도착] PlayerExperience.AddExperience
  → (요구치 이상인 동안) 레벨업·LevelChanged × n → ExperienceGained → GameHUD 경험치 바·레벨
       └─ LevelChanged마다 LevelUpPanel.HandleLevelChanged : _pendingCount++
            └─ (화면 닫힘) Open
[상자 획득] TreasureChest.Update (Enemy.md) → LevelUpPanel.RequestChestReward : _pendingChestCount++
            └─ (화면 닫힘) Open
Open → GameSession.Pause → timeScale = 0 → RefreshRewardCards → _panelRoot 켬
RefreshRewardCards : _isShowingChest = (_pendingCount == 0) → 카드 (상자 _chestCardCount / 레벨업 _levelUpCardCount)개만 켬
[완료 버튼] LevelUpPanel.HandleCompleteClicked : _isShowingChest면 _pendingChestCount--, 아니면 _pendingCount--
  ├─ 둘 중 하나라도 남음 → RefreshRewardCards (다음 회차, 레벨업 우선, 카드 내용 갱신 없음)
  └─ 둘 다 0 → _panelRoot 끔 → GameSession.Resume → timeScale = 1

[종료] GameSession.End(state)   (Playing일 때 첫 호출만)
  ├─ State = Cleared/GameOver → ElapsedTime 누적·EnemySpawner 생성 즉시 정지
  └─ 코루틴 WaitForSeconds(_endFreezeDelay)
       ├─ Time.timeScale = 0
       └─ Ended(cleared) → ResultPanel : 문구·통계 채움 → _panelRoot 켬
[타이틀 버튼] ResultPanel → LoadScene("TitleScene") → GameSession.OnDestroy → Time.timeScale = 1

[ESC] UI/Cancel performed → PauseMenu.HandleCancelPerformed
  ├─ !IsPlaying 또는 레벨업 화면 열림 → 무시 (종료 대기·결과 패널·레벨업 화면 중)
  ├─ 확인창 켜짐 → 확인창 끔
  ├─ 설정 판넬 켜짐 → 판넬 끔 → SettingsPanel.OnDisable(GameSettings.Save) → GameSession.Resume → timeScale = 1
  └─ 열린 창 없음 → GameSession.Pause → timeScale = 0 → 판넬 켬 → SettingsPanel.OnEnable(현재 값 표시)
[설정 변경] 슬라이더·토글 → GameSettings setter → 캐시·PlayerPrefs (볼륨은 AudioListener.volume도)
[닫기 버튼] PauseMenu.CloseSettingsAndResume (ESC의 판넬 닫기와 같음)

[타이틀 복귀] 판넬의 ReturnTitleButton → 확인창 켬
  ├─ 예 → LoadScene("TitleScene") → GameSession.OnDestroy → Time.timeScale = 1
  └─ 아니오 → 확인창 끔 (일시정지 유지)
```

- 위 `Awake`·`OnEnable`·`Start`의 컴포넌트 사이 순서는 지정되지 않았다. `PlayerHealthBar`가 `Start`에서 첫 갱신을 하는 것은 이 때문이다.

## Scene·Prefab·Inspector 사용 방법

### `Assets/Prefabs/Player/PFB_Player.prefab` (이번 이슈 추가분)

```
PFB_Player                    (… PlayerAnimator, PlayerHealth, PlayerExperience 추가)
├─ CowboyDefault
├─ Weapon
└─ HealthBarCanvas            (Layer UI, Canvas World Space, 스케일 0.01, 크기 120×16, Image(배경), PlayerHealthBar)
   └─ HealthFill              (Image Filled·Horizontal, SPR_BarFill, 빨강)
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `PlayerHealth` (루트) | `_maxHealth` | 100 |
| | `_invulnerabilityDuration` | 0.5 |
| | `_contactRadius` | 0.6 (코드 주석 : Enemy 정지 거리 1.0 - 캡슐 반지름 0.5 = 표면 0.5 + 여유) |
| | `_enemyLayer` | Enemy |
| | `_disableOnDeath` | `PlayerMovement`, `PlayerAimController`, `Weapon`의 `WeaponAttackController` |
| `PlayerExperience` (루트) | `_collectRadius` | 0.4 (코드 주석 : `CharacterController` 반지름) |
| `PlayerHealthBar` (HealthBarCanvas) | `_playerHealth` | 루트 `PlayerHealth` |
| | `_fill` | `HealthFill`의 Image |
| | `_camera` | Prefab에서는 비어 있음. Scene 인스턴스에서 `MainCamera` 연결 필요 |

- `_camera`는 Prefab에서 Scene 오브젝트를 참조할 수 없어 Scene 인스턴스 오버라이드로 연결한다. `GameScene`·`TestMap`의 `PFB_Player` 인스턴스 모두 `MainCamera`의 Camera로 연결되어 있다. 새 Scene에 `PFB_Player`를 놓으면 직접 연결해야 한다 (비면 `LateUpdate`에서 `NullReferenceException`).
- 배경 Image는 Unity 기본 `UISprite`(Sliced)를 쓴다. 채움 Image만 `SPR_BarFill`이다 (아래 `스프라이트` 참고).

### `Assets/Scenes/GameScene.unity`

```
Managers
└─ GameSession                (GameSession)
UI
├─ HUDCanvas                  (Canvas Screen Space Overlay, CanvasScaler 1920×1080 · Match 0.5, GraphicRaycaster, GameHUD, ResultPanel, PauseMenu, LevelUpPanel)
│  ├─ ExperienceBar           (상단 가로 바)
│  │  ├─ ExperienceFill       (Image Filled, F_UI_BlueBar, 색 흰색, GameHUD._experienceFill)
│  │  └─ LevelText            (GameHUD._levelText)
│  ├─ TimerText               (GameHUD._timerText)
│  ├─ KillCounter
│  │  ├─ KillCountText        (GameHUD._killCountText)
│  │  └─ KillIcon             (SPR_Skull)
│  ├─ CurrencyCounter
│  │  ├─ CurrencyText         (GameHUD._currencyText)
│  │  └─ CurrencyIcon         (SPR_SphereGem)
│  ├─ PFB_LevelUpPanel        (PFB_LevelUpPanel 인스턴스, Scene 저장 시 비활성, LevelUpPanel._panelRoot, 구성은 아래 `PFB_LevelUpPanel`)
│  ├─ ResultPanel             (ResultPanel._panelRoot, Scene 저장 시 비활성)
│  │  └─ PanelBox
│  │     ├─ ResultTitleText   (ResultPanel._resultTitleText)
│  │     ├─ StatsText         (ResultPanel._statsText)
│  │     └─ TitleButton       (ResultPanel._titleButton)
│  ├─ PFB_SettingsPanel       (PFB_SettingsPanel 인스턴스, Scene 저장 시 비활성, PauseMenu._settingsPanel)
│  │  └─ PanelBox             (아래 `PFB_SettingsPanel` 구성 + 이 인스턴스에만 추가된 자식)
│  │     ├─ (Prefab 자식 6개)
│  │     └─ ReturnTitleButton (인스턴스에만 추가, Button, PauseMenu._returnTitleButton, PanelBox 하단 중앙 기준 y 140 · 360×80)
│  │        └─ Label          (Text "타이틀로 복귀")
│  └─ ReturnDialog            (Scene 저장 시 비활성, Image 전체 화면 검정 α 0.6, PauseMenu._returnDialog)
│     └─ PanelBox             (900×360)
│        ├─ MessageText       (Text "정말 타이틀로 돌아가시겠습니까?(줄바꿈)현재 진행상황을 잃게됩니다.")
│        ├─ YesButton         (PauseMenu._returnYesButton)
│        │  └─ Label          (Text "예")
│        └─ NoButton          (PauseMenu._returnNoButton)
│           └─ Label          (Text "아니오")
└─ EventSystem                (EventSystem, InputSystemUIInputModule)
```

- `KillCounter`·`CurrencyCounter` 아래 텍스트·아이콘의 부모 관계는 `GameHUD` 연결과 오브젝트 이름 기준이며, 형제 순서는 확인하지 않았다.
- `HUDCanvas`의 마지막 네 자식 `PFB_LevelUpPanel` → `ResultPanel` → `PFB_SettingsPanel` → `ReturnDialog`는 Transform 순서로 확인했다. 뒤의 것이 위에 그려지므로 확인창이 설정 판넬 위에, 결과 패널이 레벨업 화면 위에 표시된다. 레벨업 화면은 HUD 요소(경험치 바·타이머 등)보다 뒤라 HUD 위에 그려지지만, 배경이 반투명이라 HUD가 비쳐 보인다 (주의사항 q).
- `ReturnTitleButton`은 Prefab 인스턴스의 추가 오브젝트(`m_AddedGameObjects`)라 `TitleScene`의 설정 판넬에는 없다. 판넬 안에서의 형제 순서는 확인하지 않았다.
- 새 텍스트는 레거시 `UnityEngine.UI.Text`이며 폰트는 Unity 내장 폰트(`fileID: 10102`)다. HUD·결과 패널의 TMP와 다르다 (아래 `텍스트·폰트`).

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `GameSession` | `_playerHealth` | 플레이어 Prefab 인스턴스의 `PlayerHealth` |
| | `_clearTime` | 1380 (23:00, Inspector에서 조절) |
| | `_endFreezeDelay` | 1.5 |
| `GameHUD` (HUDCanvas) | `_gameSession` | `Managers/GameSession` |
| | `_playerExperience` | 플레이어 `PlayerExperience` |
| | `_timerText` / `_levelText` / `_killCountText` / `_currencyText` / `_experienceFill` | 위 Hierarchy 참고 |
| `ResultPanel` (HUDCanvas) | `_gameSession` | `Managers/GameSession` |
| | `_playerExperience` | 플레이어 `PlayerExperience` |
| | `_weaponLoadout` | 플레이어 `Weapon`의 `WeaponLoadout` |
| | `_panelRoot` | `HUDCanvas/ResultPanel` (컴포넌트가 붙은 `HUDCanvas`와 다른 오브젝트) |
| | `_resultTitleText` / `_statsText` / `_titleButton` | 위 Hierarchy 참고 |
| `PauseMenu` (HUDCanvas) | `_gameSession` | `Managers/GameSession` |
| | `_settingsPanel` | `HUDCanvas/PFB_SettingsPanel` (인스턴스 루트 GameObject) |
| | `_settingsCloseButton` | 인스턴스의 `PanelBox/CloseButton` Button |
| | `_returnTitleButton` | 인스턴스의 `PanelBox/ReturnTitleButton` Button |
| | `_returnDialog` | `HUDCanvas/ReturnDialog` |
| | `_returnYesButton` / `_returnNoButton` | `ReturnDialog/PanelBox/YesButton` / `NoButton` |
| | `_cancelAction` | `InputSystem_Actions` 에셋(GUID `052faaac586de48259a63d0c4782560b`)의 액션 참조 (`fileID 7727032971491509709`) |
| | `_levelUpPanel` | `HUDCanvas`의 `LevelUpPanel` 컴포넌트 |
| `LevelUpPanel` (HUDCanvas) | `_gameSession` | `Managers/GameSession` |
| | `_playerExperience` | 플레이어 `PlayerExperience` |
| | `_panelRoot` | `HUDCanvas/PFB_LevelUpPanel` (Scene 인스턴스 루트 GameObject, 컴포넌트가 붙은 `HUDCanvas`와 다른 오브젝트) |
| | `_completeButton` | 인스턴스의 `CompleteButton` Button |
| | `_rewardCards` | 인스턴스의 보상 카드 GameObject 5개 (Scene 인스턴스 오브젝트) |
| | `_levelUpCardCount` / `_chestCardCount` | 3 / 5 |

- `LevelUpPanel`의 참조는 모두 Scene 인스턴스 오브젝트를 가리킨다 (Prefab 에셋을 가리키지 않음, Scene 파일의 stripped 오브젝트로 확인). `_rewardCards` 배열 순서가 `PFB_RewardCard_01`~`05` 순서와 일치함을 Unity MCP 조회로 확인했다.

- `_cancelAction`의 fileID가 `UI/Cancel` 액션인지는 Scene 파일만으로 대조할 수 없었다. 필드 주석과 브리핑은 `UI/Cancel`이다 (미검증, `TitleScene`의 `TitleMenu`도 같은 fileID). `InputSystem_Actions`의 `UI` 맵에 `Cancel` 액션이 있는 것은 확인했다.

- `TestMap`에는 `GameSession`·HUD·`ResultPanel`이 없다. `PFB_Player`의 `PlayerHealth`·`PlayerExperience`·체력바는 동작하지만 처치 수·종료 판정·결과 패널은 없다.

### `Assets/Prefabs/UI/PFB_SettingsPanel.prefab`

```
PFB_SettingsPanel             (Prefab에서는 활성, 전체 화면 stretch, Image 검정 α 0.6, SettingsPanel)
└─ PanelBox                   (760×640, Image)
   ├─ HeaderText              (Text "설정")
   ├─ VolumeLabel             (Text "사운드 볼륨")
   ├─ VolumeSlider            (Slider 0~1, 저장 값 1, SettingsPanel._volumeSlider)
   │  ├─ Background
   │  ├─ FillArea
   │  │  └─ Fill
   │  └─ HandleSlideArea
   │     └─ Handle
   ├─ HitFlashToggle          (Toggle, 저장 값 켜짐, SettingsPanel._hitFlashToggle)
   │  ├─ Background
   │  │  └─ Checkmark
   │  └─ Label                (Text "피격 효과")
   ├─ DamageNumberToggle      (Toggle, 저장 값 켜짐, SettingsPanel._damageNumberToggle)
   │  ├─ Background
   │  │  └─ Checkmark
   │  └─ Label                (Text "피격 데미지 숫자 출력")
   └─ CloseButton             (Button, OnClick 영구 리스너 없음)
      └─ Label                (Text "닫기")
```

- 텍스트는 모두 레거시 `Text`, Unity 내장 폰트(`fileID: 10102`)다.
- 슬라이더·토글에 저장된 값은 표시 초기값일 뿐이다. 판넬이 켜질 때 `SettingsPanel.OnEnable`이 `GameSettings` 값으로 덮어쓴다.
- 닫기 버튼 동작은 Prefab에 없다. 사용하는 Scene의 메뉴 스크립트(`PauseMenu._settingsCloseButton`, `TitleMenu._settingsCloseButton`)가 코드로 연결한다.
- 인스턴스는 Scene에서 비활성으로 저장한다 (`GameScene`, `TitleScene` 모두 `m_IsActive` 0 오버라이드). Prefab 원본은 활성이다.

### `Assets/Prefabs/UI/PFB_LevelUpPanel.prefab`

좌표는 화면 중앙 앵커 기준(1920×1080 기준 해상도)이며 `InventoryLayout-1920x1080.png`에 맞춘 값이다.

```
PFB_LevelUpPanel              (Prefab에서는 활성, 전체 화면 stretch, Image 검정 α 0.6)
├─ StoragePanel               (Image StoragePanel, 중앙 기준 (-695, 25) · 336×830)
│  └─ StorageTitleText        (Text "Storage 0/5")
├─ InventoryFrame             (Image InventoryFrame, 중앙 기준 (129, 180) · 1216×540)
│  └─ Grid                    (756×452, GridLayoutGroup 셀 72×72 · 간격 4 · 열 고정 10)
│     └─ Slot_01 ~ Slot_60    (Image InventorySlot, 10열×6행. 중앙 4×4 흰색, 나머지 틴트 (0.45, 0.5, 0.58)로 잠금 표시)
├─ RewardCards                (중앙 기준 (128, -239) · 1135×158, HorizontalLayoutGroup 간격 54 · 가운데 정렬 · 자식 폭/높이 제어·확장)
│  └─ PFB_RewardCard_01 ~ 05  (PFB_RewardCard 인스턴스. 아이콘 임시 : 01 Pistol, 02 Shotgun, 03 Minigun, 04 GunStock, 05 AmmoBelt)
└─ CompleteButton             (Button, Image RewardCard, 중앙 기준 (820, -460) · 220×64, LevelUpPanel._completeButton)
   └─ Label                   (Text "완료")
```

- 슬롯은 표시 전용이다. 스크립트·이벤트가 없다. 흰색 16개·틴트 44개 개수와 `Slot_47`(흰색)은 확인했고, 나머지 슬롯 위치별 색은 개별 대조하지 않았다.
- `CompleteButton`의 위치는 레이아웃 이미지에 완료 버튼이 없어 정한 임시 위치다 (브리핑 기준).
- 카드 아이콘·이름·설명은 임시 값이다. 보상 데이터와 연결되지 않는다.
- 인스턴스는 `GameScene`에서 비활성으로 저장한다 (`m_IsActive` 0 오버라이드). Prefab 원본은 활성이다. `LevelUpPanel.Awake`도 `_panelRoot`를 끄므로 비활성 상태가 두 곳에서 정해진다 (주의사항 o).
- 텍스트는 모두 레거시 `Text`, Unity 내장 폰트(`fileID: 10102`)다. 한글 표시를 위해서다 (브리핑 기준).

### `Assets/Prefabs/UI/PFB_RewardCard.prefab`

```
PFB_RewardCard                (342×158, Image RewardCard Sliced)
├─ Icon                       (Image, 100×100, 기본 스프라이트 Pistol)
├─ NameText                   (Text 크기 24, 기본 "권총 Lv.1")
├─ DescriptionText            (Text 크기 18, 기본 "설명")
└─ RerollButton               (Button, Image RewardCard Sliced, 170×56, 카드 하단 중앙 기준 아래 10에 상단을 맞춤)
   ├─ RerollIcon              (Image F_UI_MenuIcons_Tile Map 16x16_11, 36×36)
   ├─ CurrencyIcon            (Image SPR_SphereGem, 30×30)
   └─ CostText                (Text 크기 28, "2")
```

- 리롤 버튼 동작은 없다. OnClick 영구 리스너도, 코드 연결도 없다.
- 카드 크기 342×158은 3장 배치 기준이다. `RewardCards`의 `HorizontalLayoutGroup`이 자식 폭을 제어하므로 실제 폭은 켜진 카드 수로 정해진다 (3장 약 342, 5장 약 184, 주의사항 p).

### 스프라이트 (`Assets/Sprites/UI/`)

| 파일 | 사용처 | 비고 |
|---|---|---|
| `SPR_Skull.png` | `KillIcon` | |
| `SPR_SphereGem.png` | `CurrencyIcon`, `PFB_RewardCard/RerollButton/CurrencyIcon` | 3D 프리팹을 렌더 캡처한 이미지 |
| `SPR_BarFill.png` | `PFB_Player/HealthBarCanvas/HealthFill` | 단색. 기본 `UISprite`는 가장자리가 둥근 투명이라 Filled 비율 0 근처에서 채움이 보이지 않아 교체했다. Issue #17부터 체력바만 사용한다 |

- 그 밖의 UI 스프라이트

| 파일 | 사용처 | 비고 |
|---|---|---|
| `Assets/Sprites/Inventory/InventoryUI/*.png`, `InventoryItems/*.png` | `PFB_LevelUpPanel`, `PFB_RewardCard` (`StoragePanel`, `InventoryFrame`, `InventorySlot`, `RewardCard`, 카드 임시 아이콘 5종) | 모두 Texture Type Sprite (`textureType: 8`). 이 중 `RewardCard.png`만 9-Slice 테두리(`spriteBorder` 70 사방)가 있다. `InventoryLayout-1920x1080.png`은 배치 기준 이미지이며 Prefab·Scene에서 참조하지 않는다 |
| `Assets/Imports/UI/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Slide Bar/F_UI_BlueBar.png` | `ExperienceFill` (색 흰색) | 외부 에셋 |
| `.../Sprites PNG/Menu Icons/F_UI_MenuIcons_Tile Map 16x16.png`의 `F_UI_MenuIcons_Tile Map 16x16_11` | `PFB_RewardCard/RerollButton/RerollIcon` | 외부 에셋 시트의 하위 스프라이트 |

### 텍스트·폰트

- HUD·결과 패널 텍스트는 TextMesh Pro 기본 폰트(`LiberationSans SDF`)를 쓴다. 결과 패널 문구는 영문이다.
- `Assets/TextMesh Pro/`는 `.gitignore` 대상이다. 저장소를 새로 받은 뒤에는 Unity 메뉴에서 TMP Essential Resources를 임포트해야 텍스트가 표시된다.

## 예외·주의사항·검증 방법

### 주의사항·알려진 한계

| # | 내용 | 후속 시점 |
|---|---|---|
| a | 게임 흐름 스크립트는 `Assets/Scripts/GameFlow/`에 둔다. `Assets/Scripts/Core/` 폴더의 스크립트가 컴파일 대상에서 빠지는 현상이 있어 `GameFlow` 폴더를 쓴다 (원인 미확인) | 원인 확인 시 |
| b | (해결) `GameSession`, `PlayerHealth`가 `Time.deltaTime`을 직접 쓰던 일시정지 미연동 `shortcut` 주석은 Issue #13에서 삭제됐다. 일시정지·종료 정지 모두 `Time.timeScale`로 구현하므로 `Time.deltaTime` 직접 사용이 의도된 방식이다 | 해결 (#13) |
| c | `PlayerHealth`의 접촉 버퍼는 static 4칸이다. 반경 안 Enemy Collider가 4개를 넘으면 앞 4개만 검사한다. 앞 4개가 모두 사망·비활성이면 살아 있는 Enemy와 접촉해도 피해가 없을 수 있다 | 문제가 보이면 버퍼 확장 |
| d | `_disableOnDeath`에 없는 컴포넌트(`WeaponViewController`, `PlayerAnimator`, `PlayerHealth` 자신 등)는 사망 후에도 동작한다 | 참고 |
| e | (해결) `LevelChanged` 구독자가 없던 문제는 Issue #17에서 `LevelUpPanel`이 구독해 해결됐다. 보상 선택·획득은 여전히 구현되지 않았다 | 해결 (#17), 보상은 후속 |
| f | 재화 표시는 "0" 고정이다 (`INITIAL_CURRENCY_TEXT`) | 재화 획득 구현 시 |
| g | 결과 패널에는 다시하기 버튼이 없다. 타이틀 복귀만 있다 | 다시하기 구현 시 |
| h | `_endFreezeDelay` 대기는 `WaitForSeconds`(scaled time)라 대기 중 `timeScale`이 0이면 `Ended`가 발생하지 않는다. Issue #13에서 두 경로를 막았다 : `Pause`·`Resume`은 `IsPlaying`일 때만 동작해 종료 대기 중에는 일시정지되지 않고, 일시정지 중에는 `PlayerHealth`가 접촉 판정을 생략해 사망(`End`)이 일어나지 않는다. 단, 이 생략은 접촉 판정에만 있어 `TakeDamage` 직접 호출로 일시정지 중 사망하면 같은 문제가 남는다. 접촉 외 호출 경로는 `IDamageable`을 통한 `Projectile` 적중뿐이다. Issue #19부터 보스 탄(`PFB_BossBullet`, `_hitMask` Player)이 이 경로로 플레이어에게 피해를 준다 (`Docs/TechDocs/Enemy.md`). 일시정지 중 보스 탄 적중으로 사망하는 상황이 실제로 생기는지는 확인하지 않았다 (미검증) | 플레이어에게 피해를 주는 다른 경로 추가 시 |
| i | Inspector 연결 누락 시 `null` 검사가 없다. `GameSession._playerHealth`, `GameHUD`·`ResultPanel`의 참조, `PlayerHealthBar._camera`, `LevelUpPanel`의 참조, `PauseMenu._levelUpPanel`이 비면 `Awake`·`OnEnable`·`Update`·`LateUpdate`·ESC 입력에서 예외가 난다. `PauseMenu`는 ESC마다 `_levelUpPanel.IsOpen`을 읽으므로 `LevelUpPanel`이 없는 Scene에 두면 ESC 입력 시 예외가 난다 | Scene 구성 시 연결 필수 |
| j | `PlayerExperience`의 레벨 곡선은 임시다 (코드의 `shortcut` 주석) | 레벨 곡선 기획 확정 시 |
| k | (해결) 레벨업 화면은 Issue #17에서 `PauseMenu`의 ESC 무시 조건(`_levelUpPanel.IsOpen`)에 포함됐다. 이후 일시정지를 쓰는 다른 판넬이 추가되면 같은 방식으로 무시 조건에 포함해야 한다. 보물 상자 보상(#19)은 같은 `LevelUpPanel` 화면을 쓰므로 이미 포함된다 | 해결 (#17), 다른 판넬 추가 시 |
| l | 피격 효과를 꺼도 이미 반짝이는 중인 Enemy는 남은 시간이 지난 뒤 원래 재질로 돌아온다 (`EnemyHitFeedback.Update`는 설정과 무관하게 복원) | 참고 |
| m | 데미지 숫자(DamageNumbersPro)가 일시정지(`timeScale` 0) 중에도 애니메이션되는지 확인하지 않았다 (미검증) | 일시정지 중 표시가 문제되면 |
| n | 종료 대기(`_endFreezeDelay`) 중 레벨업하면 `IsPlaying`이 false라 `LevelUpPanel.Open`의 `Pause()`는 무시되고 화면만 열린다. 이어서 `Ended`가 발생하면 결과 패널이 레벨업 화면 위에 겹쳐 표시될 수 있다. 완료 버튼의 `Resume()`도 무시되므로 종료 정지가 풀리지는 않는다 (reviewer 권고, 미반영). 종료 대기 중 보물 상자 획득(`RequestChestReward`)도 같은 경로로 화면만 열린다 (코드 확인, Play 미검증) | 레벨업 보상 구현 시 |
| o | `PFB_LevelUpPanel` 인스턴스의 비활성 상태를 Scene 저장(`m_IsActive` 0)과 `LevelUpPanel.Awake`의 `_panelRoot.SetActive(false)` 두 곳이 정한다 (이중 책임, reviewer 권고, 미반영). `ResultPanel`도 같은 구조다 | 참고 |
| p | 카드 5장(보물 상자 회차, `_chestCardCount` 5)이면 `HorizontalLayoutGroup`이 카드 폭을 약 184px((1135 − 54 × 4) / 5)로 줄여 이름·설명 텍스트가 세로로 깨진다. 기본 크기 342×158은 3장 기준이다. Issue #19 이후 5장 표시의 실제 화면은 이 문서 작성 시 확인하지 않았다 (미검증) | 카드 레이아웃 보정 시 |
| q | 레벨업 화면 배경이 반투명(α 0.6)이라 아래 HUD가 보이고, 타이머가 `InventoryFrame` 상단에 겹친다 (1920×1080 렌더 캡처 기준) | 레이아웃 보정 시 |
| r | 완료 버튼은 조건 검증 없이 누를 때마다 회차를 넘긴다 (보상 선택 여부 확인 없음). 리롤 버튼은 표시만 있고 동작이 없다. 인벤토리 슬롯·저장고("Storage 0/5")도 표시 전용이다 | 보상·리롤·인벤토리 구현 시 |
| s | 연속 레벨업 시 다음 회차로 넘어가도 레벨업 효과음·새 보상 UI·새 후보가 없다. 같은 화면이 유지되고 대기 횟수만 줄어든다. `Docs/Spec/GameFlow.md`의 `연속 레벨업`(회차마다 효과음·UI·후보 3개 재생성)을 충족하지 않는다. 마지막 회차 전 재개하지 않는 조건은 충족한다 | 레벨업 보상 구현 시 |
| t | `Docs/Spec/GameFlow.md`의 `게임 상태`는 "레벨업·인벤토리 정리"를 별도 상태로 구분하도록 요구하지만, 현재 구현은 `SessionState`를 바꾸지 않고 `Time.timeScale`과 `LevelUpPanel.IsOpen`으로만 표현한다. 별도 상태 도입 여부는 추후 확정 필요 | 레벨업 보상·보물상자 구현 시 확정 |

### 검증 방법

- EditMode 테스트 (`Assets/Tests/Editor/`)
    - `PlayerExperienceTests` : 양수 추가 시 누적 증가·이벤트 1회(추가량 전달), 두 번 호출 시 누적·호출마다 이벤트, 0·음수 무시(값·이벤트·레벨·현재 레벨 경험치 유지), 요구치 곡선(레벨 1·2·3 → 5·10·15), 요구치와 정확히 같으면 레벨업·나머지 0, 큰 값이면 다중 레벨업·이월
    - `GameSessionTests` : 클리어 시간 직전 false·클리어 시간 true, `FormatTime` 0 → `00:00`, 소수점 버림, 클리어 시간 → `23:00`
    - `ResultPanelTests` : 무기 1개 종류·레벨, 진화 접미사·줄바꿈, 빈 목록 `"-"`, 진화 무기만 셈
- `LevelChanged`가 `ExperienceGained`보다 먼저 발생하는 순서는 코드로 확인했고, 이를 검증하는 테스트는 없다.
- 테스트 실행 결과 : EditMode 테스트 161/161 통과 (2026-11-18, RP 단계 기준).
- Play 모드 (AI agent) : 처치 수 증가, 클리어 → 1.5초 후 결과 패널 표시(`timeScale` 0) → 타이틀 이동 후 `timeScale` 1.
- Play 모드 (사용자) : 클리어 시각·사망 게임 오버, HUD(타이머·경험치/레벨·처치 수·HP 바, 0 근처 표시), 결과 패널, 타이틀 이동 후 다른 캐릭터로 재진행까지 전체 게임 루프 확인.
- Issue #13 (일시정지·설정)
    - 새 EditMode 테스트는 없다. 컴파일 에러 없음, 기존 EditMode 161개 통과 (developer 보고 기준).
    - Play 모드 : 사용자가 확인하고 문제 없다고 보고했다. 확인한 세부 항목은 이 문서 작성 입력에 포함되지 않았다.
    - 미검증 : 빌드 실행(설정 저장·해상도 포함), 게임 → 타이틀 복귀 직후 타이틀에서의 ESC 동작(`PauseMenu`·`TitleMenu`가 같은 `UI/Cancel` 액션을 각자 `Enable`/`Disable`하므로 Scene 전환 시 호출 순서에 따라 결과가 달라질 수 있음), 주의사항 m.
- Issue #17 (레벨업 화면)
    - 새 EditMode 테스트는 없다. 컴파일 에러 없음, 기존 EditMode 161/161 통과.
    - 레이아웃 : 메인 세션이 1920×1080 렌더 캡처로 `InventoryLayout-1920x1080.png`과 배치가 일치함을 확인했다.
    - Play 모드 : 사용자 승인으로 전달받았다. 확인한 세부 항목은 이 문서 작성 입력에 포함되지 않았다.
    - 미검증 : 1920×1080 외 해상도에서의 레이아웃, 주의사항 n(종료 대기 중 레벨업)의 실제 표시.
- Issue #19 (보물 상자 보상 요청·최종 보스 처치 클리어)
    - `LevelUpPanel`·`GameSession` 변경분을 검증하는 새 EditMode 테스트는 없다. 테스트 실행 결과와 Play 모드 확인 항목은 이 문서 작성 입력에 포함되지 않았다 (미검증으로 표기).

## 관련 Issue/PR

- Issue #8 (몬스터 프로토타입. 게임 세션·HUD·플레이어 체력·경험치 포함)
- PR : 미정
- Issue #10 (TitleScene·캐릭터 선택. 결과 패널의 타이틀 복귀 대상, `Docs/TechDocs/Character.md`)
- Issue #13 (타이틀 메뉴·설정·일시정지). PR : 미정
- Issue #17 (레벨업 일시정지·레벨업 화면 레이아웃). PR : 미정
- Issue #19 (몬스터 최종 M06~M12. 보물 상자 보상 요청, 회차 종류별 카드 수, 최종 보스 처치 클리어. `Docs/TechDocs/Enemy.md`). PR : 미정
