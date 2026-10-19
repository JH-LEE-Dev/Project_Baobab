# 로그라이크 UI 작업 목록 (기획서 v2.1 기준)

UI 담당이 해야 할 일을 한곳에 모았다. 규칙의 근거는 [기획서 v2.1](Roguelike_Planning_v2.1.md)이고,
실행 방법·디버그 창·UIView 구조는 [로그라이크 시스템 안내](../RoguelikeSystem.md) 0장·3장을 먼저 본다.

- 시스템(규칙·판정·골드·카드 효과·연출 대기)은 다 돌아간다. UI가 비어 있어도 런은 끝까지 진행된다(디버그 창으로 대신 조작).
- UI 표기 원칙(기획서 10장): 디자인이 확정되지 않은 UI는 **테스트 버튼**으로 먼저 두고, 확정되면 바꾼다.
- 🔌 표시 = 지금은 시스템 → UI 연결(코디네이터·뷰 메서드)이 없어 **연결부터 필요한 항목**이다. 신호와 읽을 값은 이미 있다.
  연결 코드를 시스템 쪽에서 먼저 만들어 주길 원하면 요청한다.
- 📦 표시 = 카드 데이터·문구가 더 있어야 하는 항목이다(시스템 쪽 필드 추가가 필요하면 요청).

## 1. 이미 연결된 화면 (빈 껍데기 채우기)

UIView 5종은 API와 입력 모드 처리까지 들어 있고, **각 클래스의 `virtual` 메서드 본문만 채우면 된다**(하위 클래스를 새로 만들지 않는다).
스크립트는 `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/`, 프리팹은 `Assets/Prefabs/Roguelike/UI/`(빈 껍데기).
시스템 ↔ 뷰 연결은 `Assets/Scripts/Application Layer/UISystem/Coordinator/Roguelike/RoguelikeUICoordinator.cs`.

### 1-1. 전투 HUD — `UIView_RoguelikeHUD` (기획서 10-1)

| 할 일 | 받는 값 / 규칙 |
|---|---|
| 웨이브 표기 | `SetWave(번호, 언리밋 여부, 언리밋 번호)`. 언리밋은 `Unlimit-N` |
| 웨이브 진행도 | `SetWaveProgress(처치, 목표)` |
| 임계치 바 | `SetThreshold(나무 수, 임계치 T, 스폰 상한, 상태, 유예 진행도, 쿨다운 진행도)`. 하단 중앙, 스태미너 바 위나 아래. 스태미너 바와 통일감을 주고 **색만 다르게**. 유예 시간이 흐르는 진행도도 이 바에서 보여 준다(방식은 디자인 결정). T는 루비 "인내의 시간" 등으로 줄어든 값이 이미 반영되어 들어온다 |
| 임계치 피해 반응 | `PlayPressureDamaged(피해량)` |
| 코인 HUD 배치 | 본편 하단 코인 HUD(가방 옆 `CurrencyCounterHUD`)가 런 골드를 그대로 보여 준다(이미 연결됨). 기획서 배치(하단 중앙, 바 기준 왼쪽 위)로 옮길지, 가방 HUD를 숨길지 정한다. 로그라이크 HUD에 코인 표시를 따로 두면 `SetGold`가 같은 값을 받고, 그 아이콘을 `CoinFlyTarget`으로 돌려주면 흡수 코인이 그쪽으로 날아간다 |
| 언리밋에서 골드 숨김 | `SetGoldVisible(false)` (기획서 7-2) |
| 로비에서 전투 HUD 숨김 | `SetCombatVisible` |
| 보유 카드 창 열기 버튼 | 누르면 `RequestOwnedCards()`. 마우스로 열 수 있어야 한다. 게임패드 키는 미정 → 테스트 버튼 |

스태미너 바는 본편 HUD(`UIView_HUD`)가 그대로 그린다. 따로 만들지 않는다.

### 1-2. 카드 선택 화면 — `UIView_RoguelikeCardSelect` (기획서 4-1, 4-3, 10-2, 10-3)

