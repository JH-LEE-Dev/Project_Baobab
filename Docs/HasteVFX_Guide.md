# 헤이스트(이동속도 버프) VFX 가이드

캐릭터가 이동속도 버프를 받는 동안 따라다니는 연출 `VFX_Haste`의 구조와 연동 방법입니다. **이펙트와 프리팹까지 만들었고, 게임 코드(`Character.prefab` 풀 항목, `CharacterAnimator`/`CharacterVisualComponent`, 버프 시스템 호출)는 아직 연결하지 않았습니다.** 3장의 코드를 연동 담당자가 적용합니다.

- 구조 기준: [VFX_Procedural_Pixel_Guide.md](VFX_Procedural_Pixel_Guide.md) 10장, [DungeonVFXOptimization.md](DungeonVFXOptimization.md) 8장. 대시 박차기 연동([DashKickoffVFX_Guide.md](DashKickoffVFX_Guide.md))과 같은 패턴입니다.
- 수치 중 "실측"은 에디터 캡처로 확인한 값입니다. 확인하지 못한 항목은 5장에 모았습니다.

---

## 1. 연출

화살표나 상승 표시 없이 **"몸 주변 공기가 같이 빠르게 흐른다"** 는 느낌입니다. 색은 별 표식과 같은 **하늘색 한 계열**(흰색, 하늘색, 파랑)입니다.

| 요소 | 내용 |
|---|---|
| 발동 연출(0.85초) | 입자 14개가 나선으로 감아 오르며 가슴 높이에 `◆`가 번쩍임. **바닥 고리는 없음**(요청으로 제거) |
| 속도선 | 이동할 때 이동 반대 방향으로 공중에 남았다가 앞에서부터 사라지는 1px 선. 속도에 비례해 많아지고 길어짐 |
| 발걸음 바람 먼지 | 걸음마다(13px 보폭) 발밑에서 `●` -> `+` -> `.`로 줄어드는 먼지가 뒤로 밀림. 두 걸음에 한 번 발끝에 `◆` 반짝임 |
| 발끝 반짝임 | 멈춰 있어도 몸 주변에서 `◆` `+` `.` 3개가 올라감(버프 중 표시) |

시뮬레이션에서 승인한 설정입니다: 블룸 켬, 바람 리본(소용돌이) 끔, 속도선 켬, 잔상 끔, 발걸음 바람 먼지 켬. 리본과 잔상은 구현하지 않았습니다.

속도와 방향, 걸음 수는 **대상 Transform의 위치 변화량으로 스스로 계산**합니다(이동 코드와 결합 없음). 캐릭터의 실제 이동속도는 이 이펙트가 바꾸지 않으므로 이동속도 버프와 같이 써야 "빨라졌다"는 느낌이 납니다.

## 2. 파일과 API

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_Haste.cs` | 이펙트 본체 |
| `Assets/Prefabs/VFX/Buff/VFX_Haste.prefab` | 풀 프리팹(루트 ParticleSystem 수명 3600초 + 앞/뒤 메쉬) |
| `PixelPainter.cs`, `PixelEllipse.cs` | 공용 그리기 도우미(스텔라 이펙트와 공유) |

| 멤버 | 설명 |
|---|---|
| `Begin(Transform target, SpriteRenderer sortingSource)` | 풀에서 꺼낸 직후 호출. `target`은 따라다닐 캐릭터(위치 = 발밑), `sortingSource`는 캐릭터 본체 `SpriteRenderer`(`baseSR`). 소팅 오더를 매 프레임 따라감(뒤 메쉬 -1, 앞 메쉬 +1) |
| `End()` | 새 입자 생성을 멈추고 남은 입자가 사라진 뒤(0.6초) 스스로 풀 반환 |
| `Cancel()` | 연출 없이 즉시 풀 반환(캐릭터 사망, 씬 이탈) |
| 인스펙터 `intensity` | 속도선/입자 배율(0.4~1.5, 기본 1) |
| 인스펙터 `referenceSpeedPx` | 헤이스트로 달릴 때의 바닥 속도(px/s, 기본 78). 속도선 길이/개수 기준. **실제 캐릭터 속도에 맞출 것** |

- 대상 오브젝트가 비활성화되면 스스로 끝납니다.
- 레이어: 먼지는 캐릭터 소팅 -1(캐릭터 뒤), 속도선/반짝임/발동 입자는 캐릭터 소팅 +1(캐릭터 앞). 바닥 메쉬는 없다.

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

`CharacterVisualComponent`에는 같은 이름의 전달 메서드 두 개(`PlayHaste`, `StopHaste(bool)`)를 `PlayDashKickoff`와 같은 방식으로 추가합니다.

### 3.3 버프 시스템

이동속도 버프가 적용될 때 `PlayHaste()`, 해제될 때 `StopHaste()`, 캐릭터 사망/런 종료 때 `StopHaste(true)`를 부릅니다. 버프가 갱신(지속시간 연장)될 때는 `PlayHaste()`를 다시 불러도 이미 재생 중이면 무시되므로 발동 연출이 반복되지 않습니다. 현재 코드에는 헤이스트 버프 자체가 없어서 호출 위치는 버프 구현에 따라 정합니다.

## 4. 구현 규칙 준수 현황

| 규칙 | 적용 |
|---|---|
| JAGGIES | 타원 윤곽이 없어 해당 없음(발동 고리 제거) |
| DOUBLE PIXEL | 속도선의 L자 모서리 제거(`PixelPainter.Line`) |
| HUGGING | 선 옆에 보조선을 겹치지 않음 |
| 입자 모양 | `.` `+` `x` `●` `◆`만 사용 |
| 30fps 이상 | 모든 그림이 매 프레임 `Time.deltaTime`으로 갱신됨 |
| 최적화 | 정적 `PixelQuadBuffer`, 해시로 업로드 생략, 그린 범위로 경계 축소, `PixelVfxCulling`, 소팅 값 바뀔 때만 쓰기, 힙 할당 없음(고정 크기 배열, 람다/LINQ 없음) |

## 5. 검증과 미검증

검증(실측, 에디터 플레이 모드, 640x360 캡처, 임시 캐릭터 + 기둥):

- 컴파일 오류/경고 0개.
- 발동 -> 정지 -> 달리기 -> 정지 -> 달리기 -> `End` -> 파티클 종료까지 확인했습니다. 쿼드 최대 **74개**(앞/뒤 합계, 바닥 고리 제거 후. 제거 전 173개).
- 속도선, 먼지, 반짝임, 발동 고리가 그려지고 `End` 뒤 루트 ParticleSystem이 종료되는 것을 확인했습니다.

미검증:

| 항목 | 상태 |
|---|---|
| 실제 캐릭터 스프라이트 크기에 맞는 입자 크기와 높이(속도선 높이 2~24px, 반짝임 반경 9px) | 시뮬레이션용 임시 캐릭터(약 24px)로만 확인. 실제 캐릭터와 다르면 `VFX_Haste`의 상수를 조절 |
| 블룸이 켜진 실제 게임 화면의 색 | 캡처는 포스트 프로세싱 없이 했음 |
| 풀 반환, `CharacterAnimator` 연동 경로, 사망/씬 이탈 | 연동 전이라 미확인 |
| 프레임 시간/할당 측정 | 미측정(10.10 절차 미수행) |
| 물 위, 계단 등 특수 지형에서 발밑 위치 | 미확인 |
