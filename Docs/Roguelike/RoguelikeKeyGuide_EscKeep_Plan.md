# 로그라이크 키 안내 수정 — ESC(뒤로가기 · 닫기) 안내는 키보드에서도 유지

> 작업자용 정밀 계획표. 직전 작업(`RoguelikeKeyGuide_PadOnly_Plan.md`)의 후속이다.
>
> 사용자 결정(2026-11-10): 키보드 단축키는 **뒤로가기 / 종료(ESC)** 만 남긴다. 키보드·마우스 모드에서도 ESC 안내는 보이고 ESC도 동작한다. 그 밖의 키보드 단축키 안내(탭 전환 PageUp/PageDown)는 패드 모드에서만 보인다.
>
> 프로젝트 규칙은 `CLAUDE.md`를 따른다 — 한국어 주석, Yoda 표기, 명시적 접근 제어자, LINQ · 클로저 람다 금지, 미사용 코드 정리. 이 문서 범위 밖의 파일은 수정하지 않는다. 계획과 다르면 고치지 말고 보고한다.

## 0. 결과 모습 (키보드·마우스 모드)

| 화면 | 키 안내 줄 |
|---|---|
| 기록 창 | `[ESC] 뒤로`만 보인다. `[PageUp][PageDown] 탭 전환`은 숨긴다. 패드 모드에서는 `[LB][RB] 탭 전환 [B] 뒤로` 그대로 |
| 보유 카드 창 | `[ESC] 닫기`가 보인다(직전 작업 이전으로 되돌림). 이동 안내 `[스틱] 이동`은 패드 모드에서만 보인다(원래 동작) |

- PageUp/PageDown 탭 전환 차단(`UIView_RoguelikeRecords.OnTabShift`의 패드 검사)은 **유지**한다.
- 판 크기 · 위치는 바꾸지 않는다. 키 안내 줄은 `HorizontalLayoutGroup` + `ContentSizeFitter`라 꺼진 항목은 자동으로 빠지고, `[ESC] 뒤로`가 줄의 왼쪽 시작점으로 붙는다.

## 1. 기록 창 — `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeRecords.cs`

### 1-1. 필드 교체

직전 작업에서 추가한 아래 2줄을 지운다.

```csharp
    [SerializeField, Tooltip("키 안내 줄(KeyGuide). 패드 모드에서만 켠다 - 키보드·마우스 모드는 마우스 조작이라 키 안내와 단축키를 두지 않는다.")]
    private RectTransform guideRow;
```

같은 자리에 다음을 넣는다.

```csharp
    [SerializeField, Tooltip("탭 전환 안내 항목(이전 탭 키 · 다음 탭 키 아이콘 · '탭 전환' 문구). 패드 모드에서만 켠다 - 키보드는 탭 전환 단축키를 두지 않는다(◀ ▶ 클릭). '뒤로' 안내는 늘 보인다.")]
    private RectTransform[] tabGuideItems = new RectTransform[0];
```

### 1-2. 메서드 교체

`RefreshGuideRow()` 메서드 전체를 지우고 다음으로 바꾼다.

```csharp
    /// <summary>탭 전환 안내는 패드 모드에서만 보인다(키보드는 탭 전환 단축키 없음 - ◀ ▶ 클릭). '뒤로'(ESC / B) 안내는 늘 보인다. 판 크기는 그대로 둔다.</summary>
    private void RefreshTabGuide()
    {
        bool bPad = null != inputManager && true == inputManager.IsGamepadMode;
        for (int i = 0; tabGuideItems.Length > i; i++)
        {
            if (null != tabGuideItems[i]) tabGuideItems[i].gameObject.SetActive(bPad);
        }
    }
```

`RefreshGuideRow()`를 부르는 두 곳(`OnRecordsPresented`, `OnDeviceChanged`)을 `RefreshTabGuide()`로 바꾼다. `OnDeviceChanged`의 나머지는 그대로다.

### 1-3. 클래스 주석

직전 작업에서 넣은 줄

```csharp
///   키 안내 줄은 패드 모드에서만 보인다(키보드·마우스 모드는 마우스 조작). 키보드 Esc 닫기는 안내 없이 그대로 동작한다.
```

을 다음으로 바꾼다.

```csharp
///   키 안내: 탭 전환 안내는 패드 모드에서만 보인다. 뒤로(키보드 Esc / 패드 B) 안내는 늘 보인다 - 키보드 단축키는 뒤로가기 · 닫기만 둔다.
```

