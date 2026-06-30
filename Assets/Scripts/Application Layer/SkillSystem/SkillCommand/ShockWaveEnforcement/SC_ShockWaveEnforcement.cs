using UnityEngine;

[CreateAssetMenu(fileName = "ShockWave Enforcement", menuName = "Game/Skill Command/ShockWave Enforcement")]
public class SC_ShockWaveEnforcement : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateShockWaveEnforcement(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateShockWaveEnforcement(false);
    }
}
