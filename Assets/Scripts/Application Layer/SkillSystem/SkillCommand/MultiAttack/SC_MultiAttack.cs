using UnityEngine;

[CreateAssetMenu(fileName = "Multi Attack", menuName = "Game/Skill Command/Multi Attack")]
public class SC_MultiAttack : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.SetMultiAttack(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.SetMultiAttack(false);
    }
}

