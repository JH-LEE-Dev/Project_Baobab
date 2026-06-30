using UnityEngine;

[CreateAssetMenu(fileName = "ShockWave Critical", menuName = "Game/Skill Command/ShockWave Critical")]
public class SC_ShockWaveCritical : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateShockWaveCritical(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateShockWaveCritical(false);
    }
}
