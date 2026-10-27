# 보유 카드 보관함 — 어플리케이션 레이어 작업 의뢰서

- 작성: UI 레이어 작업자 (2026-10-27)
- 대상: 어플리케이션 레이어 담당자
- 관련 기획: `Roguelike_Planning_v2.8.md` 5-2 "보유 카드 확인" · 10-2 "보유 카드 보관함"
- 관련 UI 작업 목록: `UI_WorkList.md`

UI 레이어에서 보유 카드 보관함 화면·HUD 트레이를 구현했다. 아래 세 가지(요청 A · B · C)는 **UI 레이어에서 할 수 없는 기능**이라 어플리케이션 레이어에서 구현해 주어야 한다. 이 문서는 필요한 기능과 이유, 제안 형태, 주의점만 적는다. 구현 방식은 담당자 판단에 맡긴다.

---

## 0. 이미 되어 있는 것 / UI 레이어가 만든 것

### 이미 있던 연결 (수정 불필요)

| 항목 | 위치 | 내용 |
|---|---|---|
| 열기 함수 | `RoguelikeUICoordinator.OwnedCardsRequested` | 목록을 만들어 `SetCards` → 일시정지 → `Show`. **호출처가 아직 없다** - 아래 요청 B-2(인벤토리 키)가 부른다. HUD 트레이 클릭으로 열던 경로(`hudUI.OwnedCardsRequestedEvent`)는 폐지되어 삭제했다 |
| 일시정지 / 해제 | `PauseForOwnedCards` / `ResumeFromOwnedCards` / `Tick` | 열리면 `Time.timeScale = 0`, 닫히면 복원. ESC 메뉴가 사이에서 timeScale을 1로 돌려도 `Tick`이 다시 건다 |
| 열 수 있는 단계 | `CanOpenOwnedCards` | RunStarting · CardSelect · TierTransition · WaveCombat · WaveClear |
| 입력 모드 | `RoguelikeModalInput.NotifyOpened/Closed` | 화면이 열리면 UI 모드 + 이동 잠금, 마지막 모달이 닫히면 원래 상태로 |
| 닫기 | 프리팹 `bCloseableByESC = 1` | ESC / 패드 B(UI Cancel)는 기존 `UIDepthController` 경로로 닫힌다 |

### UI 레이어가 이번에 추가한 것

| 항목 | 내용 |
|---|---|
| `UIView_RoguelikeOwnedCards` | 화면 본체 구현(카드 선택 화면의 카드 `UI_RoguelikeCard`를 그대로 쓴 카드 목록(레어 먼저) + 고정 설명 패널 + 현재 효과 + 키 안내 줄). **공개 시그니처는 그대로**(`SetCards`, `Show/Hide`, `HiddenEvent`, `ClosedEvent`) |
| `UI_RoguelikeOwnedCardCell` | 카드 한 칸(`UI_RoguelikeCard` + 판정 박스) |
| `UI_RoguelikeOwnedCardTray` | HUD 우하단 보관함 트레이. **표시 전용**이다(인벤토리 키 아이콘 + 보유 카드 총량 숫자판 하나를 보여 주며 클릭 · 호버에 반응하지 않고 레이캐스트도 받지 않는다). 창 열기 · 닫기와 짝을 이루는 연출(`PlayOpenEffect` / `PlayAbsorbEffect`)을 갖는다 - 요청 C |
| `UIView_RoguelikeOwnedCards` 열기 · 닫기 연출 | 트레이 칸에서 책이 펼쳐지고 같은 칸으로 접힌다(입자 · 탄성 없음, unscaled 시간). **`Hide()`가 닫기 연출 뒤에 실제로 닫는다(약 0.39초)**. 새 공개 메서드 `SetTray(UI_RoguelikeOwnedCardTray)`, `HideImmediate()` - 요청 C |
| `UIView_RoguelikeHUD.OwnedTray` | 트레이 접근자(읽기 전용 프로퍼티). 요청 C가 쓴다 |
| `UIView_RoguelikeHUD.SetOwnedCardCounts(int _rareCount, int _normalCount)` | 트레이 숫자판(레어 · 일반 장수) 갱신. 새 virtual 메서드 |
| `RoguelikeCardEffectText.BuildCurrent` | 누적 "현재 효과" 문구(UI 내부 사용) |
| 로컬라이징 | `RoguelikeUI.json`에 `Roguelike_OwnedCards_*` 키 추가(id 523~538). 문자열 키로 조회하므로 `LocKeys` 재생성 불필요 |

