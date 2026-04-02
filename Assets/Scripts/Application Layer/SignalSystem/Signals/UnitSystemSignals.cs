
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