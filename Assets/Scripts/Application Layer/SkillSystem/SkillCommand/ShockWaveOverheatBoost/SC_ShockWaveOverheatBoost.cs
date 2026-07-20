using UnityEngine;

[CreateAssetMenu(fileName = "ShockWave Overheat Boost", menuName = "Game/Skill Command/ShockWave Overheat Boost")]
public class SC_ShockWaveOverheatBoost : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateShockWaveOverheatBoost(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateShockWaveOverheatBoost(false);
    }
}
