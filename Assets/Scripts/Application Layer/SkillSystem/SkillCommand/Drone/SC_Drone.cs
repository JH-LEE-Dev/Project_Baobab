using UnityEngine;

[CreateAssetMenu(fileName = "Drone", menuName = "Game/Skill Command/Drone")]
public class SC_Drone : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseDroneCount((int)amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseDroneCount(-(int)amount);
    }
}
