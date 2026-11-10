# 전투 HUD 좌측 하단 재배치 — 어플리케이션 레이어 작업 의뢰서

- 작성: UI 레이어 작업자 (2026-11-10)
- 대상: 어플리케이션 레이어 담당자
- 관련 기획: `HUD_Polish/RoguelikeHUD_Polish_Plan.md` 5-5 · 5-6
- 관련 UI 작업 목록: `UI_WorkList.md`

UI 레이어에서 로그라이크 전투 HUD를 화면 좌측 하단 세로 묶음(웨이브 줄 / 임계치 바 + 유예 줄 / 처치 바 / 스태미너 바 + 대쉬 칸)으로 재배치했다. 그 과정에서 스태미너 바 · 대쉬 칸을 로그라이크 HUD가 직접 그리게 되었고, 본편 `UIView_HUD`와 연결하기 위해 **임시 연결**을 하나 두었다. 이 문서는 그 연결을 정식으로 바꾸는 선택지와, 어플리케이션 레이어에서만 확인할 수 있는 항목만 적는다. 구현 방식은 담당자 판단에 맡긴다. 이번 작업에서 어플리케이션 레이어 코드는 수정하지 않았다.

---

## 0. UI 레이어가 이번에 한 것

| 항목 | 내용 |
|---|---|
| `UI_RoguelikeStaminaBar` / `UI_RoguelikeDashCells` | 로그라이크 HUD(`UIView_RoguelikeHUD`)에 새로 추가한 스태미너 바 · 대쉬 칸. 본편 `HUD_Stemina` · `HUD_DashGauge`와 같은 호출(`UpdateValue`, `PlayDamaged`, `PlayRecovered`, `PlayRecoverWasted`, `SetForcedWarning`, `SetInvincible`, `SetUnlocked`, `SetMaxCharges`, `SetCharge` 등)을 받는다 |
| `RoguelikeHUDLink` (정적 클래스) | `UIView_RoguelikeHUD.Initialize`가 위 두 컴포넌트를 `RoguelikeHUDLink.StaminaBar` · `DashCells`에 등록하고, `OnDestroy`에서 자기 것이면 지운다 |
| `UIView_HUD` 전달 | `SetEquipmentHiddenForRun(true)`인 동안(= `RoguelikeUICoordinator`가 런을 시작해 `mainHudUI?.SetEquipmentHiddenForRun(true)`를 부른 동안) 본편 스태미너 · 대쉬 호출을 링크 대상에도 똑같이 전달한다. 본편 스태미너 루트는 `CanvasGroup` alpha 0으로 **화면에서만** 숨긴다(오브젝트는 켜 둬서 `SetForcedWarning`의 붉은 화면(BloodScreen) 등 본편 동작은 그대로) |
| 공개 시그니처 | 기존 공개 메서드의 이름 · 시그니처는 바꾸지 않았다 |

### 현재 연결이 하는 일 (임시)

`RoguelikeUICoordinator.cs:67`의 `SetEquipmentHiddenForRun(true)` 한 줄에 "로그라이크 런" 의미를 얹었다. 이 신호는 원래 "장비 숨김"이라 이름과 의미가 어긋나고, 링크는 정적 전역이라 HUD가 둘 이상 생기면(씬 전환 · 재생성) 마지막 등록이 이긴다.

---

## 요청 A (선택). 정식 연결 메서드로 교체

### 필요한 이유

위의 임시 연결은 동작하지만 (1) 이름이 의도를 말해 주지 않고 (2) 정적 전역에 기대며 (3) 로그라이크 HUD가 늦게 생기면 `UIView_HUD`가 매 호출 · 매 프레임 링크를 확인해야 한다(`SyncRunHud`).

### 제안 형태

`RoguelikeUICoordinator`가 로그라이크 HUD를 직접 넘긴다.

1. `UIView_HUD`에 정식 메서드를 둔다. 예: `SetRunHud(UIView_RoguelikeHUD _runHud)` — null이면 일반 모드. 전달 대상 · alpha 0 처리는 지금 `UIView_HUD`의 `RunStaminaBar` / `RunDashCells` / `SyncRunHud`를 그대로 옮기면 된다.
2. `RoguelikeUICoordinator`는 런 시작(`:67` 근처)에서 `mainHudUI?.SetRunHud(hudUI)`, 종료(`:96` 근처)에서 `SetRunHud(null)`.
3. `RoguelikeHUDLink`와 `UIView_RoguelikeHUD`의 등록 · 해제 코드를 삭제하고, `SetEquipmentHiddenForRun`은 본래 의미(장비 숨김)로 되돌린다.

UI 레이어는 위 1번의 `UIView_HUD` 쪽 코드를 옮기는 작업을 도울 수 있다. 코디네이터 쪽(2번)은 어플리케이션 레이어 몫이다.

### 주의점

- 본편(로그라이크가 아닐 때)의 스태미너 · 대쉬 동작과 배치는 바뀌지 않아야 한다. 교체 후 본편 스태미너 바가 보이는지 확인한다.
- 런이 끝나(`SetRunHud(null)`) 본편으로 돌아갈 때 본편 스태미너 루트의 alpha가 1로 돌아와야 한다.

---

## 요청 B. 로그라이크에서 대쉬가 해금되는지 확인

### 필요한 이유

로그라이크 HUD의 대쉬 칸(`UI_RoguelikeDashCells`)은 `UIView_HUD.UpdateDashCharges(bUnlocked, current, max)` 호출을 그대로 받아 **해금되었을 때만** 보인다. 그런데 `IDashState.cs:12` 주석은 "bUnlocked가 false면(전리품 미보유, 마을, 로그라이크)"라고 적고 있고, `UIView_HUD`의 주석도 로그라이크에서는 false라고 한다. 지금 코드에서 로그라이크 런 중 `DashCharge.SetUnlocked(true)`가 불리는 경로가 있는지 UI 레이어에서는 알 수 없다.

### 확인 · 결정을 부탁하는 것

- 로그라이크 런에서 대쉬가 해금되는가?
  - **아니오**: 대쉬 칸은 영영 보이지 않는다(스태미너 바만 보인다). 이 경우 UI 쪽에서 대쉬 칸을 아예 제거할 수 있으니 알려 달라. 지금은 코드를 남겨 두었다.
  - **예(카드 · 보상 등으로 해금)**: 해금 시점에 `DashCharge.SetUnlocked(true)` + 충전 수 신호가 `UIView_HUD.UpdateDashCharges` · `UpdateDashRecharge`로 흘러오면 로그라이크 HUD가 자동으로 칸을 보여 준다. 최대 칸 수는 2칸까지 그린다(`UI_RoguelikeDashCells.MaxCells`). 3칸 이상이 필요하면 알려 달라.
- 본편과 달리 로그라이크 대쉬 칸은 "해금되면 항상 보임"이다(본편 게이지처럼 가득 차면 숨기지 않는다). 기획 의도와 다르면 알려 달라.

---

## 변경하지 않은 것

- `Assets/Scripts/Application Layer/` 전부, `RoguelikeUICoordinator`, 시그널.
- 본편 HUD 프리팹(`HUD_Stamina.prefab`, `HUD_StaminaGroup.prefab`)과 그 배치.
