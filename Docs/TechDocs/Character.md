# 캐릭터 선택·타이틀 (Character)

> 근거 : `Assets/Scripts/Character/CharacterDefinition.cs`, `Assets/Scripts/Character/CharacterSelection.cs`, `Assets/Scripts/UI/CharacterSelectUI.cs`, `Assets/Scripts/UI/TitleMenu.cs`, `Assets/Scripts/Weapon/Weapon/WeaponLoadout.cs`, `Assets/Scripts/Weapon/Weapon/WeaponViewController.cs`, `Assets/Tests/Editor/WeaponLoadoutTests.cs`, `Assets/Data/Character/*.asset`, `Assets/Sprites/Character/*.png.meta`, `Assets/Scenes/TitleScene.unity`, `Assets/Prefabs/Player/PFB_Player.prefab`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/EditorSettings.asset`, `ProjectSettings/ProjectSettings.asset`
>
> 설계 원본 : `Docs/Spec/GDD.md` §14의 1 (캐릭터 선택), `Docs/Spec/GameFlow.md` (이 문서는 현재 구현 상태만 기술한다)

## 목적 / 기능 범위

- `TitleScene`에서 시작 버튼을 누르면 캐릭터 선택 패널을 연다.
- 캐릭터 3종 중 하나를 고르면 초상·이름·시작 무기 이름·설명을 표시하고, 선택한 뒤에만 게임 시작 버튼이 눌린다.
- 게임 시작 시 선택 캐릭터를 static 값으로 저장하고 `GameScene`을 로드한다. `GameScene`의 `WeaponLoadout`이 그 캐릭터의 시작 무기를 장착하고, `WeaponViewController`가 그 캐릭터의 생김새(Body·Head)를 켠다.
- 타이틀을 거치지 않고 `GameScene`을 단독 실행하면 `WeaponLoadout._defaultCharacter`(카우보이)를 쓴다.

| 범위 | 내용 |
|---|---|
| 포함 | Issue #10 : 캐릭터 SO, 선택 결과 전달(static), 타이틀·캐릭터 선택 UI, 캐릭터 기준 시작 무기·생김새, Build Settings |
| 포함 | Issue #13 : 타이틀 메뉴(`TitleMenu`, 설정·게임 종료 버튼, 종료 확인창), 1920×1080 고정 해상도 |
| 제외 (후속) | 캐릭터 필수(고정) 아이템, 고유 특성 (추후 이슈), 한글 TMP 폰트, 패널 닫기·타이틀 복귀 |

## 주요 클래스와 역할

| 클래스 | 책임 | 의존성 |
|---|---|---|
| `CharacterDefinition` (ScriptableObject) | 캐릭터별 선택 화면 표시 정보와 시작 무기. 필드 `_displayName`, `_description`(TextArea), `_portrait`(Sprite), `_startWeaponName`(선택 화면 표시용 문자열), `_startWeapon`(시작 시 장착하는 고유 무기). 읽기 전용 프로퍼티 `DisplayName`, `Description`, `Portrait`, `StartWeaponName`, `StartWeapon`만 노출. 생성 메뉴 `Data/Character Definition` | `WeaponDefinition` |
| `CharacterSelection` (static) | 타이틀에서 고른 캐릭터를 `GameScene`으로 전달. static 프로퍼티 `Selected` (미선택이면 `null`) | `CharacterDefinition` |
| `CharacterSelectUI` (MonoBehaviour) | 캐릭터 선택 패널. 슬롯 클릭 시 선택 표시·상세 텍스트 갱신, 선택 후에만 시작 버튼 활성, 시작 시 `CharacterSelection.Selected` 저장 후 `"GameScene"` 로드 | `CharacterDefinition`, `CharacterSelection`, `UnityEngine.UI`(`Button`, `Image`, `Text`), `SceneManager` |
| `CharacterSelectUI.CharacterSlot` (private, Serializable) | 슬롯 하나의 `_character`, `_button`, `_portrait`(Image), `_selectionMark`(GameObject) | `CharacterDefinition` |
| `WeaponLoadout` (기존, 변경) | `Character`(`CharacterSelection.Selected`가 있으면 그것, 없으면 `_defaultCharacter`), `SignatureWeapon`(= `Character.StartWeapon`). 둘 다 읽을 때마다 계산 | `CharacterDefinition`, `CharacterSelection` |
| `WeaponViewController` (기존, 변경) | `ApplyAppearance`에서 `_loadout.Character`와 같은 `_appearances` 항목의 Body·Head만 켬 | `WeaponLoadout`, `CharacterDefinition` |
| `TitleMenu` (MonoBehaviour, Issue #13) | 타이틀 메뉴의 설정 판넬·게임 종료 확인창 열고 닫기. 확인창 예 → `Application.Quit()`. ESC(`UI/Cancel`)로 열린 창을 하나씩 닫음. 입력 액션을 직접 `Enable`/`Disable` | `Button`, `InputActionReference`, `Application` |

- `WeaponLoadout`·`WeaponViewController`의 상세는 `Docs/TechDocs/Weapon.md` 참고.
- `StartButton`은 `TitleMenu`와 무관하게 Button OnClick 영구 리스너로 `CharacterSelectPanel.SetActive(true)`를 호출한다 (Issue #13 이후에도 유지).
- 설정 판넬(`PFB_SettingsPanel`, `SettingsPanel`)과 `GameSettings`는 `Docs/TechDocs/GameFlow.md` 참고.

## API / 이벤트 / 데이터 계약

이벤트는 없다. 선택 결과는 static 프로퍼티 하나로 전달한다.

### 선택 결과 (`CharacterSelection.Selected`)

| 구분 | 위치 | 내용 |
|---|---|---|
| 쓰기 | `CharacterSelectUI.StartGame` | 선택한 `CharacterDefinition`을 대입한 직후 `SceneManager.LoadScene("GameScene")` |
| 쓰기 | `WeaponLoadoutTests.SetUp` | 테스트 간 영향을 막기 위해 `null`로 비움 |
| 읽기 | `WeaponLoadout.Character` | `null`이 아니면 그 값, `null`이면 `_defaultCharacter` |

- 값은 static이므로 Scene 로드와 관계없이 유지된다. 에디터에서 Play 모드를 다시 시작할 때 비워지는지는 Domain Reload 설정에 달려 있다 (주의사항 b).

### 캐릭터 선택 패널 (`CharacterSelectUI`)

| 시점 | 동작 |
|---|---|
| `Awake` | 슬롯 수만큼 클릭 핸들러(`() => Select(slotIndex)`)를 만들어 배열에 보관한다. 람다 리스너를 `OnDisable`에서 같은 참조로 해제하기 위해서다 |
| `OnEnable` | 슬롯 버튼마다 보관한 핸들러를, 시작 버튼에 `StartGame`을 `onClick.AddListener`로 구독한다 |
| `OnDisable` | 같은 핸들러·`StartGame`을 `RemoveListener`로 해제한다 |
| `Start` | 슬롯마다 `Portrait.sprite = Character.Portrait`, 선택 표시(`SelectionMark`) 끔. 이름·시작 무기·설명 텍스트를 빈 문자열로, 시작 버튼 `interactable = false` |
| `Select(slotIndex)` | 해당 슬롯의 선택 표시만 켜고 나머지는 끔. `_selected`에 슬롯 캐릭터 저장, 텍스트에 `DisplayName`·`StartWeaponName`·`Description` 표시, 시작 버튼 `interactable = true` |
| `StartGame` | `CharacterSelection.Selected = _selected` 후 `"GameScene"` 로드 (`GAME_SCENE_NAME` 상수) |

- `CharacterSelectPanel`은 Scene에 비활성으로 저장되어 있다. 첫 활성화 때 `Awake` → `OnEnable` → `Start` 순으로 실행된다 (같은 컴포넌트 안의 Unity 생명주기 순서).
- 시작 버튼은 Scene에 `m_Interactable: 1`로 저장되어 있지만 `Start`에서 `false`로 바뀐다. 슬롯을 고르기 전에는 `StartGame`이 호출되지 않으므로 `_selected`가 `null`인 채 저장되는 경로는 없다.

### 타이틀 메뉴 (`TitleMenu`)

| 시점·입력 | 동작 |
|---|---|
| `OnEnable` | `SettingsButton` → 판넬 켬, 판넬 닫기 버튼 → 판넬 끔, `QuitButton` → 확인창 켬, 예 → `Application.Quit()`, 아니오 → 확인창 끔을 `onClick.AddListener`로 구독. `_cancelAction.action.performed` 구독 후 액션 `Enable` |
| `OnDisable` | 위 구독을 모두 해제하고 액션 `Disable` |
| ESC | 확인창이 켜져 있으면 확인창만 닫음 → 아니면 설정 판넬이 켜져 있으면 판넬만 닫음 → 둘 다 꺼져 있으면 무시 |

- `_cancelAction`은 `InputSystem_Actions`의 `UI/Cancel` 참조다 (필드 주석). Scene마다 이 액션을 쓰는 컴포넌트가 메뉴 스크립트 하나뿐이라 직접 `Enable`/`Disable`한다 (코드 주석). `EventSystem`의 UI 입력 모듈과는 별개다.
- ESC는 `CharacterSelectPanel`을 닫지 않는다 (주의사항 d).
- 에디터에서는 `Application.Quit`이 무시되어 Play 모드가 유지된다 (코드 주석).
- 확인창은 별도 스크립트 없이 `TitleMenu`가 버튼을 처리한다. 설정 판넬을 닫을 때 `SettingsPanel.OnDisable`이 설정을 저장한다 (`Docs/TechDocs/GameFlow.md`).

### 캐릭터 데이터 (`Assets/Data/Character/`)

| SO | `_displayName` | `_startWeaponName` | `_startWeapon` | `_portrait` |
|---|---|---|---|---|
| `SO_Cowboy` | 카우보이 | 권총 | `SO_Pistol` | `SPR_Cowboy` |
| `SO_Survivor` | 겁쟁이 생존자 | 샷건 | `SO_Shotgun` | `SPR_Survivor` |
| `SO_HeavyGunner` | 헤비거너 | 미니건 | `SO_Minigun` | `SPR_HeavyGunner` |

`_description`

| SO | 값 |
|---|---|
| `SO_Cowboy` | 모자는 남자의 자존심.(줄바꿈)빠르게 연사하는 권총으로 적을 한 발씩 정확히 쓰러뜨린다. |
| `SO_Survivor` | 몸을 지키기 위해 전투 조끼를 챙겼다.(줄바꿈)가까이 다가온 적을 샷건의 넓은 산탄으로 밀어낸다. |
| `SO_HeavyGunner` | 무거운 무기를 능숙하게 다룬다.(줄바꿈)미니건으로 쉬지 않고 탄을 쏟아붓는다. |

- 표시 문자열은 에셋의 유니코드 이스케이프를 풀어 옮긴 값이다.
- `_startWeapon`·`_portrait`는 에셋의 GUID를 각 `.meta`와 대조해 확인했다.
- 생김새(Body·Head) 대응은 `PFB_Player`의 `WeaponViewController._appearances`가 정한다 (`SO_Cowboy` = `Body10`·`Head10`, `SO_Survivor` = `Body06`·`Head06`, `SO_HeavyGunner` = `Body08`·`Head08`, `Docs/TechDocs/Weapon.md` 참고).

### 초상 이미지 (`Assets/Sprites/Character/SPR_Cowboy|Survivor|HeavyGunner.png`)

- Import 설정 : Texture Type `Sprite (2D and UI)`(`textureType: 8`), Sprite Mode `Single`(`spriteMode: 1`), Pixels Per Unit 100.
- `PFB_Player` 모델을 캡처한 임시 초상이다 (메인 세션 보고 기준, 이미지 제작 과정은 확인하지 않음).

## 호출 순서 또는 데이터 흐름

```
[TitleScene]
StartButton 클릭
  └─ OnClick 영구 리스너 → CharacterSelectPanel.SetActive(true)
       (첫 활성화) CharacterSelectUI.Awake → OnEnable(리스너 구독) → Start(초상·선택 표시·텍스트·시작 버튼 초기화)

