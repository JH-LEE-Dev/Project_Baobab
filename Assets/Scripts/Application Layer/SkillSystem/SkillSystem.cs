
public class SkillSystem
{
    private SkillManager skillManager;
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
