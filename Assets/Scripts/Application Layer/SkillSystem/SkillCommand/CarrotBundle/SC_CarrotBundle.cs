using UnityEngine;

[CreateAssetMenu(fileName = "Carrot Bundle", menuName = "Game/Skill Command/Carrot Bundle")]
public class SC_CarrotBundle : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.carrotItemCH.IncreaseCarrotDrop(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.carrotItemCH.IncreaseCarrotDrop(-amount);
    }
}
