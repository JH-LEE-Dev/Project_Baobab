using UnityEngine;

[CreateAssetMenu(fileName = "Lumberjack NPC", menuName = "Game/Skill Command/Lumberjack NPC")]
public class SC_LumberjackNPC : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonUnitSpawnerCH.SetLumberjackNPCCount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonUnitSpawnerCH.SetLumberjackNPCCount(0);
    }
}
