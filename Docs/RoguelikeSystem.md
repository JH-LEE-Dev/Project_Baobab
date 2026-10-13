# 로그라이크 모드 시스템 안내

노션 기획서 "로그라이크 모드 — 코어 루프 기획서 (초안 v1.1)"를 구현한 시스템 골격이다.
UI·이펙트는 이 시스템 위에 각 담당이 입힌다. 이 문서는 어디에 무엇을 꽂으면 되는지 정리한다.

## 1. 한눈에 보기

| 영역 | 위치 |
|---|---|
| 규칙(카드·가격·제시·압박 판정·웨이브·기록) | `Assets/Scripts/Application Layer/RoguelikeSystem/Core/` (어셈블리 `Baobab.Roguelike`, 본편 코드 참조 없음) |
| 오케스트레이터(런 상태 머신) | `RoguelikeSystem/RoguelikeSystem.cs`, `RoguelikeSystem.Flow.cs` |
| 웨이브 나무 스폰 | `RoguelikeSystem/RoguelikeWaveTreeStrategySO.cs` |
| 카드 효과 → 캐릭터 스탯 | `RoguelikeSystem/RunEffectApplier.cs` |
| 신규 스킬(오프로드·스텔라·포자폭탄)·조합 5종 | `RoguelikeSystem/Skill/RoguelikeSkillController.cs` |
| 코인 | `RoguelikeSystem/Coin/` |
| 로비 오브젝트 | `RoguelikeSystem/Lobby/RoguelikeLobbyInteractable.cs` |
| 기록 저장 | `RoguelikeSystem/RoguelikeRecordRepository.cs` (`%LOCALAPPDATA%/LumberBoy/RoguelikeRecords.json`) |
| 시그널 | `SignalSystem/Signals/RoguelikeSystemSignals.cs` |
| UI 코디네이터 | `UISystem/Coordinator/Roguelike/RoguelikeUICoordinator.cs` |
| UIView 5종 | `Presentation Layer/UISystem/UIView/Roguelike/` |
| 데이터 | `Assets/Data/Roguelike/` |
| 프리팹 | `Assets/Prefabs/Roguelike/` |
| 씬 | `Assets/Scenes/RoguelikeScene.unity` (빌드 설정 등록됨) |
| 테스트 | `Assets/Tests/EditMode/Roguelike/` (Test Runner의 EditMode → `Baobab.Roguelike.Tests`) |
| 디버그 창 | `Tools/Roguelike/Debug Window` |

흐름(기획서 7장): 로비 → 런 시작 → [카드 선택 → (티어 전환) → 웨이브 전투 → 웨이브 클리어] × 30 → 런 클리어 → 언리밋 → 게임오버/포기 → 결과 기록 → 결과 화면 → 로비.

## 2. 본편과의 관계 (본편 동작은 바뀌지 않는다)

- 메인메뉴 → `BootStrap.GoToRoguelikeFromMainMenu()` → `GameModeContext.IsRoguelike = true` → `RoguelikeScene`.
  메인메뉴로 돌아오면 플래그가 꺼지고 GameInstaller가 통째로 파괴된다.
- **본편 세이브는 로그라이크 세션에서 절대 쓰이지 않는다.** `SaveManager.SaveGameData()` 첫 줄에서 막는다(자동저장·포커스 상실·종료·F4 전부).
  로그라이크는 본편 세이브를 읽지도 않는다(능력 트리 0인 기본 캐릭터로 시작).
- 본편 코드에 넣은 것은 전부 "로그라이크일 때만" 다르게 도는 가드다. 기본값이면 기존 경로·값·순서 그대로다.

