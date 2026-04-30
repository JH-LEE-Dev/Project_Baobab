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
    public DungeonSelectedSignal(MapType _type)
    {
        type = _type;
    }
}

public struct SleepSignal { }

public struct SpendMoneySignal { }