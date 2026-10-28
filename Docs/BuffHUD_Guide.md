# 버프 HUD 사용 가이드

스태미나 바 위에 뜨는 **버프 아이콘 줄**(`HUD_BuffBar`)을 쓰는 방법입니다.
게임 로직(윗단)이 "무슨 버프가 어떤 상태인지"를 알려 주면, HUD가 등장·갱신·퇴장 연출을 알아서 그립니다.

> 현재 상태 (2026-10-28 기준)
> - **HUD 쪽은 완성**: 호출 API, 연출, 아이콘 6종(id 테이블), 종류 4가지(`Duration`/`StackHits`/`StackThreshold`/`Status`).
> - **윗단 연결은 아직 없음**: 게임 로직에서 HUD로 신호를 보내는 코드가 없습니다. 지금 화면에 뜨는 버프는 전부 임시 테스트(`HUD_BuffRandomTester`)가 만든 가짜입니다.
> - **연결하는 분은 5장(버프별 연결 명세)부터 보세요.** 6장이 권장 구조, 8장이 연결이 끝난 뒤 임시 테스트를 지우는 절차입니다.

### 연결 담당자 빠른 시작

1. 5장 표에서 연결할 버프의 **HUD 호출**과 **로직 쪽 훅 위치**를 확인합니다.
2. 로직 쪽에서 상태가 바뀌는 순간(켜짐/값 변경/꺼짐)에 이벤트를 내고, `UIView_HUD.BuffBar`에서 `AddOrUpdate` / `SetValue` / `Dispel`을 부릅니다.
3. 버프 id는 `(int)EBuffHudId.xxx`, 아이콘은 비워 두면 id 테이블이 채웁니다. 스프라이트를 직접 다룰 필요가 없습니다.
4. 6종이 다 붙으면 **8장의 체크리스트**대로 임시 테스트를 삭제합니다.

---

## 1. 한눈에 보기

```
[게임 로직 (Application / 컴포넌트)]
        │  신호 (버프 켜짐 / 값 변경 / 해제)
        ▼
[GameplayUICoordinator 또는 Character 이벤트]  ──►  [UIView_HUD.BuffBar]  ──►  [HUD_BuffBar]  ──►  [HUD_BuffIcon × N]
                                                        (접근자)                 (목록/배치/입자)        (칸 하나의 연출)
```

- 윗단은 **`BuffDisplayInfo` 한 덩어리와 id**만 알면 됩니다. 아이콘 칸 만들기, 위치, 슬라이드, 입자, 숫자 그리기는 전부 HUD가 합니다.
- 버프 하나 = **정수 `id` 하나**입니다. 같은 id로 다시 `AddOrUpdate`하면 새로 만들지 않고 갱신합니다.
- 상한: 한 번에 최대 **16개**(풀 크기 `poolCapacity`). 넘으면 경고 로그만 남기고 무시합니다.

관련 파일

| 파일 | 역할 |
|---|---|
| [`HUD_BuffBar.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/HUD_BuffBar.cs) | 공개 API, 목록/배치/오버플로, 입자 오버레이, 아이콘 테이블 조회 |
| [`HUD_BuffIcon.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/HUD_BuffIcon.cs) | 칸 하나의 연출(등장/갱신/퇴장/만료 점멸/숫자/게이지) |
| [`BuffDisplayInfo.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/BuffDisplayInfo.cs) | 표시 정보 구조체, 종류 열거형(`BuffGaugeKind`) |
| [`EBuffHudId.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/EBuffHudId.cs) | 버프 id 열거형 |
| [`HUD_BuffIconTable.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/HUD_BuffIconTable.cs) | id → 아이콘 스프라이트 테이블(ScriptableObject) |
| [`UIView_HUD.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/UIView_HUD.cs) | `BuffBar` 접근자, 마을에서 숨기기, 로그라이크에서 숨기기 |
| 프리팹 | `Assets/Prefabs/UI/HUD/Buff/HUD_BuffBar.prefab`, `HUD_BuffIcon.prefab`, `HUD_StaminaGroup.prefab` |

