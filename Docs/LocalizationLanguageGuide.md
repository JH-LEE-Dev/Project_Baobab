# 다국어(로컬라이징) 작업 가이드

지원 언어에 **독일어 · 프랑스어 · 포르투갈어 · 스페인어 · 러시아어**를 추가했고,
이어서 **폴란드어 · 튀르키예어 · 중남미 스페인어 · 이탈리아어**,
**우크라이나어 · 체코어 · 인도네시아어 · 베트남어 · 태국어**를 추가했습니다.
코드·설정·폰트 쪽 연결은 모두 끝나 있으며, **남은 일은 JSON에 번역문을 채우는 것과
첫 실행 팝업에 언어 버튼 13개를 더 만드는 것**입니다.

> ⚠️ **태국어만 아직 잠겨 있습니다.** 프로젝트에 태국 문자를 가진 폰트가 없어서, 옵션·첫 실행
> 팝업에서 고를 수 없게 막아 두었습니다. 번역은 미리 채워도 됩니다. 여는 방법은 5장을 보세요.

---

## 1. 지원 언어 목록

옵션 화면에서 좌우 키로 넘길 때의 순서와 같습니다.

| 순서 | 옵션 항목 (`EOptionLanguage`) | JSON 열 | 화면 표기 | Steam 언어 코드 |
|---|---|---|---|---|
| 1 | `Korean` | `kr` | 한국어 | `koreana` |
| 2 | `English` | `en` | English | `english` |
| 3 | `ChineseSimplified` | `zhHans` | 简体中文 | `schinese` |
| 4 | `ChineseTraditional` | `zhHant` | 繁體中文 | `tchinese` |
| 5 | `Japanese` | `ja` | 日本語 | `japanese` |
| 6 | `German` | `de` | Deutsch | `german` |
| 7 | `French` | `fr` | Français | `french` |
| 8 | `Portuguese` | `pt` | Português | `portuguese`, `brazilian` |
| 9 | `Spanish` | `es` | Español (España) | `spanish` |
| 10 | `Russian` | `ru` | Русский | `russian` |
| 11 | `Polish` | `pl` | Polski | `polish` |
| 12 | `Turkish` | `tr` | Türkçe | `turkish` |
| 13 | `SpanishLatAm` | `esLatam` | Español (Latinoamérica) | `latam` |
| 14 | `Italian` | `it` | Italiano | `italian` |
| 15 | `Ukrainian` | `uk` | Українська | `ukrainian` |
| 16 | `Czech` | `cs` | Čeština | `czech` |
| 17 | `Indonesian` | **`ind`** | Bahasa Indonesia | `indonesian` |
| 18 | `Vietnamese` | `vi` | Tiếng Việt | `vietnamese` |
| (잠김) | `Thai` | `th` | ไทย | `thai` |

> 인도네시아어 열 이름은 `id`가 아니라 **`ind`** 입니다. `id`는 이미 항목 번호로 쓰고 있어서
> 같은 이름을 쓰면 번호가 덮어써집니다.

포르투갈어는 브라질 변종을 따로 두지 않고 하나로 모았습니다.
스페인어는 **스페인(`es`)과 중남미(`esLatam`)를 나눴습니다.** Steam 언어가 `latam`이면
중남미 스페인어로 시작합니다. (OS 언어는 둘을 구분하지 못하므로 OS 언어로만 판별되면 스페인 쪽으로 시작합니다)

중남미 스페인어는 기존 스페인어 번역을 다듬어 채우는 방식이라, **`esLatam`이 비어 있으면
영어가 아니라 `es`를 먼저 보여줍니다.** (`es`도 비어 있으면 영어)
즉 중남미식으로 바꿀 필요가 있는 항목만 골라 채우면 됩니다.

> 옵션 화면 순서에서 중남미 스페인어가 스페인어 바로 옆이 아니라 뒤쪽에 있는 이유:
> 옵션 값은 설정 파일에 번호로 저장되므로, 중간에 끼워 넣으면 기존 유저의 언어 설정이
> 다른 언어로 바뀌어 버립니다. 새 언어는 항상 맨 뒤에 붙입니다.

---

## 2. JSON에 무엇을 추가하면 되는가

