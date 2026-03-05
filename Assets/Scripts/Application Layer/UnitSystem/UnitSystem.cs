

public class UnitSystem
{
    //외부 의존성
    private SignalHub signalHub;
    private UnitSpawner unitSpawner;

    public void Initialize(SignalHub _signalHub, UnitSpawner _unitSpawner)
    {
        signalHub = _signalHub;
        unitSpawner = _unitSpawner;

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
        unitSpawner.CharacterSpawnedEvent += CharacterSpawned;
    }

    private void ReleaseEvents()
    {
        unitSpawner.CharacterSpawnedEvent -= CharacterSpawned;
    }

    private void CharacterSpawned(Character _character)
    {
        signalHub.Publish(new CharacterSpawendSignal(_character));
    }
}
