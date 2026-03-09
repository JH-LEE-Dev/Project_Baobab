using UnityEngine;

public class TownSystem : MonoBehaviour
{
    //내부 의존성
    [SerializeField] private Transform townStartPoint;
    private SignalHub signalHub;
    private TownObjectManager townObjectManager;
    private IEnvironmentProvider environmentProvider;


    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider)
    {
        signalHub = _signalHub;
        environmentProvider = _environmentProvider;

        townObjectManager = GetComponentInChildren<TownObjectManager>();

        townObjectManager.Initialize(environmentProvider);


        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();
    }

    public void StartTownSystem(SceneChangeData _sceneChangeData)
    {
        signalHub.Publish(new TownStartedSignal(townStartPoint));
        townObjectManager.ReadyObj();
    }

    private void BindEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
        townObjectManager.PortalActivatedEvent += PortalActivated;
    }

    private void ReleaseEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
    }

    private void SubscribeSignals()
    {

    }

    private void UnSubscribeSignals()
    {

    }

    private void PortalActivated(PortalType _type)
    {
        signalHub.Publish(new PortalActivatedSignal(_type));
    }
}
