using UnityEngine;

[CreateAssetMenu(fileName = "Lumberjack NPC Boomerang", menuName = "Game/Skill Command/Lumberjack NPC Boomerang")]
public class SC_LumberjackNPCBoomerang : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonUnitSpawnerCH.SetBoomerangEnable(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonUnitSpawnerCH.SetBoomerangEnable(false);
    }
}
