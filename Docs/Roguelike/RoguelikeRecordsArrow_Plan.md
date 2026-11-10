# 기록 창 탭 명판 화살표를 옵션 화살표 스프라이트로 교체 — 작업 계획표

> 작업자용 정밀 계획표. 사용자 요청(2026-11-10): 기록 창 명판의 ◀ ▶(노란 글자)를 옵션 창에서 쓰는 화살표 스프라이트로 바꿔 통일감을 맞춘다. UI 밖으로 삐져나가지 않아야 한다. 시안은 사용자에게 보냈고(명판 안쪽 8px, 밀림 연출 때 6px), 승인을 받았다.
>
> 프로젝트 규칙은 `CLAUDE.md`를 따른다 — 한국어 주석, Yoda 표기, 명시적 접근 제어자, 기존 변수명 변경 금지, LINQ · 클로저 람다 금지, 미사용 코드 정리, 필요한 `using` 확인. 이 문서 범위 밖의 파일은 수정하지 않는다. 계획과 다르면 고치지 말고 보고한다.

## 0. 기준값 (실측)

| 항목 | 값 |
|---|---|
| 옵션 화살표 스프라이트 | `Assets/Graphics/HUD/Option/Chevron-Arrow-Left-export.png`, `Assets/Graphics/HUD/Option/Chevron-Arrow-Right-export.png` (각 24×24, Point 필터) |
| 화살표 실제 그림 범위 | 두 장 모두 24×24 칸 안의 x 7~15(폭 9), 위에서 y 5~18(높이 14) |
| 옵션 화살표 색 반응(`UI_OptionButton`, Color 모드) | 평소 흰색(1,1,1,1), 마우스 올림 (0.8,0.8,0.8,1), 누름 (0.6,0.6,0.6,1), 0.1초 |
| 옵션 값 넘김 연출(`UI_OptionSelector.PlayArrowNudge`) | 누른 쪽 화살표를 바깥으로 3px(정수) 옮겼다가 0.1초 OutQuad로 제자리, unscaled |
| 기록 창 명판 `TabPlate` | 높이 25, 폭 = max(238, 탭 이름 폭 + 22×2 + 10), 9-슬라이스 테두리 4 |
| 기록 창 화살표 버튼 `Prev` / `Next` | 명판 왼쪽 / 오른쪽 끝에 붙은 폭 22 · 명판 높이 전체 판정. 지금은 자식 `Glyph`(TMP 글자 ◀ ▶, 금색)를 그린다 |

### 배치 (시안과 동일)

- 화살표 이미지는 24×24, `Prev`는 앵커 · 피벗 (0, 1), 위치 (1, -1). `Next`는 앵커 · 피벗 (1, 1), 위치 (-1, -1).
  - 그림이 명판 가장자리에서 **8px 안쪽**(x 8~16), 세로 6~19줄(명판 25의 가운데)에 온다.
- 탭을 넘길 때 3px 바깥으로 밀려도 **가장자리에서 6px**이라 테두리(4px) 안쪽이다.
- 그림이 끝나는 위치(가장자리에서 17px)는 판정 폭 22 안이다. 명판 폭 공식이 탭 이름과 22px 영역을 이미 떼어 두므로 긴 언어에서도 글자와 겹치지 않는다.
- 이미지 사각형 자체는 왼쪽 1px · 오른쪽 1px · 위 1px 안쪽에 있어 명판 밖으로 나가지 않는다. 밀림 때 투명 여백이 2px 나가지만 보이는 픽셀은 안쪽이다.

## 1. 구성기 — `Assets/Scripts/Editor/Roguelike/RoguelikeLobbyUIBuilder.cs`

### 1-1. 경로 상수

`BasicButtonPath` 상수 아래에 추가한다.

```csharp
    // 옵션 창 선택기(UI_OptionSelector)와 같은 화살표 그림 - 기록 창 탭 명판의 ◀ ▶를 이것으로 통일한다.
    private const string OptionArrowLeftPath = "Assets/Graphics/HUD/Option/Chevron-Arrow-Left-export.png";
    private const string OptionArrowRightPath = "Assets/Graphics/HUD/Option/Chevron-Arrow-Right-export.png";
    private const float ArrowIconSize = 24f;
    private static readonly Color ArrowHoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private static readonly Color ArrowPressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
```

