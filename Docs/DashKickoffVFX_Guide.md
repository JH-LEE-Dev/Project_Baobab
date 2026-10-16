# 대시 박차기 먼지 VFX 연동 가이드

캐릭터가 대시를 시작할 때 발밑에서 대시 반대 방향으로 퍼지는 흰 먼지 이펙트(**대시 박차기**)를 게임에 연결하는 방법을 정리한 문서입니다.

- 이펙트(Presentation Layer)는 구현되어 있고 프리팹까지 만들어져 있습니다. **연결 코드 2곳과 풀 항목 1개를 추가하면 게임에서 재생됩니다.**
- 연결 전에는 아무 일도 일어나지 않습니다. 기존 동작은 깨지지 않습니다.
- 이 문서의 코드는 예시입니다. 이펙트 쪽 파일은 이미 완성되어 있습니다.

---

## 1. 만든 것

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_DashKickoff.cs` | 이펙트 본체(절차적 픽셀 이펙트) |
| `Assets/Scripts/Presentation Layer/VFX/Editor/DashKickoffBuilder.cs` | 에디터 메뉴 `LumberBoy/VFX/Build Dash Kickoff`. 머티리얼과 프리팹을 다시 만든다(여러 번 실행해도 같은 결과) |
| `Assets/Graphics/VFX/Materials/M_DashKickoff.mat` | 정점 색(흰색 + 알파)을 그대로 그리는 무조명 머티리얼(`Custom/VFX/SpriteGlow`, `_Glow` 1.0) |
| `Assets/Prefabs/VFX/Dash/VFX_DashKickoff.prefab` | 풀 규약 프리팹(루트 ParticleSystem 수명 0.5초 + 앞/뒤/바닥 메쉬) |

### 연출 구성 (한 번 재생 = 대시 한 번)

| 순간 | 내용 |
|---|---|
| 발사(0초) | 십자 섬광, 뒤쪽 부채꼴 먼지, 뒤쪽 속도선, 돌 조각 |
| 이동 중 | 발밑에서 작은 먼지가 이어서 생김(대시 경로를 따라) |

- 먼지는 명암 없는 순백 원판, 알파 0.92. 수명 후반에 줄어들어 `+`, `.`으로 끝납니다.
- 돌 조각은 바닥에 닿기 전에 사라집니다(바운스 없음).
- **전체 길이는 대시 시작부터 0.20초 이내**입니다(정지 후 남는 연출은 없음). 입자 수명, 속도선, 섬광, 돌 조각이 모두 이 안에서 끝나도록 상한을 두었습니다.
- 잔상은 포함하지 않습니다(이미 적용되어 있음). 카메라 흔들림도 포함하지 않습니다.
- 위치는 경과 시간만으로 계산하는 닫힌 식이라 프레임 속도와 무관하고, 재생 중 힙 할당이 없습니다.

---

## 2. 한 장 요약

| 순서 | 어디를 | 무엇을 |
|---|---|---|
| R1 | `Character.prefab`의 `VFXComponent` 풀 목록 | `DashKickoff` 태그 항목 추가 |
| R2 | `CharacterAnimator.cs`, `CharacterVisualComponent.cs` | 재생/종료 메서드 2개 추가(+ 전달 메서드 2개) |
| R3 | `Character.TryStartDash`, `DashState.Exit` | R2의 메서드를 한 줄씩 호출 |

R1이 빠지면 `VFXComponent.Play`가 태그를 못 찾아 **경고 없이 `null`을 돌려주므로** 증상만으로는 원인을 알기 어렵습니다. 이 경우 R2의 `effect == null` 분기에서 조용히 종료됩니다.

---

## 3. R1. 풀 태그 등록

`Assets/Prefabs/Objects/Character/Character.prefab`에서 `Dust` 태그가 들어 있는 `VFXComponent`의 **VFX Pool List**에 같은 형식으로 항목 하나를 추가합니다(걷기 먼지 `Dust`가 이 풀을 씁니다).

| 필드 | 값 |
|---|---|
| `vfxTag` | `DashKickoff` |
| `effectPrefab` | `Assets/Prefabs/VFX/Dash/VFX_DashKickoff.prefab` |
| `initialPoolSize` | 2 |
| `allowDynamicExpansion` | 켜기(1) |
| `maxPoolSize` | 4 |
| `uiParticleScale` | 0 |

대시는 충전 횟수와 쿨타임이 있어서 동시에 겹치는 개수가 적습니다. 위 값이면 충분합니다.

---

## 4. R2. 재생/종료 메서드

`CharacterAnimator.cs`의 `PlayFootstepEffects` 근처에 추가합니다. 걷기 먼지와 같은 `vfxComponent`와 `baseSR`(정렬 기준)을 씁니다.

```csharp
// 진행 중인 대시 박차기. 풀로 돌아간 인스턴스를 건드리지 않도록 활성 여부를 확인한다.
private PresentationLayer.VFX.VFX_DashKickoff activeDashKickoff;

/// <summary>대시 시작 시 호출. _direction은 월드 기준 단위 방향, _distance는 월드 이동 거리, _duration은 이동 시간(초).</summary>
public void PlayDashKickoff(Vector2 _direction, float _distance, float _duration)
{
    if (null == vfxComponent) return;

    ParticleSystem effect = vfxComponent.Play("DashKickoff", transform.position, Quaternion.identity);
    if (null == effect) return;

    if (null != baseSR)
    {
        vfxComponent.SetSortingSettings(effect, baseSR.sortingLayerID, baseSR.sortingOrder);
    }

    // 풀에서 꺼낸 직후(첫 LateUpdate 전)에 대시 값을 정해야 한다.
    PresentationLayer.VFX.VFX_DashKickoff kickoff = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_DashKickoff>(true);
    if (null == kickoff) return;

    kickoff.SetDash(_direction, _distance, _duration);
    activeDashKickoff = kickoff;
}