> **보유 카드 창은 키로만 열린다.** 트레이 클릭 열람은 폐지했다(기획 결정). 요청 A · B-2를 구현하기 전까지는 창을 열 방법이 없다.
>
> 트레이 오른쪽 위 모서리에는 보유 카드 창을 여는 **인벤토리 키 아이콘**이 걸려 있다(흰 외곽선은 인벤토리의 키 아이콘과 같은 `UIEffect`; `UI_KeyboardImage`, `RebindableAction` 모드 + `ERebindableAction.Inventory`; 리바인딩과 입력 장치 전환(키보드 ↔ 패드 LT)을 자동으로 따라간다. HUD `Initialize`에서 `InputManager`를 넘긴다). 앱 레이어 작업은 필요 없다. 키 연결(요청 A · B-2)이 아직 없어도 아이콘은 보인다. 트레이 본체는 카드 묶음 아이콘 + 보유 카드 총량(레어 + 일반) 숫자 하나가 든 숫자판 하나다(숫자는 판 오른쪽 끝에 붙는 오른쪽 정렬). **카드를 골라도 숫자는 요청 B-1을 구현하기 전까지 0으로 남는다.** 숫자판은 `SetOwnedCardCounts`가 불리기 전에는 **0**으로 표기된다(HUD가 켜질 때 `ResetTray`가 0으로 둔다). 카드가 0장이어도 0으로 표기한다. 요청 B-1을 구현하기 전까지는 항상 0이 보이는 것이 정상이다. 레어 · 일반 구분은 보유 카드 창 제목 줄이 보여 준다.

---

## 요청 A. 열기 키 — 인벤토리 키를 보유 카드 열기로 대체 (모든 장치)

### 필요한 이유

기획 결정: 보유 카드 창은 **키로만** 연다(트레이 클릭은 없다). 장치와 관계없이 **인벤토리 여는 키(키보드 Tab · 게임패드 LT, 같은 액션)** 를 쓴다. 로그라이크에는 가방이 없어서 이 키를 대신 쓴다.

그런데 지금은 로그라이크 세션 내내 이 키가 잠겨 있어 UI 쪽에서 받을 방법이 없다.

- `RoguelikeSystem.cs:310` `inputManager.SetInventoryKeyLock("RoguelikeSession", true)` (해제는 `:397`). 이유는 `:234` 주석 — 본편 가방이 카드 선택 · 결과 화면 위에 겹쳐 뜨는 것을 막으려는 것.
- `InputReader.OnInventoryKeyPressed` (`InputReader.cs:946`)는 잠금 소유자가 하나라도 있으면(`inventoryLockOwners.Count > 0`) 바로 반환한다.
- 잠금을 풀면 `GameplayUICoordinator.OnInventoryKeyPressed` (`GameplayUICoordinator.cs:362`)가 `InventoryKeyEvent`를 받아 본편 가방(`popUpUI.Show()`)을 연다. 즉 **그냥 풀 수는 없다**.

### 제안 형태 (기능 단위)

"로그라이크 세션 동안 인벤토리 키 입력을 본편 가방 대신 지정한 핸들러로 보낸다."

