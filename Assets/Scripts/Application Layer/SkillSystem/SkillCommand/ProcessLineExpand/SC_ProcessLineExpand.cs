using UnityEngine;

[CreateAssetMenu(fileName = "Process Line Expand", menuName = "Game/Skill Command/Process Line Expand")]
public class SC_ProcessLineExpand : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logProcessingSystemCH.ExpandProcessLineCnt(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logProcessingSystemCH.ExpandProcessLineCnt(-amount);
    }
}
