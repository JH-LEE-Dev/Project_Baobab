using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Distance", menuName = "Game/Skill Command/Boomerang Distance")]
public class SC_BoomerangDistance : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseBoomerangDistance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseBoomerangDistance(-amount);
    }
}
