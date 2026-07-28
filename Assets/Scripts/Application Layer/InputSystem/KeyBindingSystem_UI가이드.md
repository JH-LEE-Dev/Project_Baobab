# 키 설정(리바인딩) 시스템 - UI 작업 가이드

키 리바인딩의 실제 처리는 `InputManager` / `InputReader`가 담당합니다. UI는 `viewCtx.inputManager`를 통해서만 접근하면 되고, Input System(`InputAction` 등) 타입을 직접 다룰 필요는 없습니다.

- 실행부: [InputReader.cs](InputReader/InputReader.cs)
- 위임부(UI가 실제로 호출하는 곳): [InputManager.cs](InputManager/InputManager.cs)
- 타입 정의: [RebindTypes.cs](InputReader/RebindTypes.cs)

## 1. 동작 방식 요약 (편집 세션)

키 설정 창은 **"편집 세션"** 개념으로 동작합니다. 창을 여는 순간부터 닫을 때까지의 변경사항은 메모리에만 쌓이고, **저장 버튼을 눌러야만** 실제 파일에 기록됩니다.

```
창 열기        → BeginEditSession()   // 현재 상태 스냅샷 저장
  ├─ 리바인딩/리셋 → 메모리에만 반영 (즉시 저장 안 됨)
  ├─ [저장] 클릭  → CommitEditSession()  // 중복 없으면 파일에 기록, true 반환
  └─ [취소]/닫기  → DiscardEditSession() // 스냅샷 시점으로 전부 되돌림
```

**중요**: `StartRebind`, `ResetBinding`, `ResetAllBindings`은 파일에 즉시 저장되지 않습니다. 오직 `CommitEditSession()`만 실제로 저장합니다. 창을 닫을 때 `CommitEditSession()`과 `DiscardEditSession()` 둘 중 하나는 반드시 호출해야 변경사항이 유실되거나 다음에 창을 열었을 때 이상한 상태가 남지 않습니다.

## 2. API 레퍼런스 (`viewCtx.inputManager`)

| 메서드 | 반환값 | 설명 |
|---|---|---|
| `GetRebindableActions()` | `IReadOnlyList<ERebindableAction>` | 리바인딩 가능한 전체 액션 목록. 이 순서대로 리스트 UI를 그리면 됨 |
| `GetBindingDisplayString(action)` | `string` | 화면에 텍스트로 보여줄 표시 문자열 (예: `"W"`, `"Left Shift"`) |
| `GetBindingPath(action)` | `string` | 아이콘 매핑용 원본 컨트롤 경로 (예: `"<Keyboard>/w"`). 로케일에 흔들리지 않으므로 **아이콘 스프라이트는 이 값 기준으로 매핑**할 것 |
| `IsConflicting(action)` | `bool` | 이 액션이 다른 액션과 키가 겹치는 상태인지 (행 하이라이트용) |
| `HasAnyConflict()` | `bool` | 편집 세션 내 중복이 하나라도 있는지 (저장 버튼 활성화 여부) |
| `IsRebinding` | `bool` | 현재 키 입력을 기다리는 중인지 |
| `BeginEditSession()` | - | **창을 열 때** 1회 호출 |
| `StartRebind(action, onFinished)` | - | 해당 액션의 키 입력 대기 시작 |
| `CancelRebind()` | - | 키 입력 대기 중단 (원래 키 유지) |
| `ResetBinding(action)` | - | 해당 액션만 기본 키로 되돌림 (메모리에만) |
| `ResetAllBindings()` | - | 전체를 기본 키로 되돌림 (메모리에만) |
| `CommitEditSession()` | `bool` | **저장 버튼.** 중복 있으면 저장 안 하고 `false` |
| `DiscardEditSession()` | - | **취소/닫기.** `BeginEditSession()` 시점으로 되돌림 |

변경 알림 이벤트는 `InputManager`가 아니라 `inputReader`에 있습니다 (다른 입력 이벤트들과 동일한 접근 관례):

```csharp
viewCtx.inputManager.inputReader.KeyBindingsChangedEvent += RefreshList;
```

리바인딩 완료/취소, 리셋, 세션 취소(Discard) 시 모두 이 이벤트가 발생합니다. **리스트 갱신은 각 API의 반환값이 아니라 이 이벤트 하나만 구독해서 처리**하면 됩니다.

### `ERebindResult` (StartRebind 콜백 결과)
```csharp
public enum ERebindResult { Success, Canceled, Duplicate }
```
- `Duplicate`여도 키는 **그대로 적용됩니다** (편집 세션 동안은 중복 허용). UI는 경고 토스트만 띄우면 되고, 되돌리는 처리는 하지 않아도 됩니다. 실제 저장 차단은 `HasAnyConflict()` / `CommitEditSession()`이 담당합니다.

## 3. 사용 예시 (UIView 기준)

