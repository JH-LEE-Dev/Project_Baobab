using UnityEngine;

[CreateAssetMenu(fileName = "WeakPointHit", menuName = "Game/Skill Command/WeakPointHit")]
public class SC_WeakPointHit : SkillCommand
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