| 할 일 | 받는 값 / 규칙 |
|---|---|
| 화면 열기 | `OpenSelection(제시 카드, 골드, 리롤 비용, 리롤 가능, 건너뛰기 가능, 첫 선택 여부)` |
| 카드 본체 | 특성 트리 노드 UI 사용, 이미지보다 **조금 더 넓은 프레임**, 희귀 나무 셰이더 참고. 카드 본체에는 텍스트를 넣지 않는다. 등급 프레임은 `displayGrade`(에메랄드·프리즘은 시스템이 자동 판정) |
| 레벨 아이콘 | 이미지 아래 중앙 `I`~`IV`, `MAX`. **구매 후 레벨**(`levelAfterPurchase`). 루비는 `bRuby` → `±` |
| 가격 | 우상단 코인 아이콘 + 숫자. `price`가 0이면 `무료`(에메랄드·프리즘·첫 선택). `bAffordable`이 false면 **붉은 숫자**, 구매 불가. 가격은 루비 할인/증가가 이미 반영된 값 |
| 설명 패널 | 카드 아래 넓은 박스. 커서가 올라간 카드의 이름(첫 줄) + 설명. 상태별 내용은 아래 📦 3-2 |
| 리롤 | `RequestReroll()`. 비용 `rerollCost`, 가능 여부 `bCanReroll`. 결과는 `ReplaceOffers(...)`로 온다 |
| 🆕 첫 선택 무료 리롤 | 첫 선택(`bFirstPick`)에는 리롤 비용이 0으로 오고 **1회** 쓸 수 있다. `무료 리롤` 표기. 쓰고 나면 `ReplaceOffers`의 리롤 가능이 false로 온다. 건너뛰기는 불가(`bCanSkip` false) |
| 건너뛰기 | `RequestSkip()`. 결과는 `CloseSelection(완료 콜백)` |
| 구매 | `RequestBuy(i)`. 결과는 `PlayPurchase(구매 정보, 함께 사라질 카드 인덱스, 완료 콜백)` |
| 거절 반응 | `PlayRejected(사유, 카드 인덱스)` (골드 부족 등) |
| 🆕 "골드가 부족합니다" 안내 | 제시된 카드가 **모두** `bAffordable == false`일 때 띄운다(기획서 4-3). 판정은 UI가 카드 목록으로 한다. 건너뛰기를 누르면 웨이브가 시작된다 |
| 🆕 제시 카드 0장인 선택 화면 | 감정사가 와 있고 맡길 카드가 있으면, 제시 카드가 0장이어도 선택 화면이 열린다(감정과 건너뛰기만 가능). `OpenSelection`이 빈 목록으로 와도 깨지지 않게 한다 |
| 조작 | 게임패드로 조작 가능해야 한다. 열리면 **제일 왼쪽 카드가 호버**된 상태. 커서는 **CursorBox UI**(OverPopupUI) 사용 |

⚠️ 반드시 지킬 것

- `PlayPurchase`/`CloseSelection`의 **완료 콜백을 꼭 부른다**. 불러야 시스템이 전투(티어 전환 웨이브면 지형 전환 연출)로 넘어간다. 안 부르면 10초 뒤 강제 진행된다.
- 보유 카드 창·ESC 메뉴가 위에 뜨면 `Time.timeScale = 0`이 된다. 카드 화면 연출은 **unscaled 시간**으로 돌린다.

카드 연출(기획서 10-3)

| 연출 | 상태 | 판단 근거 |
|---|---|---|
| 기본 등장: 뒤집히며 정면이 됨, 빠르게, N장 순차 | ✅ 확정 | `OpenSelection`·`ReplaceOffers` |
| 첫 등장 카드: 자물쇠가 풀리며 등장(강화 카드 제외) | ✅ 확정 | `bNewCard` |
| 레어 카드 등장: 희귀 셰이더로 번쩍, 프레임 디졸브(아래 → 위), 윗부분 번쩍 | ✅ 확정 | `bRare` (실버 이상·에메랄드·프리즘·루비) |
| 배타 카드 소멸: 구매 연출 뒤 함께 제시된 상충 카드가 빠르게 소멸(불타기·찢기 등) | 🟡 방식 미정 | `PlayPurchase`의 `_discardedIndices` |
| 카드 목록 연출, 일반·레어 카드 선택 연출 | 🚧 미정 | — |

### 1-3. 보유 카드 확인 — `UIView_RoguelikeOwnedCards` (기획서 10-2)

| 할 일 | 받는 값 / 규칙 |
|---|---|
| 카드 목록 | `SetCards(목록)` 후 `Show()`. 종류·레벨(`level`, 루비 `±`) |
| 설명 패널 | 고른 카드의 **현재 레벨 효과**(📦 3-2). 다음 레벨 효과는 미리 보여 주지 않는다 |
| 닫기 | `Hide()` |
| 일시정지 | 열려 있는 동안 시스템이 `Time.timeScale = 0`을 건다. 연출은 unscaled 시간으로 |

