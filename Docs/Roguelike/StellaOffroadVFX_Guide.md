# 로그라이크 이펙트 가이드: 스텔라 폭발 · 폭발 범위 링 · 오프로드 경로

로그라이크 전용 이펙트 3종의 구조, 연동 방법, 검증 결과를 정리한 인수인계 문서입니다. 이펙트 파일과 프리팹까지만 만들었고, **Application Layer(`RoguelikeSkillVfxPlayer`, `InDungeonVFXManager`, `GameInstaller.prefab`) 연동은 아직 적용하지 않았습니다.** 3장의 코드를 연동 담당자가 적용합니다.

- 구조 기준: [ConstellationNova_Guide.md](ConstellationNova_Guide.md), [VFX_Procedural_Pixel_Guide.md](VFX_Procedural_Pixel_Guide.md) 10장, [DungeonVFXOptimization.md](DungeonVFXOptimization.md) 8장
- 수치 중 "실측"은 에디터 캡처로 확인한 것이고, 나머지는 코드에서 읽은 값입니다. 확인하지 못한 항목은 6장에 모았습니다.

---

## 1. 한눈에 보기

| 이펙트 | 스크립트 | 프리팹 | 풀 태그(제안) | 풀 크기(제안) |
|---|---|---|---|---|
| 스텔라 폭발 | `VFX_StellaBurst` | `Assets/Prefabs/VFX/Roguelike/VFX_StellaBurst.prefab` | `StellaBurstEffect` | 초기 2 / 최대 6 |
| 폭발 범위 링 | `VFX_StellaRange` | `.../VFX_StellaRange.prefab` | `StellaRangeEffect` | 초기 3 / 최대 12 |
| 오프로드 경로 | `VFX_OffroadLane` | `.../VFX_OffroadLane.prefab` | `OffroadLaneEffect` | 초기 3 / 최대 8 |

