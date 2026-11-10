# 로그라이크 전투 HUD 폴리싱 구현 계획표 (2026-11-10 승인)

> 작업자: 이 문서와 같은 폴더의 시안 이미지가 **확정 사양**이다. 시안과 픽셀 단위로 같게 만드는 것이 목표다.
> 막히거나 사양이 모호하면 추측해서 진행하지 말고 작업을 멈추고 보고한다.
> 프로젝트 규칙은 `CLAUDE.md`를 따른다(Yoda 비교, 명시적 접근 제어자, 필드 camelCase, 매개변수 `_camelCase`, 라이프사이클 함수는 클래스 최하단, LINQ/람다 클로저 금지, GC 0, 기존 변수명 변경 금지, 미사용 잔재 정리, 네임스페이스 누락 금지).

## 0. 참고 자료 (같은 폴더)

| 파일 | 내용 |
|---|---|
| `ref_final_full.png` | 1920x1080 실제 게임 화면 위 최종 시안(평상시, 대쉬 2칸) |
| `ref_native_1x.png` | 640x360 캔버스 기준 1배 원본 픽셀. **좌상단이 캔버스 (0, 286)** 이다. 픽셀 검수 기준 |
| `ref_states.png` | 4배 확대: 평상시 / 유예 중 / 피해 쿨다운 / 대쉬 해금 전 |
| `ref_unlimit.png` | 언리밋 웨이브 표시. **왼쪽(A안)만 사용**, 오른쪽(왕관 + 최고 기록)은 쓰지 않는다 |
| `ref_shadow_zoom.png` | 8배 확대: 그림자 확인용 |
| `mock6.py` (+`mock2.py`) | 시안을 그린 파이썬 렌더러(리포지토리에 포함하지 않음). 숫자 글리프는 `Assets/Graphics/Font/SmallCurrencyNum.png` |

## 1. 바뀌는 것 요약

1. 전투 HUD를 **하단 중앙 → 좌측 하단**(화면 여백 8px)으로 옮기고, 요소를 세로로 쌓는다(위→아래: 웨이브 줄 / 임계치 바 + 유예 줄 / 처치 바 / 스태미너 바 + 대쉬 2칸).
2. 막대 디자인 교체: 스태미너 미니 버전 스프라이트(MiniFrame 등)와 젤리 머리(Head)를 버리고, 단순한 캡슐(모서리 1px 비움) + 3톤 채움으로 바꾼다.
3. **반투명 1px 픽셀 금지**. 모서리 반투명 점, 막대 아래 1px 반투명 그림자 같은 것은 없다. 그림자는 **HUD 실루엣을 그대로 복제해 (-1, +1)(왼쪽 아래 대각선)에 깐 한 장**만 쓴다.
4. 스태미너 · 대쉬는 로그라이크에서 본편 바(HUD_Stemina / HUD_DashGauge)를 **화면에서만 숨기고**, 같은 호출을 새 로그라이크 전용 컴포넌트로 전달한다.
5. 언리밋 웨이브: 금색 깃발 + ∞ + 단계 번호(금색) + 10칸 점(10웨이브마다 처음부터).

## 2. 좌표 사양 (640x360 캔버스, 좌상단 원점, 윤곽 포함 크기)

UGUI 변환: 앵커 · 피벗 (0,0)일 때 `anchoredPosition = (x, 360 - y - h)`. 루트는 화면 전체 스트레치 그대로.

| 요소 | x | y | w x h | 비고 |
|---|---|---|---|---|
| 깃발 아이콘(윤곽 포함) | 7 | 296 | 9 x 9 | 행렬 7x7 + 윤곽 1px |
| 웨이브 숫자 | 잉크 왼쪽 19 | 잉크 위 298 | 높이 7 | 글자 전진 4px, 윤곽 공유 |
| 진행 점 | 19 + (4×글자수+1) + 5 | 300 | 칸 2x2, 간격 3, 10칸마다 +2 | 현재 칸만 2x3(y 299~301) |
| 나무 아이콘 | 7 | 312 | 9 x 10 | |
| 임계치 바 | 19 | 313 | 112 x 8 | 안쪽 (20,314) 110x6 |
| T 표식 | 눈금 x-2 ~ x+2 | 310 ~ 312 | 5-3-1 역삼각 | 아래 3. 참고 |
| 유예 줄 | 채움 21~128 | 321~322 | 108 x 2 | 바 아래 윤곽(y 320) 공유, 옆 윤곽 x 20 · 129, 아래 윤곽 y 323 |
| 나무 숫자 | 잉크 왼쪽 135 | 313 | | 초과 시 글자색 `#FF9A80` |
| 도끼 아이콘 | 7 | 328 | 9 x 9 | |
| 처치 바 | 19 | 329 | 112 x 8 | |
| 처치 숫자 | 135 | 329 | | 960/960까지 들어가야 함 |
| 번개 아이콘 | 8 | 343 | 7 x 8 | 행렬 5x6 |
| 스태미너 바 | 19 | 342 | 112 x 10 | 1/4 · 1/2 · 3/4 눈금: 안쪽 맨 아래 행 1px `OUT` |
| 대쉬 아이콘 | 134 | 344 | 8 x 7 | 행렬 6x5 |
| 대쉬 칸 1 / 2 | 143 / 158 | 343 | 14 x 9 | 같은 캡슐 규칙 |
| 보관함 트레이 | 기존 위치 유지(우하단) | | | 변경 없음 |

