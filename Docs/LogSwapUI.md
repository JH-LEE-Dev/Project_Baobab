# 원목 교체 시스템 - UI 연동 가이드

로직/데이터는 전부 들어가 있습니다. **UI에서 할 일은 "어느 슬롯이 무엇과 바뀌는지"를 환율로 보여주고,
"교체가 일어났을 때" 연출을 붙이는 것**입니다. 이 문서는 그 두 가지에 필요한 값이 어디로 오는지만
설명합니다.

---

## 1. 기능 한 줄 요약

가방(또는 운반 상자)에 자리가 없어 원목을 더 못 담을 때, **교체 키(기본 Tab / 패드 Y)** 를 누르면
값싼 슬롯 하나를 통째로 내려놓아 자리를 만듭니다.

- 버려질 슬롯은 시스템이 고릅니다(유저가 고르지 않습니다). 누를지는 유저가 정합니다.
- **교체는 인벤토리가 열려 있을 때만 동작합니다.** 무엇이 버려지는지 눈으로 볼 수 없는 상태에서
  슬롯이 사라지면 안 되기 때문입니다.
- 버릴 슬롯이 없으면 교체는 활성화되지 않습니다.

### 시스템이 보장하는 것 (UI 문구의 근거)

> **(1) 손해인 거래는 아예 제안하지 않는다.  (2) 제안은 환율 한 줄로 설명된다.**

버튼이 떠 있다는 것 자체가 "이건 손해 아님"이라는 보증입니다. 그래서 UI는 이득이라고 **주장할 필요가
없고**, 유저가 셀 수 있는 두 가지(개수·종류)와 환율만 보여주면 됩니다.

시스템이 지키는 규칙(참고용, UI가 계산할 필요는 없습니다):

| 규칙 | 내용 |
|---|---|
| 자격 | 버릴 슬롯의 개당 가치 < 들어올 원목의 개당 가치. **보석 등급(황금/다이아/프리즘) 슬롯은 절대 버리지 않음** |
| 상한 | 버릴 슬롯의 총 가치 ≤ 들어올 원목 개당 가치 × min(지금 보이는 개수, 슬롯 최대 중첩). **"지금 눈에 보이는 만큼"만 이득으로 침** |
| 선정 | 자격·상한을 통과한 슬롯 중 총 가치가 가장 낮은 것("가장 싸게 살 수 있는 칸"). 같으면 더 싼 수종 |

가장 싼 수종이 들어올 때는 자격을 만족하는 슬롯이 없어 제안이 없습니다 — "잡템 자리를 만들라"는
제안은 구조적으로 나오지 않습니다.

### 제안이 튀지 않도록

- **한 번 띄운 제안은 그것이 무효가 될 때까지 바뀌지 않습니다**(sticky). 0.2초마다 강조 슬롯이
  튀어다니면 "뜬 걸 눌렀는데 그 사이 바뀌어 있었다"가 생기기 때문입니다.
- **교체 직후 1초 동안은 다음 제안이 뜨지 않습니다.** 버린 원목이 내려앉고 새 원목이 들어오는 결과를
  눈으로 확인할 시간입니다.

---

## 2. 어디서 값을 받나

### 2-1. 인벤토리 (`UI_Inventory`)

```csharp
// 교체 대상이 달라졌을 때(생김 / 사라짐 / 다른 슬롯으로 이동 / 개수 변화 / 들어올 원목 변화)
public event Action LogSwapInfoChangedEvent;

// 교체가 실제로 일어나 슬롯 하나가 비워졌을 때. 인자는 "방금 버려진 슬롯"의 내용
public event Action<LogSwapSlotInfo> LogSwapExecutedEvent;

public LogSwapSlotInfo LogSwapInfo { get; }          // [인벤토리 교체] 버려질 가방 슬롯 + 들어올 원목
public ELogSwapTarget ActiveLogSwapTarget { get; }   // 교체 키가 실제로 건드릴 쪽
public bool IsLogSwapReady { get; }                  // 위 둘을 합친 판정(5장 참고)

public LogSwapSlotInfo OutgoingSwapInfo { get; }     // [운반 상자 교체] 상자로 넘어갈 가방 슬롯 (아래 참고)
public int OutgoingSlotIndex { get; }                // = OutgoingSwapInfo.incomingSlotIndex, 없으면 -1
public bool IsOutgoingSwapReady { get; }             // 지금 키를 누르면 이 가방 슬롯이 상자로 넘어가는지
```

