# 대쉬 먼지 이펙트(`VFX_DashDust`) 적용 가이드

대쉬를 밟는 순간 발밑에서 한 번 튀는 흰색 반투명 먼지 이펙트를 만들어 두었습니다. **기존 대쉬 이펙트가 없어서 프리팹 교체만으로는 적용되지 않으므로**, 아래 4단계를 적용하면 대쉬에 연결됩니다.

- 작성일: 2026-10-15
- 모양 확정: 웹 시뮬레이션(대쉬 먼지 미리보기, 아이소메트릭 바닥면 모델 V2)
- 검증: 같은 시드(7)로 8방향 × 9개 시점(72건)에서 Unity 구현과 시뮬레이션의 픽셀이 완전히 일치하는 것을 확인했습니다. 메쉬 경로(행 병합 → 업로드)도 72건 모두 일치합니다. 컴파일 경고 0개, 셰이더 오류 없음.
- 아직 확인하지 않은 것: 대쉬에 연결한 뒤의 플레이 모드 동작(아래 5장 체크리스트로 확인)

---

## 1. 만들어 둔 것

| 구분 | 경로 | 내용 |
|---|---|---|
| 셰이더 | `Assets/Shaders/VFX/PixelQuadAlpha.shader` | 정점 색(RGBA)을 그대로 그리는 반투명 셰이더. `BrandWrapGlow`와 달리 **정점 알파가 투명도**이고 발광(HDR)이 없음 |
| 머티리얼 | `Assets/Graphics/VFX/Materials/M_DashDust.mat` | 위 셰이더 사용 |
| 스크립트 | `Assets/Scripts/Presentation Layer/VFX/VFX_DashDust.cs` | 절차적 픽셀 먼지(네임스페이스 `PresentationLayer.VFX`) |
| 프리팹 | `Assets/Prefabs/VFX/Basic/VFX_DashDust.prefab` | 풀 규약: 루트는 ParticleSystem(렌더러 꺼짐, 입자 1개, 수명 0.6초), 자식 `DashDustMesh`에 `VFX_DashDust` |

### 연출 사양

| 항목 | 값 |
|---|---|
| 재생 | 대쉬 시작 시 1회, 0.36초 |
| 색 / 알파 | 흰색 단색, 알파 0.30(프리팹 `VFX_DashDust`의 `alpha`로 조절), 발광 없음 |
| 모양 | 바닥면 위 입체 쐐기(출발점 쪽이 높고 꼬리가 낮음) + 물방울형 바닥 자국 + 중반에 부푸는 둥근 머리 + 꼬리 방향 1px 바닥 줄 + 알갱이 1~3개 |
| 방향 | 꼬리는 대쉬 반대쪽 바닥으로 깔림. 바닥 방향은 2:1 등각 투영이고 높이는 화면 위쪽. 8방향 모두 같은 식으로 처리 |
| 위치 | 출발 발 위치에서 꼬리 쪽 바닥으로 0.15유닛. 월드에 고정(캐릭터를 따라가지 않음) |
| 크기(px, 32 PPU) | 꼬리 10~13, 높이 6~8.5, 앞쪽 반폭 2.2~3.2. 재생마다 조금씩 다름 |

### 공개 API (`VFX_DashDust`)

| 멤버 | 설명 |
|---|---|
| `void SetDirection(Vector2 _worldDashDirection)` | 대쉬 방향(월드, y 위가 +)을 넘김. **풀에서 Play한 직후(같은 프레임) 호출**해야 함. 호출하지 않으면 오른쪽 대쉬로 그림 |
| `static Vector3 GetSpawnPosition(Vector3 _footPosition, Vector2 _worldDashDirection, float _backOffsetUnits)` | 발 위치에서 대쉬 반대쪽 바닥으로 물러난 생성 위치. 바닥 거리를 등각으로 재므로 시뮬레이션과 같은 배치가 됨 |

재생 시작(`OnEnable`)에서 방향을 오른쪽으로 초기화하므로, 풀이 오브젝트를 활성화한 **뒤에** `SetDirection`을 불러야 합니다. `VFXComponent.Play`가 반환한 뒤 부르면 됩니다.

---

## 2. 적용 방법

모든 변경은 Presentation Layer와 캐릭터 프리팹입니다. Application Layer는 건드리지 않습니다.

### 2.1 캐릭터 프리팹에 풀 항목 추가

