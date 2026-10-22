# 로그라이크 모드 시스템 안내

노션 기획서 "로그라이크 모드 — 코어 루프 기획서"를 구현한 시스템이다. 현재 기준은 [기획서 v2.7](Roguelike/Roguelike_Planning_v2.7.md)이고(2026-10-22 적용: 임계치 50 · 스폰 상한 80 · 웨이브표 v2.7 · 유예 = 반복 피해 간격 4초 · 피해 쿨다운 카드 삭제(27장) · 이동속도 대쉬 2개 · 범위 패시브 도끼 동시 타격 · 부메랑 나무 조준 · 충격파 · 스텔라 · 오프로드 · 회전베기 수치 · 포자 조합 3종 대폭발 · 오염 · 묘목 복귀 연출 · 스텔라 · 오프로드 이펙트 연동. 그 전 v2.5(2026-10-21): 초기 나무 = 임계치의 절반 · 3초 묶음 분산 스폰 · 섬 가장자리 1칸 제외 · 4티어 용암 피해 제거 · 드론 자동 발사와 체인 확대 · 오프로드 소환 위치 · 스텔라 별 타격 판정과 4티어 폭발 상한 · 포자 수류탄 비행 · 착지 연출. 그 전 v2.4: 골드 제거 · 카드 25장 · 액티브 7레벨 · 리롤 선택마다 1회 · 건너뛰기 없음 · 포자 오염), UI 할 일은 [UI 작업 목록](Roguelike/UI_WorkList.md)에 모았다.
UI·이펙트는 이 시스템 위에 각 담당이 입힌다. 이 문서는 어디에 무엇을 꽂으면 되는지 정리한다.

## 0. 실행 방법 (UI·이펙트 담당은 여기부터)

### 들어가기

1. `Assets/Scenes/MainMenuScene.unity`를 열고 Play 한다. `RoguelikeScene`을 직접 열어 Play 하지 않는다 - 씬 전환을 맡는 BootStrap과 GameInstaller가 메인메뉴에서 만들어진다.
2. 스플래시 → (처음 실행이면 초기 설정 팝업 확인) → 아무 키 → 메인메뉴 버튼이 보일 때까지 넘긴다.
3. 메뉴 `Tools/Roguelike/Debug Window`를 열고 **"메인메뉴 → 로그라이크 진입"**을 누른다.
   - 실제 메인메뉴 로그라이크 버튼과 같은 경로다(`UIView_MainMenu.OnRoguelikeButtonClicked` → 디스토션 → 암전 → 씬 전환 → 바닥 타일 파도 → 플레이어 낙하).
   - 메인메뉴 버튼이 안 보이는 동안(스플래시·초기 설정 팝업)에는 들어가지 않는다.
   - 메인메뉴의 로그라이크 버튼 자체는 아직 없다. UI 담당이 `UI_MainMenu`에 버튼을 추가하고 `parentView.OnRoguelikeButtonClicked()`를 부르면 된다(3장).
4. 로비에서 **게임 시작 오브젝트를 도끼로 치면** 런이 시작된다(디버그 창 "게임 시작 오브젝트 타격"도 같다). 순위표 오브젝트를 치면 기록 창이 열린다.
5. 카드 선택 화면이 아직 비어 있는 동안은 디버그 창의 **카드 고르기(번호별)·리롤**로 진행한다. 모든 카드는 무료이고 건너뛰기는 없다. 리롤은 선택마다 1회다.
6. 나가기: 로비에서 ESC → 메인메뉴(본편 마을 → 메인메뉴와 같은 상승 연출). 런 도중 ESC의 메인메뉴 버튼은 "로비"(포기)다.

### 보고 싶은 장면 바로 가기

디버그 창(`Tools/Roguelike/Debug Window`)은 플레이 중에만 동작하고, 지금 단계에서 쓸 수 있는 버튼만 켜진다.

| 보고 싶은 것 | 하는 법 |
|---|---|
| 로비 진입 연출(디스토션·암전·바닥 타일 파도·해안선 조립·낙하) | 위 3번. 결과 화면에서 확인을 눌러도 같은 연출이다(결과 화면 위에 디스토션) |
| 티어 전환 파도 | 로비에서 **F6** 또는 "다음 티어 지형 전환 연출" - 1 → 2 → 3 → 4 → 1 티어 지형을 차례로 깐다 |
| 웨이브 전투 HUD(임계치·웨이브 진행) | 런 시작 → 카드 선택 → 전투. "나무 쓰러뜨리기"(그루 수 입력)로 처치 수를 올린다 |
| 웨이브 클리어(묘목 사라짐) | 전투 중 "웨이브 즉시 클리어" - 남은 목표 수만큼 쓰러뜨린 것으로 친다 |
| 30웨이브 런 클리어 연출(줌인·슬로우·줌아웃) | "다음 웨이브 번호 지정"에 30 → 지금 웨이브를 클리어 → 30웨이브에서 "웨이브 즉시 클리어" |
| 언리밋 | "다음 웨이브 번호 지정"에 31 이상(31이 언리밋 1) |
| 게임오버 → 결과 화면 | 전투 중 "스태미너 피해"에 큰 값(예: 9999). 결과 화면이 비어 있으면 디버그 창 "확인 (로비로)" |
| 포기 → 결과 화면 | 런 도중 ESC → 로비 → 확인 |
| 신규 스킬·조합 연출(오프로드·스텔라·포자폭탄·조합 5종) | "카드 1단계 지급"에 카드 에셋을 끌어 넣는다. 초안 카드는 `Assets/Data/Roguelike/Cards/Draft/` |
| 카드 레벨·에메랄드(MAX) 표시 | "카드 1단계 지급"으로 원하는 레벨까지 올린다(액티브 6번 → 다음 제시가 MAX) |
| 초기 나무·허탕 이동 비율 | 디버그 창 상태 칸의 "초기 나무 / 나무 없이 이동". 웨이브가 끝날 때마다 콘솔에도 남는다 |
| 순위표(기록 창) | 로비에서 "순위표 오브젝트 타격". 기록은 `%LOCALAPPDATA%/LumberBoy/RoguelikeRecords.json` - 지우면 기록이 비어 있는 상태가 된다 |