## 3. 그리기 규칙

### 색 (sRGB hex)

| 이름 | 값 |
|---|---|
| OUT(윤곽) | `#2A1A12` |
| TR(홈) / TR_D(홈 위 1행) | `#4B3427` / `#3A281D` |
| 초과 구간 홈 / 위 1행 | `#4C2722` / `#3C1D1A` |
| WHITE | `#FFF4DC` |
| 그림자 | `#1A0E08`, 알파 120/255, 실루엣 전체 균일 |
| 스태미너 (밝은 줄, 기본, 그림자) | `#C8E67A`, `#8FC24A`, `#5F8F34` |
| 임계치 기본 | `#FFD27A`, `#F0A43A`, `#B9702A` |
| 임계치 초과 | `#FF8A6E`, `#D8443A`, `#9A2A28` |
| 처치 | `#F4A0AC`, `#D05A6E`, `#933A4E` |
| 유예(위, 아래) | `#C9A6FF`, `#9B6EE8` |
| 쿨다운(위, 아래) | `#FFB487`, `#F07F45` |
| 대쉬 | `#D2F6FF`, `#5CC8E2`, `#2E86A8` |
| 피해 잔상 | `#FFF4DC`, `#F3DCC6`, `#D9BCA4` |
| 무적(스태미너) | `#FFF0B8`, `#F2CF5A`, `#B8902E` |
| 진행 점: 지난 칸(위, 아래) / 남은 칸 | `#E6B450`, `#A87A2C` / `#5A4334` |
| 언리밋 금색 숫자 | `#FFE39A` |

### 캡슐 막대

- 윤곽: 위 · 아래 행은 x+1 ~ x+w-2, 좌 · 우 열은 y+1 ~ y+h-2. **네 모서리 픽셀은 비운다(투명)**.
- 홈: 안쪽 전체 TR, 맨 위 1행 TR_D. 임계치 바는 T 위치부터 오른쪽 끝까지 초과 구간 홈 색.
- 채움(구간 [a, b), 정수 px): 안쪽 0행 = 밝은 줄, 마지막 2행(안쪽 높이 6 미만이면 1행) = 그림자, 나머지 = 기본.
  - 폭 3px 이상이면 끝 열(b-1)의 1행부터 아래를 그림자 색으로 그린다(끝 경계).
  - 폭 8px 이상인 **첫 구간만** 0행의 a+2, a+3 두 픽셀을 WHITE로 그린다(반짝임).
  - 임계치 초과 시 [0, T)는 기본 팔레트, [T, 현재)는 초과 팔레트.
- 채움 폭은 항상 정수 px로 스냅한다(기존 EndReserve · 젤리 머리 방식 폐기).

### T 표식

- 바 안쪽 T 열 전체를 WHITE로 그린다.
- 바 위: y-3 행은 x-2 ~ x+2 OUT, y-2 행은 x-1 OUT · x WHITE · x+1 OUT, y-1 행은 x OUT. 모두 불투명.

### 아이콘 행렬 (`.` = 투명, 윤곽은 행렬 바깥 4방향 1px `OUT`)

