# 전리품 진화 이펙트 인수인계 가이드

마을 `LootPillar`의 전리품 단계가 오를 때(씨앗 -> 꽃, 꽃 -> 나무) 재생되는 `VFX_LootEvolve`의 구조, 연동, 측정값, 미검증 사항을 정리한 문서입니다.

> **중요: 재생 시점은 아직 적용되지 않았습니다.** 의도한 흐름은 "특성 창(TentUI)에서 프레스티지 레벨업 -> TentUI를 닫고 나왔을 때 전리품 진화 연출 재생"입니다. 이를 위해 **단계가 바뀌는 즉시 기둥에 반영하던 기존 코드(`LootPillarManager`의 `LootGrowthTierChangedEvent` 구독)를 코드에서 뺐고**, 같은 타이밍(TentUI 닫힘)에 적용하도록 9장에 방법을 정리했습니다. 따라서 **지금은 마을에서 프레스티지가 올라도 기둥의 색/스프라이트/진화 연출이 바뀌지 않고, 다음에 마을에 들어올 때 새 단계로 만들어집니다.** 9장을 적용해야 마을에서 올린 단계가 반영됩니다. 이펙트 본체와 기둥 프리팹 연결은 끝나 있습니다.

- 수치 중 "실측"이라고 적은 것만 측정값입니다. 에디터 Mono 기준이고 인게임(플레이 모드)에서는 확인하지 못했습니다.
- 관련 문서: `Docs/VFX_Procedural_Pixel_Guide.md`, `Docs/StarRainVFX_Guide.md`, `Docs/LootGrowthUI_Guide.md`

---

## 1. 한눈에 보기

| 항목 | 내용 |
|---|---|
| 무엇 | 전리품이 진화(레벨업)했다는 인식을 확 심는 3.0초 연출 |
| 언제(현재 코드) | `LootDisplayObject.SetGrowthTier`가 "이미 1단계 이상인 상태에서 더 높은 단계"를 받으면 재생(마을 처음 생성과 단계 하락에서는 재생하지 않음). 그러나 이 메서드를 단계 변경 시점에 부르는 코드를 뺐기 때문에 현재는 아무도 재생하지 않는다 |
| 언제(의도) | TentUI에서 프레스티지 레벨업 후 TentUI를 닫았을 때. 9장 참고 |
| 어디서 | `LootPillar.prefab`의 자식 `LootEvolve`(풀을 쓰지 않음) |
| 단계 교체 시점 | 후광 색 / 스프라이트 교체는 폭발 시각(0.8초)에 콜백으로 이뤄진다. 그 전까지는 이전 단계 모습이 유지된다 |

### 1.1 시간표 (24Hz 틱, 총 72틱 = 3.0초)

| 구간 | 연출 |
|---|---|
| 0 ~ 15틱 모으기 | 바닥 룬 고리(`.` `+` `x`)가 차오르고 룬 8개가 켜짐, 빛 알갱이 30개가 소용돌이로 전리품에 빨려 들어감, 후광 점선 고리 두 겹이 조여듦, 전리품이 떨리며 3px 떠오름 |
| 15 ~ 19틱 임계 | 작은 흰 구체가 전리품을 가리고 위로 흰 빛줄기가 생김 |
| 19틱 폭발 | 도트 별 섬광 4단계, 빛기둥 + 헬릭스, 위로 훑는 스캔선, 16방향 광선, 바닥 이중 충격파 + 누운 별 + 8방향 줄기, 꽃잎/잎 30개, 별가루 18개 |
| 22 ~ 70틱 | 단계 마름모 3개(새 칸이 튀며 켜짐), ^ 화살표가 빛기둥 양옆에서 위로 솟음 |
| 43 ~ 70틱 마무리 | 별 4개가 전리품 둘레를 돌다 흡수, 바닥 룬 고리가 입자로 풀림 |

---

