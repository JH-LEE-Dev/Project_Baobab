# 로그라이크 UI 키 안내 · 키보드 단축키 패드 전용화 작업 계획표

> 작업자용 정밀 계획표. 사용자 결정(2026-11-10):
> - 키보드·마우스 모드에서는 키 안내를 숨기고 키보드 단축키(PageUp/PageDown 탭 전환)를 막는다. 게임패드 모드에서만 보이고 동작한다.
> - 빈칸 처리는 **가-안(숨기기만)**: 판 크기 · 위치는 바꾸지 않는다.
> - **ESC 닫기는 유지**: 키보드 ESC로 창이 닫히는 동작은 그대로 둔다(안내만 숨김).
>
> 프로젝트 규칙은 `CLAUDE.md`를 따른다 — 한국어 주석, Yoda 표기(`null != x`, `true == b`), 명시적 접근 제어자, 기존 변수명 변경 금지, LINQ · 클로저 람다 금지, 미사용 코드 정리, 필요한 `using` 확인.
> 이 문서 범위 밖의 파일은 수정하지 않는다. 막히면 임의로 다른 방법을 쓰지 말고 보고한다.

## 0. 범위

| 화면 | 바꿀 것 | 이유 |
|---|---|---|
| 기록 창 `UIView_RoguelikeRecords` | ① 키 안내 줄(PageUp · PageDown 탭 전환, ESC 뒤로)을 패드 모드에서만 보이게 ② PageUp/PageDown 탭 전환을 패드 모드에서만 받기 | 지금은 키보드 모드에서도 안내가 보이고 PageUp/PageDown이 동작한다 |
| 보유 카드 창 `UIView_RoguelikeOwnedCards` | 키 안내 줄 전체(「ESC 닫기」 포함)를 패드 모드에서만 보이게 | 지금은 이동 안내만 패드 전용이고 「ESC 닫기」는 키보드 모드에서도 보인다 |

**바꾸지 않는 것**:
- 카드 선택 · 결과 화면: 이미 패드 전용이다.
- ESC 닫기 동작(기록 창 `escHandler` / 깊이 관리, 보유 카드 창 `bCloseableByESC`).
- 패드 B 닫기(`UICancelEvent`).
- 마우스 조작: ◀ ▶ 클릭, 딤 클릭 닫기.
- 보유 카드 창을 여는 인벤토리 키(Tab)와 HUD 트레이의 키 아이콘. 키보드 모드에서 창을 여는 유일한 수단이다.
- 판 크기 · 위치(가-안).

## 1. 기록 창 — `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeRecords.cs`

### 1-1. 필드 추가

`[Header("키 안내")]` 아래의 `hintTabText` 위에 추가한다.

```csharp
    [SerializeField, Tooltip("키 안내 줄(KeyGuide). 패드 모드에서만 켠다 - 키보드·마우스 모드는 마우스 조작이라 키 안내와 단축키를 두지 않는다.")]
    private RectTransform guideRow;
```

캐싱 델리게이트 영역(`private Action languageChangedHandler;` 아래)에 추가한다.

```csharp
    private Action<EInputDeviceType> deviceChangedHandler;
```

### 1-2. Initialize

`languageChangedHandler = OnLanguageChanged;` 아래에 추가한다.

```csharp
        deviceChangedHandler = OnDeviceChanged;
```

### 1-3. OnShowContent — 장치 전환 구독

`inputManager.inputReader.ESCButtonPressedEvent` 두 줄 바로 아래(같은 `if` 블록 안)에 추가한다.

```csharp
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
            inputManager.inputReader.InputDeviceChangedEvent += deviceChangedHandler;
```

### 1-4. UnbindInput — 구독 해제

`inputManager.inputReader.ESCButtonPressedEvent -= escHandler;` 아래에 추가한다.

```csharp
            inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
```

### 1-5. 처음 열 때 반영