공용 코드(새로 추가, 세 이펙트가 같이 씀):

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/PixelEllipse.cs` | midpoint 타원 윤곽 + L자 모서리 제거 (JAGGIES, DOUBLE PIXEL) |
| `Assets/Scripts/Presentation Layer/VFX/PixelPainter.cs` | `PixelQuadBuffer` 위에 같은 줄 병합, 경계 수집, 해시 업로드 생략, 입자 5종(`.` `+` `x` `●` `◆`), 점선 링, 선 |

세 프리팹 모두 `VFX_ConstellationNova.prefab`을 복제해 만든 같은 구조입니다(루트 ParticleSystem = 풀 수명 타이머, 자식 메쉬, 바닥 메쉬). 머티리얼은 기존 `BrandWrapGlow` 계열(`M_BrandStampBurst`)을 그대로 씁니다.

### 1.1 레이어 순서 (나무가 항상 위)

| 이펙트 | 바닥 메쉬(`ShockWave`, 나무 아래) | 공중 메쉬(루트 소팅) |
|---|---|---|
| 스텔라 폭발 | 충격 고리, 체커 띠, 바닥에 내려앉은 별가루 | 별, 낙하 별가루, 중심 섬광 |
| 범위 링 | **전부** (링, 도트 띠, 도는 표식) | 없음 |
| 오프로드 경로 | 차선 점선, `<<<<` 화살표, 소환 마커 | 소환 섬광 기둥과 불꽃 |

`Objects`(나무, 캐릭터)가 `ShockWave`보다 위라서 바닥 메쉬는 항상 나무에 가려집니다. 프리팹에 렌더러를 추가하면 `VFXComponent.Play`가 소팅을 덮어쓰므로 각 스크립트의 `LateUpdate`에서 다시 맞추도록 해야 합니다(노바 가이드 4.3과 같은 규칙).

---

## 2. 이펙트별 사용법

### 2.1 `VFX_StellaBurst` (스텔라 폭발)

박자: 0~0.22초 별가루가 모임 → 도트 별 섬광 → 타원 궤도 3겹이 바깥으로 퍼지며 궤도의 별에서 별가루가 바닥으로 떨어짐 → 바닥 충격 고리 → 내려앉아 반짝이다 사라짐. 전체 2.8초(`TotalDuration`).

| 멤버 | 설명 |
|---|---|
| `SetRadius(float units)` | 이번 재생의 반지름. **풀에서 꺼낸 직후(첫 LateUpdate 전)** 호출. 0 이하이면 인스펙터 `maxRadius`(3.0) |
| `CurrentRadius` | 이번 재생의 실제 반지름 |
| `static TotalDuration` | 2.8초. 루트 ParticleSystem 수명(3.0)은 이 값 이상이어야 함 |
| `static EvaluateShockArrivalTime(float normalizedDistance)` | 충격 고리가 반지름 비율(0~1)에 닿는 시각(초, 재생 시작 기준). 피격 타이밍을 고리에 맞출 때 사용 |
| 인스펙터 `density` | 별, 별가루, 방사 별가루 개수 배율(0.25~2). 과한 느낌 조절 |
| 인스펙터 `starHeightPx` | 별 그림 중심이 바닥에서 뜬 높이(px, 기본 34). 재생 위치는 **별의 바닥 위치**로 넘기고 높이는 이 값이 맡음 |

- 범위: 가장 바깥 궤도와 충격 고리가 반지름에 닿고, 궤도 별 수와 별가루 낙하 범위가 반지름에 비례합니다. 바닥 타원 비율은 `GroundSquashY` 0.5(피격 범위와 같은 값).
- **입자 개수(담백 설정, 2026-10-22):** 눈 피로를 줄이려고 두 번 줄였습니다. 반지름 3유닛 기준 궤도 별 약 11개(큰 별 8개당 1개), 낙하 별가루 간격 0.25초, 방사 별가루 8개(꼬리 점 1개), 바닥 잔상은 별가루의 1/6, 충격 띠는 폭 6px에 네 칸에 한 점. 별 폭발이 동시에 여러 개면 2번째 50%, 3번째부터 30%로 자동 감소합니다(`activeBurstCount`). 실측 쿼드 최대(에디터): 단독 3,130 -> 996, 연쇄 3개 7,941 -> 2,501. 더 성기게/풍성하게 하려면 `VFX_StellaBurst`의 `DustSpawnStep`, `HeroStarInterval`, `SparkBaseCount`, `SparkTrailDots`, 인스펙터 `density`를 조절하세요.
- 반지름 1~6유닛 범위를 가정해 만들었습니다(별 수 상한 40개 x 3겹, 별가루 1,600개, 방사 별가루 96개).

### 2.2 `VFX_StellaRange` (폭발 범위 링)

별이 필드에 있는 동안 유지되는 **루프 이펙트**입니다. 0.3초 동안 안쪽에서 펼쳐지고, 그 뒤 점선 링, 링을 따라 도는 밝은 구간, 가장자리 도트 띠, 별 표식 6개가 계속 돕니다.

| 멤버 | 설명 |
|---|---|
| `SetRadius(float units)` | 링의 가로 반지름. **재생 중에도 호출 가능**(범위 증가 카드 반영) |
| `Activate()` | 폭발 직전 밝은 강조(0.35초). `VFX_StellaBurst` 재생 직전에 호출 |
| `End()` | 링이 깜빡이며 사라지고(0.3초) 스스로 루트 ParticleSystem을 멈춰 풀에 반환 |

- **반드시 `End()`를 불러야 합니다.** 부르지 않으면 루트 ParticleSystem 수명(60초)이 끝날 때만 반환됩니다. 별이 터지거나 사라지거나 웨이브가 끝나 정리될 때 호출하세요.
- 별 하나당 인스턴스 하나입니다. 풀 최대 크기는 동시에 필드에 있는 별 수 이상으로 잡으세요(스텔라 최대 별 수는 `RoguelikeSkillTuningSO.stellaBaseMaxStars` + `CountAdd`).
- 소팅은 `ShockWave`/0으로 고정이며 매 프레임 비교 후 쓰기만 합니다.

### 2.3 `VFX_OffroadLane` (오프로드 소환 위치 · 돌진 방향 표시, v2.8 방식)

기획서 v2.8 규칙: **소환될 위치를 발동 주기가 시작될 때 정해 주기 내내 표시**하고, 발동 때 소환 섬광과 함께 표시를 걷는다(예전의 "소환 직전 약 0.8초 예고"는 없음). 위치가 정해지는 순간 `Begin`, 발동 순간 `Fire`입니다.

표시 내용: 소환 위치 마커(수축 링, 게이트 선, 도는 표식), 진행 방향 `<<<<` 화살표 펄스(밝은 파동 두 줄), 차선 가장자리 점선. 주기가 아무리 길어도(상한 55초) 같은 모양이 계속 돌아가고, 발동하면 소환 섬광 -> 소환 지점부터 걷힘 -> 스스로 풀 반환입니다.

| 멤버 | 설명 |
|---|---|
| `Begin(Vector3 from, Vector3 to, float halfWidthUnits = 0)` | **위치가 정해지는 시점(발동 주기 시작)** 에 풀에서 꺼낸 직후 호출. `from`/`to`는 차량의 출발점/도착점(`OffroadLanes.TryGetLane`의 entry/exit). `halfWidthUnits`는 돌진 폭의 반폭(0 이하이면 `laneHalfWidth` 0.4) |
| `SetRemaining(float seconds)` | (선택) 발동까지 남은 시간. 알리면 마지막 0.5초에 마커가 흰색으로 깜빡임. 소환 주기가 바뀌면(레벨, 공격속도) 다시 불러 갱신. 한 번도 안 부르면 깜빡임 없이 유지 |
| `Fire(float dashSeconds = 0)` | **발동(차량 소환) 순간** 호출. 소환 섬광 + 걷힘 시작. `dashSeconds`는 차량이 경로를 지나는 시간(`OffroadDash` 신호의 `duration`)이며 0 이하이면 `clearSeconds`(0.8). 끝나면 스스로 풀 반환 |
| `Cancel()` | 웨이브/런 종료 등 발동 없이 지울 때. 연출 없이 즉시 풀 반환 |
| 인스펙터 `clearSeconds` | `Fire`에 시간을 넘기지 않을 때의 걷힘 시간(0.8초) |
| 인스펙터 `groundOffset` | 차량 위치에서 바닥 접지점까지 내려가는 높이(기본 0). 차량 pivot이 바닥보다 높으면 조절 |
| `Begin(from, to, warmSeconds, halfWidth)` (4인자) | **호환용, 지울 예정.** 예열 시간 뒤 자동 발동하는 v2.8 이전 방식. 현재 `InDungeonVFXManager.PlayOffroadLaneVFX`가 이것을 부르고 있음 |

- 위치는 미리 정해 둔 `from`/`to`로 그리므로 **경로 위 나무가 파괴돼도 표시와 소환은 그대로**입니다(이펙트는 나무를 보지 않음).
- 같은 시점에 여러 대면 대마다 인스턴스 하나를 모두 `Begin`하면 동시에 표시됩니다(풀 최대 8). 방구차 조합도 같은 차량이라 같은 호출을 씁니다.
- 프리팹 루트 ParticleSystem 수명은 60초입니다. 발동을 영영 안 불러도 55초에 스스로 끝납니다.
- 화살표와 차선 폭은 **바닥 평면(세로 2배 편 좌표)에서 정의한 뒤 화면으로 투영**합니다. `OffroadLanes`의 아이소 대각선 경로에서도 `<<<<` 방향이 정확합니다.
- 색은 앰버(경고색)로 별 계열(하늘색)과 구분합니다.

---|---|
| `Begin(Vector3 from, Vector3 to, float warmSeconds, float halfWidthUnits = 0)` | 풀에서 꺼낸 직후 호출. `from`/`to`는 `OffroadWarmup` 신호의 `position`/`position2`. `halfWidthUnits`가 0 이하이면 `laneHalfWidth`(0.4) |
| `TotalDuration` | 예열 + `clearSeconds` + 0.15초 |
| 인스펙터 `clearSeconds` | 경로가 걷히는 시간(기본 0.8초). 차량이 경로를 지나는 시간에 맞춤 |
| 인스펙터 `groundOffset` | 차량 위치에서 바닥 접지점까지 내려가는 높이(기본 0). 차량 pivot이 바닥보다 높으면 조절 |

- 화살표와 차선 폭은 **바닥 평면(세로 2배 편 좌표)에서 정의한 뒤 화면으로 투영**합니다. `OffroadLanes`가 만드는 아이소 대각선(왼쪽 아래, 오른쪽 아래) 경로에서도 `<<<<` 방향이 정확합니다. 다른 방향도 같은 식으로 동작합니다.
- 색은 앰버(경고색)로 별 계열(하늘색)과 구분합니다.

---

## 3. Application Layer 연동 (직접 적용 필요)

### 3.1 풀 항목 추가 (`GameInstaller.prefab` -> `VFXManager`의 `VFXComponent.vfxPoolDataList`)

```yaml
- vfxTag: StellaBurstEffect      # VFX_StellaBurst.prefab 루트 ParticleSystem
  initialPoolSize: 2
  allowDynamicExpansion: 1
  maxPoolSize: 6