### 알아 둘 것

- **로그라이크에서는 본편 세이브가 절대 저장되지 않는다**(2장). 마음껏 시험해도 본편 데이터는 그대로다.
- **플레이 중에 스크립트를 고치지 않는다.** 저장하면 도메인 리로드로 NullReference가 쏟아지고 흐름이 멈춘다. Play를 멈추고 고친다.
- 일시정지: 보유 카드 창과 ESC 메뉴는 `Time.timeScale = 0`이다. 그 위에서 도는 UI 연출은 unscaled 시간을 쓴다. 런 클리어 슬로우 동안은 시간 배율이 0.25다.
- 연출이 비어 있어도 흐름은 끝까지 돈다. 기다리는 연출(`IRoguelikeProductionHandler`)이 없으면 프리팹에 적힌 대체 시간만 기다린다(4장).
- 이펙트를 붙일 자리: 기다릴 필요 없는 장식은 시그널(`RoguelikeSystemSignals.cs`), 시스템이 끝날 때까지 기다려야 하는 연출은 `RoguelikeSystem` 프리팹 아래에 `IRoguelikeProductionHandler` 컴포넌트를 붙인다. 바닥 타일 한 칸 단위 장식은 `RoguelikeSystem.TileWave.TileRevealedEvent`.
- UI를 붙일 자리: `Presentation Layer/UISystem/UIView/Roguelike/`의 UIView 5종 안 `virtual` 메서드 본문(3장). 프리팹은 `Assets/Prefabs/Roguelike/UI/`.

## 1. 한눈에 보기

| 영역 | 위치 |
|---|---|
| 규칙(카드·제시·압박 판정·웨이브·생성 위치·기록) | `Assets/Scripts/Application Layer/RoguelikeSystem/Core/` (어셈블리 `Baobab.Roguelike`, 본편 코드 참조 없음) |
| 오케스트레이터(런 상태 머신) | `RoguelikeSystem/RoguelikeSystem.cs`, `RoguelikeSystem.Flow.cs` |
| 웨이브 나무 스폰 | `RoguelikeSystem/RoguelikeWaveTreeStrategySO.cs` |
| 카드 효과 → 캐릭터 스탯 | `RoguelikeSystem/RunEffectApplier.cs` |
| 신규 스킬(오프로드·스텔라·포자폭탄)·조합 5종 | `RoguelikeSystem/Skill/RoguelikeSkillController.cs` |
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

로비 진입 연출(기획서 4-1)은 시스템이 전부 한다. 메인메뉴의 로그라이크 버튼 또는 결과 화면 확인 →
1. 디스토션: 그 순간의 화면(UI 포함)을 떠서 출렁인다(`RoguelikeScreenTransition` + `DistortedScreenImage`, 셰이더 없음).
2. 암전: 검게 덮인다. 메인메뉴에서 오면 검은 화면이 씬 로드 동안 유지되고, 그 아래에서 메인메뉴를 본편 퇴장 끝 자리로 옮겨 둔다(돌아갈 때 본편 `PlayEnterAnimation`이 같은 자리에서 내려온다). 결과 화면은 암전이 덮은 뒤 닫힌다.
3. 암전 아래에서 1티어 지형을 깔고 화면 안을 "물만 깔린 바다"로 되돌린 뒤 암전을 걷는다 - 물 타일은 깔린 채로 보인다.
4. 바닥 타일 파도: 섬 중앙에서 동심원으로(아이소 화면에서는 타원) 물결이 퍼지며 육지가 솟아올라 깔린다.
5. 배경 조립: 나머지 배경(해안선)이 퍼즐 조각처럼 알파 페이드로 나타난다. 3~5단계 합이 `entryCompleteSeconds`(10장 임시값 3초) 안에 끝나도록 파도 속도를 맞춘다.
6. 플레이어 낙하 → 7. 조작 가능.
진입 BGM: 메인메뉴 BGM은 디스토션 동안 잦아들고, 화면이 드러날 때 지역 BGM(Stage1~4BGM)이 시작된다. 3D 효과음 볼륨은 페이드인 동안 되살아난다. 로비 → 메인메뉴는 본편 마을 → 메인메뉴와 같은 순서(BGM 페이드아웃 → 커튼 → 구름 → HUD 내림 → 카메라 상승·SkyUP)다. 지역 BGM은 티어가 바뀔 때 바뀐다.