### 1-4. 결과 화면 — `UIView_RoguelikeResult` (기획서 8-2)

| 할 일 | 받는 값 / 규칙 |
|---|---|
| 화면 열기 | `OpenResult(RoguelikeResultViewData)` |
| 항목 | 도달 웨이브(`reachedWave`, 언리밋이면 `Unlimit-{unlimitWave}`), 플레이 시간(`playTimeSeconds`), 총 부순 나무(`treesFelled`), 총 획득 코인(`coinsCollected`), 보유 카드 목록(`cards`, 레벨 아이콘 달린 카드 그대로). 포기한 판은 `bAbandoned` |
| 신기록 연출 | `clearRank`/`unlimitRank`가 0 이상이면 그 순위로 랭킹 진입(-1이면 진입 못함) |
| 확인 | `RequestConfirm()`. **결과 화면을 직접 닫지 않는다** - 확인 직후 결과 화면 위에 디스토션이 걸리고, 암전이 덮은 뒤 시스템이 닫는다 |
| 구조·연출 | 본편 결과 UI의 구조와 연출을 비슷하게 가져간다 |

### 1-5. 순위표(기록 3탭) — `UIView_RoguelikeRecords` (기획서 8-1)

| 할 일 | 받는 값 / 규칙 |
|---|---|
| 화면 열기 | `OpenRecords(최근 1판, 클리어 Top5, 언리밋 Top5)`. 로비의 순위표 오브젝트를 치면 열린다 |
| 3탭 | 🕒 최근 기록 / 🏆 클리어 랭킹 / ♾️ 언리밋 랭킹 |
| 공통 표시 | 사용한 카드 목록(구매 + 해금한 히든 카드), 플레이 시간, 도달 웨이브. 클리어 랭킹의 비교 시간은 `rankingTimeSeconds` |
| 빈 칸 | `bValid == false`인 줄은 기록 없음 |
| 닫기 | `Hide()` |

## 2. v2.1에서 새로 생긴 UI (🔌 연결부터 필요)

아래는 시스템이 신호를 이미 보내지만, 코디네이터가 아직 구독하지 않고 뷰에 메서드도 없다.
연결은 기존 패턴 그대로 하면 된다: 코디네이터가 시그널을 받아 뷰 메서드를 부르고, 뷰 이벤트를 요청 시그널로 다시 보낸다.
시그널은 `Assets/Scripts/Application Layer/SignalSystem/Signals/RoguelikeSystemSignals.cs`, 읽을 값은 `IRoguelikeRunInfo`.

### 2-1. 카드 감정 (기획서 4-4, 10-3)

한 판에 한 번, 웨이브 10~20 중 하나(무작위)의 카드 선택 단계에 감정사가 온다.

| 순서 | 시스템 → UI | UI가 할 일 |
|---|---|---|
| 1 | `RoguelikeAppraiserNoticeSignal` (웨이브 10의 카드 선택 시작) | "감정사가 숲을 찾아온다" 예고 표시. 실제 등장 웨이브는 알려주지 않는다 |
| 2 | `RoguelikeCardSelectionOpenedSignal` 직후 `RoguelikeAppraiserArrivedSignal` | 카드 선택 화면에 감정 버튼·감정사 표시 |
| 3 | `IRoguelikeRunInfo.CanAppraise` | false면 **감정 버튼 잠금 표시**(맡길 카드가 없거나 골드가 가장 싼 감정비보다 적음). 리롤로 골드가 바뀌면 다시 읽는다 |
| 4 | `IsAppraisable(카드)`, `GetAppraisalFee(카드)` | 보유 카드 중 감정 대상을 고르게 하고 감정비를 보여 준다. 대상은 레벨 I~IV의 액티브·공통 패시브(MAX·루비·프리즘 제외), **서로 다른 카드로 최대 3장**, 같은 카드 반복 불가 |
| 5 | (UI → 시스템) `RoguelikeCardAppraiseRequestedSignal(카드)` | 감정비 지불 요청 |
| 6 | `RoguelikeCardAppraisedSignal(result)` | 결과 연출. `result.card`, `fromLevel` → `toLevel`(같으면 유지, 하락 없음), `feePaid`, `bPromoted`. 골드 변화는 `RoguelikeGoldChangedSignal`로도 온다 |
| 6' | `RoguelikeCardAppraisalRejectedSignal(사유, 카드)` | 거절 반응. 사유 `ECardAppraisalResult`: NotAvailable(감정사 없음) / NotEligible(대상 아님) / NotEnoughGold / LimitReached(3장 다 씀) |
| 7 | `result.bOffersChanged`가 true면 이어서 `RoguelikeOffersChangedSignal` | 승격된 카드가 제시 중이었으면 그 칸의 레벨·가격이 바뀌었거나(MAX가 되어) 다른 카드로 바뀌었다. 제시 목록을 다시 그린다 |

