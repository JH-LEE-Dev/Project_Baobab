# 전리품 UI 가이드

전리품 1~3스테이지(원목 보험 증서, 포자 포션, 별빛 나침반)의 기능이 바뀌고 프레스티지 성장(I → II → III)이 생겼습니다. 시스템 코드는 들어갔고, **UI는 아무것도 바뀌지 않은 상태**입니다. 이 문서는 UI에서 읽을 수 있는 데이터와, 지금 내용이 맞지 않게 된 UI 목록을 정리합니다.

- 기준 커밋: `d1a58ecf` (`NewNewLee` 브랜치)
- 검증 수준: 프로젝트 파일로 컴파일만 확인했습니다. Unity 안에서 플레이 테스트는 아직 하지 않았습니다.
- 전리품 이름은 코드에서 바꾸지 않았습니다. 코드 이름(`LootType`)과 기획 이름은 아래 표로 맞춰 보세요.

---

## 1. 전리품 한눈에 보기

| 코드 이름 (`LootType`) | 기획 이름 (I / II / III) | 효과 | II 성장 | III 성장 |
|---|---|---|---|---|
| `LostAndFoundBox` (원목 보험 증서) | 바람개비 씨앗 / 꽃 / 나무 | 대시 | 프레스티지 4 | 프레스티지 7 |
| `SporePotion` (포자 포션) | 뭉글 씨앗 / 꽃 / 나무 | 포자 대폭발 | 프레스티지 8 | 프레스티지 10 |
| `StarCompass` (별빛 나침반) | 별비 씨앗 / 꽃 / 나무 | 유성우 | 프레스티지 9 | 프레스티지 11 |
| `ObsidianCharm` (흑요 부적) | - | 나무 열기 면역 (변경 없음) | 성장 없음 | 성장 없음 |

단계별 효과 수치 (툴팁 문구용, 현재값):

| 전리품 | I | II | III |
|---|---|---|---|
| 대시 | 저장 1회, 충전 4초 | 저장 1회, 충전 3초 | 저장 2회, 충전 2.5초 |
| 포자 대폭발 | 피해 50, 확률 5%, 피해 증폭 20% | 피해 100, 확률 7.5%, 피해 증폭 30% | 피해 200, 확률 10%, 피해 증폭 50% |
| 유성우 | 지속 3초 | 지속 4초 | 지속 5초 |

- 수치는 코드가 아니라 인스펙터에 있어서 밸런스 작업으로 바뀔 수 있습니다. 툴팁에 숫자를 넣을 거면 기획과 한 번 더 맞춰 주세요.
  - 대시: Character 프리팹 `Dash Settings`
  - 포자 대폭발·유성우·성장 기준 레벨: GameInstaller 프리팹의 `InDungeonObjectManager`
- 유성우의 피해·낙하 사정거리·게이지 충전량은 기획서에 N으로 되어 있어 임시값입니다.

획득 시점 (참고):

| 전리품 | 확률 드랍 | 보장 드랍 |
|---|---|---|
| 원목 보험 증서 | 1-2 | 1-3 이후 모든 숲 (그 방문에서 4~5그루째) |
| 포자 포션 | 2-1, 2-2, 2-3 | 3-1 이후 모든 숲 |
| 별빛 나침반 | 3-2 | 3-3 이후 모든 숲 |
| 흑요 부적 | 4-2 | 4-3 이후 모든 숲 |

---

## 2. 성장 단계 읽기 - `ILootDataProvider`

파일: `Assets/Scripts/Application Layer/ObjectSystem/Interface/ILootDataProvider.cs`

```csharp
IReadOnlyList<LootType> CurrentOwnedLoots { get; }       // 기존
event Action<LootType> LootAcquiredEvent;                 // 기존 - 처음 얻었을 때
int GetLootGrowthTier(LootType _lootType);                // 추가 - 지금 단계 (1~3, 아직 없거나 성장하지 않으면 0)
event Action<LootType, int> LootGrowthTierChangedEvent;   // 추가 - 단계가 올랐을 때 (전리품, 새 단계)
```

씨앗 / 꽃 / 나무 표시는 이렇게 하면 됩니다.

