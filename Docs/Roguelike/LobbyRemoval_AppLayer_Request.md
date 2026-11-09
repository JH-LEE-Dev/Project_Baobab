# 로그라이크 로비 폐지 — 어플리케이션 레이어 작업 의뢰서

- 작성: UI 레이어 작업자 (2026-11-09)
- 대상: 어플리케이션 레이어 담당자
- 결정 배경: 로비가 메인메뉴와 같은 "배경 위 메뉴" 형태가 되어 중복이고, 걸어 다니는 로비 안에는 할 일이 없어 미완성처럼 보였다. 그래서 **로비를 없애고 메인메뉴 → 로그라이크 하위 메뉴 → 입장 연출 → 바로 런**으로 바꾼다. 결과 화면에서는 **다시 도전 / 메인메뉴**를 고른다.
- 시안: https://claude.ai/artifact/L2YuhswvPM2znvY9YGrj9t (v3)
- 관련 UI 작업 목록: `UI_WorkList.md`

UI 레이어에서 할 수 있는 것(화면 · 프리팹 · 문구 · 연결 지점)은 이미 만들었다(0장). 아래 S1~S7은 **UI 레이어에서 할 수 없는 시스템 변경**이다. 이 문서는 필요한 동작과 이유, 제안 형태만 적는다. 구현 방식과 이름은 담당자가 정한다.

---

## 0. UI 레이어가 만든 것 (연결 지점)

| 위치 | 내용 | 시스템이 연결하기 전 동작 |
|---|---|---|
| `UI_MainMenu` / `UIView_MainMenu` | `로그라이크` 버튼이 하위 메뉴(게임 시작 / 기록 / 뒤로 + 최고 기록판)를 연다. **게임 시작**은 기존 `OnRoguelikeButtonClicked` 진입 흐름 그대로다 | 그대로 동작 |
| `UIView_MainMenu.OpenRoguelikeMenuOnShow()` | 다음에 메인메뉴가 보일 때 버튼 등장 대신 로그라이크 하위 메뉴를 연 상태로 보여 준다(한 번만) | 아무도 부르지 않으면 평소처럼 메인 버튼이 나온다 |
| 메인메뉴의 기록 창 | 메인메뉴 프리팹 안에서 `UIView_RoguelikeRecords`를 열고 `RoguelikeRecordRepository.Load()`(읽기 전용)로 채운다 | 그대로 동작 |
| `UIView_RoguelikeResult.RetryRequestedEvent` / `MainMenuRequestedEvent` | 결과 화면의 두 버튼 | **다시 도전**은 기존 `ConfirmRequestedEvent`도 함께 부른다 → 지금처럼 로비로 간다. **메인메뉴**는 아직 연결되지 않아 `ConfirmRequestedEvent`만 부른다(로비로 감) |
| `UIView_RoguelikeHUD.SetCombatVisible(false)` | 웨이브 배지는 위로, 바 3종은 아래로, 보관함 트레이는 오른쪽으로 정수 픽셀로 빠지는 퇴장 모션(약 0.25초, unscaled) | 지금 호출 시점(로비 진입)에 모션만 더해진다 |
| `UIView_ESC.SetLobbyMode(true)` | 런 도중 메인메뉴 버튼 문구가 `로비` → **`포기`**(키 `Roguelike_ESC_Abandon`) | 그대로 동작 |
| `RoguelikeUI.json` | 새 문구 키 `Roguelike_Menu_*`, `Roguelike_Records_*`, `Roguelike_Result_*`, `Roguelike_ESC_Abandon`(18개 언어, 한·영 외는 임시 번역) | — |

---

## 1. 의뢰 항목

### S1. 입장 연출이 끝나면 곧바로 런 시작
- 지금: `StartSession` → `EnterLobbyRoutine(false)` → 낙하 → `SetPhase(ERunPhase.Lobby)` → 오브젝트 타격으로 `RunRoutine`.
- 요청: 낙하가 끝나면 Lobby 단계 없이 `RunRoutine`(웨이브 1 카드 선택)을 바로 시작한다. 조작(`EnableControl`)은 카드 선택이 끝나고 전투가 시작될 때 푼다. 그래야 낙하 직후 카드 선택이 뜨기 전에 캐릭터가 움직이지 않는다.
- 캐릭터 낙하 위치는 지금처럼 섬 정중앙(`GetPlayerStartPos`)이다. 로비 오브젝트가 없으니 런은 항상 카메라 중심에서 시작한다.
- `ERoguelikeProduction.LobbyToRun` 재생 지점이 없어진다(아래 S6 참고).