| 본편 파일 | 넣은 것 |
|---|---|
| `Bootstrap.cs`, `SceneManager.cs`, `BootstrapUsingData.cs`, `IBootstrapProvider.cs` | 로그라이크 씬 전환 경로(`SceneType.Roguelike`는 기존 값이 바뀌지 않게 `MAX` 뒤에) |
| `GameInstaller.cs` | 로그라이크 세션일 때만 `RoguelikeSystem` 생성·해제, `SetupGameInstaller` 분기 |
| `SaveManager.cs` | 로그라이크 세션 저장 차단 |
| `InDungeonObjectManager.cs` | `IRunTreeRules` 훅(등급 고정·HP 배율·기믹/드랍 끄기), 외부 전략 주입, 묘목 회수·전체 회수·지연 잔디 개방 취소(`CancelDelayedGrassOpenForRun`) API |
| `TreeObj.cs` | `RevertToSaplingForRun()` (새 메서드) |
| `EnvironmentSystem.cs`, `TileMapGenerator.cs` | 시그널 없이 필드 맵을 만드는 `GenerateRoguelikeField`, 안전구역 반경 덮어쓰기 |
| `TownSystem.cs` | 마을 오브젝트를 치우는 `PrepareForRoguelike()` (새 메서드) |
| `UnitSystem.cs` | 로그라이크면 본편 결과창 대신 `RoguelikeCharacterDownCompletedSignal` |
| `Character.cs` | `RefreshDronesForRun()`, `RecoverStaminaForRun()` (새 메서드) |
| `AttackComponent.cs` | 회전베기 간격 덮어쓰기(기본 3), 휘두름 알림(`RunCombatHooks`) |
| `Boomerang.cs` | 부메랑 적중 알림(구독자가 없으면 위치도 읽지 않음) |
| `GameplayUICoordinator.cs` | 로그라이크면 HUD 상승 시 숲 상태 배너 생략 |
| `GameplayUIInstaller.cs` | 추가 UIView를 여는 `OpenAdditionalView`/`GetOpenedView` |
| `UIView_MainMenu.cs`, `MainMenuUIInstaller.cs` | `RoguelikeButtonClickedEvent`/`OnRoguelikeButtonClicked()` |
| `UIView_ESC.cs`, `UI_EscapeMenu.cs`, `EscapeMenuUI.json` | 런 도중 메인메뉴 버튼 문구를 "로비"로(`SetLobbyMode`), 문구 id 7·8 추가 |
| `GameInstaller.prefab` | `roguelikeSystemPrefab` 연결, 로그라이크 UIView 5종을 `viewPrefabs`에 등록 |

정적 상태(`RunCombatOverrides`, `RunCombatHooks`)는 `RoguelikeSystem.Release`와 `GameModeContext.ExitRoguelike`(메인메뉴 진입) 양쪽에서 초기화된다.

## 3. UI 담당

UIView는 API(퍼블릭 메서드·이벤트)와 입력 모드 처리까지만 들어 있다. **각 클래스 안의 `virtual` 메서드 본문을 채우면 된다.**
`UIManager`가 정확한 타입으로 프리팹을 찾으므로 하위 클래스를 새로 만들지 말고 이 클래스들을 직접 고친다.
프리팹은 `Assets/Prefabs/Roguelike/UI/`에 빈 껍데기로 있다(레이어·ESC 닫힘 설정 완료).

| View | 시스템이 부르는 것 | 뷰가 올리는 것 |
|---|---|---|
| `UIView_RoguelikeHUD` | `SetWave`, `SetWaveProgress`, `SetThreshold(나무 수, T, 상한, 상태, 유예 진행도, 쿨다운 진행도)`, `PlayPressureDamaged`, `SetGold`, `SetGoldVisible`(언리밋 숨김), `SetCombatVisible`(로비 숨김) | `RequestOwnedCards()` |
| `UIView_RoguelikeCardSelect` | `OpenSelection`, `ReplaceOffers`(리롤), `PlayPurchase(구매, 소멸 인덱스, 완료 콜백)`, `CloseSelection(완료 콜백)`, `PlayRejected` (보유 카드·ESC 일시정지가 걸릴 수 있어 연출은 unscaled 시간으로) | `RequestBuy(i)`, `RequestSkip()`, `RequestReroll()` |
| `UIView_RoguelikeOwnedCards` | `SetCards` + `Show` (열려 있는 동안 `Time.timeScale = 0` 일시정지 - 연출은 unscaled 시간으로) | 닫기 = `Hide()` |
| `UIView_RoguelikeResult` | `OpenResult(RoguelikeResultViewData)` | `RequestConfirm()` |
| `UIView_RoguelikeRecords` | `OpenRecords(최근, 클리어 Top5, 언리밋 Top5)` | 닫기 = `Hide()` |