1. 화면을 열거나 갱신할 때 `GetLootGrowthTier(type)`으로 지금 단계를 읽어 그립니다.
2. `LootGrowthTierChangedEvent`를 받으면 그 전리품만 새 단계로 바꿉니다. 성장 연출을 넣을 자리입니다.
3. 처음 얻을 때는 `LootGrowthTierChangedEvent`가 오지 않습니다. `LootAcquiredEvent`를 받은 뒤 `GetLootGrowthTier`로 읽으세요.

알아둘 점:

- 단계는 **지금 프레스티지 레벨 기준**이라, 레벨이 오른 순간 바로 바뀝니다. 실제 게임 효과는 다음 숲 방문부터 적용되지만, 표시는 알림이 온 시점에 바꿔도 됩니다.
- 세이브를 불러올 때는 알림이 오지 않습니다. 화면을 열 때 `GetLootGrowthTier`로 읽으면 맞는 값이 나옵니다. 불러온 값을 "성장"으로 착각해 연출이 터지는 일은 없습니다.
- 흑요 부적은 성장하지 않아 항상 0입니다.

### 어디서 받을 수 있나

- 지금 `ILootDataProvider`를 주입받는 화면은 **HUD 하나**입니다. `GameplayUIManager`가 `hudUI.DependencyInjection(inDungeonObjProvider as ILootDataProvider)`로 넘깁니다.
- 마을 전리품 기둥 상호작용 화면(`UIView_ScreenModal` → `HUD_LootReveal.Show(LootType, ...)`)은 지금 `LootType`만 받고 단계를 모릅니다. 기둥에서도 씨앗 / 꽃 / 나무를 보여주려면 같은 방식으로 `ILootDataProvider`를 넘겨 주세요(위 `GameplayUIManager`의 한 줄을 참고).

---

## 3. 대시 HUD - `IDashState`

파일: `Assets/Scripts/Presentation Layer/UnitSystem/Interface/IDashState.cs`, 읽는 곳은 `ICharacter.dashState`

```csharp
event Action DashStateChangedEvent;   // 해금 여부·찬 횟수·최대 횟수가 바뀔 때만 (진행도 변화로는 안 옴)
bool bUnlocked { get; }               // false면 숨기기 (전리품 없음, 마을, 로그라이크)
int CurrentCharges { get; }           // 지금 쓸 수 있는 횟수
int MaxCharges { get; }               // 칸 수 (I·II 1칸, III 2칸)
float RechargeProgress { get; }       // 다음 1칸이 찬 정도 0~1, 다 찼으면 1
```

**구독은 이미 되어 있고, 빈 함수 두 개만 채우면 됩니다.** 파일은 `Assets/Scripts/Presentation Layer/UISystem/UIView/HUD/UIView_HUD.cs`의 `#region HUD_Dash Logic`입니다.

```csharp
// 해금 여부·찬 횟수·최대 횟수가 바뀔 때 (처음 연결될 때도 한 번)
private void UpdateDashCharges(bool _bUnlocked, int _currentCharges, int _maxCharges) { }

// 매 프레임 - 다음 1칸 충전 진행도 (0~1)
private void UpdateDashRecharge(float _rechargeProgress) { }
```

- 최대 칸 수는 판 중에 바뀔 수 있습니다(던전 입장 때 단계가 바뀌는 경우 등). 칸을 미리 고정해 두지 말고 `_maxCharges`를 보고 그리세요.
- 대시 키 아이콘은 `ERebindableAction.Dash`입니다(기본 Space / 패드 X). `UI_KeyboardImage`의 `boundAction`을 Dash로 두면 됩니다.

---

## 4. 유성우 게이지 - 아직 HUD까지 연결 안 됨

유성우는 나무를 벨 때마다 게이지가 차고, 가득 차면 쏟아집니다. 게이지 값은 있지만 **UI로 내려가는 길이 아직 없습니다.**

- 값: `InDungeonObjectManager.MeteorShowerCharge` (0~1). 숲에 들어갈 때마다 0에서 시작하고, 유성우가 쏟아지는 동안에는 차지 않습니다.
- 알림: `InDungeonObjectManager.MeteorShowerStartedEvent(float 지속시간)` - 유성우 시작
- 둘 다 `ILootDataProvider`에는 없고 `InDungeonObjectManager`에만 있습니다.