가방 UI는 **두 가지 상황을 다르게 그려야 합니다.**

| 상황 | 가방 UI가 강조할 슬롯 | 뜻 |
|---|---|---|
| 인벤토리 교체 (`IsLogSwapReady`) | `LogSwapInfo.slotIndex` | 이 칸이 **버려짐** |
| 운반 상자 교체 (`IsOutgoingSwapReady`) | `OutgoingSlotIndex` | 이 칸이 **상자로 넘어감** (버려지는 건 상자 쪽 슬롯) |

둘은 동시에 true가 되지 않습니다(`ActiveLogSwapTarget`이 한쪽만 가리킵니다).

**교체 한 번 = 슬롯 하나가 빠지고 슬롯 하나가 들어옵니다.** 인벤토리 교체면 비운 칸에 바닥의 원목이 곧바로
흡입되고, 운반 상자 교체면 상자 슬롯을 비운 뒤 "넘어감"으로 표시된 가방 슬롯 하나가 곧바로 상자로
날아갑니다. 어느 쪽도 E를 다시 누를 필요가 없습니다. 상자 앞에 서면 상자 UI와
가방이 자동으로 열리는 그 순간 이 값들이 함께 들어옵니다 — **E를 누르기 전에** 뜹니다.

### 2-2. 이동식 운반 상자 (`UI_Storage` — `UIView_WorldPopup.ui_CarStorage` 인스턴스)

이름과 시그니처가 인벤토리와 완전히 같습니다.

```csharp
public event Action LogSwapInfoChangedEvent;
public event Action<LogSwapSlotInfo> LogSwapExecutedEvent;

public LogSwapSlotInfo LogSwapInfo { get; }
public ELogSwapTarget ActiveLogSwapTarget { get; }
public bool IsLogSwapReady { get; }
```

> `UI_Storage`는 마을의 나무 보관함에도 쓰이는 같은 클래스입니다. 교체는 이동식 운반 상자에만
> 있으므로, 마을 보관함 인스턴스에서는 이 값들이 계속 비어 있습니다(`bHasSlot == false`).

---

## 3. `LogSwapSlotInfo` — 버려질 슬롯과 들어올 원목

`Assets/Scripts/Application Layer/UnitSystem/LogSwap/LogSwapTypes.cs`

**버릴 쪽**

| 필드 | 설명 |
|---|---|
| `target` | `ELogSwapTarget.Inventory` / `OffroadContainer` / `None`(대상 없음) |
| `slotIndex` | **버려질 슬롯의 인덱스.** 없으면 `-1` |
| `treeType` / `logState` | 버려질 원목의 수종 / 등급 |
| `count` | 쌓여 있는 개수 = **교체하면 버려지는 원목 수** |
| `unitValue` | 개당 가치(코인 단위, 기본 가치 × 등급 배율). 코인 환산을 보여주고 싶을 때 |

**들어올 쪽**

| 필드 | 설명 |
|---|---|
| `incomingTreeType` / `incomingLogState` | 비운 자리에 들어올 원목의 수종 / 등급 |
| `incomingCount` | 지금 대기 중인 개수. 인벤토리면 **화면 안에 보이는 개수(공중에 떠 있는 것 포함)**, 운반 상자면 **가방에 든 개수**. 슬롯 최대 중첩을 넘지 않게 잘라서 옴 |
| `incomingUnitValue` | 들어올 원목의 개당 가치 |
| `incomingSlotIndex` | 들어올 원목이 **지금 있는 가방 슬롯**. 운반 상자 교체면 "교체 뒤 상자로 넘어갈 가방 슬롯"의 인덱스, 인벤토리 교체면 `-1`(바닥에 있음) |

**프로퍼티**

| 이름 | 설명 |
|---|---|
| `bHasSlot` | 버릴 슬롯이 정해져 있는지 |
| `exchangeRate` | **환율** = `incomingUnitValue / unitValue`. "들어올 원목 1개 = 버릴 원목 몇 개" |

