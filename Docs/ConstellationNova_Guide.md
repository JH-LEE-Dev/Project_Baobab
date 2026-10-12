# 별자리 노바(마법진 + 별 폭발) 이펙트 인수인계 가이드

3스테이지 "별자리 발현"이 일어날 때 별자리 중심에서 재생되는 `VFX_ConstellationNova`의 구조, 기존 코드에 들어간 변경, 사용법과 주의점, 최적화 현황을 정리한 문서입니다. 다른 프로그래머나 에이전트가 이 이펙트를 다른 곳에 다시 적용하거나 수정할 때 기준으로 씁니다.

- 기준 커밋: `e17e16b9`(최초 구현), `a4013b28`(바닥/공중 분리, 시각 위계, 소멸 입자화, 도트 별 섬광/코어, 빛기둥 위치 수정까지 포함). 이 문서는 `a4013b28` 상태의 코드를 기준으로 합니다.
- 수치 중 "실측"이라고 적은 것만 측정값이고, 나머지는 코드에서 읽은 값이거나 추정입니다.
- 인게임에서 직접 확인하지 못한 항목은 8장에 따로 모았습니다.

관련 문서:

| 문서 | 내용 | 비고 |
|---|---|---|
| `Docs/VFX_Procedural_Pixel_Guide.md` | 절차적 픽셀 VFX 제작 방식, 요청 체크리스트, 최적화 가이드라인(10장) | 로컬 전용(`.git/info/exclude`)일 수 있음 |
| `Docs/DungeonVFXOptimization.md` | 던전 VFX 재생 경로 최적화(풀, 소팅 캐시, 공유 이미터) | 이 이펙트의 풀 규칙은 여기에 따름 |

---

## 1. 한눈에 보기

| 항목 | 내용 |
|---|---|
| 무엇 | 별자리 중심 바닥에 마법진이 그려지고, 별이 나선으로 휘감겨 마법진 밖까지 터지며, 마지막에 마법진이 `.` `+` `x` 입자로 풀려 올라가는 2.8초짜리 연출 |
| 언제 | 별 표식 나무 그룹을 전부 벌목해서 별자리가 발현될 때(기존 레이저와 같은 시점, 레이저는 그대로 유지) |
| 어디서 | 별자리 모서리 좌표의 중심(centroid) |
| 어떻게 | 스프라이트가 아니라 32 PPU 정수 격자에 맞춘 **절차적 쿼드 메쉬**를 매 프레임 다시 그림(`PixelQuadBuffer`) |
| 풀링 | `VFXComponent` 태그 풀 `ConstellationNovaEffect`(초기 2, 최대 4) |
| 피격 | **관여하지 않음.** 시각 연출만이며 데미지는 기존 `ConstellationBeamRoutine`이 처리 |

### 1.1 재생 흐름

```
Stage3TreeGenerationStrategySO.OnTreeDead            (그룹 마지막 별 표식 나무 벌목)
  -> InDungeonObjectManager.TriggerConstellationManifestation(groupId, positions)
       ("별자리 발현" 특성 bConstellationManifestUnlocked가 켜져 있어야 함)
  -> InDungeonVFXManager.BeginConstellationGroundMarkManifest(groupId)   (별 안착 -> 회전 -> 소멸 연출)
  -> ConstellationManifestReadyEvent
  -> InDungeonObjectManager.OnConstellationManifestReady(groupId)
       Sound.Play(StarExplosion, center)
       inDungeonVFXManager.PlayConstellationNovaVFX(center)      <-- 이번에 추가한 호출
       StartCoroutine(ConstellationBeamRoutine(...))             (기존 레이저, 변경 없음)
```

### 1.2 시간표 (`TimelineLength` 2.8초)

| 시각(초) | 연출 |
|---|---|
| 0.00 ~ 0.24 | 중앙 도트 별 섬광(4단계로 끊어서 줄어듦) + 점선 충격파 고리 |
| 0.12 ~ 0.90 | 마법진 4겹이 바깥에서 안쪽 순서로 그려짐 |
| 0.12 ~ 1.6 | 별 나선, 주인공 별, 방사 스파크, 불씨 |
| 0.70 ~ 1.70 | 빛기둥(9각 별 꼭짓점 9곳, 3곳 걸러 하나만 높음) |
| 1.60 ~ 2.50 | 마법진 분해(바깥 링부터 입자로 풀림) + 분해 시작 고리 |
| 2.50 ~ 2.80 | 별과 불씨 잔여 점멸 |
| 3.00 | 루트 ParticleSystem 수명 종료(풀 반환) |

---

## 2. 기존 스크립트를 건드린 곳 (이펙트 파일 외)

이펙트 자체(`VFX_ConstellationNova.cs`, 프리팹)를 제외하고 기존 파일을 바꾼 곳은 **3곳**입니다.

### 2.1 `InDungeonObjectManager.cs` — 호출 한 줄

파일: `Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs`
위치: `OnConstellationManifestReady`, `Sound.Play(SoundID.StarExplosion, ...)` 바로 아래

```csharp
// 레이저와 별개로, 같은 발현 시점에 별자리 중심에서 마법진 + 별 폭발 연출을 추가로 재생한다.
inDungeonVFXManager.PlayConstellationNovaVFX(constellationCenter);
```

