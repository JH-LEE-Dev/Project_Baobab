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

// Tent(집) UI가 닫혔음을 알리는 신호. ESC로 닫힌 경우도 포함해 항상 발행된다.
public struct TentUIClosedSignal { }

// LootPillar 상호작용 UI(UIView_ScreenModal)가 닫혔음을 알리는 신호.
// TentUIClosedSignal과 같은 역할 - E키 토글뿐 아니라 ESC/패드 Cancel로 닫힌 경우도 포함해 항상 발행된다.
public struct LootPillarUIClosedSignal { }

public struct RetryButtonClickedSignal { }

public struct WarningUIClosedSignal
{
    public bool bResult;
    public WarningUIClosedSignal(bool _bResult)
    {
        bResult = _bResult;
    }
}

// MainMenu → Dungeon 튜토리얼: 카메라 하강 완료 2초 뒤 스튜디오 로고 연출을 재생하라는 신호
public struct StudioLogoRevealSignal { }

// MainMenu → Dungeon 튜토리얼: 스튜디오 로고 UI 연출이 끝났음을 알리는 신호(캐릭터 하차 트리거)
public struct CompanyLogoProductionCompletedSignal { }