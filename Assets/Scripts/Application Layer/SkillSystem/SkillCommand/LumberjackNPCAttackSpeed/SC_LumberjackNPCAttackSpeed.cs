using UnityEngine;

[CreateAssetMenu(fileName = "Lumberjack NPC AttackSpeed", menuName = "Game/Skill Command/Lumberjack NPC AttackSpeed")]
public class SC_LumberjackNPCAttackSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonUnitSpawnerCH.IncreaseAttackSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonUnitSpawnerCH.IncreaseAttackSpeed(-amount);
    }
}

