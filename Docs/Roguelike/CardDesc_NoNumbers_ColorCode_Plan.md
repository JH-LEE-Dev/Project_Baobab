# 카드 설명 수치 은폐 + 버프/디버프 색 구분 계획표

## 목표
- 카드 설명에서 `+100%`, `30%`, `9개` 같은 수치 표현을 모두 숨긴다.
- 어떤 효과가 강화(버프)이고 어떤 효과가 약화(디버프)인지 **글자 색**으로 구분한다.
- 선택 화면·보유 카드 화면 모두, 전 카드에 동일 규칙을 적용한다.

## 현황 (탐색 결과)
- 문구 원본: `Assets/Resources/Localization/RoguelikeUI.json` (`Roguelike_Card_*_Desc` 27개, kr/en만 채워져 있고 나머지 16개 언어는 en 폴백).
- 선택 화면: `UI_RoguelikeCardDescPanel.BuildDescription` 이 JSON 원문만 표시 (수치 줄 없음). 수치는 원문에 박힌 것이 전부.
  - 수치가 든 원문: 다다익선(`+100%`, `30%`), 별·드론 조합(`9개`) — 총 3곳.
- 보유 화면: `UI_RoguelikeCardDescPanel.BuildOwnedDescription` → `RoguelikeCardEffectText.BuildCurrent` 가 `피해 +100%`, `이동속도 -30%`, `A → B` 형태의 수치 줄을 자동 생성.
- 색 체계는 이미 존재: 버프 `#F2D27A`(35행), 디메리트 `#E86A6A`(36행). 계약 카드만 `IsBeneficial` 로 분기.
- 카드 데이터에 버프/디버프 필드는 없음 → 새 필드 추가 없이 처리.

## 변경 방안

### A. 선택 화면 (JSON 원문)
1. 원문에서 수치 제거 및 문구 재작성 (kr/en).
   - 다다익선: `발동 수와 공격력 증가. 대신 스폰 속도 증가, 이동속도 감소.`
   - 별·드론 조합: `9개` 제거.
2. 원문 안 효과 구절에 색 마커를 둔다. 언어 무관하게 폴백되도록 마커는 번역과 분리된 고정 토큰 사용.
   - 강화: `{+}발동 수와 공격력 증가{/}` → `<color=#F2D27A>…</color>`
   - 약화: `{-}스폰 속도 증가, 이동속도 감소{/}` → `<color=#E86A6A>…</color>`
   - 마커 → 리치텍스트 변환은 `UI_RoguelikeCardDescPanel` 의 `ApplyTexts`/`LocalizeCard` 경로에서 1회 치환 (GC 최소화를 위해 StringBuilder 사용).
   - 마커가 없는 카드(전부 버프)는 구절 단위로 `{+}` 를 감싸거나, 마커 없는 문장은 기본 글자색 유지 (아래 질문 참조).
3. 전 카드 27개 `_Desc` 를 순회하며 같은 규칙으로 강화/약화 구절에 마커 부여. `passive_count` 의 누락 키도 이 시점에 보고(수정 여부는 별도 확인).

### B. 보유 카드 화면 (코드 생성 효과 줄)
1. `RoguelikeCardEffectText.AppendValue` / `BuildCurrent` 에서 수치·단위·`A → B` 를 출력하지 않고 **효과 카테고리 이름만** 출력 (`피해`, `이동속도`, `나무 스폰 속도` …).
2. 색은 `IsBeneficial` 결과로 결정: 이로우면 버프색, 해로우면 디메리트색. 비계약 카드도 부호 기준으로 동일 판정.
3. 증가/감소 방향은 수치 대신 화살표 없이 문구로만 전달할지 결정 필요 (아래 질문).

### C. 정리
- 수치 출력에만 쓰이던 `GetUnit` 및 단위 키(`Roguelike_EffectUnit_*`) 참조, 미사용 잔재 변수 점검·제거 (CS0414/CS0168 방지).
- 변수명은 변경하지 않는다. 신규 필드는 camelCase, 매개변수는 `_camelCase`, Yoda 표기법, LINQ·람다 미사용.
- 참조처 전수 확인: `RoguelikeCardEffectText` 호출부(보유 카드 화면, 선택 화면 폭 측정 `UIView_RoguelikeCardSelect` 356~368행), 18개 언어 기준 줄바꿈·폭 재검수(마커 제거 후 길이 기준).

## 영향 파일
- `Assets/Resources/Localization/RoguelikeUI.json` (원문 수정)
- `UI_RoguelikeCardDescPanel.cs` (마커 변환)
- `RoguelikeCardEffectText.cs` (수치 제거·색 결정)

## 검증
- Unity 컴파일 오류 없음, 콘솔 경고 0.
- 선택 화면에서 다다익선 → 수치 없이 강화 구절은 금색, 약화 구절은 적색.
- 보유 카드 화면에서 수치·화살표 없이 색으로만 구분.
- 영어 폴백 및 가장 긴 언어에서 줄 넘침 없음.
