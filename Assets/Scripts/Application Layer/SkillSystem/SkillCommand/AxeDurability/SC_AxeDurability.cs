using UnityEngine;

[CreateAssetMenu(fileName = "Axe Durability", menuName = "Game/Skill Command/Axe Durability")]
public class SC_AxeDurability : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseAxeDurability(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeDurability(-amount);
    }
}
