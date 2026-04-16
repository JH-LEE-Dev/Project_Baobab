using UnityEngine;

public abstract class SkillCommand : ScriptableObject
{
    public int level = 0;
    public float amount = 0f;
    public SkillCommandType skillCommandType;
    public abstract void Execute(ICommandHandleSystem _system);
    public abstract void Undo(ICommandHandleSystem _system);
}