**`slotIndex`는 UI가 이미 그리고 있는 슬롯 목록과 같은 인덱스입니다.**

- `UI_Inventory` → `inventory.inventorySlots[slotIndex]`
- `UI_Storage` → `storage.inventorySlots[slotIndex]`

버릴 쪽의 `treeType/logState/count`는 그 시점의 사본이라, **교체가 끝나 슬롯이 비워진 뒤에도**
(`LogSwapExecutedEvent`) 무엇이 몇 개 버려졌는지 그대로 읽을 수 있습니다.

---

## 4. 화면에 무엇을 보여주나 — 환율

이득이라고 **주장하지 말고**, 유저가 셀 수 있는 것만 나란히 놓습니다.

```
  내려놓기                자리 만들기
  소나무  ×3      ↔       자작나무  ×5          환율 3.3 : 1
  (슬롯 하이라이트)        (바닥에서 못 먹고 있는 것)
```

- 왼쪽: `treeType` / `count` — 실제 슬롯을 하이라이트해서 "저 3개구나"를 확인시킵니다.
- 오른쪽: `incomingTreeType` / `incomingCount` — 지금 못 먹고 있는 그 원목입니다.
- 환율: `exchangeRate` — "자작 하나가 소나무 셋 값". **숲마다 수종이 둘뿐이라 환율은 숲당 사실상
  하나**입니다. 한 번 보면 외워지는 숫자라, 유저가 시스템을 믿는 게 아니라 자기가 아는 사실로 판단하게
  됩니다.
- 보석이 끼면 환율이 커집니다("소나무 20개 ↔ 프리즘 자작 5개, 환율 67 : 1"). 숫자가 크지만 정직하고,
  그 크기 자체가 "이건 엄청난 거래"를 전달합니다.
- "이득입니다" 같은 문구는 쓰지 마세요. 종류와 개수와 환율만 보여주면 판단은 유저 몫이고, 그래서
  결과에 배신감이 없습니다.

`unitValue` / `incomingUnitValue`로 코인 환산도 붙일 수 있습니다. 제재소에서 본 숫자와 같은 단위입니다.
환율만으로 충분하면 생략해도 됩니다.

### 등급 표시

`logState`가 `Normal`보다 높으면 보석 원목(황금 = Fascinating / 다이아 = Advanced / 프리즘 = Perfect)
입니다. 들어올 쪽이 보석이면 그걸 눈에 띄게 해주세요 — 게임 안에서 보석 원목은 아우라와 전용 효과음으로
"특별한 것"으로 학습되어 있어, 같은 언어를 쓰면 환율의 큰 숫자가 자연스럽게 읽힙니다.
(버릴 쪽은 보석이 절대 오지 않습니다.)

---

## 5. `ActiveLogSwapTarget` — 왜 필요한가

인벤토리와 운반 상자 **양쪽 모두** 교체 후보를 가질 수 있습니다(상자 앞에 서 있는데 가방도 꽉 찬
경우). 이때 교체 키는 **운반 상자를 우선**으로 처리합니다.

- `LogSwapInfo.bHasSlot` → "이 창 기준으로 버려질 후보는 이 슬롯이다"
- `IsLogSwapReady` → "지금 키를 누르면 **이 창의** 그 슬롯이 실제로 버려진다"

가장 단순하게 가려면 `IsLogSwapReady`만 보고 켜고 끄면 됩니다.

---

## 6. 사용 예

