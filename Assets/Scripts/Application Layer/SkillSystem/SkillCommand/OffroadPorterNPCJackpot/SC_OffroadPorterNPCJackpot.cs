using UnityEngine;

[CreateAssetMenu(fileName = "Offroad Porter NPC Jackpot", menuName = "Game/Skill Command/Offroad Porter NPC Jackpot")]
public class SC_OffroadPorterNPCJackpot : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.townUnitSpawnerCH.IncreaseOffroadPorterNPCJackpotChance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.townUnitSpawnerCH.IncreaseOffroadPorterNPCJackpotChance(-amount);
    }
}

