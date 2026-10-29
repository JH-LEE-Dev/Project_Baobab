# 헤이스트(이동속도 버프) VFX 가이드

캐릭터가 이동속도 버프를 받는 동안 따라다니는 연출 `VFX_Haste`의 구조와 연동 방법입니다. `CharacterAnimator.PlayHaste/StopHaste`, `CharacterVisualComponent`, `Character`의 `HasteStartedEvent/HasteEndedEvent` 연결은 현재 코드에 있습니다. `Character.prefab`의 풀 항목(`Haste`)은 이번 개편에서 확인하지 않았습니다. 3장은 연동 참고용입니다.

- 구조 기준: [VFX_Procedural_Pixel_Guide.md](VFX_Procedural_Pixel_Guide.md) 10장, [DungeonVFXOptimization.md](DungeonVFXOptimization.md) 8장. 대시 박차기 연동([DashKickoffVFX_Guide.md](DashKickoffVFX_Guide.md))과 같은 패턴입니다.
- 수치 중 "실측"은 에디터 캡처로 확인한 값입니다. 확인하지 못한 항목은 5장에 모았습니다.

---

## 1. 연출

**발끝 스파크(가루)**로 속도를 표현하고 나머지는 절제합니다(2026-10-29 개편). 몸 스프라이트를 복제하는 방식(잔상)과 바닥에 남는 자국(발자국)은 쓰지 않습니다. 색은 별 표식과 같은 **하늘색 한 계열**(흰색, 하늘색, 파랑)입니다.

| 요소 | 담당 | 내용 |
|---|---|---|
| 발끝 스파크 | `VFX_Haste`(앞 메쉬) | 걸음마다 3~4개가 이동 반대쪽으로 튀어 오르고 중력(95px/s^2)으로 떨어짐. 0.32~0.54초 동안 `x` -> `+` -> `.`, 흰색 -> 하늘색. 최대 40개 |
| 발동 연출(0.85초) | `VFX_Haste` | 입자 14개가 나선으로 감아 오르며 가슴 높이에 `◆`가 번쩍임. **바닥 고리는 없음**(요청으로 제거) |
| 걸음 먼지 | `VFX_Haste` | 걸음마다 좌우 발 번갈아 발밑에서 `●` -> `+` -> `.`로 줄어드는 먼지 2~3개가 이동 반대쪽으로 밀림 |
| 발끝 반짝임 | `VFX_Haste` | 멈춰 있어도 몸 주변에서 `◆` `+` `.` 3개가 올라감(버프 중 표시) |

스파크와 먼지는 같은 발 위치(걸음마다 좌우 번갈아, 이동 방향에 수직으로 3px)에서 만들어집니다. 방향과 속도는 **대상 Transform의 위치 변화량으로 스스로 계산**하며(이동 코드와 결합 없음), 아이소 지면 평면 기준(화면 y를 2배로 환산)으로 환산합니다. 캐릭터의 실제 이동속도는 이 이펙트가 바꾸지 않으므로 이동속도 버프와 같이 써야 "빨라졌다"는 느낌이 납니다.

### 폐기한 안과 이유

| 안 | 폐기 이유 |
|---|---|
| 직선·점선 꼬리(속도선) | 아이소 화면에서 이질적("너무 직선으로 뻗은 형태") |
| 킥 & 방전 + 감아 오르는 바람 | "너무 요란하다" |
| 반투명 실루엣 잔상 에코 | 대시 잔상과 구분이 안 됨 |
| 발자국 트레일 | 요청으로 제외("바닥에 자국 남는 거 빼고 가루만 남기자"). 구현·검증 후 삭제 |
| 디더 분해 잔상 에코 | 위로 이동할 때 버벅이는 느낌. 잔상의 정렬 순서가 만든 시점에 고정(`-round(y x precision) + offset`)되어, 위로 달리면 캐릭터의 순서는 작아지는데 잔상은 큰 값을 유지해 **캐릭터 몸 위로 덮여 번쩍임**. 몸을 복제하는 방식 전체의 구조적 문제라 방식 자체를 버림 |

## 2. 파일과 API

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_Haste.cs` | 발동/먼지/스파크/반짝임 본체 |
| `Assets/Prefabs/VFX/Buff/VFX_Haste.prefab` | 풀 프리팹(루트 ParticleSystem 수명 3600초 + 앞/뒤 메쉬) |
| `PixelPainter.cs` | 공용 그리기 도우미(스텔라 이펙트와 공유). 입자는 `Shape` |

| 멤버 | 설명 |
|---|---|
| `Begin(Transform target, SpriteRenderer sortingSource)` | 풀에서 꺼낸 직후 호출. `target`은 따라다닐 캐릭터(위치 = 발밑), `sortingSource`는 캐릭터 본체 `SpriteRenderer`(`baseSR`). 소팅 오더를 매 프레임 따라감(뒤 메쉬 -1, 앞 메쉬 +1) |
| `End()` | 새 입자 생성을 멈추고 남은 입자가 사라진 뒤(0.6초) 스스로 풀 반환 |
| `Cancel()` | 연출 없이 즉시 풀 반환(캐릭터 사망, 씬 이탈) |
| `CharacterVisualComponent.PlayHaste()` / `StopHaste(bool immediate)` | `CharacterAnimator`로 VFX를 재생/종료. `immediate`면 즉시 정리(사망, 씬 이탈) |

- 이전의 인스펙터 `intensity`, `referenceSpeedPx`는 삭제했습니다. `VFX_Haste.prefab`에 `referenceSpeedPx: 78`이 남아 있지만 Unity가 무시합니다(프리팹을 다시 저장하면 사라짐).
- 대상 오브젝트가 비활성화되면 VFX는 스스로 끝납니다.
- 레이어: 먼지는 캐릭터 소팅 -1(캐릭터 뒤), 스파크/반짝임/발동 입자는 캐릭터 소팅 +1(캐릭터 앞). 바닥 고리 메쉬는 없습니다.
- 실행 순서: `DefaultExecutionOrder(10000)`. 캐릭터 이동과 카메라 갱신 뒤의 위치를 따라갑니다. 속도 보간 계수는 45/s(방향 전환 지연 축소). 모든 위치는 `RoundToInt`로 통일.

## 3. 연동 (직접 적용 필요)

### 3.1 풀 항목 (`Character.prefab` -> `VFXComponent`의 VFX Pool List)

| 필드 | 값 |
|---|---|
| `vfxTag` | `Haste` |
| `effectPrefab` | `Assets/Prefabs/VFX/Buff/VFX_Haste.prefab` |
| `initialPoolSize` | 1 |
| `allowDynamicExpansion` | 켜기 |
| `maxPoolSize` | 2 |
| `uiParticleScale` | 0 |

태그가 다르면 `Play`가 경고 없이 null을 돌려줍니다.

### 3.2 `CharacterAnimator` (`PlayDashKickoff` 아래에 같은 패턴으로)

```csharp
private PresentationLayer.VFX.VFX_Haste activeHaste;