## 2. 변경된 파일

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_LootEvolve.cs` | 이펙트 본체 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/LootEvolveBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Loot Evolve`. 머티리얼과 `LootPillar.prefab` 자식을 만들고 참조를 연결 |
| `Assets/Graphics/VFX/Materials/M_LootEvolve.mat` | `Custom/VFX/BrandWrapGlow`, GlowMin 1.2 / GlowMax 6 |
| `Assets/Prefabs/Objects/LootPillar/LootPillar.prefab` | 자식 `LootEvolve`(루트 + `HaloMesh` + `GroundMesh`) 추가, `LootDisplayObject.evolveEffect` 연결 |
| `Assets/Scripts/Presentation Layer/Environment/LootDisplayObject.cs` | `evolveEffect` 필드, `SetGrowthTier` 분기, `OnEvolveBurst`, `OnDisable` 추가 |
| `Assets/Scripts/Presentation Layer/VFX/ItemAuraOrbitController.cs` | 중앙 글로우 진화 변조 API(`SetEvolveModulation`, `ClearEvolveModulation`)와 공용 `ApplyCenterGlowProperties` 추가 |

### 2.1 `LootDisplayObject` 변경 요약

```csharp
public void SetGrowthTier(int _tier)
{
    // 이미 1단계 이상에서 단계가 오르면 연출 재생 + 교체는 폭발 시각까지 지연
    if (null != evolveEffect && true == isActiveAndEnabled && 1 <= currentGrowthTier && currentGrowthTier < _tier)
    {
        int fromTier = 0 < pendingGrowthTier ? pendingGrowthTier : currentGrowthTier;
        evolveEffect.Play(fromTier, _tier, cachedOnEvolveBurst);   // 캐시된 델리게이트(할당 없음)
        pendingGrowthTier = _tier;
        return;
    }
    ...  // 그 외는 기존처럼 바로 적용
}
```

- `evolveEffect`가 비어 있으면 기존처럼 바로 바뀝니다(하위 호환).
- 연출 중에 오브젝트가 꺼지거나 다른 단계가 지정되면 `OnDisable`/`SetGrowthTier`가 보류된 단계를 바로 적용합니다.

---

## 3. 렌더 구조

```
LootPillar
  ├─ Pillar / Loot(전리품) / ...
  └─ LootEvolve              (공중 메쉬 + VFX_LootEvolve, 전리품 중심에 위치)   FlyingItem / 10
       ├─ HaloMesh           (받침대와 전리품 사이)    전리품 렌더러보다 한 칸 아래(받침대 위)
       └─ GroundMesh         (바닥)                    ShockWave / 0
```

- 좌표는 전리품 중심 기준 화면 픽셀(오른쪽 +, 아래쪽 +)입니다. 바닥 고리 중심은 전리품에서 아래로 29px입니다(받침대 아래 바닥).
- 받침대와 전리품 사이 층은 `itemRenderer.sortingLayerID`/`sortingOrder - 1`을 따라가서, 기둥의 Y 정렬(`ApplySortingBasis`)이 바뀌어도 맞춰집니다.
- 모든 그림은 톤 격자(`byte[]`)에 그린 뒤 가로로 이어진 같은 톤을 쿼드 하나로 합쳐 `PixelQuadBuffer`에 넣습니다. 나중에 그린 것이 이기므로 중복 쿼드가 없습니다.
- 격자와 정점 버퍼는 클래스 정적(세 층)이고 모든 기둥이 공유합니다. 각 기둥의 `LateUpdate`가 그리기, 병합, 메쉬 업로드를 한 번에 끝내므로 여러 기둥이 동시에 재생해도 서로 간섭하지 않습니다.

### 3.0 빛기둥 길이

- 빛기둥은 고정 길이가 아니라 **메인 카메라(직교) 위쪽 끝까지** 닿도록 매 프레임 길이를 정합니다(`ComputeBeamHeight`): `(카메라 위쪽 월드 Y - 전리품 Y) x 32 + 24px`, 128 ~ 420px로 제한. 카메라는 한 번 찾아 캐시하고 꺼지면 다시 찾습니다. 직교 카메라를 못 찾으면 128px입니다.
- 공중 층 격자만 위쪽으로 키웠습니다(`AirGridMinY` = -432). 바닥/받침대 사이 층은 그대로입니다. 격자는 클래스 정적 초기화(씬 로딩) 시점에 한 번 확보합니다.
- 카메라 크기별 실측(에디터, `RebuildMesh` 호출 단위): 직교 크기 4.5 -> 빛기둥 210px, 최대 1,256쿼드 / 직교 크기 7.0 -> 290px, 최대 1,496쿼드(빛기둥 가장자리의 점선이 길이에 비례해 쿼드를 늘림). 폭발 정점(1.0초) 프레임의 그리기 + 메쉬 구성은 약 0.12ms, Mono 힙 증가는 측정되지 않음(측정 단위 약 32KB).
- 주의: 카메라가 매우 멀리 줌아웃되어 위쪽 끝이 420px보다 멀면 기둥이 화면 안에서 끊깁니다(상한 때문). 그런 줌이 있으면 `MaxBeamHeight`와 격자 크기를 같이 늘리세요.

