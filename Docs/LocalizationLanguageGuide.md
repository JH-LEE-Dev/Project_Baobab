# 다국어(로컬라이징) 작업 가이드

지원 언어에 **독일어 · 프랑스어 · 포르투갈어 · 스페인어 · 러시아어**를 추가했고,
이어서 **폴란드어 · 튀르키예어 · 중남미 스페인어 · 이탈리아어**,
**우크라이나어 · 체코어 · 인도네시아어 · 베트남어**를 추가했습니다.
코드·설정·폰트 쪽 연결과 번역, 첫 실행 팝업의 언어 선택기까지 모두 끝나 있습니다.

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
`pl` / `tr` / `esLatam` / `it` / `uk` / `cs` / `ind` / `vi` 열세 개 열을 이미 넣어두었습니다.** (번역 전 열은 빈 문자열) 새로 키를 만들 필요 없이,
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
  "enumType": "",
  "enumValue": ""
}
```

### 지켜야 할 것

- **열 이름은 정확히 `de`, `fr`, `pt`, `es`, `ru`, `pl`, `tr`, `esLatam`, `it`, `uk`, `cs`, `ind`, `vi`** 입니다.
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

> 현재 번역 기준으로 (2)를 돌려 둔 상태입니다. (`Lorem_Optimum` 232자)
> 번역문을 고치거나 새 문장을 넣으면 다시 돌려 주세요. 안 돌리면 새 글자만 갈무리 자형으로
> 섞여 나오거나 네모로 나옵니다.

### 언어별로 어떤 폰트를 쓰는가

| 언어 | 폰트 | 아틀라스 |
|---|---|---|
| 한국어 · English | `Galmuri11_Optimum` (프리팹 원본 유지) | 동적 |
| 日本語 | `FusionPixel_JA` | 정적 → 굽기 필요 |
| 简体中文 | `FusionPixel_zh_hans` | 정적 → 굽기 필요 |
| 繁體中文 | `FusionPixel_zh_hant` | 정적 → 굽기 필요 |
| **독·불·포·서·러·폴·튀·중남미 서·이·우·체·인니** (12종) | **`Lorem_Optimum`** | **정적 → 굽기 필요** |
| Tiếng Việt | `Galmuri11_Optimum` (프리팹 원본 유지) | 동적 |

베트남어는 `Lorem`에 성조 글자(ạ ế ợ …)가 없어서 한국어·영어처럼 **갈무리11을 그대로** 씁니다.
갈무리11은 베트남어 글자를 전부 갖고 있고 동적 아틀라스라, 베트남어는 **굽기를 안 해도 됩니다.**

`Lorem`에는 한글·CJK 글리프가 없습니다. 첫 실행 팝업과 옵션 화면은 앱 언어가 바뀐 상태에서도
다른 언어의 이름을 보여주는데, 앱 언어가 `Lorem` 언어면 화면 전체가 `Lorem`으로 교체되므로
`한국어 / 日本語 / 简体中文 / 繁體中文` 네 라벨(12자)만 글리프를 못 찾습니다.
그래서 `Lorem_Optimum`의 폴백에 `FusionPixel_zh_hans`를 걸어 두었습니다.
**이 폴백을 지우면 그 네 라벨이 깨집니다.**

같은 이유로 `Tiếng Việt`의 `ế`는 `Lorem`·일본어·중국어 폰트 어디에도 없어서,
`Lorem_Optimum`과 `FusionPixel` 세 폰트의 폴백 끝에 `Galmuri11_Optimum`을 걸어 두었습니다.
굽기 후 콘솔에 `ế ệ`와 한글·한자 라벨 글자가 "미지원 문자"로 뜨는 것은 이 폴백이 처리하므로 정상입니다.

---

## 4. 첫 실행 언어 선택 팝업

`Assets/Prefabs/UI/MainMenu/UIView_MainMenu.prefab`의 `UI_InitialSetupPopup`은
`< 현재 언어 >` 좌우 선택기와 체크 버튼으로 언어를 고릅니다. 18개 언어가 모두 들어가 있습니다.
넘길 때마다 앱 언어가 바로 바뀌고, 체크 버튼을 누르면 약관 동의 화면으로 넘어갑니다.
약관 동의 화면에서 ESC나 패드 B를 누르면 언어 선택으로 되돌아옵니다.

### 언어를 추가할 때

1. `UI_InitialSetupPopup.languageBindings`에 한 줄을 넣습니다. 목록 순서가 곧 좌우 이동 순서입니다.
2. 프리팹의 `LanguageBox/LanguageSelector/ValueFrame` 안에 그 언어의 이름 라벨(`TXT_...`)을 만듭니다.
   라벨은 **그 언어를 표시할 폰트**(갈무리/FusionPixel/Lorem)와 `LocalizedFontTracker`를 갖고 있어야 합니다.
3. `Language Labels` 배열에 언어와 라벨을 짝지어 넣고, `LanguageBox/PageDots`에 점을 하나 추가해
   `Language Dots` 배열에 넣습니다. 점은 `languageBindings`와 **같은 순서**여야 합니다.

### 코드가 알아서 해주는 것

- **라벨에 표시될 언어 이름** — `OptionUI.json`에서 읽어 넣습니다. 직접 타이핑하지 마세요.
- 현재 언어의 라벨만 켜고, 영어 부제·페이지 점·점 커서를 함께 갱신합니다.

> 옵션 화면의 언어 선택기는 폭이 100px뿐이라 `Español (España)`, `Español (Latinoamérica)`,
> `Bahasa Indonesia`만 `OptionUI.json`의 짧은 표기 키(`LanguageSpanishShort`, `LanguageSpanishLatAmShort`,
> `LanguageIndonesianShort` → `Español`, `Español (LA)`, `Indonesia`)를 씁니다.
> 첫 실행 팝업은 폭이 넉넉해 전체 이름 키를 그대로 씁니다.

---

## 5. 언어를 더 늘릴 때

`Assets/Scripts/Application Layer/SettingsSystem/SettingsManager.cs` 상단에
손봐야 할 곳이 번호 순서로 정리되어 있습니다. 그 체크리스트를 따르세요.
