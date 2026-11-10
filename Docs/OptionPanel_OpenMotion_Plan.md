# 옵션 패널 등장 연출 추가 계획표 (implementation_plan)

## 1. 현황 (코드 근거)
- 옵션 패널은 [UI_Option.cs](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/Option/UI_Option.cs) 한 클래스가 담당한다. `Show`(369행)는 `gameObject.SetActive(true)` → `optionPanelRoot.SetActive(true)`(400~403행)만 수행하며 연출이 없다.
- 세 경로(메인메뉴 `UIView_MainMenu.OnOptionButtonClicked`, 인게임 ESC `UIView_ESC.OnOptionButtonClicked`, 로그라이크 메인 `UIView_MainMenu.OpenRoguelikeOption`)가 모두 `UI_Option.Show`로 합류한다. 따라서 `Show` 한 곳만 고치면 세 곳이 동시에 해결된다.
- 인스턴스는 `UIView_MainMenu.prefab`과 `ESCMenu.prefab`에 각각 1개씩. 인게임 ESC에서는 `Time.timeScale = 0` 이므로 연출은 `SetUpdate(true)` 필수.
- 프리팹 루트(`UI_Option`)에는 CanvasGroup이 없고, `DimBG`·`PopupWindowRoot`에만 있다.
- 기존 패널 연출 규격(`UIView_RoguelikeRecords.PlayEnter` 431행): Dim 페이드 0.18s + 패널 6px 아래에서 0.2s OutCubic 상승, 모두 `SetUpdate(true)`.

## 2. 변경안 (UI_Option.cs 한 파일, 프리팹 수정 없음)
1. **연출 상수 추가** (Records와 동일 값): `OpenFadeSeconds = 0.18f`, `OpenPanelSeconds = 0.2f`, `OpenPanelOffset = 6f`.
2. **필드 추가** (`//내부 의존성`/`//상태 변수` 구역): `rootCanvasGroup`(CanvasGroup), `panelRootRect`(RectTransform), `panelBasePosition`(Vector2), `openSequence`(Sequence).
3. **`Initialize`**: 루트 `CanvasGroup`을 `TryGetComponent`로 찾고 없으면 `AddComponent`(프리팹 무수정). `optionPanelRoot`의 RectTransform과 기준 `anchoredPosition` 캐싱.
4. **`PlayOpenProduction()` 신규 private 메서드**: `openSequence` Kill → `alpha=0`, 패널을 `기준위치 + (0, -6)`로 세팅 → `DOFade(1)` + `DOAnchorPos(기준위치)` OutCubic을 Sequence로 재생, `SetUpdate(true).SetLink(gameObject)`. 람다/클로저 없이 캐시된 Tween 호출만 사용.
5. **`Show`**: 마지막(`RefreshCurrentTabScrollbar()` 뒤, 레이아웃 갱신 이후)에 `PlayOpenProduction()` 호출. 입력 잠금은 두지 않는다(0.2초로 짧고 기존 포커스 로직 유지).
6. **`ForceHide`**: 맨 앞에서 `openSequence` Kill 후 `alpha=1`, 패널 위치를 기준 위치로 복원(연출 도중 닫혀도 다음 `Show`에서 상태 오염 없음). 이미 `Hide`로 이중 호출되는 경로는 그대로 둔다.
7. 신규 `using` 필요 없음(`DG.Tweening`, `UnityEngine.UI` 이미 선언됨).

## 3. 영향도·안전성
- 변수명 변경 없음, 기존 필드/이벤트 바인딩 삭제 없음. 변경은 추가 위주.
- `Show` 호출 참조처 3곳(UIView_MainMenu 545행·829행, UIView_ESC 376행) 시그니처 불변.
- 리바인딩 오버레이 연출(2465~2570행)과는 대상(`rebindOverlay`)이 달라 충돌 없음.
- Yoda 표기, 명시적 접근 제어자, 메서드 매개변수 `_camelCase`, GC 없는 구현 준수.

## 4. 결정 요청 사항
- **닫기 연출**은 이번 범위에 포함하지 않는다. `ForceHide`가 동기식으로 `onCloseAction`(ESC 메뉴 복귀 연출 등)을 즉시 호출하므로 퇴장 연출을 넣으면 콜백 지연·입력 재진입 처리가 필요해 위험이 크다. 요청이 "등장"이므로 제외하고, 필요하면 별도 계획으로 제안한다.

## 5. 검증
- 메인메뉴·로그라이크 메인메뉴·인게임 ESC 세 경로에서 열기/연타 열고 닫기/Dirty 상태 닫기 확인. 인게임은 `timeScale=0`에서 연출 재생 확인. (Unity 컴파일 에러 로그 확인 포함)
