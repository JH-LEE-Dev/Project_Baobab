using UnityEngine;

[CreateAssetMenu(fileName = "Lumberjack NPC Damage", menuName = "Game/Skill Command/Lumberjack NPC Damage")]
public class SC_LumberjackNPCDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonUnitSpawnerCH.IncreaseDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonUnitSpawnerCH.IncreaseDamage(-amount);
    }
}