지형 전환 파도(기획서 3-4)는 진입과 같은 `RoguelikeTileWave`다. 섬 중앙에서 퍼지는 물결 앞머리가 닿은 칸이 솟았다 가라앉고, 가장 높이 솟는 순간 직전 티어 타일(바닥·물·해안선)이 새 티어 타일로 바뀐다. 티어 전환 때 캐릭터는 서 있던 자리 그대로다(기획서 3-4에 이동 언급 없음, 섬 모양이 티어마다 같다).
물결 모양: 칸 높이는 앞머리에서 떨어진 거리만의 함수인 감쇠 물결(sin(2π·d/λ) × (1 - d/L), 마루 → 골 → 작은 마루)이라 이웃 칸 높이가 이어져 띠 전체가 한 물결로 출렁인다. 타일에 명암이 없어 골(내려간 칸)을 살짝 어둡게 해 물결 띠가 읽히게 한다. 진입은 물결이 닿은 칸에 블록이 제 크기로 나타나며 섬이 퍼져 나가고(작게 돋아나게 하면 이웃 블록·바다와 맞물리지 않아 가장자리가 톱니처럼 지저분해진다), 티어 전환은 첫 마루에서 칸이 새 지형으로 바뀐다.
검은 빈 공간 금지: 진입에서는 섬 자리·물가 칸을 깊은 물 타일로 채운 채 페이드인하고, 그 칸의 실제 바닥·물은 물결이 다 지나간 뒤 새 지형으로 바뀐다(그 전까지 블록 밑 틈은 바다). 실제 바닥·물 층은 들지 않고 제자리에서 바꾸며, 파도 동안 화면 안 바닥·물(티어 전환은 해안선도)은 전부 모든 타일 층 위의 복제 타일맵(`RoguelikeTileWaveLift_*`, 층마다 같은 재질, 칸 단위 정렬)이 그린다 - 실제 층을 들면 원래 자리가 비어 물이 드러나고, 움직이는 칸만 복제하면 앞 칸이 뒤 칸의 흙 옆면을 가리지 못해 굵은 흙 줄이 생긴다. 복제층끼리의 앞뒤는 실제 층과 같게 맞춘다: 실제 바닥·물은 정렬 순서가 같고 칸 단위 정렬이라 앞 칸의 물이 뒤 칸 블록의 물가 절벽 아랫부분을 덮으므로, 복제층도 바닥·물에 같은 순서를 준다(물을 복제하지 않거나 순서를 달리하면 파도 동안 절벽이 물 위로 통째로 드러났다가 끝나는 순간 잠긴다). 물과 해안선은 출렁이지 않고 육지만 솟고 가라앉는다. 지면 스텐실 층(GroundStencil)의 복제에는 솟은 블록과 같은 타일을 놓아 블록이 그려진 자리의 물 스텐실을 지운다 - 이것이 없으면 아직 물(물 스텐실)뿐인 칸 위로 솟은 블록에 구름 그림자(`Custom/OnWaterCloud`, 물 스텐실에만 그림)가 비친다. 파도가 끝나면 복제 타일맵은 비운다.
수치는 `RoguelikeSystem` 프리팹의 `tileWaveSettings`(앞머리 속도 = 기획서 6장 "바닥 타일 파도 간격", 물결 높이·파장·길이·골 명암, 진입 완성 시간, 배경 조각 크기·페이드), `screenTransitionSettings`(디스토션 강도·시간 = 6장 "디스토션 강도와 지속 시간", 암전 유지·페이드인), `characterDropHeight`/`characterDropSeconds`. 메인메뉴 쪽 디스토션 수치는 `UIView_MainMenu.roguelikeEntryTransition`. 이펙트는 `RoguelikeEntryScreenCoveredSignal`/`RoguelikeEntryScreenRevealStartedSignal`, `RoguelikeTileWaveStartedSignal`/`RoguelikeTileWaveCompletedSignal`(`bEntry` = 로비 진입 파도면 true, 티어 전환이면 false), 칸이 생기는(진입)·바뀌는(전환) 순간의 `RoguelikeSystem.TileWave.TileRevealedEvent`, 낙하 신호(`RoguelikeCharacterDropStartedSignal`/`RoguelikeCharacterLandedSignal`)에 붙인다.

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
| `InDungeonObjectManager.cs` | `IRunTreeRules` 훅(등급 고정·HP 배율·기믹/드랍 끄기), 외부 전략 주입, 묘목 회수·전체 회수·지연 잔디 개방 취소(`CancelDelayedGrassOpenForRun`) API. 웨이브 클리어 묘목 회수 `RevertAllTreesToSaplingForRun(사라지는 시간, 묘목 그림 덮어쓰기)` - 두 번째 인자는 기본값 null(본편 그림 그대로) |
| `TreeObj.cs` | `RevertToSaplingForRun(사라지는 시간, 묘목 위 그림 = null)`(묘목으로 바뀔 때 본편 성장 흰 번쩍임 `PlayGrowUpFlash`), `RestoreAfterVanishForRun()` (새 메서드), 포자 대폭발 증폭률 읽기 `SporeDamageAmplify` (새 읽기 전용 속성) |
| `TreeVisualComponent.cs` | 묘목 위 그림을 바꿔 끼우는 `SetSaplingTopSpriteForRun` (새 메서드). 본편 공용 나무 데이터(`Tree Visual Data Base.asset`)에서 솜털 균사 나무 묘목이 다른 나무 묘목 그림을 가리키는 문제를 로그라이크에서만 보정한다(공용 데이터는 그대로) |
| `SaplingVEComponent.cs` | 웨이브 클리어 묘목 "뽀잉"(찌그러졌다 탄성 있게 튐) → 작아지며 사라짐 `AnimateRevertAndVanishForRun`, 크기 복구 `RestoreScaleForRun` (새 메서드) |
| `Bootstrap.cs` | 메인메뉴 진입 때 로그라이크 진입 암전이 남아 있으면 걷기(`RoguelikeScreenTransition.ClearIfCovering`, 들어간 적 없으면 아무 일도 없음) |
| `UIView_Popup.cs`, `UI_Inventory.cs` | 하단 코인 HUD·가방 HUD 표시 여부 `SetCoinVisibleForRun`·`SetInventoryVisibleForRun` 등 (새 메서드). v2.4부터 로그라이크는 둘 다 숨긴다 |
| `InDungeonVFXManager.cs` | 땅 별 그림 중심 오프셋을 읽는 `ConstellationGroundMarkVisualOffset` (새 읽기 전용 속성). 스텔라 · 오프로드 이펙트 재생 `PlayStellaBurstVFX` · `PlayStellaRangeVFX` · `PlayStellaGaugeVFX` · `PlayOffroadLaneVFX` (새 메서드, 본편은 부르지 않는다) |
| `IBasicAxeOnlyTarget.cs` | 기본 도끼 휘두름으로만 맞는 대상 표식(새 인터페이스, 로그라이크 별만 구현). 도끼 범위 강조를 켜고 끄는 `SetAxeHighlighted` |
| `EnvironmentSystem.cs`, `TileMapGenerator.cs` | 시그널 없이 필드 맵을 만드는 `GenerateRoguelikeField`, 안전구역 반경 덮어쓰기, 타원 섬(풀 한 종류, 장식 전부 생략 - 장식 타일·움직이는 장식·바위·물 장식), 바닥 타일 파도용 시각 타일맵·칸 위치 조회(`GetVisualTilemapsForRun` 등, 새 메서드) |
| `TownSystem.cs` | 마을 오브젝트를 치우는 `PrepareForRoguelike()` (새 메서드) |
| `UnitSystem.cs` | 로그라이크면 본편 결과창 대신 `RoguelikeCharacterDownCompletedSignal` |
| `Character.cs` | `RefreshDronesForRun()`, `RecoverStaminaForRun()`, 카드로 바뀐 최대 스태미너를 바로 반영하는 `SyncMaxStaminaForRun()`, 도끼질과 무관하게 드론을 활성 상태로 잇는 `KeepDronesActiveForRun()` (새 메서드). 로그라이크면 맵 끝 용암(위험 지형) 스태미너 소모를 건너뛴다. 대쉬 칸 수 · 충전 시간 덮어쓰기 `SetDashForRun(칸, 초)`(0이면 본편 전리품 대쉬 규칙 그대로). 로그라이크 부메랑은 나무를 노려 던지는 `ThrowBoomerangForRun`(`RunCombatOverrides.bBoomerangAimAtTrees`가 켜졌을 때만, 본편은 `ThrowBoomerangAt` 그대로). 드론 레이저 알림에 맞은 나무 바닥 위치를 함께 넘긴다 |
| `AttackComponent.cs` | 범위 증가 패시브의 도끼 동시 타격(`RunCombatOverrides.AxeMaxTargets`, 기본 1 = 본편처럼 가장 가까운 한 그루. 2 이상이면 가까운 순으로 N그루를 함께 치고 아웃라인도 N그루에 그린다), 충격파 MAX 두 번째 충격파 피해 배율, 회전베기 간격 덮어쓰기(기본 3), 회전베기 타격에만 붙는 피해·범위 배율(기본 1), 휘두름 알림(`RunCombatHooks`). `IBasicAxeOnlyTarget`(별)은 "가장 가까운 대상" 다툼에서 빼고, 휘두름 각도 · 범위 안이면 나무와 함께 늘 맞히고 범위 안에서 강조한다(기획서 v2.5 "별의 타격은 나무와 같은 규칙"). 회전베기 360도는 스텔라 III에서만 별을 친다. 본편에는 이런 대상이 없어 경로가 같다 |
| `Boomerang.cs` | 부메랑 적중 알림(구독자가 없으면 위치도 읽지 않음) |
| `ShockWave.cs`, `ShockWaveVisualComponent.cs`, `AxeExtraAttackCreator.cs` | 충격파 피해 배율 `MultiplyDamage`(새 메서드), 충격파에 맞은 나무 알림(포자 충격파), 포자 충격파 초록 색(`RunCombatOverrides.bShockWaveTinted`, 기본 꺼짐), 충격파 카드의 나무당 피해 배율(`RunCombatOverrides.ShockWaveDamageScale`, 기본 1) |
| `GameplayUICoordinator.cs` | 로그라이크면 HUD 상승 시 숲 상태 배너 생략 |
| `SkyCameraProductionManager.cs` | 본편 마을 → 메인메뉴 카메라 상승을 완료 콜백으로 받는 `PlayAscendOutForRun` (새 메서드, 연출·사운드·볼륨은 본편과 같음) |
| `GameplayUIInstaller.cs` | 추가 UIView를 여는 `OpenAdditionalView`/`GetOpenedView` |
| `UIView_MainMenu.cs`, `MainMenuUIInstaller.cs` | `RoguelikeButtonClickedEvent`/`OnRoguelikeButtonClicked()`(디스토션 → 암전 뒤 퇴장 정리를 바로 끝내고 씬을 넘긴다). `PlayGameStartSequence`가 시퀀스를 돌려준다(새로하기·이어하기는 돌려받은 값을 쓰지 않아 동작 같음) |
| `UIView_ESC.cs`, `UI_EscapeMenu.cs`, `EscapeMenuUI.json` | 런 도중 메인메뉴 버튼 문구를 "로비"로(`SetLobbyMode`), 문구 id 7·8 추가 |
| `GameInstaller.prefab` | `roguelikeSystemPrefab` 연결, 로그라이크 UIView 5종을 `viewPrefabs`에 등록, 이펙트 풀 4종(`StellaBurstEffect` · `StellaRangeEffect` · `OffroadLaneEffect` · `StellaGaugeEffect`, 로그라이크에서만 꺼내 쓴다) |