`Assets/Prefabs/Objects/Character/Character.prefab` → `Base Visuals/Animator` 오브젝트의 `VFXComponent` → `Vfx Pool Data List`에 항목을 추가합니다. 발소리 먼지(`Dust`)가 있는 바로 그 컴포넌트입니다(`OverheatComponent`에 있는 VFXComponent가 아님).

| 필드 | 값 | 이유 |
|---|---|---|
| Vfx Tag | `DashDust` | |
| Effect Prefab | `VFX_DashDust.prefab` | |
| Initial Pool Size | 2 | 대쉬 최대 저장 횟수(3단계 2회) 연타 대비 |
| Allow Dynamic Expansion | 끔 | `Dust`와 같은 설정 |
| Max Pool Size | 4 | 수명 0.6초 동안 연속 대쉬해도 충분 |

### 2.2 `CharacterAnimator.cs`: 재생 메서드 추가

파일: `Assets/Scripts/Presentation Layer/ComponentSystem/CharacterVisualComponent/CharacterAnimator.cs`

1. 파일 상단에 `using PresentationLayer.VFX;`를 추가합니다(`System.Collections.Generic`은 이미 있음).
2. 필드 선언부(`//내부 의존성`)에 추가합니다.

```csharp
private const string DashDustTag = "DashDust";
// 풀 인스턴스별 VFX_DashDust를 한 번만 찾아 둔다(DungeonVFXOptimization.md 2.3 드론 VFX 조회와 같은 방식).
private readonly Dictionary<ParticleSystem, VFX_DashDust> dashDustLookup = new Dictionary<ParticleSystem, VFX_DashDust>(4);
```

3. `PlayFootstepEffects` 근처에 메서드를 추가합니다. 발 위치는 발소리 먼지와 같은 기준(`transform.position`)을 씁니다.

```csharp
/// <summary>
/// 대쉬를 시작할 때 발밑에 먼지를 한 번 낸다. _dashDirection은 월드 기준 대쉬 방향(Character.TryStartDash의 dashDirection).
/// </summary>
public void PlayDashDust(Vector2 _dashDirection)
{
    if (null == vfxComponent) return;

    Vector3 spawnPosition = VFX_DashDust.GetSpawnPosition(transform.position, _dashDirection, dustOffsetDistance);
    ParticleSystem effect = vfxComponent.Play(DashDustTag, spawnPosition, Quaternion.identity);
    if (null == effect) return;

    if (null != baseSR)
    {
        vfxComponent.SetSortingSettings(effect, baseSR.sortingLayerID, baseSR.sortingOrder);
    }

    VFX_DashDust dashDust;
    if (false == dashDustLookup.TryGetValue(effect, out dashDust))
    {
        dashDust = effect.GetComponentInChildren<VFX_DashDust>(true);
        dashDustLookup.Add(effect, dashDust);
    }

    if (null != dashDust) dashDust.SetDirection(_dashDirection);
}
```

- `dustOffsetDistance`(기본 0.15)는 발소리 먼지와 같은 값을 재사용합니다. 따로 조절하고 싶으면 별도 `[SerializeField]` 필드를 만드세요. **새 필드는 프리팹에 `SerializedObject`로 값을 명시적으로 써 줘야 합니다**(VFX 가이드 8장 "프리팹 직렬화 캐시").
- 소팅은 발소리 먼지와 같습니다(캐릭터 몸통 레이어/오더). 먼지가 캐릭터 앞에 그려지는 게 거슬리면 `baseSR.sortingOrder - 1`로 바꿉니다.

### 2.3 `CharacterVisualComponent.cs`: 전달 메서드 추가

파일: `Assets/Scripts/Presentation Layer/ComponentSystem/CharacterVisualComponent/CharacterVisualComponent.cs`

`SetFacingDirection` 근처에 추가합니다.

```csharp
/// <summary>
/// 대쉬 시작 연출(발밑 먼지)을 재생한다.
/// </summary>
public void PlayDashEffect(Vector2 _dashDirection)
{
    if (null != characterAnimator) characterAnimator.PlayDashDust(_dashDirection);
}
```

### 2.4 `Character.cs`: 대쉬 시작 시 호출

파일: `Assets/Scripts/Presentation Layer/UnitSystem/Character/Character.cs` → `TryStartDash()`

`stateMachine.ChangeState<DashState>();` **바로 다음 줄**에 추가합니다.

```csharp
stateMachine.GetState<DashState>().Setup(dashDirection, dashWorldDistance, dashDuration);
stateMachine.ChangeState<DashState>();

// 대쉬를 밟는 순간 발밑 먼지(출발 위치 기준). 이동은 다음 물리 틱부터라 지금 위치가 출발점이다.
characterVisualComponent.PlayDashEffect(dashDirection);
```

