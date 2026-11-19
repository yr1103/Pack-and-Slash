# Unity Hierarchy 작성 규칙

> Scene·Prefab의 Hierarchy를 구성하거나 변경할 때 따르는 규칙이다.
>
> Unity MCP로 작업할 때와 사용자가 에디터에서 직접 작업할 때 모두 적용한다.
>
> Scene·Prefab 작업에 필요한 기준은 이 문서에서만 관리한다.

---

## 0. 변경 기준

- Unity MCP의 읽기 작업(Hierarchy 조회, 콘솔 로그, 컴파일 결과 확인)은 승인 없이 수행할 수 있다.
- Unity MCP로 Scene·Prefab을 변경(오브젝트 생성·배치·이동·이름 변경, Inspector 연결)하는 것은 구현 브리핑에 대상이 기재되고 승인된 경우에만 수행한다.
- 브리핑에 없는 Scene·Prefab·에셋은 변경하지 않고 보고한다.
- 인벤토리·UI 작업은 `GameScene`을 복제한 Scene에서 검증한 뒤 `GameScene`에 덮어쓴다.

## 1. 최상위 구조

- Scene 최상위에는 역할별 그룹 오브젝트만 둔다.

| 그룹 | 포함 대상 |
|---|---|
| `Managers` | 게임 진행·데이터를 관리하는 오브젝트 |
| `Cameras` | Camera, Cinemachine |
| `Lights` | Light, Volume |
| `Environment` | 배경, 지형 등 움직이지 않는 오브젝트 |
| `Gameplay` | 플레이어, 적, 블록 등 게임 진행 오브젝트 |
| `UI` | Canvas, EventSystem |

- 해당하는 오브젝트가 없는 그룹은 만들지 않는다.
- 그룹 오브젝트는 컴포넌트를 붙이지 않고, Transform은 위치 (0, 0, 0), 회전 (0, 0, 0), 크기 (1, 1, 1)로 둔다.

## 2. 이름

- 영문 PascalCase로 작성하고, 공백은 쓰지 않는다. (예 : `BoardBackground`)
- Unity 기본 이름(`GameObject`, `Cube`, `Image (1)` 등)을 그대로 두지 않는다.
- Prefab 인스턴스는 Prefab 이름을 유지한다.
- 같은 종류가 여러 개면 `이름_번호` 형식으로 작성한다. (예 : `Spawner_01`, `Spawner_02`)
- 스크립트가 하나만 붙은 오브젝트는 가능하면 스크립트 이름과 맞춘다.

## 3. 계층 구조

- 부모-자식 관계는 Transform 종속이 필요하거나 묶어서 관리할 때만 만든다.
- 정리만을 위한 깊은 중첩은 피하고, 그룹 아래 깊이는 3단계 이내를 권장한다.
- 런타임에 생성되는 오브젝트는 생성 위치(부모)를 코드에서 지정해 해당 그룹 아래에 둔다.

### 기능 단위 분리

- 캐릭터·무기처럼 여러 기능이 모인 오브젝트는 **빈 루트 오브젝트** 아래에 기능별 자식으로 나눈다.
    - 루트 : 이동, 입력, 상태 등 오브젝트 전체에 대한 스크립트와 Collider·Rigidbody를 둔다.
    - 기능 자식(`Body`, `Weapon` 등) : 해당 기능의 스크립트를 두고, 독립적으로 회전·이동할 수 있는 단위로 나눈다.
    - 표시 자식(`BodySprite`, `WeaponSprite` 등) : SpriteRenderer·Animator 등 그래픽만 둔다. 스크립트 로직은 두지 않는다.
    - 위치 기준점(`MuzzlePoint`, `HitPoint` 등) : 컴포넌트 없는 빈 오브젝트로, 생성·판정 위치로만 사용한다.
- 그래픽을 표시 자식으로 분리하면, 그래픽을 뒤집거나 교체해도 루트의 이동·판정 로직에 영향을 주지 않는다.

## 4. Hierarchy 예시

> 아래는 규칙 적용 형태를 보여주는 예시이며, 실제 구성은 구현 브리핑에서 결정한다.

```
GameScene (Scene)
├─ Managers
│  ├─ GameManager
│  └─ SpawnManager
├─ Cameras
│  └─ MainCamera
├─ Lights
│  └─ GlobalLight
├─ Environment
│  └─ Ground
├─ Gameplay
│  ├─ Player                  (빈 루트 : 이동·입력 스크립트, Collider, Rigidbody)
│  │  ├─ Body                 (몸체 기능 : 피격, 애니메이션 제어)
│  │  │  └─ BodySprite        (SpriteRenderer, Animator)
│  │  └─ Weapon               (무기 기능 : 조준 회전, 발사)
│  │     ├─ WeaponSprite      (SpriteRenderer)
│  │     └─ MuzzlePoint       (빈 오브젝트 : 탄 생성 위치)
│  ├─ Enemies                 (런타임 생성 Enemy의 부모)
│  │  ├─ Enemy_01
│  │  └─ Enemy_02
│  └─ Projectiles             (런타임 생성 탄의 부모)
└─ UI
   ├─ HUDCanvas
   │  ├─ HealthBar
   │  └─ PauseButton
   └─ EventSystem
```

### 잘못된 예시

```
GameScene (Scene)
├─ Main Camera                (그룹 밖, 이름에 공백)
├─ GameObject                 (기본 이름 그대로)
├─ Player                     (SpriteRenderer와 이동·무기 스크립트가 루트에 모두 있음)
│  └─ Square                  (기본 이름, 무기 그래픽인지 알 수 없음)
│     └─ GameObject           (발사 위치용 빈 오브젝트에 기본 이름)
├─ Enemy (1)                  (그룹 밖, 기본 번호 형식)
└─ Canvas
   └─ Panel
      └─ Panel
         └─ Group
            └─ Text           (불필요한 깊은 중첩)
```

## 5. Unity MCP 작업 절차

1. 변경 전 `rql-query`로 현재 Hierarchy를 조회한다.
2. 같은 용도의 Prefab이 있으면 새로 만들지 않고 Prefab을 배치한다.
3. `rql-mutate`는 `dry_run`으로 결과를 먼저 확인한 뒤 실행한다.
4. 변경 후 Scene을 저장하고, 변경된 Hierarchy를 트리 형태로 보고한다.

### 보고 예시

```
Gameplay
├─ Board          (추가)
│  └─ BoardBackground
└─ Piece          (이동 : 최상위 → Gameplay)
```
