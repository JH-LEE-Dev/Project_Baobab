# 버프 HUD 사용 가이드

스태미나 바 위에 뜨는 **버프 아이콘 줄**(`HUD_BuffBar`)을 쓰는 방법입니다.
게임 로직(윗단)이 "무슨 버프가 어떤 상태인지"를 알려 주면, HUD가 등장·갱신·퇴장 연출을 알아서 그립니다.

> 현재 상태: **HUD 쪽은 완성**되어 있고, **윗단에서 신호를 내려 주는 연결은 아직 없습니다.**
> 이 문서의 3장이 그 연결을 어떻게 하면 되는지(권장 구조)이고, 4장부터가 호출 규칙입니다.

---

## 1. 한눈에 보기

```
[게임 로직 (Application / 컴포넌트)]
        │  신호 (버프 켜짐 / 값 변경 / 해제)
        ▼
[GameplayUICoordinator]  ──►  [UIView_HUD.BuffBar]  ──►  [HUD_BuffBar]  ──►  [HUD_BuffIcon × N]
                                  (접근자)                 (목록/배치/입자)        (칸 하나의 연출)
```

- 윗단은 **`BuffDisplayInfo` 한 덩어리와 id**만 알면 됩니다. 아이콘 칸 만들기, 위치, 슬라이드, 입자, 숫자 그리기는 전부 HUD가 합니다.
- 버프 하나 = **정수 `id` 하나**입니다. 같은 id로 다시 `AddOrUpdate`하면 새로 만들지 않고 갱신합니다.
- 상한: 한 번에 최대 **16개**(풀 크기 `poolCapacity`). 넘으면 경고 로그만 남기고 무시합니다.

관련 파일

| 파일 | 역할 |
|---|---|
| [`HUD_BuffBar.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/HUD_BuffBar.cs) | 공개 API, 목록/배치/오버플로, 입자 오버레이 |
| [`HUD_BuffIcon.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/HUD_BuffIcon.cs) | 칸 하나의 연출(등장/갱신/퇴장/만료 점멸/숫자/게이지) |
| [`BuffDisplayInfo.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/BuffDisplayInfo.cs) | 표시 정보 구조체, 종류 열거형 |
| [`UIView_HUD.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/UIView_HUD.cs) | `BuffBar` 접근자, 마을에서 숨기기 |
| 프리팹 | `Assets/Prefabs/UI/HUD/Buff/HUD_BuffBar.prefab`, `HUD_BuffIcon.prefab`, `HUD_StaminaGroup.prefab` |

---

## 2. 속성별로 어떤 종류를 쓰나 (가장 중요)

버프의 **성질**을 보고 종류(`BuffGaugeKind`)를 고릅니다.

| 버프의 성질 | 종류 | 게이지가 뜻하는 것 | 숫자 | 퇴장 시점 |
|---|---|---|---|---|
| 일정 시간 동안 유지, 다시 걸리면 시간이 처음부터 | **`Duration`** | 남은 시간 비율(줄어듦) | 없음 | 시간이 0이 되면 자동 |
| 횟수/타수가 쌓이고, 쌓인 만큼 효과가 커짐(상한 있음) | **`StackHits`** | 현재 중첩 / 최대 중첩 | 중첩 수 | 중첩이 0이 될 때 |
| 수치가 연속적으로 쌓이다가 **기준값에 닿으면 발동** | **`StackThreshold`** | 현재 값 / 발동 수치(가득 차면 발동) | 현재 값 | 값이 0까지 줄면 |

색은 종류별로 구분됩니다: `Duration`=초록(25% 아래는 붉은색 경고), `StackHits`=금색, `StackThreshold`=청록.

### 이 프로젝트 예시

코드를 확인한 실제 동작에 맞춘 매핑입니다.

