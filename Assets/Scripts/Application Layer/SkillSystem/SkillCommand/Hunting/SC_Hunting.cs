using UnityEngine;

[CreateAssetMenu(fileName = "Hunting", menuName = "Game/Skill Command/Hunting")]
public class SC_Hunting : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.CanHunting();
    }

    public override void Undo(ICommandHandleSystem _system)
    {

    }
}