```
나무 7x8           a #8FC24A  b #5F8F34  t #8A5A36
...a...
..aab..
.aaabb.
..aab..
.aaabb.
aaaabbb
...t...
..ttt..

도끼 7x7 (게임 도끼: 회색 날 + 빨간 머리 + 갈색 자루)
g #E3E7EC  G #9AA4B2  r #D84A45  R #9E2A2C  w #A8744A
..ggrr.
.gGrrrR
.gGrrRR
..GrRR.
..ww...
.ww....
ww.....

깃발 7x7   p #C9B89C  f #E04B3E  g #A8322C   (언리밋: f #FFD060, g #C8962E)
pff....
pfffg..
pffgg..
pgg....
p......
p......
p......

번개 5x6   l #D6EF86  d #9CC24E
...ll
..ld.
.llld
llld.
..ld.
.ld..

대쉬 6x5   l #9FE6F5
l..l..
ll.ll.
.ll.ll
ll.ll.
l..l..

무한대 7x4  i #FFD060
.ii.ii.
i..i..i
i..i..i
.ii.ii.
```

슬래시 글리프: `SmallCurrencyNum.png`에 없으므로 같은 규격(8x8, 흰 심 + 검은 윤곽)으로 만든다. 흰 심 픽셀 (5,2)(5,3)(4,4)(4,5)(3,6), 그 주변 8방향을 윤곽으로 채운다(시안 렌더러 기준). 기존 HUD에서 슬래시를 어떻게 그리고 있는지 먼저 확인하고, 이미 있으면 그것을 쓴다.

### 그림자 구현

- UGUI 내장 `UnityEngine.UI.Shadow` 컴포넌트를 쓴다: `effectDistance = (-1, -1)`(UI 좌표는 y가 위라서 화면상 왼쪽 아래), `effectColor = #1A0E08 / 알파 120`, `useGraphicAlpha = true`.
- **실루엣 바깥을 이루는 그래픽에만** 붙인다: 각 막대 · 대쉬 칸의 배경(윤곽) 그래픽, 아이콘, 숫자 그래픽, 진행 점, T 표식, 유예 줄 윤곽. 막대 안쪽 그래픽(채움, 끝 열, 반짝임, 초과 홈)에는 붙이지 않는다. 안쪽 그래픽의 그림자가 홈 위에 그려지면 안 된다.
- 결과가 `ref_shadow_zoom.png`처럼 HUD 실루엣 전체를 복제한 한 장으로 보여야 한다. 겹친 그림자가 진해지는 곳이 있는지 확인한다(있으면 그 그래픽의 Shadow를 뺀다).

## 4. 동작 · 연출 (24Hz 틱, unscaled, 알파 페이드 · 화면 흔들림 없음)

| 요소 | 사양 |
|---|---|
| 채움 변화 | 기존처럼 지수 보간(τ 0.12초) 후 정수 px 스냅 |
| 값 증가 | 채움 끝 열을 2틱 동안 밝은 줄 색으로 |
| 스태미너 피해(PlayDamaged) | 줄어든 구간을 피해 잔상 팔레트로 0.15초 유지한 뒤 0.25초에 걸쳐 현재 값까지 줄어듦 |
| 스태미너 회복(PlayRecovered / PlayRecoverWasted) | 채움 끝 열 2틱 WHITE |
| 스태미너 위험(SetForcedWarning의 바 경고) | 6틱마다 기본 ↔ 임계치 초과 팔레트 교대(2Hz). 흔들림 없음 |
| 스태미너 무적(SetInvincible) | 원인 하나라도 켜져 있으면 무적 팔레트. 원인별 비트로 관리 |
| T를 넘는 순간 | T 표식을 3틱 숨김 · 3틱 표시를 2회 |
| 임계치 피해(PlayDamaged) | 채움 전체를 2틱 WHITE. 기존 흔들림 패턴은 제거 |
| 처치 목표 달성 | 숫자 `#FFD060`, 2px WHITE 열이 채움을 왼쪽→오른쪽으로 7틱에 훑음(1회) |
| 웨이브 전환 | 현재 점이 다음 칸으로, 깃발 기존 깜빡임(6틱) 유지, 숫자 홉(HopPattern) 유지 |
| 대쉬 충전 완료(SetCharge가 1에 도달) | 그 칸 채움을 2틱 WHITE |
| 대쉬 해금(SetUnlocked true) | 바로 나타남(기존 스케일 튕김 등장 없음) |
| 퇴장(SetCombatVisible false) | 좌측 하단 요소 전부 **왼쪽**으로 빠짐(기존 거리 · 시간), 트레이는 기존대로 오른쪽 |

## 5. 파일별 작업

