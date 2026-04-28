using UnityEngine;

[CreateAssetMenu(fileName = "Steel Axe", menuName = "Game/Skill Command/Steel Axe")]
public class SC_SteelAxe : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeDurabilityDecIgnoreChance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeDurabilityDecIgnoreChance(-amount);
    }
}
