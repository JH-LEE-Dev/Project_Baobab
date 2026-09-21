# 원목 교체 시스템 - UI 연동 가이드

로직/데이터는 전부 들어가 있습니다. **UI에서 할 일은 "어느 슬롯이 버려질 예정인지"를 표시하고,
"교체가 일어났을 때" 연출을 붙이는 것**입니다. 이 문서는 그 두 가지에 필요한 값이 어디로 오는지만
설명합니다.

---

## 1. 기능 한 줄 요약

가방(또는 운반 상자)에 자리가 없어 원목을 더 못 담을 때, **교체 키(기본 Tab / 패드 Y)** 를 누르면
가장 값싼 슬롯 하나를 통째로 버려 자리를 만듭니다.

- 버려질 슬롯은 시스템이 자동으로 고릅니다(유저가 고르지 않습니다).
- **교체는 인벤토리가 열려 있을 때만 동작합니다.** 무엇이 버려지는지 눈으로 볼 수 없는 상태에서
  슬롯이 사라지면 안 되기 때문입니다.
- 버려질 슬롯이 없으면(=버릴 만큼 싼 슬롯이 없으면) 교체는 활성화되지 않습니다.

버려질 슬롯을 고르는 기준(참고용, UI가 계산할 필요는 없습니다):

1. 지금 못 담고 있는 원목보다 **싼** 슬롯만 후보
2. 그중 **가장 싼 것**(TreeType → LogState 순으로 비교)
3. 같은 값이면 **가장 적게 쌓인 것**

---

## 2. 어디서 값을 받나

### 2-1. 인벤토리 (`UI_Inventory`)

```csharp
// 교체 대상 슬롯이 달라졌을 때(생김 / 사라짐 / 다른 슬롯으로 이동 / 개수 변화)
public event Action LogSwapInfoChangedEvent;

// 교체가 실제로 일어나 슬롯 하나가 비워졌을 때. 인자는 "방금 버려진 슬롯"의 내용
public event Action<LogSwapSlotInfo> LogSwapExecutedEvent;

public LogSwapSlotInfo LogSwapInfo { get; }          // 지금 버려질 예정인 슬롯
public ELogSwapTarget ActiveLogSwapTarget { get; }   // 교체 키가 실제로 건드릴 쪽
public bool IsLogSwapReady { get; }                  // 위 둘을 합친 판정(아래 참고)
```

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

## 3. `LogSwapSlotInfo` — 버려질 슬롯 한 건

`Assets/Scripts/Application Layer/UnitSystem/LogSwap/LogSwapTypes.cs`

| 필드 | 설명 |
|---|---|
| `target` | `ELogSwapTarget.Inventory` / `OffroadContainer` / `None`(대상 없음) |
| `slotIndex` | **버려질 슬롯의 인덱스.** 없으면 `-1` |
| `treeType` | 그 슬롯의 수종 |
| `logState` | 그 슬롯의 등급 |
| `count` | 쌓여 있는 개수 = **교체하면 버려지는 원목 수** |
| `bHasSlot` | 버릴 슬롯이 정해져 있는지(프로퍼티) |

**`slotIndex`는 UI가 이미 그리고 있는 슬롯 목록과 같은 인덱스입니다.**

- `UI_Inventory` → `inventory.inventorySlots[slotIndex]` (= `inventorySlots` 리스트의 같은 번째 칸)
- `UI_Storage` → `storage.inventorySlots[slotIndex]`

그래서 `slotIndex`만으로 화면의 어떤 칸인지 바로 찾을 수 있습니다. `treeType/logState/count`는 그
시점의 사본이라, **교체가 끝나 슬롯이 비워진 뒤에도**(`LogSwapExecutedEvent`) 무엇이 몇 개 버려졌는지
그대로 읽을 수 있습니다.

---

## 4. `ActiveLogSwapTarget` — 왜 필요한가

인벤토리와 운반 상자 **양쪽 모두** 교체 후보를 가질 수 있습니다(상자 앞에 서 있는데 가방도 꽉 찬
경우). 이때 교체 키는 **운반 상자를 우선**으로 처리합니다.

그래서 각 UI는 두 가지를 구분할 수 있습니다.

- `LogSwapInfo.bHasSlot` → "이 창 기준으로 버려질 후보는 이 슬롯이다"
- `IsLogSwapReady` → "지금 키를 누르면 **이 창의** 그 슬롯이 실제로 버려진다"

가장 단순하게 가려면 `IsLogSwapReady`만 보고 켜고 끄면 됩니다. 후보는 있지만 지금 키가 다른 쪽을
건드리는 상태를 흐리게 표시하고 싶다면 `LogSwapInfo.bHasSlot`과 `ActiveLogSwapTarget`을 따로 보세요.