### 3.1 색

| 계열 | 톤 |
|---|---|
| 이전 단계 / 새 단계(초록, 보라, 금) | 계열마다 옅음 / 중간 / 진함 3톤 |
| 중립색 | 옅음 / 진함(단계 마름모 전용) |
| 흰색 | 1톤(섬광, 머리 코어) |

- 발광은 정점 알파로 정하며 톤마다 하나로 고정됩니다. 표시색은 `기본색 x lerp(1.2, 6, 알파)`이고 1을 넘는 분량이 블룸입니다.
- 단계 색 값은 `VFX_LootEvolve.FamilyTones`입니다. `LootGrowthVisualData`의 단계 색(5BE070 / B79CEB / FFC94A)과 같은 계열이지만 값 자체는 이펙트 안의 상수입니다.

### 3.2 아우라(전리품 뒤 중앙 글로우) 연동

기둥의 아우라는 현재 위성 개수가 0이라 전리품 뒤의 **중앙 글로우**(`ItemAuraOrbitController`의 `AuraCenterGlow`) 하나입니다. 이펙트가 같은 시간표로 이 글로우를 변조합니다.

| 구간(틱) | 크기 배율 | 발광 배율 | 흰색 섞기 |
|---|---|---|---|
| 모으기 0 ~ 15 | 1.0 -> 0.55 | 1.0 -> 2.2 | 0 -> 0.4 |
| 임계 15 ~ 19 | 0.55 -> 0.40 | 2.2 -> 2.6 | 0.4 -> 0.75 |
| 폭발 19 ~ | 1.5 -> 1.0 (12틱) | 2.3 -> 1.0 (24틱) | 0.75 -> 0 (7틱) |
| 안정 33 ~ 67 | 1.0 근처 +-5% 맥동 | 1.0 근처 +-0.2 맥동 | 0 |

- 곡선은 `VFX_LootEvolve.UpdateAura`에 있습니다. 컬링과 무관하게 매 프레임 갱신합니다(글로우는 이펙트 메쉬와 별개 오브젝트).
- 변조는 인스펙터 값(`centerGlowScale` 0.85, `centerGlowBloomMultiplier` 1)과 `LootDisplayObject`가 정한 단계 색 **위에** 곱하거나 섞입니다. 폭발 시각에 후광 색이 새 단계로 바뀌면(콜백) 흰색이 그 새 색에서 풀리며 새 색이 드러납니다.
- 끝나거나 중단되면(`FinishPlayback`/`OnDestroy`) `ClearEvolveModulation`으로 인스펙터 값과 단계 색 그대로 복구됩니다.
- 재사용 방법: `auraController`가 비어 있으면 아우라는 그대로입니다. 위성이 있는 아우라(`satelliteCount > 0`)를 쓰는 경우 위성 궤도 반지름/속도 변조는 이번에 넣지 않았습니다.
- 글로우 색 섞기는 **현재 단계 색 위에 흰색을 섞는 방식**이라 `ItemAuraOrbitController`의 `SetCenterGlowShaderColors`(단계 색)와 충돌하지 않습니다.

---

## 4. 이펙트 API

```csharp
// LootDisplayObject가 부른다
evolveEffect.Play(int fromTier, int toTier, Action onBurst);
bool playing = evolveEffect.IsPlaying;
```

- `onBurst`는 폭발 시각(19틱, 약 0.8초)에 한 번 불립니다. 연출이 도중에 끊기면(비활성화, 재생 재시작) 아직 안 불렸을 때 먼저 불립니다.
- 재생 중에 다시 `Play`하면 처음부터 다시 시작합니다.
- 전리품(`itemTransform`)은 모으기 동안 1~2px 떨리고 3px 떠오르며 폭발 후 원래 자리로 돌아옵니다. 연출이 끝나거나 꺼지면 원래 위치로 복구됩니다.

