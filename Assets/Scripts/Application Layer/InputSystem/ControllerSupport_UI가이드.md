# 게임패드 지원 - 입력 시스템 가이드

패드 지원의 **시스템(Application Layer) 쪽은 전부 완료**되어 있습니다. 이 문서는 UI 작업자가 그 위에 화면을 붙이기 위한 안내입니다.

- 장치 판별: [InputDeviceTracker.cs](InputDevice/InputDeviceTracker.cs) · [InputDeviceTypes.cs](InputDevice/InputDeviceTypes.cs) · [InputDeviceSettings.cs](InputDevice/InputDeviceSettings.cs)
- 진동: [GamepadHaptics.cs](Haptics/GamepadHaptics.cs)
- 입력 모드: [InputMode.cs](InputMode.cs)
- 바인딩·리바인딩: [InputReader.cs](InputReader/InputReader.cs) · [RebindTypes.cs](InputReader/RebindTypes.cs)
- **UI가 실제로 호출하는 곳**: [InputManager.cs](InputManager/InputManager.cs)
- 진단 도구: `Tools > Input > 게임패드 진단`

키보드 리바인딩의 기본 흐름(편집 세션 등)은 별도 문서입니다 → [KeyBindingSystem_UI가이드.md](KeyBindingSystem_UI가이드.md)

---

## 0. UI 작업자 할 일 체크리스트

**전부 백엔드가 완료되어 있어 지금 바로 착수 가능합니다.** 각 항목의 상세는 아래 3장을 보세요.