감정 결과 연출(🟡 방식 확정, 세부 미정): 돋보기 스프라이트가 카드 여러 곳을 조사 → 카드가 번쩍 → 레벨 표기(스티커)가 뜯기는 표시 →
유저가 마우스로 잡아 끌어내리면 결과 공개(게임패드는 스틱). **결과는 감정비를 낸 순간 이미 확정**되어 있다(속도·거리가 결과에 영향 없음).

주의

- 감정은 **선택 기회를 쓰지 않는다**. 감정 후에도 선택 화면은 열린 채로 구매·리롤·건너뛰기를 할 수 있다.
- 감정사가 있는 선택 단계가 닫히면(구매·건너뛰기) 남은 기회는 사라진다. 다음 선택으로 이월하지 않는다.
- 🔌 지금 코디네이터는 `RoguelikeOffersChangedSignal`을 받으면 리롤과 같은 `ReplaceOffers`를 부른다. 감정으로 바뀐 경우에는 카드 등장 연출을 다시 틀지 않도록 리롤과 구분해서 넘겨야 한다.
- 스티커 연출 도중 구매·건너뛰기로 선택이 닫히지 않게 할지는 UX에서 정한다.

감정비(기획서 4-4, 수치는 밸런스 단계에서 바뀜 - UI는 `GetAppraisalFee`만 쓴다)

| 보유 레벨 | 브론즈 | 실버 | 골드 | 결과 분포 (유지 / +1 / +2 / MAX) |
|---|---|---|---|---|
| I | 90 | 224 | 449 | 55% / 25% / 15% / 5% |
| II | 110 | 276 | 552 | 50% / 25% / 17% / 8% |
| III | 138 | 345 | 690 | 45% / 28% / — / 27% |
| IV | 66 | 165 | 330 | 65% / — / — / 35% |

### 2-2. "지금은 구매할 수 있는 카드가 없습니다" 안내 (기획서 4-3)

| 시스템 → UI | UI가 할 일 |
|---|---|
| `RoguelikeNoCardsAvailableSignal` (선택 화면 대신 옴) | 안내 연출을 재생하고, 끝나면 `RoguelikeCardSelectionPresentationDoneSignal`을 보낸다. 확인 입력 없이 웨이브가 시작된다. 조용히 넘기지 않는다 |

- 안내 뷰가 없으면 시스템은 `RoguelikeSystem` 프리팹의 `noCardsNoticeFallbackSeconds`(지금 1.5초)만 기다린다.
  **안내 연출이 이보다 길면 이 값을 연출 길이 이상으로 올린다**(아니면 연출 도중 웨이브가 시작된다).

## 3. 데이터·문구 준비 (📦)

### 3-1. 카드 이미지

- 초안 카드 28종(`Assets/Data/Roguelike/Cards/Draft/`)의 `icon`이 전부 비어 있다. 카드 이미지를 넣는다.

### 3-2. 설명 패널 문구 (기획서 5-6, 10-2)

| 카드 상태 | 설명 패널 내용 | 지금 데이터 |
|---|---|---|
| 처음 구매 | 이름 / 기본 설명 한 줄 | `nameKey`, `descriptionKey` 있음 (문구 미작성) |
| 업그레이드(II~IV) | 이름 / 기본 설명 / **이번 레벨의 변화 한 줄**(액티브는 변화 문구, 패시브는 누적 수치) | 📦 레벨별 문구 키 없음 |
| 에메랄드(MAX) | 이름 / 기본 설명 / MAX 효과 한 줄 | 📦 레벨별 문구 키 없음 |
| 프리즘 | 이름(`A + B`) / 조합 설명 | `nameKey`, `descriptionKey` 있음 |
| 루비 | 이름 / 버프 한 줄 / **디메리트 한 줄(붉은색)** | 📦 디메리트 문구 키 없음 |
| 보유 카드 창 | 현재 레벨 효과 | 📦 레벨별 문구 키 없음 |

