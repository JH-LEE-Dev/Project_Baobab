# 다국어(로컬라이징) 작업 가이드

지원 언어에 **독일어 · 프랑스어 · 포르투갈어 · 스페인어 · 러시아어**를 추가했습니다.
코드·설정·폰트 쪽 연결은 모두 끝나 있으며, **남은 일은 JSON에 번역문을 채우는 것과
첫 실행 팝업에 언어 버튼 5개를 더 만드는 것**입니다.

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
| 9 | `Spanish` | `es` | Español | `spanish`, `latam` |
| 10 | `Russian` | `ru` | Русский | `russian` |

포르투갈어와 스페인어는 지역 변종(브라질 / 중남미)을 따로 두지 않고 하나로 모았습니다.
나눠야 할 만큼 번역이 달라지면 그때 `LanguageAutoDetect`의 코드 매핑부터 가르면 됩니다.

---

## 2. JSON에 무엇을 추가하면 되는가

`Assets/Resources/Localization/*.json`의 **모든 항목에 `de` / `fr` / `pt` / `es` / `ru`
다섯 개 열을 이미 빈 문자열로 넣어두었습니다.** 새로 키를 만들 필요 없이,
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
  "enumType": "",
  "enumValue": ""
}
```

### 지켜야 할 것

- **열 이름은 정확히 `de`, `fr`, `pt`, `es`, `ru`** 입니다. 오타가 나면 그 열은 통째로
  무시되고 조용히 영어로 나옵니다. (오류도 경고도 뜨지 않습니다)
- **빈칸으로 둬도 됩니다.** 비어 있는 항목은 영어(`en`)로 폴백합니다.
  그러니 한 번에 다 채우지 않고 파일 단위로 나눠 작업해도 게임은 정상 동작합니다.
- **`{0}`, `{1}` 같은 중괄호는 그대로 두세요.** 게임이 숫자·이름을 끼워 넣는 자리입니다.
  순서를 바꿔야 하는 언어라면 `{0}`/`{1}` 번호는 유지한 채 위치만 옮기면 됩니다.
- **`<COLOR=...>`, `<Wave>` 같은 태그도 그대로 두세요.** 태그 안의 값은 번역 대상이 아닙니다.
- `id`, `key`, `enumType`, `enumValue`는 건드리지 마세요. 코드가 이 값으로 문자열을 찾습니다.
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

신규 5개 언어가 쓰는 `Lorem_Optimum`도 **정적 아틀라스**이고 원본 TTF 참조가 비어 있어
런타임에 글리프를 채우지 못합니다. 즉 **번역문을 채울 때마다 (2)를 돌려야 합니다.**

> ⚠️ **지금 (2)를 한 번도 돌리지 않은 상태입니다.**
> `Lorem_Optimum` 아틀라스에는 ASCII 104자밖에 없어서,
> **이대로 러시아어를 선택하면 화면 전체가 네모로 나옵니다.**
> 프랑스어·포르투갈어·스페인어도 악센트 글자가 모두 깨집니다.
> (원본 `Lorem.ttf`에는 라틴 확장·키릴이 전부 들어 있으니, 굽기만 하면 해결됩니다)

### 언어별로 어떤 폰트를 쓰는가

| 언어 | 폰트 | 아틀라스 |
|---|---|---|
| 한국어 · English | `Galmuri11_Optimum` (프리팹 원본 유지) | 동적 |
| 日本語 | `FusionPixel_JA` | 정적 → 굽기 필요 |
| 简体中文 | `FusionPixel_zh_hans` | 정적 → 굽기 필요 |
| 繁體中文 | `FusionPixel_zh_hant` | 정적 → 굽기 필요 |
| **신규 5종** | **`Lorem_Optimum`** | **정적 → 굽기 필요** |

`Lorem`에는 한글·CJK 글리프가 없습니다. 첫 실행 팝업은 언어 이름을 한 화면에 모두
띄우는데 그때 화면 전체가 `Lorem`으로 교체되므로, 그 상태에서
`한국어 / 日本語 / 简体中文 / 繁體中文` 네 라벨(12자)만 글리프를 못 찾습니다.
그래서 `Lorem_Optimum`의 폴백에 `FusionPixel_zh_hans`를 걸어 두었습니다.
**이 폴백을 지우면 그 네 라벨이 깨집니다.**

---

## 4. 첫 실행 언어 선택 팝업 (UI 작업 필요)

`Assets/Prefabs/UI/MainMenu/UIView_MainMenu.prefab` 안
`LanguagePanel/LanguageButtons` 아래에 지금 버튼이 5개 있습니다.
여기에 **5개를 더 만들어 주세요.**

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

3. `UI_InitialSetupPopup`의 `Language Buttons` 배열에 추가합니다. (순서는 상관없습니다)

### 코드가 알아서 해주는 것

- **버튼에 표시될 언어 이름** — `OptionUI.json`에서 읽어 넣습니다. 직접 타이핑하지 마세요.
- **게임패드 상하좌우 이동 배선** — 버튼이 화면에 놓인 위치를 읽어 격자로 자동 계산합니다.
  `GridLayoutGroup`이 3열로 흘려주므로 10개면 3 / 3 / 3 / 1 로 배치되고, 그대로 이동합니다.
  좌우는 전체를 한 바퀴 돌고, 상하는 위아래 줄의 같은 칸으로 갑니다.
- 배치를 몇 열로 바꾸든 코드는 손대지 않아도 됩니다.

이름 조각이 하나도 걸리지 않으면 그 버튼은 한국어로 떨어지고 **콘솔에 경고가 뜹니다.**
버튼을 만들었는데 한국어 버튼이 두 개로 보인다면 이름을 확인하세요.

> `LanguageButtons` 오브젝트의 가로 크기(현재 220)는 3열 기준입니다.
> 줄 수가 늘어나는 만큼 패널 세로 크기와 창 크기도 함께 조정해 주세요.

---

## 5. 언어를 더 늘릴 때

`Assets/Scripts/Application Layer/SettingsSystem/SettingsManager.cs` 상단에
손봐야 할 곳이 번호 순서로 정리되어 있습니다. 그 체크리스트를 따르세요.