---

## 5. 사용 예

```csharp
public class UI_LogSwapHighlight : MonoBehaviour
{
    [SerializeField] private UI_Inventory uiInventory;
    [SerializeField] private GameObject highlight;   // 슬롯 위에 얹을 테두리 등

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
        if (false == uiInventory.IsLogSwapReady)
        {
            highlight.SetActive(false);
            return;
        }

        LogSwapSlotInfo _info = uiInventory.LogSwapInfo;

        // _info.slotIndex 번째 슬롯 위로 하이라이트를 옮기고 켠다
        // _info.count 로 "N개가 버려집니다" 같은 안내도 붙일 수 있다
        highlight.SetActive(true);
    }

    private void OnLogSwapExecuted(LogSwapSlotInfo _info)
    {
        // _info.slotIndex 칸이 방금 비워졌다. 사라지는 연출 등을 여기서 재생.
        // (슬롯 데이터 자체는 이미 갱신되어 비어 있다)
    }
}
```

---

## 6. 타이밍과 주의사항

- **`LogSwapInfoChangedEvent`는 내용이 실제로 달라졌을 때만** 발생합니다. 매 프레임 오지 않습니다.
  같은 슬롯이라도 그 안의 개수가 바뀌면(원목을 더 주워 담는 등) 한 번 더 옵니다.
- 이벤트를 **구독하는 시점에는 이미 값이 들어와 있을 수 있으므로**, 위 예시처럼 `OnEnable`에서 현재
  값을 한 번 직접 읽어 반영하세요.
- **교체가 일어나면 슬롯 데이터는 즉시 사라집니다.** 버린 원목이 바닥으로 흩뿌려지는 연출
  (`DropAllItem`과 같은 연출)은 그 뒤에 따라붙을 뿐이고, 데이터는 연출을 기다리지 않습니다.
- 슬롯 목록 자체의 갱신은 기존 경로(`ItemRemovedFromInventorySignal` / `OffroadContainerUpdatedSignal`)로
  이미 오고 있습니다. `LogSwapExecutedEvent`는 **"그 변화가 교체 때문이었다"** 를 구분해 연출을 붙이고
  싶을 때만 쓰면 됩니다. 목록을 다시 그리려고 쓸 필요는 없습니다.
- 인벤토리를 닫아도 값은 그대로 남아 있습니다. "닫혀 있으면 안 보여준다"는 판단은 UI가 하세요
  (`UI_Inventory.IsOpening`).

---

## 7. 교체 키 아이콘

교체 키는 리바인딩 가능한 액션으로 등록되어 있습니다.

- `ERebindableAction.LogSwap` (기본값: 키보드 `Tab`, 패드 `buttonNorth` = Y/△)
- 옵션 > 키 설정 화면에 "원목 교체" 행이 자동으로 생깁니다.

화면에 키 아이콘을 띄우려면 기존 `UI_KeyboardImage`를 쓰면 됩니다.

- `Mode = RebindableAction`
- `Action = LogSwap`

유저가 키를 바꾸면 아이콘도 자동으로 따라갑니다. Tab / Y 아이콘은 `KeyIconDatabase`에 이미 있습니다.

---

## 8. 신호 흐름 (참고)

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

## 9. 관련 파일

| 파일 | 역할 |
|---|---|
| `Application Layer/UnitSystem/LogSwap/LogSwapTypes.cs` | `LogSwapSlotInfo`, `ELogSwapTarget`, 가치 비교 기준 |
| `Application Layer/UnitSystem/InventoryManager/InventoryManager.cs` | 인벤토리 쪽 교체 판정/실행 (`교체 시스템(인벤토리)` 구역) |
| `Application Layer/UnitSystem/OffroadContainer/OffroadContainer.cs` | 운반 상자 쪽 교체 판정/실행 (`교체 시스템(이동식 운반 상자)` 구역) |
| `Application Layer/UnitSystem/UnitSystem.cs` | 두 쪽 중 어디를 쓸지 결정하고 신호 발행 |
| `Application Layer/UISystem/Coordinator/Gameplay/GameplayUICoordinator.cs` | 키 입력 게이트 + UI로 전달 |
| `Presentation Layer/UISystem/UIView/Popup/Inventory/UI_Inventory.cs` | 인벤토리 UI가 받는 값 |
| `Presentation Layer/UISystem/UIView/WorldPopup/WoodenStorage/UI_Storage.cs` | 운반 상자 UI가 받는 값 |