정적 상태(`RunCombatOverrides`, `RunCombatHooks`)는 `RoguelikeSystem.Release`와 `GameModeContext.ExitRoguelike`(메인메뉴 진입) 양쪽에서 초기화된다.

## 3. UI 담당

UIView는 API(퍼블릭 메서드·이벤트)와 입력 모드 처리까지만 들어 있다. **각 클래스 안의 `virtual` 메서드 본문을 채우면 된다.**
`UIManager`가 정확한 타입으로 프리팹을 찾으므로 하위 클래스를 새로 만들지 말고 이 클래스들을 직접 고친다.
프리팹은 `Assets/Prefabs/Roguelike/UI/`에 빈 껍데기로 있다(레이어·ESC 닫힘 설정 완료).

| View | 시스템이 부르는 것 | 뷰가 올리는 것 |
|---|---|---|
| `UIView_RoguelikeHUD` | `SetWave`, `SetWaveProgress`, `SetThreshold(나무 수, T, 상한, 상태, 유예 진행도, 쿨다운 진행도)`(v2.7: T 50 · 상한 80, 쿨다운 총 초 = 유예 시간 4초), `PlayPressureDamaged`, `SetCombatVisible`(로비 숨김). `SetGoldVisible(false)`는 세션 시작 때 한 번만 오고 `SetGold`는 오지 않는다(골드 없음) | `RequestOwnedCards()` |
| `UIView_RoguelikeCardSelect` | `OpenSelection`, `ReplaceOffers`(리롤), `PlayPurchase(선택, 소멸 인덱스, 완료 콜백)`, `CloseSelection(완료 콜백)`, `PlayRejected` (보유 카드·ESC 일시정지가 걸릴 수 있어 연출은 unscaled 시간으로). 골드·리롤 비용·건너뛰기 인자는 빈 값으로 온다 | `RequestBuy(i)`(고르기, 무료), `RequestReroll()`(선택마다 1회). `RequestSkip()`은 시스템이 받지 않는다 |
| `UIView_RoguelikeOwnedCards` | `SetCards` + `Show` (열려 있는 동안 `Time.timeScale = 0` 일시정지 - 연출은 unscaled 시간으로) | 닫기 = `Hide()` |
| `UIView_RoguelikeResult` | `OpenResult(RoguelikeResultViewData)` | `RequestConfirm()` |
| `UIView_RoguelikeRecords` | `OpenRecords(최근, 클리어 Top5, 언리밋 Top5)` | 닫기 = `Hide()` |