1. `InputReader` / `InputManager`에 인벤토리 키 리다이렉트를 추가한다. 예: `SetInventoryKeyRedirect(string _owner, Action _handler)` / 해제는 `_handler = null`.
2. `OnInventoryKeyPressed`에서 기존 검사(Alt+Tab 걸러내기, `CanDispatchGameplay`)를 그대로 통과한 뒤:
   - 리다이렉트가 등록되어 있고
   - **잠금 소유자가 리다이렉트 소유자(`"RoguelikeSession"`) 하나뿐이면** → `InventoryKeyEvent` 대신 핸들러를 호출하고 반환
   - 다른 소유자(`WARNINGUI`, `TENTUI`, `SCREENMODAL` 등)의 잠금이 하나라도 있으면 → 기존처럼 무시
3. `RoguelikeSystem`은 세션 시작(`:310`)에서 리다이렉트를 등록하고, 해제(`:397`)에서 반드시 푼다(잠금과 같은 수명. 입력 매니저는 BootStrap 소속이라 메인메뉴까지 살아 있다).
4. 핸들러는 요청 B-2에서 `RoguelikeUICoordinator`가 받는다.

대안으로 `RoguelikeSession` 잠금을 풀고 `GameplayUICoordinator.OnInventoryKeyPressed`가 로그라이크 중에는 무시하게 하는 방법도 가능하지만, 잠금 집합이 앱 수명이라는 기존 설계와 주석의 의도에서 벗어나므로 위 리다이렉트를 권한다.

### 주의점

- **키보드 Tab도 같은 액션**이다(`actions.Normal.Inventory`, 기본 키 Tab). 트레이 클릭이 없으므로 키보드/마우스에서는 Tab이 유일한 여는 방법이다. 장치로 거르지 말고 액션 그대로 핸들러를 부른다.
- 보유 카드 창이 열려 있으면 입력 모드가 UI라서(`RoguelikeModalInput`) `CanDispatchGameplay`가 거짓이다 → **같은 키로 닫히지는 않는다**. 닫기는 ESC / 패드 B(UI Cancel) 또는 창 바깥(딤) 클릭이 맡는다(화면 하단 키 안내 줄은 `UI_KeyboardImage`가 입력 장치에 맞는 아이콘으로 보여 준다: 키보드 = [Esc] 닫기, 패드 = [스틱] 이동 · [B] 닫기). 키보드/마우스 모드는 마우스 전용이라 EventSystem 선택을 잡지 않아 방향키가 움직이지 않는다(가이드 3-8, UI 단 처리 - 시스템 변경 불필요). 같은 키 토글이 필요하면 UI 액션 맵 쪽 처리가 따로 필요하며, 이번 범위에서는 제외했다.
- 리모핑(`ERebindableAction.Inventory`, `InputReader.cs:1667`) 결과를 그대로 따라야 한다. 패드 LT를 코드에서 직접 읽는 방식은 쓰지 말 것.
- 로비 · 메인메뉴에서는 리다이렉트가 남아 있으면 안 된다(세션 종료 시 해제).

---

## 요청 B. 보유 카드 열기 키 수신 + 트레이 숫자 공급 (`RoguelikeUICoordinator`)

### B-1. 트레이 숫자판 갱신

트레이에 레어(케이스) · 일반(슬리브) 장수를 보여 주려면 코디네이터가 값을 넣어 주어야 한다.

- 호출: `hudUI.SetOwnedCardCounts(int _rareCount, int _normalCount)`
- 구독 시그널: `RoguelikeModifiersChangedSignal` (`RoguelikeSystemSignals.cs:181`). 런 시작(`RoguelikeSystem.Flow.cs:859`)과 카드 선택 때 발행된다고 주석에 적혀 있다. 현재 `RoguelikeUICoordinator.SubscribeSignals`에는 구독이 없다. 필요하면 `RoguelikeCardPurchasedSignal`을 대신 쓰거나 함께 쓴다.
- 계산: `runInfo.Build.OwnedCards`를 돌며 `CardRarity.IsRare(CardRarity.GetDisplayGrade(card, build.GetLevel(card)))`로 센다. `OwnedCardsRequested` 안의 `ToOwnedView`가 쓰는 규칙과 같다. **표시 등급은 레벨에 따라 변한다**(에메랄드 = MAX 도달 시)이므로 카드 수가 아니라 레벨업에서도 다시 세어야 한다.
- 조합(프리즘)으로 재료 카드가 `OwnedCards`에서 빠지는 경우에도 같은 시그널 경로에서 다시 센다.
- 값이 바뀔 때만 호출한다(코디네이터의 다른 HUD 값과 같은 규칙). 마지막 값 두 개를 필드로 기억하면 된다.
- 런 시작 시 0 · 0을 한 번 넣는다. (`ResetTray`로 0 · 0에 두는 시점은 HUD `OnShow`뿐이다. 새 런에서 이전 런의 숫자가 남지 않게 런 시작 때 0을 넣는 편이 맞다.)