- 카드 문구는 기획서 6장 카드 도감의 "카드에 표시되는 설명 문구"가 기준이다. 작성 규칙은 5-6(이름 그대로, 효과 한 줄, `도끼 공격`, `지속 피해`·`유지 시간` 용어).
- 📦 레벨별 변화 문구·MAX 문구·루비 디메리트 문구를 담을 키가 카드 데이터와 `RoguelikeCardViewData`에 없다. 필요한 형식을 정해 시스템 쪽에 요청한다.
- I~V 전체 효과 목록은 표시하지 않는다(확정). MAX 효과는 에메랄드 카드에서만 공개한다.
- 조합(히든 카드) 정보는 어떤 UI에도 미리 보여 주지 않는다(기획서 4-3).

### 3-3. 그 밖의 문구

- 감정사 예고, 감정 버튼·잠금, 감정 결과, "골드가 부족합니다", "지금은 구매할 수 있는 카드가 없습니다", `무료`, `무료 리롤`, `Unlimit-N`, 결과 화면·순위표 항목명.
- ESC 메뉴의 `메인메뉴` → `로비` 표기는 이미 들어가 있다(번역 검수만 필요).

## 4. 그 밖의 UI

| 항목 | 할 일 |
|---|---|
| 메인메뉴 로그라이크 버튼 | **아직 없다.** `UI_MainMenu`에 버튼을 추가하고 `parentView.OnRoguelikeButtonClicked()`를 부른다. 해금 조건(본편 모든 특성 + 엔딩)은 엔딩이 없어 개발 중 상시 활성 |
| 로비 오브젝트 외형 | 게임 시작·순위표 오브젝트(`Assets/Prefabs/Roguelike/LobbyObject_*.prefab`)의 `Visual` 자식을 교체. 타격 반응은 `HitReactionEvent`. 버튼을 누르지 않고 캐릭터로 쳐서 실행한다(기획서 9-2) |
| 게임패드 | 카드 선택(커서·호버 시작 위치), 보유 카드 창 키(미정 → 테스트 버튼), 감정 스티커 끌기(스틱) |

## 5. UI가 하지 않아도 되는 것

- 로비 진입·결과 → 로비 연출(디스토션·암전·바닥 타일 파도·낙하), 티어 전환 파도, 런 클리어 줌인·슬로우, 웨이브 클리어 묘목·코인 흡수: 시스템이 한다.
- 결과 화면 닫기 시점, 일시정지(`timeScale`) 걸고 풀기, 카드 판단(등급·가격·살 수 있는지·처음 보는 카드·배타 소멸 대상): 시스템이 끝내서 넘긴다.
- 스킬·조합 이펙트: 이펙트 담당(`RoguelikeSkillVfxSignal`).

## 6. 확인하는 법

- 실행: [로그라이크 시스템 안내](../RoguelikeSystem.md) 0장. `MainMenuScene`에서 Play → `Tools/Roguelike/Debug Window` → "메인메뉴 → 로그라이크 진입".
- 카드 선택: 런 시작 → 첫 선택. "골드 추가"로 가격·붉은 가격·리롤 비용을, "카드 1단계 지급"으로 업그레이드·에메랄드 표시를 본다.
- 결과 화면: 전투 중 "스태미너 피해"에 큰 값. 순위표: 로비에서 "순위표 오브젝트 타격".
- 언리밋 HUD: "다음 웨이브 번호 지정"에 31.
- `RoguelikeSystem` 프리팹은 지금 임시 카드 세트(`RoguelikeCardDatabase.asset`, 9장)로 돈다. v2.1 카드 28종으로 보려면 프리팹의 `cardDatabase`를 `RoguelikeCardDatabase_Draft.asset`으로 바꾼다(커밋 여부는 기획과 맞춘다).
- 감정사는 웨이브 10~20 중 무작위라 강제로 부르는 디버그 버튼이 아직 없다. 감정 UI 작업을 시작할 때 필요하면 요청한다.
- 플레이 중에 스크립트를 고치지 않는다(도메인 리로드로 흐름이 멈춘다).