- 카드 데이터(`RoguelikeCardViewData`)에 판단이 끝나 있다: 표시 프레임(일반 = `Bronze`, 에메랄드·프리즘·계약 자동), 고른 뒤 레벨과 레벨 상한(액티브 7 · 패시브 5), 처음 보는 카드(자물쇠 연출), 레어 여부(`CardRarity.IsRare`). `price`(0)·`bAffordable`(true)은 v2.4에서 의미가 없다.
- `PlayPurchase`/`CloseSelection`의 완료 콜백을 **반드시** 불러야 시스템이 다음 단계로 간다(안 부르면 10초 뒤 강제 진행).
- 대쉬 게이지도 본편 것이 그대로 뜬다: 이동속도 증가 3 · 5레벨에 `Character.SetDashForRun`이 대쉬를 열면 `IDashState`를 읽는 하단 HUD `HUD_DashGauge`와 캐릭터 발밑 `UI_DashCharaGauge`(최대 2칸)가 보인다. 로그라이크 전용 대쉬 UI는 만들지 않는다.
- v2.6 · v2.7에서 생긴 카드 효과 4종(`ShockwaveFireOnWhiff` · `DashChargeAdd` · `AxeTargetCountAdd` · `ShockwaveDamageScalePercent`)은 `RoguelikeUI.json`에 효과 문구 키가 없다. 목록과 단위는 [UI 작업 목록](Roguelike/UI_WorkList.md) 3-2.
- 스태미너 바는 본편 HUD(`UIView_HUD`)가 그대로 그린다. 본편 하단 코인 HUD·가방 HUD·도끼 내구도 HUD는 로그라이크 세션 동안 시스템이 숨긴다(v2.4에서 골드가 없어졌다).
- 메인메뉴 로그라이크 버튼: `UI_MainMenu`에 버튼을 추가하고 `parentView.OnRoguelikeButtonClicked()`를 부른다. 해금 조건은 본편 엔딩이 없어 아직 적용하지 않는다(상시 활성).
- 로비 오브젝트(게임 시작·순위표) 외형: `Assets/Prefabs/Roguelike/LobbyObject_*.prefab`의 `Visual` 자식을 교체. 타격 반응은 `HitReactionEvent`.

## 4. 이펙트 담당

두 가지 방법이 있다.

1. **기다릴 필요 없는 연출** - 시그널 구독. `RoguelikeSystemSignals.cs` 참고.
   - 상태: `RoguelikePhaseChangedSignal`, `RoguelikeWaveStartedSignal`, `RoguelikeWaveClearedSignal`, `RoguelikeTierChangedSignal`, `RoguelikeRunClearedSignal`, `RoguelikePressureStateChangedSignal`, `RoguelikePressureDamagedSignal`, `RoguelikeCardPurchasedSignal`, `RoguelikeFinalTreeKilledSignal`(30웨이브 마지막 나무 위치)
   - 신규 스킬: `RoguelikeSkillVfxSignal`(종류 `ERoguelikeSkillVfx` - 오프로드 예열/돌진/충돌/퇴장, 별 생성/공격/연속 폭발/소멸, 별 도끼 범위 강조 켜짐 · 꺼짐(`StarHighlightOn` / `StarHighlightOff` - 별 아웃라인을 그릴 자리, `RoguelikeStar.prefab`의 `outlineVisualObj` 슬롯도 있다), 포자 수류탄 투척(`SporeBombThrown`)·포자 대폭발(`SporeBigExplosion`), 드론이 별을 함께 맞힘(`DroneStarShot`, 총구 → 별), 360도 충격파, 회전베기+부메랑, 포자 폭발(스텔라+포자폭탄), 이음선 빔, 노바, 홀로 남은 별, 이음선 변경). 이음선 목록은 `RoguelikeSkillController.StarLinks`.
