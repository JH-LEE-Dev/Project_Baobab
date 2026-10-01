# 던전 VFX 최적화 가이드

이 문서는 던전(숲)에서 돌아가는 VFX의 비용 구조, 지금까지 적용한 최적화의 **구현 방식과 전제 조건**, 그리고 보류한 항목과 그 이유를 정리한 것입니다. 새 이펙트를 추가하거나 기존 이펙트를 손볼 때 이 문서의 7장(체크리스트)과 8장(보류 항목)을 먼저 보세요.

- 기준 시점: 2026-10-01 ~ 10-02 최적화 작업 (커밋 `1차 최적화`, `1차 최적화 종료` 이후의 VFX 구조 변경 포함)
- 적용 원칙: **보이는 결과(위치·색·모양·타이밍)를 바꾸지 않는다.** 결과가 달라지는 항목은 8장에 "보류"로 남겼습니다.
- 검증 수준: `dotnet build` 컴파일, 독립 코드 리뷰(에이전트) 2~3회, 공유 이미터는 편집 모드에서 입자 수·위치를 실측했습니다. 플레이 모드 전체 회귀는 사람이 확인해야 합니다.

---

## 1. 던전 VFX 파이프라인 한눈에 보기

| 구성 요소 | 역할 | 파일 |
|---|---|---|
| `VFXComponent` | 태그별 ParticleSystem 풀. `Get/Play/Stop`, 소팅·색 오버라이드, 풀 상한·최오래된 인스턴스 회수 | `Presentation Layer/ComponentSystem/VFXComponent/VFXComponent.cs` |
| `VFXPoolInstanceHelper` | 풀 인스턴스 1개마다 붙는 헬퍼. 반납, 지연 재부모화, 자식 캐시, 소팅 캐시 | 같은 파일 |
| `InDungeonVFXManager` | 던전 VFX 진입점. 나무 피격/사망/포자막/열기/별자리/낙인 등 재생 요청을 받아 풀 또는 공유 이미터로 보냄 | `Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs` |
| `SharedBurstEmitter` | 단발 버스트 이펙트를 인스턴스 하나로 공유 재생 (3장) | `Presentation Layer/VFX/SharedBurstEmitter.cs` |
| `TreeFlashSystem` | 나무 피격/성장 플래시를 한 곳에서 틱 (4장) | `Presentation Layer/ComponentSystem/TreeVisualComponent/TreeFlashSystem.cs` |
| 절차적 픽셀 VFX | `VFX_TreeBurn`, `VFX_TreeHeatBurst`, `VFX_OverheatAura`, `VFX_LaserHit`, `VFX_ChargeVortex`, `VFX_BrandStampBurst` 등. `PixelQuadBuffer`로 매 프레임 메쉬를 다시 만든다 | `Presentation Layer/VFX/*` |
| 과열 충격파 입자 | `OverheatShockWaveParticleRunner`. SpriteRenderer 58개를 직접 움직인다 | `Presentation Layer/ComponentSystem/ArmComponent/Axe/OverheatShockWaveVisualComponent.cs` |

이펙트가 **어디서 비용이 느는지**는 두 축으로 나뉩니다.

1. **재생 횟수에 비례하는 비용**: 풀 인스턴스 활성화(`SetParent`/`SetActive`/`Play`), 자식 탐색(`GetComponentsInChildren`), 코루틴·트윈 생성. 광역 공격으로 한 프레임에 수십 그루가 맞으면 그대로 스파이크가 됩니다.
2. **활성 수에 비례하는 매 프레임 비용**: 메쉬 재구성(화상·열기), 렌더러별 소팅/트랜스폼 세터, MPB 갱신.

---

## 2. 적용한 최적화 목록

### 2.1 VFXComponent 풀 (재생마다 들던 비용 제거)