```csharp
public class UI_LogSwapPrompt : MonoBehaviour
{
    [SerializeField] private UI_Inventory uiInventory;
    [SerializeField] private GameObject highlight;      // 버려질 슬롯 위에 얹을 테두리
    [SerializeField] private TextMeshProUGUI rateText;  // "소나무 ×3 ↔ 자작나무 ×5  (3.3 : 1)"

    private void OnEnable()
    {
        uiInventory.LogSwapInfoChangedEvent += OnLogSwapInfoChanged;
        uiInventory.LogSwapExecutedEvent += OnLogSwapExecuted;
        OnLogSwapInfoChanged();   // 켜질 때 현재 상태 한 번 반영
    }

    private void OnDisable()
    {
        uiInventory.LogSwapInfoChangedEvent -= OnLogSwapInfoChanged;
        uiInventory.LogSwapExecutedEvent -= OnLogSwapExecuted;
    }

    private void OnLogSwapInfoChanged()
    {
        // 1) 운반 상자 교체: 이 가방의 슬롯이 "넘어가는" 쪽이다. 버려지는 건 상자 슬롯(상자 UI가 그린다).
        if (true == uiInventory.IsOutgoingSwapReady)
        {
            LogSwapSlotInfo _out = uiInventory.OutgoingSwapInfo;

            // uiInventory.OutgoingSlotIndex 번째 슬롯에 "상자로 넘어감" 표시 (버림 표시와 다른 색/아이콘으로)
            highlight.SetActive(true);
            rateText.text = $"{Name(_out.incomingTreeType)} ×{_out.incomingCount} → 상자   (상자의 {Name(_out.treeType)} ×{_out.count} 내려놓음, {_out.exchangeRate:0.#} : 1)";
            return;
        }

        // 2) 인벤토리 교체: 이 가방의 슬롯이 "버려지는" 쪽이다.
        if (false == uiInventory.IsLogSwapReady)
        {
            highlight.SetActive(false);
            return;
        }

        LogSwapSlotInfo _info = uiInventory.LogSwapInfo;

        // _info.slotIndex 번째 슬롯 위로 하이라이트를 옮기고 켠다
        highlight.SetActive(true);

        // 환율 한 줄. 종류 이름은 기존 로컬라이징 매핑을 쓰면 된다.
        rateText.text = $"{Name(_info.treeType)} ×{_info.count}  ↔  {Name(_info.incomingTreeType)} ×{_info.incomingCount}   ({_info.exchangeRate:0.#} : 1)";
    }

    private void OnLogSwapExecuted(LogSwapSlotInfo _info)
    {
        // _info.slotIndex 칸이 방금 비워졌다. 사라지는 연출 등을 여기서 재생.
        // (슬롯 데이터 자체는 이미 갱신되어 비어 있다)
    }
}
```

---

## 7. 타이밍과 주의사항

- **인벤토리 교체 안내는 나무가 쓰러져 원목이 공중에 있는 동안에도 뜹니다.** "보이는 개수"에 아직 착지하지
  않은 원목이 포함되기 때문입니다. 흡입 선점과 같은 표를 보므로 두 안내가 어긋나지 않습니다.
- **운반 상자 교체 안내는 사정권에 들어오는 순간 뜹니다.** E를 누르기 전에, 상자 UI와 가방이 자동으로 열리는
  그 프레임에 값이 들어옵니다. 사정권 안에 서 있는 동안은 가방 사정이 바뀔 때마다(0.25초 주기) 따라갑니다.
- **`LogSwapInfoChangedEvent`는 내용이 실제로 달라졌을 때만** 발생합니다. 매 프레임 오지 않습니다.
  들어올 원목의 개수(`incomingCount`)가 바뀌어도 한 번 더 옵니다.
- 이벤트를 **구독하는 시점에는 이미 값이 들어와 있을 수 있으므로**, 위 예시처럼 `OnEnable`에서 현재
  값을 한 번 직접 읽어 반영하세요.
- **교체가 일어나면 슬롯 데이터는 즉시 사라집니다.** 버린 원목이 바닥으로 흩뿌려지는 연출은 그 뒤에
  따라붙을 뿐이고, 데이터는 연출을 기다리지 않습니다.
- 슬롯 목록 자체의 갱신은 기존 경로(`ItemRemovedFromInventorySignal` / `OffroadContainerUpdatedSignal`)로
  이미 오고 있습니다. `LogSwapExecutedEvent`는 **"그 변화가 교체 때문이었다"** 를 구분해 연출을 붙이고
  싶을 때만 쓰면 됩니다.
- 인벤토리를 닫아도 값은 그대로 남아 있습니다. "닫혀 있으면 안 보여준다"는 판단은 UI가 하세요
  (`UI_Inventory.IsOpening`).

---

## 8. 교체 키 아이콘

교체 키는 리바인딩 가능한 액션으로 등록되어 있습니다.

- `ERebindableAction.LogSwap` (기본값: 키보드 `Tab`, 패드 `buttonNorth` = Y/△)
- 옵션 > 키 설정 화면에 "원목 교체" 행이 자동으로 생깁니다.