### 5-1. `Assets/Scripts/Editor/Roguelike/RoguelikeHUDBuilder.cs`
- 위 좌표로 다시 배치하고 앵커를 좌측 하단으로 바꾼다(트레이 제외).
- 새 스프라이트를 `Assets/Graphics/Roguelike/hud/`에 만든다: 막대 윤곽 + 홈 112x8 · 112x10 · 14x9(이름 예: `BarFrame112x8`), 채움 행 스트립, 아이콘 7종(나무 · 도끼 · 깃발 · 금색 깃발 · 번개 · 대쉬 · 무한대, 윤곽 포함), T 표식, 유예 줄 윤곽, 슬래시 글리프(필요 시).
- 쓰지 않게 되는 스프라이트(MiniFrame, MiniFill*, MiniGrace*, IconAxe/Tree/Flag/Infinity 구버전)는 빌더에서 생성 코드를 지우고, 다른 곳에서 참조하지 않는지 전수 검색한 뒤 결과를 보고한다. **에셋 파일 삭제는 하지 말고 목록만 보고**한다.
- 그림자 Shadow 컴포넌트를 3. 규칙대로 붙인다.
- 스태미너 · 대쉬 오브젝트를 새로 만들고 `UIView_RoguelikeHUD`에 연결한다.
- 헤더 주석의 배치 설명을 새 배치로 고친다.
- 끝나면 메뉴 `LumberBoy/UI/Build Roguelike HUD`를 실행해 프리팹을 다시 만든다.

### 5-2. 기존 요소 컴포넌트 (`Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/`)
- `UI_RoguelikeWaveLabel.cs`: 30칸 진행 점(일반), 10칸 진행 점 + 금색 깃발 + ∞ + 금색 번호(언리밋). 기존 `SetWave(int, bool, int)` 시그니처 유지. 언리밋 칸 = `(unlimitIndex - 1) % 10 + 1`.
- `UI_RoguelikePressureStrip.cs`: 새 채움 규칙, 초과 구간 홈, T 표식(+넘는 순간 깜빡임), 초과 시 숫자색, 피해 시 2틱 흰색. 젤리 머리(Head) · 흔들림 제거.
- `UI_RoguelikeThresholdBar.cs`: 새 채움 규칙, 숫자를 막대 뒤(x 135)로, 목표 달성 연출. 젤리 머리 제거.
- `UI_RoguelikeGraceTimer.cs`: 임계치 바에 붙은 2px 줄. 5행 주석의 존재하지 않는 `SetTimer` 언급 삭제.
- `UI_RoguelikeIconNumber.cs`: 글자색(틴트) 지정이 없으면 추가한다.
- `RoguelikeJellyHead.cs`: 더 이상 쓰지 않게 되면 참조처를 전수 확인해 보고만 한다(파일 삭제는 사용자 확인 후).
- 공개 메서드 이름 · 시그니처는 **바꾸지 않는다**(`RoguelikeUICoordinator`가 부른다).

### 5-3. 신규 컴포넌트 (같은 폴더)
- `UI_RoguelikeStaminaBar.cs`: `UpdateValue(float)`, `PlayDamaged(float)`, `PlayRecovered(float)`, `PlayRecoverWasted()`, `SetForcedWarning(bool _bBarWarning)`, `SetInvincible(EStaminaInvincibleSource, bool)`, `SetVisible(bool)`. 4. 표 사양대로 연출한다.
- `UI_RoguelikeDashCells.cs`: `HUD_DashGauge`와 같은 입력을 받는다. `SetUnlocked(bool)`, `SetMaxCharges(int)`, `SetCharge(int, float)`, `SetChargeSilently(int, float)`, `ResetCharges(float)`. 잠겨 있으면 아이콘 · 칸 모두 숨긴다. 최대 칸 수가 1이면 두 번째 칸을 숨긴다.
- 정적 연결점 `RoguelikeHUDLink.cs`(static class): `public static UI_RoguelikeStaminaBar StaminaBar`, `public static UI_RoguelikeDashCells DashCells`. `UIView_RoguelikeHUD.Initialize`에서 넣고 `OnDestroy`에서 비운다(자기 것일 때만 비운다).

### 5-4. `UIView_RoguelikeHUD.cs`
- 새 컴포넌트 필드(`staminaBar`, `dashCells`)를 추가하고 `RoguelikeHUDLink`에 등록한다.
- 퇴장 대상 배열 5 → 7(스태미너, 대쉬 추가). 방향은 웨이브 · 바 · 스태미너 · 대쉬 모두 `Vector2.left`, 트레이는 `Vector2.right`.
- `ApplyCombatVisible`에 새 요소를 포함한다.
- 헤더 주석(9행 "화면 상단 중앙" 등)을 실제 배치로 고친다.