2. **시스템이 끝날 때까지 기다리는 연출** - `IRoguelikeProductionHandler` 구현.
   `RoguelikeSystem` 프리팹 아래에 컴포넌트로 붙이면 자동 등록된다(또는 `RoguelikeSystem.ProductionGate.Register`).
   `Play(요청, 완료 콜백)`에서 연출이 끝나면 콜백을 부른다. 핸들러가 없으면 프리팹에 적힌 대체 시간만 기다린다.

   | `ERoguelikeProduction` | 시점 |
   |---|---|
   | `LobbyEntry` | 로비 진입의 바닥 타일 파도·배경 조립과 함께 돈다(디스토션·암전·파도·낙하는 시스템이 한다. 이펙트는 장식만 - 착지 먼지는 `RoguelikeCharacterLandedSignal`) |
   | `TierTransition` | 티어 전환. 바닥 타일 파도(시스템)와 함께 돈다. 이펙트는 그 위에 장식을 입힌다 |
   | `WaveClear` | 묘목 연출(시스템)과 함께 돈다. 장식만 |
   | `RunClear` | 30웨이브 줌인·슬로우·줌아웃(시스템)과 함께 돈다 (`position` = 나무 위치). 장식만 |
   | `ResultToLobby` | 결과 화면 디스토션(시스템)과 함께 돈다. 장식만 |
   | `LobbyToRun` | 로비 → 런(기획서에 없음, 기본 대기 0) |

- 시스템이 직접 재생하는 연출(본편 에셋 재사용):
  - 웨이브 클리어: 남은 나무가 흰색으로 번쩍이며 원래 묘목 그림으로 바뀌어 "뽀잉" 튀었다가 작아지며 사라진다(`waveClearSaplingVanishSeconds`, 본편 묘목 등장음 1회). 30웨이브는 줌인 연출 뒤에 같은 정리를 한다(기획서 3-5 "나무 정리").
  - 런 클리어: 마지막 나무로 빠르게 다가가 확대(본편 `CameraMoveController.ZoomCamera`) + 슬로우(시간 배율) → 섬 전체로 줌아웃. 그동안 ESC·보유 카드 창은 막힌다. 수치 `runClear*`.
  - 웨이브 시작: 현재 임계치의 절반(T 50이면 25그루)이 본편 성장 연출로 필드 곳곳에 솟아난다. 이후 웨이브표의 묶음 주기(티어별 3 / 2.5 / 3 / 2.5초)마다 정해진 그루 수가 한꺼번에 흩어져 솟아나고, 스폰 상한(80)을 넘는 몫은 버린다. 나무끼리 최소 간격을 두고, 플레이어 바로 옆과 섬 가장자리 1칸(물 · 용암 옆)에는 생기지 않는다(`RoguelikeWaveTreeStrategySO` · `TreeSpawnRules`, 수치는 `RoguelikeBalance.asset` "초기 나무 · 묶음 스폰 · 생성 위치").
  - 포자 수류탄: 초록 포자 주머니(`Booty Items_16x` 두 번째 칸)가 빙글빙글 돌며 포물선으로 날아가고(오르내릴 때 세로로 늘어남), 착지하면 납작하게 찌그러졌다 감쇠하며 출렁이고 부풀며 사라진다(수치 `vfxAssets.sporeBomb*`).
  - 스킬(`RoguelikeSkillVfxPlayer`): 스텔라 별 = 본편 3스테이지 땅 별(등장 오라 + Starappear, 사라질 때 본편 회전 소멸), 별 공격 = 본편 별똥별 착지 폭발 + StarExplosion, 포자 폭발(스텔라+포자폭탄) = 본편 포자 폭발 4연발 + SporeExplosion, 포자 폭탄 = 포물선 투척 → 착지에서 본편 포자 대폭발(2스테이지 전리품 연출, 단계는 오염 중첩 상한) → 맞은 나무에 본편 포자 디버프 표시(오염 중첩 1~3단계, 오염이 풀리면 지움), 스텔라+드론 = 본편 별자리 점선(이음선)·본편 드론 레이저와 드론 발사음(빔, 기획서 "드론 레이저가 이음선 위로 발사")·별자리 노바(노바 때 땅 별도 함께 소멸). 스텔라+드론의 별은 노바 때까지 웨이브를 넘어 남는다(기획서 "별들은 노바가 발동될 때만 모두 소멸") - 본편 나무 정리가 그림을 거두면 다시 그린다(`RestoreKeptStarVisuals`), 이음선·빔·별 폭발은 땅 별 그림 중심(프리팹 `visualLocalOffset`, `InDungeonVFXManager.ConstellationGroundMarkVisualOffset`)끼리 잇는다, 회전베기+부메랑 = 본편 회전베기 VFX. 오프로드 = 본편 차량 그림·바퀴 애니메이터(`RoguelikeOffroadVisual`): 예열 동안 본편 플래시(_FlashAmount)가 깜빡이고 시동음(OffroadNonEdit), 진행 방향으로 좌우를 맞추고 돌진 중 바퀴가 돈다.
  - 스텔라 · 오프로드 이펙트(기획서 v2.7 필수, [StellaOffroadVFX_Guide.md](Roguelike/StellaOffroadVFX_Guide.md) · [StellaDurationGauge_Guide.md](Roguelike/StellaDurationGauge_Guide.md) 3장을 시스템이 연동): 별마다 바닥 폭발 범위 원(`VFX_StellaRange`) + 남은 시간 게이지(`VFX_StellaDurationGauge`, 스텔라 + 드론의 별은 시간 제한이 없어 게이지 없음), 별 폭발 · MAX 연속 폭발 · 홀로 남은 별 = `VFX_StellaBurst`(크기 = 판정 반경), 오프로드 예열 = 소환 자리에서 돌진 경로를 따라 흐르는 셰브론 띠(`VFX_OffroadLane`, 폭 = 돌진 판정 폭), 드론의 별 사격 = 본편 드론 레이저 한 줄 더. 포자 대폭발 카메라 흔들림은 0.2초에 한 번으로 묶는다(조합으로 연달아 터질 때).