| 능력 | 코드상 동작 | 권장 종류 | 이유 |
|---|---|---|---|
| **벌목 리듬** (`AttackRythm1`) | 도끼 공격이 성공할 때마다 콤보 +1(**최대 10**). 마지막 공격 후 **3초**(`COMBO_RESET_TIME`) 지나면 **한 번에 0으로 초기화**. 쿨타임이 `콤보 × attackRythmSpeedMul`% 줄어듦([`AxeComponent.cs`](../Assets/Scripts/Presentation%20Layer/ComponentSystem/ArmComponent/Axe/AxeComponent.cs)) | **`StackHits`** (max 10) | 타수가 쌓이는 중첩형. 만료는 일괄 초기화이므로 HUD 자동 감소를 쓰지 않고 윗단이 직접 `SetValue(id, 0)` |
| **속도의 원천** (`SourceOfSpeed1`) | 발동 시 이동속도 증가 버프가 **3초** 유지. 다시 발동하면 타이머만 3초로 되돌림(중첩 없음)([`StatComponent.cs`](../Assets/Scripts/Presentation%20Layer/ComponentSystem/StatComponent/StatComponent.cs)) | **`Duration`** (3초) | 지속형. 다시 걸릴 때 `AddOrUpdate`가 가득 차오르며 튀는 갱신 연출을 냄 |
| **체력의 원천** (`SourceOfStamina1`) | 나무를 벨 때 **즉시** 피로도 회복(남는 효과 없음) | **버프 HUD 대상 아님** | 상태가 남지 않음. 회복 연출은 스태미나 바(`PlayRecovered`)가 이미 처리 |

> 속도의 원천을 "중첩되는 버프"로 바꾸려면(예: 발동마다 +1, 시간이 지나면 하나씩 감소) `StackHits`에 `decayDelay`/`decayInterval`을 주면 HUD가 단계적으로 줄이고 숫자가 내려가며 붉은 점이 떨어지는 감소 연출까지 그려 줍니다. 3.3절의 표를 보세요.

---

## 3. 표시 정보와 호출 규칙

### 3.1 `BuffDisplayInfo`

| 필드 | 쓰는 종류 | 의미 |
|---|---|---|
| `id` | 공통 | 버프 식별자(정수). 호출 전부 이 값으로 대상을 찾음 |
| `kind` | 공통 | `Duration` / `StackHits` / `StackThreshold` |
| `icon` | 공통 | 12×12 스프라이트(Point 필터 권장). **null이면 임시 아이콘**(id를 3으로 나눈 나머지) |
| `accentColor` | 공통 | 프레임 윗줄 색. 알파 0이면 종류별 기본색 |
| `duration` | Duration | 지속 시간(초) |
| `maxValue` | 스택형 | 값의 상한(StackHits는 게이지가 가득 차는 값). `StackThreshold`는 임계점 이상으로 자동 보정 |
| `initialValue` | 스택형 | 시작 값 |
| `thresholdValue` | StackThreshold | 발동 수치. **게이지가 가득 차는 지점이 곧 임계점**. 0이면 `maxValue`를 사용 |
| `decayDelay` | 스택형 | 마지막 증가 후 이 시간(초) 지나면 감소 시작 |
| `decayInterval` | StackHits | 초마다 1씩 감소(0이면 자동 감소 없음) |
| `decayPerSecond` | StackThreshold | 초당 감소량(0이면 자동 감소 없음) |

### 3.2 공개 API (`HUD_BuffBar`)

| 메서드 | 하는 일 | 언제 쓰나 |
|---|---|---|
| `AddOrUpdate(in info)` | 없으면 **등장 연출**과 함께 추가, 있으면 갱신 | 버프가 걸리는 순간, 지속형이 다시 걸릴 때 |
| `SetValue(id, value)` | 스택형: 현재 값, 지속형: **남은 시간(초)** 설정 | 중첩이 바뀔 때, 시간 보정이 필요할 때 |
| `Trigger(id)` | 발동 연출(불꽃 + 번쩍) 수동 재생 | 임계 돌파가 HUD가 모르는 사건일 때 |
| `Dispel(id)` | **퇴장 연출**과 함께 제거 | 해제/만료가 외부 사건일 때 |
| `Remove(id)` | 연출 없이 즉시 제거 | 씬 전환 정리, 중복 표시 방지 |
| `Clear()` | 전부 즉시 제거 + 입자 정리 | 마을 진입, 런 종료 |
| `Contains(id)` | 표시 중인지 | 중복 호출 방지 |
| `TryGetValue(id, out v)` | 현재 값(스택 값/남은 시간) 읽기 | 증가량을 현재값 기준으로 계산할 때 |