| # | 할 일 | 상세 |
|---|---|---|
| 1 | 패드 아이콘 스프라이트 + 세트별 DB (Xbox/PS/Nintendo/Generic) | [3-3](#3-3-아이콘-데이터베이스-구조-제안) |
| 2 | `UI_KeyboardImage` 연결 + 패드 분기 1줄 수정 | [3-2](#3-2-ui_keyboardimage-연결) |
| 3 | 옵션 — **버튼 표기 셀렉터** | [2-1](#2-1-옵션의-버튼-표기-항목--저장까지-이미-연결됨) |
| 4 | 옵션 — **진동 세기 슬라이더** | [진동 세기 설정](#진동-세기-설정) |
| 5 | 옵션 — **패드 키 설정 탭** (Move 항목 잠금 필수) | [3-5](#3-5-키-설정-화면과-패드-패드-리바인딩-지원됨) |
| 6 | **팝업 열고 닫을 때 `SetInputMode` 호출** ← 빠뜨리면 패드에서 오작동 | [3-7](#3-7-입력-모드--팝업을-열-때-반드시-호출하세요) |
| 7 | UI 포커스 이동 (선택 표시, 복원, 먹통 방어) | [3-8](#3-8-ui-포커스-이동) |
| 8 | 패드 연결 해제 모달 | [3-6](#3-6-패드-연결-해제) |
| 9 | `UI_PressAnyKey` 이관 | [3-4](#3-4-ui_pressanykey-이관-권장) |
| 10 | 마우스 좌표를 직접 읽는 UI 4곳 가드 | [3-10](#3-10-마우스-좌표를-직접-읽는-곳-패드에서-오작동) |
| 11 | **마을 가상 커서 그리기 + 그 좌표로 집기** | [3-12](#3-12-마을-가상-커서-패드-전용) |

기획 결정이 필요한 것 하나: **무엇이 언제 진동할지.** 진동 시스템은 완료되어 호출 지점만 비어 있습니다.

---

## 0-1. 시스템 쪽 구현 상태

| 완료 | 비고 |
|---|---|
| 키보드/마우스 ↔ 패드 자동 전환 판정 | 노이즈 필터·쿨다운 포함 |
| 패드 벤더 판별 (Xbox / PS / Nintendo / Generic) | Steam Input 한계는 [4장](#4-알려진-한계) |
| 아이콘 표기 수동 지정 + 저장·복원 | UI는 셀렉터만 붙이면 됨 |
| 패드 연결/해제 감지 | 모달은 UI 몫 |
| 패드 기본 배치 | 아래 표 |
| 패드 리바인딩 + 장치별 충돌 검사 | |
| 패드 조준 (오른쪽 스틱) | 에임 어시스트는 쓰지 않기로 확정 |
| 패드 진동 + 세기 설정 저장 | 호출 지점은 기획 결정 |
| UI 액션 맵 + 입력 모드 전환 | EventSystem 5개 씬 교체 완료 |
| "아무 입력이나 있었는가" 조회 | |
| 마을 가상 커서 (좌표·토글·영역 제한) | 그림과 집기는 UI 몫 → [3-12](#3-12-마을-가상-커서-패드-전용) |
| 가상 커서 토글 키 리바인딩 | 패드 탭에만 표시됨 (`GetRebindableActions(장치)`) |
| 패드 사용 중 OS 커서 숨김 | InputManager가 자동 처리 → [3-9](#3-9-os-커서-표시숨김-시스템이-처리함) |

**패드 기본 배치**

| 액션 | 패드 | | 액션 | 패드 |
|---|---|---|---|---|
| Move | `leftStick` | | Interaction | `buttonSouth` (A/×) |
| Aim | `rightStick` | | Inventory | `buttonNorth` (Y/△) |
| Attack | `rightTrigger` (RT/R2) | | PotionKey | `buttonWest` (X/□) |
| ESC(메뉴) | `start` | | 가상 커서 토글 | `rightStickPress` (R3) |
| *(B/○ 는 취소 전용으로 비워둠)* | | | | |

`buttonEast (B/○)`는 **의도적으로 비워둔 것**입니다. 리바인딩 취소이자 "뒤로가기"라는 보편 관례라, 다른 기능에 할당하면 유저가 리바인딩 대기 상태에서 빠져나올 수단을 잃습니다.

---

## 1. 동작 방식 요약

**마지막으로 실제 조작한 장치가 이깁니다.** 키보드와 패드가 동시에 꽂혀 있어도 **둘 다 항상 입력을 받습니다.** 바뀌는 것은 "무엇을 화면에 보여줄지"뿐입니다.

```
유저가 패드 스틱을 기울임  → CurrentDevice = Gamepad     → InputDeviceChangedEvent 발생
유저가 마우스를 움직임      → CurrentDevice = KeyboardMouse → InputDeviceChangedEvent 발생
패드 연결이 끊김           → GamepadConnectionChangedEvent(false)
                          → 패드 모드였다면 즉시 KeyboardMouse로 강제 복귀
```

노이즈는 조작으로 치지 않습니다. 스틱 드리프트나 마우스 미세 떨림으로 아이콘이 깜빡이지 않도록 문턱값(스틱 0.5, 마우스 누적 12px)과 전환 쿨다운(0.3초)이 걸려 있습니다.

값 조정은 `Assets/Scriptable Obj/Input/InputDeviceSettings.asset`에서 합니다. (`MainMenuScene`의 `Bootstrap` 오브젝트에 이미 연결되어 있습니다) 실제 패드의 드리프트 수치는 진단 창에서 잴 수 있습니다.

---

## 2. API 레퍼런스 (`viewCtx.inputManager`)

### 상태 조회

| 멤버 | 반환값 | 설명 |
|---|---|---|
| `CurrentDevice` | `EInputDeviceType` | `KeyboardMouse` / `Gamepad`. **마지막으로 조작한 쪽** |
| `IsGamepadMode` | `bool` | `CurrentDevice == Gamepad` 의 축약 |
| `IsGamepadConnected` | `bool` | 패드가 물리적으로 꽂혀 있는지. **사용 여부와는 별개** |
| `CurrentGamepadIconSet` | `EGamepadIconSet` | 그려야 할 아이콘 표기. 수동 지정이 있으면 그 값 |
| `DetectedGamepadIconSet` | `EGamepadIconSet` | 수동 지정과 무관한 자동 판별 결과 |
| `AnyInputThisFrame` | `bool` | 이번 프레임에 **어느 장치에서든** 조작이 있었는지 |

`AnyInputThisFrame`은 "아무 키나 누르세요" 화면이나 유휴 타이머 해제처럼 **무슨 키인지는 상관없고 입력이 있었다는 사실만 필요한 곳**에 씁니다. 장치 전환과 같은 문턱값을 쓰므로 책상 진동으로 인한 마우스 지터나 스틱 드리프트는 입력으로 치지 않습니다. `InputManager.Update`에서 갱신되므로 스크립트 실행 순서에 따라 최대 1프레임 늦을 수 있습니다.

`IsGamepadMode`와 `IsGamepadConnected`를 헷갈리지 마세요. **패드를 꽂아둔 채 키보드로 플레이하는 유저**가 흔하고, 이때 `IsGamepadConnected == true`지만 `IsGamepadMode == false`입니다. 화면 표기는 **반드시 `IsGamepadMode` 기준**으로 판단해야 합니다.

### 변경 알림 이벤트

리바인딩 이벤트와 동일한 관례로, `InputManager`가 아니라 `inputReader`에 있습니다.

```csharp
viewCtx.inputManager.inputReader.InputDeviceChangedEvent      += OnDeviceChanged;      // Action<EInputDeviceType>
viewCtx.inputManager.inputReader.GamepadIconSetChangedEvent   += OnIconSetChanged;     // Action<EGamepadIconSet>
viewCtx.inputManager.inputReader.GamepadConnectionChangedEvent += OnConnectionChanged; // Action<bool>
```

> 구독은 반드시 해제하세요. `-=` 후 `+=` 하는 이 프로젝트의 기존 패턴을 그대로 따르면 됩니다.

### 장치별 바인딩 조회

```csharp
string GetBindingPath(ERebindableAction _action, EInputDeviceType _device);           // 없으면 null
string GetBindingDisplayString(ERebindableAction _action, EInputDeviceType _device);  // 없으면 ""
string GetBindingPathForCurrentDevice(ERebindableAction _action);
string GetBindingDisplayStringForCurrentDevice(ERebindableAction _action);
bool   HasBindingFor(ERebindableAction _action, EInputDeviceType _device);
```

기존의 **인자 하나짜리** `GetBindingPath(action)` / `GetBindingDisplayString(action)`은 **항상 키보드 바인딩**을 돌려줍니다. 키 설정 화면은 어떤 장치를 쓰든 키보드 배치를 보여줘야 하므로 일부러 그대로 뒀습니다. HUD 조작 안내처럼 현재 장치를 따라가야 하는 곳에서만 위의 장치 인자 버전을 쓰세요.

패드 바인딩이 **없는** 액션을 `_device: Gamepad`로 조회하면 `null` / `""`이 나옵니다. UI는 `HasBindingFor`가 `false`면 아이콘을 숨기도록 방어해 두세요. (현재 리바인딩 대상 8개는 전부 패드 바인딩이 있지만, 나중에 액션이 늘어날 때를 대비한 것입니다)

### 제어

```csharp
// 연출/튜토리얼 전용. 다음 실제 입력이 들어오면 자동 판별로 돌아갑니다.
void ForceInputDevice(EInputDeviceType _device);

// 저장 없이 표기만 즉시 바꾸는 저수준 API. 옵션 UI는 이걸 직접 쓰지 말고 아래 2-1을 쓰세요.
void SetGamepadIconSetOverride(bool _bUseOverride, EGamepadIconSet _iconSet);
```

### 2-1. 옵션의 "버튼 표기" 항목 — 저장까지 이미 연결됨

설정값의 **저장·복원·적용은 전부 끝나 있습니다.** UI는 다른 옵션 항목과 똑같이 셀렉터만 붙이면 됩니다.

```csharp
public enum EGamepadIconPreference { Auto, Xbox, PlayStation, Nintendo, Generic }

// 읽기
EGamepadIconPreference cur = SettingsManager.Instance.Current.gamepadIconPreference;

// 쓰기 — 둘 다 선택 즉시 화면 아이콘에 실시간 반영되고, 저장은 CommitChanges에서 이뤄집니다.
SettingsManager.Instance.CycleGamepadIconPreference(+1);   // 좌/우 화살표형
SettingsManager.Instance.SetGamepadIconPreference(EGamepadIconPreference.Xbox);  // 목록형
```

`Auto`를 고르면 자동 판별로 돌아갑니다. 라벨에 판별 결과를 같이 보여주고 싶으면 `inputManager.DetectedGamepadIconSet`을 읽어 `"자동 (Xbox)"`처럼 조합하면 됩니다.

> 다른 옵션 항목과 마찬가지로 **창을 닫을 때 `CommitChanges()`가 불려야 파일에 기록**됩니다. (기존 옵션 창 흐름을 그대로 따르면 됩니다)

---

### 2-2. 패드 진동

```csharp
// 세게 짧게 (피격·타격)
viewCtx.inputManager.Haptics.Play(0.8f, 0.2f, 0.15f);

// 약하게 길게 (엔진 아이들 등)
viewCtx.inputManager.Haptics.Play(0.15f, 0.05f, 1.0f);

viewCtx.inputManager.Haptics.Stop();

bool canPlay   = viewCtx.inputManager.Haptics.CanPlay;    // 패드 없음 / 세기 0이면 false
bool isPlaying = viewCtx.inputManager.Haptics.IsPlaying;
```

`Play(저주파, 고주파, 지속시간초)` — 저주파는 굵고 묵직한 진동, 고주파는 잔진동입니다. **패드가 없거나 세기 설정이 0이면 아무 일도 일어나지 않으니 호출부에서 검사할 필요가 없습니다.**

겹침 정책은 **"더 강한 쪽이 이긴다"** 입니다. 약하고 긴 진동이 도중에 들어온 강한 타격 진동을 덮어써 타격감을 없애는 일을 막기 위해서입니다. 약한 요청이 더 길면 남은 시간만 늘어납니다.

시스템이 알아서 처리하므로 호출부가 신경 쓰지 않아도 되는 것:
- 포커스 상실(알트탭)·게임 종료 시 자동으로 모터를 끕니다. **이걸 빠뜨리면 게임을 꺼도 패드가 계속 울립니다.**
- 일시정지(`timeScale = 0`) 중에도 지속시간이 정상적으로 흘러 끝납니다.
- 재생 도중 패드가 교체·재연결되어도 새 패드에 자동으로 이어집니다.

### 진동 세기 설정

아이콘 표기와 동일한 방식으로 저장·복원이 이미 연결되어 있습니다. UI는 슬라이더만 붙이면 됩니다.

```csharp
float cur = SettingsManager.Instance.Current.hapticStrength;   // 0~100
SettingsManager.Instance.SetHapticStrength(v);                 // 즉시 반영 + CommitChanges에서 저장
```

슬라이더를 조절하는 동안 실제로 패드가 울려야 세기를 가늠할 수 있으므로, 드래그 콜백에서 짧은 `Play`를 함께 호출해 주면 좋습니다.

---

## 3. UI 작업 시 알아둘 것

### 3-1. 아이콘 전환의 정석

```csharp
private void OnEnable()
{
    viewCtx.inputManager.inputReader.InputDeviceChangedEvent -= OnDeviceChanged;
    viewCtx.inputManager.inputReader.InputDeviceChangedEvent += OnDeviceChanged;

    // 이벤트는 "변화"만 알려주므로, 켜지는 시점의 현재 상태를 한 번 직접 반영해야 한다.
    ApplyDevice(viewCtx.inputManager.CurrentDevice);
}

private void OnDisable()
{
    viewCtx.inputManager.inputReader.InputDeviceChangedEvent -= OnDeviceChanged;
}

private void OnDeviceChanged(EInputDeviceType _device) => ApplyDevice(_device);
```

이 "이벤트 구독 + 현재 상태 1회 반영" 조합을 빠뜨리면, UI를 연 시점의 장치가 반영되지 않아 처음 한 번은 항상 키보드 아이콘이 뜹니다.

### 3-2. `UI_KeyboardImage` 연결

[UI_KeyboardImage.cs](../../Presentation%20Layer/UISystem/UIView/Option/UI_KeyboardImage.cs)에 이미 `SetGamepadMode(bool)`과 패드 버튼 enum이 들어 있지만, **아직 아무도 호출하지 않습니다.** 일부러 연결하지 않았습니다. 지금 연결하면 패드를 꽂는 순간 `KeyIconDatabase`에 패드 스프라이트가 없어서 모든 키 아이콘이 사라지기 때문입니다.

패드 아이콘 에셋이 준비되면 `Initialize`에서 아래 한 줄만 추가하면 연결됩니다.

```csharp
inputManager.inputReader.InputDeviceChangedEvent += _device => SetGamepadMode(EInputDeviceType.Gamepad == _device);
```

동시에 `RefreshIcon()`의 패드 분기가 지금은 키보드 경로를 조회하고 있으므로(`inputManager.GetBindingPath(boundAction)`), 이것을 `GetBindingPath(boundAction, EInputDeviceType.Gamepad)`로 바꿔야 합니다.

### 3-3. 아이콘 데이터베이스 구조 제안

패드 바인딩 경로는 `<Gamepad>/buttonSouth`처럼 **벤더 중립**입니다. 즉 Xbox/PS/Nintendo 세 표기의 경로 문자열은 전부 동일하고 스프라이트만 다릅니다. 따라서 `KeyIconDatabase`(경로→스프라이트) 형태의 SO를 **아이콘 세트별로 하나씩** 만들고 `CurrentGamepadIconSet`으로 골라 쓰는 구조가 가장 단순합니다.

### 3-4. `UI_PressAnyKey` 이관 (권장)

[UI_PressAnyKey.cs](../../Presentation%20Layer/UISystem/UIView/MainMenu/UI_PressAnyKey.cs)가 `Gamepad.current.allControls`를 직접 순회하고 있는데, 이제 같은 판정을 시스템이 제공합니다. `Initialize`에 `InputManager`를 넘겨받도록 바꾼 뒤 `Update`의 3분기를 아래 한 줄로 대체할 수 있습니다.

```csharp
bool _anyInputReceived = inputManager.AnyInputThisFrame;
```

부수 효과로 **마우스 입력 판정이 개선됩니다.** 현재 코드는 마우스 클릭만 보고 이동은 무시하는데, 시스템 판정은 의도적인 마우스 이동도 입력으로 인정하면서 미세 지터는 걸러냅니다.

### 3-5. 키 설정 화면과 패드 (패드 리바인딩 지원됨)

키 설정 API는 전부 **장치 인자 오버로드**가 생겼습니다. 인자를 안 넘기면 기존처럼 키보드입니다.

```csharp
inputManager.GetRebindableActions(EInputDeviceType.Gamepad);   // 탭에 그릴 목록
inputManager.StartRebind(action, EInputDeviceType.Gamepad, onFinished);
inputManager.ResetBinding(action, EInputDeviceType.Gamepad);
inputManager.IsConflicting(action, EInputDeviceType.Gamepad);
inputManager.HasAnyConflict(EInputDeviceType.Gamepad);   // 탭별 표시용
inputManager.IsRebindable(action, EInputDeviceType.Gamepad);
```

**행 목록은 `GetRebindableActions(장치)`로 받으세요.** 두 장치의 항목 수가 다릅니다 — 가상 커서 토글(R3)은 패드에만 있어서 키보드 탭에는 나오지 않습니다. 전체 enum을 그대로 돌면 키보드 탭에 빈 칸 행이 하나 끼어듭니다.

> 행과 액션을 인덱스로 짝짓는 구조라면, **행을 만들 때와 갱신할 때 같은 장치의 목록**을 써야 합니다. 섞으면 라벨과 키가 어긋납니다. 지금 [UI_Option](../../Presentation%20Layer/UISystem/UIView/Option/UI_Option.cs)이 그 구조이고, 인자 없는 `GetRebindableActions()`가 키보드 목록을 돌려주므로 **키보드 탭은 고칠 것이 없습니다.**
>
> 패드 탭을 만들 때 `GetActionLabel`에 `ERebindableAction.VirtualCursor` 케이스를 추가하세요. 없으면 화면에 `VirtualCursor`가 그대로 나옵니다. (로컬라이징 키도 함께 필요합니다)

**`IsRebindable`을 반드시 확인하세요.** 패드의 이동(`MoveUp/Down/Left/Right`)은 전부 왼쪽 스틱 하나에 묶여 있어 개별 변경이 불가능합니다. 이 항목들은 **표시는 되지만 "변경" 버튼을 비활성화**해야 합니다.

시스템이 이미 처리하는 것:
- **패드 버튼이 키보드 바인딩으로 잡히지 않습니다.** (막지 않으면 그 기능의 키보드 키가 패드 경로로 덮어써져 조용히 사라집니다)
- 반대로 패드 리바인딩 중에는 키보드/마우스가 잡히지 않습니다.
- **B / ○ (buttonEast)는 새 바인딩으로 잡히지 않습니다.** 리바인딩 취소 겸 "뒤로가기" 전용이라, 다른 기능에 할당되면 유저가 리바인딩 대기에서 빠져나올 수단을 잃습니다.
- **패드로 리바인딩을 취소할 수 있습니다.** 안내 문구는 현재 장치에 맞춰 "ESC" 또는 "B"를 보여주세요.

**중복 검사는 장치 안에서만** 합니다. 키보드의 E와 패드의 A는 동시에 눌릴 일이 없으므로 충돌이 아닙니다. 단 저장 차단용 `HasAnyConflict()`(인자 없음)는 **모든 장치를 통틀어** 검사합니다 — 키보드 탭만 보고 저장을 허용하면 패드 쪽 중복이 그대로 기록되기 때문입니다.

### 3-6. 패드 연결 해제

패드가 빠지면 `GamepadConnectionChangedEvent(false)`가 오고 표기는 자동으로 키보드로 돌아갑니다. 다만 **게임을 일시정지하고 "컨트롤러 연결이 끊겼습니다" 모달을 띄우는 것은 UI 몫**입니다. 콘솔 인증 필수 항목이고 PC에서도 표준입니다.

### 3-7. 입력 모드 — 팝업을 열 때 반드시 호출하세요

패드 때문에 새로 생긴 개념입니다. 마우스 시절에는 "커서가 UI 위에 있는가"로 구분했지만, 패드에는 커서가 없습니다. **A 버튼은 UI에서 "확인"이고 게임에서 "상호작용"이라, 팝업을 확인하려고 A를 누르면 뒤에서 캐릭터가 같이 상호작용합니다.**

```csharp
// 팝업/메뉴를 열 때
viewCtx.inputManager.SetInputMode(EInputMode.UI);

// 닫을 때
viewCtx.inputManager.SetInputMode(EInputMode.Gameplay);
```

`UIView_Popup`이 지금 `SetCursorHoveredOnUI(true/false)`를 부르는 **바로 그 자리**에 같이 넣으면 됩니다. (`IsCursorHoveredOnUI()`는 이제 UI 모드일 때도 true를 반환하므로, 기존 호출부는 고치지 않아도 패드에서 같은 보호가 걸립니다)

UI 모드에서 막히는 것: 이동 · 조준 · 공격 · 상호작용 · 물약
UI 모드에서도 통과하는 것: **ESC/Start**(메뉴를 닫아야 하므로) · **인벤토리 키**(같은 키로 닫으므로) · **버튼을 떼는 신호**(누른 채 창이 열리면 공격이 눌린 채로 남으므로)

### UI 전용 입력

```csharp
viewCtx.inputManager.inputReader.UICancelEvent  += OnCancel;    // 패드 B/○
viewCtx.inputManager.inputReader.UITabShiftEvent += OnTabShift; // -1 = LB/PageUp, +1 = RB/PageDown
viewCtx.inputManager.inputReader.InputModeChangedEvent += OnModeChanged;
```

키보드 ESC는 **기존대로** `ESCButtonPressedEvent`로 옵니다. UI 맵의 Cancel에는 일부러 ESC를 넣지 않았습니다 — 넣으면 같은 키가 두 경로로 동시에 처리됩니다.

### EventSystem

씬 5개(Town / Dungeon / MainMenu / AbilityTool / [Test]Motion)의 `InputSystemUIInputModule`이 **패키지의 `DefaultInputActions` → 프로젝트의 `InputActionSystem`** 으로 교체되었습니다. 이제 UI 네비게이션 바인딩을 프로젝트에서 직접 관리합니다.

| UI 액션 | 바인딩 |
|---|---|
| Navigate | 키보드 방향키 · `leftStick` · `dpad` |
| Submit | `Enter` · `buttonSouth` (A/×) |
| Cancel | `buttonEast` (B/○) |
| Point / Click / RightClick / MiddleClick / ScrollWheel | 마우스 |
| TabLeft / TabRight | `LB`·`RB` · `PageUp`·`PageDown` |

### 3-8. UI 포커스 이동

패드에는 커서가 없으므로 **"지금 선택된 것"이 없으면 아무것도 조작할 수 없습니다.** 현재 프로젝트에는 `EventSystem.SetSelectedGameObject` / `firstSelectedGameObject` 호출이 **한 군데도 없어서**, 패드 Navigate/Submit이 배선되어 있어도 실제로는 아무 일도 일어나지 않습니다.

화면을 만들 때 챙겨야 할 것:

- 패드 모드로 창이 열리면 **첫 항목을 선택**해 두기 (마우스 모드면 선택하지 않기 — 안 그러면 마우스 유저에게 엉뚱한 하이라이트가 남습니다)
- 창을 닫을 때 **직전 화면의 마지막 선택을 복원**하기 (인벤토리 → 상세 → 뒤로 갔을 때 커서가 맨 위로 튀는 문제)
- 각 `Selectable`의 Navigation은 **Explicit 권장** — Automatic은 픽셀아트 레이아웃에서 자주 엉뚱한 곳으로 갑니다
- **가장 흔한 버그**: 선택 중인 버튼이 비활성화되면 `currentSelectedGameObject`가 `null`이 되고 **패드 조작이 완전히 먹통**이 됩니다. 매 프레임 확인해서 복구하세요

탭 전환은 `UITabShiftEvent`(LB/RB · PageUp/PageDown)를 쓰면 됩니다. ([3-7](#3-7-입력-모드--팝업을-열-때-반드시-호출하세요))

### 3-9. OS 커서 표시/숨김 (시스템이 처리함)

**UI가 할 일은 없습니다.** `InputManager`가 장치 상태를 보고 알아서 처리합니다.

- 패드로 조작하는 동안 OS 커서를 감춥니다 (`Cursor.visible`)
- 마우스를 조금(기본 12px) 움직이면 장치가 키보드/마우스로 바뀌면서 다시 나타납니다
- 알트탭 등으로 포커스를 잃으면 장치와 무관하게 돌려줍니다

판단 기준은 `IsGamepadConnected`(연결 여부)가 아니라 **`IsGamepadMode`(실제 사용 중)** 입니다. 패드를 꽂아둔 채 키보드로 플레이하는 유저가 흔한데, 연결 여부로 판단하면 그 유저는 커서를 통째로 잃습니다.

> 커서가 숨겨져 있어도 마우스 이동 자체는 그대로 읽히므로, "커서가 사라져서 되돌릴 방법이 없는" 상태에는 빠지지 않습니다. 에디터에서 패드로 테스트하다 커서가 사라지면 마우스를 살짝 움직이면 됩니다.

[UIView_CursorBox](../../Presentation%20Layer/UISystem/UIView/CursorBox/UIView_CursorBox.cs)는 이름과 달리 마우스 포인터가 아니라 **UI 선택 표시(하이라이트 박스)** 라 여기와 무관합니다. 그쪽은 [3-8 포커스 이동](#3-8-ui-포커스-이동)에 속합니다.

### 3-10. 마우스 좌표를 직접 읽는 곳 (패드에서 오작동)

아래 네 곳이 `Mouse.current.position`을 직접 읽습니다. 패드 모드에서는 커서가 멈춰 있어 **마지막 마우스 위치에 고정된 채로 동작**합니다.

| 파일 | 하는 일 | 패드에서 |
|---|---|---|
| [UI_EscapeMenuButton.cs:368](../../Presentation%20Layer/UISystem/UIView/ESCMenu/UI_EscapeMenuButton.cs) | 메뉴가 커서 아래 나타났을 때 호버를 수동 레이캐스트 | 의미 없음 |
| [UI_MainMenuButton.cs:719](../../Presentation%20Layer/UISystem/UIView/MainMenu/UI_MainMenuButton.cs) | `OnPointerEnter` 누락 대비 폴백 | 의미 없음 |
| [UI_Credit.cs:106](../../Presentation%20Layer/UISystem/UIView/MainMenu/UI_Credit.cs) | 마우스 홀드 스크롤 | **대체 수단 필요** |
| [UI_MainMenuBackground.cs:117](../../Presentation%20Layer/UISystem/UIView/MainMenu/UI_MainMenuBackground.cs) | 배경 패럴랙스 | 멈추기만 함 (경미) |

앞의 둘과 넷째는 `if (inputManager.IsGamepadMode) return;` 한 줄이면 됩니다. `UI_Credit`의 스크롤만 패드 대체(스틱 스크롤 등)가 필요합니다.

### 3-11. 진단 창

`Tools > Input > 게임패드 진단` 으로 엽니다. 연결된 패드와 벤더 판별, 실시간 스틱·버튼 값, 스틱 드리프트 실측, 진동 테스트, 바인딩 표와 중복 여부를 한 화면에서 볼 수 있습니다. 실기 확인 체크리스트도 들어 있습니다. 가상 커서의 켜짐 여부·좌표·이동 영역도 여기서 실시간으로 보이므로, 커서 그림을 붙이기 전에도 동작을 확인할 수 있습니다.

편집 모드에서는 **장치 원시값까지만** 나옵니다. 액션 값과 게임의 실제 장치 상태는 플레이 모드에서만 확인됩니다(아래 한계 참고).

### 3-12. 마을 가상 커서 (패드 전용)

패드에는 포인터가 없어 마을에서 화면의 임의 지점을 가리킬 수 없습니다. 그래서 **가상 커서**를 시스템 쪽에 넣어 두었습니다.

- **토글**: `rightStickPress` (R3). 마을에서만 켜지고, 던전·메인 메뉴에서는 눌러도 반응하지 않습니다. **옵션에서 다른 패드 버튼으로 바꿀 수 있습니다** ([3-5](#3-5-키-설정-화면과-패드-패드-리바인딩-지원됨))
- **켤 때 위치**: 언제나 **화면 중앙**. 마지막 위치를 기억하지 않습니다 (유저가 커서 위치를 예측할 수 있어야 하므로)
- **이동**: 오른쪽 스틱. 커서가 켜져 있는 동안 그 스틱은 캐릭터 조준으로 가지 않습니다
- **자동 해제**: 마우스를 만지면 스스로 꺼집니다. 씬을 벗어날 때도 꺼집니다
- **이동 영역**: 카메라가 실제로 그리는 사각형(`pixelRect`)으로 제한됩니다. 울트라와이드 Pillarbox의 검은 띠로 나가지 않습니다

```csharp
GamepadVirtualCursor _cursor = viewCtx.inputManager.VirtualCursor;

_cursor.ActiveChangedEvent += OnCursorActiveChanged; // bool  — 커서 이미지 보이기/숨기기
_cursor.MovedEvent         += OnCursorMoved;         // Vector2(화면 좌표) — 이미지 옮기기

// 폴링해도 됩니다
bool    _bOn = viewCtx.inputManager.IsVirtualCursorActive;
Vector2 _pos = _cursor.ScreenPosition;
```

**UI 작업자가 할 일**

| 할 일 | 비고 |
|---|---|
| 커서 스프라이트를 화면 좌표에 그리기 | 좌하단 원점 픽셀 좌표. 마우스 좌표와 같은 좌표계입니다 |
| 그 좌표로 무엇을 집을지 정하기 | 월드 레이캐스트 / UI 히트 테스트 — 마우스 클릭 경로를 그대로 재사용하면 됩니다 |
| "집기" 버튼 정하기 | 시스템은 좌표만 줍니다. RT를 클릭으로 볼지, A로 볼지는 UI/기획 결정입니다 |
| 켜져 있을 때 조작 안내 바꾸기 | 선택 사항 |

> 시스템은 **좌표 하나만** 책임집니다. 커서 위치를 마우스로 흘려보내거나 클릭을 대신 만들어 주지 않습니다. 그렇게 하면 캐릭터 조준이 커서를 따라 도는 등 게임플레이가 함께 끌려가기 때문입니다.

속도·데드존·반응 곡선은 [`InputDeviceSettings`](InputDevice/InputDeviceSettings.cs)의 `Gamepad Virtual Cursor` 항목에서 조절합니다. 속도는 픽셀/초가 아니라 **초당 화면 높이 배수**라, 스팀덱(1280x800)과 울트라와이드에서 체감이 같습니다.

---

## 4. 알려진 한계

### 자동 테스트로 검증할 수 없는 것

Input System의 **액션은 Dynamic 업데이트에서만 처리**되고(`ProcessEventsInDynamicUpdate`), 버튼의 "이번 프레임 눌림"도 에디터에서는 항상 false입니다. 그래서 EditMode 테스트로는 다음을 확인할 수 없습니다.

- 패드 조준(`AimEvent`)의 실제 동작
- 패드 버튼 입력 전반, 키보드 `anyKey` 경로
- 리바인딩 대기·취소

바인딩 해석과 구조는 자동으로 검증되지만, **위 항목은 플레이 모드에서 사람이 한 번은 눌러봐야 합니다.** 진단 창(3-7)의 체크리스트를 쓰세요.

### Steam Input

**Steam Input이 켜져 있으면 DualSense/DualShock도 XInput 가상 패드로 위장해서 들어옵니다.** 이 경우 자동 판별은 Xbox로 나오며, 이건 어떤 판별 로직으로도 뚫을 수 없습니다. 대응은 두 가지입니다.

1. Steamworks 파트너 설정에서 이 게임의 Steam Input을 **비활성**으로 두어 Unity가 raw HID를 직접 받게 한다. (권장)
2. 옵션에 "버튼 표기" 수동 지정 항목을 두어 유저가 직접 고르게 한다. (`SetGamepadIconSetOverride`)

실제 출시작 대부분은 **둘 다** 합니다. 자동 판별은 서드파티 어댑터나 모드 전환형 패드(8BitDo 등)에서도 틀리기 때문에, 수동 지정은 사실상 필수입니다.