### B-2. 열기 키 수신

- 요청 A의 리다이렉트 핸들러에서 `OwnedCardsRequested()`를 호출한다. 이미 열려 있거나(`ownedCardsUI.IsVisible`) 열 수 없는 단계면 그 함수가 스스로 반환한다.
- 코디네이터 생성자(`Initialize`)에서 등록하고 `Release`에서 해제한다. `InputManager`를 코디네이터가 모르므로 `RoguelikeSystem`이 핸들러 연결을 중개하는 쪽이 자연스럽다(담당자 판단).
- 카드 선택 화면(`UIView_RoguelikeCardSelect`)이 떠 있을 때는 `RoguelikeModalInput`이 이미 UI 모드라 키가 흐르지 않는다. 즉 **전투 중(WaveCombat · WaveClear)에만 컨트롤러 키가 먹는다**. 카드 선택 중에 보유 카드를 보는 요구는 현재 없다.

---

## 요청 C. 보유 카드 창 ↔ HUD 트레이 연결 + 강제 닫기 — **UI 담당이 `RoguelikeUICoordinator`에 직접 적용함 (확인만 부탁)**

보유 카드 창은 열릴 때 우하단 HUD 트레이 칸에서 펼쳐지고, 닫힐 때 같은 칸으로 접혀 들어간다. 트레이는 열 때와 닫을 때 모두 키를 누른 즉시 반응한다(판 눌림 · 테두리 점멸). 닫힌 창이 트레이 칸에 닿는 도착 순간에는 반응하지 않는다. 카드 · 숫자 · 키 아이콘은 단독으로 움직이지 않고 입자 · 임팩트 선도 없다. 두 쪽이 같은 시간표(`RoguelikeOwnedCardsMotion`)를 쓴다.

> **왜 코디네이터를 직접 고쳤나**: 창(`Overlay Canvas`, sort 100)이 HUD(`ScreenSpaceCanvas`, sort 0)보다 위 캔버스라 딤과 책이 트레이를 덮는다. 창이 열려 있는 동안 트레이를 창의 맨 위 레이어로 올리려면 창이 트레이를 알아야 해서, 아래 두 줄을 넣었다. 둘 다 한 줄짜리 추가 · 교체이며 흐름은 바뀌지 않는다. 구현 방식이 마음에 안 들면 같은 효과의 다른 연결로 바꿔도 된다.