### 1-2. Assets 클래스와 LoadAssets

- `private class Assets` 안 `public Sprite basicButton;` 아래에 `public Sprite arrowLeft;`, `public Sprite arrowRight;`를 추가한다.
- `LoadAssets()`의 객체 초기화에 다음 두 줄을 추가한다.
  ```csharp
  arrowLeft = AssetDatabase.LoadAssetAtPath<Sprite>(OptionArrowLeftPath),
  arrowRight = AssetDatabase.LoadAssetAtPath<Sprite>(OptionArrowRightPath),
  ```
- 같은 메서드의 null 검사 조건에 `|| null == a.arrowLeft || null == a.arrowRight`를 추가한다.
- 에러 메시지 끝에 ` (옵션 화살표 그림 경로도 확인)`을 덧붙인다.

### 1-3. ArrowButton 교체

기존 `private static Button ArrowButton(string _name, RectTransform _parent, TMP_FontAsset _font, string _glyph, bool _bLeft)` 메서드 전체를 아래로 바꾼다. 글자(`_font`, `_glyph`) 인자는 더 이상 쓰지 않으므로 없앤다.

```csharp
    /// <summary>
    /// 탭 명판 끝의 화살표 버튼(판정 폭 22 · 명판 높이 전체). 그림은 옵션 창 선택기와 같은 화살표 스프라이트(24×24)이고
    /// 명판 가장자리에서 8px 안쪽에 온다(탭을 넘길 때 3px 밀려도 테두리 안쪽). 색 반응은 옵션 화살표와 같다(흰색 → 올림 0.8 → 누름 0.6, 0.1초).
    /// </summary>
    private static Button ArrowButton(string _name, RectTransform _parent, Sprite _arrow, bool _bLeft, out RectTransform _icon)
    {
        RectTransform rect = new GameObject(_name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = LayerMask.NameToLayer("UI");
        rect.SetParent(_parent, false);
        rect.anchorMin = new Vector2(_bLeft ? 0f : 1f, 0f);
        rect.anchorMax = new Vector2(_bLeft ? 0f : 1f, 1f);
        rect.pivot = new Vector2(_bLeft ? 0f : 1f, 0.5f);
        rect.sizeDelta = new Vector2(22f, 0f);
        rect.anchoredPosition = Vector2.zero;

        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        // 화살표 그림: 위쪽 기준으로 놓아 명판 높이(25)의 가운데에 정수 픽셀로 온다.
        _icon = NewRect("Arrow", rect);
        _icon.anchorMin = new Vector2(_bLeft ? 0f : 1f, 1f);
        _icon.anchorMax = _icon.anchorMin;
        _icon.pivot = _icon.anchorMin;
        _icon.sizeDelta = new Vector2(ArrowIconSize, ArrowIconSize);
        _icon.anchoredPosition = new Vector2(_bLeft ? 1f : -1f, -1f);
        Image arrowImage = _icon.gameObject.AddComponent<Image>();
        arrowImage.sprite = _arrow;
        arrowImage.raycastTarget = false;

        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = arrowImage;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ArrowHoverColor;
        colors.pressedColor = ArrowPressedColor;
        colors.selectedColor = Color.white;
        colors.disabledColor = ArrowPressedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return button;
    }
```

> 판정(`hit`, 투명)은 버튼 오브젝트에 그대로 두고 레이캐스트를 받는다. 색 반응 대상은 화살표 그림이다.

### 1-4. BuildRecords 호출부

```csharp
            Button prev = ArrowButton("Prev", tabPlate, a.font, "◀", true);
            Button next = ArrowButton("Next", tabPlate, a.font, "▶", false);
```

위 두 줄을 다음으로 바꾼다.

```csharp
            Button prev = ArrowButton("Prev", tabPlate, a.arrowLeft, true, out RectTransform prevArrow);
            Button next = ArrowButton("Next", tabPlate, a.arrowRight, false, out RectTransform nextArrow);
```

같은 메서드의 `Set(so, "nextTabButton", next);` 아래에 추가한다.

```csharp
            Set(so, "prevArrowIcon", prevArrow);
            Set(so, "nextArrowIcon", nextArrow);
```