- 별 판정체: `RoguelikeStar.prefab`(본편 땅 별로 그릴 수 있으면 자체 그림은 숨긴다). 포자 수류탄 그림·회전베기 프레임: `RoguelikeSystem` 프리팹 `RoguelikeSkillController.vfxAssets`.
- 이펙트 담당 몫으로 남은 것(기획서 v2.5 C-1): 별 표식 전용 이펙트(지금은 링만), 별 아웃라인 그림, 오염 단계 이펙트와 바닥 링 색(중첩 1 초록 · 2 보라 · 3 금색, 한 나무에 현재 단계 하나만). 별 폭발 범위 이펙트는 v2.7에서 `VFX_StellaBurst`로 들어갔다. 별 바닥 범위 원은 지금 늘 같은 진하기다 - 기획서 v2.6의 '평소 옅게, 도끼 범위에 들면 선명'은 `VFX_StellaRange`에 강조 API가 없어 남아 있다(`StarHighlightOn` / `StarHighlightOff` 신호에 붙이면 된다). 별 게이지의 소팅 순서(별 그림 위)와 인게임 색(블룸)은 함께 확인한다.
- 포자 조합 3종(포자 레이저 · 방구차 · 포자 충격파)은 포자 수류탄과 같은 본편 포자 대폭발 · 오염 연출을 쓴다. 조합 전용 이펙트는 아직 없고, 포자 충격파만 시스템이 충격파를 초록(`RoguelikeSkillTuning.sporeShockwaveColor`)으로 칠한다.
- 360도 충격파는 본편 충격파 프리팹의 각도를 전 범위로 넓혀 쓴다. 본편 충격파 셰이더가 각도대로 그리므로 원형으로 보인다.
- 오프로드 엔진 루프음은 본편에도 튜토리얼 대기 전용(OffroadNonEditLoop)이라 쓰지 않았다. 돌진 효과음·흙먼지(본편 "Go" VFX)는 이펙트 담당이 `OffroadDash` 시그널에 붙인다.

## 5. 기획·밸런스

| 에셋 | 내용 |
|---|---|
| `RoguelikeBalance.asset` | 부록 A 파라미터(웨이브표 - 웨이브별 묶음 그루 수 · 묶음 주기 · 나무 HP · 목표 처치 수, 언리밋 규칙 · 티어 · T 50 · 스폰 상한 80 · 유예 4초(= 반복 피해 간격) · 스태미너 · 초기 나무 비율 · 최소 간격 · 가장자리 칸 수 · 제시 · 리롤 횟수 · 종류 비중 가중치). v2.7 값이 들어 있다. 우클릭 메뉴 "기획서 v2.4 부록 A 초안 값으로 채우기"는 옛 값이라 쓰지 않는다 |
| `RoguelikeSkillTuning.asset` | 전투 기준값과 신규 스킬·조합 기본 수치(부록 A-1, A-4). v2.7: 오프로드 피해 10, 스텔라 확률 20% · 폭발 11, 포자 오염 6초, 대쉬 칸당 충전 4초(`dashRechargeSeconds`), 포자 조합 처리 상한(`sporeCombo*`) · 포자 충격파 색. 스텔라 폭발 상한(`stellaBlastMaxHpRatio` 25%, `stellaBlastCapFromTier` 4티어부터) 포함 |
| `RoguelikeCardDatabase_Draft.asset` | **현재 사용 중.** v2.7 카드 27장(액티브 7 · 조합 7 · 공통 패시브 10 · 계약 3, 피해 쿨다운 증가 삭제). 액티브 7레벨 · 패시브 5레벨 효과는 부록 A-4·A-5 v2.7 수치. 카드 아이콘은 아직 비어 있다 |
| `RoguelikeCardDatabase.asset` | 예전 임시 카드 세트 9장(쓰지 않음) |
| `RoguelikeSystem.prefab` | 티어별 지형(1 넓은 초원, 2 뭉글 포자, 3 별뿌리, 4 마그마), 섬 크기(기준 화면 20 × 11.25 + 가장자리 물 칸 수), 로비 배치, 연출 대체 시간 |