- **왜:** 별자리 발현 시점은 이 함수에서만 확정됩니다(별이 모두 사라진 뒤 `ConstellationManifestReadyEvent`가 이 함수를 부름). 같은 함수 안에서 이미 `constellationCenter`를 구해 발현음을 재생하고 있어서, 그 좌표를 그대로 쓰는 것이 가장 변경이 작았습니다.
- **레이저는 건드리지 않았습니다.** `ConstellationBeamRoutine`, `ConstellationPixelLaser` 등은 그대로입니다.
- **한 번만 재생됩니다.** 별자리 잔상(`constellationHitCount`)으로 레이저가 여러 발 나가도 노바는 발현 1회당 1회입니다. 발현음과 같은 정책입니다.

### 2.2 `InDungeonVFXManager.cs` — 재생 메서드 추가

파일: `Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs`
위치: `PlayStarImpactExplosionVFX` 아래

```csharp
public void PlayConstellationNovaVFX(Vector3 _center, float _radius = 0.0f)
{
    if (vfxComponent == null) return;

    const string sortingLayerName = "FlyingItem";
    const int sortingOrder = 10;

    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "ConstellationNovaEffect", _center, Quaternion.identity,
        sortingLayerName, sortingOrder, null));

    if (effect == null || _radius <= 0.0f) return;

    // 풀 재생 직후(메쉬 시작은 첫 LateUpdate로 미뤄져 있다) 이번 재생의 범위를 덮어쓴다.
    PresentationLayer.VFX.VFX_ConstellationNova nova =
        effect.GetComponentInChildren<PresentationLayer.VFX.VFX_ConstellationNova>(true);
    if (nova != null) nova.SetRadius(_radius);
}
```

- **왜:** 이 프로젝트에서 던전 VFX는 로직 쪽(`InDungeonObjectManager` 등)이 `InDungeonVFXManager`의 `PlayXxxVFX` 메서드를 부르고, 그 메서드가 `vfxComponent.Play(new VFXPlaySettings(태그, 위치, 회전, 소팅, 부모))`로 풀에서 꺼내 재생하는 구조입니다(`PlayManifestationBrandStampVFX`, `PlayStarImpactExplosionVFX` 등과 같은 패턴). 노바도 같은 규약을 따랐습니다.
- **`parent = null`:** 나무와 분리해서 월드에 둡니다. 나무가 풀로 돌아가도 이펙트가 끊기지 않습니다.
- **소팅:** `FlyingItem`/10은 별자리 레이저와 같은 값입니다. 이 값은 **공중 메쉬**에만 적용되고, 바닥 메쉬는 스크립트가 자기 레이어(`ShockWave`)로 다시 맞춥니다(4.3).
- **`_radius`:** 0 이하이면 프리팹의 `maxRadius`(기본 4.0유닛)를 씁니다. 지금 호출부는 반지름을 넘기지 않습니다.
- **풀 태그 문자열** `"ConstellationNovaEffect"`는 `GameInstaller.prefab`의 풀 항목과 반드시 같아야 합니다. 다르면 `Play`가 null을 돌려주고 아무것도 나오지 않습니다(오류 로그 없음).

### 2.3 `GameInstaller.prefab` — 풀 항목 추가

파일: `Assets/Prefabs/Installer/Installer/GameInstaller.prefab`
대상: `VFXManager` 오브젝트의 `VFXComponent.vfxPoolDataList`

```yaml
- vfxTag: ConstellationNovaEffect
  effectPrefab: {fileID: ..., guid: 156668c4261dfcf4f8d00161acc67633, type: 3}   # VFX_ConstellationNova.prefab의 루트 ParticleSystem
  initialPoolSize: 2
  allowDynamicExpansion: 1
  maxPoolSize: 4
  uiParticleScale: 0
```

- **왜:** `VFXComponent`는 초기화 때 이 목록의 프리팹을 `initialPoolSize`만큼 미리 만들어 둡니다. 목록에 없으면 `Get`이 null을 돌려줍니다.
- **크기를 2/4로 잡은 이유:** 별자리 발현은 그룹당 1회이고 연출이 2.8초라서 동시 재생이 드뭅니다. 그룹이 여러 개 동시에 완성되는 경우만 고려했습니다.
- **풀이 가득 찬 경우:** 단발 이펙트 풀은 가장 오래 재생 중인 인스턴스를 회수해 재사용합니다(`VFXComponent.RecycleOldestActive`). 새 발현이 통째로 빠지는 대신 가장 오래된 노바가 조금 일찍 끊깁니다.

#### 부수 변경 (의도하지 않음)

같은 커밋의 `GameInstaller.prefab` diff에 `sharedBurstEmitterCapacity: 120`이 한 줄 같이 들어 있습니다. 프리팹을 코드로 다시 저장하면서 `InDungeonVFXManager`의 직렬화되지 않았던 필드가 기본값(120)으로 기록된 것이며 **동작은 같습니다.** 되돌리고 싶으면 그 한 줄만 지워도 됩니다(다음 저장 때 다시 생길 수 있음).

### 2.4 되돌리는 법 / 다른 곳에서 쓰는 법

| 하고 싶은 것 | 방법 |
|---|---|
| 노바를 끄기 | `OnConstellationManifestReady`의 호출 한 줄만 주석 처리. 풀 항목과 메서드는 그대로 둬도 무해 |
| 다른 상황에서 재생 | `inDungeonVFXManager.PlayConstellationNovaVFX(위치, 반지름)` 호출 |
| 완전히 제거 | 호출 한 줄, 메서드, `GameInstaller.prefab`의 풀 항목, 프리팹/스크립트 삭제 |

---

## 3. 이펙트 구성

