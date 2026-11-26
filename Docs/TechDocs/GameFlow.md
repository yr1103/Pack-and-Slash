# 게임 흐름·플레이어 상태·HUD (GameFlow)

> 근거 : `Assets/Scripts/GameFlow/GameSession.cs`, `Assets/Scripts/Player/PlayerHealth.cs`, `Assets/Scripts/Player/PlayerExperience.cs`, `Assets/Scripts/UI/GameHUD.cs`, `Assets/Scripts/UI/PlayerHealthBar.cs`, `Assets/Scripts/UI/ResultPanel.cs`, `Assets/Scripts/UI/{GameSettings,SettingsPanel,PauseMenu,LevelUpPanel,RewardCardUI,ItemTooltipUI}.cs`, `Assets/Scripts/Inventory/{RewardCandidateGenerator,InventoryItemUI,InventoryUI,SellConfirmPopup,Inventory,InventoryData,InventoryWeaponSync}.cs`, `Assets/Scripts/Player/{PlayerStatSync,PlayerMovement}.cs`, `Assets/Scripts/UI/WeaponReloadGauge.cs`, `Assets/Scripts/Character/CharacterDefinition.cs`(고유 효과 필드), `Assets/Data/Character/SO_*.asset`, `Assets/Tests/Editor/{InventoryTests,InventoryWeaponSyncTests,UniqueItemEffectTests}.cs`, `Assets/Data/Weapon/Inventory/SO_Inventory_*.asset`, `Assets/Tests/Editor/InventoryTests.cs`(`GetSellPrice_DataAndLevel_ReturnsLevelTimesPricePerLevel`), `Assets/Scripts/Currency/Currency.cs`, `Assets/Scripts/Combat/TreasureChest.cs`(`RequestChestReward` 호출부), `Assets/Scripts/Enemy/Enemy.cs`(`ClearsSessionOnDeath`), `Assets/Scripts/Combat/Health.cs`, `Assets/Scripts/Combat/DamageGate.cs`, `Assets/Tests/Editor/{PlayerExperienceTests,GameSessionTests,ResultPanelTests,RewardCandidateGeneratorTests}.cs`, `Assets/Prefabs/Player/PFB_Player.prefab`, `Assets/Prefabs/UI/{PFB_SettingsPanel,PFB_LevelUpPanel,PFB_RewardCard,PFB_InventoryItem}.prefab`, `Assets/InputSystem_Actions.inputactions`, `Assets/Scenes/GameScene.unity`, `Assets/Scenes/TestMap.unity`, `Assets/Sprites/UI/*.png.meta`, `Assets/Sprites/Inventory/**/*.png.meta`, `Assets/Imports/UI/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/{Slide Bar/F_UI_BlueBar,Menu Icons/F_UI_MenuIcons_Tile Map 16x16}.png.meta`, `.gitignore`
>
> 설계 원본 : `Docs/Spec/GameFlow.md` (이 문서는 현재 구현 상태만 기술한다)
>
> 관련 문서 : Enemy 사망 이벤트·경험치 젬은 `Docs/TechDocs/Enemy.md`, 무기·플레이어 애니메이션은 `Docs/TechDocs/Weapon.md`, 타이틀 메뉴(`TitleMenu`)는 `Docs/TechDocs/Character.md`

## 목적 / 기능 범위

