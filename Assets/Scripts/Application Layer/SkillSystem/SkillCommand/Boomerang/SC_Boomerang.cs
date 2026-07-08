using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang", menuName = "Game/Skill Command/Boomerang")]
public class SC_Boomerang : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseBoomerangCount((int)amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseBoomerangCount(-(int)amount);
    }
}
