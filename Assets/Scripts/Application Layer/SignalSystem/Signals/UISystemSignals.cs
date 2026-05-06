public struct GoHomeButtonClickedSignal { }

public struct DeleteItemSignal
{
    public IInventorySlot slot;
    public DeleteItemSignal(IInventorySlot _slot)
    {
        slot = _slot;
    }
}

public struct DungeonSelectedSignal
{
    public MapType type;
    public ForestType forestType;
    public DungeonSelectedSignal(MapType _type, ForestType _forestType)
    {
        type = _type;
        forestType = _forestType;
    }
}

public struct SleepSignal { }

public struct SpendMoneySignal { }

public struct HiddenMapSelectedSignal
{
    public MapType type;
    public ForestType forestType;
    public HiddenMapSelectedSignal(MapType _type, ForestType _forestType)
    {
        type = _type;
        forestType = _forestType;
    }
}