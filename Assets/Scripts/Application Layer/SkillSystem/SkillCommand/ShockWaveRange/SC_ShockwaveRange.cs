using UnityEngine;

[CreateAssetMenu(fileName = "Shockwave Range", menuName = "Game/Skill Command/Shockwave Range")]
public class SC_ShockwaveRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseShockWaveDuration(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseShockWaveDuration(-amount);
    }
}