## 2. 기록 창 구성기 — `Assets/Scripts/Editor/Roguelike/RoguelikeLobbyUIBuilder.cs`

`BuildRecords()`에서 직전 작업이 추가한

```csharp
            Set(so, "guideRow", guide);
```

를 다음으로 바꾼다(`prevKey` · `nextKey` · `hintTab`은 같은 메서드의 지역 변수다).

```csharp
            SetArray(so, "tabGuideItems", prevKey.transform, nextKey.transform, hintTab.rectTransform);
```

> `SetArray`는 `params UnityEngine.Object[]`를 받는다. `Transform` · `RectTransform`은 Object라 그대로 넘긴다. 필드 타입이 `RectTransform[]`이므로 `prevKey.transform`은 실제로 RectTransform이다(`NewRect`로 만든 오브젝트).

## 3. 보유 카드 창 — `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeOwnedCards.cs` (직전 작업 되돌리기)

1. 클래스 `<summary>`의 줄
   `///   키 안내 줄: 패드 = [스틱] 이동 [B] 닫기. 키 안내 줄은 패드 모드에서만 보인다(키보드·마우스 모드는 Esc 닫기가 안내 없이 동작한다).`
   를 원래대로 되돌린다.
   `///   키 안내 줄: 키보드 = [Esc] 닫기, 패드 = [스틱] 이동 [B] 닫기(이동 안내는 패드 모드에서만 보인다).`
2. `ApplyDevice` 위 `<summary>`에서 직전 작업이 덧붙인 줄 `/// 키 안내 줄 전체가 패드 모드에서만 보인다.`를 지운다.
3. `ApplyDevice`에서 직전 작업이 넣은 두 줄을 지운다.
   ```csharp
           // 키 안내 줄 전체(이동 · 닫기)는 패드 모드에서만 보인다. 키보드·마우스 모드는 마우스 조작(딤 클릭으로 닫기)이고 Esc 닫기는 안내 없이 동작한다.
           keyGuideRoot.gameObject.SetActive(_bGamepad);
   ```
4. `OnShowContent`에서 직전 작업이 넣은 한 줄 `keyGuideRoot.gameObject.SetActive(bGamepadMode);`를 지운다.

> 되돌린 뒤에는 `git diff`로 이 파일의 변경이 **0줄**(직전 작업 이전과 동일)인지 확인한다. 이 파일은 이번 세션에서 다른 작업이 건드리지 않았다.

## 3.5 키 안내 아이콘 높이 1px 맞춤 (스프라이트 영역 수정)

### 원인 (실측)

- 키 아이콘은 모두 16×16 칸이다. 칸 안에서 그림이 차지하는 줄(위에서부터)을 재면 다음과 같다.
  - 키보드 키캡 61개(ESC · Tab · A~Z · 숫자 · F키 · 방향키 등)는 2~12줄에 있다.
  - **PageUp · PageDown 2개만 1~11줄**에 있어, 화면에서 ESC보다 1px 위로 뜬다.
- PlayStation 어깨 버튼 **PS_L1 · PS_R1은 1~13줄**이고 PS_B(○)는 1~14줄이다. 그래서 아랫변이 1px 위다.
  - Xbox는 LB · RB가 0~14줄, B가 1~14줄이라 아랫변이 맞다.
- 키 안내 줄의 배치 코드 문제는 아니다(모두 16×16, 같은 줄에서 세로 가운데 정렬).

### 수정 방법

그림 파일은 건드리지 않는다. 스프라이트 영역(`.meta`의 `rect.y`)만 1 올려 칸 안의 그림을 1px 아래로 보이게 한다.
- 텍스처 좌표는 아래가 0이라 `y`를 늘리면 그림이 아래로 내려간다.
- 새로 포함되는 맨 윗줄은 이웃 칸과의 빈 줄이라 다른 그림이 끼어들지 않는다. 시안 캡처로 확인했다.

| 파일 | 스프라이트 이름 | 바꿀 값 |
|---|---|---|
| `Assets/Graphics/HUD/Keyboard/kb_light_all.png.meta` | `name: PageUp`(약 4964행, `PageUp_Outline` 등 다른 이름 아님) | `rect` 아래 `y: 240` → `y: 241` |
| 같은 파일 | `name: PageDown`(약 5316행) | `y: 224` → `y: 225` |
| `Assets/Graphics/HUD/GamePad/tilemap.png.meta` | `name: PS_L1`(약 16500행) | `y: 86` → `y: 87` |
| 같은 파일 | `name: PS_R1`(약 16522행) | `y: 86` → `y: 87` |

