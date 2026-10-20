# TMP 스프라이트 에셋 사용 가이드

텍스트 안에 아이콘(예: 꽃)을 글자처럼 끼워 넣는 방법입니다. 어순이 언어마다 달라도 문장 안 원하는 자리에
`<sprite>` 태그만 넣으면 됩니다.

> 상태: 에셋 생성 도구와 `TMPInlineStyleAnimator` 보정은 코드에 들어가 있습니다.
> **생성 도구를 실행해 `.asset`을 만드는 것과 실제 화면 정렬 확인은 에디터에서 한 번 해야 합니다**(4장, 7장).

---

## 1. 무엇이 만들어지나

| 항목 | 내용 |
|---|---|
| 에셋 | `Assets/TextMesh Pro/Resources/Sprite Assets/LootGrowthIcons.asset` |
| 원본 텍스처 | `Assets/Graphics/Icons/Currency/Icon_Flower.png` (16×16) |
| 스프라이트 이름 | `flower` |
| 생성 도구 | [`TMPSpriteAssetBuilder.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/TextMeshPro/Editor/TMPSpriteAssetBuilder.cs) — 메뉴 `LumberBoy/UI/Build LootGrowthIcons Sprite Asset` |
| 애니메이터 보정 | [`TMPInlineStyleAnimator.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/TextMeshPro/TMPInlineStyleAnimator.cs) — `<sprite>` 태그가 글자 스타일 순서를 어긋나게 하지 않도록 보정 |

---

## 2. 텍스트에 넣는 방법

### 2.1 태그 문법

```
<sprite="LootGrowthIcons" name="flower">
```

- `"LootGrowthIcons"`는 에셋 이름, `name`은 그 에셋 안의 스프라이트 이름입니다.
- 에셋 이름을 **명시**하는 것을 권장합니다. 이렇게 하면 TMP 전역 기본 스프라이트 에셋(TMP Settings)을 건드리지 않고,
  텍스트 컴포넌트의 `Sprite Asset` 필드가 비어 있어도 `Resources/Sprite Assets/` 안에서 이름으로 찾아 씁니다.
- 닫는 태그는 없습니다.

### 2.2 현지화 JSON 예시 (`LootTooltipUI.json`)

```json
{
  "id": 8,
  "key": "growth_next",
  "kr": "<sprite=\"LootGrowthIcons\" name=\"flower\"> <COLOR=54D86A>{0}</COLOR> 에서 <COLOR=7EEDBE,EDD87E><Wave>성장</Wave></COLOR>",
  "en": "Grows at <sprite=\"LootGrowthIcons\" name=\"flower\"> <COLOR=54D86A>{0}</COLOR>"
}
```

- 언어마다 아이콘 위치를 다르게 둘 수 있습니다(위 예: 한국어는 앞, 영어는 뒤).
- `{0}` 같은 자리 표시자는 코드에서 `string.Format`으로 채웁니다.
- 최대 성장 문구는 색만 바꿉니다: `<COLOR=58D7F2>최대 성장</COLOR>`

### 2.3 코드에서 쓸 때

```csharp
string _text = locManager.GetText("growth_next");          // 태그가 들어 있는 원문
growthText.text = string.Format(_text, nextLevel);          // {0} 채우기
```

- 텍스트 컴포넌트에는 `TMPInlineStyleAnimator`가 붙어 있어야 `<COLOR=...>`/`<Wave>`가 동작합니다
  (이미 `TMP_Name`, `TMP_Description`에 붙어 있는 것과 같은 구성).
- `<sprite>` 태그 자체는 TMP가 처리하므로 애니메이터가 없어도 아이콘은 나옵니다.

---

## 3. `TMPInlineStyleAnimator`와의 관계 (확장 내용)

이 프로젝트의 `<COLOR=...>`, `<Wave>`, `<SHAKE>`는 TMP 표준 태그가 아니라 `TMPInlineStyleAnimator`가
직접 해석합니다. 글자마다 스타일을 기억하고, TMP의 **글자 인덱스**로 꺼내 씁니다.

- `<sprite>`는 눈에 보이는 글자 하나를 만들지만 예전에는 스타일 항목이 없어서, 뒤의 글자들이 한 칸씩 밀렸습니다.
- 지금은 `sprite`로 시작하는 태그를 만나면 스타일 항목을 하나 추가합니다.
  - **색 스타일은 입히지 않습니다**: 스프라이트는 원래 색(흰색 기준 텍스처 색)을 그대로 유지합니다.
  - **Wave/Shake 상태는 그대로 이어받습니다**: `<Wave>` 안에 스프라이트를 넣으면 아이콘도 같이 출렁입니다.
    출렁이지 않게 하려면 `<Wave>` 바깥에 두세요.