화면에 키 아이콘을 띄우려면 기존 `UI_KeyboardImage`를 쓰면 됩니다.

- `Mode = RebindableAction`
- `Action = LogSwap`

유저가 키를 바꾸면 아이콘도 자동으로 따라갑니다. Tab / Y 아이콘은 `KeyIconDatabase`에 이미 있습니다.

---

## 9. 신호 흐름 (참고)

```
InputReader.LogSwapKeyPressedEvent
    └ GameplayUICoordinator.OnLogSwapKeyPressed   (인벤토리가 열려 있을 때만 통과)
        └ LogSwapRequestedSignal
            └ UnitSystem.LogSwapRequested
                ├ OffroadContainer.ExecuteLogSwap()   (상자 우선)
                └ InventoryManager.ExecuteLogSwap()
                     └ LogSwapExecutedSignal ──┐
                                               │
InventoryManager / OffroadContainer            │
    └ SwapStateChangedEvent                    │
        └ UnitSystem.LogSwapStateChanged       │
            └ LogSwapAvailabilityChangedSignal │
                                               │
GameplayUICoordinator ─────────────────────────┘
    ├ UIView_Popup.LogSwapTargetChanged / LogSwapExecuted      → UI_Inventory
    └ UIView_WorldPopup.LogSwapTargetChanged / LogSwapExecuted → UI_Storage(ui_CarStorage)
```

UI에서 신호를 직접 구독할 일은 없습니다. `UI_Inventory` / `UI_Storage`의 이벤트만 보면 됩니다.

---

## 10. 함께 바뀐 것 — 흡입 선점 (UI 무관, 참고)

가방이 거의 찼을 때 **어느 원목이 마지막 칸을 가져가는지**도 같은 원칙으로 돌아갑니다.

> 칸의 가치 = 개당 가치 × min(지금 보이는 개수, 슬롯 최대 중첩)

개당 가치만 보면 보석 **한 개**(황금 소나무 60)가 마지막 칸을 잠그고, 뒤따르는 일반 자작 15개(600)를
통째로 튕겨냈습니다. 지금은 "보이는 만큼"이 더 큰 쪽이 칸을 가져갑니다. 선점과 교체가 같은 값을 보고
움직이므로 "비싸다고 먼저 먹어놓고 싸다고 버리는" 모순이 생기지 않습니다.

개당 가치는 **기본 가치 × 등급 배율**의 실제 값입니다(`LogValue`). 등급 배율(황금 ×5 / 다이아 ×10 /
프리즘 ×20)은 `LogItemValueDataBase`가 단일 출처이고, 제재소 평가(`LogEvaluator`)도 같은 표를 봅니다.

---

## 11. 관련 파일

| 파일 | 역할 |
|---|---|
| `Application Layer/UnitSystem/LogSwap/LogSwapTypes.cs` | `LogSwapSlotInfo`, `ELogSwapTarget`, `LogValue`(실제 가치 창구) |
| `Application Layer/ObjectSystem/Item/LogItemValueDataBase.cs` | 기본 가치 + 등급 배율의 단일 출처 |
| `Application Layer/UnitSystem/InventoryManager/InventoryManager.cs` | 인벤토리 쪽 교체 판정/실행 (`교체 시스템(인벤토리)` 구역) |
| `Application Layer/UnitSystem/OffroadContainer/OffroadContainer.cs` | 운반 상자 쪽 교체 판정/실행 (`교체 시스템(이동식 운반 상자)` 구역) |
| `Application Layer/UnitSystem/UnitSystem.cs` | 두 쪽 중 어디를 쓸지 결정하고 신호 발행 |
| `Application Layer/UISystem/Coordinator/Gameplay/GameplayUICoordinator.cs` | 키 입력 게이트 + UI로 전달 |
| `Presentation Layer/UnitSystem/ItemDetector.cs` | 흡입 선점 정렬 |
| `Presentation Layer/UISystem/UIView/Popup/Inventory/UI_Inventory.cs` | 인벤토리 UI가 받는 값 |
| `Presentation Layer/UISystem/UIView/WorldPopup/WoodenStorage/UI_Storage.cs` | 운반 상자 UI가 받는 값 |
