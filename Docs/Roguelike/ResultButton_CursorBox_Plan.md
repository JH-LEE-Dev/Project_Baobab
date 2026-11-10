# 결과창 버튼 호버 → CursorBox 통일 계획표

## 목표
로그라이크 결과창(다시 도전 / 메인메뉴) 버튼의 자체 호버 표현(금색 테두리 + 어두운 덮개 + 1px 띄움)을 없애고, 카드 선택 화면과 같은 공용 CursorBox로 통일한다.

## 변경 (변수명 변경 금지, 컨벤션: Yoda 표기, 매개변수 `_camelCase`, 접근 제어자 명시)
1. `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UI_RoguelikeTextButton.cs`
   - `focusFrame`, `idleShade`, `visualRoot` 필드와 관련 로직(띄움/덮개/테두리), `IdleTextColor` 제거.
   - `SetFocused`는 `bFocused` 상태만 보관하고, 글자색은 항상 `FocusedTextColor`(진한 색)로 고정(Initialize 시점/프리팹 기본색이 아니라 SetFocused·SetText 등에서 일관되게 보장되도록 처리 필요 시 Awake 없이 label 색을 한 번 설정).
   - 클래스 상단 요약 주석을 새 동작에 맞게 수정.
2. `.../UIView_RoguelikeResult.cs`
   - `SetFocus`에서 포커스 버튼이 있으면 `viewCtx?.cursorBoxUI`로 `Show(button.Rect, button.Rect.rect.size + new Vector2(CursorPadding, CursorPadding))` (`CursorPadding = 4`, 카드 선택 화면 `UIView_RoguelikeCardSelect.ShowCursor` 패턴 참고), null이면 `Hide()`.
   - `OnHideContent`, `Request`(요청 후)에서도 `Hide()`/`HideImmediately()` 처리.
3. `Assets/Scripts/Editor/Roguelike/RoguelikeLobbyUIBuilder.cs`
   - FocusFrame / IdleShade / visualRoot 생성·직렬화 연결(`Set(so, ...)`) 및 `IdleShadeColor`, 미사용 색 상수 제거.
   - 결과 프리팹 재생성(`UIView_RoguelikeResult.prefab`). 메뉴: LumberBoy/UI/Build Roguelike Result. Unity MCP로 실행 가능하면 실행, 불가하면 사용자에게 안내.
4. 참조처 전수 확인
   - `UI_RoguelikeTextButton`을 쓰는 다른 스크립트/프리팹 검색 후 영향도 확인. 잔재 코드(미사용 using, 상수) 정리.

## 검수
- 컴파일 에러/워닝(CS0414 등) 없음, 네임스페이스 누락 없음.
- 두 버튼 호버 시 CursorBox만 표시되고 덮개/띄움이 없음.