- 이 위치는 대쉬가 **실제로 시작될 때만** 지나갑니다. 해금 전, 충전 없음, UI 열림 같은 경우는 위쪽 `return`에서 빠지므로 먼지도 나오지 않습니다.
- `DashState.Enter`나 `FixedUpdate`에 넣지 마세요. 상태 머신은 넉백, 리셋 등 다른 경로로도 상태를 바꾸므로, 연출은 `TryStartDash` 한 곳에서만 냅니다.

---

## 3. 동작 원리 요약 (수정할 때 참고)

1. **바닥면 모델**: 바닥 좌표 a(꼬리 쪽), b(옆), 높이 z로 먼지를 정의합니다. 대쉬 방향(월드)은 바닥 벡터 (x, 2y)로 정규화하고, 바닥 1px는 화면 (x, y/2)로 투영합니다(32×16 타일, 2:1). 높이는 화면 위쪽입니다.
2. **래스터**: 바닥 기둥(a, b)을 0.5px 간격으로 돌며 높이만큼 화면 세로로 칸을 찍습니다. 둥근 머리는 원, 바닥 줄은 1px 선, 알갱이는 점 또는 짧은 대시입니다.
3. **합집합과 행 병합**: 칸은 정적 마스크(96×96)에 모아 겹침을 없애고(알파 이중 누적 방지), 행마다 이어진 칸을 한 쿼드로 합쳐 `PixelQuadBuffer`에 올립니다. 평균 프레임당 약 8.5쿼드입니다.
4. **시간**: 솟음(0~22%) → 둥근 머리(42% 근처) → 낮아지며 꼬리 쪽으로 밀림(45~100%) → 4×4 순서 디더로 흩어짐(78~100%). 알갱이는 후반 30Hz로 깜빡입니다.
5. **시뮬레이션과의 일치**: 같은 난수(mulberry32), double 연산, JS `Math.round`와 같은 반올림(`RoundJs`, 0.5는 위로)을 씁니다. `Mathf.RoundToInt`(짝수 반올림)로 바꾸면 결과가 달라지므로 바꾸지 마세요. 모양을 바꿀 때는 시뮬레이션과 스크립트를 함께 고쳐야 비교 검증이 유지됩니다.

## 4. 적용된 최적화

`VFX_Procedural_Pixel_Guide.md` 10장과 `DungeonVFXOptimization.md`를 따랐습니다.

- 정적 공유 `PixelQuadBuffer`와 `ReserveMesh`, 구조체·고정 배열을 써서 런타임 힙 할당 0
- 정점 수와 내용 해시가 같으면 업로드 생략(`Begin`에서 -1로 초기화)
- 메쉬 경계는 실제 그린 픽셀 범위, 화면 밖이면 재구성 생략(`PixelVfxCulling`, 첫 3프레임은 보이는 것으로 봄)
- 색은 재생 시작 때 1회 계산(에디터 `OnValidate`에서 갱신)
- Transform·소팅·enabled는 바뀔 때만 쓰고, 캐시는 `OnEnable`에서 무효화(풀 재생 시 `VFXComponent`가 소팅을 덮어쓰므로)
- `_CameraSortingLayerTexture`를 쓰지 않으므로 `SortingLayerTextureGate` 등록 불필요
- 호출 측: 인스턴스별 컴포넌트 조회는 Dictionary로 1회(2.2)

## 5. 적용 후 확인 체크리스트

1. 플레이 중이 아닐 때 컴파일하고 경고 0개
2. 던전에서 대쉬(해금 상태) → 출발 지점 발밑에 먼지가 한 번 나오는지
3. 8방향 대쉬 → 꼬리가 항상 대쉬 반대쪽 바닥으로 깔리는지(위/아래는 짧은 둔덕)
4. 저장 횟수 2회 연타 → 먼지 두 개가 각각 출발 지점에 남는지, 풀 경고가 없는지
5. 대쉬 직후 캐릭터 앞뒤 정렬이 어색하지 않은지(필요하면 2.2의 소팅 오더 조정)
6. 잔디·흙·밝은 바닥에서 알파 0.30이 "보일락 말락"인지(필요하면 프리팹 `alpha` 조절)
7. 프로파일러: 대쉬 프레임에 `GC.Alloc`이 없는지(첫 재생의 Dictionary 등록 1회는 예외)