---

## 5. 최적화 (가이드 10.11 체크리스트 기준)

| 항목 | 상태 |
|---|---|
| 정적 공유 `PixelQuadBuffer` | 바닥 900 / 받침대 사이 300 / 공중 1500쿼드 초기 용량 |
| 메쉬 미리 확보 | 인스턴스 `Awake`에서 `ReserveMesh`(1800쿼드). 톤 격자(층당 약 44KB)는 클래스 정적 초기화 시점(씬 로딩)에 확보되어 첫 재생 중 할당이 없음 |
| 내용 해시 업로드 생략 | 적용(`Play`마다 -1로 초기화) |
| 메쉬 경계 | 실제 그린 쿼드 범위(월드 픽셀)로 갱신 |
| 화면 밖 | `PixelVfxCulling` |
| 소팅 캐시 | 받침대 사이 층 소팅은 값이 바뀔 때만 씀 |
| 쿼드 예산 | 층마다 `quadBudget`(기본 1800) 초과분은 그리지 않음 |
| 재생하지 않을 때 | 컴포넌트 `enabled = false`라 `LateUpdate`가 돌지 않음 |
| 힙 할당 | 재생 중 없음(정적 버퍼/격자, 고정 배열, 캐시된 델리게이트) |
| 해시 | 마무리 섞기를 넣은 상태 없는 해시(연속 번호가 사다리처럼 오르지 않음) |

### 실측 (에디터, Mono, `RebuildMesh` 호출 단위, 0.02초 간격 전체 구간)

| 항목 | 값 |
|---|---|
| 최대 쿼드 | 공중 621 / 받침대 사이 59 / 바닥 370, 합계 약 1,010 |
| 평균 | 약 0.045ms/프레임(600회 평균, 리플렉션 호출 포함) |
| Mono 힙 증가 | 600회 재구성에서 증가 없음(측정 단위 약 32KB) |

- 힙은 `Profiler.GetMonoUsedSizeLong` 증가량으로 쟀습니다. `GC.GetAllocatedBytesForCurrentThread`는 이 에디터에서 항상 0이라 쓰지 않았습니다(`StarRainVFX_Guide.md`의 0바이트 표기는 이 때문에 근거가 약함).
- 시간 측정은 메쉬 생성/업로드만 포함하고 렌더링/GPU 비용은 포함하지 않습니다.

---

## 6. 검증 내용

1. 컴파일/콘솔 오류 0.
2. 에디터에서 실제 `LootPillar.prefab`을 인스턴스화해 확인:
   - 처음 생성(0 -> 2단계)은 연출 없이 바로 `currentGrowthTier = 2`.
   - 2 -> 3단계 상승은 `playing = true`, `current = 2`, `pending = 3`으로 대기.
   - 폭발 시각 전(경과 0.33초)에는 `current = 2` 유지, 0.8초를 넘으면 `current = 3`, `pending = 0`.
3. 아우라: 틱별 글로우 크기/발광/코어 색을 읽어 곡선과 일치 확인(크기 0.85 -> 0.36 -> 1.26 -> 0.85, 코어 색이 이전 단계 쪽 흰색에서 새 단계 색으로 풀림). 종료 후 크기/발광이 기본값(0.85, 1.00)으로 복구되고, `SetEvolveModulation` 4000회 호출에서 Mono 힙 증가가 측정되지 않음(측정 단위 약 32KB).
4. 8개 시각의 렌더 캡처(모으기, 임계, 폭발, 빛기둥, 마름모/화살표, 마무리)가 시뮬레이션과 같은 모양으로 나옴.

---

## 7. 미검증 사항과 알려진 한계

