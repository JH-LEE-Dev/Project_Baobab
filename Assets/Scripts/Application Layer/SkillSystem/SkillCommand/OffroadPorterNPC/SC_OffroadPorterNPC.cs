using UnityEngine;

[CreateAssetMenu(fileName = "Offroad Porter NPC", menuName = "Game/Skill Command/Offroad Porter NPC")]
public class SC_OffroadPorterNPC : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.townUnitSpawnerCH.SetOffroadPorterNPCCount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.townUnitSpawnerCH.SetOffroadPorterNPCCount(0);
    }
}