`OnRecordsPresented()`에서 `ApplyStaticTexts();` 바로 아래에 추가한다. 장치 이벤트는 "변화"만 알리므로 열린 시점의 장치를 한 번 직접 반영한다(가이드 3-1).

```csharp
        RefreshGuideRow();
```

### 1-6. 탭 전환 입력 막기

`OnTabShift`를 다음으로 바꾼다.

```csharp
    private void OnTabShift(int _direction)
    {
        // 키보드 PageUp/PageDown 탭 전환은 두지 않는다(키보드·마우스 모드는 ◀ ▶ 클릭). 패드 LB/RB만 받는다.
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        if (0 > _direction) ShowPrevTab();
        else if (0 < _direction) ShowNextTab();
    }
```

> 참고: 키보드 모드에서 패드 LB를 처음 누르면, 장치 전환 이벤트와 탭 이벤트 중 어느 쪽이 먼저 오는지에 따라 그 한 번이 무시될 수 있다. 그다음 입력부터는 정상이다. 허용 범위로 보고 따로 처리하지 않는다.

### 1-7. 새 메서드

`// // 닫기 · 문구` 구역의 `ApplyStaticTexts()` 아래에 추가한다.

```csharp
    /// <summary>키 안내 줄은 패드 모드에서만 보인다(키보드·마우스 모드는 마우스 조작). 판 크기는 그대로 둔다.</summary>
    private void RefreshGuideRow()
    {
        if (null == guideRow) return;
        bool bPad = null != inputManager && true == inputManager.IsGamepadMode;
        guideRow.gameObject.SetActive(bPad);
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (false == IsVisible) return;
        RefreshGuideRow();
    }
```

### 1-8. 클래스 주석

`/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-7] 탭 전환 = UITabShiftEvent(LB/RB · PageUp/PageDown) 또는 명판의 ◀ ▶ 클릭.` 줄을 다음으로 바꾼다.

```csharp
/// [조작 - ControllerSupport_UI가이드.md 3-1 · 3-7] 탭 전환 = 패드 LB/RB(UITabShiftEvent - 키보드 PageUp/PageDown은 받지 않는다) 또는 명판의 ◀ ▶ 클릭.
///   키 안내 줄은 패드 모드에서만 보인다(키보드·마우스 모드는 마우스 조작). 키보드 Esc 닫기는 안내 없이 그대로 동작한다.
```

## 2. 기록 창 구성기 — `Assets/Scripts/Editor/Roguelike/RoguelikeLobbyUIBuilder.cs`

`BuildRecords()`에서 `Set(so, "hintTabText", hintTab);` 바로 위에 한 줄을 추가한다.

```csharp
            Set(so, "guideRow", guide);
```

(`guide`는 같은 메서드의 `RectTransform guide = GuideRow("KeyGuide", panel);`다.)

## 3. 보유 카드 창 — `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeOwnedCards.cs`

`keyGuideRoot`(KeyGuide)는 `keyGuideGroup`(CanvasGroup)과 같은 오브젝트다. 비활성이어도 열기 · 닫기 연출의 알파 쓰기와 `LayoutRebuilder.ForceRebuildLayoutImmediate`는 문제없다. 레이아웃 높이 계산(`LayoutAll`)은 바꾸지 않는다(가-안).

### 3-1. ApplyDevice

```csharp
        moveGuideRow.gameObject.SetActive(_bGamepad);
        LayoutRebuilder.ForceRebuildLayoutImmediate(keyGuideRoot);
```

위 두 줄을 다음으로 바꾼다.

```csharp
        // 키 안내 줄 전체(이동 · 닫기)는 패드 모드에서만 보인다. 키보드·마우스 모드는 마우스 조작(딤 클릭으로 닫기)이고 Esc 닫기는 안내 없이 동작한다.
        keyGuideRoot.gameObject.SetActive(_bGamepad);
        moveGuideRow.gameObject.SetActive(_bGamepad);
        LayoutRebuilder.ForceRebuildLayoutImmediate(keyGuideRoot);
```