| 항목 | 상태 |
|---|---|
| 플레이 모드에서 실제 프레스티지 상승 -> 마을 기둥 진화 재생 | 미확인 |
| 후처리 블룸이 적용된 실제 색 | 미확인(캡처는 블룸 없는 에디터 카메라) |
| 마을 실제 소팅(다른 기둥, 캐릭터, 나무와의 앞뒤) | 미확인. 빛기둥은 `FlyingItem`이라 항상 위에 그려짐 |
| 새 단계 전리품 외형 교체의 시각 효과 | 현재 단계 스프라이트가 임시(같은 그림)라 외형이 바뀌는 느낌은 확인 못 함. 흰 구체가 전리품을 가리고 스캔선이 훑는 것으로 대체 |
| 흰 실루엣 / 새 색 외곽선 1px / 스프라이트 팝 | 구현하지 않음(스프라이트 마스크가 필요하고, 비정수 배율 스케일은 픽셀이 깨져서) |
| 두 기둥이 동시에 진화하는 경우 | 각 기둥의 `LateUpdate`가 "그리기 -> 병합 -> 메쉬 업로드"를 한 번에 끝내므로 격자 공유로 인한 간섭은 없다(코드상 판단, 두 개 동시 재생은 미확인). 쿼드 합만 늘어난다(기둥당 약 1,010 최대) |
| IL2CPP/저사양 비용 | 미측정 |
| 24Hz 규칙과의 차이 | 이동/광선/입자는 렌더 프레임마다 갱신(부드럽게). 섬광/점멸/고리 회전은 24Hz 단계 |
| 어두워짐, 전체 화면 플래시, 받침대 윗면 빛 | 시뮬레이션에서 뺌(이펙트 쿼드로 구현 불가 / 요청으로 제거) |

---

## 8. 튜닝

| 증상 | 조절 |
|---|---|
| 쿼드가 부담된다 | 인스펙터 `quadBudget` |
| 모으기가 길다 / 짧다 | 코드 상수 `GatherTicks`(15), `BurstTick`(19) |
| 빛기둥 길이를 바꾸고 싶다 | 카메라 위쪽 끝까지 자동으로 늘어난다(`ComputeBeamHeight`). 최소 `MinBeamHeight`(128px), 최대 `MaxBeamHeight`(420px, 약 13유닛), 화면 위쪽을 넘기는 여유 `BeamScreenMargin`(24px) |
| 단계 색을 바꾸고 싶다 | `FamilyTones` 표(색 계열마다 3톤 유지) |
| 이펙트를 쓰고 싶지 않다 | `LootPillar.prefab`의 `LootDisplayObject.evolveEffect`를 비움(바로 교체) |

---

## 9. 적용 방법 (Application Layer 담당자용)

### 9.1 의도한 흐름

```
TentUI(특성 창)에서 특성을 찍어 프레스티지 레벨이 오름
  -> PrestigeLevelIncreasedSignal -> InDungeonObjectManager.PrestigeLevelIncreased
  -> 성장형 전리품의 단계가 바뀌면 LootGrowthTierChangedEvent 발행 (TentUI가 열려 있는 동안)
       => 이 시점에는 기둥을 건드리지 않고 "보류"만 한다 (연출 재생 안 함)
  -> TentUI가 닫힘 (TentUIClosedSignal: E 토글뿐 아니라 ESC / 패드 Cancel도 항상 발행)
       => 보류해 둔 진화를 기둥에 재생한다
```

### 9.2 현재 코드 상태 (이 문서가 적용을 요청하는 이유)

| 위치 | 상태 |
|---|---|
| `LootPillarManager` | **기존 즉시 반영 코드를 뺐다.** 단계 변경 이벤트(`InDungeonObjectManager.LootGrowthTierChangedEvent`)를 구독하지 않으므로, 마을에서 프레스티지가 올라도 이미 서 있는 기둥은 바뀌지 않는다. 기둥은 `SpawnAcquiredPillars`(마을에 들어올 때)에서만 현재 단계로 만들어진다 |
| `LootDisplayObject.SetGrowthTier` | 이미 1단계 이상에서 단계가 오르면 자동으로 진화 연출을 재생하는 분기가 들어 있다. 부르는 곳이 없어 현재는 작동하지 않으며, 9.3에서 연출 재생을 `PlayGrowthEvolve`로 분리한다 |

뺀 코드(참고, 이전에는 이 코드가 단계 변경 즉시 기둥에 반영했다):

