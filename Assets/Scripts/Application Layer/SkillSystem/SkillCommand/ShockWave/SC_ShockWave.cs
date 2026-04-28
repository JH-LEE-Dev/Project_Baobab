using UnityEngine;

[CreateAssetMenu(fileName = "Shock Wave", menuName = "Game/Skill Command/Shock Wave")]
public class SC_ShockWave : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseShockWaveChance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseShockWaveChance(-amount);
    }
}
