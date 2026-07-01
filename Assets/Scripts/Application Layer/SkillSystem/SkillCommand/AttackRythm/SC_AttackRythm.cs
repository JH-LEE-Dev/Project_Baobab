using UnityEngine;

[CreateAssetMenu(fileName = "Attack Rythm", menuName = "Game/Skill Command/Attack Rythm")]
public class SC_AttackRythm : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.SetAttackRythmSpeedAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.SetAttackRythmSpeedAmount(0f);
    }
}
