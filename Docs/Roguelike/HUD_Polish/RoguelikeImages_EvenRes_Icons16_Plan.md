# 로그라이크 이미지 정리 계획표: 16x16 아이콘 통일 · 짝수 해상도 · 카드 참조 복구 (2026-11-10 승인)

> 작업자: 이 문서가 사양이다. 모호하면 추측하지 말고 멈추고 보고한다. 프로젝트 규칙은 `CLAUDE.md`를 따른다.
> 참고 자료는 같은 폴더 `icons16/`에 있다. 아이콘 PNG 7개(원본), `hud16_states.png`(4배 확대 배치 시안), `hud16_full.png`(1920x1080)가 있다. 시안 렌더러와 검사 스크립트(refscan · usage)는 리포지토리에 포함하지 않는다.
> 이전 작업 계획표: `RoguelikeHUD_Polish_Plan.md`(같은 폴더). 색과 막대 규칙은 그대로다.

## 배경 (사용자가 직접 바꾼 것)
- 사용자가 `Assets/Graphics/Roguelike/hud/HudIconBolt.png`를 직접 16x16으로 다시 그렸다. **이 파일은 절대 덮어쓰지 않는다.**
- 사용자가 여러 이미지의 임포트 설정을 Single / PPU 32 / Full Rect / Extrude 1 / Pivot Center / Generate Physics Shape on / Clamp / Point / Max 2048 / Mitchell / Automatic / 압축 None으로 바꿨다.
  - 그 결과 `card/CardFrame, CardProfile, I, II, III, IV, V`가 Multiple → Single이 되어 `UI_RoguelikeCard.prefab`의 스프라이트 참조 7개가 끊겼다(예전 조각 ID 예: CardFrame_0 = -1306988184310151341).
  - 이 설정은 **되돌리지 않는다.**

## 1. 16x16 아이콘 교체
- `icons16/HudIcon{Tree,Axe,Flag,FlagGold,FlagWhite,Infinity,Dash}.png`(각 16x16)를 `Assets/Graphics/Roguelike/hud/`의 같은 이름 파일에 **PNG 내용만 덮어쓴다**.
  - `.meta`(GUID)는 유지해 프리팹 참조가 그대로 이어지게 한다. 임포트 설정은 배경의 사용자 설정과 같게 맞춘다.
- `RoguelikeHUDBuilder`의 `MakeIcon`(그리고 같은 방식으로 HUD 이미지를 만드는 `Make` · `MakeFresh`):
  - **파일이 이미 있으면 읽기만 하고 다시 쓰지 않는다.** 파일이 없을 때만 생성한다.
  - 아이콘 행렬 데이터는 16x16 원본과 맞지 않게 되므로, 아이콘은 생성 경로를 없애고 "파일이 없으면 오류 로그" 방식으로 바꾼다. 다른 생성물(막대 틀 등)은 없을 때만 생성한다.
- 배치(640x360 캔버스, 좌상단 원점, 이미지 16x16의 좌상단 좌표):

| 아이콘 | x | y | 비고 |
|---|---|---|---|
| 깃발(일반) / 금색 깃발 / 흰 깃발 | 4 | 295 | 같은 자리에서 스프라이트만 바뀜 |
| ∞ (언리밋) | 17 | 295 | 언리밋 번호 잉크 왼쪽 x=32, 점은 번호 뒤 +5 |
| 나무 | 4 | 310 | |
| 도끼 | 4 | 326 | |
| 번개(사용자 파일) | 4 | 340 | |
| 대쉬 | 131 | 340 | 대쉬 칸 1 / 2를 x 146 / 161로 이동(y는 짝수 맞춤 후 기준, 3. 참고) |

- 웨이브 숫자(x 19), 막대(x 19), 막대 뒤 숫자(x 135) 위치는 그대로다.
- 칸 크기는 빌드할 때 실제 스프라이트 크기(`sprite.rect.size`)를 따른다. 컴포넌트 코드에 아이콘 크기나 위치 상수(예: 깃발 9x9, ∞ 9x6 오프셋, 언리밋 번호 위치)가 있으면 같이 고친다.
- 결과 화면은 `icons16/hud16_states.png`와 같아야 한다.

## 2. 짝수 해상도 (Assets/Graphics/Roguelike 아래 전부)
- 규칙: 홀수인 축에만 **투명 1px을 오른쪽 열(가로) / 위쪽 행(세로)**에 덧붙인다. 원래 픽셀은 바꾸지 않는다.
- 그 스프라이트를 쓰는 모든 Image · RawImage의 칸 크기를 새 크기로 맞추고, **원래 그림이 화면의 같은 픽셀에 남도록** 위치를 보정한다.
  - 피벗과 앵커가 (0,0)이면 오른쪽 · 위로 늘어나니 위치는 그대로다. 다른 피벗이면 계산해서 보정한다.
