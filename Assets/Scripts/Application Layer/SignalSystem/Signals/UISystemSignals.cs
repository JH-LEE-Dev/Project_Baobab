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
    public DungeonType type;
    public DungeonSelectedSignal(DungeonType _type)
    {
        type = _type;
    }
}