### S2. 결과 화면 선택 분기
- 지금: `RoguelikeResultConfirmedSignal` 하나 → `FinishRunRoutine`이 `EnterLobbyRoutine(true)`.
- 요청
  - **다시 도전**: 결과 화면에서 바로 디스토션 → 암전 아래 런 상태 정리·1티어 지형 → 입장 연출 → S1과 같이 곧바로 다음 런.
  - **메인메뉴**: 결과 화면에서 기존 `ExitToMainMenuRoutine`(BGM 페이드 → 커튼 → 하늘 연출)로 나간다. 메인메뉴에는 S3의 상태로 도착한다.
- 코디네이터가 `UIView_RoguelikeResult.RetryRequestedEvent` / `MainMenuRequestedEvent`를 구독한다. 연결하면 `ConfirmRequestedEvent` 구독은 빼도 된다(UI는 호환용으로 계속 부른다).
- 선택지(담당자 결정)
  - (가) `RoguelikeResultConfirmedSignal`에 선택값(enum 예: `ERoguelikeResultChoice { Retry, MainMenu }`)을 싣는다.
  - (나) 신호를 둘로 나눈다(`…RetryRequestedSignal` / `…MainMenuRequestedSignal`).

### S3. 메인메뉴 복귀 때 로그라이크 하위 메뉴를 연 상태로 도착
- 결과 화면 → 메인메뉴로 나간 경우에만 메인메뉴가 로그라이크 하위 메뉴를 연 상태로 나타나야 한다(바로 다시 시작하거나 기록을 볼 수 있게).
- 요청: BootStrap이 메인메뉴 씬을 세팅할 때(`SetupMainMenuScene` 근처) `UIView_MainMenu.OpenRoguelikeMenuOnShow()`를 한 번 부른다. 언제 불러야 하는지(로그라이크 결과 화면에서 나온 경우)는 시스템이 기억한다.
- `GameModeContext`는 "판정 전용"으로 문서화되어 있으므로 이 용도로 쓰지 않는 것을 권한다(BootStrap 필드 또는 별도 정적 값).

### S4. 로비 잔재 삭제
| 대상 | 비고 |
|---|---|
| `Lobby/RoguelikeLobbyInteractable.cs` | 파일 삭제 |
| `Prefabs/Roguelike/LobbyObject_StartRun.prefab`, `LobbyObject_Records.prefab` | 삭제 |
| `ERoguelikeLobbyAction`(`RoguelikeUsingData.cs`), `RoguelikeLobbyActionSignal`, `RoguelikeRecordsRequestedSignal`(로비에서만 발행) | 삭제. 기록 창은 이제 메인메뉴가 연다 |
| `RoguelikeSystem` 필드 `startRunObjectPrefab`, `recordsObjectPrefab`, `startRunObjectOffset`, `recordsObjectOffset`, `lobbyObjectLayer`, `fallbackLobbyToRunSeconds`, `lobbyActionHandler` | `RoguelikeSystem.prefab` 직렬화 값도 함께 정리 |
| `CreateLobbyObjects` / `PlaceLobbyObjects` / `RemoveLobbyObjects` / `OnLobbyAction` / `LobbyActionRequested` | 삭제 |
| `GoToMainMenuRequested`의 `ERunPhase.Lobby` 분기 | 삭제(로비 → 메인메뉴 경로가 없어진다). 런 도중 ESC → 포기 → 결과 흐름은 유지 |
| 코디네이터 `RecordsRequested` / `RecordsClosed` | 로그라이크 씬에서는 기록 창을 열 곳이 없다. 디버그용으로 남길지는 담당자 판단 |
| `RoguelikeDebugWindow.DrawLobby`, `RoguelikeSystem.Debug.cs`의 로비 재입장 디버그 | "결과: 다시 도전 / 메인메뉴" 버튼으로 바꾸는 것을 권한다 |
| `GameplayUIManager`의 `UIView_RoguelikeRecords` 등록 | 로그라이크 씬에서 쓰지 않게 되면 빼도 된다 |