---

## 2. 속성별로 어떤 종류를 쓰나 (가장 중요)

버프의 **성질**을 보고 종류(`BuffGaugeKind`)를 고릅니다.

| 버프의 성질 | 종류 | 게이지가 뜻하는 것 | 숫자 | 퇴장 시점 |
|---|---|---|---|---|
| 일정 시간 동안 유지, 다시 걸리면 시간이 처음부터 | **`Duration`** | 남은 시간 비율(줄어듦) | 없음 | 시간이 0이 되면 자동 |
| 횟수/타수가 쌓이고, 쌓인 만큼 효과가 커짐(상한 있음) | **`StackHits`** | 현재 중첩 / 최대 중첩 | 중첩 수 | 중첩이 0이 될 때 |
| 수치가 연속적으로 쌓이다가 **기준값에 닿으면 발동** | **`StackThreshold`** | 현재 값 / 발동 수치(가득 차면 발동) | 현재 값 | 값이 0까지 줄면 |
| 켜져 있는 동안만 의미가 있고 값·시간이 없음(범위 안에 있는 동안 등) | **`Status`** | 가득 찬 채 고정 | 없음 | **`Dispel`/`Remove`를 부를 때만** |

색은 종류별로 구분됩니다: `Duration`=초록(25% 아래는 붉은색 경고), `StackHits`=금색, `StackThreshold`=청록, `Status`=파랑.

### 이 프로젝트 6종 매핑

| 버프 | 기획 | 종류 | 윗단이 알려 줄 것 |
|---|---|---|---|
| **벌목 리듬** | 도끼 타격 시 공격속도 N% 상승, 10중첩, 3초 유지(중첩 갱신 시 시간 갱신) | **`StackHits`** (max 10) | 중첩 수가 바뀔 때마다 값, 0이 되면 해제 |
| **속도의 원천** | 벌목 시 3초간 이동속도 증가 | **`Duration`** (3초) | 발동할 때마다(재발동 포함) |
| **휴식** | 오프로드 차량 근처에서 피로도 지속 회복 | **`Status`** | 범위에 들어가면 켜짐, 나가면 해제 |
| **별길 걸음** | 별 표식 나무 벌목 시 5초간 이동속도 추가 증가 | **`Duration`** (5초) | 발동할 때마다(재발동 포함) |
| **과열** | 0~100 | **미정** (5장 참고) | 값 정의가 정해져야 함 |
| **유성우(별비)** | 스택 0~100 | **`StackThreshold`** (max 100) | 충전 값이 바뀔 때마다 |

> 체력의 원천(`SourceOfStamina1`)은 나무를 벨 때 **즉시** 회복하고 남는 효과가 없어 버프 HUD 대상이 아닙니다(회복 연출은 스태미나 바가 처리).

> 속도의 원천을 "중첩되는 버프"로 바꾸려면(예: 발동마다 +1, 시간이 지나면 하나씩 감소) `StackHits`에 `decayDelay`/`decayInterval`을 주면 HUD가 단계적으로 줄이고 숫자가 내려가며 붉은 점이 떨어지는 감소 연출까지 그려 줍니다. 3.3절을 보세요.

---

## 3. 표시 정보와 호출 규칙

### 3.1 `BuffDisplayInfo`

