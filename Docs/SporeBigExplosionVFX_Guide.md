# 포자 대폭발 VFX 연동 가이드

2스테이지 전리품 "포자 포션"의 효과인 **포자 대폭발**(나무를 벌목하면 확률로 타원 범위 폭발)의 이펙트를 게임에 연결하는 방법을 정리한 문서입니다.

- 이펙트(Presentation Layer)는 구현되어 있습니다. **Application Layer 3곳을 고치면 게임에서 재생됩니다.**
- 연결 전에는 판정(피해, 피해 증폭)만 동작하고 이펙트는 나오지 않습니다. 기존 동작은 깨지지 않습니다.
- 인 게임 확인용 단축키와 임시 확인기는 제거했습니다. 확인은 7장의 방법으로 합니다.

---

## 1. 한 장 요약

| 순서 | 어디를 | 무엇을 |
|---|---|---|
| R1 | `GameInstaller.prefab` | VFX 풀에 `SporeBigExplosionEffect` 항목 추가 |
| R2 | `InDungeonVFXManager.cs` | `PlaySporeBigExplosionVFX(_center, _radius, _tier)` 메서드 추가 |
| R3 | `InDungeonObjectManager.cs` | 포자 대폭발 발동 지점에서 R2 호출 한 줄 추가 |

세 곳 중 하나라도 빠지면 이펙트는 나오지 않습니다. R1이 빠지면 `VFXComponent.Play`가 태그를 못 찾아 **경고 없이 `null`을 돌려주므로**(R2의 `effect == null` 분기에서 조용히 종료) 증상만으로는 원인을 알기 어렵습니다.

---

## 2. R1. 풀 태그 등록

`Assets/Prefabs/Installer/Installer/GameInstaller.prefab`의 VFX 풀 목록(`ConstellationNovaEffect` 항목이 있는 곳)에 같은 형식으로 항목 하나를 추가합니다.

| 필드 | 값 |
|---|---|
| `vfxTag` | `SporeBigExplosionEffect` |
| `effectPrefab` | `Assets/Prefabs/VFX/Spore/VFX_SporeBigExplosion.prefab` |
| `initialPoolSize` | 4 |
| `allowDynamicExpansion` | 켜기(1) |
| `maxPoolSize` | 16 |
| `uiParticleScale` | 0 |

- 대폭발은 확률 발동이라 동시에 겹칠 일이 적습니다. 풀 크기는 위 값이면 충분합니다.
- 프리팹 루트는 `ParticleSystem`(수명 1.6초)이고, 풀 규약(루트 수명 > 이펙트 길이 1.3초)을 지킵니다.

---

## 3. R2. 재생 메서드 추가

`Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs`의 `PlayConstellationNovaVFX` 바로 아래에 같은 패턴으로 추가합니다.

```csharp
/// <summary>
/// 포자 대폭발 연출. _center는 쓰러진 나무 자리, _radius는 판정 범위(월드 유닛, 가로 반지름), _tier는 포자 포션 성장 단계(1~3)다.
/// 알갱이 위치는 재생마다 무작위라 같은 호출도 매번 다른 모양이 나온다.
/// </summary>
public void PlaySporeBigExplosionVFX(Vector3 _center, float _radius, int _tier)
{
    if (vfxComponent == null) return;

    // 별자리 노바와 같은 소팅 레이어/순서(바닥 메쉬는 이펙트 스크립트가 ShockWave 레이어로 다시 맞춘다).
    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "SporeBigExplosionEffect",
        _center,
        Quaternion.identity,
        "FlyingItem",
        10,
        null
    ));

    if (effect == null) return;

    // 풀에서 꺼낸 직후(첫 LateUpdate 전)에 범위와 단계를 정해야 한다.
    PresentationLayer.VFX.VFX_SporeBigExplosion explosion = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_SporeBigExplosion>(true);
    if (explosion == null) return;

    if (0.0f < _radius) explosion.SetRange(_radius);
    explosion.SetTier(_tier);
}
```

### 인자

