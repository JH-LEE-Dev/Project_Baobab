using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Attack Speed", menuName = "Game/Skill Command/Boomerang Attack Speed")]
public class SC_BoomerangAttackSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseBoomerangAttackSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseBoomerangAttackSpeed(-amount);
    }
}
