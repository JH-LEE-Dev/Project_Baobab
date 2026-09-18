using UnityEngine;

[CreateAssetMenu(fileName = "Blast Furnace Speed", menuName = "Game/Skill Command/Blast Furnace Speed")]
public class SC_BlastFurnaceSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.blastFurnaceCH.IncreaseProcessingSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.blastFurnaceCH.IncreaseProcessingSpeed(-amount);
    }
}
