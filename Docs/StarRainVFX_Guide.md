# 별 비(유성우) 이펙트 인수인계 가이드

전리품 "별 비"의 연출 `VFX_StarRain`의 구조, 사용법, Application Layer 연동 방법(코드 포함), 측정값, 미검증 사항을 정리한 문서입니다. Application Layer는 이 작업에서 수정하지 않았고, 아래 4장의 코드를 다른 프로그래머가 적용해야 합니다.

- 수치 중 "실측"이라고 적은 것만 측정값이고, 나머지는 코드에서 읽은 값이거나 추정입니다.
- 인게임(플레이 모드)에서 확인하지 못한 항목은 7장에 모았습니다.

관련 문서: `Docs/VFX_Procedural_Pixel_Guide.md`(절차적 픽셀 VFX 규칙, 10장 최적화), `Docs/ConstellationNova_Guide.md`(같은 구조의 선행 이펙트)

---

## 1. 한눈에 보기

| 항목 | 내용 |
|---|---|
| 무엇 | 캐릭터 주변에 별이 비스듬히 여러 개 떨어지는 연출. 낙하 하나는 예고(과녁) -> 낙하 -> 충돌 폭발 -> 바닥 별 자국이 풀림 |
| 언제 | 나무를 벨 때마다 충전되어 100%가 되면 발동(발동 조건 자체는 Application Layer) |
| 구조 | **이펙트 하나가 낙하 N개를 돌린다.** 낙하마다 인스턴스를 만들지 않고, 같은 바닥 메쉬 / 공중 메쉬 둘에 함께 그린다 |
| 풀링 | `VFXComponent` 태그 풀(제안 태그 `StarRainEffect`) |
| 데미지 | 이펙트는 그림만 그린다. 낙하의 충돌 위치/시각은 `ComputeImpacts`로 같은 시드에서 미리 얻어 판정에 쓴다 |

단계별 지속시간과 낙하 수:

| 단계 | 지속시간 | 최소 낙하 수 | 강조색 |
|---|---|---|---|
| 씨앗 | 3초 | 9 | 하늘색(푸른 계열 옅음) |
| 꽃 | 4초 | 12 | 보라 |
| 나무 | 5초 | 15 | 금색, 큰 별 몇 개 |

낙하 수는 `[ceil(지속시간 x 3), 40]`로 제한됩니다(`VFX_StarRain.ClampCount`). 최소 = 초당 3개, 최대 40은 쿼드 비용 상한입니다.

---

## 2. 파일

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_StarRain.cs` | 이펙트 본체 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/StarRainBuilder.cs` | 머티리얼/프리팹 생성기. 메뉴 `LumberBoy/VFX/Build Star Rain` |
| `Assets/Prefabs/VFX/StarRain/VFX_StarRain.prefab` | 풀에서 쓰는 프리팹(별자리 노바 프리팹 구조 복제) |
| `Assets/Graphics/VFX/Materials/M_StarRain.mat` | `Custom/VFX/BrandWrapGlow`, GlowMin 1.2 / GlowMax 6(노바와 같음) |
| 공용(기존) | `PixelQuadBuffer`, `PixelVfxCulling` |

프리팹 구조: `VFX_StarRain`(루트 ParticleSystem, 풀 수명 타이머 10초) -> `RainMesh`(공중 메쉬 + `VFX_StarRain`), `GroundMesh`(바닥 메쉬).

---

## 3. 이펙트 API

```csharp
// 풀에서 꺼낸 직후(첫 LateUpdate 전)에 호출
rain.Play(int count, float duration, float radius, int seed, int tier);

// 허용 범위로 제한(최소 ceil(duration x 3), 최대 40). 판정 쪽도 같은 함수로 맞춘다
int clamped = VFX_StarRain.ClampCount(count, duration);

// 이펙트 인스턴스 없이 충돌 위치/시각을 계산(Play에 같은 인자를 쓰면 이펙트가 그리는 낙하와 정확히 일치)
int n = VFX_StarRain.ComputeImpacts(origin, seed, clamped, duration, radius, StarRainImpact[] results);
```

| 인자 | 의미 |
|---|---|
| `count` | 낙하 수. `ClampCount`로 제한된다 |
| `duration` | 낙하가 이어지는 시간(초), 2~8로 제한 |
| `radius` | 낙하 범위 가로 반지름(월드 유닛, 1~6로 제한). 세로는 절반(쿼터뷰 바닥 타원) |
| `seed` | 낙하 위치/시각을 정하는 시드. 같은 값이면 같은 결과 |
| `tier` | 1 씨앗 / 2 꽃 / 3 나무(강조색, 3단계는 큰 별) |

- `StarRainImpact.position`은 충돌 중심(월드), `time`은 재생 시작 후 충돌까지의 초입니다.
- `origin`은 이펙트를 재생한 위치(시전자 발밑)와 같아야 합니다. 이펙트는 시작 시 그 위치를 정수 픽셀로 내림해 쓰고, `ComputeImpacts`도 같은 처리를 합니다.
- 인스턴스에서는 `ImpactCount`, `TryGetImpact(index, out StarRainImpact)`로도 읽을 수 있습니다(첫 LateUpdate 이후).
- `Play`를 부르지 않으면 인스펙터 기본값(단계 2, 4초, 반지름 2.4, 임의 시드)으로 재생됩니다.

