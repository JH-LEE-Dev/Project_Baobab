# 체력바 무적 연출 UI 가이드

체력바 무적 연출은 `HUD_Stemina.PlayInvincibleEffect(bool)` 한 곳만 채우면 됩니다. 언제 켜고 끌지, 마을에서 막기, 중복 호출 거르기는 코드에서 이미 처리되어 있습니다.

- 기준 커밋: `6b618f9a` (`NewNewLee` 브랜치)
- 검증 수준: 코드 흐름만 확인했습니다. Unity 컴파일과 플레이 테스트는 아직 하지 않았습니다.

---

## 1. 작업할 곳

파일은 `Assets/Scripts/Presentation Layer/UISystem/UIView/HUD/Stemina/HUD_Stemina.cs` 하나입니다. 지금은 아래처럼 TODO만 있는 빈 함수입니다.

```csharp
private void PlayInvincibleEffect(bool _bOn)
{
    // TODO: 무적 연출 (켜짐: 연출 시작 / 꺼짐: 연출 종료)
}
```

- `_bOn == true`: 무적 연출을 시작합니다(반복 연출이면 루프 시작).
- `_bOn == false`: 무적 연출을 끝내고 체력바를 평소 모습으로 되돌립니다.
- 연출에 필요한 참조(이미지, 이펙트, 모션 태그)는 이 클래스에 `[SerializeField]`로 추가하고 HUD_Stemina 프리팹에 연결하면 됩니다. 기존 `motionPlayer`(ObjectMotionPlayer)를 써도 됩니다.

건드리지 않아도 되는 부분:

- `SetInvincible`, `ResetInvincible`, `IsInvincible`: 켜고 끄는 판단을 이미 하고 있습니다. 연출 코드는 여기 넣지 마세요.
- `UIView_HUD`, `GameplayUICoordinator`, 신호 정의: 신호를 전달만 합니다.

---

## 2. 연출이 켜지고 꺼지는 타이밍

무적 원인은 두 가지이고, 둘 중 하나라도 켜져 있으면 연출이 켜집니다. `PlayInvincibleEffect`는 이 합친 상태가 실제로 바뀔 때만 불립니다.

| 원인 | 켜지는 순간 | 꺼지는 순간 |
|---|---|---|
| 숲 입장 직후 | HUD가 올라오기 시작하는 프레임 | 약 0.7초 뒤 스태미나가 실제로 닳기 시작하는 프레임. 그 사이 메인메뉴로 나가면 나가는 순간 |
| 휴식 구역("휴식" 특성) | 차량 주변 타원 안에 들어갈 때 | 원 밖으로 나갈 때, 쓰러졌을 때, 차량이 정리될 때 |

숲에 들어가는 흐름에서는 두 원인이 거의 항상 겹칩니다. 캐릭터 스폰 지점이 휴식 구역 안이기 때문입니다.

1. 캐릭터가 숲에 도착합니다. "휴식" 특성이 있으면 이때 이미 연출이 켜집니다. HUD는 아직 화면 밖에 있습니다.
2. HUD가 올라오면서 입장 무적이 켜집니다. 특성이 없으면 여기서 연출이 처음 켜집니다.
3. 약 0.7초 뒤 입장 무적이 끝납니다. 휴식 구역 안이면 연출이 그대로 이어지고, 밖이면 꺼집니다.
4. 휴식 구역을 벗어나는 순간 꺼지고, 다시 들어가면 다시 켜집니다.

마을에서는 연출이 절대 켜지지 않습니다. 마을 차량에도 같은 휴식 구역이 있어 신호는 오지만, 무시하도록 되어 있습니다. 마을/숲으로 넘어갈 때마다(`SetActivate`) 무적 상태와 연출도 초기화됩니다.

---

## 3. 구현 시 주의사항