| 적용 위치 | 내용 |
|---|---|
| `RoguelikeUICoordinator.Initialize` | `ownedCardsUI?.SetTray(hudUI?.OwnedTray);` - 창에 트레이를 연결한다. 연결하지 않으면 창은 우하단 기본 칸(가장자리 8칸, 34x19)을 쓰고 트레이 반응만 빠진다. |
| `RoguelikeUICoordinator.CloseOwnedCards` | `ownedCardsUI.Hide()` → `ownedCardsUI.HideImmediate()` - 런 종료 · 단계 전환처럼 시스템이 강제로 닫을 때는 연출 없이 즉시 닫는다. |
| `RoguelikeUICoordinator.RequestOwnedCards` / `IsOwnedCardsOpen()` | 같은 인벤토리 키로 닫는다(토글). 열려 있으면 `ownedCardsUI.Hide()`(ESC · 패드 B · 딤 클릭과 같은 닫기), 닫혀 있으면 열 수 있는 단계에서만 연다. `IsOwnedCardsOpen()`은 아래 입력 통과 판정용이다. |
| `InputReader.SetInventoryKeyRedirect` / `InputManager.SetInventoryKeyRedirect` | 선택 인자 `Func<bool> _uiModePass`를 추가했다. 창이 떠 있는 동안은 입력 모드가 UI라 인벤토리 키가 `CanDispatchGameplay`에서 막히는데, 이 함수가 `true`를 돌려주면 UI 모드에서도 리다이렉트 핸들러를 부른다. 없거나 `false`면 기존과 같다(ESC 메뉴 · 결과창 등 다른 UI에서는 계속 막힘). 잠금 소유자 검사와 Alt+Tab 거름은 그대로 앞에서 걸러진다. |
| `RoguelikeSystem` 리다이렉트 등록 | `SetInventoryKeyRedirect(SessionInventoryLockOwner, uiCoordinator.RequestOwnedCards, uiCoordinator.IsOwnedCardsOpen)` - 세 번째 인자만 추가했다. |

### 동작 규칙 (참고)

- `Hide()`는 **닫기 연출을 재생한 뒤에 실제로 닫는다**. `IsVisible` · `HiddenEvent` · `ClosedEvent`는 연출이 끝나는 약 0.39초 뒤(실시간, 연출 속도 2.2배)에 온다. 그 사이 `Hide()`를 또 불러도 무시되고, 열리는 중(약 0.45초)이면 열기가 끝난 뒤에 닫힌다. ESC · 패드 B · 딤 클릭은 이 `Hide()`가 맞다.
- **`Show()`와 같은 프레임의 `Hide()`는 연출 없이 즉시 닫는다.** `RoguelikeSystem.OpenViews`가 뷰를 만들며 `Open`(= `Show`) 직후 `Hide()`하는 구조라 이 규칙이 없으면 로그라이크 진입 때 열기 · 닫기 연출이 재생된다.
- 열려 있는 동안 인벤토리 키는 닫기다(토글). 열리는 중에 누르면 열기가 끝난 뒤 닫히고, 닫히는 중에 누르면 무시된다. 시간은 `HiddenEvent`(연출 끝)에서 풀린다.
- 알려진 한계: 보유 카드 창 위에 다른 UI가 겹쳐 떠 있는 상태에서 인벤토리 키를 누르면 아래 창이 닫힐 수 있다. 현재는 ESC가 창을 먼저 닫아서 겹치는 경우가 거의 없다.
- 창이 열려 있는 동안 트레이는 창의 `TrayLayer`(맨 위 레이어)의 자식이다. 닫기 도착 직후 · 즉시 닫기 · 파괴 때 HUD로 돌아간다(`UI_RoguelikeOwnedCardTray.AttachTo` / `Detach`). 트레이가 꺼져 있으면(로비 등) 올리지 않는다.

---

## 검증 체크리스트 (구현 후)

