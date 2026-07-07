using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Range", menuName = "Game/Skill Command/Boomerang Range")]
public class SC_BoomerangRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseBoomerangRange(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseBoomerangRange(-amount);
    }
}
