
public class ObjectSystem
{
    //외부 의존성
    private SignalHub signalHub;

    //내부 의존성
    private ObjectManager objectManager;

    public void Initailize(SignalHub _signalHub, ObjectManager _objectManager)
    {
        signalHub = _signalHub;
        objectManager = _objectManager;

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void SetupObjects(SceneType _type)
    {
        objectManager.SetupObj(_type);
    }

    private void BindEvents()
    {
        if (objectManager == null)
            return;

        objectManager.PortalActivatedEvent -= PortalActivated;
        objectManager.PortalActivatedEvent += PortalActivated;
    }

    private void ReleaseEvents()
    {
        objectManager.PortalActivatedEvent -= PortalActivated;
    }

    private void PortalActivated(PortalType _type)
    {
        signalHub.Publish(new PortalActivatedSignal(_type));
    }
}