- 런타임에 크기나 위치를 정하는 코드가 있으면 같이 고친다(예: 카드 파티클 `Fx_Dot1` 1x1 → 2x2, `Fx_Bar2` 2x1 → 2x2, `Fx_PlusS/L`; 트레이 숫자판 상수 `UI_RoguelikeOwnedCardTray.PlateWidth/PlateHeight`; 대쉬 칸 크기).
- 9-슬라이스(`owned/Scroll_Thumb`, `Scroll_Track` 3x6, border y/w 2)는 오른쪽 투명 열 추가 후에도 테두리 값과 늘어나는 방향이 맞는지 확인한다.
- 이 이미지를 만드는 빌더(`RoguelikeHUDBuilder`, `RoguelikeCardBuilder`, `RoguelikeCardSelectBuilder`, `RoguelikeOwnedCardsBuilder`, `RoguelikeLobbyUIBuilder`)도 짝수 크기로 생성하도록 고친다(1.의 "없을 때만 생성" 규칙과 함께).
- 대상(현재 홀수):
  - card: `Card_Back`, `Card_FrameFull` 50x81, `Card_Shadow` 40x5, `Fx_EdgeStrip` 4x77, `Fx_Dot1` 1x1, `Fx_Bar2` 2x1, `Fx_PlusL` 5x5, `Fx_PlusS` 3x3, `Icon_Reroll` 13x13
  - hud: `BarFrame14x9`, `GraceFrame110x3`, `TMarker` 5x3, `TrayPlate`, `TrayPlateLit1`, `TrayPlateLit2` 34x19, `TrayCardBack`, `TrayCardFront` 7x9, `IconAxe` 7x7, `IconFlag` 9x9, `IconInfinity` 9x7, `IconTree` 7x7(메인메뉴 `UI_MainMenu.prefab`에서 사용)
  - owned: `Scroll_Thumb`, `Scroll_Track` 3x6
  - HudIcon 7개는 1.에서 16x16이 되므로 대상이 아니다.
- 파일 이름에 크기가 들어간 것(`BarFrame14x9`, `GraceFrame110x3`)은 이름 변경 여부를 사용자에게 묻지 않고 **이름은 그대로 둔다**(GUID 유지가 우선).
- 끝나면 `Assets/Graphics/Roguelike` 아래 모든 PNG가 가로 · 세로 짝수인지 스크립트로 확인한다.

## 3. 임포트 설정 통일
- 빌더가 새로 만들거나 설정을 고치는 텍스처는 배경의 사용자 설정과 같게 맞춘다: Sprite, Single, PPU 32, Full Rect, Extrude 1, Pivot Center, Physics Shape on, Clamp, Point, 밉맵 끔, 알파 투명, 압축 None, Max 2048.
- 지금 `RoguelikeHUDBuilder`는 PPU 100을 넣는 곳이 있다(약 535행). 사용자가 이미 바꾼 설정을 빌더가 되돌리지 않게, 설정 확인 조건에 PPU · 메쉬 타입을 포함한다.

## 4. 카드 참조 복구
- `RoguelikeCardBuilder`는 `CardFrame`, `CardProfile`, `I`~`V`를 예전 Multiple 조각(잘린 사각형) 기준으로 배치한다(헤더 주석: Frame (0,0), Profile (8,38) 등).
- 이제 Single + Full Rect라 스프라이트가 **전체 캔버스**(카드 64x96, 로마 숫자 32x32)다.
  - 예전 조각 사각형은 git `HEAD`의 각 `.meta`(`spriteSheet.sprites[].rect`)에 있다. 이를 읽어 새 위치 = 예전 위치 − 조각 오프셋으로 보정한다.
  - 화면상 같은 픽셀에 그려지게 빌더를 고친 뒤 메뉴 `LumberBoy/UI/Build Roguelike Card`로 `UI_RoguelikeCard.prefab`을 다시 만든다.
- `UI_RoguelikeCard`를 다시 쓰는 프리팹(카드 선택, 보관함, 결과 등)이 카드 프리팹을 중첩으로 쓰는지 확인하고, 필요하면 해당 빌더도 다시 실행한다.
- 런타임 코드가 로마 숫자 스프라이트를 바꿔 끼우면서 크기나 위치를 정하면 같이 고친다.
- 사용자가 다른 의도로 카드를 바꿨을 수 있으므로, 빌더 재실행으로 카드 프리팹이 크게 달라지면 diff 요약을 보고한다.

## 5. 바꾸지 않는 것
- `HudIconBolt.png`(사용자 그림)와 그 설정.
- 사용자가 바꾼 임포트 설정(Single 등).
- Application Layer 전부, 공개 메서드 시그니처, 기존 변수명, 에셋 GUID.
- 작업과 무관한 미커밋 변경(전적 화면, 튜토리얼 퀘스트, CHANGELOG 등).
- git 커밋 금지.

## 6. 검수 (작업자)
1. 컴파일 오류와 경고가 없어야 한다.
2. 프로젝트 전체 프리팹 · 씬에서 로그라이크 스프라이트 참조가 끊기지 않았는지 확인한다(2026-11-10 확인: 0개).
3. `Assets/Graphics/Roguelike` 아래 PNG 전부가 짝수 해상도인지 확인한다.
4. `HudIconBolt.png`가 작업 전과 바이트 단위로 같은지 확인한다(작업 시작 전에 해시를 기록한다).
5. **작업 전에** 플레이 모드 기준 캡처를 먼저 찍어 둔다: HUD(평상시), 카드 선택 화면, 보유 카드 창(인벤토리 키), 메인메뉴 로그라이크 버튼. 작업 후 같은 화면을 찍어 비교한다.
   - HUD는 `hud16_states.png`와 같아야 한다.
   - 나머지 화면은 그림이 같은 픽셀에 있어야 한다. 카드 참조 복구로 카드 틀 · 초상 · 로마 숫자가 다시 보여야 한다.
   - 진입 방법은 이전 계획표 7.과 같다.
     - `UIView_MainMenu.OnPressAnyKeyCompleted()` → `OnRoguelikeButtonClicked()`
     - `RoguelikeSystem.Phase == CardSelect`이면 카드 선택 화면 캡처 → `DebugBuyOffer(0)`으로 전투
   - 캡처 저장 위치: `C:/Users/xarek/AppData/Local/Temp/claude/8df19ef0c106e315/bc0d1ac0-16e5-4f7f-880f-b171e68ffe0f/scratchpad/impl2/`
6. 보고: 바꾼 파일, 덧붙인 이미지 목록(전 → 후 크기), 위치를 보정한 칸, 캡처 경로, 확인하지 못한 화면.