Slot_0n 클릭
  └─ Select(n) → 선택 표시 전환, 상세 텍스트 표시, GameStartButton.interactable = true

GameStartButton 클릭
  └─ StartGame
       ├─ CharacterSelection.Selected = 선택 캐릭터
       └─ SceneManager.LoadScene("GameScene")
            (TitleScene 언로드 → CharacterSelectUI.OnDisable에서 리스너 해제)

[GameScene]
WeaponLoadout.Awake
  └─ Equip(SignatureWeapon)        SignatureWeapon = Character.StartWeapon
                                   Character = CharacterSelection.Selected, 없으면 _defaultCharacter(SO_Cowboy)
WeaponViewController.Start
  └─ ApplyAppearance()             _loadout.Character와 같은 _appearances 항목의 Body·Head만 켬
TempLoadoutTester.Start (임시)
  └─ SO_Shotgun, SO_Minigun 추가 장착 (선택 캐릭터와 무관)
```

- `GameScene` 이후의 무기 흐름(호출 순서·실행 순서 미지정)은 `Docs/TechDocs/Weapon.md`의 `호출 순서 또는 데이터 흐름` 참고.
- `TitleScene` 언로드 시 `OnDisable`이 호출되는지는 Unity의 Scene 언로드 동작 기준 설명이며, 이 문서 작성 시 Play 모드로 확인하지 않았다 (미검증).

## Scene·Prefab·Inspector 사용 방법

### `Assets/Scenes/TitleScene.unity`

```
Cameras
└─ MainCamera
Lights
└─ DirectionalLight
UI
├─ TitleCanvas                   (Canvas Screen Space - Overlay, CanvasScaler 기준 해상도 1920×1080, GraphicRaycaster, TitleMenu)
│  ├─ TitleText                  (Text "Pack and Slash")
│  ├─ StartButton                (Button, OnClick → CharacterSelectPanel.SetActive(true), 위치 (-480, 0))
│  │  └─ Label                   (Text "게임 시작")
│  ├─ SettingsButton             (Button, TitleMenu._settingsButton, 위치 (-480, -140))
│  │  └─ Label                   (Text "설정")
│  ├─ QuitButton                 (Button, TitleMenu._quitButton, 위치 (-480, -280))
│  │  └─ Label                   (Text "게임 종료")
│  ├─ CharacterSelectPanel       (비활성으로 저장, Image, CharacterSelectUI)
│  │  ├─ HeaderText              (Text "캐릭터 선택")
│  │  ├─ Slot_01                 (Button, Image)
│  │  │  ├─ SelectionMark
│  │  │  └─ Portrait             (Image, Preserve Aspect, Raycast Target 끔)
│  │  ├─ Slot_02                 (Slot_01과 같은 구성)
│  │  ├─ Slot_03                 (Slot_01과 같은 구성)
│  │  ├─ DetailPanel             (Image)
│  │  │  ├─ NameText             (Text)
│  │  │  ├─ WeaponLabel          (Text "시작 무기")
│  │  │  ├─ WeaponText           (Text)
│  │  │  └─ DescriptionText      (Text)
│  │  └─ GameStartButton         (Button, OnClick 영구 리스너 없음)
│  │     └─ Label                (Text "게임 시작")
│  ├─ PFB_SettingsPanel          (PFB_SettingsPanel 인스턴스, 비활성으로 저장, TitleMenu._settingsPanel. 구성은 GameFlow.md)
│  └─ QuitDialog                 (비활성으로 저장, Image 전체 화면 검정 α 0.6, TitleMenu._quitDialog)
│     └─ PanelBox                (900×360)
│        ├─ MessageText          (Text "정말 게임을 종료하시겠습니까?")
│        ├─ YesButton            (TitleMenu._quitYesButton)
│        │  └─ Label             (Text "예")
│        └─ NoButton             (TitleMenu._quitNoButton)
│           └─ Label             (Text "아니오")
└─ EventSystem                   (EventSystem, InputSystemUIInputModule)
```

- 모든 Text는 레거시 `UnityEngine.UI.Text`이며 폰트는 Unity 내장 폰트(`fileID: 10102`)다.
- `StartButton`·`SettingsButton`·`QuitButton`은 화면 중앙 기준 왼쪽 절반(x -480)에 위에서 아래로 놓이며 크기는 모두 400×110이다. `TitleCanvas` 자식 순서는 `TitleText` → `StartButton` → `SettingsButton` → `QuitButton` → `CharacterSelectPanel` → `PFB_SettingsPanel` → `QuitDialog`다 (Transform 순서로 확인).
- `TitleScene`의 `PFB_SettingsPanel` 인스턴스에는 추가 오브젝트가 없다. 타이틀 복귀 버튼(`ReturnTitleButton`)은 `GameScene` 인스턴스에만 있다.
- `EventSystem`의 `InputSystemUIInputModule`은 액션 에셋 GUID `ca9f5fa95ffab41fb9a615ab714db018`을 참조한다. 프로젝트의 `Assets/InputSystem_Actions.inputactions`(GUID `052faaac586de48259a63d0c4782560b`)와 다른 에셋이며, Input System 패키지의 `InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions`다 (`.meta` 파일의 GUID로 확인).

`CharacterSelectUI` (`CharacterSelectPanel`)

| 필드 | 연결 값 |
|---|---|
| `_slots[0]` | `_character` = `SO_Cowboy`, `_button` = `Slot_01`, `_portrait` = `Slot_01/Portrait`, `_selectionMark` = `Slot_01/SelectionMark` |
| `_slots[1]` | `_character` = `SO_Survivor`, `_button` = `Slot_02`, `_portrait` = `Slot_02/Portrait`, `_selectionMark` = `Slot_02/SelectionMark` |
| `_slots[2]` | `_character` = `SO_HeavyGunner`, `_button` = `Slot_03`, `_portrait` = `Slot_03/Portrait`, `_selectionMark` = `Slot_03/SelectionMark` |
| `_nameText` | `DetailPanel/NameText` |
| `_weaponText` | `DetailPanel/WeaponText` |
| `_descriptionText` | `DetailPanel/DescriptionText` |
| `_startButton` | `GameStartButton` |

- Scene의 `Portrait` Image에는 Sprite가 비어 있다. `Start`에서 슬롯 캐릭터의 `Portrait`로 채운다.
- Scene의 `SelectionMark`는 활성으로 저장되어 있다. `Start`에서 모두 끈다.

`TitleMenu` (`TitleCanvas`)

| 필드 | 연결 값 |
|---|---|
| `_settingsButton` | `SettingsButton` |
| `_settingsPanel` | `PFB_SettingsPanel` (인스턴스 루트 GameObject) |
| `_settingsCloseButton` | 인스턴스의 `PanelBox/CloseButton` Button |
| `_quitButton` | `QuitButton` |
| `_quitDialog` | `QuitDialog` |
| `_quitYesButton` / `_quitNoButton` | `QuitDialog/PanelBox/YesButton` / `NoButton` |
| `_cancelAction` | `InputSystem_Actions` 에셋(GUID `052faaac586de48259a63d0c4782560b`)의 액션 참조 (`fileID 7727032971491509709`, `GameScene`의 `PauseMenu`와 같은 값) |

- `_cancelAction`의 fileID가 `UI/Cancel` 액션인지는 Scene 파일만으로 대조할 수 없었다. 필드 주석과 브리핑은 `UI/Cancel`이다 (미검증).

### `Assets/Prefabs/Player/PFB_Player.prefab`

| 컴포넌트 | 필드 | 연결 값 |
|---|---|---|
| `WeaponLoadout` (Weapon) | `_defaultCharacter` | `SO_Cowboy` |
| `WeaponViewController` (Weapon) | `_appearances` | 캐릭터 SO 3종 → Body·Head (`Docs/TechDocs/Weapon.md`의 `_appearances` 표) |

### Build Settings (`ProjectSettings/EditorBuildSettings.asset`)

| 순서 | Scene |
|---|---|
| 0 | `Assets/Scenes/TitleScene.unity` |
| 1 | `Assets/Scenes/GameScene.unity` |

- `Assets/Scenes/TestMap.unity`는 Build Settings에 없다. 파일은 유지되어 있다.
- `StartGame`은 Scene 이름 `"GameScene"`으로 로드하므로 `GameScene`이 Build Settings에 있어야 한다.
- `GameScene.unity`는 Issue #10에서 `TestMap.unity`의 내용으로 교체되었다 (`Docs/TechDocs/Weapon.md`의 `GameScene` 절, `Docs/TechDocs/Map.md` 참고).

### 해상도 (`ProjectSettings/ProjectSettings.asset`, Issue #13)

| 설정 | 값 | 의미 |
|---|---|---|
| `defaultScreenWidth` / `defaultScreenHeight` | 1920 / 1080 | 기본 해상도 1920×1080 |
| `defaultIsNativeResolution` | 0 | 모니터 기본 해상도 대신 위 기본 해상도 사용 |
| `fullscreenMode` | 1 | FullScreenWindow (변경 없이 유지) |
| `allowFullscreenSwitch` | 0 | 전체 화면 전환 단축키 허용 안 함 |
| `resizableWindow` | 0 | 창 크기 조절 안 함 |

- 실제 빌드에서 이 설정대로 표시되는지는 확인하지 않았다 (빌드 실행 미검증).

## 예외·주의사항·검증 방법

### 주의사항·후속 작업

| # | 내용 | 후속 시점 |
|---|---|---|
| a | 텍스트는 레거시 UI `Text`이며 한글은 OS 폰트 폴백으로 표시된다 (코드의 `shortcut` 주석) | 한글 TMP 폰트 에셋 준비 시 TMP로 교체 |
| b | `CharacterSelection.Selected`는 static이라 값이 비워지는 시점이 에디터의 Domain Reload 설정에 달려 있다. 현재 `EditorSettings`는 `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0`(비활성화 옵션 없음)이라 Play 모드 진입 시 Domain Reload를 수행한다. 이 설정으로 보아 타이틀을 거친 뒤 `GameScene`을 단독 실행해도 이전 선택이 남지 않을 것으로 추정한다 (설정 기반 추론, Play로 확인하지 않음, 미검증). Domain Reload를 끄면 이전 Play의 선택이 남아 `_defaultCharacter` 대신 쓰인다 | Enter Play Mode 설정 변경 시 |
| c | 생존자 초상(`SPR_Survivor`)은 Pistol Animator 자세로 캡처되었다 (메인 세션 보고 기준). 생존자의 시작 무기는 샷건이다 | 정식 초상 교체 시 |
| d | `Start`는 첫 활성화 때만 실행된다. 패널을 껐다 다시 켜면 초기화하지 않아 이전 선택·텍스트·시작 버튼 상태가 유지된다. 현재 패널을 닫는 경로는 없다 | 닫기·뒤로 가기 추가 시 |
| e | 캐릭터 필수(고정) 아이템과 고유 특성은 범위 밖이다. `CharacterDefinition`에 해당 필드가 없다 | 추후 이슈 |
| f | 현재 캐릭터(`Character`)를 `WeaponLoadout`이 소유한다. 필수 아이템·특성이 추가되면 캐릭터 정보를 읽는 곳이 무기 밖으로 늘어나므로 별도 컴포넌트로 분리할 후보다 | 필수 아이템·특성 추가 시 |
| g | `WeaponLoadout._defaultCharacter`가 비어 있고 `Selected`도 `null`이면 `Character`가 `null`이라 `WeaponLoadout.Awake`의 `SignatureWeapon` 접근에서 `NullReferenceException`이 난다 (`Docs/TechDocs/Weapon.md` 주의사항 z11) | `PFB_Player` 연결 필수 |

### 검증 방법

- EditMode 테스트 : 캐릭터 선택 전용 테스트는 없다. `WeaponLoadoutTests`·`WeaponInstanceTests`가 `_defaultCharacter`에 임시 `CharacterDefinition`을 주입해 기존 주무기 테스트를 유지한다 (`Docs/TechDocs/Weapon.md`의 `검증 방법`). 결과는 EditMode 104/104 통과 (메인 세션 확인 기준).
- 컴파일 오류 0건 (메인 세션 확인 기준).
- Play 모드 : 사용자가 Play 모드로 확인한 뒤 검수를 승인했다 (임시 구현 상태로 확정). 확인한 세부 항목은 이 문서 작성 입력에 포함되지 않았다. 주의사항 d(패널 재활성화), g(연결 누락)는 재현하지 않았다 (미검증).

## 관련 Issue/PR

- Issue #10 (TitleScene·캐릭터 선택 구현)
- Issue #13 (타이틀 메뉴·설정·일시정지. 설정·일시정지는 `Docs/TechDocs/GameFlow.md`)
