using UnityEngine;

public class TeleportManager : MonoBehaviour
{
    //외부 의존성
    private SignalHub signalHub;
    private IBootStrapProvider bootStrapProvider;

    public void Initialize(SignalHub _signalHub, IBootStrapProvider _bootstrapProvider)
    {
        signalHub = _signalHub;
        bootStrapProvider = _bootstrapProvider;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<PortalActivatedSignal>(PortalActivated);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<PortalActivatedSignal>(PortalActivated);
    }

    private void PortalActivated(PortalActivatedSignal portalActivatedSignal)
    {
        switch (portalActivatedSignal.type)
        {
            case PortalType.ToDungeonPortal:
                bootStrapProvider.GoToOtherScene("DungeonScene");
                break;
            case PortalType.ToTownPortal:
                bootStrapProvider.GoToOtherScene("TownScene");
                break;
        }
    }
}
