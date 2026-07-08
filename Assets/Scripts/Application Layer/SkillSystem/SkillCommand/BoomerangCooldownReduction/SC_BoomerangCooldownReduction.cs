using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Cooldown Reduction", menuName = "Game/Skill Command/Boomerang Cooldown Reduction")]
public class SC_BoomerangCooldownReduction : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseBoomerangCooldownReduction(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseBoomerangCooldownReduction(-amount);
    }
}