| 항목 | 이전 | 이후 |
|---|---|---|
| 헬퍼 조회 | `Get/Play/Stop/StopAll/Clear`마다 `GetComponent<VFXPoolInstanceHelper>` | `helperLookup`(Dictionary) 조회. 풀 밖 인스턴스는 예전처럼 `GetComponent` 폴백 |
| `Stop`의 소속 확인 | `masterList.Contains` 선형 탐색 | 조회표 hit면 생략 |
| 자식 ParticleSystem | `Play`마다 `GetComponentsInChildren` 배열 할당 | 헬퍼가 `Initialize`에서 1회 수집 (`ChildSystems`) |
| 자식 Renderer | `SetSortingSettings`마다 배열 할당 | 헬퍼가 **첫 활성화 뒤 첫 사용 시** 1회 수집 (`GetChildRenderers`). 일부 VFX 스크립트가 `Awake`에서 메쉬 자식을 만들기 때문에 활성화 전에는 수집하지 않는다 |
| 소팅 재적용 | 매 호출 렌더러 전체에 대입 | 마지막 적용값과 같으면 생략. `MarkPlayed`(재생)·`ReturnToPool`(반납)에서 무효화 |
| 소팅 레이어 지정 | `sortingLayerName` 문자열 (게터가 호출마다 string 할당) | `SetSortingSettings(ParticleSystem, int layerID, int order)` 오버로드 추가. 매 프레임 호출부(`LogItem` Shiny, 발소리 먼지)는 ID 사용 |
| 정지 코루틴 | `GetComponentsInChildren` + `new WaitForSeconds` | 자식 캐시 + 대기 객체 재사용 |

**소팅 캐시의 전제**: 같은 인스턴스의 렌더러 소팅을 `VFXComponent` 밖에서 직접 쓰는 코드가 `SetSortingSettings` 반복 호출 사이에 끼어들면 안 됩니다. 현재 그런 조합은 없습니다(드론 과열 아우라·충전 VFX는 직접 쓰지만 `SetSortingSettings`를 다시 부르지 않음). 새로 그런 코드를 넣는다면 `MarkPlayed`를 거치거나 캐시를 무효화해야 합니다.

### 2.2 절차적 픽셀 VFX (매 프레임 비용)

| 파일 | 변경 |
|---|---|
| `VFX_OverheatAura`, `VFX_LaserHit`, `VFX_ChargeVortex`, `VFX_BrandStampBurst` | 루트 ParticleSystemRenderer의 (layer, order)가 바뀐 프레임에만 메쉬 렌더러 소팅을 다시 쓴다. `OnEnable`에서 캐시 무효화 — 풀 재생 시 `VFXComponent.ApplySortingSettings`가 자식 소팅을 덮어쓰기 때문 |
| `VFX_OverheatAura` | 소스 소팅 레이어 이름을 `SortingLayer.NameToID`로 1회 변환. `OverheatSilhouette.CollectSources(Transform, int)` 오버로드로 렌더러별 문자열 비교 제거 |
| `ConstellationPixelLaser` | 도착 값은 `StageArrival`로 블록에만 적고, 프레임 끝 `FlushStagedArrivals`에서 세그먼트당 1회만 `SetPropertyBlock` |
| `OverheatShockWaveParticleRunner` | 입자에 `Transform` 캐싱, 머티리얼/소팅은 `Play`에서 풀 전체에 1회, `propertyBlock.Clear()` 제거(블록에 `_TintColor` 하나만 씀) |
| `ItemAuraOrbitController` | 위성 `Transform`·중앙 글로우 렌더러 캐싱, `RebaseSortingOrder` 같은 값이면 생략, 그라데이션 캐시는 원본/배율이 바뀔 때만 재생성(에디터 `OnValidate`는 강제 재생성) |
| `AnimatedObj`, `StaticObj`, `DecoSpritePatternAnimator` | `MaterialPropertyBlock` static 공유 (Get→Set 복사 방식이라 안전). **반드시 지연 생성**(`sharedMpb ??= new ...`)으로 둔다. `static readonly` 필드 초기화로 만들면 타입이 MonoBehaviour 생성 중에 처음 쓰일 때 정적 생성자가 돌고, Unity가 그 문맥에서 `MaterialPropertyBlock` 생성을 금지해(`CreateImpl is not allowed ... from a MonoBehaviour constructor`) 타입 초기화가 통째로 실패한다. 실제로 한 번 겪은 버그다 |

### 2.3 드론 / 충격파 재생 경로