접근은 `UIView_HUD.BuffBar`로 합니다(조합 프리팹을 쓰지 않으면 `null`이므로 항상 null 체크).

### 3.3 종류별 연출 규칙 (무엇이 어떤 연출을 내는가)

**공통**
- 처음 `AddOrUpdate`: 작은 노란 실루엣에서 `OutBack`으로 커지며 등장 + 노란 점/`+`·`x` 입자.
- 아이콘은 약 2.6초마다 밝은 선이 한 번 훑고 지나감(유휴 반짝임).
- 퇴장: 번쩍 → 부풀었다 → 줄어들며 사라짐 → 아이콘 픽셀이 점으로 흩어짐. 이웃 칸이 미끄러져 빈자리를 채움.
- 스태미나 20% 이하 경고 시 프레임/아이콘이 스태미나 바의 붉은 펄스에 동기화(자동).

**`Duration`**
- 남은 시간은 **HUD가 자체 타이머로 센다.** 윗단이 매 프레임 갱신할 필요 없음.
- 같은 id로 `AddOrUpdate` → 게이지가 가득 차고 위로 튀며 번쩍(갱신 연출).
- 남은 시간 30% 미만이면 깜빡이고(만료 임박), 25% 미만이면 게이지가 붉은색으로 바뀜.
- 0이 되면 **자동으로 퇴장 연출**.

**`StackHits`**
- `SetValue(id, n)`에서 n이 **오르면**: 아이콘이 튀고 숫자가 위로 튀며 `+`/점 입자, 게이지 앞쪽이 밝아짐.
- 값이 `maxValue`에 **처음 닿으면**: 금색 외곽(프레임 색) 번쩍임 + 큰 불꽃 + 점 폭발. 최대 상태에선 가끔 금색으로 반짝.
- n이 **내려가면**: 숫자가 아래로 내려가며 붉은 점이 떨어짐.
- `decayDelay`/`decayInterval`을 주면 HUD가 스스로 1씩 깎음. 값이 0이 되면 퇴장 연출.
- **한 번에 0으로 초기화**되는 버프(벌목 리듬)는 `decayInterval`을 0으로 두고 윗단이 `SetValue(id, 0)`을 호출하면 그 즉시 퇴장 연출이 나옵니다.

**`StackThreshold`**
- 값은 부드럽게 따라 올라가고, `thresholdValue`에 닿으면 게이지가 가득 참 + **발동 연출**(청록 불꽃)이 한 번 나옴. 기준 아래로 내려가면 다시 발동 가능.
- `decayDelay`/`decayPerSecond`로 연속 감소(숫자가 내려가고 붉은 점이 가끔 떨어짐). 0까지 줄면 퇴장.
- 값이 임계를 넘어도 `maxValue`까지는 숫자가 계속 오름(게이지는 가득 찬 상태 유지).

---

## 4. 사용 예시 (벌목 리듬 / 속도의 원천)

> 아래 id는 예시입니다. 실제로는 한 곳(enum)에 모아 관리하세요.

```csharp
public enum EBuffHudId
{
    AttackRythm = 1,   // 벌목 리듬
    SourceOfSpeed = 2, // 속도의 원천
}
```

### 4.1 벌목 리듬 — 중첩(StackHits) + 일괄 만료

```csharp
HUD_BuffBar _bar = hudView.BuffBar;
if (null == _bar) return;

// 첫 공격 성공: 1중첩으로 등장
BuffDisplayInfo _info = new BuffDisplayInfo();
_info.id = (int)EBuffHudId.AttackRythm;
_info.kind = BuffGaugeKind.StackHits;
_info.icon = attackRythmIcon;      // 12x12 스프라이트
_info.maxValue = 10f;              // 최대 10중첩
_info.initialValue = 1f;           // 처음 중첩 수
_info.decayInterval = 0f;          // 자동 감소 없음(만료는 한 번에 초기화)
_bar.AddOrUpdate(_info);

// 이후 공격 성공마다: 현재 중첩 수를 그대로 전달 (오르면 튀기기/불꽃, 10이 되면 최대 연출)
_bar.SetValue((int)EBuffHudId.AttackRythm, attackComboStack);

// 3초 안에 공격이 없어 콤보가 0으로 초기화될 때: 퇴장 연출
_bar.SetValue((int)EBuffHudId.AttackRythm, 0f);
```

