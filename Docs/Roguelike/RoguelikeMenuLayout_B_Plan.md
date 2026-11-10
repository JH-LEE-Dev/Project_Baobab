# 로그라이크 메뉴 배치 B안(양쪽 끝 대칭) 작업 계획표

> 작업자용 정밀 계획표. 사용자가 시안 B를 골랐다(2026-11-10). 프로젝트 규칙은 `CLAUDE.md`를 따른다 — 한국어 응답, Yoda 표기, 명시적 접근 제어자, 기존 변수명 변경 금지, LINQ · 람다 클로저 금지, 미사용 코드 정리.
> 이 문서 범위 밖의 파일은 수정하지 않는다. 막히면 임의로 다른 방법을 쓰지 말고 사용자에게 묻는다.

## 0. 목표 (시안 B)

- 왼쪽 메뉴(명판 「로그라이크」 + 버튼 4개)와 오른쪽 기록판의 **바깥 여백을 양쪽 16px로 같게** 한다.
- 두 묶음의 **위쪽 끝(명판 윗변)을 같은 줄**에 맞춘다.
- 크기 · 글자 · 연출 · 버튼 간격은 바꾸지 않는다. **위치만** 바꾼다.

### 좌표계

- 캔버스 기준 해상도 640×360(1920×1080에서 3배).
- 아래 표의 y는 **화면 위에서부터의 거리(px)**. `anchoredPosition`의 y는 위가 +라서 부호가 반대다.

| 요소 | 현재 | B안 | 변화 |
|---|---|---|---|
| `RoguelikeMenu` 루트 `anchoredPosition` (앵커 왼쪽 가운데) | (116, 0) | **(121, 18)** | 오른쪽 5, 위 18 |
| 명판 「로그라이크」 x 범위 | 11~127 | 16~132 | 왼쪽 여백 11 → **16** |
| 명판 세로 중심(위에서) | 180 | 162 | |
| 명판 윗변(위에서) | 167.5 | 149.5 | |
| 버튼 중심 y(위에서) | 211 / 235 / 259 / 283 | 193 / 217 / 241 / 265 | |
| `RoguelikeRecordPanel` `anchoredPosition` (앵커 오른쪽 위) | (-16, -148) | **(-16, -162)** | 아래 14 |
| 기록판 판 윗변 = 기록판 명판 세로 중심(위에서) | 148 | 162 | 왼쪽 명판 중심과 같은 줄 |
| 기록판 명판 윗변(위에서) | 135.5 | 149.5 | 왼쪽 명판 윗변과 같은 줄 |
| 기록판 아래끝(위에서, 판 높이 150) | 298 | 312 | 버전 글자(위끝 약 338)와 26px 떨어짐 |
| 기록판 오른쪽 여백 | 16 | 16 | 유지 |

- 기록판의 명판(`TitlePlate`)은 판 위쪽 가장자리에 세로 중심이 걸친다(`SlicedChild` 피벗 0.5). 그래서 판 윗변 = 명판 세로 중심이다.
- 판 높이는 언어와 무관하게 줄 수가 고정이라 약 150이다(`UI_RoguelikeMenuRecordPanel.Layout`).

## 1. 수정 파일 (1개)

`Assets/Scripts/Editor/Roguelike/RoguelikeLobbyUIBuilder.cs` — `BuildMainMenu()` 안 두 곳과 주석.

> `UI_MainMenu.cs`는 수정하지 않는다. 확인한 근거:
> - 하위 메뉴 루트의 위치를 쓰는 곳은 `SlideRoguelikeMenu`(처음 슬라이드 때 현재 위치를 `roguelikeMenuBasePosition`으로 캡처)와 `CloseRoguelikeMenuImmediate`(캡처한 기준 위치로 복귀)뿐이다. 둘 다 프리팹 위치를 기준으로 삼으므로 프리팹 값만 바꾸면 따라온다.
> - 기록판도 `UI_RoguelikeMenuRecordPanel.CaptureBase()`가 현재 위치를 기준으로 캡처한다.

### 1-1. 상수 추가

클래스 상단 상수 영역(`KeyIconSize` 아래)에 추가한다.

```csharp
    // 로그라이크 메뉴 배치(B안 - 양쪽 끝 대칭). 640×360 기준.
    // 하위 메뉴 루트는 메인 버튼 묶음(BTNs) 위치에서 이만큼 옮긴다: 왼쪽 여백 11 → 16, 명판 윗변을 기록판 명판 윗변과 같은 줄로.
    private static readonly Vector2 RoguelikeMenuOffset = new Vector2(5f, 18f);
    // 기록판(오른쪽 위 기준): 오른쪽 여백 16, 판 윗변(= 명판 세로 중심) 162.
    private static readonly Vector2 RecordPanelPosition = new Vector2(-16f, -162f);
```

### 1-2. 하위 메뉴 루트 위치

`BuildMainMenu()`의 아래 줄을 찾는다.

```csharp
            menuRoot.anchoredPosition = buttons.anchoredPosition;
```

바꾼다.