- 전투 한 판(세션)의 진행 상태·생존 시간·처치 수를 관리하고, 클리어(생존 시간 도달 또는 최종 보스 처치)·게임 오버(플레이어 사망)를 판정한다.
- 종료 후 일정 시간 뒤 전투를 정지(`Time.timeScale` 0)하고 결과 패널을 표시한다. 결과 패널에서 타이틀로 돌아간다.
- 전투 중 ESC로 일시정지(`Time.timeScale` 0)하며 설정 판넬을 열고, 닫으면 재개한다. 설정 판넬에서 확인창을 거쳐 타이틀로 돌아갈 수 있다.
- 레벨업마다 일시정지하고 레벨업 화면(인벤토리·보상 카드 표시)을 열며, 쌓인 레벨업 횟수만큼 회차를 진행한 뒤 닫고 재개한다. 보물 상자 보상 요청도 같은 화면을 쓴다(카드 5장, 대기 중인 레벨업 다음 순서).
- 회차마다 보상 후보 선택(`Selecting`, 레벨업 3중 2택·보물 상자 5중 3택, 특수 재화로 리롤) → 선택 아이템을 Temporary 영역에서 인벤토리·저장고로 정리(`Organizing`) 순서로 진행하고, Temporary가 비어야 완료할 수 있다.
- 보물 상자 회차는 `Organizing`에서 인벤토리 잠긴 칸 중 확장 후보 4칸을 클릭해 해제해야 완료할 수 있다.
- 설정(볼륨·피격 효과·데미지 숫자)을 `PlayerPrefs`에 저장하고 게임 시작 시 불러온다.
- 플레이어 체력·무적 시간·Enemy 접촉 피해, 경험치·레벨을 관리한다.
- 생존 시간·처치 수·경험치 바·레벨·특수 재화 잔액(`Currency`)을 HUD로, 플레이어 체력을 머리 위 체력바로, 재장전 진행률을 체력바 옆 세로 게이지로 표시한다.
- 인벤토리 아이템을 합성(같은 아이템·같은 레벨 → 레벨 +1)하고, 무기 레벨·패시브 인접 효과·패시브 Lv.3 효과·캐릭터 고유 아이템 장착/해제 효과를 무기 스탯(`Docs/TechDocs/Weapon.md`)과 플레이어 스탯(이동속도·최대 체력·경험치 배율·젬 수집 반경)에 반영한다. 수치는 모두 임시값이다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #8 : `GameSession`(상태·생존 시간·처치 수·종료 판정·전투 정지), `PlayerHealth`, `PlayerExperience`(임시 레벨 곡선), `GameHUD`, `PlayerHealthBar`, `ResultPanel` |
| 포함 | Issue #13 : `GameSession.Pause`/`Resume`, `PauseMenu`(ESC 일시정지·타이틀 복귀 확인창), `SettingsPanel`·`PFB_SettingsPanel`, `GameSettings`(저장·로드), `PlayerHealth` 일시정지 중 접촉 판정 생략 |
| 포함 | Issue #17 : `LevelUpPanel`(레벨업 횟수 누적·일시정지·완료 버튼으로 재개), `PFB_LevelUpPanel`·`PFB_RewardCard`(표시 전용 레이아웃), `PauseMenu`가 레벨업 화면 중 ESC 무시, 경험치 바 스프라이트 교체 |
| 포함 | Issue #19 : `GameSession`의 최종 보스 처치 클리어(`Enemy.ClearsSessionOnDeath`), `LevelUpPanel`의 보물 상자 보상 요청(`RequestChestReward`)·회차 종류별 카드 수(레벨업 3장, 보물 상자 5장). 상자 생성·획득 판정은 `Docs/TechDocs/Enemy.md` |
| 포함 | 이슈 미등록 (브랜치 `art/fix-inven-ui`) : `LevelUpPanel`의 보상 단계(`RewardPhase.Selecting`/`Organizing`), 레벨업 3중 2택·보물 상자 5중 3택, 리롤(특수 재화 차감), 선택 아이템의 Temporary 배치, 완료 조건(Temporary 0개), `RewardCardUI`, `RewardCandidateGenerator`, `PFB_InventoryItem` |
| 포함 | 이슈 미등록 (브랜치 `art/fix-inven-ui`) : 공통 아이템 툴팁 `ItemTooltipUI`(인벤토리·저장고·Temporary 아이템과 보상 후보 카드 Hover), 아이템 설명 데이터(`InventoryData`의 설명·장착 효과·해제 버프·해제 디버프 텍스트), `Inventory.GetSellPrice(InventoryData, int)` |
| 포함 | 이슈 미등록 (브랜치 `art/fix-inven-ui`) : 아이템 합성(`Inventory.CanMerge`/`TryMerge`), 무기 레벨 성장·패시브 인접 효과(`GetAdjacencyBonus`/`GetEffectiveAdjacencyBonus`), 패시브 Lv.3 효과(`GetLevel3Bonus`), 캐릭터 고유 아이템 장착/해제 효과(`CharacterDefinition` 고유 효과 필드, `Inventory.Character`), 시작 아이템 지급(`InventoryWeaponSync`), 플레이어 스탯 반영(`PlayerStatSync`, `PlayerMovement`/`PlayerHealth`/`PlayerExperience` 배율), 툴팁 스탯 표시(레벨 보너스·인접·고유 효과·최종 공격력 범위·Lv.3 활성), 재장전 게이지(`WeaponReloadGauge`), 플레이어 피격 반경·젬 획득 반경 조정 |
| 포함 | Issue #23 : 보물 상자 회차의 인벤토리 4칸 확장 선택(`LevelUpPanel.BeginExpansion`, `Inventory.IsCellAvailable`/`IsExpansionCandidate`/`CountExpansionCandidates`/`TryUnlockCell`, `InventoryUI` 칸 색 표시·`SetExpansionMode`·`ExpansionCellClicked`, 안내 문구 `ExpansionGuideText`), 보상 선택(`Selecting`) 중 아이템 hover 툴팁 표시(`InventoryItemUI.IsDragEnabled`로 드래그만 차단), 레벨업 인벤토리 UI에 `InventoryUI_casual` 스프라이트 적용 |
| 포함 | Issue #25 : 소비 아이템 2종(구급 상자 `SO_Inventory_HealthPack` 회복 20, 확장권 `SO_Inventory_ExpansionTicket` 1칸 확장) 효과 적용(`InventoryData.ExpendableEffectType`/`ExpendableEffectValue`, `LevelUpPanel.ApplyExpendable`, `PlayerHealth.Heal`/`Healed`, `Health.Heal`), 확장 후보가 없을 때 확장권 후보 제외, 보상 카드 레벨 표기 생략(`MaxLevel` 1) |
| 포함 | Issue #40 : 캐릭터 특성(항상 적용)과 고유 아이템 장착/해제 효과의 두 층 구조(`CharacterDefinition` 필드 재구성, `Inventory.GetTraitDamageBonus`/`GetTraitProjectileBonus`/`GetCharacterAccuracyBonus`, `PlayerStatSync` 이동속도 공식), 고유 아이템 장착 시 보관함 한도 확장(`Inventory.StorageCapacity`, `InventoryUI.RefreshStorageSlots`, 보관함 슬롯 7개), 조끼 전용 칸 (4,5)·(5,5) 예약·열림·잠김·해제 거부, `LevelUpPanel` 보관함 표시 갱신 경로(`Changed`), 툴팁 특성 줄 |
| 제외 (후속) | 인벤토리 전체 기술 문서(별도 작업), 판매 외 재화 획득, 다시하기, 무기별 재장전 게이지 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `GameSession` (MonoBehaviour) | 상태 `SessionState`(`Playing`·`Cleared`·`GameOver`), `ElapsedTime`, `KillCount` 소유. 생존 시간이 `_clearTime` 이상이거나 `ClearsSessionOnDeath`인 Enemy(최종 보스)가 죽으면 클리어, 플레이어 사망 시 게임 오버. 종료 후 `_endFreezeDelay`초 뒤 `Time.timeScale = 0`과 `Ended(bool)` 발생. 일시정지 `Pause`·재개 `Resume`(진행 중일 때만). `Time.timeScale`을 바꾸는 유일한 클래스. 판정·표시 함수 `IsCleared`, `FormatTime`(static) | `PlayerHealth`(`Died`), `Enemy`(`Died`, static) |
| `PlayerHealth` (MonoBehaviour) | `IDamageable` 구현. `Health`·`DamageGate` 소유. 매 프레임 `Physics.OverlapSphereNonAlloc`으로 Enemy 접촉 판정 후 피해 (`Time.timeScale` 0이면 생략). 이벤트 `Damaged`, `Healed`, `Died`. 사망 시 `_disableOnDeath` 컴포넌트를 끔. `MaxHealth`(배율 적용), `CurrentHealth`, `IsDead`, `IsAlive` 노출. 최대 체력 배율 `SetMaxHealthMultiplier`, 회복 `Heal` | `Health`, `DamageGate`, `Enemy`(`ContactDamage`, `IsAlive`), `Physics` |
| `PlayerExperience` (MonoBehaviour) | 누적 경험치 `Experience`, `Level`, 현재 레벨 경험치 `CurrentLevelExperience`, 요구치 `RequiredExperience`, 젬 수집 반경 `CollectRadius`(배율 적용). `AddExperience`로 증가(배율 적용·소수 누적)·다중 레벨업·이월. 배율 설정 `SetExperienceMultiplier`, `SetCollectRadiusMultiplier`. 이벤트 `LevelChanged`, `ExperienceGained`. 요구치 함수 `GetRequiredExperience`(static, 임시 곡선) | 없음 |
| `PlayerStatSync` (MonoBehaviour) | `Inventory.Changed`마다(`Start`에서 1회 포함) 캐릭터 특성·고유 아이템 장착/해제·패시브 Lv.3 효과로 플레이어 이동속도·최대 체력·경험치·젬 수집 반경 배율을 기본값 기준으로 다시 계산해 적용 (`ApplyStats`) | `Inventory`(`Character`, `IsUniqueItemEquipped`, `GetLevel3Bonus`, `Changed`), `PlayerMovement`, `PlayerHealth`, `PlayerExperience`, `CharacterDefinition` |
| `WeaponReloadGauge` (MonoBehaviour) | `HealthBarCanvas` 자식의 World Space 세로 재장전 게이지. 매 `LateUpdate`에 장착 무기 중 첫 재장전 중 무기를 찾아 `_visualRoot`를 켜고 `fillAmount = ReloadProgress`, 없으면 끈다. 빌보드는 부모 `HealthBarCanvas`(`PlayerHealthBar`)가 처리 | `WeaponLoadout`(`Weapons`), `WeaponInstance`(`IsReloading`, `ReloadProgress`), `Image` |
| `GameHUD` (MonoBehaviour) | 생존 시간·처치 수는 매 프레임 폴링해 값이 바뀔 때만 텍스트 갱신. 경험치 바·레벨은 `ExperienceGained` 구독으로 갱신. 재화는 `Start`에서 현재 잔액을 표시하고 `Currency.Changed` 구독으로 갱신 | `GameSession`, `PlayerExperience`, `Currency`(`Balance`, `Changed`), TMP(`TMP_Text`), `Image` |
| `PlayerHealthBar` (MonoBehaviour) | 플레이어 머리 위 World Space 체력바. `PlayerHealth.Damaged`·`Healed`로 채움 비율 갱신, `LateUpdate`에서 카메라와 같은 회전(빌보드) | `PlayerHealth`, `Image`, `Camera` |
| `HelpHoverUI` (MonoBehaviour, Issue #39) | `?` 버튼(`GameScene`의 `HelpButton`, `InventoryFrame` 우상단)에 마우스를 올리면 `_helpPanel`(`TooltipLayer`의 `HelpPanel`)을 켜고, 벗어나면 끈다. `Awake`·`OnDisable`에서도 끈다(상위 패널이 닫히면 Exit가 오지 않으므로, 코드 주석). `GameScene`에는 회전 안내 `RotateHint`(R키·우클릭 아이콘, `Assets/Sprites/UI/InputPrompts`)도 추가됐다. Scene 배치 세부값은 메인 세션 변경 요약 기준이며 대조하지 않았다 (미검증) | `IPointerEnterHandler`, `IPointerExitHandler` |
| `ResultPanel` (MonoBehaviour) | `GameSession.Ended` 구독. 결과 문구(`CLEAR`/`GAME OVER`)와 통계(생존 시간·레벨·무기 목록·처치 수·진화 무기 수)를 한글 항목명으로 표시. 타이틀 버튼으로 `TitleScene` 로드. 문자열 함수 `BuildWeaponList`, `CountEvolved`(static) | `GameSession`, `PlayerExperience`, `WeaponLoadout`, `SceneManager`, TMP, `Button` |
| `PauseMenu` (MonoBehaviour) | ESC(`UI/Cancel`) 입력으로 일시정지하며 설정 판넬을 열고, 닫으며 재개. 설정 판넬의 타이틀 복귀 버튼·확인창(예/아니오) 처리. 입력 액션을 직접 `Enable`/`Disable`. 레벨업 화면이 열려 있으면 ESC 무시 | `GameSession`(`IsPlaying`, `Pause`, `Resume`), `LevelUpPanel`(`IsOpen`), `Button`, `InputActionReference`, `SceneManager` |
| `LevelUpPanel` (MonoBehaviour) | `PlayerExperience.LevelChanged`마다 레벨업 대기 횟수를, `RequestChestReward()`마다 보물 상자 대기 횟수를 1 늘리고, 화면이 닫혀 있으면 `GameSession.Pause()` 후 화면을 연다. 회차 종류는 레벨업 우선이며 종류에 따라 카드 수(3/5)·선택 수(2/3)를 정한다. 회차 단계 `RewardPhase`(`Selecting` → `Organizing`)를 소유한다. `Selecting`에서 후보 생성·카드 선택·리롤·확정, 확정 시 선택 아이템을 Temporary 영역에 생성하고 `Organizing`으로 전환. Temporary가 비면 완료 버튼으로 표시 중인 종류의 대기 횟수를 1 줄이고, 둘 다 0이면 화면을 닫고 `Resume()`. 표시 여부 `IsOpen` 노출. `RewardPhase` enum도 같은 파일에 선언 | `GameSession`(`Pause`, `Resume`), `PlayerExperience`(`LevelChanged`), `PlayerHealth`(`Heal`), `Currency`(`Balance`, `TrySpend`, `Changed`), `Inventory`(`TemporaryCount`, `StorageCount`, `StorageCapacity`, `MoveToTemporary`, `GetRotatedShape`, 이벤트 `TemporaryCountChanged`·`Changed`), `InventoryUI`(`GetItemSize`, `ClampToTemporary`, `SetRotateEnabled`), `InventoryItemUI`(프리팹 생성·`Initialize`), `SellConfirmPopup`·`ItemTooltipUI`(`_tooltip`, 아이템 UI에 전달), `RewardCardUI`, `RewardCandidateGenerator`, `Button`, `Text`, `CanvasGroup`. 호출자 `TreasureChest`(`Docs/TechDocs/Enemy.md`) |
| `RewardCardUI` (MonoBehaviour) | 보상 후보 카드 1장 표시. `Bind(InventoryData)`로 데이터를 `_data`에 보관하고 아이콘·이름(`MaxLevel` > 1이면 `"{이름} Lv.1"`, 아니면 이름만. `ItemTooltipUI` 레벨 표기와 같은 기준, 코드 주석)·설명 표시, `SetSelected(bool)`로 배경 색(`_normalColor`/`_selectedColor`) 변경. 카드 버튼 클릭 시 `Clicked(this)` 발생. 선택 판정은 하지 않는다. 마우스를 올리면(`IPointerEnterHandler`) `_tooltip.Show(_data, this)`, 벗어나거나(`IPointerExitHandler`) 비활성화되면 `_tooltip.Hide(this)`. `_tooltip`이 비어 있으면 툴팁 없음 | `Button`, `Image`, `Text`, `InventoryData`(`Icon`, `ItemName`, `Description`), `ItemTooltipUI` |
| `ItemTooltipUI` (MonoBehaviour) | 보유 아이템·보상 후보 공통 툴팁 하나를 커서 옆에 표시. `Show(Inventory.ItemInstance, source)`(현재 레벨·판매가) / `Show(InventoryData, source)`(Lv.1·미배치 기준) / `Hide(source)`(마지막으로 `Show`한 `source`만 숨김). 무기·패시브는 스탯 줄(레벨 보너스·인접·특성·정확도·해제 효과·최종 공격력 범위·Lv.3 활성 여부)을 표시. 표시 중에는 `LateUpdate`마다 커서 위치로 갱신 | `Inventory`(`GetSellPrice` 두 오버로드, `GetWeaponLevelMultiplier`, `GetEffectiveAdjacencyBonus`, `GetPassiveAdjacencyBonus`, `GetTraitDamageBonus`, `GetTraitProjectileBonus`, `GetCharacterAccuracyBonus`, `GetUniqueAttackSpeedBonus`, `GetUniqueSpreadAngle`, `GetWeaponStats`, `IsLevel3EffectActive`), `InventoryData`(`Icon`, `ItemName`, `ItemType`, `Description`, `MaxLevel`, `WeaponDefinition`, `AdjacencyEffectType`, `IsSellable`, `EquippedEffect`, `UnequippedBuff`, `UnequippedDebuff`), `WeaponDefinition`(`ProjectileDamage`, `DamageVariance`, `GetRolledDamage`), `Mouse`(Input System), `LayoutRebuilder`, `Image`, `Text`. 호출자 `RewardCardUI`, `InventoryItemUI` |
| `RewardCandidateGenerator` (static class) | 후보 풀에서 서로 다른 아이템을 최대 `count`개 추첨 (`Generate`). 종류별 가중치로 종류를 고른 뒤 종류 안에서 균등 추첨, 직전 후보에 없는 아이템 우선 | `InventoryData`(`ItemType`, `IsSellable`, `Id`), `System.Random` |
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
| `PlayerHealth.Healed` | `PlayerHealth` | 없음 | `Heal` 호출마다. 사망 상태라 회복이 무시돼도 발생 (요약 주석 : 체력바 갱신용) | `PlayerHealthBar.Refresh` |
| `PlayerHealth.Died` | `PlayerHealth` | 없음 | 처음 사망한 피해에서 1회. `_disableOnDeath`를 끈 뒤 발생 | `GameSession.HandlePlayerDied` |
| `PlayerExperience.LevelChanged` | `PlayerExperience` | 새 레벨(`int`) | 레벨업 1회마다. 한 번에 여러 레벨이 오르면 레벨 수만큼 발생 | `LevelUpPanel.HandleLevelChanged` |
| `PlayerExperience.ExperienceGained` | `PlayerExperience` | 이번 추가량(`int`) | `AddExperience`(양수)마다 1회. 같은 호출의 `LevelChanged`가 모두 끝난 뒤 발생 | `GameHUD.HandleExperienceGained` |
| `RewardCardUI.Clicked` | `RewardCardUI` | 클릭한 카드(`RewardCardUI`) | 카드 루트 Button 클릭마다 | `LevelUpPanel.HandleCardClicked` |
| `Currency.Changed` | `Currency` | 새 잔액(`int`) | `Add`(양수)·`TrySpend`(성공) 후 잔액이 바뀔 때만 | `GameHUD.RefreshCurrency`, `LevelUpPanel.RefreshCurrency` |
| `Inventory.TemporaryCountChanged` | `Inventory` | 없음 | 아이템이 Temporary 영역에 들어오거나 나갈 때 (Temporary 안 좌표 이동은 제외) | `LevelUpPanel.RefreshCompleteButton` |
| `Inventory.StorageCountChanged` | `Inventory` | 없음 | 아이템이 보관함에 들어오거나 나갈 때 | 없음 (Issue #40에서 `LevelUpPanel`이 `Changed` 구독으로 바뀜, `Assets/Scripts` 안 구독자 없음) |
| `Inventory.Changed` | `Inventory` | 없음 | 공개 조작(배치·이동·회전·제거·보관·Temporary 이동·판매·합성·`Character` 설정·칸 해제(`TryUnlockCell`))이 성공해 상태가 확정된 뒤 1회. 실패·조회 시 없음. 조끼 전용 칸 갱신(`RefreshVestCells`)은 이 통지보다 먼저 끝난다 | `PlayerStatSync.ApplyStats`, `InventoryWeaponSync.SyncWeapons`(`Docs/TechDocs/Weapon.md`), `LevelUpPanel.RefreshStorageCount`, `InventoryUI.RefreshCellColors`·`RefreshStorageSlots` |

- 구독 위치 : `GameSession`(`Enemy.Died`, `PlayerHealth.Died`), `GameHUD`(`ExperienceGained`, `Currency.Changed`), `PlayerHealthBar`, `ResultPanel`, `LevelUpPanel`(`LevelChanged`, `Currency.Changed`, `Inventory.TemporaryCountChanged`·`Inventory.Changed`, `InventoryUI.ExpansionCellClicked`, 카드마다 `RewardCardUI.Clicked`), `InventoryUI`(`Inventory.Changed`), `PlayerStatSync`(`Inventory.Changed`) 모두 `OnEnable` 구독, `OnDisable` 해제.
- `Currency`·`Inventory`는 이 문서의 클래스가 아니다. 위 표에는 이 문서 클래스가 구독하는 경우만 적는다.

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
- 피격 반경 `_contactRadius` : `PFB_Player` 값 0.45 (임시값, 이 브랜치에서 축소. 코드 기본값은 0.6이며 필드 주석 "Enemy 정지 거리 1.0 - 캡슐 반지름 0.5 = 표면 0.5 + 여유"는 0.6 기준으로 남아 있다).
- `SetMaxHealthMultiplier(multiplier)` : `Health.SetMaxHealth(_maxHealth × multiplier)`. 매번 Inspector 기본값에서 다시 계산한다. 최대 체력이 줄면 현재 체력을 새 최대치로 clamp하고, 늘어나도 회복하지 않는다 (`Health.SetMaxHealth` 요약 주석).
- `Heal(amount)` : `Health.Heal`(최대 체력 clamp, 사망 상태면 무시) 후 `Healed`. 회복이 무시돼도 `Healed`는 발생한다. 호출부 `LevelUpPanel.ApplyExpendable`.

### `PlayerExperience`

- `GetRequiredExperience(level)` = `5 + 5 × (level − 1)` (코드의 `shortcut` 주석 : 임시 곡선, 레벨 곡선 기획 확정 시 교체). 레벨 1 → 5, 2 → 10, 3 → 15.
- `AddExperience(amount)` : 0 이하면 무시(값·이벤트 모두 없음). 양수면 `amount × _experienceMultiplier + 이전 소수 잔량`의 정수 부분(오차 허용 0.0001을 더해 내림)만 지급하고 남은 소수는 `_experienceRemainder`에 보관한다. 지급량이 0이면 여기서 끝난다(이벤트 없음). 지급량을 `Experience`와 `CurrentLevelExperience`에 더한 뒤, `CurrentLevelExperience ≥ RequiredExperience`인 동안 요구치를 빼고 레벨을 올리며 `LevelChanged(새 레벨)`를 발생시킨다. 남는 경험치는 다음 레벨로 이월된다. 마지막에 `ExperienceGained(지급량)`.
- `SetExperienceMultiplier(multiplier)` : 기본 1, 다음 `AddExperience`부터 적용.
- `CollectRadius` : `_collectRadius × _collectRadiusMultiplier`(XZ). `SetCollectRadiusMultiplier`로 배율만 바꾸고 기본값은 유지한다. 사용처는 `ExperienceGem`(`Enemy.md`).
- 젬 획득 반경 기본값 `_collectRadius` : `PFB_Player` 값 0.8 (임시값, 이 브랜치에서 확대. 코드 기본값은 0.4이며 필드 주석 "초기값 = CharacterController 반지름"은 0.4 기준으로 남아 있다).

### `PlayerStatSync`

- `OnEnable`/`OnDisable`에서 `Inventory.Changed` 구독/해제. `Start`에서 1회 `ApplyStats`(`PlayerHealth.Awake`의 `Health` 생성 이후, `InventoryWeaponSync.Start`와 순서가 바뀌어도 `Changed`로 다시 계산되어 결과 동일, 코드 주석).
- `ApplyStats` : 매번 기본값 기준으로 다시 계산한다 (누적 없음).

| 대상 | 배율 |
|---|---|
| 이동속도 (`PlayerMovement.SetMoveSpeedMultiplier`) | `Max(0, 1 + Min(보너스 합, 0.5))`. 보너스 합 = 패시브 Lv.3 `MoveSpeed` 합 + 캐릭터 특성 `TraitMoveSpeedBonus`(항상) + (고유 아이템 해제 중이면 `UnequippedMoveSpeedBonus`). 상한 `MAX_MOVE_SPEED_BONUS` 0.5(테스트 상한), 하한 없음 |
| 최대 체력 (`PlayerHealth.SetMaxHealthMultiplier`) | `1 + (고유 아이템 해제 중이면 UnequippedMaxHealthBonus, 아니면 0)`. 장착 중이거나 특성에는 최대 체력 효과가 없다 |
| 경험치 (`PlayerExperience.SetExperienceMultiplier`) | `1 + 패시브 Lv.3 Experience 합` |
| 젬 수집 반경 (`PlayerExperience.SetCollectRadiusMultiplier`) | `1 + 패시브 Lv.3 CollectRadius 합` |

- `Inventory.Character`가 `null`이면 특성·고유 효과 없이 Lv.3 효과만 적용한다.
- 예 (헤비거너, Lv.3 패시브 없음) : 셰이크 장착 중 보너스 합 −0.1(특성) → 배율 0.9, 해제 중 −0.1 + −0.2 = −0.3 → 배율 0.7.
- `PlayerMovement.MoveSpeed` = `_moveSpeed`(Prefab 5) × 배율. 이동은 `SimpleMove(입력 방향 × MoveSpeed)`.

### `WeaponReloadGauge`

- 매 `LateUpdate` : `_loadout.Weapons`를 순서대로 보고 `IsReloading`인 첫 무기를 찾는다. 있으면 `_visualRoot.SetActive(true)`, `_fill.fillAmount = ReloadProgress`. 없으면 `_visualRoot.SetActive(false)`. 진행률이 매 프레임 바뀌어 이벤트가 아닌 폴링이다 (코드 주석).
- `_visualRoot`는 자기 자신이 아닌 자식이다. 자신을 끄면 `LateUpdate`가 멈추기 때문이다 (필드 주석).
- 여러 무기가 동시에 재장전하면 첫 무기만 표시한다 (코드의 `shortcut` 주석 : 무기별 게이지가 필요해지면 무기마다 게이지를 둔다). 현재 탄창이 있는 무기는 미니건(`SO_Minigun`)뿐이다.
- 재장전 시간이 0 이하인 무기는 `ReloadProgress`가 0이라 빈 게이지로 표시된다 (재장전 중인 1프레임).

### `GameHUD`

- 구독 (`OnEnable`) : `ExperienceGained` → `HandleExperienceGained`, `Currency.Changed` → `RefreshCurrency`. `OnDisable`에서 해제.
- `Start` : 재화 텍스트를 `Currency.Balance`로, 경험치 바·레벨을 1회 갱신.
- 재화 : `RefreshCurrency(balance)`가 잔액을 숫자 그대로(`balance.ToString()`) 표시한다. 잔액이 바뀔 때마다 `Currency.Changed`로 갱신된다.
- `Update` : `FloorToInt(ElapsedTime)`이 바뀌면 `FormatTime`으로 타이머 갱신. `KillCount`가 바뀌면 처치 수 갱신 (`GameSession`에 처치 수 변경 이벤트가 없어 폴링, 코드 주석).
- 경험치 바 : `fillAmount = CurrentLevelExperience / RequiredExperience`, 레벨 텍스트 `"LV {레벨}"`. `ExperienceGained`만 구독한다. 레벨업 시에도 `LevelChanged` 뒤에 `ExperienceGained`가 발생하므로 이것만으로 레벨까지 갱신된다.

### `PlayerHealthBar`

- `Start`에서 1회, `Damaged`·`Healed`마다 `fillAmount = CurrentHealth / MaxHealth`. `PlayerHealth.Awake`에서 체력이 만들어진 뒤 읽도록 첫 갱신을 `Start`에서 한다.
- `LateUpdate`에서 `transform.rotation = _camera.transform.rotation`.

### `ResultPanel`

- `Awake`에서 `_panelRoot.SetActive(false)`.
- `HandleEnded(cleared)` : 제목 `CLEAR`/`GAME OVER`, 통계를 아래 형식으로 채운 뒤 `_panelRoot`를 켠다. 항목명과 값은 TMP `<pos=50%>` 태그로 정렬한다.

| 항목명 | 값 |
|---|---|
| `생존 시간` | `GameSession.FormatTime(ElapsedTime)` |
| `레벨` | `PlayerExperience.Level` |
| `무기` | `BuildWeaponList(WeaponLoadout.Weapons)` (둘째 줄부터도 값 열에 정렬) |
| `처치 수` | `GameSession.KillCount` |
| `진화` | `CountEvolved(WeaponLoadout.Weapons)` |

- 항목명은 Issue #39에서 한글로 바뀌었다. 제목 `CLEAR`/`GAME OVER`와 HUD 레벨 표기 `"LV"`는 영문 그대로다.
- `BuildWeaponList` : 무기마다 `"{WeaponType} Lv.{Level}"`, 진화면 `" (진화)"`를 붙여 줄바꿈으로 연결한다. 빈 목록이면 `"-"`.
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
| `_levelUpMaxSelect` / `_chestMaxSelect` (Inspector, `Min(1)`) | 회차 종류별 최대 선택 수. 기본값·`GameScene` 값 2 / 3 |
| `_rewardPool` (Inspector) | 후보 풀 `InventoryData[]`. `GameScene`에는 8개 연결 |
| `_weaponWeight` / `_passiveWeight` / `_expendableWeight` (Inspector, `Min(0)`) | 종류 등장 가중치. 기본값·`GameScene` 값 50 / 35 / 15 |
| `_rerollCost` (Inspector, `Min(1)`) | 리롤 1회 비용. 기본값·`GameScene` 값 2 |
| `_phase` (private, `RewardPhase`) | 현재 회차 단계. `Selecting`(후보 선택) / `Organizing`(Temporary 정리) |
| `_candidates` (private) | 현재 후보 목록. 인덱스 = 카드 인덱스 |
| `_selectedIndices` (private) | 선택한 카드 인덱스. 선택 순서대로 저장되고 확정 시 이 순서로 Temporary에 배치한다 |
| `CardCount` (private) | `Min(회차 종류별 카드 수, _rewardCards.Length)` |
| `RequiredSelectCount` (private) | `Min(회차 종류별 최대 선택 수, _candidates.Count)`. 후보가 최대 선택 수보다 적으면 후보 수만큼만 선택한다 |
| `BeginExpansion(int count)` | 남은 확장 수 `_remainingExpansionCount = Min(count, Inventory.CountExpansionCandidates())`. `Organizing`이면 즉시 `InventoryUI.SetExpansionMode(남은 수 > 0)`. 이어서 `RefreshExpansionGuide`·`RefreshCompleteButton`. 보물 상자 외 확장 출처(1칸 확장권 등)도 같은 경로로 호출하도록 둔 진입점이며 (요약 주석), 호출부는 `StartRound`와 `ApplyExpendable`(확장권 : 현재 남은 수 + 효과 수치) 두 곳이다 |
| `_chestExpansionCount` (Inspector, `Min(0)`) | 보물 상자 회차당 확장 칸 수. 기본값 4 |
| `_remainingExpansionCount` (private) | 현재 회차에서 남은 확장 수. 0이어야 완료할 수 있다 |
| `_expansionGuideText` (Inspector, `Text`) | 확장 안내 문구 `"확장할 {남은 수}칸을 선택하세요!!"`. `null` 허용 (미연결 Scene은 안내 생략) |

- `RefreshExpansionGuide` : `_expansionGuideText`가 `null`이면 무시. `Organizing`이고 남은 확장 수 > 0일 때만 문구를 켜고 남은 수로 갱신한다. 남은 수가 0이 되면 숨긴다.

- Issue #17의 `_previewCardCount` 필드와 그 `shortcut` 주석(3/5 레이아웃 확인용 임시 값)은 Issue #19에서 제거되고 위 카드 수 필드로 바뀌었다.
- `Awake`에서 `_panelRoot.SetActive(false)`.
- 구독 (`OnEnable`) : `LevelChanged` → `HandleLevelChanged`, 완료 버튼 → `HandleCompleteClicked`, 선택 완료 버튼 → `HandleConfirmClicked`, 리롤 버튼 → `HandleRerollClicked`, `Inventory.TemporaryCountChanged` → `RefreshCompleteButton`, `Inventory.Changed` → `RefreshStorageCount`(Issue #40에서 `StorageCountChanged` 대신. 보관 수와 함께 고유 아이템 탈착에 따른 한도 변경도 반영, 코드 주석), `Currency.Changed` → `RefreshCurrency`, `InventoryUI.ExpansionCellClicked` → `HandleExpansionCellClicked`, 각 카드의 `RewardCardUI.Clicked` → `HandleCardClicked`. `OnDisable`에서 모두 해제.
- `HandleLevelChanged(level)` : `_pendingCount++`. 화면이 닫혀 있을 때만 `Open`. 한 번에 여러 레벨이 올라 이벤트가 연속 발생해도 횟수만 쌓고 화면은 한 번만 연다 (코드 주석). 인자 `level`은 쓰지 않는다.
- `Open` : `_gameSession.Pause()` → `StartRound()` → `RefreshStorageCount()`(`"보관함 {Inventory.StorageCount}/{Inventory.StorageCapacity}"`, `STORAGE_COUNT_FORMAT`) → `_panelRoot.SetActive(true)`.

#### 회차 시작 (`StartRound`)

- `_isShowingChest = (_pendingCount == 0)`이므로 레벨업 대기가 남아 있으면 항상 레벨업 회차가 먼저다.
- `_phase = Selecting` → `BeginExpansion(보물 상자 회차면 _chestExpansionCount, 아니면 0)` → 선택 초기화 → `_candidates = GenerateCandidates(null)`(직전 후보 없음) → `RefreshRewardCards()` → `ApplyPhase()`. 이전 회차의 후보·선택은 이어받지 않는다 (코드 주석). `Selecting`이므로 확장 모드는 켜지지 않고, `Organizing` 진입 시 `ApplyPhase`가 켠다 (코드 주석).
- `RefreshRewardCards` : `_rewardCards[i]`를 `i < 후보 수`이면 켜고 `Bind(후보)`·`SetSelected(선택 여부)`, 나머지는 끈다. 이어서 `RefreshSelection`.

#### 카드 선택·취소 (`HandleCardClicked`)

- `Selecting`이 아니면 무시.
- 이미 선택한 카드면 선택 취소. 선택하지 않은 카드는 선택 수가 `RequiredSelectCount` 미만일 때만 선택한다. 필요 수에 도달하면 다른 카드를 취소해야 새로 고를 수 있다.
- `RefreshSelection` : 선택 수 텍스트 `"선택 {선택 수}/{필요 수}"`. 안내 텍스트 `_selectionGuideText`는 선택 수 < 필요 수이면 `"보상을 선택하세요! {후보 수}개 중 {필요 수}개를 클릭해 선택하세요."`, 아니면 `"{필요 수}개 선택 완료! 인벤토리 정리하기를 눌러 진행하세요."`. 선택 완료 버튼은 `Selecting`이고 선택 수 == 필요 수일 때만 `interactable`.

#### 리롤 (`HandleRerollClicked`)

1. `Selecting`이 아니거나 `Currency.Balance < _rerollCost`이면 무시.
2. 현재 후보를 직전 후보로 넘겨 새 후보 생성 (`GenerateCandidates(_candidates)`).
3. 새 후보가 0개면 차감 없이 중단.
4. `Currency.TrySpend(_rerollCost)`가 실패하면 중단. 성공한 경우에만 후보를 교체하고 선택을 초기화한 뒤 `RefreshRewardCards`.

- 선택한 카드가 있어도 리롤할 수 있다. 선택은 초기화된다.
- 리롤 버튼 `interactable`과 재화 텍스트(`"보유 {잔액}"`)는 `RefreshCurrency`가 갱신한다. `Currency.Changed` 구독으로 차감 직후 갱신되며, 버튼은 `Selecting`이고 잔액 ≥ 비용일 때만 켜진다.

#### 확정 (`HandleConfirmClicked`)

1. `Selecting`이 아니거나 선택 수 != `RequiredSelectCount`이면 무시.
2. 연속 클릭으로 중복 지급되지 않도록 `_phase = Organizing`을 먼저 설정한다 (코드 주석).
3. 선택 순서대로 후보를 모은다. `ItemType.Expendables`는 `ApplyExpendable(data)`로 효과를 즉시 적용하고 Temporary에 등록하지 않는다.
   - `ExpendableEffectType.Heal` : `_playerHealth.Heal(ExpendableEffectValue)`.
   - `ExpendableEffectType.ExpandCell` : `BeginExpansion(남은 확장 수 + RoundToInt(ExpendableEffectValue))`. 결과는 `CountExpansionCandidates()`로 제한된다.
   - `None` : 효과 없음.
4. 나머지 아이템마다 `AddToTemporary(data, i, 개수)` : `Inventory.ItemInstance` 생성 → Temporary 영역을 가로로 개수만큼 등분한 i번째 칸 중심을 구함 → `InventoryUI.GetItemSize(회전 0 Shape)`와 `ClampToTemporary(..., rotation 0, camera null)`로 영역 안으로 보정 → `Inventory.MoveToTemporary(item, 위치, 0)` → `_itemPrefab`을 `_itemLayer`에 `Instantiate` → `InventoryItemUI.Initialize(item, _inventory, _inventoryUI, _sellPopup, _tooltip)`. 데이터를 먼저 Temporary로 옮기는 것은 UI가 초기화 시 데이터의 영역·좌표로 표시하기 때문이다 (코드 주석).
5. 선택·후보를 비우고 `RefreshRewardCards()`로 카드를 모두 끈다. 카드는 프리팹 구조상 `_selectingGroup` 밖이라 그룹 비활성화로 숨겨지지 않기 때문이다 (코드 주석, 주의사항 ab).
6. `ApplyPhase()`.

- `ClampToTemporary`에 카메라 `null`을 넘기는 것은 Screen Space Overlay 전제다 (코드의 `shortcut` 주석 : Camera 캔버스 전환 시 카메라 전달 필요).

#### 단계 UI 전환 (`ApplyPhase`)

| 대상 | `Selecting` | `Organizing` |
|---|---|---|
| `_selectingGroup` (리롤·재화·선택 수·선택 완료) | 켬 | 끔 |
| `_temporaryArea` | 끔 | 켬 |
| `_completeButton` (GameObject) | 끔 | 켬 |
| `InventoryItemUI.IsDragEnabled` (아이템 드래그, hover 툴팁은 유지) | false | true |
| `InventoryUI.SetRotateEnabled` (회전 입력) | false | true |
| `InventoryUI.SetExpansionMode` (확장 모드) | false | 남은 확장 수 > 0 |

- 이어서 `RefreshExpansionGuide`, `RefreshCompleteButton`, `RefreshSelection`, `RefreshCurrency(현재 잔액)`을 호출한다.
- 이전의 `_itemLayerGroup`(ItemLayer CanvasGroup `blocksRaycasts` 전환) 필드는 Issue #23에서 제거되었다. 보상 선택 중에도 아이템이 포인터 이벤트를 받아 툴팁을 띄우고, 드래그만 `IsDragEnabled`로 막는다.
- `RefreshCompleteButton` : 완료 버튼 `interactable = Organizing && TemporaryCount == 0 && 남은 확장 수 == 0`, Temporary 경고(`_temporaryWarning`)는 `Organizing && TemporaryCount > 0`일 때만 켠다. `TemporaryCountChanged`마다 다시 계산한다.

#### 완료 (`HandleCompleteClicked`)

- 버튼 비활성화와 별개로 함수 안에서도 `Organizing`이 아니거나 `TemporaryCount > 0`이거나 남은 확장 수 > 0이면 무시한다 (코드 주석 : 외부 호출 방어). 완료 조건이 버튼(`RefreshCompleteButton`)과 처리 함수 두 곳에 있다.
- `_isShowingChest`이면 `_pendingChestCount--`, 아니면 `_pendingCount--`. 표시 중인 종류만 줄이므로 표시 중에 도착한 다른 종류의 요청이 잘못 차감되지 않는다 (코드 주석).
- 두 횟수가 모두 0이면 `InventoryUI.SetExpansionMode(false)`, `InventoryItemUI.IsDragEnabled = true`, `RefreshExpansionGuide()` → `_panelRoot.SetActive(false)` 후 `_gameSession.Resume()`. 아니면 `StartRound()`로 다음 회차를 새 후보로 시작한다. 따라서 마지막 회차를 완료하기 전에는 재개하지 않는다.
- 화면이 열린 동안 들어온 요청은 횟수만 늘고 표시를 바꾸지 않는다. 예 : 보물 상자 회차(5장) 표시 중 레벨업하면 그 회차는 보물 상자로 유지되고, 완료하면 `_pendingChestCount`가 줄어든 뒤 다음 회차는 레벨업(3장)이다.

#### 확장 칸 클릭 (`HandleExpansionCellClicked`)

1. `Organizing`이 아니거나 남은 확장 수가 0이거나 `Inventory.TryUnlockCell(cell)`이 실패하면 무시.
2. 남은 확장 수 1 감소. 해제 후 `CountExpansionCandidates()`가 0이면 남은 수를 0으로 정리한다 (남은 수가 있어도 후보가 없어 완료가 막히지 않도록, 코드 주석).
3. 남은 수가 0이면 `InventoryUI.SetExpansionMode(false)`.
4. `RefreshExpansionGuide`, `RefreshCompleteButton`.

- 칸 색 갱신은 `TryUnlockCell`이 발생시키는 `Inventory.Changed`를 `InventoryUI`가 받아 처리한다.

#### 후보 추첨 (`RewardCandidateGenerator.Generate`)

`Generate(pool, count, weaponWeight, passiveWeight, expendableWeight, previous, random)` → `List<InventoryData>`

- 풀에서 제외 : `null`, `ItemType.Essential`, `IsSellable`이 false(진화 무기), 가중치 0 이하인 종류. 같은 `Id`는 처음 것만 사용한다.
- 목록 안에서 같은 아이템은 중복되지 않는다 (뽑을 때마다 버킷에서 제거).
- 직전 후보(`previous`)에 없는 아이템을 먼저 뽑고, 부족할 때만 직전 후보에서 채운다.
- 뽑을 수 있는 아이템이 `count`보다 적으면 가능한 수만큼만 반환한다. 풀이 비면 0개이며, 이때 `RequiredSelectCount`도 0이라 선택 없이 확정할 수 있다.
- 1개를 뽑을 때마다 남은 아이템이 있는 종류끼리만 가중치(무기 50 / 패시브 35 / 소비 15, `GameScene` 값)를 정규화해 종류를 고르고, 그 종류 안에서 균등 추첨한다.
- 종류 인덱스는 `ItemType` 정수값 0~2(Weapon·Passive·Expendables)를 그대로 쓴다 (`DRAWABLE_TYPE_COUNT` 주석).
- `LevelUpPanel`은 `System.Random` 인스턴스 1개를 필드로 두고 매번 넘긴다.
- `LevelUpPanel.GenerateCandidates`는 `Inventory.CountExpansionCandidates()`가 남은 확장 수보다 크지 않으면 `ExpendableEffectType.ExpandCell` 아이템(확장권)을 풀에서 빼고 넘긴다. 이미 예약된 확장 수를 빼면 남는 칸이 없어 확장권이 효과가 없기 때문이다 (코드 주석).

#### 기타

- `Pause`·`Resume`은 `GameSession`의 `IsPlaying` 조건을 따른다. 종료 대기 중 레벨업의 처리는 주의사항 n 참고.
- `_panelRoot`는 이 컴포넌트가 붙은 오브젝트와 달라야 한다 (필드 주석 "이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨"). 이유는 `ResultPanel`과 같다.

### `ItemTooltipUI`

| 멤버 | 내용 |
|---|---|
| `Show(Inventory.ItemInstance item, object source)` | 보유 아이템 표시. 레벨 `item.Level`, 판매가 `Inventory.GetSellPrice(item)`. 호출부 `InventoryItemUI.OnPointerEnter` |
| `Show(InventoryData data, object source)` | 보상 후보 표시. 레벨 `REWARD_LEVEL`(1)인 미배치(`Location` None) `ItemInstance`를 임시로 만들어 표시한다 (인벤토리에 넣지 않음, 인접 효과 0·Lv.1 기준으로 계산됨, 코드 주석). 판매가 `Inventory.GetSellPrice(data, 1)`. 호출부 `RewardCardUI.OnPointerEnter` |
| `Hide(object source)` | `source`가 현재 표시 요청자(`_source`)일 때만 숨긴다. 다른 아이템 `Enter` 직후 이전 아이템의 `Exit`가 와도 새 툴팁이 꺼지지 않게 하기 위해서다 (요약 주석) |
| `_tooltipRect` (Inspector) | 툴팁 패널 루트. 표시/숨김은 이 오브젝트의 `SetActive`로 한다 |
| `_cursorOffset` (Inspector) | 커서에서 띄우는 화면 픽셀 거리. 기본값·`GameScene` 값 (16, -16) |

- `source`는 요청한 UI 자신(`this`)이다. 툴팁은 1개이며 마지막 `Show`가 이전 표시를 덮어쓴다.
- `Awake` : `_tooltipRect`를 끈다. `OnDisable` : `_source`를 비우고 `_tooltipRect`를 끈다 (패널이 닫혔다 열릴 때 이전 툴팁이 남지 않도록, 코드 주석). `_tooltipRect`가 자신이면 끄지 않는다.

#### 표시 항목 (`Display`)

| 항목 | 내용 | 숨김 조건 |
|---|---|---|
| 아이콘·이름 | `Icon`, `ItemName` | 없음 |
| 종류 | `ItemType` → `"무기"`·`"패시브"`·`"소비"`, 그 외(`Essential`)는 `"고유"` (`switch`의 `default`) | 없음 |
| 레벨 | `"Lv.{레벨}"` | `MaxLevel ≤ 1`이면 줄 숨김 |
| 설명 | `Description` | 없음 (빈 문자열이어도 줄은 켜져 있다) |
| 스탯 (`_attackText`, 여러 줄) | 아래 `스탯 줄` | 무기(`WeaponDefinition` 있음)·패시브가 아니면 줄 숨김 |
| 장착 효과 | `"장착: {EquippedEffect}"` | 값이 비면 줄 숨김 |
| 해제 버프 | `"해제 시: {UnequippedBuff}"` | 값이 비면 줄 숨김 |
| 해제 디버프 | `"해제 시: {UnequippedDebuff}"` | 값이 비면 줄 숨김 |
| 효과 묶음 (`_effectContainer`) | 위 세 줄의 부모 | 세 줄이 모두 비면 숨김 |
| 판매가 | `IsSellable`이면 `"판매가 {판매가}"`, 아니면 `"판매 불가"` | 없음 |

- 줄을 끄는 것은 레이아웃 공간을 없애기 위해서다 (코드 주석). 루트를 켠 뒤 `LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect)`로 크기를 즉시 반영하고 위치를 계산한다 (줄 활성 상태가 바뀐 직후 크기를 반영해야 위치 보정이 맞음, 코드 주석).
- 스탯 줄 (`GetStatText`, 줄바꿈으로 연결)
    - 무기 : `"기본 공격력 {ProjectileDamage}"` → `"레벨 보너스 +{(레벨 배율 − 1) × 100:0}%"` → 인접 효과 공격력·발사 간격·탄퍼짐(`GetEffectiveAdjacencyBonus`, 상한 적용 값, 0이면 줄 생략) → 특성·고유 효과 줄 (Issue #40, 0 이하이면 줄 생략, `AddUniqueLine`) : `"특성 공격력 +{GetTraitDamageBonus × 100:0}%"` → `"특성 총알 +{GetTraitProjectileBonus:0}"` → `"무기 정확도 +{GetCharacterAccuracyBonus × 100:0}%"`(특성 + 장착 중 장착 정확도 합계, 상한 미적용) → `"해제 효과 공격속도 +{GetUniqueAttackSpeedBonus × 100:0}%"` → `"해제 효과 확산각 {GetUniqueSpreadAngle:0}°"` → `"최종 공격력 {최소:0}~{최대:0}"` → Lv.3 줄.
    - 해제 효과 두 줄은 고유 아이템 해제 중에만 값이 있어 표시된다. 대상 외 무기(`UniqueWeaponType`이 다름)는 특성·고유 효과 줄이 모두 생략된다.
    - 최종 공격력 범위 = `GetRolledDamage(GetWeaponStats(item).Damage, DamageVariance, 0)` ~ `GetRolledDamage(…, 1)`, 반올림 정수 표기. 전투의 탄별 피해와 같은 함수·같은 최종 공격력을 쓴다 (`Docs/TechDocs/Weapon.md`).
    - 패시브 : `AdjacencyEffectType`이 None이 아니면 인접 효과 줄(`GetPassiveAdjacencyBonus(레벨)`, 패시브 1개가 주는 수치) → Lv.3 줄.
    - Lv.3 줄 : `IsLevel3EffectActive(item)`이 false면 `"Lv.3 효과 비활성"`. 활성이면 `InventoryData.Level3EffectName`(`_level3EffectName`)이 있을 때 `"Lv.3 활성: {효과명}"`, 비어 있으면 `"Lv.3 효과 활성"`.
- 종류 라벨 `ItemTooltipUI.GetTypeLabel(ItemType)`(무기/패시브/소비/고유)은 public static이며 `RewardCardUI`도 쓴다.
- 무기 툴팁의 인접 탄퍼짐 줄은 상한 적용 인접 값만 보여 주고, 실제 탄퍼짐 감소율(인접 + 정확도 + Lv.3 합산 후 상한 0.7, `Docs/TechDocs/Weapon.md`)과 다를 수 있다. 무기 Lv.3 감소율은 별도 줄로 표시하지 않는다.
- 해제 버프와 해제 디버프는 같은 접두어 `"해제 시:"`이며 텍스트 색(`GameScene` 값 : 버프 초록, 디버프 빨강)으로만 구분한다 (주의사항 ae).

#### 위치 계산 (`UpdatePosition`)

1. `Mouse.current`가 null이면 아무것도 하지 않는다.
2. 기본 pivot (0, 1) + `_cursorOffset` = 커서 오른쪽 아래로 펼침.
3. 오른쪽으로 화면(`Screen.width`)을 넘으면 pivot x = 1, offset x 반전 (커서 왼쪽으로 펼침). 아래로 화면을 넘으면 pivot y = 0, offset y 반전 (커서 위로 펼침). 넘는 축만 뒤집는다.
4. `커서 + offset`을 pivot 기준 툴팁 크기(`rect.size × lossyScale`)만큼 화면 안으로 `Mathf.Clamp`한 뒤 `_tooltipRect.pivot`·`position`에 대입.

- 월드 좌표를 화면 좌표로 그대로 쓰는 것은 Screen Space Overlay 전제다 (코드의 `shortcut` 주석 : Camera 캔버스 전환 시 카메라 기준 변환 필요).

#### 호출 측 규칙

| 호출 측 | `Show` | `Hide(this)` |
|---|---|---|
| `RewardCardUI` | `OnPointerEnter` (`_tooltip`·`_data`가 있을 때) | `OnPointerExit`, `OnDisable`(확정·회차 전환으로 꺼지면 `Exit`가 오지 않으므로, 코드 주석) |
| `InventoryItemUI` | `OnPointerEnter` (`_tooltip`·`item`이 있고 드래그 중이 아닐 때 : `eventData.dragging`이 false이고 `_draggingItem`이 null) | `OnPointerExit`, `OnBeginDrag`(드래그 시작 시 숨김), `OnDisable`(판매 등으로 꺼지면 `Exit`가 오지 않으므로, 코드 주석) |

- 드래그 중에는 다른 아이템 위를 지나도 툴팁을 띄우지 않는다 (코드 주석).
- 보상 카드는 `Selecting`에서만 켜져 있다. 아이템 UI는 Issue #23부터 `Selecting`에서도 포인터 이벤트를 받아 hover 툴팁을 띄운다. 드래그는 `InventoryItemUI.IsDragEnabled`가 false이면 `OnBeginDrag`에서 `eventData.pointerDrag = null`로 취소되어 `OnDrag`·`OnEndDrag`가 호출되지 않는다 (코드 주석).

#### 아이템 설명 데이터 (`InventoryData`)

| 필드 (프로퍼티) | 내용 |
|---|---|
| `_description` (`Description`) | 설명. `[TextArea]` |
| `_equippedEffect` (`EquippedEffect`) | 장착 효과 텍스트. `[TextArea]` |
| `_unequippedBuff` (`UnequippedBuff`) | 해제 시 버프 텍스트. `[TextArea]` |
| `_unequippedDebuff` (`UnequippedDebuff`) | 해제 시 디버프 텍스트. `[TextArea]` |

- 텍스트 필드 3개는 툴팁 표시 전용이며 전투 수치와 연결하지 않는다 (필드 주석, 주의사항 af).
- 값 : `Assets/Data/Weapon/Inventory/SO_Inventory_*.asset` 14개(소모품 `ExpansionTicket`·`HealthPack` 포함) 모두 `_description`이 입력되어 있고, 텍스트 필드 3개는 `CowboyHat`·`CombatVest`·`ProteinShake` 3개에만 입력되어 있다. 소모품 2종을 제외한 12종의 출처는 `Docs/Spec/Inventory/ItemDescriptions.md`다. 고유 아이템 3종의 텍스트 필드는 하단 `캐릭터별 특성·고유 아이템 효과 요약` 표의 장착 효과·해제 버프·해제 디버프 열과 문구를 대조해 수치가 일치함을 확인했다 (이슈 #40). 예외 : 모자 `_equippedEffect`는 요약 표의 "없음" 대신 같은 문서 §1 `장착 효과`의 툴팁 문구("[장착 효과: 없음] 대신 거울을 볼 때마다 …")이고, 셰이크 `_unequippedBuff`는 빈 문자열(요약 표 "없음")이며, 조끼 `_equippedEffect`는 "가방 +2칸, 보관함 한도 +2개"(요약 표는 "보관함 +2개", 상단 표 ID 11은 "보관함 한도 +2개")다. 14개 `_description` 문구는 대조하지 않았다 (미검증).

### 인벤토리 API (변경분 누적 : `art/fix-inven-ui`, Issue #23)

인벤토리 전체 기술 문서는 없다 (별도 작업). 보상 화면·툴팁·성장·효과가 쓰는 변경 API만 적는다. 무기 최종 스탯(`GetWeaponStats`)과 무기 장착 동기화(`InventoryWeaponSync`)는 `Docs/TechDocs/Weapon.md`.

| 멤버 | 내용 |
|---|---|
| `InventoryItemUI.Initialize(target, targetInventory, targetInventoryUI, sellPopup, tooltip)` | 런타임 생성 아이템용 오버로드. `Inventory`·`InventoryUI`·`SellConfirmPopup`·`ItemTooltipUI` 참조를 주입하고, 아이콘(`Data.Icon`)과 크기(`GetItemSize(회전 0 Shape)`)를 설정한 뒤 기존 `Initialize(target)`으로 표시한다. 표시 위치를 데이터에서 읽으므로 영역 이동(`MoveToTemporary` 등) 뒤에 호출해야 한다 (요약 주석). `Awake`에서 얻은 `_image`를 쓰므로 `Awake`가 실행된 뒤여야 한다 (주의사항 y) |
| `InventoryUI.SetRotateEnabled(bool)` | 공유 회전 액션의 허용 여부. 값을 `_isRotateEnabled`에 보관하고, 컴포넌트가 활성일 때만 즉시 `Enable`/`Disable`한다. 비활성 중이면 다음 `OnEnable`에서 `_isRotateEnabled`가 true일 때만 `Enable`한다 (패널 밖에서 회전 입력이 살아남지 않게, 코드 주석) |
| `InventoryUI.GetItemSize(Vector2Int[] shape)` | 회전 0 정규화 Shape의 바운딩 칸 수로 이미지 크기 계산 : `칸 수 × cellSize + (칸 수 − 1) × spacing` (`GameScene` 값 cellSize 72, spacing 4) |
| `InventoryItemUI.OnPointerEnter` / `OnPointerExit` | `IPointerEnterHandler`·`IPointerExitHandler` 구현. 툴팁 표시·숨김 (위 `ItemTooltipUI`의 `호출 측 규칙`) |
| `Inventory.IsCellAvailable(Vector2Int)` | 사용 가능(잠금 해제) 칸인지. 범위 밖이면 false (Issue #23) |
| `Inventory.IsExpansionCandidate(Vector2Int)` | 확장 후보인지. 범위 안이고 잠긴 칸이며 상하좌우에 사용 가능한 칸이 있으면 true (Issue #23) |
| `Inventory.CountExpansionCandidates()` | 현재 확장 후보 칸 수 (10×6 전체 순회, Issue #23) |
| `Inventory.TryUnlockCell(Vector2Int)` | 후보일 때만 칸을 사용 가능으로 바꾸고 `Changed` 1회 발생 후 true. 후보가 아니면 상태 변화 없이 false (Issue #23) |
| `InventoryUI.SetExpansionMode(bool)` | 확장 후보 강조·클릭 통지 여부. 활성 중이면 즉시 칸 색 갱신, 비활성 중이면 다음 `OnEnable`의 갱신으로 반영 (코드 주석, Issue #23) |
| `InventoryUI.ExpansionCellClicked` (`Action<Vector2Int>`) | 확장 모드에서 확장 후보 칸을 클릭하면 셀 좌표로 발생. `InventoryUI`가 `IPointerClickHandler`를 구현하며, Grid의 Slot 셀이 레이캐스트를 받아 부모 `InventoryUI`로 클릭이 버블링된다 (코드 주석). 확장 모드가 아니거나 후보 칸이 아니면 발생하지 않는다 (Issue #23) |
| `InventoryUI` 칸 색 표시 | `Awake`에서 `gridRect`의 자식 60개(기존 Slot, 인덱스 `y × 10 + x`, 좌상단부터 행 우선)의 `Image`를 모아 두고, `OnEnable`·`Inventory.Changed`·`SetExpansionMode`마다 색을 갱신한다. 사용 가능 `_availableCellColor`(흰색) / 확장 모드의 후보 `_candidateCellColor`(노란색 (1, 0.85, 0.2)) / 그 외 잠김 `_lockedCellColor`(회청색 (0.45, 0.5, 0.58)). 자식 수가 60이 아니면 `LogError` 후 수집을 중단한다. `_inventory`(Inspector) 필드 필요 (Issue #23) |
| `InventoryItemUI.IsDragEnabled` (`static bool`, 기본 true) | 아이템 드래그 허용 여부. 전체 아이템 UI가 공유하며 `LevelUpPanel.ApplyPhase`·`HandleCompleteClicked`가 바꾼다 (Issue #23) |
| `Inventory.GetSellPrice(InventoryData data, int level)` | 보유하지 않은 아이템(보상 후보 등)의 판매가 `level × SELL_PRICE_PER_LEVEL`(1). `data`가 null이면 0. 기존 `GetSellPrice(ItemInstance)`는 null이면 0, 아니면 `GetSellPrice(item.Data, item.Level)`에 위임한다 |

#### 영역·임시 배치·판매

- 아이템 영역 `ItemLocation` : `None`(생성 후 어디에도 없음·판매·합성 재료 소비), `Inventory`(그리드 셀 점유, `IsPlaced`), `Storage`(보관함, Shape 무관 1칸 = 1개. 한도는 아래 `보관함 한도`), `Temporary`(셀 점유 없음, `TemporaryPosition` 로컬 좌표).
- `TryPlace(item, position, rotation)` : 출발 영역과 무관하게 지정 회전으로 그리드 배치. 실패 시 원래 영역·위치·회전 유지. `TryStore(item)` : 현재 한도 안의 첫 빈 보관함 슬롯에 넣고 회전 0. 이미 보관함에 있으면 변화 없이 true. 고유 아이템 자신을 넣어 장착이 풀리는 경우에는 줄어든 한도(5) 안의 슬롯만 쓴다 (코드 주석). `MoveToTemporary(item, 위치, rotation)` : Temporary로 이동(이미 Temporary면 좌표·회전만 갱신, 충돌 검사 없음). `RotateItem` : Inventory는 충돌 검사 후 회전, Temporary는 검사 없이 회전.
- 반환값 (Issue #40) : `RemoveItem`·`MoveToTemporary`는 `bool`을 반환한다. `RemoveItem`은 `item`이 null·미배치·고유 아이템 해제 거부이면 false, `MoveToTemporary`는 `item`이 null·고유 아이템 해제 거부이면 false이며 둘 다 실패 시 상태 변화가 없다.
- 판매 : `CanSell`(`IsSellable`이고 `Location != None`) → `TrySell`(출발 영역 해제, `Location = None`, 재화 지급은 호출부). `InventoryItemUI`가 판매 영역(`IsOverSellArea`)에 드롭하면 `CanSell`일 때만 `SellConfirmPopup.Open`을 띄우고 아이템은 원위치로 돌아간다. 확인 버튼에서 상태 변경 전에 `GetSellPrice`로 가격을 확정한 뒤 `TrySell` 성공 시 `Currency.Add(가격)`·아이템 UI `SetActive(false)`. 팝업이 열린 동안 회전 입력을 무시한다.
- 이벤트 순서 : 출발 영역 해제(`ReleaseLocation`) → 도착 영역 기록 → 조끼 전용 칸 갱신(`RefreshVestCells`) → `TemporaryCountChanged`/`StorageCountChanged`(출발·도착 중 한쪽만 해당 영역일 때) → `Changed`. `RefreshVestCells`는 상태 확정 후 모든 통지보다 먼저 호출해 구독자가 중간 상태를 보지 않게 한다 (코드 주석). 호출 위치는 `Character` setter, `PlaceItem`, `RemoveItem`, `TryPlace`, `TryStore`, `MoveToTemporary`, `TrySell`, `TryMerge`다 (`MoveItem`·`RotateItem`은 장착 상태가 바뀌지 않아 호출하지 않음).

#### 보관함 한도 (Issue #40)

| 멤버 | 내용 |
|---|---|
| `BASE_STORAGE_CAPACITY` (private const) | 5. 기본 보관함 한도 |
| `MAX_STORAGE_CAPACITY` (private const) | 7. 보관함 슬롯 배열 크기 (기본 + 고유 아이템 장착 확장 최대) |
| `StorageCapacity` | 현재 보관함 한도 = `Min(5 + (고유 아이템 장착 중이면 Character.EquippedStorageBonus, 아니면 0), 7)`. 겁쟁이(`SO_Survivor`, 보너스 2)는 조끼 장착 중 7, 해제 중 5 |
| `StorageCount` | 슬롯 배열(7칸)에서 비어 있지 않은 슬롯 수 |
| `GetStorageItem(index)` / `GetStorageIndex(item)` | 슬롯 배열 기준 (인덱스 0~6) |

- 이전 public 상수 `STORAGE_CAPACITY`(5)는 삭제되었다.
- `InventoryUI.RefreshStorageSlots` : `OnEnable`과 `Inventory.Changed`마다 `_storageSlots[i]`를 `i < StorageCapacity`일 때만 켜고 나머지(한도 밖 슬롯)는 숨긴다. `_storageSlots`는 인덱스 = Inventory 보관함 슬롯 인덱스이며 7개(기본 5 + 확장 2)를 연결한다 (필드 주석).

#### 조끼 전용 칸 (Issue #40)

- 전용 칸 좌표는 `(4, 5)`, `(5, 5)`로 고정이다 (`_vestCells`). 초기 활성 영역(x 3~6, y 1~4)에 맞닿은 y = 5 행의 가운데 두 칸이다.
- 예약 (`IsVestReservationActive`) : `Character.EquippedUnlocksVestCells`가 true인 캐릭터(겁쟁이 `SO_Survivor`)이고 아직 예약 해제 전일 때만 예약 중이다. 그 외 캐릭터에게 두 칸은 일반 칸이다.
- 예약 중 (`RefreshVestCells`) : 두 칸의 사용 가능 여부를 고유 아이템 장착 상태와 같게 맞춘다. 장착 중이면 열림, 해제 중이면 잠김. 예약 중인 두 칸은 확장 후보에서 제외된다 (`IsExpansionCandidate`). 예약이 없으면 `RefreshVestCells`는 아무것도 하지 않아, 확장으로 연 칸의 상태가 유지된다.
- 예약 해제 (`TrySell`) : 조끼 캐릭터(`EquippedUnlocksVestCells`)이고 판매 대상이 현재 캐릭터의 고유 아이템이며, 판매 뒤 같은 데이터의 고유 아이템이 그리드·보관함에 남아 있지 않으면(`HasUniqueItemInstance`가 false) `_isVestReservationReleased = true`. 판매 처리에서 먼저 `RefreshVestCells`로 칸을 잠근 뒤 해제하므로, 두 칸은 잠긴 채 일반 확장 후보로 남는다 (코드 주석). 이후 다시 고유 아이템을 얻어도 예약은 돌아오지 않는다 (재설정 경로는 `Character` setter뿐). 합성은 같은 Id의 target이 남으므로 예약 해제 대상이 아니다 (코드 주석).
- 보스·레벨업 확장(`TryUnlockCell`)으로 연 다른 칸은 장착 상태와 무관하게 유지된다. 예약 중에는 전용 칸을 확장으로 열 수 없으므로, 전용 칸이 확장으로 열리는 것은 예약 해제 뒤뿐이다.

#### 고유 아이템 해제 거부 (`CanReleaseUniqueEffect`, Issue #40)

- 대상 : Inventory에 배치된 아이템이 그리드를 떠나 고유 아이템 장착이 풀리는 경우만 검사한다. `item`이 미배치이거나, 현재 장착 중이 아니거나, `item`을 빼도 다른 고유 아이템 인스턴스로 장착이 유지되면 항상 허용한다.
- 거부 조건 (하나라도 해당하면 거부)
    1. 조끼 예약 중이고 전용 칸 `(4,5)`·`(5,5)`에 `item` 외의 아이템이 있다.
    2. 보관함 6·7번째 슬롯(인덱스 5·6, 한도가 줄면 숨겨질 슬롯)에 아이템이 있다. 예약 여부와 무관하다.
- 거부 시 `RemoveItem`·`TryStore`·`MoveToTemporary`·`TrySell`·`TryMerge`(재료 `source` 기준)가 false를 반환하고 상태·이벤트 변화가 없다. 그리드에 놓는 조작(`PlaceItem`·`TryPlace`·`MoveItem`·`RotateItem`)은 그리드를 떠나지 않으므로 검사하지 않는다.
- `Inventory`에는 거부 이유를 사용자에게 안내하는 코드가 없다. 거부 시 UI 동작 중 코드로 확인한 부분은 아래와 같다.
    - 드롭 (`InventoryItemUI.OnEndDrag`) : 합성(`TryMerge`) 실패 시 영역별 처리로 진행하고, `TryStore`·`MoveToTemporary`의 반환값과 무관하게 마지막에 `RefreshPosition()`을 호출한다. 거부되면 데이터가 원래 영역·위치·회전 그대로이므로 원위치·원회전으로 돌아간다 (코드 주석).
    - 판매 : 드롭 시 `CanSell`(`IsSellable`·`Location != None`)만 보고 `SellConfirmPopup.Open`을 띄운다. `CanSell`은 해제 거부 조건을 검사하지 않으므로 팝업은 열리고, 확인 버튼에서 `TrySell`이 거부되면 재화 지급·아이템 UI 비활성화 없이 팝업만 닫힌다 (`HandleConfirmClicked`). 거부 안내 문구는 없다.
    - 화면에서 실제로 원위치 복귀·팝업 닫힘이 보이는지는 Play로만 확인할 수 있으며, 이 문서 작성 시 Play 확인은 하지 않았다 (미검증).

#### 합성 (`CanMerge` / `TryMerge`)

- `CanMerge(source, target)` : 서로 다른 두 아이템이 모두 어느 영역에 있고(`None` 아님), `Data.Id`가 같고, `Level`이 같고, `target.Level < MaxLevel`. 상태를 바꾸지 않는다.
- `TryMerge(source, target)` : `CanMerge` 실패 시 false·변화 없음. 성공 시 `source`의 영역을 해제해 `None`으로 소비하고 `target.Level++`. `target`의 영역·위치·회전은 유지한다. 개수 이벤트(재료가 Temporary·Storage에 있었으면) 후 `Changed` 1회.
- 호출부 : `InventoryItemUI` 드롭 시 포인터 아래 가장 위의 다른 아이템 UI에 `TryMerge`(재료 = 드래그 중인 자신)를 먼저 시도하고, 성공하면 재료 UI를 끈다. 실패하면 영역별 처리(배치·판매·보관·Temporary)로 진행한다.
- `MaxLevel` : `Pistol`·`Shotgun`·`Minigun`·`AmmoBelt`·`GunStock`·`GunCooler` SO 5, 진화 무기 3종·`CowboyHat`·`CombatVest`·`ProteinShake` SO 1 (임시값). `MaxLevel` 1이면 합성할 수 없다.

#### 레벨 성장·인접 효과·패시브 Lv.3

| 항목 | 규칙 (상수는 `Inventory.cs`, 탄띠 Lv.3 이동속도 0.05(Issue #40 확정값)를 제외하고 모두 임시값) |
|---|---|
| 무기 레벨 공격력 배율 | `GetWeaponLevelMultiplier(level)` = `1 + (level − 1) × 0.1` (복리 아님) |
| 패시브 인접 효과 | `GetPassiveAdjacencyBonus(level)` = `level × 0.1`. Inventory에 배치된 무기 셀과 상하좌우로 맞닿은 패시브만 적용(대각선 제외, 회전 반영, `GetAdjacentItems`). 같은 패시브가 여러 셀로 닿아도 1회. 패시브 여러 개는 합산 (`GetAdjacencyBonus`, 상한 미적용) |
| 인접 효과 상한 | `GetEffectiveAdjacencyBonus` : 발사 간격 0.5, 탄퍼짐 0.7, 공격력 상한 없음. 무기 최종 스탯 계산은 `Docs/TechDocs/Weapon.md` |
| 인접 효과 종류 (SO 값) | `SO_Inventory_AmmoBelt` 공격력, `SO_Inventory_GunStock` 발사 간격, `SO_Inventory_GunCooler` 탄퍼짐 |
| Lv.3 활성 (`IsLevel3EffectActive`) | 무기 : 레벨 3 이상. 패시브 : 레벨 3 이상이면서 Inventory에 배치. 그 밖(Storage·Temporary 패시브, 고유·소비 아이템)은 비활성 |
| 패시브 Lv.3 효과 (`GetLevel3Bonus(type)`) | Lv.3 활성 패시브 중 `Level3EffectType`이 같은 것의 `Level3EffectValue` 합 (인접 무관, 상한 미적용, Lv.3~5 같은 값). SO 값 : `AmmoBelt` 이동속도 0.05(Issue #40에서 0.1 → 0.05), `GunStock` 경험치 0.1, `GunCooler` 젬 수집 반경 0.15 |

- 고유(`Essential`) 아이템은 인접 효과를 주지도 받지도 않는다 (`InventoryTests.GetAdjacencyBonus_EssentialItem_NeitherGivesNorReceives`).
- enum `AdjacencyEffectType`(`None`, `Damage`, `FireInterval`, `Spread`)·`Level3EffectType`(`None`, `Experience`, `MoveSpeed`, `CollectRadius`)·`ItemType`(`Essential` 추가)은 SO 직렬화 정수값 보존을 위해 끝에만 추가한다 (코드 주석).

#### 캐릭터 특성·고유 아이템 장착/해제 효과

- 두 층 구조 (Issue #40) : 캐릭터 특성은 고유 아이템 탈착과 무관하게 항상 적용하고, 그 위에 고유 아이템 장착 효과(장착 중) 또는 해제 효과(해제 중) 중 하나만 더한다.
- 장착 판정 `IsUniqueItemEquipped()` : `Inventory.Character.UniqueItem`과 같은 데이터의 아이템이 그리드에 배치되어 있으면 장착. Storage·Temporary·판매·미지급은 해제다. `Character` 또는 `UniqueItem`이 null이면 false.
- `Inventory.Character`는 `InventoryWeaponSync.Start`가 시작 아이템 2개 배치 후 설정한다 (`Docs/TechDocs/Weapon.md`). setter는 조끼 예약을 초기화(`_isVestReservationReleased = false`)하고 `RefreshVestCells` 후 `Changed` 1회를 발생시킨다.
- 계약 : `Character`는 게임 시작 시 1회만 설정한다는 전제다. 다시 설정하면 상태가 맞지 않을 수 있다. 예 : 새 캐릭터가 조끼 캐릭터가 아니면 `RefreshVestCells`가 아무것도 하지 않아 전용 칸이 이전 상태(열림·잠김)로 남고, 조끼 캐릭터면 판매로 해제됐던 예약이 다시 살아난다 (코드 기준 추론, reviewer 권고, 미반영, 주의사항 as).
- 무기 효과는 `Character.UniqueWeaponType`과 같은 종류 무기에만 적용한다 (다른 캐릭터의 고유 효과가 섞이지 않도록, 코드 주석).

| 효과 (`CharacterDefinition` 필드) | 층 | 반영 위치 | `SO_Cowboy` | `SO_Survivor` | `SO_HeavyGunner` |
|---|---|---|---|---|---|
| 대상 무기 `_uniqueWeaponType` | — | — | Pistol | Shotgun | Minigun |
| 공격력 `_traitDamageBonus` | 특성 (항상) | 무기 공격력 `× (1 + 값)` (`GetTraitDamageBonus`) | 0.1 | 0.05 | 0 |
| 발사당 총알 `_traitProjectileBonus` | 특성 (항상) | 무기 `ProjectilesPerShot`에 더함 (`GetTraitProjectileBonus`) | 0 | 1 | 0 |
| 정확도 `_traitAccuracyBonus` | 특성 (항상) | 무기 탄퍼짐 감소율에 합산 (`GetCharacterAccuracyBonus`, 상한 0.7은 합산 후) | 0 | 0 | 0.1 |
| 이동속도 `_traitMoveSpeedBonus` | 특성 (항상) | `PlayerStatSync` 이동속도 (상한 +0.5) | 0 | 0 | −0.1 |
| 정확도 `_equippedAccuracyBonus` | 장착 중 | 특성 정확도와 합산 (`GetCharacterAccuracyBonus`) | 0 | 0 | 0.1 |
| 조끼 전용 칸 `_equippedUnlocksVestCells` | 장착 중 | 위 `조끼 전용 칸` | false | true | false |
| 보관함 한도 `_equippedStorageBonus` | 장착 중 | 위 `보관함 한도` (`StorageCapacity`) | 0 | 2 | 0 |
| 공격속도 `_unequippedAttackSpeedBonus` | 해제 중 | 무기 발사 간격 `/ (1 + 값)` (`GetUniqueAttackSpeedBonus`) | 0.6 | 0 | 0 |
| 확산각 `_unequippedSpreadAngle` (도) | 해제 중, 0보다 클 때 | 무기 기준 각도를 이 값으로 대체·랜덤 탄퍼짐 (`GetUniqueSpreadAngle`) | 70 | 0 | 0 |
| 이동속도 `_unequippedMoveSpeedBonus` | 해제 중 | `PlayerStatSync` 이동속도 (상한 +0.5) | 0 | 0.1 | −0.2 |
| 최대 체력 `_unequippedMaxHealthBonus` | 해제 중 | `PlayerStatSync` 최대 체력 | 0 | −0.6 | 0 |

- 위 SO 값은 확정 수치(`Docs/Spec/Inventory/ItemDescriptions.md`)이며 플레이 테스트 후 조절할 수 있다. 비율은 0.1 = 10%다.
- Issue #40에서 `_equippedDamageBonus`(장착 중 공격력), `_baseMoveSpeedBonus`, `_baseAccuracyBonus`, `_unequippedAccuracyBonus`는 삭제되었다. 카우보이 공격력은 특성으로 옮겨 모자 탈착과 무관하게 적용되고, 헤비거너는 해제 효과로 추가되는 정확도가 없다(특성 정확도 +10%는 해제 중에도 유지).
- `HasUniqueItemInstance` (private) : 고유 아이템 인스턴스가 남아 있는지 그리드(`IsUniqueItemEquipped`)와 보관함만 확인하고 Temporary는 확인하지 않는다 (코드의 `shortcut` 주석 : Temporary 아이템 목록이 없음, 고유 아이템은 보상 후보에서 제외되어 1개뿐이라 충분). 고유 아이템이 2개 이상 존재할 수 있게 되면 판매 시 예약 해제 판정이 틀릴 수 있다 (reviewer 권고, 미반영, 주의사항 as).
- `InventoryData`의 장착 효과·해제 버프·해제 디버프 텍스트는 여전히 표시 전용이며, 위 효과 수치와 연결되어 있지 않다. 고유 아이템 3종 문구의 수치는 위 SO 값과 일치함을 대조했다 (위 `InventoryData` 텍스트 필드 항목). 값이 바뀌면 문구를 따로 고쳐야 한다.

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
           GameHUD : ExperienceGained += …   PlayerHealthBar : Damaged·Healed += Refresh   ResultPanel : Ended += HandleEnded
           PauseMenu : 버튼 onClick 4개 구독, UI/Cancel performed += HandleCancelPerformed, 액션 Enable
           LevelUpPanel : LevelChanged += HandleLevelChanged, 완료·선택 완료·리롤 버튼 onClick, Inventory 이벤트 2개, Currency.Changed, 카드 Clicked 구독
           PlayerStatSync : Inventory.Changed += ApplyStats   InventoryWeaponSync : Inventory.Changed += SyncWeapons
[Start] GameHUD → 재화(Currency.Balance), 경험치 바·레벨   PlayerHealthBar → 체력바
[Start] PlayerStatSync → ApplyStats (Character 미설정이면 Lv.3 효과만)
[Start] InventoryWeaponSync → 시작 무기·고유 아이템 PlaceItem (각각 Changed) → Inventory.Character 설정 (Changed)
          → Changed마다 SyncWeapons(무기 장착·스탯, Weapon.md) · PlayerStatSync.ApplyStats(이동속도·최대 체력·경험치·수집 반경 배율)

[매 프레임 Update]
GameSession (Playing만)  ElapsedTime += deltaTime → IsCleared면 End(Cleared)
PlayerHealth             DamageGate.Tick → (timeScale 0이면 끝) → (살아 있고 게이트 열림) OverlapSphere → 첫 살아 있는 Enemy → TakeDamage(ContactDamage)
                           → Health.ApplyDamage → Damaged (체력바·Hit 애니메이션) → 처음 사망이면 Die
                              → _disableOnDeath 끔 → Died → GameSession.End(GameOver)
GameHUD                  타이머·처치 수 폴링 (바뀔 때만 텍스트 갱신)
[매 프레임 LateUpdate]
WeaponReloadGauge        첫 재장전 중 무기 → 게이지 켬·fillAmount = ReloadProgress / 없으면 끔

[Enemy 사망] Enemy.Died → GameSession.HandleEnemyDied   (젬·상자 드롭은 Enemy.md)
  ├─ Playing이면 KillCount++
  └─ ClearsSessionOnDeath(최종 보스)면 End(Cleared)
[젬 도착] PlayerExperience.AddExperience
  → (요구치 이상인 동안) 레벨업·LevelChanged × n → ExperienceGained → GameHUD 경험치 바·레벨
       └─ LevelChanged마다 LevelUpPanel.HandleLevelChanged : _pendingCount++
            └─ (화면 닫힘) Open
[상자 획득] TreasureChest.Update (Enemy.md) → LevelUpPanel.RequestChestReward : _pendingChestCount++
            └─ (화면 닫힘) Open
Open → GameSession.Pause → timeScale = 0 → StartRound → RefreshStorageCount → _panelRoot 켬
StartRound : _isShowingChest = (_pendingCount == 0) → Phase = Selecting
  → BeginExpansion(상자 회차면 4, 아니면 0) : 남은 확장 수 = Min(요청 수, 후보 수) (Selecting이라 확장 모드는 아직 끔) → 선택 초기화
  → RewardCandidateGenerator.Generate(풀, CardCount, 가중치, previous null) → RefreshRewardCards(후보 수만큼 카드 켬·Bind)
  → ApplyPhase (SelectingGroup 켬, TemporaryArea·완료 버튼 끔, IsDragEnabled false·회전 차단, 확장 모드·안내 문구 끔)
[카드 클릭] RewardCardUI.Clicked → HandleCardClicked : 선택이면 취소, 아니면 필요 수 미만일 때만 선택 → RefreshSelection
[리롤 버튼] HandleRerollClicked : Selecting·잔액 ≥ 비용 확인 → Generate(previous = 현재 후보)
  ├─ 새 후보 0개 → 중단 (차감 없음)
  └─ Currency.TrySpend 성공 → 후보 교체·선택 초기화 → RefreshRewardCards   (Currency.Changed → RefreshCurrency)
[선택 완료 버튼] HandleConfirmClicked : Selecting·선택 수 == 필요 수 확인 → Phase = Organizing (먼저)
  → 선택 순서대로 (Expendables는 ApplyExpendable : Heal → PlayerHealth.Heal / ExpandCell → BeginExpansion(남은 수 + 값), 등록 안 함) AddToTemporary : ItemInstance → 가로 균등 칸 중심 → ClampToTemporary
       → Inventory.MoveToTemporary → Instantiate(PFB_InventoryItem, ItemLayer) → InventoryItemUI.Initialize(5인자, _tooltip 포함)
  → 후보·선택 비움 → RefreshRewardCards (카드 모두 끔) → ApplyPhase (SelectingGroup 끔, TemporaryArea·완료 버튼 켬, 드래그·회전 허용,
       남은 확장 수 > 0이면 SetExpansionMode(true)로 후보 칸 노란색·안내 문구 "확장할 {n}칸을 선택하세요!!" 켬)
[확장 칸 클릭] Slot 클릭 → 부모 InventoryUI.OnPointerClick (확장 모드·후보 칸만) → ExpansionCellClicked(cell) → LevelUpPanel.HandleExpansionCellClicked
  → Inventory.TryUnlockCell 성공 (Changed → InventoryUI 칸 색 갱신) → 남은 수 −1 (후보 0이면 남은 수 0)
  → 남은 수 0이면 SetExpansionMode(false) → RefreshExpansionGuide (0이면 문구 숨김) → RefreshCompleteButton
[아이템 정리] 드래그로 인벤토리·저장고·판매 → Inventory.TemporaryCountChanged → RefreshCompleteButton (0개면 완료 버튼 켬, 경고 끔)
  드롭 시 포인터 아래 다른 아이템 → TryMerge (성공 : 재료 None·대상 Level+1·재료 UI 끔) / 실패 → 영역별 처리
  판매 영역 드롭 → CanSell이면 SellConfirmPopup.Open → 확인 : 가격 확정 → TrySell → Currency.Add → 아이템 UI 끔
  조작 성공마다 Inventory.Changed → InventoryWeaponSync.SyncWeapons (무기 장착·해제·스탯, Weapon.md) · PlayerStatSync.ApplyStats
    · LevelUpPanel.RefreshStorageCount (보관 수·한도 문구) · InventoryUI.RefreshCellColors · InventoryUI.RefreshStorageSlots (한도 밖 슬롯 숨김)
[툴팁 Hover]
  카드 Enter (Selecting) → RewardCardUI.OnPointerEnter → ItemTooltipUI.Show(data, 카드) → Display(Lv.1, GetSellPrice(data, 1)) → 루트 켬 → 레이아웃 재계산 → UpdatePosition
  아이템 Enter (Selecting·Organizing, 드래그 중 아님) → InventoryItemUI.OnPointerEnter → ItemTooltipUI.Show(item, 아이템) → Display(item.Level, GetSellPrice(item)) → (위와 같음)
  매 프레임 LateUpdate (_source 있음) → UpdatePosition
  Exit / 드래그 시작 / 비활성화 → Hide(자신) → 요청자가 현재 _source일 때만 루트 끔
  패널 닫힘 → TooltipLayer 비활성 → ItemTooltipUI.OnDisable → _source 비움·루트 끔
[완료 버튼] LevelUpPanel.HandleCompleteClicked : Organizing·TemporaryCount == 0·남은 확장 수 == 0 확인 → _isShowingChest면 _pendingChestCount--, 아니면 _pendingCount--
  ├─ 둘 중 하나라도 남음 → StartRound (다음 회차, 레벨업 우선, 새 후보)
  └─ 둘 다 0 → SetExpansionMode(false) · IsDragEnabled = true · 안내 문구 숨김 → _panelRoot 끔 → GameSession.Resume → timeScale = 1

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
   ├─ HealthFill              (Image Filled·Horizontal, SPR_BarFill, 빨강)
   └─ WeaponReloadGauge       (Layer UI, 중앙 앵커 기준 (72, 24) · 12×48, WeaponReloadGauge)
      └─ ReloadGaugeBackground (Prefab 저장 시 비활성, 부모 전체 stretch, Image 배경, WeaponReloadGauge._visualRoot)
         └─ ReloadGaugeFill   (Image Filled·Vertical(Fill Method 1), 초록 (0.2, 0.8, 0.2), Fill Amount 0, Raycast Target 꺼짐, 부모 기준 사방 2 안쪽, WeaponReloadGauge._fill)
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `PlayerHealth` (루트) | `_maxHealth` | 100 |
| | `_invulnerabilityDuration` | 0.5 |
| | `_contactRadius` | 0.45 (임시값. 코드 기본값 0.6, 필드 주석은 0.6 기준) |
| | `_enemyLayer` | Enemy |
| | `_disableOnDeath` | `PlayerMovement`, `PlayerAimController`, `Weapon`의 `WeaponAttackController` |
| `PlayerExperience` (루트) | `_collectRadius` | 0.8 (임시값, 배율 적용 전 기본 반경. 코드 기본값 0.4, 필드 주석은 0.4 기준) |
| `PlayerMovement` (루트) | `_moveSpeed` | 5 (배율 적용 전 기본 이동속도) |
| `WeaponReloadGauge` | `_loadout` | `Weapon`의 `WeaponLoadout` |
| | `_fill` | `ReloadGaugeFill`의 Image |
| | `_visualRoot` | `ReloadGaugeBackground` |
| `PlayerHealthBar` (HealthBarCanvas) | `_playerHealth` | 루트 `PlayerHealth` |
| | `_fill` | `HealthFill`의 Image |
| | `_camera` | Prefab에서는 비어 있음. Scene 인스턴스에서 `MainCamera` 연결 필요 |

- `_camera`는 Prefab에서 Scene 오브젝트를 참조할 수 없어 Scene 인스턴스 오버라이드로 연결한다. `GameScene`·`TestMap`의 `PFB_Player` 인스턴스 모두 `MainCamera`의 Camera로 연결되어 있다. 새 Scene에 `PFB_Player`를 놓으면 직접 연결해야 한다 (비면 `LateUpdate`에서 `NullReferenceException`).
- 배경 Image는 Unity 기본 `UISprite`(Sliced)를 쓴다. 채움 Image만 `SPR_BarFill`이다 (아래 `스프라이트` 참고).
- `WeaponReloadGauge`는 `HealthBarCanvas`의 자식이라 체력바와 함께 카메라 방향으로 회전한다. `WeaponReloadGauge` 컴포넌트에 스크립트에 없는 직렬화 키 `_camera`(비어 있음)가 남아 있다 (사용되지 않음).
- `PlayerStatSync`는 Prefab이 아니라 `GameScene`의 `PFB_Player` 인스턴스 루트에 Scene에서 추가한 컴포넌트다 (`_inventory` = Scene의 `Inventory`, `_movement`·`_health`·`_experience` = 인스턴스 루트의 컴포넌트). 다른 Scene에 `PFB_Player`를 놓으면 플레이어 배율이 적용되지 않는다.

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
│  ├─ PFB_LevelUpPanel        (PFB_LevelUpPanel 인스턴스, Scene 저장 시 비활성, LevelUpPanel._panelRoot, InventoryUI 추가, 구성은 아래 `PFB_LevelUpPanel` + 아래 추가 오브젝트)
│  │  ├─ (Prefab 자식. RewardCards는 위치 오버라이드 (128, -215))
│  │  ├─ StoragePanel/StorageSlot_01 ~ 07 (인스턴스의 Prefab 자식 StoragePanel 아래에 Scene에서 추가, 각 280×95, 중앙 기준 x 0 · y 315부터 105 간격으로 아래로(07 = -315),
│  │  │                        InventoryUI._storageSlots[0] ~ [6] 순서대로 연결. 06·07은 Issue #40 추가)
│  │  ├─ ExpansionGuideText   (인스턴스에 추가 · Prefab 자식 InventoryFrame 다음 형제, Layer 0 Default, Scene 저장 시 비활성, 중앙 기준 (129, 440) · 700×56 = 프레임 상단 중앙,
│  │  │                        Text 노란색 (1, 0.85, 0.2) · 굵게 · 크기 40 · 가운데 정렬 · Raycast Target 꺼짐, Outline 검정 (1, -1), LevelUpPanel._expansionGuideText)
│  │  ├─ SelectingGroup       (인스턴스에 추가, 중앙 기준 (120, -315) · 1150×400, LevelUpPanel._selectingGroup)
│  │  │  ├─ RerollButton      ((-300, -110) · 170×56, Button, Image RewardCard, LevelUpPanel._rerollButton)
│  │  │  │  ├─ RerollIcon     (Image F_UI_MenuIcons_Tile Map 16x16_11)
│  │  │  │  ├─ CurrencyIcon   (Image SPR_SphereGem)
│  │  │  │  └─ CostText       (Text 크기 28, "2")
│  │  │  ├─ CurrencyText      ((-100, -110) · 180×50, Text 크기 28, 저장 값 "보유 0", LevelUpPanel._currencyText)
│  │  │  ├─ SelectionCountText ((130, -110) · 160×50, Text 크기 28, 저장 값 "0/2", LevelUpPanel._selectionCountText)
│  │  │  └─ ConfirmButton     ((400, -110) · 220×64, Button, Image RewardCard, LevelUpPanel._confirmButton)
│  │  │     └─ Label          (Text "선택 완료")
│  │  ├─ TemporaryArea        (인스턴스에 추가, Layer 0 Default, 중앙 기준 (120, -315) · 1150×400, Image 검정 α 약 0.25 · 스프라이트 없음 · Raycast Target 꺼짐, LevelUpPanel._temporaryArea · InventoryUI._temporaryArea)
│  │  ├─ ItemLayer            (인스턴스에 추가, CanvasGroup blocksRaycasts true · 코드에서 사용 안 함, LevelUpPanel._itemLayer, InventoryUI.itemLayer)
│  │  │  └─ 테스트 아이템 13개 (PistolItem_01 등, InventoryItemUI, 모두 Scene 저장 시 비활성, _tooltip 미연결)
│  │  └─ TooltipLayer         (인스턴스에 추가 · 마지막 자식, 전체 stretch, Scene 저장 시 활성, ItemTooltipUI. Image 없음)
│  │     └─ ItemTooltip       (ItemTooltipUI._tooltipRect, 폭 320 · pivot (0, 1), Image 짙은 남색 (0.106, 0.133, 0.208, α 0.95) · Raycast Target 꺼짐,
│  │        │                  CanvasGroup blocksRaycasts·interactable false, VerticalLayoutGroup 패딩 14/14/12/12 · 간격 6, ContentSizeFitter 세로 Preferred)
│  │        ├─ Header         (HorizontalLayoutGroup 간격 10)
│  │        │  ├─ ItemIcon    (Image, _icon)
│  │        │  └─ ItemName    (Text, _nameText)
│  │        ├─ ItemType       (Text, _typeText)
│  │        ├─ LevelText      (Text, _levelText)
│  │        ├─ Description    (Text, _descriptionText)
│  │        ├─ AttackText     (Text, _attackText)
│  │        ├─ EffectContainer (VerticalLayoutGroup 간격 4, _effectContainer)
│  │        │  ├─ EquippedEffect   (Text 초록 (0.45, 0.9, 0.45), _equippedEffectText)
│  │        │  ├─ UnequippedBuff   (Text 초록 (0.45, 0.9, 0.45), _unequippedBuffText)
│  │        │  └─ UnequippedDebuff (Text 빨강 (1, 0.42, 0.42), _unequippedDebuffText)
│  │        └─ SellPrice      (Text, _sellPriceText)
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
- 새 텍스트는 레거시 `UnityEngine.UI.Text`이며 폰트는 `Bithumb Gothic Basic.ttf`다. HUD·결과 패널의 TMP와 다르다 (아래 `텍스트·폰트`).

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `GameSession` | `_playerHealth` | 플레이어 Prefab 인스턴스의 `PlayerHealth` |
| | `_clearTime` | 1380 (23:00, Inspector에서 조절) |
| | `_endFreezeDelay` | 1.5 |
| `GameHUD` (HUDCanvas) | `_gameSession` | `Managers/GameSession` |
| | `_playerExperience` | 플레이어 `PlayerExperience` |
| | `_currency` | Scene의 `Currency` 오브젝트의 `Currency` (`LevelUpPanel._currency`와 같은 컴포넌트) |
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
| | `_playerHealth` | 플레이어(`PFB_Player`) `PlayerHealth` |
| | `_panelRoot` | `HUDCanvas/PFB_LevelUpPanel` (Scene 인스턴스 루트 GameObject, 컴포넌트가 붙은 `HUDCanvas`와 다른 오브젝트) |
| | `_completeButton` | 인스턴스의 `CompleteButton` Button |
| | `_rewardCards` | 인스턴스의 보상 카드 `RewardCardUI` 5개 (Scene 인스턴스 오브젝트) |
| | `_levelUpCardCount` / `_chestCardCount` | 3 / 5 |
| | `_levelUpMaxSelect` / `_chestMaxSelect` | 2 / 3 |
| | `_rewardPool` | `InventoryData` 에셋 8개 (Issue #25에서 `SO_Inventory_HealthPack`(구급 상자, Heal 20)·`SO_Inventory_ExpansionTicket`(확장권, ExpandCell 1) 추가) |
| | `_weaponWeight` / `_passiveWeight` / `_expendableWeight` | 50 / 35 / 15 |
| | `_rerollCost` | 2 |
| | `_currency` | Scene의 `Currency` 오브젝트의 `Currency` |
| | `_rerollButton` / `_currencyText` / `_selectionCountText` / `_confirmButton` | `SelectingGroup` 하위 (위 Hierarchy 참고) |
| | `_selectingGroup` | 인스턴스의 `SelectingGroup` |
| | `_inventory` | Scene의 `InventorySystem` 오브젝트의 `Inventory` |
| | `_temporaryWarning` | Scene의 `TemporaryWarning` |
| | `_storageCountText` | 인스턴스의 `StorageTitleText` Text |
| | `_temporaryArea` | 인스턴스의 `TemporaryArea` (`InventoryUI._temporaryArea`와 같은 오브젝트) |
| | `_itemPrefab` | `PFB_InventoryItem`의 `InventoryItemUI` (Prefab 에셋) |
| | `_itemLayer` | 인스턴스의 `ItemLayer` RectTransform (`_itemLayerGroup` 필드는 Issue #23에서 제거) |
| | `_chestExpansionCount` | 4 (기본값, Scene 값은 확인하지 않음) |
| | `_expansionGuideText` | 인스턴스의 `ExpansionGuideText` Text |
| | `_inventoryUI` | 인스턴스 루트의 `InventoryUI` |
| | `_sellPopup` | Scene의 `SellConfirmPopup` |
| | `_tooltip` | 인스턴스의 `TooltipLayer`의 `ItemTooltipUI` (생성한 Temporary 아이템 UI에 주입) |
| `ItemTooltipUI` (TooltipLayer) | `_inventory` | Scene의 `InventorySystem` 오브젝트의 `Inventory` (`LevelUpPanel._inventory`와 같은 컴포넌트) |
| | `_tooltipRect` | `TooltipLayer/ItemTooltip` RectTransform |
| | `_icon` / `_nameText` / `_typeText` / `_levelText` / `_descriptionText` / `_attackText` / `_effectContainer` / `_equippedEffectText` / `_unequippedBuffText` / `_unequippedDebuffText` / `_sellPriceText` | `ItemTooltip` 하위 (위 Hierarchy 참고) |
| | `_cursorOffset` | (16, -16) |
| `RewardCardUI` (보상 카드 5장, Scene 인스턴스) | `_tooltip` | `TooltipLayer`의 `ItemTooltipUI` (카드마다 Scene 인스턴스 오버라이드) |

- `ItemTooltipUI`는 툴팁 루트(`ItemTooltip`)가 아닌 항상 활성인 별도 오브젝트(`TooltipLayer`)에 둔다. 같은 오브젝트에 두면 첫 표시 때 루트가 처음 켜지며 `Awake`가 실행되어 루트를 다시 끈다 (주의사항 ah).
- `TooltipLayer`는 `PFB_LevelUpPanel` 인스턴스 루트에 Scene에서 추가한 오브젝트 중 마지막(`m_AddedGameObjects` 순서)이라 패널의 다른 요소·아이템 위에 그려진다. `TooltipLayer`에는 Graphic이 없고 `ItemTooltip`은 Image의 Raycast Target 꺼짐·CanvasGroup `blocksRaycasts` false라 툴팁이 아래 카드·아이템의 포인터 이벤트를 가리지 않는다.
- `ItemTooltip`은 Scene 저장 시 활성이며, 패널이 처음 열릴 때 `ItemTooltipUI.Awake`가 끈다. 폭은 320 고정이고 높이는 `ContentSizeFitter`가 정한다.
- `InventoryUI`는 `PFB_LevelUpPanel` 원본이 아니라 Scene에서 인스턴스 루트에 추가한 컴포넌트다. Issue #23에서 `GameScene`을 포함한 Scene 4개의 `InventoryUI`에 칸 색(`_availableCellColor`·`_lockedCellColor`·`_candidateCellColor`)과 `_inventory`(Scene의 `Inventory`)를 연결했다 (작업 시 Unity에서 직접 설정·확인). 나머지 테스트 Scene 3개에는 `ExpansionGuideText`가 없다 (`_expansionGuideText` `null` 허용, 주의사항 ar).
- `ItemLayer`의 CanvasGroup은 남아 있으나 `blocksRaycasts` true이며 코드에서 쓰지 않는다. 보상 선택 중 드래그 차단은 `InventoryItemUI.IsDragEnabled`가 맡는다.
- `ItemLayer`의 테스트 아이템 13개는 `_tooltip`이 비어 있어 툴팁이 뜨지 않는다 (`null`이면 툴팁 없음, 필드 주석).

- `LevelUpPanel`의 참조는 `_itemPrefab`·`_rewardPool`을 빼면 모두 Scene 오브젝트를 가리킨다 (Scene 파일의 stripped 오브젝트·fileID로 확인). `_rewardCards` 배열 순서가 `PFB_RewardCard_01`~`05` 순서와 일치함은 Issue #17 때 Unity MCP 조회로 확인했다.
- `SelectingGroup`·`TemporaryArea`·`ItemLayer`는 `PFB_LevelUpPanel` 인스턴스 루트 아래에 Scene에서 추가한 오브젝트다 (Prefab 원본에는 없다). 세 오브젝트 모두 Scene 저장 시 활성이며, 실행 중 표시는 `ApplyPhase`가 정한다.
- 레이어 : `TemporaryArea`만 Layer 0(Default)이다. 형제 `SelectingGroup`은 Layer 5(UI), `ItemLayer`는 Layer 11(ItemLayer)이다. `TemporaryWarning`도 Layer 0이다.
- `RewardCards`는 Prefab 자식이라 `SelectingGroup` 밖에 있다. 확정 시 `RefreshRewardCards`로 카드를 꺼서 숨긴다 (주의사항 ab).
- `InventoryDragTest`(Scene 루트)와 `ItemLayer` 하위 테스트 아이템 13개는 Scene 저장 시 비활성이다. 보상 화면은 `PFB_InventoryItem`을 런타임 생성해 쓴다.
- `TemporaryWarning`·`SellArea`·`SellConfirmPopup`·`InventorySystem`·`Currency` 등 인벤토리 기능의 나머지 Scene 구성은 이 문서에 기재하지 않는다 (인벤토리 기술 문서 별도 작업). 예외로, `Inventory`와 같은 오브젝트에 `InventoryWeaponSync`가 있다 (`_inventory` = 같은 오브젝트의 `Inventory`, `_loadout` = `PFB_Player` 인스턴스의 `WeaponLoadout`, `_itemPrefab` = `PFB_InventoryItem`, `_itemLayer`·`_inventoryUI`·`_sellPopup`·`_tooltip`은 연결되어 있으나 `LevelUpPanel`과 같은 오브젝트인지는 확인하지 않았다(미검증)).

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

- 텍스트는 모두 레거시 `Text`, `Bithumb Gothic Basic.ttf`다.
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

- 슬롯에는 스크립트가 없다. Issue #23부터 실행 중 색은 `InventoryUI`가 칸 상태(활성 흰색 / 잠김 회청색 / 확장 모드의 후보 노란색)로 덮어쓰며, 슬롯 Image가 레이캐스트를 받아 클릭이 부모 `InventoryUI`로 버블링된다 (위 `인벤토리 API`). 아래 저장 색 설명은 Prefab 저장 값이다.
- 저장 값 : 흰색 16개·틴트 44개 개수와 `Slot_47`(흰색)은 확인했고, 나머지 슬롯 위치별 색은 개별 대조하지 않았다.
- `CompleteButton`의 위치는 레이아웃 이미지에 완료 버튼이 없어 정한 임시 위치다 (브리핑 기준).
- 카드 아이콘·이름·설명의 저장 값은 임시 값이다. 실행 중에는 `RewardCardUI.Bind`가 후보 데이터로 덮어쓴다.
- 인스턴스는 `GameScene`에서 비활성으로 저장한다 (`m_IsActive` 0 오버라이드). Prefab 원본은 활성이다. `LevelUpPanel.Awake`도 `_panelRoot`를 끄므로 비활성 상태가 두 곳에서 정해진다 (주의사항 o).
- 텍스트는 모두 레거시 `Text`이며 폰트는 `Bithumb Gothic Basic.ttf`다 (Issue #39, 아래 `텍스트·폰트`).

### `Assets/Prefabs/UI/PFB_RewardCard.prefab`

```
PFB_RewardCard                (342×158, Image RewardCard Sliced, Button, RewardCardUI)
├─ Icon                       (Image, 상단 가운데 기준 아래 10 · 64×64, 기본 스프라이트 Pistol, RewardCardUI._icon)
├─ NameText                   (Text 크기 20 · 상단 가운데 정렬, 상단 기준 아래 78 · 높이 28, 기본 "권총 Lv.1", RewardCardUI._nameText)
├─ DescriptionText            (Text, RewardCardUI._typeText. 오브젝트 이름은 그대로이며 종류 라벨을 표시)
└─ StatusText                 (Text, RewardCardUI._statusText)
```

- Issue #39 : 카드는 설명 대신 종류(`ItemTooltipUI.GetTypeLabel`)를 `_typeText`에 표시한다. 필드는 `FormerlySerializedAs("_descriptionText")`로 기존 연결을 유지한다. `_statusText`는 `SetSelected`에서 `"선택됨"`/`"클릭하여 선택"`을 표시한다. 두 Text의 크기·위치 값은 Prefab에서 대조하지 않았다 (미검증).

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `RewardCardUI` (루트) | `_button` | 루트 Button |
| | `_background` | 루트 Image |
| | `_icon` / `_nameText` / `_typeText` / `_statusText` | 위 Hierarchy 참고 |
| | `_normalColor` / `_selectedColor` | 흰색 / (1, 0.85, 0.3) |
| | `_tooltip` | Prefab 파일에는 저장된 값이 없다(비어 있음). `GameScene`의 카드 5장은 인스턴스 오버라이드로 연결 |

- 카드별 `RerollButton`(하위 `RerollIcon`·`CurrencyIcon`·`CostText` 포함)은 삭제됐다. 리롤 버튼은 패널에 1개만 있다 (`GameScene`의 `SelectingGroup/RerollButton`).
- 세로 배치 : 아이콘 → 이름 → 종류(`DescriptionText`, `_typeText`) → 상태(`StatusText`) 순으로 위에서 아래.
- 루트 Button의 OnClick 영구 리스너는 없다. `RewardCardUI`가 `OnEnable`에서 코드로 구독한다.
- 카드 크기 342×158은 3장 배치 기준이다. `RewardCards`의 `HorizontalLayoutGroup`이 자식 폭을 제어하므로 실제 폭은 켜진 카드 수로 정해진다. 켜지는 카드 수는 후보 수이며, 풀이 부족하면 회차 카드 수(3/5)보다 적을 수 있다 (3장 약 342, 5장 약 184, 주의사항 p).

### `Assets/Prefabs/UI/PFB_InventoryItem.prefab` (신규)

```
PFB_InventoryItem             (Prefab에서 활성, 72×72, Image(기본 스프라이트 GunCooler, Preserve Aspect), InventoryItemUI)
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| `InventoryItemUI` | `rotateAction` | `InputSystem_Actions` 에셋의 `UI/Rotate` 액션 참조 (fileID `835590030820426825`). Issue #39에서 이 액션에 마우스 우클릭 바인딩이 추가됐다 |
| | `inventory` / `inventoryUI` / `_sellPopup` / `_tooltip` | 비어 있음 (`_tooltip`은 Prefab 파일에 항목 자체가 없다). Prefab은 Scene 오브젝트를 참조할 수 없어 `LevelUpPanel`이 `Initialize` 5인자 오버로드로 주입한다 |

- 회전·드래그 입력 (Issue #39) : 판매 확인 팝업이 열려 있으면(`_sellPopup.IsOpen`) 회전 입력을 무시한다. 드래그 중이면 드래그 중인 아이템 자신이고 `hasGrabbedCell`일 때만 회전 입력(키보드·우클릭)으로 드래그 미리보기를 회전한다. 드래그 중이 아니면 마우스 장치의 회전 입력은 무시하고, 선택 아이템 회전은 키보드 입력만 받는다. 드래그는 좌클릭(`PointerEventData.InputButton.Left`)으로만 시작한다.
- `LevelUpPanel._itemPrefab`으로 쓰이며 확정 시 `ItemLayer` 아래에 생성된다. 스프라이트·크기는 `Initialize`가 데이터에 맞게 바꾼다.
- Prefab 루트를 활성 상태로 저장해야 한다. 비활성으로 저장하면 `Instantiate` 직후 `Awake`가 실행되지 않아 `Initialize`에서 `_image`가 null이 된다 (주의사항 y).
- `rotateAction`의 fileID가 `UI/Rotate` 액션인지는 Prefab 파일만으로 대조하지 않았다. `GameScene`의 기존 테스트 아이템과 같은 fileID다 (미검증).

### 스프라이트 (`Assets/Sprites/UI/`)

| 파일 | 사용처 | 비고 |
|---|---|---|
| `SPR_Skull.png` | `KillIcon` | |
| `SPR_SphereGem.png` | `CurrencyIcon`, `GameScene`의 `SelectingGroup/RerollButton/CurrencyIcon` | 3D 프리팹을 렌더 캡처한 이미지 |
| `SPR_BarFill.png` | `PFB_Player/HealthBarCanvas/HealthFill` | 단색. 기본 `UISprite`는 가장자리가 둥근 투명이라 Filled 비율 0 근처에서 채움이 보이지 않아 교체했다. Issue #17부터 체력바만 사용한다 |

- 그 밖의 UI 스프라이트

| 파일 | 사용처 | 비고 |
|---|---|---|
| `Assets/Sprites/Inventory/InventoryUI_casual/*.png` | `PFB_LevelUpPanel`, `PFB_RewardCard`, `GameScene`의 `RerollButton`·`ConfirmButton` (`StoragePanel`, `InventoryFrame`, `InventorySlot`, `RewardCard`) | Issue #23(커밋 `cb47404`)에서 기존 `Assets/Sprites/Inventory/InventoryUI/`의 같은 이름 스프라이트를 교체했다. 4개 모두 Texture Type Sprite (`textureType: 8`)이며 `RewardCard.png`만 9-Slice 테두리(`spriteBorder` 70 사방)가 있다. 같은 폴더의 `InventoryLayout-1920x1080.png`은 Texture Type Default(`textureType: 0`)인 배치 기준 이미지이며 참조하지 않는다. 교체 대상 참조 위치는 구현 보고 기준이며 개별 GUID 대조는 하지 않았다(미검증) |
| `Assets/Sprites/Inventory/InventoryItems/*.png` | `PFB_RewardCard` (카드 임시 아이콘 5종) | Texture Type Sprite (`textureType: 8`) |
| `Assets/Imports/UI/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Slide Bar/F_UI_BlueBar.png` | `ExperienceFill` (색 흰색) | 외부 에셋 |
| `.../Sprites PNG/Menu Icons/F_UI_MenuIcons_Tile Map 16x16.png`의 `F_UI_MenuIcons_Tile Map 16x16_11` | `GameScene`의 `SelectingGroup/RerollButton/RerollIcon` | 외부 에셋 시트의 하위 스프라이트 |

### 텍스트·폰트

- Issue #39부터 UI 폰트는 `Assets/Fonts/Bithumb Gothic Basic`이다. 레거시 `Text`는 `Bithumb Gothic Basic.ttf`, TMP는 동적 SDF 에셋 `Bithumb Gothic Basic SDF.asset`을 쓴다. 데미지 숫자 폰트는 기존 것을 유지한다.
- 결과 패널 제목(`CLEAR`/`GAME OVER`)과 HUD 레벨 `"LV"`는 영문이고, 결과 통계 항목명은 한글이다.
- `Assets/TextMesh Pro/`는 `.gitignore` 대상이다. 저장소를 새로 받은 뒤에는 Unity 메뉴에서 TMP Essential Resources를 임포트해야 텍스트가 표시된다.

## 예외·주의사항·검증 방법

### 주의사항·알려진 한계

| # | 내용 | 후속 시점 |
|---|---|---|
| a | 게임 흐름 스크립트는 `Assets/Scripts/GameFlow/`에 둔다. `Assets/Scripts/Core/` 폴더의 스크립트가 컴파일 대상에서 빠지는 현상이 있어 `GameFlow` 폴더를 쓴다 (원인 미확인) | 원인 확인 시 |
| b | (해결) `GameSession`, `PlayerHealth`가 `Time.deltaTime`을 직접 쓰던 일시정지 미연동 `shortcut` 주석은 Issue #13에서 삭제됐다. 일시정지·종료 정지 모두 `Time.timeScale`로 구현하므로 `Time.deltaTime` 직접 사용이 의도된 방식이다 | 해결 (#13) |
| c | `PlayerHealth`의 접촉 버퍼는 static 4칸이다. 반경 안 Enemy Collider가 4개를 넘으면 앞 4개만 검사한다. 앞 4개가 모두 사망·비활성이면 살아 있는 Enemy와 접촉해도 피해가 없을 수 있다 | 문제가 보이면 버퍼 확장 |
| d | `_disableOnDeath`에 없는 컴포넌트(`WeaponViewController`, `PlayerAnimator`, `PlayerHealth` 자신 등)는 사망 후에도 동작한다 | 참고 |
| e | (해결) `LevelChanged` 구독자가 없던 문제는 Issue #17에서 `LevelUpPanel`이 구독해 해결됐다. 보상 선택·획득(레벨업·보물 상자)은 `LevelUpPanel`의 `Selecting`/`Organizing` 단계로 구현됐다. 소비 아이템 효과는 Issue #25에서 구현됐다(주의사항 x). 남은 미구현은 레벨업 효과음(주의사항 s)이다 | 해결 (#17, 이슈 미등록 `art/fix-inven-ui`, #25) |
| f | (해결) 재화 표시는 "0" 고정이 아니다. `GameHUD`가 `Currency` 잔액을 표시하고 `Currency.Changed`로 갱신한다 (`INITIAL_CURRENCY_TEXT` 상수는 없다). 판매 외 재화 획득처는 주의사항 w 참고 | 해결 |
| g | 결과 패널에는 다시하기 버튼이 없다. 타이틀 복귀만 있다 | 다시하기 구현 시 |
| h | `_endFreezeDelay` 대기는 `WaitForSeconds`(scaled time)라 대기 중 `timeScale`이 0이면 `Ended`가 발생하지 않는다. Issue #13에서 두 경로를 막았다 : `Pause`·`Resume`은 `IsPlaying`일 때만 동작해 종료 대기 중에는 일시정지되지 않고, 일시정지 중에는 `PlayerHealth`가 접촉 판정을 생략해 사망(`End`)이 일어나지 않는다. 단, 이 생략은 접촉 판정에만 있어 `TakeDamage` 직접 호출로 일시정지 중 사망하면 같은 문제가 남는다. 접촉 외 호출 경로는 `IDamageable`을 통한 `Projectile` 적중뿐이다. Issue #19부터 보스 탄(`PFB_BossBullet`, `_hitMask` Player)이 이 경로로 플레이어에게 피해를 준다 (`Docs/TechDocs/Enemy.md`). 일시정지 중 보스 탄 적중으로 사망하는 상황이 실제로 생기는지는 확인하지 않았다 (미검증) | 플레이어에게 피해를 주는 다른 경로 추가 시 |
| i | Inspector 연결 누락 시 `null` 검사가 없다. `GameSession._playerHealth`, `GameHUD`·`ResultPanel`의 참조, `PlayerHealthBar._camera`, `LevelUpPanel`의 참조, `PauseMenu._levelUpPanel`이 비면 `Awake`·`OnEnable`·`Update`·`LateUpdate`·ESC 입력에서 예외가 난다. `PauseMenu`는 ESC마다 `_levelUpPanel.IsOpen`을 읽으므로 `LevelUpPanel`이 없는 Scene에 두면 ESC 입력 시 예외가 난다 | Scene 구성 시 연결 필수 |
| j | `PlayerExperience`의 레벨 곡선은 임시다 (코드의 `shortcut` 주석) | 레벨 곡선 기획 확정 시 |
| k | (해결) 레벨업 화면은 Issue #17에서 `PauseMenu`의 ESC 무시 조건(`_levelUpPanel.IsOpen`)에 포함됐다. 이후 일시정지를 쓰는 다른 판넬이 추가되면 같은 방식으로 무시 조건에 포함해야 한다. 보물 상자 보상(#19)은 같은 `LevelUpPanel` 화면을 쓰므로 이미 포함된다 | 해결 (#17), 다른 판넬 추가 시 |
| l | 피격 효과를 꺼도 이미 반짝이는 중인 Enemy는 남은 시간이 지난 뒤 원래 재질로 돌아온다 (`EnemyHitFeedback.Update`는 설정과 무관하게 복원) | 참고 |
| m | 데미지 숫자(DamageNumbersPro)가 일시정지(`timeScale` 0) 중에도 애니메이션되는지 확인하지 않았다 (미검증) | 일시정지 중 표시가 문제되면 |
| n | 종료 대기(`_endFreezeDelay`) 중 레벨업하면 `IsPlaying`이 false라 `LevelUpPanel.Open`의 `Pause()`는 무시되고 화면만 열린다. 이어서 `Ended`가 발생하면 결과 패널이 레벨업 화면 위에 겹쳐 표시될 수 있다. 완료 버튼의 `Resume()`도 무시되므로 종료 정지가 풀리지는 않는다 (reviewer 권고, 미반영). 종료 대기 중 보물 상자 획득(`RequestChestReward`)도 같은 경로로 화면만 열린다 (코드 확인, Play 미검증) | 종료 흐름·결과 패널 정리 시 |
| o | `PFB_LevelUpPanel` 인스턴스의 비활성 상태를 Scene 저장(`m_IsActive` 0)과 `LevelUpPanel.Awake`의 `_panelRoot.SetActive(false)` 두 곳이 정한다 (이중 책임, reviewer 권고, 미반영). `ResultPanel`도 같은 구조다 | 참고 |
| p | 카드 5장(보물 상자 회차, `_chestCardCount` 5)이면 `HorizontalLayoutGroup`이 카드 폭을 약 184px((1135 − 54 × 4) / 5)로 줄여 이름·설명 텍스트가 세로로 깨진다. 기본 크기 342×158은 3장 기준이다. Issue #19 이후 5장 표시의 실제 화면은 이 문서 작성 시 확인하지 않았다 (미검증) | 카드 레이아웃 보정 시 |
| q | 레벨업 화면 배경이 반투명(α 0.6)이라 아래 HUD가 보이고, 타이머가 `InventoryFrame` 상단에 겹친다 (1920×1080 렌더 캡처 기준) | 레이아웃 보정 시 |
| r | (해결) 완료 버튼은 `Organizing`이고 Temporary가 비었을 때만 회차를 넘긴다 (버튼 `interactable`과 `HandleCompleteClicked` 양쪽에서 확인). 리롤은 패널의 리롤 버튼 1개로 동작한다. 저장고 표시는 Issue #40부터 `"보관함 {보관 수}/{StorageCapacity}"`이며 `Inventory.Changed`로 갱신된다 (이전 : "Storage N/5", `StorageCountChanged`) | 해결 (이슈 미등록, `art/fix-inven-ui`) |
| s | (일부 해결) 연속 레벨업 시 다음 회차는 `StartRound`로 새 후보·선택 상태·단계 UI를 다시 만든다. 레벨업 효과음은 여전히 없다. `Docs/Spec/GameFlow.md`의 `연속 레벨업`(회차마다 효과음·UI·후보 재생성) 중 효과음만 충족하지 않는다. 마지막 회차 전 재개하지 않는 조건은 충족한다 | 효과음 구현 시 |
| t | `Docs/Spec/GameFlow.md`의 `게임 상태`는 "레벨업·인벤토리 정리"를 별도 상태로 구분하도록 요구하지만, 현재 구현은 `SessionState`를 바꾸지 않고 `Time.timeScale`과 `LevelUpPanel.IsOpen`으로만 표현한다. 별도 상태 도입 여부는 추후 확정 필요 | 레벨업 보상·보물상자 구현 시 확정 |
| u | 후보 풀에서 뽑을 수 있는 아이템 수가 카드 수 이하이면 리롤해도 같은 아이템 조합(순서만 다를 수 있음)이 나오지만 재화는 차감된다. 새 후보가 0개일 때만 차감하지 않는다 | 풀 확장 또는 리롤 차단 조건 검토 시 |
| v | Temporary 영역을 `LevelUpPanel._temporaryArea`(배치 좌표 계산)와 `InventoryUI._temporaryArea`(보정·표시 좌표)가 각각 참조한다. 두 필드에 같은 오브젝트를 연결해야 한다. 다르면 확정 시 배치 위치가 어긋날 수 있다 (코드 기준 추정, reviewer 권고, 미반영) | 참조 단일화 시 |
| w | 특수 재화 획득처는 아이템 판매뿐이다. 보스·엘리트 처치 시 지급은 검토 중이다 | 재화 지급처 확정 시 |
| x | (해결) 소비 아이템 선택 시 효과가 적용되지 않던 문제는 Issue #25에서 `LevelUpPanel.ApplyExpendable`(체력 회복·칸 확장)로 해결됐다. Temporary에는 여전히 등록하지 않는다 | 해결 (#25) |
| y | `PFB_InventoryItem`을 비활성으로 저장하면 `Instantiate` 직후 `Awake`가 실행되지 않아 `InventoryItemUI.Initialize`(5인자)에서 `_image`가 null이라 `NullReferenceException`이 난다. Prefab 루트는 활성으로 저장해야 한다 | Prefab 수정 시 |
| z | 판매한 아이템의 UI는 `SellConfirmPopup`이 `SetActive(false)`로 끄기만 하고 파괴하지 않는다. 판매가 반복되면 비활성 오브젝트가 `ItemLayer`에 쌓인다 | 아이템 UI 수명 관리 정리 시 |
| aa | 확정(`HandleConfirmClicked`) 시점에는 `ApplyPhase` 전이라 Temporary 영역이 비활성인 상태에서 `rect`·`TransformPoint`를 읽는다. Temporary 영역이 중앙 앵커·고정 크기라 레이아웃 재계산의 영향이 없다고 판단했다 (reviewer 권고, 미반영, 미검증) | 영역 앵커를 stretch로 바꿀 때 |
| ab | `RewardCards`는 `PFB_LevelUpPanel`의 Prefab 자식이라 `SelectingGroup` 밖에 있다. 그룹 비활성화로 숨겨지지 않으므로 확정 시 후보를 비우고 `RefreshRewardCards`로 카드를 모두 끈다. 카드 표시를 그룹 기준으로 바꾸려면 Prefab 구조 변경이 필요하다 | 레이아웃 정리 시 |
| ac | (해결) `Selecting` 단계에서 아이템 툴팁이 뜨지 않던 문제. Issue #23에서 `_itemLayerGroup`을 제거하고 드래그만 `InventoryItemUI.IsDragEnabled`로 막아 선택 중에도 아이템 hover 툴팁이 뜬다 | 해결 (#23) |
| ad | (해결) 툴팁의 무기 공격력이 레벨 계산 없는 기본값이던 문제. 레벨 보너스 줄과 `GetWeaponStats` 기준 최종 공격력 범위를 표시한다. 기본 공격력 줄은 여전히 `ProjectileDamage` 원본이다 | 해결 (`art/fix-inven-ui`) |
| ae | 해제 버프와 해제 디버프가 같은 접두어 `"해제 시:"`(`UNEQUIPPED_FORMAT`)를 쓰고 텍스트 색(`GameScene` 값 초록/빨강)으로만 구분한다. 종류 표시는 `ItemType`이 무기·패시브·소비가 아니면 모두 `"고유"`(`default`)다. 보상 Lv.1 규칙이 `ItemTooltipUI.REWARD_LEVEL`과 `RewardCardUI.NAME_FORMAT` 두 곳에 있다 (reviewer 권고, 미반영). Issue #25에서 레벨 표기 생략 기준은 두 곳 모두 `MaxLevel > 1`로 맞췄지만 규칙은 여전히 두 곳에 있다 | 툴팁 문구·보상 레벨 규칙 정리 시 |
| af | 장착 효과·해제 버프·해제 디버프 텍스트는 표시 전용이다. 실제 효과는 `CharacterDefinition` 특성·고유 효과 필드로 구현되었으나 텍스트와 수치는 서로 연결되지 않아 따로 맞춰야 한다. 전투 조끼 +2칸 확장·회수는 Issue #40에서 구현되었다 (위 `조끼 전용 칸`) | 아이템 효과 문구 정리 시 |
| ai | `PlayerHealthBar`는 `Damaged`·`Healed`와 `Start`에서만 갱신한다. 고유 아이템 탈착으로 최대 체력만 바뀌면 다음 피격·회복 전까지 체력바 비율이 갱신되지 않는다 (코드 기준, Play 미검증) | 체력바 갱신 경로 정리 시 |
| aj | 최대 체력 배율이 다시 1로 돌아와도(고유 아이템 재장착) 현재 체력은 회복되지 않는다 (`Health.SetMaxHealth`가 증가 시 유지) | 기획 확정 시 |
| ak | `PlayerHealth._contactRadius`(0.45)·`PlayerExperience._collectRadius`(0.8)의 필드 주석은 이전 값(0.6·0.4)의 근거를 설명한다. Prefab 값만 바뀌어 주석과 값이 다르다 | 주석 정리 시 |
| al | `WeaponReloadGauge`는 재장전 중인 첫 무기만 표시한다 (코드의 `shortcut` 주석). 미니건 2개를 장착해 동시에 재장전하면 목록 앞 무기의 진행률만 보인다 | 무기별 게이지가 필요해지면 |
| am | 이동속도 보너스 상한 0.5는 코드 주석상 테스트 상한이며 하한은 없다(`Max(0, …)`로 배율 0 미만만 막음). 패시브 Lv.3 수치(탄띠 이동속도 0.05는 Issue #40 확정값으로 제외)·인접·레벨 상수는 모두 임시값이다. 캐릭터 특성·고유 효과 수치는 Issue #40에서 확정 수치로 맞췄으며 플레이 테스트 후 조절할 수 있다 | 밸런스 확정 시 |
| an | 툴팁의 인접 탄퍼짐 줄은 인접 효과만(상한 0.7 적용) 표시하고, 무기 Lv.3 탄퍼짐 감소는 별도 줄이 없다. 실제 적용 감소율(합산 후 상한)과 표시가 다를 수 있다 | 툴팁 문구 정리 시 |
| ao | 확장 안내 문구(`ExpansionGuideText`, 프레임 상단 중앙)가 HUD 타이머와 겹친다 (주의사항 q와 같은 위치) | 레이아웃 조정 시 |
| ap | `InventoryItemUI.IsDragEnabled`는 `static`이다. Domain Reload를 끈 Play 모드에서 보상 선택 중 Play를 멈추면 false가 다음 Play에 남을 수 있다 (코드 기준 추정, 미검증) | Domain Reload 설정 변경 시 |
| aq | `InventoryUI`는 `gridRect` 자식이 정확히 60개(10×6 Slot)라는 전제로 칸 Image를 모은다. 60개가 아니면 `Awake`에서 `LogError` 후 수집을 건너뛰고, 곧이은 첫 `OnEnable`의 칸 색 갱신부터 예외가 난다 (코드 기준) | Grid 구조 변경 시 |
| ar | `GameScene` 외 테스트 Scene 3개에는 `ExpansionGuideText`가 배치되지 않았다. `_expansionGuideText`가 `null`이면 안내 없이 확장만 동작한다 | 테스트 Scene 정리 시 |
| as | `Inventory`의 두 전제 (Issue #40, reviewer 권고, 미반영). (1) `Character` setter는 게임 시작 시 1회 설정 전제다. 재설정 시 조끼 전용 칸 상태가 맞지 않을 수 있다. (2) `HasUniqueItemInstance`는 Temporary를 확인하지 않는 `shortcut`이며 고유 아이템이 1개뿐이라는 전제다 (위 `캐릭터 특성·고유 아이템 장착/해제 효과`) | 캐릭터 재설정 경로 추가 시 / Temporary 아이템 목록이 생기거나 고유 아이템이 2개 이상 가능해질 때 |
| at | 보관함 6·7번째 슬롯이나 예약 중 조끼 전용 칸에 아이템이 있으면 고유 아이템을 그리드 밖으로 옮기거나 판매·합성할 수 없다. `Inventory`에는 거부 이유를 알리는 안내가 없다 (위 `고유 아이템 해제 거부`) | 안내 UI 추가 시 |
| ag | `ItemTooltipUI`에는 Inspector 참조 `null` 방어가 없다. `_inventory`·`_tooltipRect`·텍스트·이미지 참조가 비면 `Awake`·`Show`에서 `NullReferenceException`이 난다. 호출 측(`RewardCardUI`·`InventoryItemUI`)은 `_tooltip`이 비면 툴팁만 생략한다 | Scene 구성 시 연결 필수 |
| ah | `ItemTooltipUI`를 툴팁 루트(`_tooltipRect`)와 같은 오브젝트에 두면, 루트가 비활성인 채 첫 `Show`가 루트를 켤 때 `Awake`가 처음 실행되어 루트를 다시 끈다. 항상 활성인 별도 오브젝트(`TooltipLayer`)에 두어야 한다. `_tooltipRect`의 필드 주석("자신과 같아도 됨")과 `OnDisable`의 같은 오브젝트 분기는 이 배치를 허용하는 것처럼 쓰여 있다 (코드 기준 추정, 같은 오브젝트 배치는 실행 확인하지 않음) | 필드 주석 정리 시 |

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
- 보상 선택·리롤·Temporary 정리 (이슈 미등록, `art/fix-inven-ui`)
    - `RewardCandidateGeneratorTests` (6개) : null·Essential·판매 불가 제외, 목록 내 Id 중복 없음, 풀이 카드 수보다 적으면 가능한 수만 반환, 새 아이템이 충분하면 직전 후보 전부 제외, 부족하면 직전 후보로 채움, 가중치 0 종류 미등장.
    - 테스트 실행 결과 : 컴파일 에러 없음, EditMode 233/233 통과 (developer 보고 기준).
    - Play 모드 : 사용자 검수 승인으로 전달받았다. 확인한 세부 항목은 이 문서 작성 입력에 포함되지 않았다.
    - `LevelUpPanel`의 선택·리롤·확정·완료 흐름을 검증하는 EditMode 테스트는 없다. 주의사항 aa는 미검증.
- 합성·레벨 성장·인접 효과·고유 효과·플레이어 스탯·재장전 게이지 (이슈 미등록, `art/fix-inven-ui`)
    - EditMode 테스트 (메서드 존재만 확인) : `InventoryTests`(`TryMerge` 성공·영역 간 합성·최대 레벨·다른 Id·다른 레벨·자기 자신·MaxLevel 1·Location None·null 실패, `GetPassiveAdjacencyBonus`, `IsLevel3EffectActive` 무기·배치 패시브·보관 패시브, `GetAdjacencyBonus` 상하좌우·대각선 제외·여러 셀 1회·두 패시브 합산·회전·영역 이탈·판매·고유 아이템 제외, Lv.3 도달 합성, `GetEffectiveAdjacencyBonus` 상한), `InventoryWeaponSyncTests`(`Changed` 1회·실패 시 미발생, `GetWeaponStats`, 동기화), `UniqueItemEffectTests`(다른 캐릭터·null 캐릭터 미적용, 헤비거너 정확도 + 인접 상한, Lv.3 보너스 Lv.2 비활성·Lv.3~5 동일·합산·보관 시 제거, `StatSync` 이동속도 상한 0.5·헤비거너 이동속도·생존자 최대 체력 clamp·경험치 소수 누적·수집 반경 배율), `WeaponInstanceTests.ReloadProgress_StartHalfFinished_ReturnsZeroHalfZero`.
    - 테스트 실행 결과·Play 모드 확인 결과는 이 문서 작성 입력에 포함되지 않았다 (미검증). 주의사항 ai·aj는 Play로 확인하지 않았다.
- Issue #23 (보물 상자 확장 선택·선택 중 툴팁·casual 스프라이트)
    - Play 확인 절차 : `LevelUpPanel.RequestChestReward` 호출 → 카드 3장 선택·선택 완료 → `Organizing`에서 후보 칸 16칸(초기 중앙 4×4 활성 기준) 노란색·안내 문구 "확장할 4칸을 선택하세요!!" 표시 → 후보 칸 클릭마다 문구 수 4 → 0 감소, 0이면 문구 숨김·확장 모드 해제 → 해제한 칸에 아이템 배치 가능 → Temporary 0개에서 완료.
    - EditMode 테스트 (`InventoryTests`, 3개 추가) : `CountExpansionCandidates_InitialGrid_Is16`, `TryUnlockCell_Candidate_UnlocksAndRecalculates`(해제 후 새 외곽 후보·해제 칸 배치), `TryUnlockCell_NotCandidate_Fails`. 칸 클릭·안내 문구·완료 버튼 흐름은 자동 테스트가 없고 Play로만 확인한다.
    - 테스트 실행 결과 : 컴파일 에러 없음, EditMode 349/349 통과.
    - Play 모드 : AI agent가 `GameScene`에서 위 절차대로 검증 완료 (포인터 이벤트를 코드로 전송, 아이템 배치·보관은 `TryPlace`·`TryStore` 직접 호출). 보상 선택 중 툴팁 표시·드래그 취소, 후보가 아닌 칸·4칸 이후 클릭 무시도 확인. 테스트 Scene 3개의 `InventoryUI` 연결은 작업 시 Unity에서 직접 설정·확인했다.
- Issue #40 (캐릭터 특성·고유 아이템 효과 구조 정리)
    - EditMode 테스트 : `InventoryTests`, `UniqueItemEffectTests`, `WeaponInstanceTests`가 변경되었다. 개별 테스트 항목은 이 문서 작성 시 대조하지 않았다.
    - 테스트 실행 결과 : EditMode 356개 중 355개 통과. 실패 1개는 이번 변경과 무관한 `ResultPanelTests` (reviewer 보고 기준).
    - Play 모드 확인 항목(보관함 슬롯 표시·숨김, 조끼 전용 칸 열림·잠김, 해제 거부 시 UI 동작, 툴팁 특성 줄)은 이 문서 작성 입력에 없다 (미검증).

## 관련 Issue/PR

- Issue #8 (몬스터 프로토타입. 게임 세션·HUD·플레이어 체력·경험치 포함)
- PR : 미정
- Issue #10 (TitleScene·캐릭터 선택. 결과 패널의 타이틀 복귀 대상, `Docs/TechDocs/Character.md`)
- Issue #13 (타이틀 메뉴·설정·일시정지). PR : 미정
- Issue #17 (레벨업 일시정지·레벨업 화면 레이아웃). PR : 미정
- Issue #19 (몬스터 최종 M06~M12. 보물 상자 보상 요청, 회차 종류별 카드 수, 최종 보스 처치 클리어. `Docs/TechDocs/Enemy.md`). PR : 미정
- 이슈 미등록 (브랜치 `art/fix-inven-ui`. 레벨업·보물 상자 보상 선택·리롤·Temporary 정리, 툴팁, 합성·레벨 성장·인접 효과·패시브 Lv.3·고유 아이템 효과, `PlayerStatSync`, 재장전 게이지, 피격·젬 획득 반경 조정). PR : 미정
- Issue #23 (브랜치 `feat/boss-inventory-expansion`. 보물 상자 보상 인벤토리 4칸 확장, 보상 선택 중 툴팁, `InventoryUI_casual` 스프라이트). PR : 미정
- Issue #40 (브랜치 `feat/character-trait-essential-effect`. 캐릭터 특성·고유 아이템 효과 두 층 구조, 보관함 한도 확장, 조끼 전용 칸, 해제 거부). PR : 미정
