using UnityEngine;

[CreateAssetMenu(fileName = "Drone Chain Attack", menuName = "Game/Skill Command/Drone Chain Attack")]
public class SC_DroneChainAttack : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseDroneChainCount((int)amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseDroneChainCount(-(int)amount);
    }
}