- 첫 호출과 이후 호출을 나누지 않아도 됩니다. `Contains(id)`가 false일 때만 `AddOrUpdate`, 이후는 `SetValue`만 쓰면 됩니다.
- **알아 둘 한계**: 지금 HUD는 "다음 초기화까지 남은 3초"를 게이지로 보여 주지 않습니다. 게이지는 중첩 비율이고 숫자가 중첩 수입니다. 남은 시간을 보여 줘야 하면 별도 요구로 알려 주세요.

### 4.2 속도의 원천 — 지속(Duration)

```csharp
BuffDisplayInfo _info = new BuffDisplayInfo();
_info.id = (int)EBuffHudId.SourceOfSpeed;
_info.kind = BuffGaugeKind.Duration;
_info.icon = sourceOfSpeedIcon;
_info.duration = 3f;               // 효과 지속 시간
hudView.BuffBar.AddOrUpdate(_info); // 다시 발동돼도 같은 호출: 갱신 연출 + 시간 리셋
```

- 만료되면 HUD가 스스로 퇴장 연출을 냅니다. 윗단은 아무것도 호출하지 않아도 됩니다.
- 윗단 타이머와 어긋날 수 있는 상황(일시정지 등)에는 `SetValue(id, 남은초)`로 보정합니다.
- 도중에 효과가 강제로 끝나면(런 초기화 등) `Dispel(id)`(연출 있음) 또는 `Remove(id)`(연출 없음).

### 4.3 중첩되고 시간 되면 하나씩 꺼지는 버프(자동 감소)

```csharp
_info.kind = BuffGaugeKind.StackHits;
_info.maxValue = 5f;
_info.initialValue = 1f;
_info.decayDelay = 3f;      // 마지막 증가 후 3초 뒤부터
_info.decayInterval = 2f;   // 2초마다 1씩 감소
hudView.BuffBar.AddOrUpdate(_info);
// 증가할 때만 SetValue(id, 새값) 호출. 감소와 퇴장은 HUD가 알아서.
```

> 이 경우 **윗단의 실제 중첩 값과 HUD의 감소 타이밍이 어긋나지 않게** `decayDelay`/`decayInterval`을 로직 값과 같게 맞춰야 합니다. 둘이 다를 수 있으면 4.1처럼 윗단이 매번 `SetValue`로 정확한 값을 내려 주는 쪽이 안전합니다.

---

## 5. 윗단에서 신호를 내려 주는 권장 구조 (미구현)

이 프로젝트의 UI 흐름은 `신호 → GameplayUICoordinator → UIView` 입니다(대시 해금도 같은 구조로 `IDashState`를 읽습니다).
Application Layer는 UI 담당이 수정하지 않으므로, **신호 정의와 발행은 로직 담당이**, 구독과 HUD 호출은 UI 담당이 맡는 분담을 권장합니다.

### 5.1 로직 쪽이 준비할 것 (제안)

```csharp
// 예시: Application Layer SignalSystem
public enum EBuffHudOp { Add, SetValue, Trigger, Dispel, Remove, ClearAll }

public struct BuffHudSignal
{
    public int id;
    public EBuffHudOp op;
    public BuffGaugeKind kind;   // Add일 때
    public float value;          // SetValue / 초기 값
    public float max;            // Add일 때(스택형)
    public float duration;       // Add일 때(Duration)
    public float threshold;      // Add일 때(StackThreshold)
    public float decayDelay;
    public float decayInterval;
    public float decayPerSecond;
}
```

- 아이콘 스프라이트는 신호에 직접 담지 말고 **id → 스프라이트 테이블**(ScriptableObject)을 UI 쪽에 두는 것을 권장합니다. 로직이 스프라이트를 몰라도 되고, 아트 교체가 쉽습니다.
- 능력(`SkillCommand`)이 켜지고 꺼질 때, 그리고 중첩 수가 바뀔 때만 신호를 내립니다(매 프레임 X).