- vfxTag: StellaRangeEffect      # VFX_StellaRange.prefab
  initialPoolSize: 3
  allowDynamicExpansion: 1
  maxPoolSize: 12
- vfxTag: OffroadLaneEffect      # VFX_OffroadLane.prefab
  initialPoolSize: 3
  allowDynamicExpansion: 1
  maxPoolSize: 8
```

태그 문자열이 다르면 `Play`가 null을 돌려주고 아무것도 나오지 않습니다(오류 로그 없음).

### 3.2 `InDungeonVFXManager` 재생 메서드 (`PlayConstellationNovaVFX` 아래에 같은 패턴으로)

```csharp
public void PlayStellaBurstVFX(Vector3 _groundPosition, float _radius, int _sortingOrder)
{
    if (vfxComponent == null) return;

    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "StellaBurstEffect", _groundPosition, Quaternion.identity, "FlyingItem", _sortingOrder, null));
    if (effect == null) return;

    PresentationLayer.VFX.VFX_StellaBurst burst = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_StellaBurst>(true);
    if (burst != null) burst.SetRadius(_radius);
}

public ParticleSystem PlayStellaRangeVFX(Vector3 _groundPosition, float _radius)
{
    if (vfxComponent == null) return null;

    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "StellaRangeEffect", _groundPosition, Quaternion.identity, "ShockWave", 0, null));
    if (effect == null) return null;

    PresentationLayer.VFX.VFX_StellaRange range = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_StellaRange>(true);
    if (range != null) range.SetRadius(_radius);
    return effect;
}

