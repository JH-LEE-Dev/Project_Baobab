using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Overheat Boost", menuName = "Game/Skill Command/Boomerang Overheat Boost")]
public class SC_BoomerangOverheatBoost : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateBoomerangOverheatBoost(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateBoomerangOverheatBoost(false);
    }
}
