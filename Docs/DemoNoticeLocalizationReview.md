# STOVE 데모 완료 안내 팝업 현지화 검수 및 크로스체크 가이드

본 문서는 STOVE 데모 빌드 출시를 위해 작성된 데모 완료 안내 팝업([DemoNoticeUI.json](file:///d:/Unity/Project/LumberBoy/Assets/Resources/Localization/DemoNoticeUI.json) 엔트리 3번 `DescriptionStove`)의 다국어 텍스트 검수 결과와, 다른 프로그래머 및 빌드 담당자가 배포 전 반드시 확인해야 하는 크로스체크 항목을 기술합니다.

---

## 1. 검수 배경 및 아키텍처 연계

### ① 스토어 분기 배경
* **Steam 버전 (엔트리 2)**: 본편 상점 페이지 이동 버튼 및 "Steam 찜하기" 문구 노출.
* **STOVE 데모 버전 (엔트리 3)**:
  * STOVE 데모는 본편 상점으로 보내지 않기로 결정되어, 상점 버튼 자체가 비활성화됩니다 ([HUD_PopupNav_DemoNotice.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Presentation%20Layer/UISystem/UIView/MenuPopup/VehicleMap/HUD_PopupNav_DemoNotice.cs#L81)).
  * 버튼이 없으므로 본문 텍스트에서도 `Steam` 및 `찜하기(Wishlist)` 관련 문구가 완전히 제거되어야 합니다.
  * 빌드 사전 검사기인 [PlatformConsistencyGuard.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Editor/Build/PlatformConsistencyGuard.cs#L270-L280)가 STOVE 데모 빌드 시 `DemoNoticeUI.json` 엔트리 3에 금지어(`steam`, `wishlist`, `찜하기`, `愿望单`, `願望單`, `ウィッシュ`)가 포함되어 있으면 빌드를 강제로 중단시킵니다.

### ② 연관 파일
* 번역 데이터: [Assets/Resources/Localization/DemoNoticeUI.json](file:///d:/Unity/Project/LumberBoy/Assets/Resources/Localization/DemoNoticeUI.json)
* 팝업 UI 컨트롤러: [Assets/Scripts/Presentation Layer/UISystem/UIView/MenuPopup/VehicleMap/HUD_PopupNav_DemoNotice.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Presentation%20Layer/UISystem/UIView/MenuPopup/VehicleMap/HUD_PopupNav_DemoNotice.cs)
* 빌드 검사기: [Assets/Scripts/Editor/Build/PlatformConsistencyGuard.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Editor/Build/PlatformConsistencyGuard.cs)
* 폰트 문자셋 생성기: [Assets/Scripts/Editor/Localization/LocalizationFontCharacterSetGenerator.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Editor/Localization/LocalizationFontCharacterSetGenerator.cs)
* 영문 현지화 가이드: [Assets/Documentation/Localization/EnglishLocalizationGlossary.md](file:///d:/Unity/Project/LumberBoy/Assets/Documentation/Localization/EnglishLocalizationGlossary.md)

---

## 2. 발견된 문제점 및 위험 요소 분석

### 🔴 [이슈 1 - Critical] 폰트 아틀라스 미베이킹으로 인한 문자 깨짐(Tofu / □) 위험

#### 원인
* 본 프로젝트는 UI 픽셀 폰트 렌더링 최적화를 위해 [LocalizationFontCharacterSetGenerator.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Editor/Localization/LocalizationFontCharacterSetGenerator.cs)를 통해 [Assets/Resources/Localization](file:///d:/Unity/Project/LumberBoy/Assets/Resources/Localization)의 모든 JSON 파일을 스캔하여 [Assets/TextMesh Pro/Font Character Sets](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Font%20Character%20Sets)에 필요한 문자셋을 추출한 뒤 폰트 아틀라스를 베이킹합니다.
* 폰트 에셋([FusionPixel_zh_hans.asset](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Fonts/FusionPixel_zh_hans.asset), [FusionPixel_JA.asset](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Fonts/FusionPixel_JA.asset) 등)은 **Static Atlas Mode**(`m_AtlasPopulationMode: 0`)로 구성되어 있어, 런타임에 동적으로 글리프를 추가하지 못합니다.
* 신규 추가된 STOVE 문구에 프로젝트 기존 문자셋 파일에 없던 한자들이 포함되었으나, 아직 **Unity 에디터에서 폰트 재베이킹 메뉴를 실행하지 않은 상태**입니다.

#### 누락된 글리프 목록
* **일본어 ([FusionPixel_JA_Characters.txt](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Font%20Character%20Sets/FusionPixel_JA_Characters.txt))**:
  * `届` (`お届けします`의 `届` - 1자)
* **중국어 간체 ([FusionPixel_ZH_HANS_Characters.txt](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Font%20Character%20Sets/FusionPixel_ZH_HANS_Characters.txt))**:
  * `留`, `着`, `再`, `相`, `见`, `场` (총 6자)
* **중국어 번체 ([FusionPixel_ZH_HANT_Characters.txt](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Font%20Character%20Sets/FusionPixel_ZH_HANT_Characters.txt))**:
  * `留`, `著`, `再`, `相`, `見`, `場` (총 6자)

#### 인게임 영향
* 폰트 아틀라스를 재베이킹하지 않고 STOVE 빌드를 출력하면, 일본어 및 중국어로 게임을 플레이할 때 해당 문자들이 **네모 박스(□, Tofu)**로 깨져 출력됩니다.

---

### 🟡 [이슈 2 - UI/UX] 중국어 간체/번체 컬러 태그 하이라이트 범위 어긋남

#### 원인
* 한국어, 영어, 일본어 및 기존 스팀 버전 중국어는 "Discord 피드백" 어구를 통째로 디스코드 브랜드 블루 컬러(`<COLOR=7289DA>`)로 감쌌습니다:
  * 한국어: `<COLOR=7289DA>디스코드 피드백</COLOR>`
  * 영어: `<COLOR=7289DA>Discord feedback</COLOR>`
  * 일본어: `<COLOR=7289DA>Discordでのフィードバック</COLOR>`
  * 기존 중국어: `<COLOR=7289DA>Discord反馈</COLOR>`
* 그러나 현재 STOVE 중국어는 아래와 같이 작성되어 있습니다:
  * `你们在<COLOR=7289DA>Discord</COLOR>留下的反馈`
* 이로 인해 실제 UI 출력 시 **`Discord` 영문 단어만 파란색이고 `留下的反馈`는 기본 흰색**으로 분리되어 타 언어 및 기존 UI와 시각적 통일성이 깨집니다.

---

### 🟡 [이슈 3 - 텍스트 톤/매너] 중국어 인칭대명사 호칭 불일치 (복수 $\leftrightarrow$ 단수)

#### 원인
* 1행: `各位探险家` (탐험가 여러분, 복수 존칭)
* 2행: `你们` (여러분, 복수)
* 3행: `再次与你相见` (다시 **너**와 만나다, 단수 2인칭 `你`)
* 동일 팝업 내에서 플레이어 전체를 지칭하는 호칭이 복수 존칭에서 갑자기 단수 2인칭으로 전환되어 문맥의 일관성과 격식이 떨어집니다. `再次与大家相见`("여러분과 다시 만나 뵙겠습니다")로 통일하는 것이 권장됩니다.

---

## 3. 프로그래머 크로스체크 체크리스트 (Checklist)

STOVE 데모 빌드를 생성하기 전 담당 프로그래머는 아래 항목을 반드시 교차 점검하십시오:

- [ ] **1. 폰트 아틀라스 재베이킹 실행**:
  * Unity 에디터 상단 메뉴 `Tools > Localization > Generate Character Sets and Bake Atlases` 실행.
  * [Assets/TextMesh Pro/Font Character Sets](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Font%20Character%20Sets) 텍스트 파일들에 신규 문자가 반영되었는지 확인.
  * [Assets/TextMesh Pro/Fonts](file:///d:/Unity/Project/LumberBoy/Assets/TextMesh%20Pro/Fonts)의 `.asset` 폰트 아틀라스 파일들이 갱신(Dirty 플러시)되었는지 확인.
- [ ] **2. 중국어 문구 태그 및 호칭 수정 반영**:
  * [DemoNoticeUI.json](file:///d:/Unity/Project/LumberBoy/Assets/Resources/Localization/DemoNoticeUI.json)의 엔트리 3번 `zhHans`, `zhHant` 텍스트가 아래 권장 수정안으로 교체되었는지 확인.
- [ ] **3. PlatformConsistencyGuard 검사 통과 여부 확인**:
  * Unity 메뉴 `Tools > 빌드`에서 스토어를 `STOVE`, 배포를 `데모`로 선택 후 재컴파일 대기.
  * [PlatformConsistencyGuard.cs](file:///d:/Unity/Project/LumberBoy/Assets/Scripts/Editor/Build/PlatformConsistencyGuard.cs)에서 에러나 경고 없이 빌드 전처리 통과하는지 확인.
- [ ] **4. 인게임 5개 언어 실기 렌더링 확인**:
  * 타이틀 씬 또는 데모 완료 팝업 트리거 시 5개 언어(KR, EN, ZH_HANS, ZH_HANT, JA)로 각각 전환하여 확인:
    1. 깨진 글자(□)가 없는지.
    2. 버튼 텍스트와 본문 줄바꿈(`\n`)이 띠 배너 영역을 벗어나거나 어색하게 끊기지 않는지.
    3. STOVE 빌드에서 상점(스팀) 버튼이 완전히 숨겨지고 디스코드 버튼 1개만 중앙 정렬되어 패드 포커스가 잡히는지.

---

## 4. 권장 수정 텍스트 대조표

| 언어 | 현재 [DemoNoticeUI.json](file:///d:/Unity/Project/LumberBoy/Assets/Resources/Localization/DemoNoticeUI.json) (엔트리 3) | 권장 수정안 (Recommended) | 비고 |
|---|---|---|---|
| **`kr`** | `럼버보이의 첫 발걸음을 함께해 주신 <COLOR=7EEDBE>탐험가 여러분</COLOR>께 진심으로 감사드립니다.\n여러분이 남겨주시는 <COLOR=7289DA>디스코드 피드백</COLOR>은 개발팀에게 가장 큰 힘이 됩니다.\n<COLOR=54D86A>정식 버전</COLOR>에서 이어질 모험으로 다시 찾아뵙겠습니다!` | *(현재 텍스트 유지)* | 이상 없음 |
| **`en`** | `Thank you, <COLOR=7EEDBE>explorers</COLOR>, for joining Lumber Boy on his very first adventure.\nYour <COLOR=7289DA>Discord feedback</COLOR> means the world to our dev team.\nWe'll be back with the <COLOR=54D86A>full release</COLOR> to continue the adventure!` | *(현재 텍스트 유지)* | [용어집](file:///d:/Unity/Project/LumberBoy/Assets/Documentation/Localization/EnglishLocalizationGlossary.md) 규칙 완벽 준수 |
| **`zhHans`** | `衷心感谢各位<COLOR=7EEDBE>探险家</COLOR>，陪伴伐木少年迈出第一步。\n你们在<COLOR=7289DA>Discord</COLOR>留下的反馈是开发团队最大的动力。\n我们将带着<COLOR=54D86A>正式版</COLOR>再次与你相见，继续这场冒险！` | `衷心感谢各位<COLOR=7EEDBE>探险家</COLOR>，陪伴伐木少年迈出第一步。\n你们留下的<COLOR=7289DA>Discord反馈</COLOR>是开发团队最大的动力。\n我们将带着<COLOR=54D86A>正式版</COLOR>再次与大家相见，继续这场冒险！` | 태그 하이라이트 범위 통일 및 `大家` 호칭 수정 |
| **`zhHant`** | `衷心感謝各位<COLOR=7EEDBE>探險家</COLOR>，陪伴伐木少年邁出第一步。\n你們在<COLOR=7289DA>Discord</COLOR>留下的反饋是開發團隊最大的動力。\n我們將帶著<COLOR=54D86A>正式版</COLOR>再次與你相見，繼續這場冒險！` | `衷心感謝各位<COLOR=7EEDBE>探險家</COLOR>，陪伴伐木少年邁出第一步。\n你們留下的<COLOR=7289DA>Discord反饋</COLOR>是開發團隊最大的動力。\n我們將帶著<COLOR=54D86A>正式版</COLOR>再次與大家相見，繼續這場冒險！` | 간체와 동일하게 태그 및 호칭 수정 |
| **`ja`** | `<COLOR=7EEDBE>冒険者の皆さん</COLOR>、ランバーボーイの初めての冒険に\nお付き合いいただき、本当にありがとうございます。\n<COLOR=7289DA>Discordでのフィードバック</COLOR>が、\n開発チームの大きな励みになります。\n<COLOR=54D86A>製品版</COLOR>で、冒険の続きをお届けします！` | *(현재 텍스트 유지, 폰트 베이킹 필수)* | 문구 자연스러움. `届` 글리프 베이킹 필수 |