| 파일 | 변경 |
|---|---|
| `Drone` | 충전 VFX 인스턴스별 (렌더러 배열, `VFX_ChargeVortex`, 최대 수명)과 피격 VFX의 `VFX_LaserHit`을 Dictionary로 1회 조회 |
| `ShockWaveVisualComponent`, `OverheatShockWaveVisualComponent` | 반환 콜백 정적 델리게이트 캐싱 (C# 9에서는 정적 메서드 그룹도 변환마다 할당) |
| `AttackComponent` | 충격파 생성 지연 `WaitForSeconds`·"충격파만 적중" 델리게이트 캐싱 |

### 2.4 나무 피격 피드백

- **플래시**: 나무마다 코루틴 → `TreeFlashSystem` 중앙 틱 (4장).
- **펀치**: `DOPunchPosition`에 `SetRecyclable(true)`. 참조를 들고 있지 않고 `visualRoot.DOKill()`로만 정리하므로 재활용 트윈을 잘못 죽일 일이 없다.
- **피격/사망 파티클**: 풀 → `SharedBurstEmitter` (3장).

---

## 3. SharedBurstEmitter — 단발 버스트 이펙트 공유 재생

### 3.1 왜 필요한가

`TreeHitEffect_Top/Bottom`, `TreeDeadEffect_Top/Bottom`은 타격마다 두 번 재생됩니다. 풀 방식은 재생마다 `SetParent → SetActive(true) → Play`를 하고, 활성 인스턴스마다 드로우콜이 따로 나갑니다(ParticleSystem은 SRP 배처를 타지 않음). 과열 충격파로 30그루가 한 프레임에 맞으면 활성화 60회만으로 1~3ms 스파이크가 납니다.

공유 이미터는 프리팹을 **한 번만** 인스턴스화해 두고, 재생 요청마다 `ParticleSystem.Emit(EmitParams, count)`로 입자만 넣습니다. 활성화 비용이 0이고 드로우콜은 이펙트 종류당 1개입니다.

### 3.2 구현 방식

```
생성 (InDungeonVFXManager.Initialize → CreateSharedBurstEmitter)
  1. VFXComponent.TryGetPoolConfig(tag)로 프리팹과 풀 상한을 얻는다
  2. SharedBurstEmitter.IsCompatible(prefab)이 false면 null → 호출부는 예전 풀 경로를 쓴다
  3. Instantiate(prefab, 원점, identity) — 부모 없음, 플레이 중이면 DontDestroyOnLoad
  4. 시스템마다: Stop(StopEmittingAndClear) → playOnAwake=false, loop=true, stopAction=None,
     cullingMode=AlwaysSimulate, maxParticles=max(원래값, 버스트개수×용량), emission.enabled=false → Play(false)

재생 (Emit)
  1. 요청 회전이 루트 회전(identity)과 다르면 false 반환 → 호출부가 풀 경로로 재생
  2. 시스템마다 startColor 덮어쓰기(루트: overrideColor, 자식: overrideColor && overrideChildrenColor)
  3. EmitParams.position = 요청 위치 + 자식 로컬 오프셋 (Local 공간이면 InverseTransformPoint),
     applyShapeToPosition = true → Emit(params, 버스트 개수)

정리
  - StopAllPooledVFX: Clear(false)로 살아있는 입자 제거 (풀의 StopAll과 같은 시점)
  - InDungeonVFXManager.OnDestroy: Dispose (루트 파괴)
```

**핵심 전제 — 루트는 절대 움직이지 않는다.** 네 프리팹의 시뮬레이션 공간이 **Local**이라, 루트를 타격마다 옮기면 이미 나간 입자까지 따라 움직입니다. 그래서 위치는 트랜스폼이 아니라 `EmitParams.position`으로 넣습니다. 루트가 원점·identity·스케일 1에 고정이면 Local 좌표 = 월드 좌표라서, 예전에 풀 인스턴스가 나무 위치에 놓여 identity로 재생되던 것과 같은 결과가 됩니다.

### 3.3 호환 조건 (`IsCompatible`)

프리팹의 **모든** ParticleSystem이 아래를 만족해야 합니다. 하나라도 어긋나면 풀 경로로 자동 폴백됩니다.

| 조건 | 이유 |
|---|---|
| `loop == false` | 루프는 Emit으로 재현 불가 |
| 시뮬레이션 공간이 Custom이 아님 | Custom은 외부 트랜스폼을 따라감 |
| World 공간 자식의 로컬 오프셋이 0 | 네이티브 Emit이 Shape 위치(자식 위치 포함)에 position을 더해 오프셋이 이중 가산될 수 있음 |
| `startDelay`가 0 (Constant 0 또는 TwoConstants 0~0) | 지연은 스케줄러 없이 재현 불가 |
| 서브이미터·트레일·속도 상속 꺼짐 | Emit 입자에 적용되는 방식이 Play와 다를 수 있음 |
| `rateOverDistance` 0 | 루트가 움직이지 않으므로 의미 없음 |
| `rateOverTime` Constant이고, `rate × duration < 1` | 풀 경로도 누적기가 0에서 시작해 duration 안에 입자 1개를 못 만든다. 그 이상이면 거부 |
| 버스트 전부 `time 0`, `cycleCount 1`, `probability 1`, 개수 Constant | 그대로 Emit 개수로 치환 |

현재 네 프리팹의 실측값: 시스템 2/3/2/4개, 버스트 개수 100+10 / 30+1+1 / 100+30 / 200+3+5+1, 자식 오프셋 0, 스케일 1, 소팅 Objects/3.

### 3.4 용량과 알려진 차이

- 용량은 `InDungeonVFXManager.sharedBurstEmitterCapacity`(기본 **120**)와 풀 상한(70) 중 큰 값입니다. 시스템별 `maxParticles = 버스트 개수 × 용량`.
- **유일한 동작 차이**: 같은 태그가 용량을 넘겨 동시에 겹칠 때. 예전 풀은 가장 오래된 이펙트 전체를 잘라 새 것을 보여줬고, 공유 이미터는 초과하는 새 입자만 누락됩니다. 용량 미만에서는 동일합니다.
- `rateOverTime 10 × 0.05s` 류는 재생 프레임에 0.1초 이상 끊기는 극단 상황에서만 예전 경로가 입자 1개를 더 낼 수 있습니다.
- 메모리: 네 태그 버스트 합 약 480개 × 120 ≈ 5.8만 입자 분량(최대 6~9MB). Unity는 쓰인 만큼 버퍼를 키우므로 평소엔 훨씬 적습니다.

### 3.5 검증 방법 (편집 모드, Unity MCP `RunCommand` 또는 에디터 스크립트)

```csharp
ParticleSystem prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
bool ok = PresentationLayer.VFX.SharedBurstEmitter.IsCompatible(prefab);
var shared = new PresentationLayer.VFX.SharedBurstEmitter(prefab, 70, "Test_Shared");
shared.Emit(target, Quaternion.identity, false, new ParticleSystem.MinMaxGradient(Color.white), false);
// 각 시스템 GetParticles → 개수가 버스트 개수와 같고, 평균 위치(Local이면 TransformPoint)가 target 근처인지 확인
shared.Dispose(); // 편집 모드에서는 DestroyImmediate로 동작
```

편집 모드에서는 `DontDestroyOnLoad`를 걸지 않도록 `Application.isPlaying` 가드가 있습니다. 테스트 오브젝트 이름이 `Test_Shared`면 씬에 남지 않았는지 확인하세요.

### 3.6 참고: 남아 있는 풀 예열

공유 경로가 켜져 있어도 GameInstaller 프리팹의 네 태그 풀은 `initialPoolSize 20`씩 예열합니다. 회전 불일치 폴백 외에는 쓰이지 않으므로 2~4개로 줄여도 동작은 같습니다(폴백이 필요하면 동적 확장이 70까지 채움).

---

## 4. TreeFlashSystem — 플래시 중앙 처리

### 4.1 등가성 규칙

예전 코루틴(`FlashRoutine`)과 **프레임별 적용 값이 같아야** 합니다. 코루틴은 `StartCoroutine`이 첫 `MoveNext`를 그 자리에서 실행했으므로:

```
등록 프레임 (Begin):   curve(0) 적용, elapsed = Time.deltaTime, registeredFrame = Time.frameCount
이후 프레임 (LateUpdate Tick):
   registeredFrame == 현재 프레임이면 건너뜀
   elapsed < duration → curve(elapsed / duration) 적용 후 elapsed += deltaTime
   아니면            → 0 적용 후 제거
duration <= 0        → 즉시 0 적용, 등록 안 함 (코루틴의 while 미진입과 동일)
나무 비활성/파괴      → 적용 없이 제거 (Unity가 코루틴을 조용히 끊던 것과 동일)
ResetVisualState     → Cancel (StopCoroutine과 동일) 후 기존대로 0 적용
```

`TreeVisualComponent.ApplyFlashAmountFromSystem`이 예전 `ApplyFlashAmountToRenderers` 호출과 같은 일을 합니다.

### 4.2 드라이버 생명주기

- 첫 `Begin`에서 숨김 GameObject(`HideInHierarchy`) + `DontDestroyOnLoad`로 생성. 마을 나무도 같은 드라이버가 처리합니다.
- `HideAndDontSave`는 쓰지 않습니다. 플레이 종료 뒤에도 남아 다음 세션의 새 드라이버와 함께 틱이 두 번 돌 수 있기 때문입니다.
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`에서 목록과 드라이버 참조를 비워 "도메인 리로드 끔" 설정에도 안전합니다. 드라이버 `Awake`는 중복 생성 시 스스로 파괴되고, `LateUpdate`는 `driver == this`일 때만 틱합니다.

---

## 5. 다른 보조 변경 (던전 외 포함, 요약)

VFX와 직접 관련은 없지만 같은 작업에서 들어간 것들입니다. 상세는 커밋 diff를 보세요.

- 나무 풀 Get/Release의 이벤트 핸들러 델리게이트 캐싱, `TreeStatDataBase` 클로저 제거, `SetConstellationMarkActive` 변화 없을 때 HDR 재적용 생략, 렌더러 `DOKill` 8회 제거.
- 화상 틱·도끼 쿨다운·충격파 지연·전송 간격의 `WaitForSeconds` 캐싱, 도끼 스윙 트윈 재활용.
- 그림자 판정을 활성 나무 전체 순회에서 `CollisionSystem` 반경 질의로(보수적 반경으로 결과 동일).
- 초기 스폰의 충돌/데코 타일 쓰기를 `SetTiles` 배치로. 맵 재생성 시 보류분 폐기, 코루틴 중단 시 명시적 종료.
- 블룸 피처 `camera.name` 캐싱, 데코 애니메이터 대기 객체 캐싱, 원목 소팅 동기화의 문자열 할당 제거.

---

## 6. 측정 가이드

"더 최적화할 게 있나"는 측정으로 답해야 합니다. 권장 시나리오와 볼 항목:

| 시나리오 | 프로파일러에서 볼 것 |
|---|---|
| 과열 충격파로 40그루 동시 타격 | `ParticleSystem.Update`, `SetActive`(폴백이 도는지), `DOTween.Update`(펀치 트윈), `SetPropertyBlock`(플래시 6렌더러 × 나무 수) |
| 15그루 이상 동시 화상 | `Mesh.Upload`·`VFX_TreeBurn.LateUpdate`(나무당 앞/뒤 메쉬 2개 재구성), `heavyLoadCount` 저부하 모드 진입 여부 |
| 보석 원목 100개 바닥에 깔기 | Shiny 루프 파티클 시뮬레이션 수(화면 안만 돈다), `LogItem.ManualUpdate` 개수 |
| 카메라 이동 중 전체 | 렌더 스레드 `Culling`/`Camera.Render` — 나무 2500그루 × 렌더러 16개 |
| GPU | 반투명 파티클 오버드로우(필레이트). 큰 입자가 겹치면 CPU보다 먼저 한계 |

---

## 7. 새 VFX를 추가할 때 체크리스트

1. **대량으로 재생되는 단발 이펙트**(피격류)라면 3.3 호환 조건에 맞춰 만들고 `InDungeonVFXManager`에서 공유 이미터로 재생하세요. 소팅 오버라이드가 **재생마다 달라야 하면**(나무 order 기준 등) 공유할 수 없으니 풀 경로를 쓰세요.
2. 풀 프리팹에 런타임으로 렌더러를 만드는 스크립트를 붙인다면 **`Awake`에서** 만드세요. `VFXPoolInstanceHelper`의 렌더러 캐시는 첫 활성화 뒤 첫 사용 시 수집되며, 그 뒤에 생긴 렌더러는 소팅 적용에서 빠집니다. 불가피하면 `ReturnToPool`에서 캐시를 비우도록 바꾸세요.
3. 풀 인스턴스의 렌더러 소팅을 코드에서 직접 쓸 거면 같은 인스턴스에 `SetSortingSettings`를 반복 호출하지 마세요(소팅 캐시가 건너뜁니다).
4. 절차적 메쉬 VFX는 `OnEnable`에서 소팅 캐시(`appliedRootSorting*`)를 무효화하는 패턴을 따르세요. 풀 재생 시 `VFXComponent`가 자식 소팅을 덮어씁니다.
5. 매 프레임 세터(`sortingOrder`, `localScale`, `sprite`, 머티리얼 프로퍼티)는 **마지막 적용값과 비교 후** 쓰세요. 단, 같은 속성을 다른 코드도 쓴다면 캐시가 어긋나므로 소유권을 먼저 확인하세요.
6. `WaitForSeconds`는 값이 바뀔 때만 재생성하고, 같은 인스턴스에서 코루틴이 동시에 둘 이상 돌지 않는지 확인하세요. 코루틴은 `StartCoroutine(string)` 대신 핸들을 쓰세요.
7. 메서드 그룹을 델리게이트 인자로 넘기는 호출이 핫패스에 있으면 필드에 1회 캐싱하세요(인스턴스/정적 모두 C# 9에서는 변환마다 할당).
8. MonoBehaviour 클래스의 `static` 필드 초기화에서 Unity 네이티브 객체(`MaterialPropertyBlock`, `Mesh`, `Material` 등)를 만들지 마세요. 정적 생성자가 MonoBehaviour 생성 중에 돌면 예외로 타입 초기화가 실패합니다. `Awake`/`Initialize`에서 지연 생성하세요.

---

## 8. 보류한 최적화와 이유

| 항목 | 기대 효과 | 보류 이유 (보이는 결과가 달라짐) |
|---|---|---|
| 화상 연출 메쉬 합치기 (`VFX_TreeBurn` 전체를 메쉬 하나로) | 불타는 나무 수만큼 들던 메쉬 업로드가 2회로 | 나무별 앞/뒤 소팅이 하나로 합쳐져 겹친 나무 사이 그리기 순서가 바뀜 |
| 과열 충격파 입자 → 단일 메쉬 | 렌더러 58개 × 프레임당 transform/MPB → 드로우콜 1 | `_TintColor`를 MPB로 주는 머티리얼이라 정점 색을 읽도록 셰이더 수정 필요 |
| 보석 원목 Shiny 공유 이미터 | 원목 수만큼의 루프 파티클 시뮬레이션 제거 | 아이템에 부모로 붙어 따라다니고 아이템별 소팅을 쓰므로 공유 불가 |
| 프레임당 타격 VFX 예산 | 극단 부하 스파이크 방지 | 그 순간 이펙트 일부 생략 |
| 화상 연출 시간적 LOD | 멀리 있는 나무는 격 프레임 재구성 | 엄밀히는 연출이 달라짐 |
| 나무/원목 컬링 켜기 | 렌더러 4만 개 컬링 비용 | 현재 구현은 GameObject 전체를 끄므로 충돌 격자 해제·코루틴 중단 등 게임 규칙이 바뀜. 켜려면 렌더러 전용 컬링으로 다시 만들어야 함 |
| `ShockWaveVisualRunner` 방향/위치 `Play` 시 1회 계산 | 매 프레임 Quaternion 연산 제거 | Owner 재사용 시 0.1초 동안 새 방향을 따라가던 동작이 사라짐 |

---

## 9. 관련 파일

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/SharedBurstEmitter.cs` | 공유 이미터 |
| `Assets/Scripts/Presentation Layer/ComponentSystem/TreeVisualComponent/TreeFlashSystem.cs` | 플래시 중앙 시스템 |
| `Assets/Scripts/Presentation Layer/ComponentSystem/VFXComponent/VFXComponent.cs` | 풀, 헬퍼 캐시, `TryGetPoolConfig`, ID 소팅 오버로드 |
| `Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs` | 공유 이미터 생성·사용·정리, `sharedBurstEmitterCapacity` |
| `Assets/Scripts/Presentation Layer/ComponentSystem/TreeVisualComponent/TreeVisualComponent.cs` | `PlayFlash` → `TreeFlashSystem` |
| `Assets/Prefabs/Installer/Installer/GameInstaller.prefab` | 풀 설정(태그별 initial/max) |
| `Assets/Prefabs/VFX/Basic/Impact/VFX_LeafImpact.prefab`, `VFX_SawAndSlashImpact.prefab`, `VFX_LeafExplosion.prefab`, `VFX_TreeExplosion.prefab` | 공유 대상 네 프리팹 |
