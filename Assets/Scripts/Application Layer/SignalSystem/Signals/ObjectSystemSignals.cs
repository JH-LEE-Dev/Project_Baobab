
public struct PortalActivatedSignal { }

public struct PortalDeActivatedSignal { }

public struct StartSkyProductionSignal 
{ 
    public bool isMainMenuRelated; 
    
    public StartSkyProductionSignal(bool _isMainMenuRelated = false)
    {
        isMainMenuRelated = _isMainMenuRelated;
    }
}
public struct RollbackSkyProductionSignal { }
public struct ActivateWarningUISignal { }