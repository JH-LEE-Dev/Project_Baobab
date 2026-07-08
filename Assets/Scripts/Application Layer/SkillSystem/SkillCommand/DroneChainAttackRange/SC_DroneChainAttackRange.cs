using UnityEngine;

[CreateAssetMenu(fileName = "Drone Chain Attack Range", menuName = "Game/Skill Command/Drone Chain Attack Range")]
public class SC_DroneChainAttackRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseDroneChainRange(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseDroneChainRange(-amount);
    }
}