### 시드와 판정 일치

시전 시점에 Application Layer가 `seed`를 하나 정하고, 같은 값으로 `Play`(이펙트)와 `ComputeImpacts`(판정)를 부르면 그림과 판정이 항상 같은 위치에서 일어납니다. 예: `seed = Time.frameCount` 또는 런 시드 + 시전 횟수.

---

## 4. Application Layer 연동 (직접 적용 필요)

### 4.1 `InDungeonVFXManager.cs` — 재생 메서드 추가

`PlayConstellationNovaVFX` 아래에 같은 패턴으로 추가합니다.

```csharp
public void PlayStarRainVFX(Vector3 _center, int _count, float _duration, float _radius, int _seed, int _tier)
{
    if (vfxComponent == null) return;

    const string sortingLayerName = "FlyingItem";
    const int sortingOrder = 10;

    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "StarRainEffect", _center, Quaternion.identity,
        sortingLayerName, sortingOrder, null));

    if (effect == null) return;

    PresentationLayer.VFX.VFX_StarRain rain =
        effect.GetComponentInChildren<PresentationLayer.VFX.VFX_StarRain>(true);
    if (rain != null) rain.Play(_count, _duration, _radius, _seed, _tier);
}
```

- `parent = null`: 시전자를 따라다니지 않고 월드에 고정됩니다(낙하가 시전 위치 주변에 떨어짐).
- 소팅 `FlyingItem`/10은 공중 메쉬에 적용되고, 바닥 메쉬는 스크립트가 `ShockWave`/0으로 다시 맞춥니다(노바와 같은 이유).

### 4.2 `GameInstaller.prefab` — 풀 항목 추가

`VFXManager` 오브젝트의 `VFXComponent.vfxPoolDataList`에 추가합니다.

```yaml
- vfxTag: StarRainEffect
  effectPrefab: <Assets/Prefabs/VFX/StarRain/VFX_StarRain.prefab 의 루트 ParticleSystem>
  initialPoolSize: 1
  allowDynamicExpansion: 1
  maxPoolSize: 2
  uiParticleScale: 0
```

- 태그 문자열이 다르면 `Play`가 null을 돌려주고 아무것도 나오지 않습니다(오류 로그 없음).
- 루트 ParticleSystem 수명이 10초라, 재생이 끝나도 풀 인스턴스는 최대 10초 점유됩니다. 동시 발동이 드물면 풀 1~2개로 충분합니다.

### 4.3 데미지 판정

이펙트는 데미지를 주지 않습니다. 시전할 때 충돌 목록을 미리 얻고, 각 충돌 시각에 판정합니다.

```csharp
// 시전 시점
int count = VFX_StarRain.ClampCount(requestedCount, duration);
StarRainImpact[] impacts = new StarRainImpact[VFX_StarRain.MaxCount];   // 재사용 버퍼로 둘 것
int n = VFX_StarRain.ComputeImpacts(origin, seed, count, duration, radius, impacts);
inDungeonVFXManager.PlayStarRainVFX(origin, count, duration, radius, seed, tier);

for (int i = 0; i < n; i++)
{
    // impacts[i].time초 뒤에 impacts[i].position 중심으로 판정(코루틴/타이머 누적 등 기존 방식 사용)
}
```

- 시각적인 폭발 반경은 가로 약 24px(0.75유닛), 세로는 절반입니다(큰 별은 1.4배). 피격 범위를 이 값에 맞추려면 별자리 노바와 같은 바닥 타원 거리(`VFX_ConstellationNova.GetGroundDistance`)를 쓰면 됩니다.
- 큰 별(나무 단계)은 4, 9, 13번째 낙하와, 낙하가 15개를 넘을 때 5의 배수 번째 낙하입니다. 데미지에 반영하려면 같은 규칙을 따라야 합니다(현재 `ComputeImpacts`는 이를 돌려주지 않음).

---

## 5. 시각 사양

- **색:** 푸른 계열 3톤(옅음 140,199,235 / 중간 77,133,255 / 진함 43,71,184), 흰색(섬광 전용), 단계 강조색(씨앗은 푸른 옅음, 꽃 보라 184,158,255, 나무 금색 255,214,107). 한 색 계열 안에서 3톤을 넘지 않습니다.
- **발광:** 정점 알파 = 발광 세기. 톤마다 하나로 고정(흰색 74, 옅음 13, 중간/진함 0, 강조색 13~16). 표시색은 `기본색 x lerp(1.2, 6, 알파)`이고 1을 넘는 분량이 블룸입니다.
- **갱신:** 낙하/줄기/파편/솟는 별은 렌더 프레임마다 시간으로 다시 계산(연속), 섬광 4단계, 빛기둥 3단계, 점멸, 디더, 고리 회전은 24Hz 단계입니다.
- **레이아웃:**
  - 바닥 메쉬(`ShockWave`/0): 예고 원판과 점선 고리, 중심 별 표식, 8방향 줄기, 바닥 별 자국, 발동 때 발밑 별
  - 공중 메쉬: 낙하 줄기, 도트 별 머리, 충돌 섬광, 빛기둥, 파편, 불씨, 솟는 별

