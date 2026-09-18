using UnityEngine;

[CreateAssetMenu(fileName = "Blast Furnace", menuName = "Game/Skill Command/Blast Furnace")]
public class SC_BlastFurnace : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.blastFurnaceCH.IncreaseFurnaceCount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.blastFurnaceCH.IncreaseFurnaceCount(-amount);
    }
}
