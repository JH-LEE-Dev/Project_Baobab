using UnityEngine;

[CreateAssetMenu(fileName = "ShockWave Mastery", menuName = "Game/Skill Command/ShockWave Mastery")]
public class SC_ShockWaveMastery: SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ShockWaveMastery(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ShockWaveMastery(false);
    }
}
