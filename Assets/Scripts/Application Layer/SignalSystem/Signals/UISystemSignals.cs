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

public struct TeleportUIClosedSignal { }
public struct TeleportUIClosedWhileTeleportSignal { }

public struct RetryButtonClickedSignal { }

public struct WarningUIClosedSignal
{
    public bool bResult;
    public WarningUIClosedSignal(bool _bResult)
    {
        bResult = _bResult;
    }
}