- 카드 데이터(`RoguelikeCardViewData`)에 판단이 끝나 있다: 표시 등급(에메랄드·프리즘 자동), 구매 후 레벨, 처음 보는 카드(자물쇠 연출), 레어 여부, 가격, 살 수 있는지(붉은 가격).
- `PlayPurchase`/`CloseSelection`의 완료 콜백을 **반드시** 불러야 시스템이 다음 단계로 간다(안 부르면 10초 뒤 강제 진행).
- 스태미너 바는 본편 HUD(`UIView_HUD`)가 그대로 그린다. 하단 코인 HUD(본편 소지금)는 런 골드가 아니다 - 런 골드는 `SetGold`로 들어온다.
- 메인메뉴 로그라이크 버튼: `UI_MainMenu`에 버튼을 추가하고 `parentView.OnRoguelikeButtonClicked()`를 부른다. 해금 조건은 본편 엔딩이 없어 아직 적용하지 않는다(상시 활성).
- 로비 오브젝트(게임 시작·순위표) 외형: `Assets/Prefabs/Roguelike/LobbyObject_*.prefab`의 `Visual` 자식을 교체. 타격 반응은 `HitReactionEvent`.

## 4. 이펙트 담당

두 가지 방법이 있다.

1. **기다릴 필요 없는 연출** - 시그널 구독. `RoguelikeSystemSignals.cs` 참고.
   - 상태: `RoguelikePhaseChangedSignal`, `RoguelikeWaveStartedSignal`, `RoguelikeWaveClearedSignal`, `RoguelikeTierChangedSignal`, `RoguelikeRunClearedSignal`, `RoguelikePressureStateChangedSignal`, `RoguelikePressureDamagedSignal`, `RoguelikeCardPurchasedSignal`, `RoguelikeFinalTreeKilledSignal`(30웨이브 마지막 나무 위치)
   - 신규 스킬: `RoguelikeSkillVfxSignal`(종류 `ERoguelikeSkillVfx` - 오프로드 예열/돌진, 별 생성/공격/소멸, 포자 연막, 360도 충격파, 회전베기+부메랑, 포자 폭발, 이음선 빔, 노바, 홀로 남은 별, 이음선 변경). 이음선 목록은 `RoguelikeSkillController.StarLinks`.
2. **시스템이 끝날 때까지 기다리는 연출** - `IRoguelikeProductionHandler` 구현.
   `RoguelikeSystem` 프리팹 아래에 컴포넌트로 붙이면 자동 등록된다(또는 `RoguelikeSystem.ProductionGate.Register`).
   `Play(요청, 완료 콜백)`에서 연출이 끝나면 콜백을 부른다. 핸들러가 없으면 프리팹에 적힌 대체 시간만 기다린다.

   | `ERoguelikeProduction` | 시점 |
   |---|---|
   | `LobbyEntry` | 로비 진입(디스토션 → 암전 → 바닥 타일 파도 → 배경 조립 → 플레이어 낙하). 지형은 이미 깔린 상태 |
   | `TierTransition` | 티어 전환(바닥 타일 파도, 로비와 공용) |
   | `WaveClear` | 나무가 묘목으로 돌아가며 사라짐 + 코인 흡수(동시) |
   | `RunClear` | 30웨이브 마지막 나무 줌인·슬로우 → 줌아웃 (`position` = 나무 위치) |
   | `ResultToLobby` | 결과 화면 확인 직후 디스토션 |
   | `LobbyToRun` | 로비 → 런(기획서에 없음, 기본 대기 0) |

- 웨이브 클리어 순간 남은 나무는 `TreeObj.RevertToSaplingForRun()`으로 묘목 외형이 되고 피해를 받지 않는다. 사라지는 애니메이션은 `WaveClear` 연출에서 입힌다(연출이 끝나면 시스템이 회수).
- 코인 외형: `Assets/Prefabs/Roguelike/RunCoin.prefab`(`RunCoin`이 이동만 담당). 전투 중 알파·흡수 간격은 `CoinController` 인스펙터.
- 별 외형: `RoguelikeStar.prefab`, 오프로드 외형: `RoguelikeOffroadVisual.prefab`(둘 다 임시 스프라이트).
- 360도 충격파는 본편 충격파 프리팹을 각도만 바꿔 쓰므로 부채꼴 비주얼이 그대로 나온다 - 원형 비주얼은 `Shockwave360` 시그널에 입힌다.

## 5. 기획·밸런스

| 에셋 | 내용 |
|---|---|
| `RoguelikeBalance.asset` | 6장 파라미터 전부(웨이브표·티어·T·스태미너·가격·리롤·제시·가중치). 우클릭 메뉴 "임시 값(기획서 10장)으로 채우기" |
| `RoguelikeSkillTuning.asset` | 신규 스킬·조합 기본 수치(9장 밸런스 항목) |
| `RoguelikeCardDatabase.asset` | **현재 사용 중.** 10장 임시 카드 세트 9장 |
| `RoguelikeCardDatabase_Draft.asset` | 3-3장 카드 29장(액티브 7·조합 5·패시브 13·루비 4). 레벨 효과는 시스템 확인용 임시 수치. `RoguelikeSystem` 프리팹의 `cardDatabase`를 바꾸면 이걸로 돈다 |
| `RoguelikeSystem.prefab` | 티어별 지형(1 넓은 초원, 2 뭉글 포자, 3 별뿌리, 4 마그마), 섬 크기(기준 화면 20 × 11.25 + 가장자리 물 칸 수), 로비 배치, 연출 대체 시간 |

