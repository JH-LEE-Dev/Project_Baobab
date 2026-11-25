
public struct PortalActivatedSignal { }

public struct PortalDeActivatedSignal { }

public struct StartSkyProductionSignal 
{ 
    public bool isMainMenuRelated; 
    // 하늘이 덮인 뒤 도착할 맵(하늘 그림을 이 맵 것으로 넘긴다). None이면 지금 맵 그대로.
    public MapType destinationMapType;
    
    public StartSkyProductionSignal(bool _isMainMenuRelated = false, MapType _destinationMapType = MapType.None)
    {
        isMainMenuRelated = _isMainMenuRelated;
        destinationMapType = _destinationMapType;
    }
}
public struct RollbackSkyProductionSignal { }
public struct ActivateWarningUISignal { }