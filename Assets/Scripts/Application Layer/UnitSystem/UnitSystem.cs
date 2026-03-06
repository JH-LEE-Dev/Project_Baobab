

public class UnitSystem
{
    //외부 의존성
    private SignalHub signalHub;
    private UnitSpawner unitSpawner;
    private UnitLogicManager unitLogicManager;

    //내부 의존성


    public void Initialize(SignalHub _signalHub, UnitSpawner _unitSpawner,UnitLogicManager _unitLogicManager)
    {
        signalHub = _signalHub;
        unitSpawner = _unitSpawner;
        unitLogicManager = _unitLogicManager;

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
        unitLogicManager.SetCharacter(_character);
    }

    public void CreateCharacter()
    {
        unitSpawner.SpawnCharacter();
    }
}