카드 한 장(`CardDefinitionSO`)은 8장 양식 그대로다. **레벨 효과는 누적**: `levels[0]`=1레벨 구매 시, `levels[1]`=2레벨로 올릴 때 "추가로" 받는 효과.
효과 항목은 `ERunEffect` + 대상 스킬(None=전역) + 값. 액티브 카드는 보유만으로 자기 스킬(태그)을 얻는다.
조합 카드는 재료 두 장과 `suppressedSkills`(기본 능력이 꺼지는 재료 스킬)를 적는다.

로그라이크 데이터는 `Assets/Data`·`Assets/Prefabs/Roguelike`에 있어 빌드 스탯 가드(`GameStatBuildGuard`)의 검사 대상이 아니다.

## 6. 구현하면서 정한 것 (기획 확인 필요)

| 항목 | 지금 동작 |
|---|---|
| 본편 능력 트리 | 적용 안 함(기본 캐릭터로 시작) |
| 스태미너 시간 감소 | 없음. 임계치 피해로만 줄어든다 |
| 나무 등장 | 묘목 단계 없이 다 자란 나무로 심고 성장 연출을 바로 재생(`RoguelikeWaveTreeStrategy`의 `bPlayGrowUpOnSpawn`). 심는 즉시 벨 수 있고 임계치 수에 바로 들어간다 |
| 나무 등급(보석 회생) | Normal 고정 |
| 스테이지 기믹(실드·열기) | 끔 |
| 첫 무료 카드 | 브론즈 전체, 건너뛰기 불가(밸런스 `firstPickActiveOnly`/`firstPickSkippable`로 전환) |
| 구매 가능 2장 보장이 불가능할 때 | 가능한 만큼만. 보장 노출 카드가 칸을 먼저 차지한다(기획서 우선순위) |
| 플레이 시간 | 카드 선택·결과 화면 시간은 세지 않는다 |
| 바겐 세일 반값 범위 | 첫 구매·업그레이드·프리즘 모두 |
| 공격속도 증가 | 도끼 포함(프리팹 `bAttackSpeedAffectsAxe`) |
| 공격력과 도트 피해 | 도트에 둘 다 곱한다 |
| 회전베기+충격파의 발동 시점 | n번 휘두를 때마다 360도 충격파(n = 회전베기 간격) |
| 회전베기+부메랑의 회전베기 | 캐릭터 주변 원형 피해(도끼 피해 기준) |
| 필드 | 플레이어 시작 칸 중심의 완벽한 타원 섬(좌우·상하 대칭). 1920×1080 기준 가장자리 물 1칸(가장자리 칸의 반 칸까지 보정, 프리팹 `islandEdgeWaterTiles`), 해상도와 무관한 월드 크기. 내부는 풀 타일 1종·연못 없음, 바다와 맞닿은 한 줄만 모래. 섬 밖은 전부 물 |
| 카메라 | 섬 중앙에 고정(캐릭터를 따라가지 않음). 흔들림·줌 연출은 그대로 |
| 섬 장식 | 충돌체가 달린 장식(바위 등 StaticObj)은 만들지 않는다. 움직이는 풀·바닥 장식 타일은 남고, 그 위에도 나무가 난다(나무가 들어서면 바닥 장식은 걷힌다) |
| 기록 Steam 클라우드 | 동기화 안 함 |
| ESC 로비 확인 문구 | "이번 판을 포기하고 로비로 돌아가시겠습니까?" (번역 검수 필요) |

## 7. 디버그

`Tools/Roguelike/Debug Window` (플레이 중):
메인메뉴 → 로그라이크 진입, 로비 오브젝트 타격, 카드 구매/건너뛰기/리롤, 웨이브 즉시 클리어, 나무 쓰러뜨리기, 스태미너 피해, 결과 확인,
골드 추가, 다음 웨이브 번호 지정(티어·30웨이브·언리밋 확인용), 카드 강제 지급(초안 카드로 신규 스킬 확인). 전부 `#if UNITY_EDITOR`.
