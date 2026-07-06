using UnityEngine;

[CreateAssetMenu(fileName = "Lumberjack NPC Speed", menuName = "Game/Skill Command/Lumberjack NPC Speed")]
public class SC_LumberjackNPCSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonUnitSpawnerCH.IncreaseSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonUnitSpawnerCH.IncreaseSpeed(-amount);
    }
}