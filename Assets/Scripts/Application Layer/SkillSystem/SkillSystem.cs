
public class SkillSystem
{
    public SkillManager skillManager { get; private set; }
    private SkillDispatcher skillDispatcher;

    public void Initialize(SkillManager _skillManager, SkillDispatcher _skillDispatcher)
    {
        skillManager = _skillManager;
        skillDispatcher = _skillDispatcher;

        BindEvents();
    }

    private void BindEvents()
    {
        skillManager.DispatchSkillsEvent -= skillDispatcher.DispatchCommand;
        skillManager.DispatchSkillsEvent += skillDispatcher.DispatchCommand;
    }

    private void ReleaseEvents()
    {
        skillManager.DispatchSkillsEvent -= skillDispatcher.DispatchCommand;
    }

    public void Release()
    {
        ReleaseEvents();
    }
}
