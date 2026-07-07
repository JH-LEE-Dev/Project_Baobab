using UnityEngine;

[CreateAssetMenu(fileName = "Offroad Porter NPC Speed", menuName = "Game/Skill Command/Offroad Porter NPC Speed")]
public class SC_OffroadPorterNPCSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.townUnitSpawnerCH.IncreaseOffroadPorterNPCSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.townUnitSpawnerCH.IncreaseOffroadPorterNPCSpeed(-amount);
    }
}