| 인자 | 값 | 설명 |
|---|---|---|
| `_center` | 쓰러진 나무 위치 | 판정 중심과 같은 값(`SporeBigExplosionRoutine`의 `_centerPos`)을 그대로 넘긴다 |
| `_radius` | `sporeBigExplosionRange`(2.5) | 타원 **가로** 반지름. 세로는 0.5배. 판정과 같은 값이어야 보이는 범위와 맞는다 |
| `_tier` | 1 씨앗 ~ 3 나무 | 연막/알갱이 색이 단계별로 달라진다. 0이면 기존 연막과 같은 민트 |

| 단계 | 색 |
|---|---|
| 0 | 민트(기존 포자 폭발과 같은 색) |
| 1 (씨앗) | 초록 |
| 2 (꽃) | 연보라 |
| 3 (나무) | 금 |

---

## 4. R3. 호출 한 줄

`Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs`의 `TriggerSporeBigExplosion`에 추가하는 것을 권장합니다.

```csharp
private void TriggerSporeBigExplosion(Vector3 _centerPos, int _tier)
{
    inDungeonVFXManager.PlaySporeBigExplosionVFX(_centerPos, sporeBigExplosionRange, _tier);   // 추가
    StartCoroutine(SporeBigExplosionRoutine(_centerPos, _tier));
}
```

- 이 위치는 나무가 쓰러진 **그 프레임**입니다. 이펙트의 응집(기 모으기)이 즉시 시작되어 0.2초 뒤에 폭발합니다.
- `inDungeonVFXManager`가 null일 수 있는 상황(테스트 씬 등)이면 기존 코드(예: `PlayTreeDeadVFX` 호출부)와 같은 방식으로 null을 걸러 주세요.

---

## 5. 판정과 이펙트 타이밍

| 시각(초) | 이펙트 | 판정 |
|---|---|---|
| 0 | 응집 시작 | 나무가 쓰러짐 |
| 한 프레임 뒤 | 응집 중 | **피해 적용**(`SporeBigExplosionRoutine`의 `yield return null` 이후) |
| 0.2 | **폭발** | 피해는 이미 들어간 상태 |

판정이 이펙트의 폭발보다 **약 0.2초 먼저** 들어갑니다. 체감이 어색하면 선택지는 두 가지입니다.

| 선택지 | 방법 | 영향 |
|---|---|---|
| A. 판정을 늦춤 | `SporeBigExplosionRoutine`의 `yield return null;`을 `yield return new WaitForSeconds(PresentationLayer.VFX.VFX_SporeBigExplosion.BurstDelay);`로 교체 | 피해와 폭발이 같은 순간. 판정이 0.2초 늦어지므로 기획 확인이 필요 |
| B. 응집을 줄임 | `VFX_SporeBigExplosion.cs`의 `PreTime`(0.2), `InhaleTravelTime`(0.15) 값을 줄임 | 판정과의 차이가 줄어듦. 기 모으는 연출이 짧아짐 |

SFX를 붙인 뒤 소리 타이밍을 보고 정하기로 했으므로, **지금은 A/B 모두 적용하지 않은 상태**입니다. 응집 길이를 바꿀 때 만지는 곳은 `VFX_SporeBigExplosion.cs`의 `PreTime`, `InhaleTravelTime`, 알갱이 출발 시차(`SetupInhale`의 `RandRange(0.0f, 0.06f)`), 이동 곡선(`DrawInhale`의 `e = pr*pr*pr`)입니다.

---

## 6. 이 이펙트에 들어 있지 않은 것

| 항목 | 이유와 방법 |
|---|---|
| 피격된 나무의 점멸/타격 표시 | 기존 `TreeFlashSystem.Begin(...)`을 판정 루프에서 부르면 됩니다 |
| 피격된 나무의 **디버프 이펙트**(영구 피해 증폭 표시) | 별도 작업(다음 단계). `ApplySporeDamageAmplify` 호출 지점에 붙일 예정 |
| 사운드(SFX) | 이번 범위 밖. 폭발 순간은 재생 시작 후 `BurstDelay`(0.2초)입니다 |
| 화면 흔들림/히트스톱 | 이번 범위 밖(카메라 기능 연동 필요) |