### 5-5. `Assets/Scripts/Presentation Layer/UISystem/UIView/HUD/UIView_HUD.cs` (사용자 결정: 기존 신호에 묶기)
- `SetEquipmentHiddenForRun(true)`이면 "로그라이크 런" 상태로 본다(기존 동작은 그대로 유지).
- 런 상태 + `RoguelikeHUDLink`에 대상이 있으면:
  - 본편 스태미너 바와 대쉬 게이지는 **동작은 그대로 두고 화면에서만** 숨긴다. 루트에 `CanvasGroup`을 두고 alpha 0. `HUD_Stemina.SetForcedWarning`의 붉은 화면(BloodScreen)은 계속 본편 쪽에서 나와야 하므로 오브젝트를 끄지 않는다.
  - 다음 호출을 새 컴포넌트에도 똑같이 전달한다: `UsedSteminaEvent`(UpdateValue), `OnStaminaDamaged`, `OnStaminaRecovered`, `OnStaminaRecoverWasted`, `SetRoguelikePressureWarning`(바 경고만), `StaminaInvincibleStateChanged`, `UpdateDashCharges`, `UpdateDashRecharge`, `ApplyDashCells`.
- `SetEquipmentHiddenForRun(false)`이면 CanvasGroup을 1로 돌린다.
- 링크 대상이 런 도중에 늦게 생겨도 동작하도록, 전달할 때마다 링크를 확인한다(null 비교만, 할당 없음).
- 본편(로그라이크가 아닐 때)의 동작과 배치가 하나도 바뀌지 않아야 한다.

### 5-6. Application Layer 요청 문서 (신규)
- `Docs/Roguelike/HUDLayout_AppLayer_Request.md`를 기존 `OwnedCards_AppLayer_Request.md`와 같은 형식으로 작성한다.
- 내용:
  - 지금은 `SetEquipmentHiddenForRun`에 로그라이크 스태미너 · 대쉬 전환을 묶었고, 정적 `RoguelikeHUDLink`로 연결했다는 사실.
  - 코디네이터가 `UIView_HUD`에 로그라이크 HUD를 직접 넘기는 정식 메서드(예: `SetRunHud(UIView_RoguelikeHUD)`)로 바꾸는 선택지.
  - 로그라이크에서 대쉬가 해금되는지(`dashState.bUnlocked`) 확인 요청. `UIView_HUD`의 주석에는 "로그라이크에서는 false"라고 적혀 있다.
- **Application Layer 코드는 수정하지 않는다.**

## 6. 바꾸지 않는 것
- `Assets/Scripts/Application Layer/` 전부, `RoguelikeUICoordinator`, 시그널.
- 본편 HUD 프리팹(`HUD_Stamina.prefab`, `HUD_StaminaGroup.prefab`)과 그 배치.
- 보관함 트레이의 위치와 디자인.
- 공개 메서드의 이름 · 시그니처, 기존 변수명.

## 7. 검수 (작업자가 할 것)
1. 컴파일 오류와 경고(CS0414, CS0168 등)가 없어야 한다.
2. 빌더를 실행해 프리팹을 다시 만든다.
3. 플레이 모드에서 확인한다.
   - 메인메뉴에서 시작해 `Tools/Roguelike/Debug Window`의 진입 경로나 `UIView_MainMenu.OnRoguelikeButtonClicked`로 런에 들어가고, `RoguelikeSystem.DebugBuyOffer(0)`으로 전투에 들어간다.
   - 화면을 캡처해 `ref_final_full.png`와 비교한다. 캡처는 `ScreenCapture.CaptureScreenshot`으로 받고, 파일은 scratchpad에 둔다.
   - 확인할 상태: 평상시, 임계 초과(유예), 피해 쿨다운, 스태미너 피해(`DebugDamageStamina`), 웨이브 전환(`DebugCompleteWave`), 언리밋(`DebugSetNextWave`로 31 이상), 퇴장(결과 화면).
   - 각 상태의 캡처 경로를 보고한다.
4. 본편(로그라이크 아님) 던전에서 스태미너 바가 이전과 같은지 확인하거나, 확인하지 못했으면 그렇다고 보고한다.
5. 바꾼 파일 목록, 남긴 잔재(미사용 에셋 목록), 시안과 다른 점을 보고한다.
