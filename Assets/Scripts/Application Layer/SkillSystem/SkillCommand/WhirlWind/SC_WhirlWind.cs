using UnityEngine;

[CreateAssetMenu(fileName = "WhirlWind", menuName = "Game/Skill Command/WhirlWind")]
public class SC_WhirlWind : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseWeakPointDamageMul(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseWeakPointDamageMul(-amount);
    }
}
