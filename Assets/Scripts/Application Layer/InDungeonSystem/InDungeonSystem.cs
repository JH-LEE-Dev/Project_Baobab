using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    private SignalHub signalHub;
    private InDungeonObjectManager inDungeonObjectManager;
    private IEnvironmentProvider environmentProvider;

    private Character character;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider);

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();
    }

    public void StartDungeonSystem(SceneChangeData _sceneChangeData)
    {
        signalHub.Publish(new DungeonStartSignal());
    }

    private void BindEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.PortalActivatedEvent += PortalActivated;
    }

    private void ReleaseEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
    }

    private void PortalActivated(PortalType _type)
    {
        signalHub.Publish(new PortalActivatedSignal(_type));
    }

    private void CharacterSpawned(CharacterSpawendSignal characterSpawendSignal)
    {
        character = characterSpawendSignal.character;
        inDungeonObjectManager.SetCharacter(character);
    }

    private void MapGenerated(MapGeneratedSignal mapGeneratedSignal)
    {
        inDungeonObjectManager.ReadyTrees(mapGeneratedSignal.grassTilePositions);
        inDungeonObjectManager.ReadyPortalAndCharacter();
    }
}