### 1-5. 정리

`ArrowButton`이 `GoldColor`나 `TMP_Text`를 쓰지 않게 된다.
- `GoldColor`는 다른 곳(결과 · 기록판 값 글자 등)에서도 쓰므로 그대로 둔다.
- `using TMPro`도 다른 곳에서 쓰므로 그대로 둔다.
- 파일 상단 주석에 `◀ 탭 ▶ 명판`이라는 표현이 있으면 `화살표 · 탭 · 화살표 명판(옵션 화살표 그림)`처럼 맞춘다(의미만 맞으면 된다).

## 2. 기록 창 — `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeRecords.cs`

옵션 선택기와 같은 "누른 쪽 화살표가 3px 밀렸다 돌아오는" 반응을 넣는다.

### 2-1. 상수 · 필드

탭 넘김 피드백 상수 영역(`DotCursorSeconds` 아래)에 추가한다.

```csharp
    // 옵션 선택기(UI_OptionSelector.PlayArrowNudge)와 같은 값: 누른 쪽 화살표가 바깥으로 3px 밀렸다가 0.1초 OutQuad로 돌아온다.
    private const float ArrowNudgeDistance = 3f;
    private const float ArrowNudgeSeconds = 0.1f;
```

`[Header("탭 명판")]`의 `nextTabButton` 아래에 추가한다.

```csharp
    [SerializeField, Tooltip("이전 탭 버튼의 화살표 그림(옵션 화살표). 탭을 넘길 때 밀림 연출에 쓴다.")]
    private RectTransform prevArrowIcon;
    [SerializeField] private RectTransform nextArrowIcon;
```

상태 변수 영역(`bTabTextBaseCaptured` 아래)에 추가한다.

```csharp
    private Vector2 prevArrowBasePosition;
    private Vector2 nextArrowBasePosition;
    private bool bArrowBaseCaptured;
```

### 2-2. 기준 위치 캡처

`CaptureTabTextBase()` 아래에 새 메서드를 추가한다.

```csharp
    private void CaptureArrowBase()
    {
        if (true == bArrowBaseCaptured) return;
        if (null != prevArrowIcon) prevArrowBasePosition = prevArrowIcon.anchoredPosition;
        if (null != nextArrowIcon) nextArrowBasePosition = nextArrowIcon.anchoredPosition;
        bArrowBaseCaptured = true;
    }
```

### 2-3. ResetTabFeedback에 화살표 복귀 추가

`ResetTabFeedback()`의 맨 앞(`CaptureTabTextBase();` 다음 줄)에 추가한다.

```csharp
        CaptureArrowBase();
        if (null != prevArrowIcon)
        {
            prevArrowIcon.DOKill();
            prevArrowIcon.anchoredPosition = prevArrowBasePosition;
        }
        if (null != nextArrowIcon)
        {
            nextArrowIcon.DOKill();
            nextArrowIcon.anchoredPosition = nextArrowBasePosition;
        }
```

`if (null == tabText) return;`보다 **앞**에 넣어, 글자가 없어도 화살표는 복귀하게 한다.

### 2-4. PlayTabSwitchFeedback에 밀림 추가

`PlayTabSwitchFeedback(int _direction)`에서 `ResetTabFeedback();` 바로 아래에 추가한다.

```csharp
        // 누른 쪽 화살표가 바깥으로 밀렸다가 돌아온다(옵션 선택기와 같은 반응). 정수 픽셀, unscaled.
        RectTransform arrow = 0 < _direction ? nextArrowIcon : prevArrowIcon;
        if (null != arrow)
        {
            Vector2 arrowBase = 0 < _direction ? nextArrowBasePosition : prevArrowBasePosition;
            float outward = 0 < _direction ? 1f : -1f;
            arrow.anchoredPosition = new Vector2(arrowBase.x + outward * ArrowNudgeDistance, arrowBase.y);
            arrow.DOAnchorPosX(arrowBase.x, ArrowNudgeSeconds, true).SetEase(Ease.OutQuad).SetUpdate(true);
        }
```

### 2-5. OnDestroy

`OnDestroy()`의 `if (null != tabDotCursor) tabDotCursor.DOKill();` 아래에 추가한다.

