using UnityEngine;

[CreateAssetMenu(fileName = "Drone Overheat Boost", menuName = "Game/Skill Command/Drone Overheat Boost")]
public class SC_DroneOverheatBoost : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateDroneOverheatBoost(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateDroneOverheatBoost(false);
    }
}