/// <summary>헤이스트 버프가 시작될 때 호출. 이미 재생 중이면 아무 일도 하지 않는다.</summary>
public void PlayHaste()
{
    if (null == vfxComponent) return;
    if (null != activeHaste && true == activeHaste.gameObject.activeInHierarchy) return;

    ParticleSystem effect = vfxComponent.Play("Haste", transform.position, Quaternion.identity);
    if (null == effect) return;

    PresentationLayer.VFX.VFX_Haste haste = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_Haste>(true);
    if (null == haste) return;

    haste.Begin(transform, baseSR);
    activeHaste = haste;
}

/// <summary>헤이스트 버프가 끝날 때 호출. _bImmediate가 참이면 연출 없이 즉시 끝낸다(사망 등).</summary>
public void StopHaste(bool _bImmediate = false)
{
    if (null == activeHaste) return;

    if (true == activeHaste.gameObject.activeInHierarchy)
    {
        if (true == _bImmediate) activeHaste.Cancel();
        else activeHaste.End();
    }

    activeHaste = null;
}
```

### 3.3 버프 시스템

이동속도 버프가 적용될 때 `PlayHaste()`, 해제될 때 `StopHaste()`, 캐릭터 사망/런 종료 때 `StopHaste(true)`를 부릅니다. 버프가 갱신(지속시간 연장)될 때는 `PlayHaste()`를 다시 불러도 이미 재생 중이면 무시되므로 발동 연출이 반복되지 않습니다. 현재 코드에는 헤이스트 버프 자체가 없어서 호출 위치는 버프 구현에 따라 정합니다.

## 4. 구현 규칙 준수 현황

| 규칙 | 적용 |
|---|---|
| JAGGIES | 타원 윤곽이 없어 해당 없음(발동 고리 제거) |
| DOUBLE PIXEL / HUGGING | 선분 없이 점/도형만 그림 |
| 입자 모양 | `.` `+` `x` `●` `◆`만 사용 |
| 30fps 이상 | 모든 그림이 매 프레임 `Time.deltaTime`으로 갱신됨 |
| 최적화 | 정적 `PixelQuadBuffer`, 해시로 업로드 생략, `PixelVfxCulling`, 소팅 값 바뀔 때만 쓰기, 고정 크기 구조체 배열(먼지 64, 스파크 40)로 힙 할당 없음 |

## 5. 검증과 미검증

검증:

- Unity 컴파일 오류/경고 0개(2026-10-29, 발자국 제거 후).
- 시뮬레이션(웹, 5개 후보 비교)에서 5번 안(발자국 + 발끝 스파크)을 승인받아 구현한 뒤, 요청으로 발자국을 빼고 스파크만 남겼습니다. 시뮬레이션 로직은 스텁 환경에서 1800프레임 실행해 NaN과 예외가 없음을 확인했습니다.
- 잔상 방식 관련 코드(`CharacterVisualComponent`의 헤이스트 링 버퍼, `Character`의 `hasteAfterimage*` 필드, 디더 셰이더 `SpriteGhostDither`와 머티리얼 `M_HasteAfterimage`)를 제거했고, 남은 참조가 없음을 확인했습니다.

미검증:

| 항목 | 상태 |
|---|---|
| 스파크의 실제 화면(상하·좌우·대각선 이동, 방향 반전, 멈췄다 다시 달릴 때) | 플레이 모드 미확인 |
| 쿼드 수와 `QuadBudget` | 스파크(최대 40개)를 더해도 예산(앞 1500, 뒤 750) 안일 것으로 계산했으나 실측하지 않음 |
| 대시와 헤이스트가 겹칠 때 | 대시 중 스파크가 계속 나오는지(대시는 걸음 수가 늘지 않을 수 있음) 미확인 |
| 좌우 이동 시 캐릭터 싱크(먼지·스파크) | 실행 순서 지연과 서브픽셀 스냅(`SubpixelSnapper`)/스프라이트 피봇 오프셋이 원인인지는 가설. 플레이 모드에서 어긋남이 남으면 피봇(`target`이 발밑인지)부터 확인 |
| 블룸이 켜진 실제 게임 화면의 색 | 캡처는 포스트 프로세싱 없이 했음 |
| 풀 반환, `CharacterAnimator` 연동 경로, 사망/씬 이탈 | 연동 전이라 미확인 |
| 프레임 시간/할당 측정 | 미측정(10.10 절차 미수행) |
| 물 위, 계단 등 특수 지형에서 발밑 위치 | 미확인 |