```csharp
            menuRoot.anchoredPosition = buttons.anchoredPosition + RoguelikeMenuOffset;
```

바로 위 주석 `// 하위 메뉴 묶음: BTNs와 같은 자리(왼쪽 가운데 기준)`은 다음으로 고친다.

```csharp
            // 하위 메뉴 묶음: BTNs 자리(왼쪽 가운데 기준)에서 RoguelikeMenuOffset만큼 옮긴다(B안 - 왼쪽 여백 16, 위쪽 끝을 기록판과 정렬).
```

### 1-3. 기록판 위치

아래 줄을 찾는다.

```csharp
            panelRoot.anchoredPosition = new Vector2(-16f, -148f);
```

바꾼다.

```csharp
            panelRoot.anchoredPosition = RecordPanelPosition;
```

그 위의 3줄 주석(`// 최고 기록판: 화면 오른쪽 위 기준으로 로고 아래에 놓고 …` ~ `… 디스코드 아이콘(위끝 약 299) 위다.`)을 다음으로 바꾼다.

```csharp
            // 최고 기록판: 화면 오른쪽 위 기준(오른쪽 여백 16). 판 윗변 = 명판 세로 중심을 162에 두어
            // 왼쪽 하위 메뉴 명판과 윗변(149.5)을 같은 줄로 맞춘다(B안). 로고 아래끝(약 131)과 명판 윗변 사이 약 18px,
            // 판(높이 약 150) 아래끝 312는 버전 글자(위끝 약 338) 위다. 로그라이크 메뉴에서는 디스코드 아이콘이 화면 밖으로 빠진다.
```

### 1-4. 바꾸지 않는 것

- 버튼 간격(`buttonSpacingY` 24), `roguelikeFirstButtonY`(-31), 명판 크기(116×25), 기록판 폭(272) · 높이.
- 메인 버튼 묶음 `BTNs` 위치(116, 0) — 메인메뉴 화면은 그대로다.
- 퇴장 거리(`roguelikeMenuExitDistance` 280)와 기록판 슬라이드 거리(판 폭 + 32).
  - 하위 메뉴가 오른쪽으로 5 옮겨도 버튼 오른쪽 끝(136)에서 280을 빼면 화면 밖이다.
- 진입 · 퇴장 연출 시간.

## 2. 프리팹 재구성

1. Unity가 컴파일을 마칠 때까지 기다린다(콘솔 에러 0).
2. 메뉴 `LumberBoy/UI/Build Roguelike Main Menu`를 실행한다(`RoguelikeLobbyUIBuilder.BuildMainMenu`).
   - **`Build Roguelike Lobby UI (All)`은 쓰지 않는다** — 결과 · 기록 프리팹까지 다시 만든다.
3. 재구성 뒤 `Assets/Prefabs/UI/MainMenu/UI_MainMenu.prefab`에서 확인한다.
   - `RoguelikeMenu` `anchoredPosition` = (121, 18)
   - `RoguelikeRecordPanel` `anchoredPosition` = (-16, -162)
   - `UI_MainMenu`의 `roguelikeStartButton` / `roguelikeRecordsButton` / `roguelikeOptionButton` / `roguelikeBackButton` / `roguelikeRecordPanel` 참조가 모두 채워져 있을 것

## 3. 검수 (플레이 모드)

MCP로 하는 경우 Unity 창이 비활성이면 시간이 명령마다만 흐른다. 연출이 끝날 때까지 명령을 몇 번 보낸 뒤 캡처한다.

1. MainMenuScene에서 플레이 → 아무 키 → 메인메뉴 → 「로그라이크」.
2. 연출이 끝난 뒤 캡처해서 다음을 확인한다.
   - 왼쪽 명판 왼쪽 끝과 기록판 오른쪽 끝의 화면 여백이 같다(1920 기준 각 48px).
   - 왼쪽 명판 윗변과 기록판 명판 윗변이 같은 높이다(1920 기준 약 449px).
   - 로고와 겹치지 않고, 기록판이 버전 글자와 겹치지 않는다.
3. 동작 회귀 검사.
   - 기록 · 옵션: 메뉴와 기록판이 화면 밖으로 빠졌다가 **새 위치로** 돌아온다.
   - 뒤로: 메인 버튼이 원래 자리(왼쪽 여백 11)에 나타나고 디스코드 아이콘이 돌아온다.
   - 다시 「로그라이크」: 하위 메뉴가 새 위치에 나타난다(기준 위치 캡처가 어긋나지 않는다).
4. 언어 확인: 옵션에서 독일어 · 일본어로 바꿔 기록판이 버전 글자 · 화면 끝과 겹치지 않는지 본다(판 폭이 언어 따라 왼쪽으로 늘 수 있다).
5. 콘솔 에러 0(Steam 미실행 에러 `SteamAPI_Init() failed`는 무시).

## 4. 보고

- 바꾼 줄(전/후)과 재구성한 프리팹, 검수 캡처를 보고한다.
- 검수 3 · 4에서 어긋나는 점이 있으면 고치지 말고 그대로 보고한다.
