using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Critical", menuName = "Game/Skill Command/Boomerang Critical")]
public class SC_BoomerangCritical : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateBoomerangCritical(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateBoomerangCritical(false);
    }
}
