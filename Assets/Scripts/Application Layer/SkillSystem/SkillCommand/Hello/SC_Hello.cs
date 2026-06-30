using UnityEngine;

[CreateAssetMenu(fileName = "Hello", menuName = "Game/Skill Command/Hello")]
public class SC_Hello : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseHelloDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseHelloDamage(-amount);
    }
}