```csharp
public class UIView_KeyBinding : UIView
{
    [SerializeField] private Button saveButton;

    protected override void OnShow()
    {
        viewCtx.inputManager.BeginEditSession();
        viewCtx.inputManager.inputReader.KeyBindingsChangedEvent += RefreshList;
        RefreshList();
    }

    protected override void OnHide()
    {
        viewCtx.inputManager.inputReader.KeyBindingsChangedEvent -= RefreshList;

        // 안전장치: 저장 버튼을 거치지 않고 닫히는 모든 경로(ESC로 닫기 등)에서
        // 커밋되지 않은 변경분이 남지 않도록 항상 호출한다.
        // 이미 저장을 마친 상태라면 스냅샷이 저장 시점으로 갱신되어 있어 아무 변화도 없다(안전한 no-op).
        viewCtx.inputManager.DiscardEditSession();
    }

    private void RefreshList()
    {
        saveButton.interactable = !viewCtx.inputManager.HasAnyConflict();

        foreach (ERebindableAction action in viewCtx.inputManager.GetRebindableActions())
        {
            string path = viewCtx.inputManager.GetBindingPath(action);      // 아이콘 매핑
            string text = viewCtx.inputManager.GetBindingDisplayString(action); // 텍스트 표시
            bool conflict = viewCtx.inputManager.IsConflicting(action);     // 행 경고 표시
            // TODO: 행 UI 갱신 (아이콘/텍스트/경고색)
        }
    }

    private void OnRebindButtonClicked(ERebindableAction action)
    {
        ShowWaitingForKeyOverlay(); // "키를 누르세요" 오버레이

        viewCtx.inputManager.StartRebind(action, (result, conflict) =>
        {
            HideWaitingForKeyOverlay();

            if (result == ERebindResult.Duplicate)
            {
                ShowWarningToast($"이미 [{GetLabel(conflict.Value)}]에 할당된 키입니다.");
            }
            // Success/Canceled/Duplicate 모두 KeyBindingsChangedEvent가 이미 발생시켰으므로
            // 여기서 리스트를 직접 갱신할 필요는 없음
        });
    }

    private void OnResetAllClicked()
    {
        viewCtx.inputManager.ResetAllBindings();
    }

    private void OnSaveClicked()
    {
        if (viewCtx.inputManager.CommitEditSession())
        {
            Hide(); // 또는 이전 화면으로
        }
        // false가 반환되는 경우는 사실상 없음: 버튼이 HasAnyConflict로 이미 비활성화되어 있어야 함
    }

    private void OnCancelClicked()
    {
        Hide(); // OnHide()의 안전장치가 DiscardEditSession()을 호출해 줌
    }
}
```

## 4. 리바인딩 가능한 액션 및 기본 키

`ERebindableAction`에 정의된 항목과 기본값입니다. `ESC`, `Mouse`(마우스 이동), `Click`(좌클릭)은 시스템 예약/포인터 입력이라 리바인딩 대상에서 제외되어 있습니다.

| ERebindableAction | 기본 키 | 비고 |
|---|---|---|
| `MoveUp` | W | 이동은 컴포지트라 4방향이 각각 독립된 항목 |
| `MoveDown` | S | |
| `MoveLeft` | A | |
| `MoveRight` | D | |
| `Inventory` | Space | |
| `Interaction` | E | |
| `SwitchMode` | Tab | |
| `AxeMode` | 1 | |
| `RifleMode` | 2 | |
| `Reload` | R | |
| `AimCorrection` | Shift | |
| `PotionKey` | Q | |

`ERebindableAction`이라는 이름 자체는 코드 식별자일 뿐이므로, 화면에 보여줄 한글 라벨(예: `Interaction` → "상호작용")은 UI 쪽에서 별도 매핑 테이블이나 로컬라이징 키로 관리해야 합니다.

## 5. 주의사항

- **ESC로 리바인딩 취소**: 키 입력 대기 중 ESC를 누르면 항상 리바인딩이 취소됩니다 (그 키 자체를 새 바인딩으로 지정할 수 없음). 별도 취소 버튼을 만들고 싶다면 `CancelRebind()`를 호출하세요.
- **세션을 반드시 마무리할 것**: 위 예시처럼 `OnHide()`에서 항상 `DiscardEditSession()`을 호출하도록 해두세요. 저장 버튼으로 이미 커밋된 상태라면 안전한 no-op이고, ESC로 닫기처럼 별도 취소 버튼을 거치지 않는 경로에서도 커밋되지 않은 변경분이 남지 않게 됩니다.
- **마우스 제외**: 리바인딩 캡처는 마우스 컨트롤을 무시하도록 되어 있어 (`WithControlsExcluding("Mouse")`) 실수로 마우스 이동/클릭이 키로 잡히지 않습니다.
- **아이콘 매핑은 `GetBindingPath` 기준으로**: `GetBindingDisplayString`은 사람이 읽기 위한 문자열이라 표시 형태가 바뀔 수 있습니다. 아이콘 스프라이트 딕셔너리의 키로는 `GetBindingPath`가 주는 원본 경로(`"<Keyboard>/w"` 등)를 쓰세요.