```csharp
        if (null != prevArrowIcon) prevArrowIcon.DOKill();
        if (null != nextArrowIcon) nextArrowIcon.DOKill();
```

같은 내용을 `OnHideContent()`의 `if (null != tabDotCursor) tabDotCursor.DOKill();` 아래에도 넣는다. 닫힐 때 밀린 채 남지 않게 하기 위해서다(다음에 열리면 `OnRecordsPresented` → `ResetTabFeedback`이 기준 위치로 되돌린다).

### 2-6. 클래스 주석

클래스 `<summary>`의 "탭 넘김 피드백" 설명(상수 위 주석)에 "누른 쪽 화살표가 3px 밀렸다 돌아온다(옵션 선택기와 같다)"를 한 줄 덧붙인다.

## 3. 컴파일 · 프리팹

1. RunCommand로 `AssetDatabase.Refresh()` → 콘솔 에러 0(`SteamAPI_Init() failed`는 무시).
2. RunCommand로 **`RoguelikeLobbyUIBuilder.BuildRecords();`만** 실행한다.
3. `Assets/Prefabs/Roguelike/UI/UIView_RoguelikeRecords.prefab`에서 다음을 확인해 로그한다.
   - `Panel/TabPlate/Prev/Arrow`, `Panel/TabPlate/Next/Arrow`가 있다. 크기 (24,24), 위치 (1,-1) / (-1,-1), 스프라이트가 Chevron-Arrow-Left/Right-export다.
   - `Glyph`가 없다.
   - 버튼 transition이 ColorTint이고 targetGraphic이 Arrow 이미지다.
   - `UIView_RoguelikeRecords`의 `prevArrowIcon` / `nextArrowIcon`이 각 Arrow를 가리킨다.

## 4. 검수 (플레이 모드)

MCP 팁: Unity 창이 비활성이면 시간이 명령마다만 흐르므로 `EditorApplication.QueuePlayerLoopUpdate()`만 하는 명령을 여러 번 보내 기다린다. RunCommand에서 System.Reflection · DOTween은 쓸 수 없다.

1. 플레이 → `UIView_MainMenu.OnPressAnyKeyCompleted()` → 메인메뉴가 보일 때까지 기다림 → `UIView_MainMenu.OpenRoguelikeRecords(null)` → 기다림.
2. 캡처 `.../scratchpad/arrow_after.png`. PIL로 명판 영역을 확인한다.
   - 1920×1080 기준 명판은 x 약 603~1317, y 약 62~137이다.
   - 밝은 화살표 픽셀(min(r,g,b) > 215)이 명판 테두리 안쪽(가장자리에서 12px = 4 캔버스 px 이상)에 있는지 수치로 보고한다.
3. 밀림 상태 검사: Arrow 두 개의 `anchoredPosition`을 각각 바깥으로 3 옮긴 뒤(왼쪽 -2, 오른쪽 +2) 캡처 `.../scratchpad/arrow_after_nudged.png`. 같은 방식으로 테두리 안쪽인지 보고한다. 확인 뒤 원래 값으로 되돌린다.
4. 긴 언어: `LocalizationManager.SetLanguage(Language.DE)`와 `Language.RU`로 바꾸고 기다린 뒤 캡처한다(`arrow_after_de.png`, `arrow_after_ru.png`). 탭 이름 글자가 화살표와 겹치지 않는지 확인한다. 끝나면 `Language.KR`로 되돌린다.
5. ◀ ▶ 클릭 동작: `Prev` / `Next` 버튼의 `onClick.Invoke()`를 불러 탭이 바뀌는지(탭 이름 글자 변화) 로그한다.
6. 플레이 종료, 콘솔 에러 0.
7. 캡처 폴더: `C:/Users/xarek/AppData/Local/Temp/claude/8df19ef0c106e315/d69c03a3-0c6e-470d-a213-872744a0bd1d/scratchpad/`
8. 마우스 호버 색 변화와 실제 밀림 애니메이션 진행은 MCP로 확인할 수 없으니 검수하지 못했다고 보고한다.

## 5. 보고

파일별 전/후, 프리팹 확인 값, 캡처 경로와 테두리 안쪽 수치, 언어별 결과, 콘솔 결과, 검수하지 못한 항목. 계획과 다르면 고치지 말고 보고한다.