---

## 6. 구현과 최적화 (가이드 10.11 체크리스트 기준)

| 항목 | 상태 |
|---|---|
| 정적 공유 `PixelQuadBuffer` | 바닥/공중 각 1개(static), 생성자 용량 1600쿼드 |
| `ReserveMesh` | 메쉬 생성 시 2400쿼드 확보 |
| 내용 해시 업로드 생략 | 적용(정점 수 + `ContentHash`, `Begin`에서 -1 초기화) |
| 실제 그린 범위의 메쉬 경계 | 적용(그린 쿼드의 월드 픽셀 범위로 갱신) |
| `PixelVfxCulling` | 낙하 가능 범위 전체로 화면 밖이면 재구성 생략 |
| 소팅 캐시 | 루트 렌더러 값이 바뀔 때만 자식에 씀(`OnEnable`에서 무효화) |
| 쿼드 합치기 | 층마다 톤 격자에 그린 뒤 가로로 이어진 같은 톤을 쿼드 하나로 합침. 겹치면 나중에 그린 것이 이김(중복 쿼드 없음) |
| 쿼드 예산 | 두 메쉬 합 한 프레임 `quadBudget`(기본 2400) 초과분은 그리지 않음 |
| 힙 할당 | 재생 중 없음(정적 버퍼, 고정 배열, 구조체, 람다/LINQ 없음) |

### 실측 (에디터, Mono, `RebuildMesh` 호출 단위, 0.02초 간격 전체 구간)

| 설정 | 최대 쿼드 | 평균 | 최대(1회) | 호출당 할당 |
|---|---|---|---|---|
| 나무 15개, 5초 | 735 | 0.063ms | 0.793ms | 0바이트 |
| 씨앗 9개, 3초 | 807 | 0.053ms | 0.164ms | 0바이트 |
| 나무 40개(최대), 5초 | 1,770 | 0.128ms | 0.397ms | 0바이트 |

- 측정에는 메쉬 생성/업로드가 포함되고, ParticleSystem/GPU/렌더링 비용은 포함되지 않습니다.
- 최대값은 JIT/에디터 잡음이 섞인 단발 스파이크일 수 있습니다.
- 쿼드 수는 낙하가 겹치는 정도에 따라 달라집니다. 같은 낙하 수에서도 지속시간이 짧으면(동시 진행이 많으면) 커집니다(씨앗 9개/3초가 나무 15개/5초보다 큼).

---

## 7. 미검증 사항과 알려진 한계

| 항목 | 상태 |
|---|---|
| 플레이 모드 동작(풀에서 재생, 풀 반환) | 미확인. 에디터에서 `Begin`/`RebuildMesh`를 직접 호출해 그림, 쿼드, 시간만 확인 |
| 후처리 블룸 적용 외형 | 미확인. 캡처는 블룸 없는 에디터 카메라. 발광 알파는 규칙 문서와 노바의 값을 참고해 정함 |
| 3스테이지 실제 배경, 나무/캐릭터와의 소팅 | 미확인. 단색 배경과 대역 스프라이트만 확인 |
| `GameInstaller` 풀 등록과 `InDungeonVFXManager` 호출 | 적용하지 않음(4장 코드를 적용해야 함) |
| 데미지 판정 연동 | 적용하지 않음(4.3 방식은 제안) |
| 지형/나무 위 낙하 위치 | 이펙트는 낙하 위치가 장애물 위인지 판단하지 않음 |
| 낙하 수 40, 반지름 6에서의 외형 | 쿼드와 시간만 측정(1,770쿼드, 반지름은 기본 2.4만 캡처) |
| IL2CPP/저사양 기기 비용 | 미측정 |
| 24Hz 규칙과의 차이 | 낙하/줄기/파편은 렌더 프레임마다 갱신(부드럽게 해 달라는 요청). 24Hz로 되돌리려면 `RebuildMesh`의 시간을 틱 단위로 내림하면 됨 |

---

## 8. 튜닝

| 증상 | 조절 |
|---|---|
| 낙하가 너무 빽빽하다 / 성기다 | `Play`의 `count`(제한 범위 안), `radius` |
| 쿼드가 부담된다 | `quadBudget`(인스펙터), 낙하 수 상한은 코드 상수 `MaxCount` |
| 낙하 속도/곡선 | 코드 상수 `FallTicks`(12), `FallCurve`(1.6) |
| 예고가 짧다 / 길다 | `TeleTicks`(17) |
| 낙하 시작이 너무 이르다 / 늦다 | `FirstImpactTime`(0.9초), `TailMargin`(0.2초) |
| 낙하끼리 너무 붙는다 | `MinSpacing`(22px, 낙하가 많으면 자동으로 줄어듦) |
| 충돌 섬광이 과하다 | `FlashArms`, `PillarHeights` 테이블, 흰색 톤 알파(`SetupTones`의 74) |
