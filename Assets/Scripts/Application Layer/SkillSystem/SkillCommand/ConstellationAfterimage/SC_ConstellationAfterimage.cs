using UnityEngine;

[CreateAssetMenu(fileName = "Constellation Afterimage", menuName = "Game/Skill Command/Constellation Afterimage")]
public class SC_ConstellationAfterimage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseConstellationHitCount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseConstellationHitCount(-amount);
    }
}