| 필드 | 쓰는 종류 | 의미 |
|---|---|---|
| `id` | 공통 | 버프 식별자(정수). 호출 전부 이 값으로 대상을 찾음. `(int)EBuffHudId.xxx` |
| `kind` | 공통 | `Duration` / `StackHits` / `StackThreshold` / `Status` |
| `icon` | 공통 | **비워 두세요.** 비어 있으면 `HUD_BuffBar`의 아이콘 테이블이 `id`로 채우고, 테이블에도 없으면 임시 아이콘(id를 3으로 나눈 나머지)을 씁니다. 직접 넣을 땐 16×16 캔버스(그림은 가운데 12×12, Point 필터) |
| `accentColor` | 공통 | 프레임 윗줄 색. 알파 0이면 종류별 기본색 |
| `duration` | Duration | 지속 시간(초) |
| `maxValue` | 스택형 | 값의 상한(StackHits는 게이지가 가득 차는 값). `StackThreshold`는 임계점 이상으로 자동 보정 |
| `initialValue` | 스택형 | 시작 값 |
| `thresholdValue` | StackThreshold | 발동 수치. **게이지가 가득 차는 지점이 곧 임계점**. 0이면 `maxValue`를 사용 |
| `decayDelay` | 스택형 | 마지막 증가 후 이 시간(초) 지나면 감소 시작 |
| `decayInterval` | StackHits | 초마다 1씩 감소(0이면 자동 감소 없음) |
| `decayPerSecond` | StackThreshold | 초당 감소량(0이면 자동 감소 없음) |

`Status`는 위 값 중 `id`/`kind`(와 선택적으로 `accentColor`)만 씁니다.

### 3.2 공개 API (`HUD_BuffBar`)

| 메서드 | 하는 일 | 언제 쓰나 |
|---|---|---|
| `AddOrUpdate(in info)` | 없으면 **등장 연출**과 함께 추가, 있으면 갱신 | 버프가 걸리는 순간, 지속형이 다시 걸릴 때 |
| `SetValue(id, value)` | 스택형: 현재 값, 지속형: **남은 시간(초)** 설정. 상태형은 무시 | 중첩이 바뀔 때, 시간 보정이 필요할 때 |
| `Trigger(id)` | 발동 연출(불꽃 + 번쩍) 수동 재생 | 임계 돌파가 HUD가 모르는 사건일 때 |
| `Dispel(id)` | **퇴장 연출**과 함께 제거 | 해제/만료가 외부 사건일 때, 상태형이 꺼질 때 |
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

**`Status`**
- 게이지는 가득 찬 파란색으로 고정, 숫자 없음, 만료 점멸 없음. 윗단이 `Dispel`/`Remove`/`Clear`를 부르기 전까지 계속 보입니다.
- 같은 id로 `AddOrUpdate`를 다시 불러도 아무 일도 일어나지 않습니다(중복 호출 안전).

---

## 4. 버프 id, 아이콘, 사용 예시