- 다른 태그(`<b>`, `<size>`)는 눈에 보이는 글자를 만들지 않으므로 변화가 없습니다.

---

## 4. 에셋을 만들고 갱신하는 법

### 4.1 이 프로젝트의 도구로 (권장)

1. 메뉴 `LumberBoy/UI/Build LootGrowthIcons Sprite Asset` 실행
2. `Assets/TextMesh Pro/Resources/Sprite Assets/LootGrowthIcons.asset`이 생기고(이미 있으면 내용만 다시 채움) 콘솔에 결과가 나옵니다.

### 4.2 아이콘을 더 추가하고 싶을 때

`TMPSpriteAssetBuilder`는 한 장짜리 텍스처를 한 항목으로 만드는 구조입니다. 아이콘이 늘어나면 두 방법 중 하나를 씁니다.

1. **방법 A (간단)**: 새 PNG마다 `Build…` 함수를 하나 더 복제해 별도 에셋으로 만든다. 태그는 `<sprite="새에셋이름" name="...">`.
2. **방법 B (한 에셋에 여러 아이콘)**: 아이콘들을 한 장의 시트 PNG로 묶고, 에디터 메뉴
   `Window > TextMeshPro > ...`의 Sprite Importer 또는 PNG의 `Sprite Mode = Multiple` 슬라이스 후
   `Create > TextMeshPro > Sprite Asset`로 만든다. 이때도 에셋 이름을 `LootGrowthIcons`와 겹치지 않게 한다.

> 도트 아트이므로 텍스처는 **Filter Mode = Point, 압축 없음**이어야 흐려지지 않습니다.
> (`Icon_Flower.png`는 이미 그렇게 설정되어 있습니다.)

---

## 5. 크기와 위치 조절

| 하고 싶은 것 | 방법 |
|---|---|
| 아이콘 크기 | 텍스트 폰트 크기에 비례합니다(스프라이트 에셋의 `pointSize`=16 기준). 크기 12 텍스트면 12px. 더 키우려면 아이콘만 `<size=110%><sprite…></size>` |
| 위아래 위치 | `<voffset=-1>` 같은 TMP 태그로 아이콘 앞에 오프셋. 에셋 쪽에서 바꾸려면 `TMP_SpriteGlyph`의 `metrics.horizontalBearingY` |
| 글자와의 간격 | 아이콘 뒤에 공백 한 칸(위 예시처럼). 좁게 하려면 `<space=2>` |
| 색 | 스프라이트는 텍스트의 `Vertex Color`에 곱해집니다. 단색 아이콘을 다른 색으로 쓰려면 `<sprite … color=#RRGGBB>` |

---

## 6. 자주 하는 실수

| 증상 | 원인 | 해결 |
|---|---|---|
| 아이콘 자리에 네모(□)/아무것도 안 나옴 | 에셋이 `Resources/Sprite Assets/` 밖에 있거나 이름 오타 | 위치와 `name` 확인(대소문자 구분 안 함, 철자는 정확히) |
| 아이콘이 흐리게 보임 | 텍스처 Filter Mode가 Bilinear | Point로 변경 |
| 아이콘 뒤 글자의 색/Wave가 한 칸씩 밀림 | `TMPInlineStyleAnimator`가 구버전 | 3장의 보정이 들어간 버전인지 확인 |
| 한 언어에서만 아이콘이 없음 | 해당 언어 JSON에 태그를 안 넣음 | 18개 언어 모두에 키 추가 |
| 에디터에서 안 보이고 플레이에서만 보임 | 에셋 참조가 Resources 경로 규칙을 벗어남 | 4.1 도구로 다시 생성 |

---

## 7. 이 문서 시점에 확인하지 못한 것

- 생성 도구로 실제 `.asset`을 만들어 보고, 12pt 텍스트에서 아이콘의 크기와 세로 위치가 텍스트와 맞는지 확인해야 합니다.
  맞지 않으면 `TMPSpriteAssetBuilder`의 `bearingY`(기본 0.875×16)와 `faceInfo` 값만 조정하면 됩니다.
- 캐릭터 선택 화면 등 폰트 폴백이 다른 곳에서의 정렬은 확인하지 않았습니다.
