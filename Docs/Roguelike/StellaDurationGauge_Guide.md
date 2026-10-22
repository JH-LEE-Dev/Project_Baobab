# 로그라이크 이펙트 가이드: 스텔라 유지시간 게이지

별 위에 뜨는 작은 푸른 유지시간 게이지의 구조, 연동 방법, 검증 결과를 정리한 인수인계 문서입니다. 이펙트 스크립트와 프리팹까지만 만들었고, **Application Layer(`RoguelikeSkillVfxPlayer`, `InDungeonVFXManager`, `GameInstaller.prefab`) 연동은 아직 적용하지 않았습니다.** 3장의 코드를 연동 담당자가 적용합니다.

- 구조 기준: [StellaOffroadVFX_Guide.md](StellaOffroadVFX_Guide.md)(`VFX_StellaRange`와 같은 풀 구조), [VFX_Procedural_Pixel_Guide.md](../VFX_Procedural_Pixel_Guide.md) 10장, [DungeonVFXOptimization.md](../DungeonVFXOptimization.md) 8장
- 시뮬레이션으로 승인받은 연출 그대로 구현했습니다(등장 스프링, 호버, 젤리 머리, 22% 이하 점멸, 예비 동작 후 납작하게 퇴장).

---

## 1. 한눈에 보기

| 항목 | 값 |
|---|---|
| 스크립트 | `Assets/Scripts/Presentation Layer/VFX/VFX_StellaDurationGauge.cs` |
| 프리팹 | `Assets/Prefabs/VFX/Roguelike/VFX_StellaDurationGauge.prefab` |
| 프리팹 빌더 | `Assets/Scripts/Presentation Layer/VFX/Editor/StellaDurationGaugeBuilder.cs` (메뉴 `LumberBoy/VFX/Build Stella Duration Gauge`, 범위 링 프리팹을 복제해 자식 스크립트만 교체) |
| 풀 태그(제안) | `StellaGaugeEffect` |
| 풀 크기(제안) | 초기 3 / 최대 12 (필드에 동시에 있는 별 수 이상) |
| 소팅 | `FlyingItem` / 0 (매 프레임 비교 후 쓰기. 별 그림 위에 보여야 하므로 별의 소팅 순서보다 크게 `SetSortingOrder`로 맞춘다) |

별 하나당 인스턴스 하나입니다. 재생 위치는 **별의 바닥 위치**이고 막대는 `gaugeHeightPx`(기본 46px) 위에 그려집니다.

### 1.1 연출 구성

| 단계 | 내용 |
|---|---|
| 등장 | 가로로 좁게 시작해 위로 튀어 오르며 스프링으로 출렁이다 안착. 별가루 `+` `.` `x` `◆` 7개 |
| 유지 | 1px 단위 호버, 막대 폭 호흡, 채움 앞쪽 젤리 머리(줄어드는 동안 눌림), 남은 비율 22% 이하는 흰색 점멸(12Hz) |
| 퇴장 | 위로 늘어나는 예비 동작(0.09초) → 납작하게 눌리며 사라짐. 입자 `+` `.` `x` `●` `◆` 10개. 입자가 끝나면 스스로 루트 ParticleSystem을 멈춰 풀에 반환 |

---

## 2. 멤버

| 멤버 | 설명 |
|---|---|
| `Begin(float lifetimeSeconds)` | 총 수명으로 시작. **풀에서 꺼낸 직후** 호출. 0 이하이면 `defaultLifetime`(6초) |
| `SetRemaining(float remainingSeconds)` | 남은 수명을 맞춘다. 수명이 늘어나는 효과로 남은 시간이 총 수명을 넘으면 총 수명도 함께 늘린다. 부르지 않아도 스스로 줄어든다. 매 프레임 불러도 됨 |
| `End()` | 퇴장 연출 시작. 이미 퇴장 중이면 무시. 남은 수명이 0이 되어도 스스로 호출한다 |
| `SetSortingOrder(int order)` | 소팅 순서 |

---

## 3. Application Layer 연동 (직접 적용 필요)

### 3.1 풀 항목 추가 (`GameInstaller.prefab` -> `VFXManager`의 `VFXComponent.vfxPoolDataList`)

```yaml
- vfxTag: StellaGaugeEffect      # VFX_StellaDurationGauge.prefab 루트 ParticleSystem
  initialPoolSize: 3
  allowDynamicExpansion: 1
  maxPoolSize: 12
```

태그 문자열이 다르면 `Play`가 null을 돌려주고 아무것도 나오지 않습니다(오류 로그 없음).

### 3.2 `InDungeonVFXManager` 재생 메서드

```csharp
public ParticleSystem PlayStellaGaugeVFX(Vector3 _groundPosition, float _lifetime, int _sortingOrder)
{
    if (vfxComponent == null) return null;

    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "StellaGaugeEffect", _groundPosition, Quaternion.identity, "FlyingItem", _sortingOrder, null));
    if (effect == null) return null;

    PresentationLayer.VFX.VFX_StellaDurationGauge gauge = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_StellaDurationGauge>(true);
    if (gauge != null)
    {
        gauge.SetSortingOrder(_sortingOrder);
        gauge.Begin(_lifetime);
    }

    return effect;
}
```