- **켜짐/꺼짐은 번갈아 옵니다.** 켜짐이 두 번 연속으로 오지는 않습니다. 그래도 다시 시작해도 깨지지 않게(예: `bReset: true`) 만들어 두면 안전합니다.
- **HUD가 화면 밖일 때도 불립니다.** 숲 도착 직후(HUD 올라오기 전)와 귀환 중(HUD 내려가는 중)에도 켜지고 꺼집니다. 체력바가 보이는지에 기대지 말고, 받은 대로 켜고 끄면 됩니다.
- **꺼짐은 연출 도중에도 옵니다.** 입장 무적은 0.7초뿐이고, 맵이 바뀔 때도 강제로 꺼집니다. 어느 시점에 꺼도 색·크기·투명도가 평소 값으로 제대로 돌아와야 합니다.
- **저피로도 경고 연출과 같이 켜질 수 있습니다.** 스태미나가 낮은 채로 휴식 구역에 들어가면, 기존 경고(`DangerShake`, `DangerColor` 모션 + 붉은 화면)와 무적 연출이 동시에 켜져 있습니다. 모션은 별도 태그로 두고, 하나를 끄면서 다른 하나까지 멈추지 않게 해 주세요. 같은 색을 건드린다면 어느 쪽이 우선인지 정해야 합니다.
- **참조는 Unity 방식으로 null 체크하세요.** 씬이 넘어가는 도중에도 꺼짐이 올 수 있습니다. `motionPlayer?.Play(...)`처럼 `?.`를 쓰면 이미 파괴된 오브젝트를 걸러내지 못하니, `if (null != motionPlayer)`로 확인해 주세요.
- **ESC 일시정지 중에는 연출이 멈춥니다(결정됨).** 일시정지는 `Time.timeScale = 0`입니다. 체력바 연출(프리즘, "+", 차오름, 잔상, 플래시·흔들림)은 모두 scaled time(`Time.deltaTime`, 트윈은 `SetUpdate(false)`)을 쓰므로, 일시정지 동안 그 자리에서 멈췄다가 풀리면 이어서 진행합니다. 기존 저피로도 경고 모션도 scaled time이라 함께 멈춥니다. 연출을 새로 추가할 때도 `unscaledDeltaTime`이나 `SetUpdate(true)`는 쓰지 마세요.

기획 확인 필요: "휴식" 특성이 있으면 귀환 경고창이 떠 있는 동안과 귀환 탑승 중에도 연출이 켜져 있습니다(차량 옆이라 실제로 무적). 이 구간에 연출을 숨기고 싶으면 프로그래머에게 요청해 주세요.

---

## 4. 확인 시나리오

"휴식" 특성을 찍기 전과 후를 모두 확인해 주세요.

- [ ] 특성 없음, 마을 → 숲: HUD가 올라올 때 켜지고 약 0.7초 뒤 꺼짐
- [ ] 특성 있음, 마을 → 숲: HUD가 올라올 때 이미 켜져 있고, 차량 주변 원을 벗어날 때 꺼짐
- [ ] 특성 있음: 원 안팎을 여러 번 드나들면 그때마다 켜지고 꺼짐
- [ ] 특성 있음, 마을: 차량 옆에 가도 연출이 켜지지 않음
- [ ] 사망 후 재도전: 새 숲에서 첫 입장과 같게 동작
- [ ] 숲 → 마을 귀환: 마을에 도착했을 때 연출이 꺼져 있음
- [ ] HUD가 올라온 직후 0.7초 안에 ESC → 메인메뉴: 연출이 남지 않고, 다음 판에도 이어지지 않음
- [ ] 스태미나가 낮은 채로 원에 들어감: 저피로도 경고와 무적 연출이 서로 꺼뜨리지 않음
- [ ] 튜토리얼 첫 입장(메인메뉴 → 숲): 하차 후 HUD가 올라올 때 켜지고 꺼짐

---

## 5. 참고: 신호 경로

모든 신호는 `StaminaInvincibleStateChangedSignal(EStaminaInvincibleSource source, bool bInvincible)` 하나로 옵니다.

| 원인 | 발행 위치 | UI까지 경로 |
|---|---|---|
| `DungeonEntry` | `UnitSystem` (`ActivateCharacter` / `StartDecreaseStamina` / `GoToMainMenuRequested`) | `GameplayUICoordinator` → `UIView_HUD` → `HUD_Stemina.SetInvincible` |
| `RestArea` | `StaminaRecoverCircle` → `PHealthComponent.SetInRestArea` → `Character` → `UnitLogicManager` → `UnitSystem` | 위와 같음 |

신호 정의는 `Assets/Scripts/Application Layer/SignalSystem/Signals/TownSystemSignals.cs`에 있습니다.
