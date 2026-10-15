# 전리품 성장 UI 가이드 (다음 성장 시점 문구 / 단계별 아우라)

전리품(원목 보험 증서, 포자 포션, 별빛 나침반)의 성장 단계(씨앗 → 꽃 → 나무)를 UI에 보여 주는 작업의 구성과,
**Application Layer 담당에게 요청할 연결 3건**을 정리한 문서입니다.

- UI(Presentation Layer) 쪽은 구현되어 있고, 아래 3장의 연결이 들어와야 실제 게임에서 문구와 단계별 색이 나옵니다.
- 연결 전에는 **성장 줄이 숨겨지고, 아우라 색은 기존(종류별) 그대로**입니다. 기존 동작은 깨지지 않습니다.

---

## 1. 무엇이 바뀌었나

| 화면 | 변경 |
|---|---|
| 전리품 기둥 상호작용 화면 (`HUD_LootReveal`) | 설명 아래 한 줄 추가: `🌸 N 에서 성장` / 3단계면 `최대 성장`(하늘색 `58D7F2`) |
| 마을 전리품 기둥 (`LootDisplayObject`) | 뒤 아우라 색을 **단계별**로(같은 단계면 전리품이 달라도 같은 색) |
| 텍스트 효과 | `<sprite>` 태그 사용 시 글자 색/Wave가 밀리지 않도록 `TMPInlineStyleAnimator` 보정 |

### 레이아웃 (`HUD_LootReveal`)

- 띠(`DimBG`/`DissolveBG`) 높이 120 → **132**(중심 0, 66 ~ -66).
- 이름/설명(`TextField`)은 기존 크기/줄바꿈 그대로이고 성장 줄은 별도 `GrowthMask`(RectMask2D) 안의 `TMP_Growth`입니다.
- 이름·설명·성장 줄 묶음을 **위로 10** 올려 위 여백 약 12 / 아래 여백 약 11을 둡니다. 기둥과 `pillarStartY`/`pillarTargetY`는 원래 값입니다.
- 값 변경은 `LootGrowthUiBuilder`의 `BandHeight`/`TextShiftUp` 상수를 고친 뒤 메뉴 `LumberBoy/UI/Build Loot Growth UI`를 다시 실행합니다.

### 문구 서식

| 부분 | 서식 |
|---|---|
| 꽃 아이콘 | `<sprite="LootGrowthIcons" name="flower">` (TMP 스프라이트 에셋, 원본 `Icon_Flower.png`) |
| `N` | `<COLOR=54D86A>` (초록) |
| `성장` | `<COLOR=7EEDBE,EDD87E><Wave>` |
| `최대 성장` | `<COLOR=58D7F2>` (하늘색) |

현지화 키는 `LootTooltipUI.json`의 `growth_next`(`{0}` = 다음 단계 프레스티지 레벨), `growth_max`이며 18개 언어가 들어 있습니다.
(영어 등은 아이콘과 `{0}` 위치가 한국어와 다릅니다. 번역은 초안이므로 번역가 검수가 필요합니다.)

### 단계별 아우라 색 (시안 A)

| 단계 | 색 |
|---|---|
| 1 (씨앗) | `5BE070` 초록 |
| 2 (꽃) | `58D7F2` 하늘 |
| 3 (나무) | `FFC94A` 금 |

값은 `LootGrowthVisualData` 에셋(`Assets/Scriptable Obj/LootData/`)에서 바꿉니다.
흑요 부적(성장 없음)은 단계 0이라 기존 색을 유지합니다.

---

## 2. 임시 스프라이트 → 정식 스프라이트 교체

단계별 스프라이트는 아직 없어서, `LootGrowthVisualData`의 9개 항목(전리품 3종 × 단계 3)이 **기존 스프라이트를 복사한 임시값**이며
`isPlaceholder = true`로 표시되어 있습니다.

교체 방법
1. `LootGrowthVisualData.asset`을 연다.
2. `Tier Sprites`에서 해당 (전리품, 단계) 항목의 `Sprite`를 정식 아트로 바꾼다.
3. 그 항목의 `Is Placeholder`를 끈다.

코드 수정은 필요 없습니다. 마을 기둥과 전리품 화면이 같은 데이터를 읽습니다.

임시임을 아는 방법
- 인스펙터의 `Is Placeholder` 체크.
- 플레이 중 임시 스프라이트가 처음 쓰이면 콘솔에 한 번만 경고가 나옵니다:
  `[LootDisplayObject] 임시 스프라이트 사용 중: <전리품> 단계 <N> ...` (화면에는 아무것도 추가로 그리지 않습니다.)

---

## 3. Application Layer에 요청할 연결 (UI는 준비 완료)

UI 담당은 Application Layer를 수정하지 않으므로, 아래 세 가지를 요청합니다. 모두 **한두 줄**입니다.

### R1. 다음 단계가 되는 프레스티지 레벨 조회

`InDungeonObjectManager`의 단계 기준 레벨은 비공개 직렬화 필드라 UI가 읽을 방법이 없습니다.
(`lostAndFoundBoxTier2PrestigeLevel`/`Tier3`, `sporePotionTier2`/`Tier3`, `starCompassTier2`/`Tier3`)