---

## 7. 연동 후 확인 방법

1. 2스테이지(포자 숲)에서 "포자 포션"을 가진 상태로 나무를 계속 벌목합니다(확률 발동: 1단계 5%, 2단계 7.5%, 3단계 10%).
2. 확인할 것:
   - 나무가 쓰러진 자리에서 알갱이가 한 점으로 모였다가 0.2초 뒤에 터지는가
   - 범위가 판정 범위(타원 5x5 정도)와 눈으로 맞는가
   - 연막이 나무 위로 올라오지만 플레이어와 나무를 완전히 가리지는 않는가
   - 성장 단계에 따라 색이 달라지는가(초록, 연보라, 금)
   - 같은 단계에서도 매번 연막 모양이 다른가
3. 디버그로 단계를 바꾸고 싶으면 인스펙터에서 포자 포션 성장 단계의 프레스티지 레벨 기준값(`sporePotionTier2PrestigeLevel` 등)을 조정하세요.

### 문제 해결

| 증상 | 원인과 해결 |
|---|---|
| 이펙트가 전혀 안 나옴 | R1 항목(`vfxTag` 오타 포함) 확인. 태그가 없으면 `Play`가 경고 없이 `null`을 돌려주므로, R2에서 `effect == null`일 때 임시로 `Debug.LogWarning`을 찍어 확인 |
| 범위가 판정과 어긋남 | R3에서 `_radius`로 `sporeBigExplosionRange`를 넘겼는지 확인 |
| 항상 민트 색 | `SetTier`가 호출되지 않거나 `_tier`가 0. R2의 `explosion.SetTier(_tier)` 확인 |
| 연막만 나오고 알갱이가 없음 | `VFX_SporeBigExplosion` 컴포넌트가 프리팹의 `SporeMesh` 자식에 붙어 있는지, 머티리얼이 `M_SporeBigExplosion`인지 확인 |
| 이펙트가 나무 뒤로 가려짐 | 소팅 레이어/오더 인자(`"FlyingItem", 10`) 확인 |
| 이펙트가 너무 많이 가림 | 프리팹 인스펙터의 `puffCount`(밝은 연막), `smokeCount`(스모크), `puffHeightPx`를 줄임 |
| 너무 밝게 번짐/안 번짐 | `M_SporeBigExplosionPuff.mat`의 `_Glow`(연막), 인스펙터의 `glowBase/glowLight/glowHot`(알갱이). 게임 블룸 임계값은 1.345 |

---

## 8. 구성 파일

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_SporeBigExplosion.cs` | 이펙트 본체(`SetRange`, `SetTier`, `BurstDelay`, `TotalDuration`) |
| `Assets/Prefabs/VFX/Spore/VFX_SporeBigExplosion.prefab` | 루트 ParticleSystem + `SporeMesh`(공중) + `GroundMesh`(바닥, ShockWave) + `Puff_0~4`(밝은 연막) + `Smoke_0~1`(스모크) |
| `Assets/Graphics/VFX/Materials/M_SporeBigExplosion.mat` | 알갱이/고리 메쉬용(`BrandWrapGlow`, GlowMin 1.0 / GlowMax 3.0) |
| `Assets/Graphics/VFX/Materials/M_SporeBigExplosionPuff.mat` | 연막/스모크용(`Custom/VFX/SpriteGlow`, `_Glow` 1.4) |
| `Assets/Shaders/VFX/SpriteGlow.shader` | 스프라이트에 HDR 배율을 곱해 블룸을 받게 하는 셰이더 |
| `Assets/Graphics/VFX/Resources/Spore/SporeExplosion_Tier1~3.png` | 단계별 색의 연막 스프라이트 시트(원본 `SporeExplosion.png`의 색만 교체) |
| `Assets/Graphics/VFX/Resources/Spore/SporeSmoke_Tier0~3.png` | 단계별 어두운 연막(스모크) 시트 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/SporeBigExplosionBuilder.cs` | 프리팹/머티리얼 재생성 메뉴 `LumberBoy/VFX/Build Spore Big Explosion` |