`Assets/Resources/Localization/*.json`의 **모든 항목에 `de` / `fr` / `pt` / `es` / `ru` /
`pl` / `tr` / `esLatam` / `it` / `uk` / `cs` / `ind` / `vi` / `th` 열네 개 열을 이미 넣어두었습니다.** (번역 전 열은 빈 문자열) 새로 키를 만들 필요 없이,
따옴표 사이에 번역문만 채우면 됩니다.

```json
{
  "id": 101,
  "key": "Windowed",
  "kr": "창모드",
  "en": "Windowed",
  "zhHans": "窗口化",
  "zhHant": "窗口化",
  "ja": "ウィンドウ",
  "de": "Fenstermodus",
  "fr": "Fenêtré",
  "pt": "Janela",
  "es": "Ventana",
  "ru": "Оконный",
  "pl": "W oknie",
  "tr": "Pencereli",
  "esLatam": "",
  "it": "In finestra",
  "uk": "Віконний",
  "cs": "V okně",
  "ind": "Jendela",
  "vi": "Cửa sổ",
  "th": "หน้าต่าง",
  "enumType": "",
  "enumValue": ""
}
```

### 지켜야 할 것

- **열 이름은 정확히 `de`, `fr`, `pt`, `es`, `ru`, `pl`, `tr`, `esLatam`, `it`, `uk`, `cs`, `ind`, `vi`, `th`** 입니다.
  (`esLatam`은 대소문자까지 정확히 맞춰야 합니다) 오타가 나면 그 열은 통째로
  무시되고 조용히 영어로 나옵니다. (오류도 경고도 뜨지 않습니다)
- **빈칸으로 둬도 됩니다.** 비어 있는 항목은 영어(`en`)로 폴백합니다.
  (`esLatam`만 예외로 `es` → `en` 순서로 폴백합니다)
  그러니 한 번에 다 채우지 않고 파일 단위로 나눠 작업해도 게임은 정상 동작합니다.
- **`{0}`, `{1}` 같은 중괄호는 그대로 두세요.** 게임이 숫자·이름을 끼워 넣는 자리입니다.
  순서를 바꿔야 하는 언어라면 `{0}`/`{1}` 번호는 유지한 채 위치만 옮기면 됩니다.
- **`<COLOR=...>`, `<Wave>` 같은 태그도 그대로 두세요.** 태그 안의 값은 번역 대상이 아닙니다.
- `id`, `key`, `enumType`, `enumValue`는 건드리지 마세요. 코드가 이 값으로 문자열을 찾습니다.
- 우크라이나어의 아포스트로피는 **`’`(U+2019) 또는 `'`** 를 쓰세요. `ʼ`(U+02BC)는 폰트에 없어 네모로 나옵니다.
- 태국어는 단어 사이에 띄어쓰기가 없어서, 게임이 줄을 어디서 바꿔야 할지 모릅니다.
  긴 문장은 줄이 바뀌어도 되는 자리에 **보이지 않는 공백(U+200B, zero-width space)** 을 넣어 주세요.
- 파일 인코딩은 **UTF-8**입니다. 메모장으로 저장하면 인코딩이 깨질 수 있으니
  VS Code 등으로 편집하세요.

### 새 문자열을 추가할 때

`id`는 파일 안에서만 고유하면 됩니다. 기존 번호 규칙(100번대 = enum 표기,
200번대 = 항목 이름, 300번대 = 그 외)을 따라 뒤에 붙이면 됩니다.
추가한 뒤 **`Tools/Localization/Generate Keys`** 를 실행해야 `LocKeys`에 상수가 생깁니다.

---

## 3. 작업 후 반드시 실행할 것

Unity 상단 메뉴에서 **두 가지를 모두** 실행해야 합니다.

### (1) `Tools/Localization/Generate Keys`