카드 한 장(`CardDefinitionSO`)은 부록 B 양식 그대로다. 가격·등급은 없고, 레벨 수는 종류가 정한다(액티브 7, 공통 패시브 5, 계약·조합 1). **레벨 효과는 누적**: `levels[0]`=1레벨 선택 시, `levels[1]`=2레벨로 올릴 때 "추가로" 받는 효과.
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
| 첫 선택 | 액티브만 제시(밸런스 `firstPickActiveOnly`), 리롤 1회 |
| 회전베기 레벨 효과 | 회전 피해·범위 증가는 회전베기 타격(과 회전베기+부메랑의 회전베기)에만 붙고 일반 도끼 공격에는 붙지 않는다. 필요 휘두름 n이면 n+1번째 공격에 발동(상시 = 매 공격) |
| 포자 대폭발 | 판정은 본편과 같은 등각 타원(장반경 `sporeRadius` 2.5), 폭발 자체 피해 0(오염 지속 피해만). 착지 후 0.2초 뒤 판정 |
| 회전베기 범위(v2.7) | 기획서의 "범위 안 나무 약 N그루"를 그루 수 ∝ 반경²로 보고 반경 %로 바꿨다(1레벨 +25%, II · IV · VI +4%씩) |
| 범위 증가 패시브 도끼 동시 타격 | 휘두름 각도 · 범위 안 나무 중 가까운 순으로 N그루(1 + 레벨 보너스). 기본 1이면 본편과 같은 한 그루 |
| 대쉬(이동속도 증가 3 · 5레벨) | 본편 대쉬(전리품) 동작 · 키 · 게이지 그대로, 칸 수와 칸당 충전 시간(4초)만 바꾼다 |
| 부메랑 나무 조준 | 감지 반경 안에서 날아가는 부메랑이 덜 노리는 나무 → 가까운 나무 순. 나무가 없으면 마지막 이동 방향(조작이 잠긴 동안에는 던지지 않음). 던지는 박자는 본편 0.5초 그대로 |
| 스텔라 III 드론의 별 우선 조준 | 본편 드론 타겟팅(나무 전용)은 그대로 두고, 레이저가 나갈 때마다 사거리 안 가장 가까운 별 하나를 함께 맞힌다(레이저 한 줄 더) |
| 유예와 반복 피해 간격 | 같은 값(기본 4초)을 쓴다. 유예 시간 증가 패시브가 둘 다 올린다 |
| 묘목 복귀 그림 | 솜털 균사 나무는 `RoguelikeSystem` 프리팹 `saplingTopOverrides`의 묘목 그림으로 덮어쓴다(본편 공용 데이터의 잘못된 연결은 본편 담당 확인 필요) |
| 초안 수치(기획서에 숫자 없음) | 충격파 크기 한 단계 = 범위 +20%, 스텔라 범위 II +10% · VI +12%, 포자 범위 II +10% · VI +15%, 나무 최소 간격 1.0 · 플레이어 금지 반경 1.2(기획서 TBD) |
| 맵 가장자리 | 기획서의 "끝 1픽셀 단축"을 **1타일**로 해석해 물 · 용암 칸과 8방향으로 붙은 칸 전부(모든 변)를 뺐다(`fieldEdgeInsetCells`) |
| 4티어 용암 | 피해만 없앴고 용암 그림은 그대로다 |
| 오프로드 소환 위치 | 두 대각선 방향마다 섬 폭을 최소 간격(돌진 판정 반지름)으로 나눈 차선 중 경로에 나무가 있는 것만 후보. 같은 방향은 차선 간격, 반대 방향은 소환 위치 거리가 최소 간격보다 좁으면 겹침으로 본다. 이웃 차선의 돌진 판정 폭은 일부 겹칠 수 있다(같은 자리 소환만 막는다). 나무가 고루 있으면 MAX 6대가 늘 다 나간다 |
| 스텔라 폭발 상한 | 별 폭발(MAX 연속 폭발 포함)과 홀로 남은 별의 표식 공격에 적용. 표식 · 포자 증폭 배율을 곱한 최종 피해 기준 |
| 플레이 시간 | 카드 선택·결과 화면 시간은 세지 않는다 |
| 공격속도 증가 | 도끼 포함(프리팹 `bAttackSpeedAffectsAxe`) |
| 공격력과 도트 피해 | 도트에 둘 다 곱한다 |
| 회전베기+충격파의 발동 시점 | n번 휘두를 때마다 360도 충격파(n = 회전베기 간격) |
| 회전베기+부메랑의 회전베기 | 캐릭터 주변 원형 피해(도끼 피해 기준). 부메랑 타격 1회당 1회. 캐릭터 중심의 같은 공격이라 같은 프레임에 여러 그루를 맞혀도 한 번만 친다(겹쳐 쌓이지 않게). 간격 제한 `spinBoomerangCooldown` 기본 0 |
| 부메랑+충격파 | 맞은 나무마다 그 자리 충격파. 같은 프레임에 `boomerangShockwaveMergeRadius`(기본 0.6) 안에 붙은 자리는 하나로 합친다. 간격 제한 `boomerangShockwaveCooldown` 기본 0 |
| 필드 | 플레이어 시작 칸 중심의 완벽한 타원 섬(좌우·상하 대칭). 1920×1080 기준 가장자리 물 1칸(가장자리 칸의 반 칸까지 보정, 프리팹 `islandEdgeWaterTiles`), 해상도와 무관한 월드 크기. 내부는 풀 타일 1종·연못 없음, 바다와 맞닿은 한 줄만 모래. 섬 밖은 전부 물 |
| 카메라 | 섬 중앙에 고정(캐릭터를 따라가지 않음). 흔들림·줌 연출은 그대로 |
| 섬 장식 | 모든 티어에서 장식을 하나도 만들지 않는다(바위 등 충돌 장식·움직이는 풀·바닥 장식 타일·물 장식·물 위 움직이는 장식). 바닥·물·해안선과 물 전체에 까는 블룸 층은 그대로다 |
| 기록 Steam 클라우드 | 동기화 안 함 |
| ESC 로비 확인 문구 | "이번 판을 포기하고 로비로 돌아가시겠습니까?" (번역 검수 필요) |

## 7. 디버그

`Tools/Roguelike/Debug Window` (플레이 중):
메인메뉴 → 로그라이크 진입(실제 버튼과 같은 경로), 로비 오브젝트 타격, 다음 티어 지형 전환 연출(로비에서 F6), 카드 고르기/리롤, 웨이브 즉시 클리어, 나무 쓰러뜨리기, 스태미너 피해, 오프로드 발동(F7, 1·2·4·6대), 결과 확인,
다음 웨이브 번호 지정(티어·30웨이브·언리밋 확인용), 카드 강제 지급(초안 카드로 신규 스킬 확인), 선택 횟수·초기 나무·나무 없이 이동한 비율 표시. 전부 `#if UNITY_EDITOR`. 상황별 사용법은 0장 표.
