
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

        skillDispatcher.DeclareAccumulatedValueEvent -= DeclareSkillAccumulatedValue;
        skillDispatcher.DeclareAccumulatedValueEvent += DeclareSkillAccumulatedValue;

        skillManager.SkillValuePreviewRequestEvent -= RequestSkillValuePreviewData;
        skillManager.SkillValuePreviewRequestEvent += RequestSkillValuePreviewData;

        skillDispatcher.ProvideAccumulatedValueChangeEvent -= ProvideSkillAccumulatedValueChange;
        skillDispatcher.ProvideAccumulatedValueChangeEvent += ProvideSkillAccumulatedValueChange;
    }

    private void ReleaseEvents()
    {
        skillManager.DispatchSkillsEvent -= SkillDispatched;
        skillManager.PrestigeLevelIncreasedEvent -= PrestigeLevelIncreased;
        skillDispatcher.DeclareAccumulatedValueEvent -= DeclareSkillAccumulatedValue;
        skillManager.SkillValuePreviewRequestEvent -= RequestSkillValuePreviewData;
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void SkillDispatched(SkillDispatchInfo _skillDispatchInfo)
    {
        skillDispatcher.DispatchCommand(_skillDispatchInfo);
        signalHub.Publish(new SkillDispatchedSignal(_skillDispatchInfo.commandInfo.skillCommandType));
    }

    private void PrestigeLevelIncreased(int _level)
    {
        signalHub.Publish(new PrestigeLevelIncreasedSignal(_level));
    }

    private void DeclareSkillAccumulatedValue(SkillAccumulatedValueData _data)
    {
        signalHub.Publish(new DeclareSkillAccumulatedValueSignal(_data));
    }

    private void RequestSkillValuePreviewData(SkillDispatchInfo _skillDispatchInfo)
    {
        skillDispatcher.DispatchCommandWithChange(_skillDispatchInfo);
    }

    private void ProvideSkillAccumulatedValueChange(SkillAccumulatedValueChangeData _data)
    {
        signalHub.Publish(new ProvideSkillAccumulatedValueChangeSignal(_data));
    }

}
