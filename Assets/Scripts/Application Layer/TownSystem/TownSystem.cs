using UnityEngine;

public class TownSystem : MonoBehaviour
{
    //내부 의존성
    private SignalHub signalHub;
    private TownObjectManager townObjectManager;
    private IEnvironmentProvider environmentProvider;

    private Character character;


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
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    private void PortalActivated(PortalType _type)
    {
        signalHub.Publish(new PortalActivatedSignal(_type));
    }

    private void CharacterSpawned(CharacterSpawendSignal characterSpawendSignal)
    {
        character = characterSpawendSignal.character;
    }
}