### 5.2 UI 쪽에서 받는 곳 (UI 담당)

`GameplayUICoordinator`에서 구독 → `UIView_HUD`에 메서드 추가 → `BuffBar` 호출:

```csharp
// GameplayUICoordinator
private void BuffHud(BuffHudSignal _s) { hudUI.ApplyBuffHud(in _s); }

// UIView_HUD
public void ApplyBuffHud(in BuffHudSignal _s)
{
    if (null == hudBuffBar) return;
    switch (_s.op)
    {
        case EBuffHudOp.Add:    /* BuffDisplayInfo 채워서 AddOrUpdate */ break;
        case EBuffHudOp.SetValue: hudBuffBar.SetValue(_s.id, _s.value); break;
        case EBuffHudOp.Dispel:   hudBuffBar.Dispel(_s.id); break;
        // ...
    }
}
```

---

## 6. 알아 둘 동작

- **마을에서는 숨김**: `UIView_HUD`가 마을 진입 때 `BuffBar.Clear()` 후 비활성화합니다. 던전에 다시 들어와도 **이전 버프는 복원되지 않습니다.** 필요한 버프는 윗단이 던전 진입 시 다시 `AddOrUpdate`해야 합니다.
- **줄 배치**: 칸 폭 18, 간격 20(피치 20). 스태미나 바 폭(160)에 맞춰 **최대 2줄**.
  - 대시 게이지가 보이면 첫 줄 오른쪽을 비움: 대시 2칸 → **첫 줄 5칸**, 1칸 → 6칸, 대시 없음 → 8칸. 둘째 줄은 항상 8칸.
  - 두 줄이 모두 차면 마지막 칸이 `+N`으로 바뀌고 넘친 버프는 숨겨짐(타이머는 계속 진행).
- **시간 기준**: 지속 시간 타이머는 `Time.deltaTime`입니다. 시간 정지(`timeScale 0`) 중에는 HUD 타이머도 멈춥니다.
- **알파 페이드 없음**: 외곽선이 겹쳐 보이는 문제 때문에 등장/퇴장은 크기 단계, 색, 점멸로만 표현합니다.
- **아이콘 규격**: 12×12, Point 필터, 불투명 픽셀 위주. 퇴장 때 불투명 픽셀 하나하나가 점으로 흩어지므로 안티앨리어싱(반투명 가장자리)이 있으면 지저분해집니다.

---

## 7. 테스트하는 법

윗단 신호가 아직 없으므로, 다음 중 하나로 HUD만 확인합니다.

1. 임시 `MonoBehaviour`에서 `FindAnyObjectByType<UIView_HUD>().BuffBar`로 접근해 4장의 호출을 그대로 실행합니다.
2. 프리팹 재생성이 필요하면 메뉴 `LumberBoy/HUD/Build Buff HUD Prefabs`를 실행합니다(임시 아트와 프리팹을 같은 규칙으로 다시 만듭니다).

---

## 8. 자주 하는 실수

| 증상 | 원인 | 해결 |
|---|---|---|
| 같은 버프가 칸이 두 개 생김 | id가 호출마다 다름 | id를 enum으로 고정 |
| `SetValue`를 호출했는데 아무 일도 없음 | 아직 `AddOrUpdate`로 만들지 않음 | `Contains(id)` 확인 후 먼저 추가 |
| 갱신 연출이 안 나옴 | `StackHits`/`StackThreshold`에 `AddOrUpdate` 재호출 | 스택형은 `SetValue`로 값만 바꿈(`AddOrUpdate`는 지속형 갱신용) |
| 마을 다녀오니 버프가 사라짐 | 마을 진입 시 `Clear()`가 호출됨 | 던전 진입 후 필요한 버프를 다시 추가 |
| `BuffBar`가 null | 조합 프리팹(`HUD_StaminaGroup`)이 아닌 단독 스태미나 프리팹 사용 | `HUD_UI.prefab`의 `hudSteminaBarPrefab`이 조합 프리팹인지 확인 |
