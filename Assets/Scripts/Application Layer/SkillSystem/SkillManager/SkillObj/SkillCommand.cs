using UnityEngine;

public abstract class SkillCommand<THandler> : ScriptableObject
    where THandler : class, ICommandHandler
{
    public virtual void Execute(ICommandHandler handler)
    {
        if (handler is THandler target)
        {
            Execute(target);
        }
    }
    public virtual void Undo(ICommandHandler handler)
    {
        if (handler is THandler target)
        {
            Undo(target);
        }
    }

    protected abstract void Execute(THandler handler);
    protected abstract void Undo(THandler handler);
}