### 3.1 파일

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_ConstellationNova.cs` | 이펙트 본체(메쉬 생성/갱신) |
| `Assets/Prefabs/VFX/Constellation/VFX_ConstellationNova.prefab` | 풀에서 쓰는 프리팹 |
| `Assets/Scripts/Presentation Layer/VFX/PixelQuadBuffer.cs` | 공용 정점 버퍼(기존) |
| `Assets/Shaders/VFX/BrandWrapGlow.shader` + `M_BrandStampBurst.mat` | 정점 알파 글로우 셰이더/머티리얼(기존 것 재사용, GlowMin 1.2 / GlowMax 6) |

### 3.2 프리팹 구조

```
VFX_ConstellationNova           (루트, 레이어 19) ParticleSystem + ParticleSystemRenderer
  ├─ NovaMesh                   MeshFilter + MeshRenderer + VFX_ConstellationNova   <- 공중 메쉬
  └─ GroundMesh                 MeshFilter + MeshRenderer                           <- 바닥 메쉬
```

- **루트 ParticleSystem:** 이펙트가 아니라 **풀 수명 타이머**입니다. duration 3.0, startLifetime 3.0, 루프 꺼짐, `stopAction = Callback`, 렌더 모드 `None`, 버스트로 입자 1개. 이 수명이 끝나면 `VFXComponent`가 풀로 되돌립니다. **`VFX_ConstellationNova.TotalDuration`(2.8초)보다 길어야 합니다.**
- **NovaMesh:** 별, 스파크, 불씨, 빛기둥, 중심 섬광, 풀려난 분해 입자. 소팅은 루트 렌더러(VFXComponent가 정한 값)를 따라갑니다.
- **GroundMesh:** 충격파, 마법진 4겹, 분해 시작 고리, 아직 풀리지 않은 마법진 픽셀. 소팅은 인스펙터의 `groundSortingLayerName`(`ShockWave`)과 `groundSortingOrder`(0)입니다.
- 두 메쉬는 같은 머티리얼을 쓰지만 **레이어가 달라서 합칠 수 없습니다.**

### 3.3 마법진 4겹과 연출 요소

| 요소 | 내용 |
|---|---|
| 충격파 | 점선 고리 하나가 0.4초 안에 `maxRadius`까지 퍼짐 |
| 0번 겹 | 바깥 2px 링(호 단위 틈 3곳, 고정) + 안쪽 점선 링 + 12개 눈금 |
| 1번 겹 | 9각 별 `{9/4}`(12fps 스텝으로 느리게 회전) |
| 2번 겹 | 룬 링(점선) + 금색 룬 6개 |
| 3번 겹 | 코어 링 + 7x7 / 5x5 도트 별(6Hz로 번갈아 맥동) |
| 시작 섬광 | 도트 별 4단계(팔 길이 12 → 9 → 6 → 3, 단계당 0.06초) |
| 별 | 나선팔 3개, `particleSpread`(기본 1.3)만큼 마법진 밖까지. 약 70개 중 주인공 별 8개만 크고 밝으며 꼬리를 남김 |
| 방사 스파크 | 충격파와 함께 달려나가는 큰 `x`/`+` 8개(꼬리 5점) |
| 불씨 | 약 22개, 오르면서 바깥쪽으로 퍼짐 |
| 빛기둥 | 9각 별 꼭짓점 9곳(3곳 걸러 하나만 높음), 끝에 금색 `+` |
| 소멸 | 샘플의 약 22%만 `.`(60%) `+`(25%) `x`(15%) 입자가 되어 떠오르고 나머지는 그 자리에서 사라짐. 바깥 링부터 안쪽으로 파도처럼 진행 |

### 3.4 인스펙터 파라미터 (`VFX_ConstellationNova`)

| 필드 | 기본값 | 설명 |
|---|---|---|
| `maxRadius` | 4.0 | 바깥 마법진 링 반지름(월드 유닛, 1유닛 = 32px). 피격 범위와 맞추는 기준 값 |
| `groundSquashY` | 0.5 | 바닥 타원 세로 비율(쿼터뷰). 히트 판정의 Y축 2배 보정과 같은 값 |
| `particleSpread` | 1.3 | 별/스파크가 퍼지는 최대 거리 배율(링 반지름 대비). 피격 범위와 무관 |
| `density` | 1.0 | 별, 주인공 별, 불씨, 스파크 개수에 곱하는 전체 배율(0.25~2). **과한 정도를 조절하는 스위치** |
| `starsPerUnit` | 17.5 | density 1 기준 반지름 1유닛당 별 수(최대 300) |
| `heroStarCount` | 8 | density 1 기준 주인공 별 수 |
| `embersPerUnit` | 5.5 | density 1 기준 반지름 1유닛당 불씨 수(최대 150) |
| `sparksPerUnit` | 2.0 | density 1 기준 반지름 1유닛당 스파크 수(최대 16) |
| `sortingOrderOffset` | 0 | 공중 메쉬를 루트 렌더러보다 이만큼 위에 그림 |
| `groundMeshFilter` / `groundMeshRenderer` | 연결됨 | 프리팹의 `GroundMesh`. 비어 있으면 전부 공중 메쉬 하나로 그림(4.3) |
| `groundSortingLayerName` / `groundSortingOrder` | `ShockWave` / 0 | 바닥 메쉬 소팅 |

시간, 발광, 모양 상수는 스크립트 상단의 `private const`입니다(인스펙터에 노출하지 않았습니다).

### 3.5 공개 API

| 멤버 | 설명 |
|---|---|
| `SetRadius(float units)` | 이번 재생의 반지름을 정함. **풀에서 꺼낸 직후, 첫 LateUpdate 전**에만 유효. 0 이하이면 `maxRadius` |
| `CurrentRadius` | 이번 재생의 실제 반지름 |
| `static TotalDuration` | 2.8초 |
| `EvaluateRadius(float elapsed)` | 경과 시간에서의 충격파 선두 반지름(월드 유닛). 0.4초에 `maxRadius`에 닿음. 피격 타이밍을 파동에 맞출 때 사용 |

---

## 4. 사용할 때 주의점

### 4.1 호출 타이밍과 반지름

- `SetRadius`는 `Play` 직후, 같은 프레임 안에서 부릅니다. 메쉬 시작은 첫 `LateUpdate`로 미뤄져 있어서 그 사이에 반지름이 반영됩니다. **다른 프레임에 부르면 이미 시작한 뒤라 무시됩니다.**
- `PlayConstellationNovaVFX`는 매 호출마다 `GetComponentInChildren`으로 노바 컴포넌트를 찾습니다. 호출 빈도가 낮아 문제없지만, 자주 부를 일이 생기면 풀 인스턴스 생성 시 캐시하는 쪽이 좋습니다(6장).

### 4.2 프리팹 직렬화 값이 스크립트 기본값을 덮어씁니다 (실제로 겪은 문제)

프리팹에 이미 저장된 `starsPerUnit` 등의 값은 스크립트의 새 기본값을 **무시**합니다. 스크립트 기본값만 바꾸고 프리팹을 확인하지 않으면 "바꿨는데 그대로"가 됩니다. 이번 작업에서도 프리팹에 이전 값(별 70/유닛 등)이 남아 있어, 개선 전 밀도로 캡처되는 일이 있었습니다.

- 파라미터를 바꿀 때는 **프리팹 인스펙터 값**을 같이 확인합니다.
- 스크립트에서 `[SerializeField]` 기본값을 바꾼 뒤에는 프리팹에 `Reset` 또는 값 직접 입력이 필요합니다.

### 4.3 소팅 — 바닥은 나무/캐릭터 뒤, 공중은 앞

| 레이어(오름차순) | 비고 |
|---|---|
| `…TreeShadow, ItemShadow` | |
| `ShockWave` (12) | **바닥 메쉬.** 도끼 충격파, 회오리(`WhirlwindVFX`)와 같은 레이어/오더 0 |
| `Objects` (13) | 나무, 캐릭터(SortingGroup, Y 기반 오더) |
| `FlyingItem` (14) | **공중 메쉬**, 별자리 레이저 |

- 나무와 캐릭터는 둘 다 `Objects`입니다. 노바 전체를 `FlyingItem`에 두면 마법진 선이 나무와 캐릭터를 덮습니다(이번에 겪은 문제).
- `VFXComponent.Play`는 자식 렌더러 **전체**에 같은 소팅을 덮어씁니다. 그래서 `VFX_ConstellationNova.LateUpdate`가 바닥 렌더러만 자기 레이어로 다시 맞춥니다. 프리팹에 렌더러를 더 추가하면 같은 문제가 생기므로 이 스크립트에서 소팅을 관리해야 합니다.
- `groundMeshRenderer` 참조가 비면 모든 것을 공중 메쉬 하나로 그립니다(폴백). 이 경우 나무를 덮습니다.
- 풀려난 분해 입자는 공중 메쉬로 나갑니다(나무 위로 떠오르는 것이 자연스러워서).

### 4.4 풀과 수명

- 루트 ParticleSystem의 duration은 `TotalDuration`(2.8초) 이상이어야 합니다. 짧게 바꾸면 연출 중간에 풀로 돌아갑니다. 길게 바꾸면 그만큼 풀 인스턴스를 점유합니다.
- 풀 크기 최대 4 이상으로 동시에 재생되면 가장 오래된 노바가 끊깁니다. 그룹을 동시에 많이 완성하는 빌드가 있다면 `maxPoolSize`를 올립니다.
- 반환 시 `OnDisable`에서 `overrideRadius`가 0으로 돌아가므로, 이전 재생의 반지름이 다음 재생에 남지 않습니다.

### 4.5 재생 중 시간

- 시간은 `Time.deltaTime`(스케일 시간)으로 흐릅니다. `timeScale`을 0으로 만들면(일시정지, 히트스톱) 같이 멈추고, 느리게 하면 같이 느려집니다.

### 4.6 시각 설정 의존성

| 의존 | 내용 | 바뀌면 |
|---|---|---|
| 블룸 | `Global Post Process Volume Profile 2`: 임계값 1.345, 강도 5. 톤매핑 **없음** | 임계값이 낮아지면 구조선도 번짐. 임계값이 높아지면 섬광/별 임팩트 감소 |
| 발광 알파 | 정점 알파 = 발광 세기(0~1). 배율 = `lerp(1.2, 6, alpha)` | 머티리얼 `GlowMin/Max`를 바꾸면 모든 요소가 같이 바뀜 |
| 구조선 발광 | `LineGlow` 0.07, `GroundGlow` 0.28, 섬광 흰색 0.12 | **블룸 임계값 근처로 낮춰야** 선이 흰색으로 날아가지 않음 |
| 불투명 | 셰이더가 출력 알파를 항상 1로 둠(알파는 발광 세기). 반투명 페이드가 없음 | 페이드가 필요하면 크기 단계나 점멸로 표현 |
| `ZTest Always` | 셰이더 설정 | 깊이와 무관하게 소팅 레이어 순서로만 가려짐 |

### 4.7 좌표

- 노바 중심은 별자리 점들의 평균입니다. `Stage3TreeGenerationStrategySO`는 별 표식 나무의 **나무 윗부분(`GetTopRootPosition`) 좌표**를 저장하므로, 평균도 나무 밑동이 아니라 윗부분 기준입니다. 마법진이 지면보다 약간 위(캐노피 오프셋만큼)에 놓일 수 있습니다. 보이는 위치가 어색하면 호출부에서 아래로 오프셋을 주거나 밑동 좌표를 쓰도록 바꾸는 것이 필요합니다(미확인, 8장).
- 모든 그림은 `Mathf.FloorToInt(pos * 32)`로 정수 픽셀에 스냅됩니다. 이펙트 루트의 회전/스케일은 identity라는 전제입니다(월드 -> 로컬 변환이 평행이동만 있는 빠른 경로 사용).

### 4.8 피격과의 관계

노바는 **시각 전용**입니다. `maxRadius`는 "눈에 보이는 범위"이고 실제 피격은 기존 레이저 스윕(`ConstellationBeamSweepRoutine`)이 처리합니다. 노바 범위로 피격을 하게 바꾸려면 `EvaluateRadius`로 파동 선두 반지름을 읽어 별도 판정을 만들어야 합니다. 이때 Y축 2배 보정(`groundSquashY` 0.5)을 같이 적용해야 합니다.

---

## 5. 렌더링 방식과 최적화 현황

### 5.1 이펙트 설계 방식 (한 줄 요약)

> 파티클을 시뮬레이션하지 않고, 매 프레임 그 순간의 그림을 **시간 `t`만으로 계산해** 픽셀 사각형으로 직접 그린다.

- **닫힌 식:** 별, 스파크, 불씨, 마법진의 위치는 전부 경과 시간에서 바로 계산합니다(적분 상태 없음). 꼬리도 과거 시각의 위치를 다시 계산해 그립니다. 프레임 속도와 무관하게 같은 모양이 나옵니다.
- **고정된 난수:** 재생 시작 때 시드(`Time.frameCount` + 위치)로 별/스파크/불씨의 속성을 한 번만 뽑습니다. 이후 프레임에서는 난수를 새로 뽑지 않습니다.
- **분해용 식별자:** 마법진은 계속 돌기 때문에 좌표 해시를 쓰면 입자가 깜빡입니다. 그리는 도형 번호(`plotSalt`)와 샘플 번호(`plotIndex`)로 해시해 입자마다 고정된 운명(남을지, 어떤 모양일지)을 줍니다.

### 5.2 적용된 최적화

| 항목 | 내용 |
|---|---|
| 정점 버퍼 | `PixelQuadBuffer`를 **정적(static)으로 공유**합니다(`skyBuffer`, `groundBuffer`, 각각 초기 용량 6000쿼드). 메인 스레드에서 한 번에 하나씩만 채우므로 안전합니다 |
| 업로드 | 위치+색 한 스트림(16바이트 정점)을 `SetVertexBufferData` 한 번으로 올리고, 쿼드 인덱스 패턴은 메쉬마다 최초 한 번만 채웁니다 |
| 가로 병합 | 같은 줄에서 이어지는 같은 색 픽셀을 하나의 쿼드로 합칩니다(`Plot`의 진행 중인 줄) |
| 쿼드 예산 | 두 버퍼의 합이 12000쿼드를 넘으면 더 그리지 않습니다(`QuadBudget`). 정점 6만 한도(UInt16 인덱스) 안입니다 |
| 힙 할당 0 | 정적 버퍼, 고정 크기 배열, 람다/LINQ 없음. 도트 별 패턴 문자열 배열도 정적으로 1회만 만듭니다 |
| 할당 없는 풀 | 메쉬는 인스턴스당 2개를 `Awake`에서 한 번만 만들고 재사용합니다 |
| 소팅 캐시 | 루트 렌더러의 소팅 값이 바뀔 때만 자식 렌더러에 씁니다(`appliedRootSorting*`, `OnEnable`에서 무효화) |
| 시작 지연 | `Begin`을 첫 `LateUpdate`로 미뤄 `SetRadius`가 반영될 시간을 줍니다 |
| 분해 픽셀 정리 | 풀리는 시점에 대부분의 샘플이 바로 사라져 입자 수를 억제합니다(쿼드 폭증 방지) |
| 도트 별 | 정수 격자 칸 배열로 정의해 쿼드를 최소로 씁니다 |

### 5.3 실측 (에디터, Mono, `RebuildMesh` 단독 호출)

측정 방법: 임시 하네스에서 `RebuildMesh`를 델리게이트로 직접 호출. 워밍업 3회 뒤 2.8초 구간을 0.05초 간격으로 56지점 x 5회(280회) 측정. 메쉬 생성/업로드를 포함하고 ParticleSystem, GPU, 렌더링 비용은 포함하지 않습니다.

| 반지름 | density | 평균 | 최대(1회 스파이크) | 최대 쿼드 | 최악 할당 |
|---|---|---|---|---|---|
| 2.0 | 1.0 | 0.146ms | 0.302ms | 1,445 | 0바이트 |
| 2.0 | 2.0 | 0.158ms | 0.407ms | 1,538 | 0바이트 |
| 4.0 | 1.0 | 0.356ms | 1.596ms | 2,740 | 0바이트 |
| 4.0 | 2.0 | 0.302ms | 0.650ms | 2,906 | 0바이트 |
| 6.0 | 1.0 | 0.417ms | 0.829ms | 4,076 | 0바이트 |
| 6.0 | 2.0 | 0.446ms | 1.021ms | 4,254 | 0바이트 |

- 최대값은 JIT/에디터 잡음이 섞인 단발 스파크일 수 있습니다(반복해도 같은 지점에서 나온다는 확인은 못 했습니다).
- 이전 과밀판(별 280개, 불씨 150개, 빛기둥 15개, 마법진 7겹)은 같은 방식으로 최대 7,764쿼드였고, 현재는 2,740쿼드(약 65% 감소)입니다.
- 에디터 Mono 기준이고 IL2CPP 빌드 수치는 측정하지 않았습니다.
- 인게임에서 동시에 여러 개가 재생될 때의 총 비용은 측정하지 않았습니다.

### 5.4 이 이펙트에 아직 적용하지 않은 것 (`VFX_Procedural_Pixel_Guide.md` 10장 기준)

가이드의 10.11 체크리스트와 대조하면 **빠진 항목**은 다음과 같습니다. 이 이펙트는 동시 재생이 거의 없고 비용이 작아 넣지 않았지만, 다른 이펙트와 규칙을 맞추려면 적용 후보입니다.

| 가이드 항목 | 현재 상태 | 이득 | 위험 |
|---|---|---|---|
| 내용 해시로 업로드 생략(10.2) | 매 프레임 올림 | 매 프레임 모양이 달라 이득이 작음. 정지 구간(마법진 유지 구간 일부)에서만 약간 | 낮음. 해시 비용 소량 |
| 메쉬 경계를 실제 범위로 좁힘(10.3) | `meshBoundsSize` 고정 대형 경계 | 렌더러 컬링이 실제 크기로 동작 | 중간. 입자가 경계 밖으로 나가면 컬링으로 사라질 수 있어 여유 필요 |
| `PixelVfxCulling`으로 화면 밖 재구성 생략(10.3) | 사용 안 함 | 화면 밖 노바(여러 그룹 동시)의 재구성 비용 제거 | 낮음 |
| `ReserveMesh`로 버퍼 사전 확보(10.2) | 사용 안 함 | 첫 재생 중 버퍼 재할당(네이티브) 방지 | 낮음. 메모리 소폭 증가 |
| 고정 발광 색 테이블(10.4) | 샘플마다 `GlowColor` 호출 | 샘플당 `Mathf.RoundToInt`/`Clamp` 반복 제거 | 낮음. 비트 동일 유지 가능 |
| 도형 샘플링 중복 제거(10.5) | 링을 반지름 x 1.3 밀도로 샘플링해 같은 픽셀을 여러 번 | 링 샘플 수 약 30% 감소 | 중간. 분해 입자 식별 번호가 바뀌어 결과 모양이 달라짐 |
| Transform/소팅 setter 비교(10.6) | 바닥 렌더러 소팅은 매 프레임 getter 비교 후 쓰기 | 이미 비교하고 있어 영향 없음 | - |

### 5.5 더 권장하는 최적화 (우선순위)

1. **`PixelVfxCulling` 적용 + 경계 축소**(가장 가성비): 노바는 화면 중앙에서 재생되는 경우가 대부분이지만, 그룹이 여러 개일 때 화면 밖의 노바가 매 프레임 재구성됩니다. `PixelVfxCulling.IsVisible(center, size)`로 건너뛰고 시간만 흘리게 하면 됩니다. 이때 경계는 `maxRadius x particleSpread` 기준으로 잡습니다.
2. **마법진 정적 구간 캐시:** 0.9~1.6초 사이는 마법진이 유지되고 12fps 스텝으로만 바뀝니다(코어 별 맥동도 12fps 스텝의 배수입니다). 바닥 메쉬를 스텝이 바뀔 때(초당 12번)만 다시 그리고 나머지 프레임은 그리기와 업로드를 생략하면, 60fps 기준 이 구간에서 바닥 메쉬 비용이 약 1/5로 줄어듭니다. 공중 메쉬는 계속 갱신합니다. 시각 결과는 동일합니다. 분해 구간은 매 프레임 달라지므로 제외합니다. **바닥 메쉬와 공중 메쉬의 비용 비중은 아직 나눠서 재지 않았으므로**, 먼저 `DrawShockwave`/`DrawMagicCircle`과 공중 요소의 시간을 나눠 측정해 이득이 있는지 확인합니다.
3. **샘플 중복 제거:** 링 샘플링 간격을 1.0px로 맞추고 인접 중복을 건너뜁니다. 결과 모양이 소폭 달라지므로 승인 후 적용합니다.
4. **`GetComponentInChildren` 캐시:** 풀 인스턴스 생성 시 노바 컴포넌트를 `Dictionary<ParticleSystem, VFX_ConstellationNova>`로 캐시해 호출마다 탐색을 없앱니다.
5. **저사양 프리셋:** 설정이나 품질 단계에서 `density`를 0.5 이하로 낮추는 경로. 연출 자체가 줄어드는 방법이라 요청이 있을 때만 적용합니다.

### 5.6 권장하지 않는 방식

| 방식 | 이유 |
|---|---|
| 스프라이트 시트/플립북 베이크 | 반지름 가변, 별 위치의 매 재생 난수, 12fps 스텝 회전과 맞지 않고 메모리를 많이 먹음 |
| ParticleSystem으로 별/불씨 재구현 | 도트 격자 스냅과 픽셀 단위 분해를 맞추기 어렵고, 개수가 적어(약 70개) 이득이 없음 |
| 바닥/공중 메쉬 합치기 | 소팅 레이어가 달라 합치면 가림 문제가 되돌아옴(4.3) |
| 마법진을 셰이더로 그리기 | 분해 입자화와 도트 단위 제어가 어려워짐. 정적 마법진만 필요하면 고려 가능하지만 현재 구조를 크게 바꿔야 함 |

---

## 6. 튜닝 가이드

| 증상 | 조절 |
|---|---|
| 너무 과하다 / 정신없다 | `density`를 0.5~0.7로 낮춤. 주인공 별과 불씨가 가장 먼저 줄어듦 |
| 너무 소박하다 | `density` 1.3~1.5 또는 `heroStarCount` 증가 |
| 범위를 피격 범위에 맞추고 싶다 | `maxRadius`(또는 호출 시 `_radius`). 마법진, 별, 기둥, 불씨가 모두 비례해 변함 |
| 별이 마법진 밖으로 너무 멀리 나간다 | `particleSpread`를 1.0~1.15로 |
| 마법진이 흐릿하다 / 선이 흰색으로 날아간다 | 구조선 발광(`LineGlow`)을 블룸 임계값 근처로 낮춤(코드 상수) |
| 임팩트가 약하다 | 시작 섬광 발광(`DrawCore`의 흰색 0.12)을 올림. 올리면 블룸이 도트 윤곽을 덮으므로 0.2 이하 권장 |
| 소멸이 성기다 / 빽빽하다 | `DissolveSpawnRate`(기본 0.22) |
| 소멸 입자 모양 | 모양 비율(`ResolveDissolve`의 0.6 / 0.85 경계), 모양 유지 구간 `DissolveShapeEnd` |
| 나무/캐릭터를 가린다 | 바닥 메쉬 레이어 확인(`groundSortingLayerName`), `groundMeshRenderer` 참조 확인 |

---

## 7. 검증 방법

이 이펙트를 고친 뒤 확인하는 순서입니다(이번 작업에서 쓴 방식).

1. **컴파일/콘솔:** 오류와 경고 0개.
2. **빈 씬 캡처(프레임 단위):**
   - Unity MCP `RunCommand`는 `System.Reflection`과 일부 네임스페이스를 막으므로, 플레이 모드에서 도는 **임시 MonoBehaviour**(`Assets/Scripts/Temp_*.cs`)를 만들어 쓴다.
   - 빈 씬에서 프리팹을 `Instantiate` -> `SetRadius` -> `ParticleSystem.Play(true)`, `Time.captureFramerate = 20`으로 프레임을 고정한다.
   - 카메라는 `UniversalAdditionalCameraData.renderPostProcessing = true`, 프로젝트의 `Global Post Process Volume Profile 2`를 글로벌 Volume으로 붙인다. 블룸이 없으면 모든 선이 흰색으로 보여 판단이 틀어진다.
   - `WaitForEndOfFrame` 이후 RenderTexture(ARGBHalf)를 `ReadPixels`해 PNG로 저장한다.
3. **정렬 검증:** Tree/Character 프리팹의 `Objects` 레이어 스프라이트를 같은 소팅 값으로 복제해 빈 씬에 세우고, 마법진이 뒤로 가는지 확인한다.
4. **쿼드 수/할당:** 자식 `MeshFilter`의 서브메쉬 `indexCount / 6`을 합산해 최대 쿼드를 보고, `RebuildMesh`를 델리게이트로 직접 불러 `GC.GetAllocatedBytesForCurrentThread()` 차이를 잰다. **에디터 전체 GC 수치(`GC Allocated In Frame`)는 에디터 잡음이 커서 근거로 쓰지 않는다.**
5. **풀 반환:** 루트 ParticleSystem이 수명 후 죽는지(`IsAlive`)와 쿼드가 0이 되는지 확인한다.
6. **정리:** 임시 스크립트 삭제, 씬 복구, 콘솔 확인, 저장 폴더 목록 전후 비교.

주의: 플레이 모드 캡처는 씬을 바꾸므로, 작업 후 원래 씬(`MainMenuScene`)을 다시 열고 dirty 상태가 아닌지 확인합니다.

---

## 8. 미검증 사항과 알려진 한계

| 항목 | 상태 |
|---|---|
| 인게임에서 타일/장식물과 `ShockWave` 레이어의 겹침 | 미확인. 빈 씬에서 `Objects` 레이어 스프라이트와의 관계만 확인 |
| 별자리 레이저와 동시 재생 시 겹침 | 미확인 |
| 노바 중심이 나무 윗부분 좌표라는 점의 시각적 영향(4.7) | 미확인 |
| 동시에 여러 그룹이 완성될 때의 총 비용, 풀 4개 초과 시 체감 | 미측정 |
| IL2CPP 빌드, 저사양 기기 비용 | 미측정 |
| 주인공 별, 룬 `+`, 빛기둥 끝 `+`의 도트 느낌 | 시작 섬광과 코어 별만 굵기가 있는 도트 별로 바꿨고, 이 세 곳은 아직 1px 십자(사용자 지적에서 "픽셀 느낌이 약하다"고 한 것과 같은 원인일 수 있음) |
| 소멸 구간 중심부의 입자 겹침 | 코어 주변은 입자가 밀집해 짧은 가로선처럼 보일 수 있음(블룸 영향) |

---

## 9. 디자인 변천 요약

같은 실수를 반복하지 않기 위해 사용자 피드백과 대응을 남깁니다.

| 순서 | 피드백 | 대응 |
|---|---|---|
| 1 | 이음선이 거의 한 개뿐이라 마법진 같지 않다. 천체 느낌 | 마법진을 여러 겹(동심원, 별 다각형, 눈금 링)으로 만들고 은하 나선 별을 추가 |
| 2 | 심심하다. 레퍼런스의 마법진 느낌을 살리고 더 화려하게 | 빛기둥, 상승 링, 불씨, 방사 스파크 추가 |
| 3 | 파티클을 더 바깥으로 팡 퍼지게, 사라질 때 너무 정적 | 확산 곡선 quintic, `particleSpread`, 분해를 가속 상승 + 소용돌이로 |
| 4 | 너무 과해서 AI 느낌 | **밀도와 위계를 줄임**: 마법진 7겹 -> 4겹, 별 약 220 -> 70(주인공 8), 빛기둥 15 -> 6 -> 9(높이 위계), 스텝 회전, `density` 스위치 |
| 5 | 이펙트가 나무와 캐릭터를 가린다 | 바닥/공중 메쉬 분리, 바닥은 `ShockWave` 레이어 |
| 6 | 소멸이 `-` 모양으로 찢어져 픽셀 느낌이 아니다 | 4샘플 묶음 제거, 샘플 단위 독립 입자(`.` `+` `x`) |
| 7 | 시작 `+`와 코어 `+`에 픽셀 느낌이 없다 | 굵기가 변하는 도트 별 4단계 섬광, 7x7/5x5 도트 코어 별 |
| 8 | 꼭짓점 사이에도 기둥이 있다 | 기둥 6개(60° 간격)와 9각 별(40°)의 개수 불일치였음. 같은 상수(`StarPolygonPoints`)로 통일 |

배운 점:

- 1px 선은 도트가 아니라 벡터 선으로 읽힙니다. 도트 느낌이 필요한 곳은 굵기가 있는 계단식 모양으로 그립니다.
- 블룸이 강한 프로젝트라 발광을 높이면 도트 윤곽이 먼저 사라집니다. 구조선은 임계값 근처로 낮춥니다.
- 요소를 늘리기보다 **위계(크고 밝은 몇 개 + 어둡고 작은 다수)** 를 만드는 쪽이 "의도된 연출"처럼 보입니다.
- 도형 개수와 각도를 코드 두 곳에서 따로 쓰면 어긋납니다(`StarPolygonPoints`처럼 공통 상수로).

---

## 10. 다른 프로그래머/에이전트에게 넘길 때

### 10.1 체크리스트

1. 이 문서와 `VFX_Procedural_Pixel_Guide.md` 10장을 읽었는가.
2. 수정 전 기준 캡처(7장)를 떴는가. 결과를 바꾸는 최적화는 계획표에 등급과 함께 적고 승인받는가.
3. 파라미터를 바꿀 때 프리팹 직렬화 값(4.2)을 확인했는가.
4. 소팅 레이어 구조(4.3)를 바꾸지 않았는가.
5. 힙 할당 0, LINQ/람다 금지, 컬렉션 재사용(CLAUDE.md 5장)을 지켰는가.
6. 임시 스크립트와 플래그 파일을 정리했는가.

### 10.2 작업 요청 예시

```
Docs/ConstellationNova_Guide.md를 먼저 읽고 작업해 줘.
- 목표: (예) 별자리 노바를 TownScene의 ○○ 연출에서 재사용한다.
- 호출 경로: InDungeonVFXManager.PlayConstellationNovaVFX 패턴을 따라 새 PlayXxxVFX를 만들고, 풀 태그는 기존 ConstellationNovaEffect를 공유한다.
- 소팅: 바닥 메쉬 ShockWave/0, 공중 메쉬는 호출 측이 지정한다.
- 금지: VFX_ConstellationNova의 시각 결과(위치, 색, 모양, 타이밍)를 바꾸는 최적화는 계획표에 등급을 적고 승인 전에는 하지 않는다.
- 검증: 7장 절차(빈 씬 캡처, 정렬 검증, 쿼드/할당 측정)를 수행하고 결과를 보고한다.
```

### 10.3 자주 나오는 질문

| 질문 | 답 |
|---|---|
| 노바를 다른 풀 태그로 쓰고 싶다 | `GameInstaller.prefab`에 같은 프리팹으로 새 항목을 추가하고 `PlayConstellationNovaVFX`와 같은 방식으로 태그만 바꿈 |
| 반지름을 레이저 폭에 맞추고 싶다 | `PlayConstellationNovaVFX(center, 반지름)` 인자로 넘김. 마법진, 별, 기둥이 모두 비례해 변함 |
| 노바 범위로 데미지를 주고 싶다 | `EvaluateRadius(elapsed)`로 파동 선두를 읽어 별도 판정을 작성. 현재는 시각 전용 |
| 색을 바꾸고 싶다 | 팔레트는 흰색/하늘색/파랑/금색 4색(`ColorWhite/Ice/Blue/Gold`). 다른 이펙트(각인 폭발, 별 감쌈)와 같은 계열이므로 맞춰서 바꿔야 함 |
| 길이를 바꾸고 싶다 | `TimelineLength`와 루트 ParticleSystem duration을 함께 바꿈. 분해 시작(`DissolveStart`), 빛기둥 끝(`PillarEnd`)도 같이 확인 |