- **텍스트 편집(Edit 도구)으로 해당 스프라이트 블록의 `y:` 한 줄만** 바꾼다.
  - `TextureImporter.spritesheet` 같은 API로 다시 쓰지 않는다. 내부 ID가 바뀌면 `KeyIconDatabase`의 참조가 끊길 수 있다.
  - Edit의 `old_string`에는 `name: PageUp` 줄부터 `y:` 줄까지 포함해 유일하게 맞춘다.
- 편집 뒤 RunCommand로 `AssetDatabase.ImportAsset("<png 경로>", ImportAssetOptions.ForceUpdate);`를 두 텍스처에 각각 실행한다.
- 이 아이콘들은 옵션 창의 키 설정 줄 등 다른 UI에서도 같이 쓰인다. 다른 키 아이콘과 높이가 같아지는 방향이라 의도된 변화다.

### 확인

1. `KeyIconDatabase.asset`(`Assets/Prefabs/UI/Option/KeyIconDatabase.asset`)을 SerializedObject로 열어 `<Keyboard>/pageUp`, `<Keyboard>/pageDown`, playStationEntries의 `<Gamepad>/leftShoulder`, `<Gamepad>/rightShoulder` 아이콘이 null이 아니고 이름이 각각 PageUp / PageDown / PS_L1 / PS_R1인지 로그한다.
2. 각 스프라이트의 `rect`를 로그한다(y가 241 / 225 / 87 / 87).
3. 칸 안 그림 줄을 다시 잰다(PNG 바이트를 `ImageConversion.LoadImage`로 읽은 새 Texture2D에서 알파 > 0.1인 행).
   - 기대값: PageUp · PageDown은 2~12줄, PS_L1 · PS_R1은 2~14줄.
   - 위에서부터 줄 번호 = `15 - (아래 기준 행)`.

## 4. 컴파일 · 프리팹

1. RunCommand로 `AssetDatabase.Refresh()` → 콘솔 에러 0(`SteamAPI_Init() failed`는 무시).
2. RunCommand로 **`RoguelikeLobbyUIBuilder.BuildRecords();`만** 실행한다.
3. `Assets/Prefabs/Roguelike/UI/UIView_RoguelikeRecords.prefab`에서 `tabGuideItems` 길이가 3이고 `TabPrevKey` · `TabNextKey` · `HintTab`을 가리키는지 SerializedObject로 확인한다. 직렬화에 `guideRow`가 남아 있어도 필드가 없으면 무시되므로 문제없다.

## 5. 검수 (플레이 모드, 키보드·마우스 모드)

직전 작업과 같은 방법을 쓴다(스플래시 건너뛰기 → `UIView_MainMenu.OpenRoguelikeRecords(null)` / 보유 카드 프리팹 Instantiate · `SetCards` · `Show`).

- 기록 창: `KeyGuide` 아래 `TabPrevKey` · `TabNextKey` · `HintTab`의 activeSelf가 false이고, `BackKey` · `HintBack`이 true인지 로그 → 캡처 `.../scratchpad/esckeep_records.png`
- 보유 카드 창: `keyGuideRoot` activeSelf가 true, `moveGuideRow` activeSelf가 false인지 로그 → 캡처 `.../scratchpad/esckeep_owned.png`
- 아이콘 높이: 기록 창 캡처에서 `[ESC]` 아이콘 하나만 보이므로, `KeyGuide`와 그 자식을 잠시 모두 켜서 `[PageUp][PageDown] 탭 전환 [ESC] 뒤로`를 한 번 더 캡처한다(`.../scratchpad/esckeep_records_all.png`). 세 아이콘의 밝은 픽셀 위·아래 끝 y가 같은지 PIL 등으로 재서 보고한다. 이 확인용 조작은 저장하지 않는다(플레이 모드 안에서만).
- 캡처 폴더: `C:/Users/xarek/AppData/Local/Temp/claude/8df19ef0c106e315/d69c03a3-0c6e-470d-a213-872744a0bd1d/scratchpad/`
- 플레이 종료 후 콘솔 에러 0.
- 실제 키 · 패드 입력은 검수하지 못했다고 보고한다.

## 6. 보고

파일별 전/후, 프리팹 확인 값, 로그, 캡처 경로, 콘솔 결과, 검수하지 못한 항목. 계획과 다르면 고치지 말고 보고한다.