1. Tab / 패드 LT → 전투 중 보유 카드 창이 열리고 게임이 멈춘다. ESC / B로 닫으면 원래 속도로 돌아온다.
2. 카드 선택 · 결과 · 쓰러짐 중에는 Tab / LT가 아무 일도 하지 않는다. 본편 가방이 겹쳐 뜨지 않는다.
3. ESC 메뉴를 보유 카드 창 위에서 열었다 닫아도 시간이 멈춘 채로 유지된다(기존 `Tick` 보정).
4. 로비로 돌아간 뒤 LT / Tab이 본편 규칙으로 복구된다(리다이렉트 해제 확인). 다음 런 진입 시 다시 등록된다.
5. 카드를 고르는 즉시 트레이 숫자가 바뀐다. 레벨업으로 에메랄드(MAX)가 되는 순간 레어 +1, 일반 −1.
6. 새 런 시작 시 트레이 숫자가 이전 런 값 없이 0 · 0에서 시작한다. 카드가 0장이면 숫자판에 0이 보인다(빈 칸이 아니다).
7. 트레이를 마우스로 눌러도 올려도 아무 반응이 없다(표시 전용, 창이 열리지 않는다).
8. (요청 C) 열 때도 닫을 때도 키를 누른 즉시 판이 1칸 가라앉았다 돌아오며 테두리가 번쩍인다(흰색 → 살구색 → 평소, 약 0.11초). 책이 트레이 칸에 닿아 창이 사라지는 도착 순간에는 트레이가 따로 반응하지 않는다. 카드 · 숫자 · 키 아이콘은 움직이지 않는다.
9. (요청 C) 열리는 중 · 닫히는 중에 ESC를 연타해도 한 번만 닫힌다. 닫히는 중 인벤토리 키는 무시된다.
10. (요청 C) 보유 카드 창이 열려 있는 상태에서 런이 끝나거나 쓰러지면 연출 없이 즉시 닫힌다.
11. (요청 C) 보유 카드 창이 열린 상태에서 인벤토리 키(Tab · 패드 LT)를 누르면 닫힌다. ESC · 패드 B · 딤 클릭의 닫기도 그대로 동작한다. ESC 메뉴 · 결과창이 떠 있을 때는 인벤토리 키가 아무 일도 하지 않는다.

## 알려진 문제 — SkyProduction의 구름이 화면 전체 입력을 가린다

Play 중 로그라이크 전투 화면에서 `EventSystem.RaycastAll`로 확인한 결과(마우스 위치 · HUD 트레이 중심 모두 같다).

- 맨 위에 맞는 것: `UIInstaller/Overlay Canvas(Clone)/OverlayRoot(Clone)/SkyProduction_Instance/Cloud` (Overlay 캔버스, sort 100).
- `Cloud`는 `Image`, `raycastTarget = true`, 활성 상태, 크기 640×2000으로 화면 전체를 덮는다. 구름 그림의 투명한 부분도 막는다(`alphaHitTestMinimumThreshold` 미사용).
- sort 100 캔버스는 sort 0 · 1 캔버스(`ScreenSpaceCanvas`의 HUD, `Canvas`의 본편 UI)보다 위라서, 이 레이어 아래의 UI는 마우스 호버 · 클릭을 받지 못한다. EventSystem(`InputSystemUIInputModule`)과 각 캔버스의 `GraphicRaycaster`는 모두 정상이다.
- 보유 카드 창(`UIView_RoguelikeOwnedCards`)은 같은 Overlay 캔버스에서 구름보다 나중에 열려 영향이 없을 가능성이 높지만 **검증하지 않았다**. 카드 호버 · 딤 클릭이 안 먹으면 이 문제부터 의심한다.
- `UIView_SkyProduction.cs`는 `Cloud` · `Sky` · `LoadingSkyCloud`의 `raycastTarget`을 건드리지 않는다. 연출 중 입력을 막으려는 의도인지 확인하지 못했다.

제안: 장식 이미지(`Sky` · `Cloud` · `LoadingSkyCloud`)의 `raycastTarget`을 끈다(프리팹 또는 `SetupUI`). 연출 중 입력 차단이 필요하다면 그 구간만 막는 별도 수단을 쓴다. 이 문제는 보유 카드 작업 범위 밖이라 UI 레이어에서 고치지 않았다.

---

## UI 레이어에서 확인이 필요한 항목 (참고)

- 에디터에서 `LumberBoy/UI/Build Roguelike HUD`, `LumberBoy/UI/Build Roguelike Owned Cards`를 실행해야 프리팹에 반영된다(UI 레이어 작업자가 수행).
- `CardDefinitionSO.icon`이 비어 있는 카드는 종류별 색의 임시 보석이 보인다. 아이콘이 채워지면 자동으로 바뀐다(코드 변경 없음).
