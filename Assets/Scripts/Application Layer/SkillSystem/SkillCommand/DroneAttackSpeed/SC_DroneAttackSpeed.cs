using UnityEngine;

[CreateAssetMenu(fileName = "Drone Attack Speed", menuName = "Game/Skill Command/Drone Attack Speed")]
public class SC_DroneAttackSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseDroneAttackSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseDroneAttackSpeed(-amount);
    }
}
