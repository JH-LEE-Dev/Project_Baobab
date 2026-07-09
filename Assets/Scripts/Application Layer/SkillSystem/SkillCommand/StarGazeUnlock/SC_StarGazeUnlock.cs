using UnityEngine;

[CreateAssetMenu(fileName = "Star Gaze Unlock", menuName = "Game/Skill Command/Star Gaze Unlock")]
public class SC_StarGazeUnlock : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.UnlockStarGaze(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.UnlockStarGaze(false);
    }
}
