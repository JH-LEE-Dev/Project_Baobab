
public struct CharacterSpawnedSignal
{
    public Character character;
    public CharacterSpawnedSignal(Character _character)
    {
        character = _character;
    }
}

public struct InventoryUpdatedSignal { }

public struct InventoryInitializedSignal
{
    public IInventory inventory;
    public InventoryInitializedSignal(IInventory _inventory)
    {
        inventory = _inventory;
    }
}

public struct ContainerInteractStateChangedSignal
{
    public bool state;
    public ContainerInteractStateChangedSignal(bool _state)
    {
        state = _state;
    }
}

public struct CharacterEarnMoneySignal
{
    public MoneyType moneyType;
    public CharacterEarnMoneySignal(MoneyType _moneyType)
    {
        moneyType = _moneyType;
    }
}

public struct WeaponModeChangedSignal
{
    public WeaponMode weaponMode;
    public WeaponModeChangedSignal(WeaponMode _weaponMode)
    {
        weaponMode = _weaponMode;
    }
}

public struct InventorySpecChangedSignal { }
public struct LogContainerSpecChangedSignal { }
public struct OffraodContainerSpecChangedSignal { }

// 공격 범위 안에 나무가 하나도 없다가 처음 감지/감지되어 있다가 전부 사라졌을 때만 발생(AttackComponent.SetTreesDetected 참고)
public struct TreeDetectedSignal { }
public struct TreeDetectionClearedSignal { }

public struct CharacterStaminaIsEmptySignal { }

public struct OffroadContainerInteractStateChangedSignal
{
    public bool state;
    public OffroadContainerInteractStateChangedSignal(bool _state)
    {
        state = _state;
    }
}

public struct LoosAllInventoryItemSignal { }

public struct OffroadContainerUpdatedSignal { }

public struct InventoryIsFullSignal { }

public struct ItemAddedToInventorySignal { }
public struct ItemRemovedFromInventorySignal { }
public struct ItemCantAcquiedSignal { }

// "지금 교체하면 버려질 슬롯"이 달라졌을 때. 생기거나 사라질 때뿐 아니라 다른 슬롯으로 옮겨가거나
// 그 슬롯의 개수가 바뀔 때도 발행되므로, UI는 이 신호만 받아 표시를 통째로 다시 맞추면 된다.
//
// 인벤토리 쪽과 운반 상자 쪽을 각각 실어 보낸다(둘 다 bHasSlot == false일 수 있다). activeTarget은
// 지금 교체 키를 누르면 실제로 버려지는 쪽이다 - 둘 다 가능할 때는 운반 상자가 우선이다.
public struct LogSwapAvailabilityChangedSignal
{
    /// <summary>인벤토리에서 버려질 슬롯. 없으면 bHasSlot == false.</summary>
    public LogSwapSlotInfo inventoryInfo;

    /// <summary>운반 상자에서 버려질 슬롯. 없으면 bHasSlot == false.</summary>
    public LogSwapSlotInfo containerInfo;

    /// <summary>교체 키를 누르면 실제로 버려질 쪽. None이면 지금은 교체가 되지 않는다.</summary>
    public ELogSwapTarget activeTarget;

    public LogSwapAvailabilityChangedSignal(in LogSwapSlotInfo _inventoryInfo, in LogSwapSlotInfo _containerInfo,
        ELogSwapTarget _activeTarget)
    {
        inventoryInfo = _inventoryInfo;
        containerInfo = _containerInfo;
        activeTarget = _activeTarget;
    }
}

// 교체가 실제로 일어났을 때(= 슬롯 하나를 비웠을 때). info에는 방금 버려진 슬롯의 내용이 담긴다
// (target / slotIndex / treeType / logState / count). 데이터는 이 신호가 오기 전에 이미 지워져 있으므로,
// UI는 이 신호를 받고 그 슬롯을 비운 모습으로 갱신하거나 버려진 내용을 띄우면 된다.
//
// 슬롯 목록 자체의 갱신은 기존 신호(ItemRemovedFromInventorySignal / OffroadContainerUpdatedSignal)로도
// 이미 오고 있다. 이 신호는 "그 변화가 교체 때문이었다"를 구분해 연출을 붙이고 싶을 때 쓴다.
public struct LogSwapExecutedSignal
{
    public LogSwapSlotInfo info;

    public LogSwapExecutedSignal(in LogSwapSlotInfo _info)
    {
        info = _info;
    }
}