// v2.8: 위치가 정해지는 시점에 부르고, 반환된 컴포넌트를 들고 있다가 발동 때 Fire, 웨이브 종료 때 Cancel
public PresentationLayer.VFX.VFX_OffroadLane PlayOffroadLaneVFX(Vector3 _from, Vector3 _to, float _halfWidth)
{
    if (vfxComponent == null) return null;

    ParticleSystem effect = vfxComponent.Play(new VFXPlaySettings(
        "OffroadLaneEffect", _from, Quaternion.identity, "FlyingItem", 0, null));
    if (effect == null) return null;

    PresentationLayer.VFX.VFX_OffroadLane lane = effect.GetComponentInChildren<PresentationLayer.VFX.VFX_OffroadLane>(true);
    if (lane != null) lane.Begin(_from, _to, _halfWidth);
    return lane;
}
```

- `GetComponentInChildren` 탐색은 호출마다 일어납니다. 호출 빈도가 높아지면 풀 인스턴스 생성 시 캐시하세요(노바 가이드 5.5).
- 범위 링은 별마다 하나이므로 별 번호를 키로 `Dictionary<int, ParticleSystem>`에 보관하세요. 정리할 때 `effect.GetComponentInChildren<VFX_StellaRange>(true).End()`를 부릅니다.

### 3.3 `RoguelikeSkillVfxPlayer.Play` 신호 연결

| 신호 | 현재 | 변경 |
|---|---|---|
| `StarSpawned` | 본편 땅 별 + 등장음 | 그대로 두고 `PlayStellaRangeVFX(position, radius)`를 추가. 반지름은 `stellaBlastRadius * RangeMul(Stella)`와 같은 값을 쓰도록 신호에 `radius`를 실어야 함(현재 `StarSpawned`는 `radius`를 넘기지 않음 - 발행처 `RoguelikeSkillController`에서 값을 채울 것) |
| `StarBlast` / `StarBlastRepeat` / `LoneStarPulse` | 본편 별똥별 착지 폭발(`PlayStarImpactExplosionVFX`) | 이 호출을 `PlayStellaBurstVFX(position, radius, Order(position) + 40)`로 교체. 범위 링은 `Activate()` 후 `End()` |
| `StarExpired` | 본편 땅 별 소멸 | `End()` 추가 |
| `OffroadWarmup` | (연출 신호 없음, 차량만 예열) | **이 신호에는 경로 표시를 연결하지 않는다.** 이 신호는 발동 시점에 오므로 v2.8 규칙(주기 내내 표시)과 맞지 않는다. 아래 3.5의 절차를 따를 것 |

`StarBlast`의 `radius`는 "공격 반경"입니다. 폭발 판정이 바닥 타원의 가로 반지름이면 그대로 `SetRadius`에 넘기면 보이는 범위와 판정이 같아집니다.

### 3.4 되돌리기

연동 한 줄씩을 지우면 기존 연출로 돌아갑니다. 이펙트 파일과 프리팹은 그대로 둬도 무해합니다.

### 3.5 오프로드 경로 표시 연동 절차 (v2.8, 구현 담당)

이펙트 쪽 API는 준비되어 있습니다. 게임 코드에서 바꿔야 할 것은 **소환 위치를 발동 시점이 아니라 주기 시작 시점에 정하는 것**입니다(기획서 부록 C-1 · C-3 4-5). 현재 `RoguelikeSkillController.StartOffroadVolley`는 발동 때 차선을 고르고 `StartOffroad`로 바로 차량을 만듭니다.

1. 주기가 시작될 때(웨이브 시작, 직전 발동 직후) 이번 주기의 차선들(`OffroadLane`: entry, exit, 폭)을 미리 고른다. 겹치는 위치 제외, 나무가 있는 경로만 후보 등 기존 규칙은 그대로.
2. 고른 차선마다 `PlayOffroadLaneVFX(entry, exit, radius)`를 부르고 반환된 `VFX_OffroadLane`을 차선과 같이 보관한다.
3. 주기 타이머가 바뀔 때(레벨, 공격속도, 첫 주기) 보관한 모든 컴포넌트에 `SetRemaining(남은 초)`를 불러 마지막 깜빡임을 맞춘다(생략 가능).
4. 발동 시점에 보관한 차선으로 `StartOffroad`를 호출해 차량을 만들고, 같은 순간 컴포넌트마다 `Fire(돌진 시간)`을 부른다. 돌진 시간은 `StartOffroad`가 쓰는 `car.length / car.maxSpeed` 근사값이면 된다(`OffroadDash` 신호의 `duration`과 같은 값).
5. 위치는 미리 정해 둔 값이므로 경로 위 나무가 파괴돼도 그대로 소환한다.
6. `EndCombat`에서 보관한 컴포넌트에 `Cancel()`을 불러 정리한다.
7. 기존 `PlayOffroadLaneVFX(from, to, warmSeconds, halfWidth)` 호출과 `OffroadWarmup` 신호에 걸린 호출은 지운다. 호출을 옮긴 뒤에는 `VFX_OffroadLane`의 호환용 4인자 `Begin`도 지운다.
8. 방구차 조합도 같은 차량이라 같은 경로를 쓴다.

---

## 4. 구현 규칙 준수 현황

| 규칙 | 적용 |
|---|---|
| JAGGIES | 모든 타원은 `PixelEllipse` midpoint 한 가지(충격 고리, 범위 링, 소환 마커) |
| DOUBLE PIXEL | 타원 윤곽과 `PixelPainter.Line`의 L자 모서리 제거. 화살표 선은 1px 한 줄 |
| HUGGING | 충격 고리끼리 3px 이상 띄움, 범위 링의 바깥 보조 링 4px 띄움, 소환 마커의 수축 링은 본 링과 3px 이상일 때만 그림. 화살표 옆에 그림자 선을 겹치지 않음 |
| 입자 모양 | `.` `+` `x` `●` `◆`만 사용(`PixelPainter.Shape`) |
| 30fps 이상 | 모든 그림이 매 프레임 `Time.deltaTime`으로 다시 계산됨(입자에 고정 저FPS 스텝 없음) |
| 최적화 | 정적 `PixelQuadBuffer`, 같은 줄 병합, 해시로 업로드 생략, 그린 범위로 메쉬 경계 축소, `PixelVfxCulling` 화면 밖 생략, 소팅 값 비교 후 쓰기, `ReserveMesh`, 힙 할당 없음(LINQ/람다 없음) |

미적용(가이드 10장 기준): 발광 색 테이블은 스텔라 별가루(`CoolSteps`/`GoldSteps`)와 화살표(`ChevronColors`)에만 있고 나머지는 `GlowColor`를 직접 호출합니다. 호출 수가 적어 넣지 않았습니다.

---

## 5. 시각 설정과 튜닝

| 증상 | 조절 |
|---|---|
| 별가루가 흰색으로만 보인다 | 정점 알파(발광)가 높으면 `GlowMin 1.2 / GlowMax 6` 배율로 색이 흰색으로 날아갑니다. 각 스크립트 상단의 `GlowColor(색, 세기)` 값을 낮추세요. 지금 값은 블룸이 있는 게임 화면 기준으로 낮춰 둔 상태입니다 |
| 너무 과하다 / 소박하다 | `VFX_StellaBurst.density` 0.5~1.5 |
| 범위 링이 눈에 띄지 않는다 | `VFX_StellaRange.RingGlow`(코드 상수 0.28)와 도트 띠 발광 |
| 경로 표시가 걷히는 속도가 차량과 안 맞는다 | `Fire(dashSeconds)`에 실제 돌진 시간을 넘긴다. 넘기지 않을 때만 `clearSeconds`(0.8초)를 쓴다 |

---

## 6. 검증과 미검증

검증(실측, 에디터 플레이 모드, 640x360 1:1 캡처, 빈 배경 + `Objects` 레이어 사각형):

- 컴파일 오류/경고 0개.
- 세 이펙트 모두 렌더링되고, 바닥 메쉬(링, 경로)가 `Objects` 레이어 사각형 **뒤에** 그려지는 것을 확인했습니다.
- 오프로드 경로가 대각선 경로에서 소환 마커, 화살표 파동, 걷힘 순서로 진행되는 것을 확인했습니다.

미검증:

| 항목 | 상태 |
|---|---|
| 블룸/톤매핑이 있는 실제 게임 화면의 색 | 캡처는 포스트 프로세싱 없이 했습니다. 5장의 발광 값은 그 상태 기준으로 낮춘 값이라 인게임에서 다시 확인 필요 |
| 반지름 1 이하, 6 초과의 외형 | 3.0 기준으로만 확인 |
| 연동 후 풀 반환, `End()` 호출 경로, 별 여러 개 동시 | 연동 전이라 미확인 |
| 쿼드 수/프레임 시간/할당 측정 | **미측정** (가이드 10.10 절차 미수행) |
| 오프로드 경로 방향 4방향 외 다른 각도, 차량 pivot 높이(`groundOffset`) | 미확인 |
| `StarSpawned` 신호에 `radius` 전달 | 현재 값이 없어 발행처 수정 필요 |

---

## 7. 별 표식 링 블룸과 표식 별 감싸기 (2026-10-22 추가, 직접 적용됨)

스텔라 폭발에 맞은 나무에 표식이 생길 때의 연출 보강입니다. 위 1~6장과 달리 **게임 코드에 직접 반영**했습니다.

| 변경 | 파일 | 내용 |
|---|---|---|
| 링 블룸 | `TreeVisualComponent.cs`, `TreeObj.cs` | 표식 링(`constellationRenderer`)의 HDR은 `highlightHDRIntensity`(대부분 1.0)를 써서 블룸 임계값(1.345)을 넘지 못했습니다. `SetStarMarked(bool, float _markHdrOverride = 0)`와 `SetConstellationMarkActive(bool, float _hdrOverride = 0)`에 덮어쓰기 인자를 추가했습니다. 0이면 기존 동작 그대로라 **본편 별자리 나무는 바뀌지 않습니다**. 표식이 꺼지면 덮어쓰기는 자동으로 지워집니다 |
| 링 밝기 값 | `RoguelikeSkillController.cs` | `StarMarkRingHdr = 1.75f` — 본편 땅 별(`TreeStarMarkGroundAnimator.hdrIntensity`)과 같은 값. 1.345보다 커서 블룸이 걸립니다 |
| 별 감싸기 | `RoguelikeSkillVfxPlayer.cs`, `RoguelikeSkillController.cs` | 표식이 찍히면 본편 `VFX_BrandStarWrap`(타원 궤도를 도는 별)을 `InDungeonVFXManager.BeginBrandStarWrap`으로 시작합니다 |

별 감싸기 수명:

| 상황 | 처리 |
|---|---|
| 표식 만료(나무 생존) | `End()` — 별이 궤도 안쪽으로 모이며 사라진 뒤 스스로 풀 반환 |
| 나무가 죽음/풀 반환 중 (`OnTreeKilled`, `UpdateMarks`) | 연출 없이 즉시 회수(`ForceRelease`) |
| 웨이브/런 종료 (`EndCombat`) | `ClearMarkWraps()`로 전부 즉시 회수 |
| 이미 표식이 있는 나무에 다시 맞음 | 새로 시작하지 않고 만료 시간만 갱신 |

- 풀 프리팹(`brandStarWrapPrefab`)은 `GameInstaller.prefab`에 연결돼 있습니다. 풀이 비어 있으면 감싸기만 조용히 건너뜁니다.
- **미검증:** 컴파일만 확인했습니다. 실제 로그라이크 런에서 링이 번지는지, 별 감싸기의 시작/회수 타이밍, 표식 나무가 많을 때의 비용은 확인하지 못했습니다.