```csharp
// SpawnAcquiredPillars 안, spawnedPillars.Clear() 바로 뒤
if (null != subscribedObjectManager) subscribedObjectManager.LootGrowthTierChangedEvent -= OnLootGrowthTierChanged;
subscribedObjectManager = _inDungeonObjectManager;
subscribedObjectManager.LootGrowthTierChangedEvent += OnLootGrowthTierChanged;

private void OnLootGrowthTierChanged(LootType _lootType, int _tier)   // 같은 종류 기둥에 즉시 SetGrowthTier
{
    for (int i = 0; i < spawnedPillars.Count; i++)
    {
        LootDisplayObject pillar = spawnedPillars[i];
        if (null == pillar || _lootType != pillar.CurrentLootType) continue;
        pillar.SetGrowthTier(_tier);
    }
}

private void OnDestroy() { /* 구독 해제 */ }
```

이 구독은 9.4에서 "보류만 하고, TentUI가 닫힐 때 재생"하는 형태로 되살려야 합니다. 색(후광)/스프라이트 교체도 그때 같이 반영됩니다(9.3의 `PlayGrowthEvolve`가 폭발 시각에 교체).

### 9.3 변경 1 - `LootDisplayObject` (Presentation Layer): 자동 재생을 명시적 호출로 분리

`SetGrowthTier`는 **연출 없이 즉시 적용**만 하도록 되돌리고, 연출 재생은 별도 메서드로 둡니다.

```csharp
// 마을 기둥 생성 / 로드 때 쓰는 즉시 적용(연출 없음). 진행 중이던 진화가 있으면 정리한다.
public void SetGrowthTier(int _tier)
{
    if (0 < pendingGrowthTier) pendingGrowthTier = 0;

    currentGrowthTier = _tier;
    ApplyGrowthVisual();
}

// 단계 상승 연출: 연출을 재생하고 후광/스프라이트 교체는 폭발 시각(0.8초)까지 미룬다.
// 연출 컴포넌트가 없거나 오브젝트가 꺼져 있거나 올라가는 변화가 아니면 즉시 적용한다.
public void PlayGrowthEvolve(int _tier)
{
    if (null == evolveEffect || false == isActiveAndEnabled || false == (1 <= currentGrowthTier && currentGrowthTier < _tier))
    {
        SetGrowthTier(_tier);
        return;
    }

    int fromTier = 0 < pendingGrowthTier ? pendingGrowthTier : currentGrowthTier;

    // 진행 중이던 연출이 있으면 Play가 먼저 정리하면서 이전 목표 단계를 적용한 뒤 새 연출을 시작한다.
    evolveEffect.Play(fromTier, _tier, cachedOnEvolveBurst);
    pendingGrowthTier = _tier;
}
```

- 지금 `SetGrowthTier`에 들어 있는 `if (null != evolveEffect && ...)` 분기를 위 `PlayGrowthEvolve`로 옮기면 됩니다. `OnEvolveBurst`, `OnDisable`, `cachedOnEvolveBurst`는 그대로 씁니다.
- 마을 기둥을 처음 만들 때(`SpawnAcquiredPillars`)는 `SetGrowthTier`를 그대로 부릅니다(연출 없음).

### 9.4 변경 2 - `LootPillarManager` (Application Layer): 보류 후 TentUI 닫힘에 재생

이벤트 구독 자체를 되살리되 즉시 반영 대신 보류합니다(9.2에 적은 구독/해제 코드의 `SetGrowthTier` 호출 부분을 아래로 바꾸는 형태).

```csharp
// 필드
private InDungeonObjectManager subscribedObjectManager;
private readonly Dictionary<LootType, int> pendingEvolutions = new Dictionary<LootType, int>(4);

// SpawnAcquiredPillars 안, spawnedPillars.Clear() 바로 뒤: 구독 + 보류 비우기(새 기둥은 이미 최신 단계로 만들어진다)
pendingEvolutions.Clear();
if (null != subscribedObjectManager) subscribedObjectManager.LootGrowthTierChangedEvent -= OnLootGrowthTierChanged;
subscribedObjectManager = _inDungeonObjectManager;
subscribedObjectManager.LootGrowthTierChangedEvent += OnLootGrowthTierChanged;

// 단계 변경 이벤트: 기둥은 건드리지 않고 최신 단계만 보류한다
private void OnLootGrowthTierChanged(LootType _lootType, int _tier)
{
    pendingEvolutions[_lootType] = _tier;
}

// TentUI가 닫혔을 때 TownSystem이 호출한다
public void PlayPendingEvolutions()
{
    if (0 == pendingEvolutions.Count) return;

    for (int i = 0; i < spawnedPillars.Count; i++)
    {
        LootDisplayObject pillar = spawnedPillars[i];
        if (null == pillar) continue;

        if (true == pendingEvolutions.TryGetValue(pillar.CurrentLootType, out int tier))
        {
            pillar.PlayGrowthEvolve(tier);
        }
    }

    pendingEvolutions.Clear();
}
```