- `LocKeys.cs` 갱신 (새로 만든 `key`에 대한 C# 상수)
- `LocalizationMapping.asset` 갱신
- CJK 폰트 **문자셋 목록(.txt)** 재생성

### (2) `Tools/Localization/Generate Character Sets and Bake Atlases`

일본어·중국어 폰트는 **필요한 글자만 미리 구워 넣은 정적 아틀라스**입니다.
(1)은 "어떤 글자가 필요한지" 목록만 다시 쓸 뿐, **아틀라스를 굽지 않습니다.**
그래서 JSON에 새 글자가 **한 자라도** 생겼다면 (2)까지 돌려야 하고,
안 그러면 **그 글자가 네모(두부)로 나옵니다.**

라틴·키릴 12개 언어가 쓰는 `Lorem_Optimum`도 **정적 아틀라스**이고 원본 TTF 참조가 비어 있어
런타임에 글리프를 채우지 못합니다. 즉 **번역문을 채울 때마다 (2)를 돌려야 합니다.**

> ⚠️ **지금 (2)를 한 번도 돌리지 않은 상태입니다.**
> `Lorem_Optimum` 아틀라스에는 ASCII 104자밖에 없어서,
> **이대로 러시아어를 선택하면 화면 전체가 네모로 나옵니다.**
> 우크라이나어도 마찬가지이고, 프랑스어·포르투갈어·스페인어·폴란드어·튀르키예어·이탈리아어·체코어도
> 악센트 글자가 모두 깨집니다.
> (원본 `Lorem.ttf`에는 라틴 확장·키릴이 전부 들어 있으니, 굽기만 하면 해결됩니다)

### 언어별로 어떤 폰트를 쓰는가

| 언어 | 폰트 | 아틀라스 |
|---|---|---|
| 한국어 · English | `Galmuri11_Optimum` (프리팹 원본 유지) | 동적 |
| 日本語 | `FusionPixel_JA` | 정적 → 굽기 필요 |
| 简体中文 | `FusionPixel_zh_hans` | 정적 → 굽기 필요 |
| 繁體中文 | `FusionPixel_zh_hant` | 정적 → 굽기 필요 |
| **독·불·포·서·러·폴·튀·중남미 서·이·우·체·인니** (12종) | **`Lorem_Optimum`** | **정적 → 굽기 필요** |
| Tiếng Việt | `Galmuri11_Optimum` (프리팹 원본 유지) | 동적 |
| ไทย | 없음 (잠김) | — |

베트남어는 `Lorem`에 성조 글자(ạ ế ợ …)가 없어서 한국어·영어처럼 **갈무리11을 그대로** 씁니다.
갈무리11은 베트남어 글자를 전부 갖고 있고 동적 아틀라스라, 베트남어는 **굽기를 안 해도 됩니다.**

`Lorem`에는 한글·CJK 글리프가 없습니다. 첫 실행 팝업은 언어 이름을 한 화면에 모두
띄우는데 그때 화면 전체가 `Lorem`으로 교체되므로, 그 상태에서
`한국어 / 日本語 / 简体中文 / 繁體中文` 네 라벨(12자)만 글리프를 못 찾습니다.
그래서 `Lorem_Optimum`의 폴백에 `FusionPixel_zh_hans`를 걸어 두었습니다.
**이 폴백을 지우면 그 네 라벨이 깨집니다.**

같은 이유로 `Tiếng Việt`의 `ế`는 `Lorem`·일본어·중국어 폰트 어디에도 없어서,
`Lorem_Optimum`과 `FusionPixel` 세 폰트의 폴백 끝에 `Galmuri11_Optimum`을 걸어 두었습니다.
굽기 후 콘솔에 `ế`(그리고 태국 문자)가 "미지원 문자"로 뜨는 것은 이 폴백이 처리하므로 정상입니다.

---

## 4. 첫 실행 언어 선택 팝업 (UI 작업 필요)

`Assets/Prefabs/UI/MainMenu/UIView_MainMenu.prefab` 안
`LanguagePanel/LanguageButtons` 아래에 지금 버튼이 5개 있습니다.
여기에 **13개를 더 만들어 주세요.** (태국어 버튼은 5장의 절차 때 만듭니다)

### 하는 방법

1. 기존 `BTN_Korean`을 복제합니다.
2. 오브젝트 이름에 아래 조각 중 하나가 **들어가게** 지읍니다. (접두사·접미사는 자유)

   | 언어 | 이름에 들어가야 할 조각 | 예시 |
   |---|---|---|
   | 독일어 | `German` 또는 `Deutsch` | `BTN_German` |
   | 프랑스어 | `French` 또는 `Francais` | `BTN_French` |
   | 포르투갈어 | `Portug` | `BTN_Portuguese` |
   | 스페인어 | `Spanish` 또는 `Espanol` | `BTN_Spanish` |
   | 러시아어 | `Russia` | `BTN_Russian` |
   | 폴란드어 | `Polish` 또는 `Polski` | `BTN_Polish` |
   | 튀르키예어 | `Turk` | `BTN_Turkish` |
   | 중남미 스페인어 | `LatAm` 또는 `Latam` | `BTN_SpanishLatAm` |
   | 이탈리아어 | `Italian` | `BTN_Italian` |
   | 우크라이나어 | `Ukrain` | `BTN_Ukrainian` |
   | 체코어 | `Czech` 또는 `Cestina` | `BTN_Czech` |
   | 인도네시아어 | `Indones` | `BTN_Indonesian` |
   | 베트남어 | `Vietnam` | `BTN_Vietnamese` |

   > 중남미 스페인어 버튼 이름에 `Spanish`가 들어가도 괜찮습니다. `LatAm`을 먼저 검사합니다.
   > 반대로 `LatAm`/`Latam`이 빠지면 **스페인 스페인어 버튼으로 잡히니** 주의하세요.

3. `UI_InitialSetupPopup`의 `Language Buttons` 배열에 추가합니다. (순서는 상관없습니다)

### 코드가 알아서 해주는 것

- **버튼에 표시될 언어 이름** — `OptionUI.json`에서 읽어 넣습니다. 직접 타이핑하지 마세요.
- **게임패드 상하좌우 이동 배선** — 버튼이 화면에 놓인 위치를 읽어 격자로 자동 계산합니다.
  `GridLayoutGroup`이 3열로 흘려주므로 18개면 3줄 × 6으로 배치되고, 그대로 이동합니다.
  좌우는 전체를 한 바퀴 돌고, 상하는 위아래 줄의 같은 칸으로 갑니다.
- 배치를 몇 열로 바꾸든 코드는 손대지 않아도 됩니다.

이름 조각이 하나도 걸리지 않으면 그 버튼은 한국어로 떨어지고 **콘솔에 경고가 뜹니다.**
버튼을 만들었는데 한국어 버튼이 두 개로 보인다면 이름을 확인하세요.

> `LanguageButtons` 오브젝트의 가로 크기(현재 220)는 3열 기준입니다.
> 줄 수가 늘어나는 만큼 패널 세로 크기와 창 크기도 함께 조정해 주세요.
> `Español (Latinoamérica)`, `Bahasa Indonesia`는 다른 언어 이름보다 꽤 깁니다. 버튼·옵션 칸에서 잘리면
> `OptionUI.json`의 해당 `Language...` 항목 표기를 줄여도 됩니다.
> (모든 열에 같은 값을 넣어야 합니다. 어느 언어로 보든 자기 표기가 나와야 하기 때문입니다)

---

## 5. 태국어를 여는 방법

태국어는 번역 열·언어 매핑·자동 감지까지 모두 연결되어 있고, **폰트만 없어서 잠겨 있습니다.**
지금 태국어 Steam·OS 유저는 영어로 시작합니다.

1. 태국 문자를 가진 픽셀 폰트(12px 계열, 갈무리와 같은 비트맵 렌더 모드)를 구해 TMP 폰트 에셋으로 만듭니다.
   - 라이선스가 상업 게임 포함 배포를 허용하는지 꼭 확인하세요.
   - 태국어는 모음·성조 기호가 글자 위아래에 붙습니다. 12px에서 겹치지 않는지 실제 문장으로 확인하세요.
2. `LocalizationFontTable`의 `language: 18`(TH)에 그 폰트를 넣습니다.
3. `LocalizationFontCharacterSetGenerator`에 태국어 폰트 항목(문자셋 파일, 베이킹 경로 세 배열)을 추가합니다.
   (동적 아틀라스로 만들었다면 이 단계는 필요 없습니다)
4. `Lorem_Optimum`, `FusionPixel` 세 폰트, (필요하면) `Galmuri11_Optimum`의 폴백에 태국어 폰트를 겁니다.
   첫 실행 팝업에서 `ไทย` 라벨이 다른 언어 화면에서도 보여야 하기 때문입니다.
5. `UI_InitialSetupPopup.languageButtonBindings`에 태국어 줄(`"Thai"`)을 추가하고 버튼을 만듭니다.
6. `SettingsData.SUPPORTED_LANGUAGE_COUNT`를 **18 → 19**로 올립니다.

---

## 6. 언어를 더 늘릴 때

`Assets/Scripts/Application Layer/SettingsSystem/SettingsManager.cs` 상단에
손봐야 할 곳이 번호 순서로 정리되어 있습니다. 그 체크리스트를 따르세요.
