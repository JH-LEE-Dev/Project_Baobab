using UnityEngine;

[CreateAssetMenu(fileName = "OffRoadVehicle", menuName = "Game/Skill Command/OffRoadVehicle")]
public class SC_OffRoadVehicle : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.townObjSystemCH.CanTravel();
    }

    public override void Undo(ICommandHandleSystem _system)
    {

    }
}