연막 시트나 색을 바꾸고 프리팹을 다시 맞출 때는 위 빌더 메뉴를 실행하면 프레임 연결과 머티리얼이 다시 맞춰집니다.

---

## 9. 이펙트 동작 요약(참고)

| 시각(초, 폭발 기준) | 연출 |
|---|---|
| -0.2 ~ 0 | 범위 안의 알갱이 300개가 한 점으로 빨려 들어감 + 압축 핵 |
| 0 ~ 0.1 | 십자 별 + 방사 점선 22가닥 |
| 0 ~ 0.65 | 연막(밝은 3~5장 + 스모크 2장)이 시차로 재생. 배치는 재생마다 무작위 |
| 0.02 ~ 0.7 | 충격파 고리 2겹 + 범위 윤곽 점선 |
| 0 ~ 1.0 | 구름/고속 알갱이(약 1,800개)가 퍼지고 낙하 중 공중에서 분해(바닥에 닿지 않음) |

픽셀 규칙(JAGGIES, DOUBLE PIXEL, HUGGING)을 코드 수준에서 지키며, 재생 중 힙 할당은 없습니다.

### 최적화 현황

| 항목 | 상태 |
|---|---|
| 구조체 배열 + 정적 공유 버퍼, 재생 중 힙 할당 없음 | 적용 |
| 화면 밖 갱신 생략(`PixelVfxCulling`) | 적용 |
| 같은 줄·같은 색 픽셀을 한 쿼드로 병합 | 적용(쿼드 약 8~11% 감소, 결과 픽셀 동일) |
| 메쉬 경계를 실제로 그린 픽셀 범위로 좁게 설정 | 적용(GPU 프러스텀 컬링용) |
| 반지름별 윤곽 표 지연 생성 + 정적 작업 배열 | 적용(첫 재생의 큰 할당 제거, 범위 윤곽만 미리 생성) |
| 연막 Transform/SpriteRenderer 세터를 값이 바뀔 때만 호출 | 적용 |
| 고리 그리기에서 안쪽 타원 구간 건너뛰기 | 적용 |
| 내용 해시로 업로드 생략 | 미적용(매 프레임 모양이 바뀌어 이득이 거의 없음) |
| 동시 재생 LOD(저부하 모드) | 미적용 |
| 풀 프리웜 | 미적용(`InDungeonVFXManager`의 프리웜 목록에 `SporeBigExplosionEffect` 추가 필요 — Application Layer) |

측정(에디터 프리뷰 씬, 리플렉션 호출 포함, 고정 시드):

| 구간 | 쿼드 전 → 후 | 재구성 ms 전 → 후 |
|---|---|---|
| 폭발 직전(0.12) | 1,581 → 1,359 | 0.094 → 0.093 |
| 폭발 후 0.1초(피크) | 4,337 → 3,844 | 0.479 → 0.466 |
| 폭발 후 0.16초 | 3,191 → 2,880 | 0.376 → 0.353 |
| 폭발 후 0.3초 | 2,772 → 2,467 | 0.411 → 0.389 |

전후 20개 프레임(단계 0, 2 x 10시각)의 렌더 결과는 **픽셀 단위로 동일**함을 확인했습니다. 동시 다발 재생과 실제 플레이 모드 비용은 측정하지 못했습니다.

---

## 10. 확인하지 못한 것

- 플레이 모드에서의 실제 재생(풀 동작, 정렬, 카메라 줌, 후처리 블룸 아래의 색감). 에디터 프리뷰 씬에서 실제 셰이더/정렬 레이어로 프레임만 확인했습니다.
- 동시 다발 재생 시 비용.
- 타원 스케일: 시안은 스크린샷에서 추정한 값(1유닛 ≈ 85px)을 기준으로 했으므로, 판정 범위와 눈으로 맞춰 보아야 합니다.
