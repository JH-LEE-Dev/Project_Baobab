
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