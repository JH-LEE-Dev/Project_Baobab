# 보유 카드 창 호버 효과음 — 계획표

## 목표
보유 카드 창(`UIView_RoguelikeOwnedCards`)에서 카드에 포커스가 옮겨 갈 때(마우스 호버 · 패드 이동) 효과음이 없다.
카드 선택 화면(`UIView_RoguelikeCardSelect`)의 호버음 `SoundID.ResultUIHover`로 통일한다.

## 기준 동작 (카드 선택 화면, 바꾸지 않음)
`Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeCardSelect.cs` 446~451행 `SetFocus`
- 사용자 입력으로 포커스가 **다른 대상으로 바뀔 때만** 재생한다.
- 열릴 때의 첫 포커스(`_bForce`)와 포커스 해제에는 재생하지 않는다.

## 수정 대상 (1개 파일, 1곳)
`Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UIView_RoguelikeOwnedCards.cs` — `OnRelayFocused` (965행 부근)

사용자 입력(마우스 호버 · 패드 이동)이 들어오는 유일한 경로다. `SetFocusCell`은 `FocusFirst` · `ApplyDevice` · `FinishOpen`(시스템이 거는 포커스)에서도 불리므로 거기에는 넣지 않는다.

### 전
```csharp
    private void OnRelayFocused(UI_RoguelikeSelectRelay _relay)
    {
        if (EMotion.None != motion) return;

        UI_RoguelikeOwnedCardCell cell = FindCell(_relay);
        if (null == cell) return;

        SetFocusCell(cell, false == _relay.IsPointerFocus);
```

### 후
```csharp
    private void OnRelayFocused(UI_RoguelikeSelectRelay _relay)
    {
        if (EMotion.None != motion) return;

        UI_RoguelikeOwnedCardCell cell = FindCell(_relay);
        if (null == cell) return;

        // 카드 선택 화면과 같은 호버음. 포커스가 실제로 바뀔 때만 낸다(같은 칸 재진입 · 열릴 때의 첫 포커스는 무음).
        if (cell != focusedCell || false == bFocusShown) Sound.PlayUI(SoundID.ResultUIHover);

        SetFocusCell(cell, false == _relay.IsPointerFocus);
```

### 조건 설명
- `cell != focusedCell`: 다른 칸으로 옮겨 감 → 재생.
- `false == bFocusShown`: 키보드/마우스 모드로 열리면 `FocusFirst`가 첫 칸을 `focusedCell`로만 잡고 강조는 하지 않는다(`bFocusShown = false`). 이 상태에서 첫 칸을 호버하면 카드가 처음으로 올라오므로 재생한다.
- 같은 칸에서 `SetFocusCell` 조기 반환 조건(`_cell == focusedCell && true == bFocusShown`)과 정확히 반대라서, 화면 변화가 있을 때만 소리가 난다.
- 열기 · 닫기 연출 중(`EMotion.None != motion`)에는 위에서 이미 반환하므로 무음.

## 바꾸지 않는 것
- 카드 선택 화면, 열기/닫기 효과음(`HUDBackpackOpen` / `HUDBackpackClose`), `SetFocusCell` · `FocusFirst` · `ApplyDevice` · `FinishOpen`.
- 변수명, 네임스페이스(`Sound` · `SoundID`는 이미 같은 파일 704 · 719행에서 사용 중 — using 추가 없음).

## 검수
1. 컴파일 에러 · 경고 없음(Unity 콘솔).
2. 런 중 Tab으로 창 열기 → 열리는 순간 호버음 없음.
3. 마우스로 카드 사이를 옮겨 다니면 칸이 바뀔 때마다 1회, 같은 칸 안에서 움직이면 무음.
4. 패드 모드: 열린 직후 무음, 스틱 · 십자키로 이동할 때마다 1회.
5. 카드 선택 화면 호버음과 같은 소리인지 귀로 비교.