`TownSystem`(이미 `TentUIClosedSignal`을 구독 중, `TownSystem.cs`의 `TentUIClosed`)에서 호출합니다.

```csharp
private void TentUIClosed(TentUIClosedSignal _tentUIClosedSignal)
{
    tentManager.SyncInteractStateOnExternalClose();
    lootPillarManager?.PlayPendingEvolutions();   // 추가
}
```

- `OnDestroy`에서는 구독을 해제하고 `pendingEvolutions.Clear()`를 부릅니다.
- `LootGrowthTierChangedEvent`는 레벨이 오를 때만 단계가 바뀐 전리품에 대해서만 발행되므로, 보류 딕셔너리는 단계가 실제로 바뀐 전리품만 담습니다.

### 9.5 여러 개가 한꺼번에 진화하거나 레벨업을 여러 번 한 경우

| 상황 | 동작 |
|---|---|
| TentUI에서 레벨업을 여러 번 하고 닫음 | 보류는 전리품마다 최신 단계 하나만 남는다. 재생은 "현재 표시 단계 -> 최신 단계"로 한 번(예: 1 -> 3이면 마름모 3칸이 한 번에 켜짐) |
| 여러 전리품이 동시에 단계가 오름 | 기둥마다 독립 연출이라 동시에 재생해도 된다. 한꺼번에 터지는 게 과하면 `PlayPendingEvolutions`에서 기둥마다 0.5~0.8초씩 시차를 두고 호출(코루틴/타이머)하는 방법이 있다 |
| 연출 재생 중 또 단계가 오름 | `PlayGrowthEvolve`가 이전 연출을 정리하면서 이전 목표 단계를 적용하고 새 연출을 시작한다 |
| 연출 중에 마을을 떠남/오브젝트가 꺼짐 | `LootDisplayObject.OnDisable`이 보류 단계를 바로 적용한다. 마을에 다시 들어오면 기둥이 최신 단계로 새로 만들어진다 |

### 9.6 기획/연출 확인이 필요한 부분

- **기둥이 화면 밖일 때:** TentUI를 닫는 시점에 캐릭터가 천막 근처에 있고 기둥이 화면 밖이면 연출이 보이지 않습니다(`PixelVfxCulling`이 화면 밖에서는 그리기를 건너뜁니다). 시간은 계속 흘러 0.8초에 단계는 바뀝니다. 선택지:
  1. 그대로 둔다(볼 수 있을 때만 보임).
  2. 연출 시작 때 카메라를 기둥으로 이동시킨다(카메라 연출은 Application Layer 작업).
  3. 보류해 두었다가 캐릭터가 기둥 근처에 도착하면 재생한다.
- **특성 창을 닫는 즉시 재생해도 되는지:** 닫히는 UI 연출(페이드)과 겹치는지 확인이 필요합니다. 겹치면 `TentUIClosedSignal`이 아니라 UI 닫힘 완료 시점에 맞추세요.

### 9.7 확인 체크리스트

0. (선행) 위 변경을 적용하기 전에는 마을에서 프레스티지를 올려도 기둥이 안 바뀌는 상태임을 확인한다.
1. 특성 창에서 프레스티지 레벨업 -> TentUI가 열려 있는 동안 기둥은 그대로이다.
2. E키로 닫은 경우 / ESC로 닫은 경우 / 패드 Cancel로 닫은 경우 모두 닫힌 직후 연출이 재생된다.
3. 프레스티지 레벨업을 해도 단계가 바뀌지 않는 구간에서는 연출이 없다.
4. 마을에 들어올 때(씬 로드)는 연출 없이 현재 단계로 보인다(`SetGrowthTier` 경로).
5. 여러 전리품이 동시에 단계가 오르는 레벨에서 모두 재생되고, 각자 폭발 시각에 후광 색/스프라이트가 바뀐다.
6. 연출 도중 마을을 떠났다가 돌아오면 최신 단계로 보인다.
