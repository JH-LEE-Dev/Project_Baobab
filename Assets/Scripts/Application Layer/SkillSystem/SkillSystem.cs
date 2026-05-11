
public class SkillSystem
{
    private SignalHub signalHub;
    public SkillManager skillManager { get; private set; }
    private SkillDispatcher skillDispatcher;

    public void Initialize(SignalHub _signalHub, SkillManager _skillManager, SkillDispatcher _skillDispatcher)
    {
        skillManager = _skillManager;
        skillDispatcher = _skillDispatcher;
        signalHub = _signalHub;

        BindEvents();
    }

    private void BindEvents()
    {
        skillManager.DispatchSkillsEvent -= SkillDispatched;
        skillManager.DispatchSkillsEvent += SkillDispatched;

        skillManager.PrestigeLevelIncreasedEvent -= PrestigeLevelIncreased;
        skillManager.PrestigeLevelIncreasedEvent += PrestigeLevelIncreased;
    }

    private void ReleaseEvents()
    {
        skillManager.DispatchSkillsEvent -= SkillDispatched;

        skillManager.PrestigeLevelIncreasedEvent -= PrestigeLevelIncreased;
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void SkillDispatched(SkillDispatchInfo _skillDispatchInfo)
    {
        skillDispatcher.DispatchCommand(_skillDispatchInfo);
        signalHub.Publish(new SkillDispatchedSignal());
    }

    private void PrestigeLevelIncreased(int _level)
    {
        signalHub.Publish(new PrestigeLevelIncreasedSignal(_level));
    }
}