이 메서드 위의 `<summary>` 주석에 "키 안내 줄 전체가 패드 모드에서만 보인다"는 내용을 한 줄 덧붙인다.

### 3-2. OnShowContent

```csharp
        moveGuideRow.gameObject.SetActive(bGamepadMode);
```

위 줄을 다음으로 바꾼다.

```csharp
        keyGuideRoot.gameObject.SetActive(bGamepadMode);
        moveGuideRow.gameObject.SetActive(bGamepadMode);
```

### 3-3. 클래스 주석

파일 상단 클래스 `<summary>`에 키 안내가 언급되어 있으면 "키 안내 줄은 패드 모드에서만 보인다"로 맞춘다. 없으면 건드리지 않는다.

> 보유 카드 창은 구성기 · 프리팹 변경이 없다(필드가 이미 연결돼 있다).

## 4. 컴파일 · 프리팹 재구성

1. Unity MCP의 RunCommand로 `AssetDatabase.Refresh()` → 콘솔 에러 0을 확인한다(`SteamAPI_Init() failed`는 무시).
2. RunCommand로 **`RoguelikeLobbyUIBuilder.BuildRecords();`만** 실행한다. `BuildAll` · `BuildMainMenu` · `BuildResult`는 실행하지 않는다.
3. `Assets/Prefabs/Roguelike/UI/UIView_RoguelikeRecords.prefab`의 `UIView_RoguelikeRecords`에서 `guideRow`가 `KeyGuide`를 가리키는지 SerializedObject로 확인한다.

## 5. 검수 (플레이 모드, 키보드·마우스 모드)

MCP 팁: Unity 창이 비활성이면 시간이 명령마다만 흐르므로 `EditorApplication.QueuePlayerLoopUpdate()`만 하는 명령을 몇 번 보내며 기다린다. RunCommand에서는 System.Reflection과 DOTween을 쓸 수 없다.

1. 플레이 → `Object.FindAnyObjectByType<UIView_MainMenu>().OnPressAnyKeyCompleted();` → 메인메뉴 버튼이 보일 때까지 기다림.
2. **기록 창**: `UIView_MainMenu.OpenRoguelikeRecords(null)` → 기다림 → 캡처.
   - 확인: 키 안내 줄 없음, 판 크기 · 위치는 이전과 같음(가-안이라 아래쪽 빈칸이 남는 것이 정상).
   - `KeyGuide` 오브젝트의 `activeSelf`가 false인지 로그.
3. **보유 카드 창**: 메인메뉴 캔버스에 `UIView_RoguelikeOwnedCards` 프리팹을 Instantiate → `var ctx = new UIViewContext(); ctx.Initialize(inputManager, localizationManager, null);`로 만든 컨텍스트로 `view.Initialize(ctx)` → `SetCards`(CardDefinitionSO 몇 장으로 `RoguelikeOwnedCardViewData` 생성) → `Show()` → 기다림 → 캡처.
   - 확인: 「ESC 닫기」 없음. SerializedObject로 `keyGuideRoot`의 `activeSelf`가 false인지 로그.
4. 실제 키 입력(PageUp · ESC)과 패드는 MCP로 누를 수 없으므로 검수하지 않았다고 보고한다.
5. 캡처 경로(이 폴더에 저장):
   - `C:/Users/xarek/AppData/Local/Temp/claude/8df19ef0c106e315/d69c03a3-0c6e-470d-a213-872744a0bd1d/scratchpad/padonly_records.png`
   - `.../scratchpad/padonly_owned.png`
6. 플레이 종료 후 콘솔 에러 0을 확인한다.

## 6. 보고

- 파일별로 바꾼 줄(전/후), 재구성한 프리팹, 로그 값, 캡처 경로, 콘솔 결과를 보고한다.
- 검수하지 못한 항목(실제 키 · 패드)을 명시한다. 계획과 다른 점이 있으면 고치지 말고 보고한다.