id는 [`EBuffHudId.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/EBuffHudId.cs)에 모여 있습니다.

| id | 버프 | 아이콘 |
|---|---|---|
| `AttackRythm` = 1 | 벌목 리듬 | `Buff_AttackRythm.png` |
| `SourceOfSpeed` = 2 | 속도의 원천 | `Buff_SourceOfSpeed.png` |
| `StaminaRecover` = 3 | 휴식 | `Buff_StaminaRecover.png` |
| `StarPathSpeedBoost` = 4 | 별길 걸음 | `Buff_StarPathSpeedBoost.png` |
| `OverHeat` = 5 | 과열 | `Buff_OverHeat.png` |
| `StarRain` = 6 | 별비(유성우) | `Buff_StarRain.png` |

**아이콘 테이블**: [`HUD_BuffIconTable`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/HUD_BuffIconTable.cs)(에셋 `Assets/Prefabs/UI/HUD/Buff/HUD_BuffIconTable.asset`)이 id → 스프라이트를 들고 있고 `HUD_BuffBar.prefab`의 `iconTable`에 연결되어 있습니다.
윗단은 `BuffDisplayInfo.icon`을 **비워 두고** `id = (int)EBuffHudId.xxx`만 넣으면 됩니다. 새 버프 아이콘을 추가할 때는 `EBuffHudId`에 값을 넣고 `Buff_{enum 이름}.png`를 `Assets/Graphics/HUD/Buff/BuffIcon/`에 둔 뒤 메뉴 `LumberBoy/HUD/Build Buff Icon Table`을 실행하면 테이블이 다시 채워집니다(프리팹은 건드리지 않음).

### 4.1 벌목 리듬 — 중첩(StackHits) + 일괄 만료

```csharp
HUD_BuffBar _bar = hudView.BuffBar;
if (null == _bar) return;

// 첫 공격 성공: 1중첩으로 등장
BuffDisplayInfo _info = new BuffDisplayInfo();
_info.id = (int)EBuffHudId.AttackRythm;
_info.kind = BuffGaugeKind.StackHits;
// _info.icon은 비워 두면 아이콘 테이블이 채움
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

### 4.2 속도의 원천 / 별길 걸음 — 지속(Duration)

```csharp
BuffDisplayInfo _info = new BuffDisplayInfo();
_info.id = (int)EBuffHudId.SourceOfSpeed;   // 별길 걸음은 StarPathSpeedBoost, duration 5
_info.kind = BuffGaugeKind.Duration;
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

### 4.4 휴식 — 상태(Status)

```csharp
// 휴식 범위에 들어감
BuffDisplayInfo _info = new BuffDisplayInfo();
_info.id = (int)EBuffHudId.StaminaRecover;
_info.kind = BuffGaugeKind.Status;
hudView.BuffBar.AddOrUpdate(_info);

// 범위를 나감(또는 사망/특성 미개방/컴포넌트 비활성)
hudView.BuffBar.Dispel((int)EBuffHudId.StaminaRecover);
```

---

## 5. 버프별 연결 명세 (연결 담당자용)

각 버프마다 **로직 쪽에서 어디를 잡아야 하는지**와 **HUD에 무엇을 부르면 되는지**입니다. 위치는 2026-10-28 기준 코드를 읽고 확인한 것입니다(줄 번호는 바뀔 수 있으니 메서드 이름으로 찾으세요).

### 공통 주의

- **로그라이크에서는 버프 줄이 숨겨집니다**(`UIView_HUD.SetBuffHiddenForRun`). 본편 전용으로 연결하면 됩니다.
- **마을에서는 `Clear()` 후 비활성화**됩니다. 던전에 다시 들어와도 이전 버프는 복원되지 않으므로, 던전 안에서 상태가 이어지는 버프는 입장 시 다시 `AddOrUpdate`해야 합니다.
- **새 이벤트가 필요한 곳이 대부분입니다.** 현재 6종 중 어느 것도 HUD가 쓸 수 있는 "변경 알림"이 없습니다(아래 표의 "지금 있는 것" 참고).

### 5.1 벌목 리듬 (`AttackRythm`, `StackHits`)

| 항목 | 내용 |
|---|---|
| 훅 위치 | [`AxeComponent.cs`](../Assets/Scripts/Presentation%20Layer/ComponentSystem/ArmComponent/Axe/AxeComponent.cs): `DecreaseDurabilityInternal`(타격 성공 시 `attackComboStack` +1, 최대 10, 타이머 3초로 갱신), `Update`(3초 경과 시 0으로 초기화), `ResetAttackState`(강제 초기화) |
| 지금 있는 것 | `attackComboStack`은 private이고 변경 이벤트가 없습니다 |
| 필요한 이벤트 | 스택 값이 바뀔 때(+1, 0으로 초기화) 현재 값을 알리는 이벤트 |
| HUD 호출 | 값 > 0이고 `Contains` 아님 → `AddOrUpdate(StackHits, max 10, initial 값)`. 이후 값 변경 → `SetValue(값)`. 0 → `SetValue(0)`(퇴장) |
| **주의 1** | 콤보는 **특성이 없어도 쌓입니다**(조건 없이 +1). HUD에는 `ctx.characterStat.attackRythmSpeedMul > 0`일 때만 알려야 합니다 |
| 주의 2 | 10중첩에서 또 쳐도 값이 같아서 HUD가 반응하지 않습니다(3초 갱신은 화면에 안 보임) |
| 주의 3 | `ShockWave`가 나무를 맞췄을 때(`DecreaseDurabilityWithoutCombo`)는 콤보가 쌓이지 않으니 알릴 필요 없음 |

### 5.2 속도의 원천 (`SourceOfSpeed`, `Duration` 3초)

| 항목 | 내용 |
|---|---|
| 훅 위치 | [`StatComponent.cs`](../Assets/Scripts/Presentation%20Layer/ComponentSystem/StatComponent/StatComponent.cs): `ActivateSourceOfSpeed`(발동, 타이머 3초), `SourceOfSpeedRoutine`(종료), `Reset`(강제 종료) |
| 지금 있는 것 | `HasteStartedEvent`/`HasteEndedEvent`가 있지만 **쓰면 안 됩니다**. 속도의 원천과 별길 걸음을 구분하지 못하고, 이미 켜져 있을 때 재발동(타이머만 3초로 리셋)은 이벤트가 오지 않습니다. 이 이벤트는 캐릭터 이펙트용입니다 |
| 필요한 이벤트 | 발동(재발동 포함)과 강제 종료를 각각 알리는 이벤트 |
| HUD 호출 | 발동마다 `AddOrUpdate(Duration, 3초)`(재발동이면 갱신 연출). 강제 종료(`Reset`)만 `Dispel`. 정상 만료는 HUD가 알아서 퇴장 |
| 주의 | 특성이 없으면(`sourceOfSpeedAmount <= 0`) `ActivateSourceOfSpeed`가 바로 반환하므로 이벤트도 나가지 않게 같은 위치에서 내면 됩니다 |

### 5.3 휴식 (`StaminaRecover`, `Status`)

| 항목 | 내용 |
|---|---|
| 훅 위치 | [`StaminaRecoverCircle.cs`](../Assets/Scripts/Presentation%20Layer/ObjectSystem/StaminaRecoverCircle.cs): `Update`에서 `bIsCharacterInside`가 true가 되는 지점(진입), `ExitRestArea`(이탈/사망/미개방/비활성) |
| 지금 있는 것 | `pHealthComponent.SetInRestArea(bool)`만 있고 HUD로 가는 알림은 없습니다 |
| 필요한 이벤트 | 진입/이탈 두 가지(bool) |
| HUD 호출 | 진입 → `AddOrUpdate(Status)`, 이탈 → `Dispel` |
| 주의 | 이 컴포넌트는 **차량 오브젝트에 붙어 있고** 던전마다 초기화됩니다. 휴식 특성이 없으면(`staminaRecoverAmount <= 0`) 진입 판정 자체가 일어나지 않으므로 그대로 두면 됩니다. 컴포넌트가 꺼질 때(`OnDisable`)도 `ExitRestArea`가 불리니 이탈 이벤트가 빠지지 않습니다 |

### 5.4 별길 걸음 (`StarPathSpeedBoost`, `Duration` 5초)

| 항목 | 내용 |
|---|---|
| 훅 위치 | `StatComponent.cs`: `ActivateStarPathSpeedBoost`(발동, 타이머 5초), `StarPathSpeedRoutine`(종료), `Reset`(강제 종료) |
| 나머지 | **5.2와 동일**하게 처리합니다(duration만 5초) |

### 5.5 과열 (`OverHeat`) — 값 정의가 미정

| 항목 | 내용 |
|---|---|
| 훅 위치 | [`OverheatComponent.cs`](../Assets/Scripts/Presentation%20Layer/ComponentSystem/OverheatComponent/OverheatComponent.cs): `AddOverheatDuration`/`AddOverheatDurationRaw`(지속시간 합산), `ActivateBuff`/`DeactivateBuff`, `ForceEnd`, `TryActivatePermanent`(화신) |
| 지금 있는 것 | `OverheatStateChangedEvent(bool)`(켜짐/꺼짐)과 `IsActive`. **0~100 같은 값은 없습니다** |
| 실제 값의 성격 | `overheatTimer`는 **초 단위, 상한 없음**. 열기를 얻을 때마다 합산되고, 매 프레임 `deltaTime × (1 − 과열 유지 감소율)`만큼 줄어듭니다. "화신" 특성이면 줄지 않습니다 |
| **결정 필요** | 기획의 "0~100"이 무엇인지 정해야 합니다. 후보: ① 남은 시간을 그대로 `Duration`으로(합산할 때마다 `AddOrUpdate`로 갱신 연출, 분모는 직전 합산 시점의 값) ② 상한 N초를 두고 남은 시간/N × 100을 `StackThreshold`/`StackHits`로 ③ 화신 상태만 `Status`로 별도 표시 |
| 주의 | 소모 속도가 `과열 유지` 특성으로 느려지므로 HUD 자체 타이머(1배속)와 어긋납니다. 후보 ①이면 주기적으로 `SetValue(남은 초)` 보정이 필요합니다 |

### 5.6 유성우 (`StarRain`, `StackThreshold` 0~100)

| 항목 | 내용 |
|---|---|
| 훅 위치 | [`InDungeonObjectManager.cs`](../Assets/Scripts/Application%20Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs): `ChargeMeteorShower`(처치 시 충전 +0.1, 1.0이 되면 0으로 리셋 후 유성우 시작), `meteorShowerCharge`/`MeteorShowerCharge`(0~1) |
| 지금 있는 것 | `MeteorShowerStartedEvent`(발동 시작만)과 `MeteorImpactEvent`. **충전 값이 바뀔 때의 이벤트는 없습니다** |
| 필요한 이벤트 | 충전 값이 바뀔 때(그리고 0으로 리셋될 때) 현재 값을 알리는 이벤트 |
| HUD 호출 | 충전 > 0이고 `Contains` 아님 → `AddOrUpdate(StackThreshold, max 100, threshold 100, initial 값×100)`. 이후 `SetValue(값×100)`. 발동 순간 100에 닿으면 HUD가 발동 연출을 냅니다 |
| **주의 1** | 발동하면 값이 0이 되므로 **아이콘이 퇴장 연출로 사라지고**, 다음 처치에서 다시 등장합니다 |
| 주의 2 | 별빛 나침반 티어가 0이면(`starCompassTierThisRun <= 0`) 충전 자체가 없습니다. 유성우가 쏟아지는 동안(`bMeteorShowerActive`)은 충전되지 않습니다 |
| 주의 3 | 던전 시작/종료 때 충전이 0으로 리셋됩니다(`meteorShowerCharge = 0f`). 이때 `Remove`로 정리하세요 |
| **계층** | 이 값은 **Application Layer**에 있어서 HUD가 직접 볼 수 없습니다. 기존 패턴(예: `TreeGetHitEvent` → `InDungeonSystem` → Signal → `GameplayUICoordinator`)대로 중계해야 합니다 |

---

## 6. 윗단에서 신호를 내려 주는 권장 구조 (미구현)

이 프로젝트의 UI 흐름은 `신호 → GameplayUICoordinator → UIView` 입니다. 대시 게이지는 다른 방식으로, `ICharacter.dashState`(`IDashState` 인터페이스 + 이벤트)를 `UIView_HUD`가 직접 구독합니다([`UIView_HUD.BindDashState`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/UIView_HUD.cs)).
어느 쪽으로 가든 **HUD에 부르는 호출(3.2절)은 같습니다.** 구조는 로직 담당과 UI 담당이 정하세요.

### 구조 A (권장): Presentation 쪽 5종은 대시 방식, 유성우만 신호

- 벌목 리듬/속도의 원천/휴식/별길 걸음/과열은 모두 **Presentation Layer**(컴포넌트)에 있고 `Character`가 소유합니다. `ICharacter`에 읽기 전용 인터페이스(예: `IBuffHudState`: 변경 이벤트)를 추가하고 `UIView_HUD.SetCharacter`에서 대시처럼 바인딩합니다. 이 경우 Application Layer를 건드리지 않습니다.
- 유성우만 Application Layer이므로 `InDungeonObjectManager` 이벤트 → `InDungeonSystem`이 Signal 발행 → `GameplayUICoordinator` 구독 → `UIView_HUD`로 전달합니다.

### 구조 B: 전부 신호로 통일

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

```csharp
// GameplayUICoordinator
private void BuffHud(BuffHudSignal _s) { hudUI.ApplyBuffHud(in _s); }

// UIView_HUD
public void ApplyBuffHud(in BuffHudSignal _s)
{
    if (null == hudBuffBar) return;
    switch (_s.op)
    {
        case EBuffHudOp.Add:      /* BuffDisplayInfo 채워서 AddOrUpdate */ break;
        case EBuffHudOp.SetValue: hudBuffBar.SetValue(_s.id, _s.value); break;
        case EBuffHudOp.Dispel:   hudBuffBar.Dispel(_s.id); break;
        // ...
    }
}
```

- 아이콘 스프라이트는 신호에 담지 않습니다. id 테이블이 HUD 쪽에 있으므로 신호는 id만 알면 됩니다.
- 상태가 바뀔 때와 값이 바뀔 때만 이벤트/신호를 냅니다(매 프레임 X).
- Application Layer는 UI 담당이 수정하지 않는 분담이므로, 이벤트/신호 정의와 발행은 로직 담당이, 구독과 HUD 호출은 UI 담당이 맡는 것을 권장합니다.

---

## 7. 알아 둘 동작

- **마을에서는 숨김**: `UIView_HUD`가 마을 진입 때 `BuffBar.Clear()` 후 비활성화합니다. 던전에 다시 들어와도 **이전 버프는 복원되지 않습니다.** 필요한 버프는 윗단이 던전 진입 시 다시 `AddOrUpdate`해야 합니다.
- **줄 배치**: 칸 폭 18, 간격 20(피치 20). 스태미나 바 폭(160)에 맞춰 **최대 2줄**.
  - 대시 게이지가 보이면 첫 줄 오른쪽을 비움: 대시 2칸 → **첫 줄 5칸**, 1칸 → 6칸, 대시 없음 → 8칸. 둘째 줄은 항상 8칸.
  - 두 줄이 모두 차면 마지막 칸이 `+N`으로 바뀌고 넘친 버프는 숨겨짐(타이머는 계속 진행).
- **시간 기준**: 지속 시간 타이머는 `Time.deltaTime`입니다. 시간 정지(`timeScale 0`) 중에는 HUD 타이머도 멈춥니다.
- **알파 페이드 없음**: 외곽선이 겹쳐 보이는 문제 때문에 등장/퇴장은 크기 단계, 색, 점멸로만 표현합니다.
- **아이콘 규격**: 16×16 캔버스에 그림은 가운데 12×12(흰 외곽선 1px 포함, 프레임 우물 크기), Point 필터, 불투명 픽셀 위주. 가장자리 2px는 투명으로 둡니다. 임포트 시 `.meta`에 남는 trim rect(14×14 등)는 무시되고 Unity는 16×16 전체로 읽습니다. 퇴장 때 불투명 픽셀 하나하나가 점으로 흩어지므로 안티앨리어싱(반투명 가장자리)이 있으면 지저분해집니다.
- **`Buff_StarRain.png`**: 그림이 12×13이라 다른 아이콘보다 위로 1px 올라가 프레임 안쪽 선에 닿을 수 있습니다(아트 확인 필요).

---

## 8. 테스트하는 법, 연결이 끝난 뒤 임시 테스트 지우기

### 8.1 지금의 임시 테스트

[`HUD_BuffRandomTester.cs`](../Assets/Scripts/Presentation%20Layer/UISystem/UIView/HUD/Buff/Temp/HUD_BuffRandomTester.cs)가 에디터와 개발 빌드에서 **플레이할 때마다 스스로 생성**되어, 1.5~4초 간격으로 id 0~7의 가짜 버프를 무작위로 추가·갱신·해제합니다(`RuntimeInitializeOnLoadMethod`). 그래서 **지금 HUD에 뜨는 버프는 실제 게임 상태가 아닙니다.**

- 임시로 끄려면 `Enabled`를 `false`로 바꿉니다.
- 프리팹 재생성이 필요하면 메뉴 `LumberBoy/HUD/Build Buff HUD Prefabs`를 실행합니다. **프레임 PNG와 프리팹을 전부 덮어쓰므로** 수작업으로 바꾼 부분이 있으면 쓰지 마세요. 아이콘 테이블만 다시 채우려면 `LumberBoy/HUD/Build Buff Icon Table`을 씁니다.

### 8.2 연결이 끝난 뒤 지울 것 (체크리스트)

실제 버프 신호가 붙으면 임시 테스트가 **실제 버프(id 1~6)와 섞여** 가짜 칸을 만들고 실제 칸을 해제하기도 합니다. 연결 작업을 시작할 때부터 꺼 두고, 연결이 끝나면 지우세요.

1. 연결 작업 중에는 `HUD_BuffRandomTester.Enabled`를 `false`로 둡니다.
2. 6종 연결 후 플레이로 확인합니다(실제 버프만 뜨는지, 마을을 다녀와도 정리되는지).
3. 폴더째 삭제합니다: `Assets/Scripts/Presentation Layer/UISystem/UIView/HUD/Buff/Temp/` (안의 `HUD_BuffRandomTester.cs`와 `.meta`, 폴더 `.meta` 포함).
4. 삭제 후 컴파일 에러가 없는지, 플레이 시 `[BuffTest]` 로그가 더 이상 나오지 않는지 확인합니다.
5. `HUD_BuffPixelUtil.GetTempIcon`(임시 아이콘)은 **지우지 않습니다.** 아이콘 테이블에 없는 id가 들어왔을 때의 안전망으로 `HUD_BuffIcon`이 씁니다. 안전망 없이 누락을 바로 알아채고 싶다면, 그때 `LoadIcon`에서 경고 로그로 바꾸는 것을 검토하세요.
6. 이 문서의 현재 상태 문구와 8.1을 정리합니다.

---

## 9. 자주 하는 실수

| 증상 | 원인 | 해결 |
|---|---|---|
| 같은 버프가 칸이 두 개 생김 | id가 호출마다 다름 | id를 `EBuffHudId`로 고정 |
| `SetValue`를 호출했는데 아무 일도 없음 | 아직 `AddOrUpdate`로 만들지 않음 / `Status` 종류임 | `Contains(id)` 확인 후 먼저 추가. 상태형은 `SetValue`를 무시함 |
| 갱신 연출이 안 나옴 | `StackHits`/`StackThreshold`에 `AddOrUpdate` 재호출 | 스택형은 `SetValue`로 값만 바꿈(`AddOrUpdate`는 지속형 갱신용) |
| 마을 다녀오니 버프가 사라짐 | 마을 진입 시 `Clear()`가 호출됨 | 던전 진입 후 필요한 버프를 다시 추가 |
| `BuffBar`가 null | 조합 프리팹(`HUD_StaminaGroup`)이 아닌 단독 스태미나 프리팹 사용 | `HUD_UI.prefab`의 `hudSteminaBarPrefab`이 조합 프리팹인지 확인 |
| 존재하지 않는 버프가 계속 뜸 | 임시 테스트가 아직 살아 있음 | 8장 체크리스트로 끄고 삭제 |
| 아이콘이 임시 그림(초록/주황/붉은 도형)으로 보임 | id가 `EBuffHudId` 밖이거나 아이콘 테이블에 없음 | id 확인, 테이블 재생성 메뉴 실행 |
| 벌목 리듬이 특성 없이도 뜸 | 콤보는 특성과 무관하게 쌓임 | `attackRythmSpeedMul > 0`일 때만 알림 |
