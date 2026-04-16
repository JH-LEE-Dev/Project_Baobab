
public struct CharacterSpawendSignal
{
    public Character character;
    public CharacterSpawendSignal(Character _character)
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

public struct FirstTimeEarnMoneySignal { }

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