게이지를 HUD에 띄울 계획이면 시스템 쪽에 요청해 주세요. 대시처럼 `ILootDataProvider`나 별도 인터페이스로 내려주고, `UIView_HUD`에 빈 갱신 함수를 마련해 드리는 방식이 됩니다.

---

## 5. 지금 내용이 맞지 않는 UI (손볼 곳)

기능이 바뀌면서 문구나 표시가 맞지 않게 된 곳입니다. 시스템 쪽에서는 일부러 건드리지 않았습니다.

### 전리품 이름·설명

- 현지화 파일: `Assets/Resources/Localization/LootTooltipUI.json`

| 전리품 | 이름 키 | 설명 키 | 지금 설명 내용 | 바뀐 효과 |
|---|---|---|---|---|
| 원목 보험 증서 | `name_lostandfoundbox` | `foundbox` | 탈진 시 원목 30% 구제 | 대시 |
| 포자 포션 | `name_sporepotion` | `sporepotion` | 특수 키로 마셔 피로도 회복 | 포자 대폭발 |
| 별빛 나침반 | `name_starcompass` | `starcompass` | 별자리·별표식을 볼 수 있게 됨 (이미 기본으로 항상 보여서 실제로는 효과가 없었음) | 유성우 |

- 단계별로 이름(씨앗 / 꽃 / 나무)이나 설명이 달라져야 하면 키를 단계별로 나눠야 합니다. 지금은 전리품당 키가 하나이고, `HUD_LootReveal`의 `GetNameKey` / `GetDescKey`가 `LootType`만 보고 키를 고릅니다.

### 포자 포션 HUD 슬롯 (`HUD_Loot`)

- 예전 "마시는 포션" 기준의 슬롯입니다. 포자 포션을 얻으면 나타나고, 포션 키 아이콘(`boundAction` = PotionKey)이 붙어 있습니다.
- 이제 포자 포션은 마시는 아이템이 아니고(자동 발동), 포션 키도 기본 바인딩이 비어 있어 아이콘이 표시되지 않습니다. 슬롯을 없앨지, 다른 용도로 쓸지 정해 주세요.
- `PlayUsePotionMotion`은 이제 부르는 곳이 없습니다(에디터 테스트 버튼만 남아 있음).

### 키 설정 화면 (옵션 → 컨트롤)

- **대시 행**: 이름표 현지화가 없어 `"Dash"`로 나옵니다. `UI_Option.GetActionLabel`에 `ERebindableAction.Dash` case가 없고, `OptionUI.json`에 키도 없습니다. 행 위치도 enum 순서대로 맨 끝입니다.
- **포션 행**: 기본 키가 비어 있어 빈 칸으로 나옵니다. 키 설정에서 숨길지 정해 주세요.
- 바뀐 기본 키: 대시 Space / X, 인벤토리 Tab / LT, 원목 교체 Q / Y, 포션 없음.

### 문서

- `Assets/Scripts/Application Layer/InputSystem/KeyBindingSystem_UI가이드.md`
- `Assets/Scripts/Application Layer/InputSystem/ControllerSupport_UI가이드.md`
- `Docs/LogSwapUI.md`

위 세 문서는 예전 키 배치(인벤토리 Space, 원목 교체 Tab, 포션 Q / X) 그대로입니다.

---

## 6. 참고: 이펙트 작업용 알림

UI 범위는 아니지만 같이 작업하는 경우를 위해 남깁니다.

| 효과 | 알림 | 상태 |
|---|---|---|
| 유성우 시작 | `InDungeonObjectManager.MeteorShowerStartedEvent(float 지속시간)` | 있음 |
| 유성 착지 | `InDungeonObjectManager.MeteorImpactEvent(Vector3 착지 위치)` | 있음 (유성마다, 피해 직전) |
| 포자 대폭발 | 없음 | 필요하면 요청 (위치·단계를 넘기는 알림을 추가) |
| 대시 | `DashState` 진입·종료 | 별도 알림 없음, 필요하면 요청 |