게이지는 별마다 하나이므로 별 번호(`StarId`)를 키로 `Dictionary<int, ParticleSystem>`에 보관하세요. 정리할 때 `effect.GetComponentInChildren<VFX_StellaDurationGauge>(true).End()`를 부릅니다. `GetComponentInChildren`은 호출 빈도가 높아지면 풀 인스턴스 생성 시 캐시하세요.

### 3.3 `RoguelikeSkillVfxPlayer.Play` 신호 연결

| 신호 | 변경 |
|---|---|
| `StarSpawned` | `PlayStellaGaugeVFX(position, lifetime, Order(position) + 50)`를 추가. `lifetime`은 `tuning.stellaStarLifetime * DurationMul(Stella)`(= `RoguelikeStar.ExpireTime - Time.time`)이다. 현재 신호에는 이 값이 실리지 않는다(`duration`이 0) - 발행처 `RoguelikeSkillController.TrySpawnStar`에서 `Publish(StarSpawned, ..., duration, id)`로 채우거나, 플레이어가 `_stars` 목록에서 `StarId`로 별을 찾아 `ExpireTime`을 읽는다 |
| `StarBlast` / `StarBlastRepeat` / `StarExpired` | 해당 별의 게이지 `End()` |
| 수명이 늘어나는 효과 | 별 `ExpireTime`이 바뀌는 곳에서 `SetRemaining(ExpireTime - Time.time)`를 부른다(없으면 게이지가 처음 수명대로만 줄어든다) |
| `RestoreStars` (런 복원) | 복원되는 별마다 `PlayStellaGaugeVFX`를 호출(남은 수명은 `ExpireTime - Time.time`) |
| 웨이브 종료로 별이 정리될 때 | 해당 별의 게이지 `End()` |

### 3.4 되돌리기

연동 한 줄씩을 지우면 게이지가 사라집니다. 스크립트와 프리팹은 그대로 둬도 무해합니다.

---

## 4. 구현 규칙 준수 현황

| 규칙 | 적용 |
|---|---|
| JAGGIES / DOUBLE PIXEL | 막대 네 모서리 1px 제거(둥근 윤곽), 입자는 `PixelPainter.Shape`의 5종만 |
| HUGGING | 막대 안쪽 3줄(하이라이트 / 몸통 / 그림자)이 테두리에 1px씩 붙고 겹치지 않음. 스프링 배율은 반올림한 정수 크기를 최근접 표본으로 다시 그려 도트가 번지지 않음 |
| 입자 모양 | `.` `+` `x` `●` `◆`만 사용 |
| 30fps 이상 | 매 프레임 `Time.deltaTime`(최대 0.05초, 스프링은 4 서브스텝)으로 다시 계산 |
| 최적화 | 정적 `PixelQuadBuffer`, 같은 줄 병합, 해시로 업로드 생략, 그린 범위로 메쉬 경계 축소, `PixelVfxCulling` 화면 밖 생략, 소팅 값 비교 후 쓰기, `ReserveMesh`, 힙 할당 없음(LINQ/람다 없음, 입자는 고정 구조체 배열) |

---

## 5. 시각 설정과 튜닝

| 증상 | 조절 |
|---|---|
| 막대가 별에 겹치거나 너무 높다 | `gaugeHeightPx`(기본 46) |
| 흰색으로 날아간다 | `GlowColor`의 발광 값(스크립트 `DrawGauge`의 0.05~0.3, 입자 0.3~0.45). BrandWrapGlow 머티리얼 기준 |
| 퇴장이 느리다/빠르다 | `ExitSpringK/C`, `AnticipationTime`, `ExitTime` 상수 |
| 점멸 시작 비율 | `LowRatio`(0.22), `BlinkRate`(12Hz) |

---

## 6. 검증과 미검증

검증(에디터 편집 모드, 임시 하네스로 프레임을 진행해 쿼드 목록을 ASCII로 덤프, 하네스는 삭제함):

- 컴파일 오류/경고 0개, 빌더 메뉴로 프리팹 생성.
- 등장(0.07초 좁고 낮음 → 0.2초 오버슈트로 넓고 높음 → 0.5초 안착 22x5), 채움 감소와 젤리 머리, 22% 이하 구간, 퇴장(예비 동작 → 납작해짐 → 입자만 남고 막대 소멸)의 모양을 확인했습니다.

미검증:

| 항목 | 상태 |
|---|---|
| 실제 게임 화면에서의 색, 블룸 | 포스트 프로세싱 없이 확인 |
| 별 그림 위 소팅 순서 | 연동 전이라 미확인(`SetSortingOrder`로 맞춤) |
| 풀 반환(`Finished` 후 루트 ParticleSystem 정지), 별 여러 개 동시 | 연동 전이라 미확인 |
| 쿼드 수/프레임 시간/할당 측정 | **미측정** (가이드 10.10 절차 미수행). 프레임당 쿼드는 최대 약 40개 |
| `RoguelikeStar` 수명 연장 효과와의 동기화 | `SetRemaining` 호출처가 아직 없음 |