```csharp
// ILootDataProvider
/// <summary>다음 단계가 되는 프레스티지 레벨. 이미 최대 단계이거나 성장하지 않는 전리품이면 0.</summary>
int GetNextGrowthPrestigeLevel(LootType _lootType);

// InDungeonObjectManager (기존 GetLootGrowthTier 근처)
public int GetNextGrowthPrestigeLevel(LootType _lootType)
{
    int _tier = GetLootGrowthTier(_lootType);
    if (_tier <= 0 || 3 <= _tier) return 0;

    switch (_lootType)
    {
        case LootType.LostAndFoundBox: return 1 == _tier ? lostAndFoundBoxTier2PrestigeLevel : lostAndFoundBoxTier3PrestigeLevel;
        case LootType.SporePotion:     return 1 == _tier ? sporePotionTier2PrestigeLevel : sporePotionTier3PrestigeLevel;
        case LootType.StarCompass:     return 1 == _tier ? starCompassTier2PrestigeLevel : starCompassTier3PrestigeLevel;
        default: return 0;
    }
}
```

### R2. 전리품 화면에 성장 정보 원천 연결

`HUD`에 `ILootDataProvider`를 넘기는 곳(`GameplayUIManager`의 `hudUI.DependencyInjection(...)` 근처)에서 한 번만 호출합니다.

```csharp
screenModalUI.SetGrowthInfoSource(provider.GetLootGrowthTier, provider.GetNextGrowthPrestigeLevel);
```

- `screenModalUI`는 `UIView_ScreenModal`입니다. `Func<LootType,int>` 두 개를 받는 메서드를 UI 쪽에 미리 만들어 두었습니다.
- 이 호출이 없으면 성장 줄은 숨겨집니다.

### R3. 마을 기둥 생성 때 단계 전달

`LootPillarManager.SpawnAcquiredPillars`에서 `SetLootDisplay` 바로 뒤에 한 줄 추가합니다.

```csharp
pillar.SetLootDisplay(DisplayOrder[i]);
pillar.SetGrowthTier(_inDungeonObjectManager.GetLootGrowthTier(DisplayOrder[i]));   // 추가
```

- 단계 0(성장하지 않음/모름)이면 UI가 기존 종류별 아우라 색을 그대로 씁니다.

---

## 4. 구성 파일 요약

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/Environment/LootGrowthVisualData.cs` | 단계별 아우라 색 + (전리품, 단계)별 스프라이트(임시 표시 포함) |
| `Assets/Scripts/Presentation Layer/Environment/LootDisplayObject.cs` | `SetGrowthTier(int)`: 단계별 아우라 색/스프라이트 적용 |
| `Assets/Scripts/Presentation Layer/UISystem/UIView/HUD/HUD_LootReveal.cs` | 성장 줄 조립(`SetGrowthInfoSource`), 단계별 스프라이트 |
| `Assets/Scripts/Presentation Layer/UISystem/UIView/ScreenModal/UIView_ScreenModal.cs` | `SetGrowthInfoSource` 전달 |
| `Assets/Scripts/Presentation Layer/UISystem/TextMeshPro/TMPInlineStyleAnimator.cs` | `<sprite>` 태그 보정 |
| `Assets/Scripts/Presentation Layer/UISystem/TextMeshPro/Editor/TMPSpriteAssetBuilder.cs` | 꽃 아이콘 TMP 스프라이트 에셋 생성(`LumberBoy/UI/Build LootGrowthIcons Sprite Asset`) |
| `Assets/Scripts/Presentation Layer/Environment/Editor/LootGrowthUiBuilder.cs` | 데이터 에셋/프리팹 구성(`LumberBoy/UI/Build Loot Growth UI`) |
| `Assets/Resources/Localization/LootTooltipUI.json` | `growth_next`, `growth_max` |

스프라이트 에셋 사용법은 [TMPSpriteAsset_Guide.md](TMPSpriteAsset_Guide.md)를 참고하세요.

---

## 5. 임시 연결기 (인 게임 확인용)

`Assets/Scripts/Presentation Layer/UISystem/UIView/ScreenModal/Temp/LootGrowthDebugConnector.cs`
- 에디터/개발 빌드에서만 동작하며 R1~R3 없이 성장 줄/아우라를 확인하게 해 줍니다. F6으로 디버그 단계 1→2→3 순환.
- R1~R3가 연결되면 `Temp` 폴더째 삭제하세요.

---

## 6. 확인하지 못한 것

- 실제 게임(마을 기둥 상호작용)에서 성장 줄이 나오는 것은 R1~R3 연결 후에야 확인할 수 있습니다.
  지금은 같은 프리팹을 별도 화면에 올려 문구/색/Wave/배치만 확인했습니다.
- 마을 기둥의 아우라 색은 `useAuraOrbit` 경로의 `SetCenterGlowColor`를 쓰므로, 실제 렌더 색(HDR/블룸)은 마을에서 직접 보셔야 합니다.
- 번역(영어 외 17개 언어)은 초안입니다.
