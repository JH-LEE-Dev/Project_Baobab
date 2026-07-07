using UnityEngine;

[CreateAssetMenu(fileName = "Lumberjack NPC ShockWave", menuName = "Game/Skill Command/Lumberjack NPC ShockWave")]
public class SC_LumberjackNPCShockWave : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonUnitSpawnerCH.SetShockWaveEnable(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonUnitSpawnerCH.SetShockWaveEnable(false);
    }
}