### S5. 결과 화면이 뜰 때 뒤쪽 HUD를 동시에 퇴장
- 지금 결과 화면이 열려도 웨이브 배지 · 목표치/임계치 바 · 보관함 트레이 · 스태미너 바가 뒤에 남는다. 결과 창 하나에만 시선이 가도록, 결과 창이 등장하는 **같은 프레임에** 모두 화면 밖으로 빠져야 한다.
- 요청: `RoguelikeUICoordinator.ResultReady`(결과 화면을 여는 곳)에서
  1. `hudUI.SetCombatVisible(false)` — UI가 만든 방향별 퇴장 모션이 재생된다.
  2. `PopupUIDownSignal` 발행 — 본편 HUD(스태미너 바 · 대쉬 게이지 등) 내림 모션.
- 다시 도전이면 다음 런 입장 때 지금처럼 암전 아래에서 `PopupUIDownSignal` 상태를 유지하고, 조작이 풀릴 때 `PopupUIUpSignal` + `SetCombatVisible(true)`로 다시 들어온다.
- 참고: 지금 코디네이터의 단계 판정(`bCombatPhase`)은 `ERunPhase.Result`를 전투 단계로 본다(Lobby · ReturningToLobby · None만 제외). S7에서 함께 정리해 주면 좋다.

### S6. 이름이 맞지 않게 되는 멤버 (선택)
로비가 없어지면 아래 이름이 실제 의미와 달라진다. **바꿀지 유지할지는 담당자가 정한다.**

| 현재 이름 | 새 의미 | 제안 이름(바꿀 경우) | 주의 |
|---|---|---|---|
| `EnterLobbyRoutine` | 런 입장 연출 + 런 시작 | `EnterRunRoutine` | — |
| `ERunPhase.Lobby` | 없어짐 | 삭제 | 디버그 창 · 코디네이터가 참조 |
| `ERunPhase.ReturningToLobby` | 입장 연출 중 | `Entering` | 값 번호 유지 권장 |
| `UIView_ESC.SetLobbyMode` | 런 도중 "포기" 문구 | (UI 쪽 이름, 유지해도 됨) | UI가 이미 문구만 바꿨다 |
| `ERoguelikeProduction.LobbyEntry` / `ResultToLobby` / `LobbyToRun` | 런 입장 / 결과에서 다시 도전 / (재생 지점 없음) | `RunEntry` / `ResultToRun` / 삭제 | **이펙트 담당 연출 핸들러가 이 값으로 구독 중**. 이름을 바꾸면 핸들러 쪽도 같이 바꿔야 한다. 직렬화된 정수 값은 유지할 것 |
| `LobbyEntryESCLockOwner` | 입장 연출 중 ESC 잠금 | `EntryESCLockOwner` | 문자열 키 |

- 유지하는 경우: 각 정의에 "로비 폐지 후 의미: …" 주석만 남기면 된다.

### S7. 단계 판정 정리
- `RoguelikeUICoordinator`의 `bCombatPhase`와 보유 카드를 열 수 있는 단계 판정에서 Lobby 단계를 빼고 `Result`를 비전투로 본다.
- `RoguelikeSystem.Debug.cs`의 `ERunPhase.Lobby` 가드 정리.

---

## 2. 확인 절차 (연결 후)
1. 메인메뉴 → 로그라이크 → 게임 시작 → 디스토션 → 섬 정중앙 낙하 → 웨이브 1 카드 선택(낙하 직후 캐릭터가 움직이지 않아야 함)
2. 게임오버 → 결과 화면이 뜨는 순간 뒤쪽 HUD가 모두 빠지는지 → **다시 도전** → 바로 다음 런
3. 게임오버 → 결과 → **메인메뉴** → 메인메뉴가 로그라이크 하위 메뉴를 연 상태로 도착
4. 런 도중 ESC → `포기` → 결과 화면
5. 패드 · 키보드 각각 1~4
6. 디버그 창에서 로비 관련 버튼이 남아 있지 않은지, 콘솔 경고 · 에러 0