/// <summary>대시가 끝날 때(정상 종료, 벽에 막힘, 중단 모두) 호출. 멈춘 위치를 알려 이동 중 잔먼지 생성을 멈춘다.</summary>
public void FinishDashKickoff()
{
    if (null == activeDashKickoff) return;

    if (true == activeDashKickoff.gameObject.activeInHierarchy)
    {
        activeDashKickoff.Finish(transform.position);
    }

    activeDashKickoff = null;
}
```

### 인자

| 인자 | 값 | 설명 |
|---|---|---|
| `_direction` | `Character.TryStartDash`의 `dashDirection` | 화면(월드) 기준 방향. 정규화 여부는 상관없다 |
| `_distance` | `dashWorldDistance` | 월드 이동 거리(`GetDashWorldDistance` 결과) |
| `_duration` | `dashDuration`(0.18) | 이동 시간(초) |

---

## 5. R3. 호출 위치

| 위치 | 추가할 줄 |
|---|---|
| `Character.TryStartDash`, `stateMachine.GetState<DashState>().Setup(...)` 바로 아래 | `characterVisualComponent.PlayDashKickoff(dashDirection, dashWorldDistance, dashDuration);` |
| `Character.OnDashFinished` | `characterVisualComponent.FinishDashKickoff();` |

- `OnDashFinished`는 `DashState.Exit`가 어떤 경로로 끝나든(만료, 장애물, 넉백, 사망, 리셋) 한 번만 부르므로 종료 호출 위치로 알맞습니다.
- `Character.cs`가 쥐고 있는 것은 `CharacterVisualComponent`이고, `CharacterAnimator`는 그 안의 private 필드(`characterAnimator`)입니다. 그래서 `CharacterVisualComponent.cs`에 같은 이름의 전달 메서드 두 개가 더 필요합니다.

```csharp
public void PlayDashKickoff(Vector2 _direction, float _distance, float _duration)
{
    if (null != characterAnimator) characterAnimator.PlayDashKickoff(_direction, _distance, _duration);
}

public void FinishDashKickoff()
{
    if (null != characterAnimator) characterAnimator.FinishDashKickoff();
}
```

- 대시 시작 호출은 `ChangeState<DashState>()` 전후 어느 쪽이든 괜찮습니다. 이펙트는 **캐릭터의 대시 시작 위치**에서 재생되므로 캐릭터가 한 틱 움직이기 전에 불리는 편이 정확합니다(위 위치는 움직이기 전입니다).

---

## 6. 튜닝

프리팹의 `VFX_DashKickoff`(DashMesh 자식) 인스펙터 값입니다.

| 필드 | 기본 | 용도 |
|---|---|---|
| `density` | 1.0 | 먼지와 조각 개수 배율 (0.25 ~ 2) |
| `sizeScale` | 1.0 | 먼지 원판 크기 배율 (0.5 ~ 2). 캐릭터 스프라이트 크기에 맞출 때 가장 먼저 만질 값 |
| `defaultDistance` / `defaultDuration` | 3 / 0.18 | `SetDash`를 부르지 않았을 때만 쓰는 값 |
| `groundSortingLayerName` / `groundSortingOrder` | `ShockWave` / 0 | 속도선 소팅 |

코드 상수로 조정하는 값(필요하면 요청 주세요): 속도선 개수(`BaseStreak`), 먼지 수명(`SetupPuffs`의 수명 범위), 전체 길이(`TotalLength`), 잔먼지 생성 구간(`TrailWindow`).

---

## 7. 확인 방법

1. 메뉴 `LumberBoy/VFX/Build Dash Kickoff`를 한 번 실행하면 머티리얼과 프리팹이 같은 결과로 다시 만들어집니다(이미 만들어져 있음).
2. R1~R3를 적용한 뒤 던전에서 8방향으로 대시합니다.
3. 확인할 항목
   - 먼지가 대시 방향의 **반대쪽**으로 퍼지는가(8방향 모두).
   - 벽에 부딪혀 멈추면 이동 중 잔먼지 생성이 멈추는가.
   - 먼지 크기가 캐릭터 대비 적당한가(`sizeScale`).
   - 캐릭터의 앞/뒤 정렬이 자연스러운가(아래 8장).

---

## 8. 미검증 항목 (게임에서 확인이 필요합니다)

| 항목 | 상태 |
|---|---|
| 캐릭터 실제 스프라이트 크기 대비 먼지 크기 | 시뮬레이션은 캐릭터 약 19px 기준. 실제 비율은 미확인 → `sizeScale`로 조정 |
| 앞/뒤 메쉬 정렬 | 루트 렌더러(캐릭터 `baseSR` 순서) 기준 앞쪽 +1, 뒤쪽 -1로 설정. 팔/도끼 등 다른 렌더러의 순서와 겹치는지는 미확인 |
| 대시 시작 위치가 발밑인가 | 걷기 먼지와 같은 `transform.position`을 썼으나 대시에서의 정확도는 미확인 |
| 잔상 이펙트와의 겹침 | 두 이펙트가 동시에 나올 때의 시각적 밀도는 미확인 |
| 성능 | 프로파일링은 하지 않았다. 입자 수가 한 번에 10개 안팎(밀도 1.0 기준)이고 정점은 행 단위 런 병합으로 줄여 두었다 |
| 실제 플레이 중 재생 | 에디터 편집 모드에서 8방향 프레임 캡처로만 확인했다. 플레이 모드 재생은 하지 않았다 |
