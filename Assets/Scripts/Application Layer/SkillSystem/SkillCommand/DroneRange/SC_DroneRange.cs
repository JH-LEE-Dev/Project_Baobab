using UnityEngine;

[CreateAssetMenu(fileName = "Drone Range", menuName = "Game/Skill Command/Drone Range")]
public class SC_DroneRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseDroneRange(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseDroneRange(-amount);
    }
}
