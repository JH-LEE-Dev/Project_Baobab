using UnityEngine;

public abstract class SkillCommand : ScriptableObject
{
    public int level = 0;
    public float amount = 0f;
    public SkillCommandType skillCommandType;
    public abstract void Execute(ICommandHandleSystem _system);
    public abstract void Undo(ICommandHandleSystem _system);
    public void PrintDebug()
    {
        Debug.Log($"특성 스킬 실행 : {skillCommandType}, 값 : {amount}");
    }
}