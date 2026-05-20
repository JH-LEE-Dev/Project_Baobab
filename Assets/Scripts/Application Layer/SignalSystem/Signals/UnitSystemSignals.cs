
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