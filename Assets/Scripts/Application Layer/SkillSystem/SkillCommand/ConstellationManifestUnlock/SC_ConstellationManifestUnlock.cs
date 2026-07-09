using UnityEngine;

[CreateAssetMenu(fileName = "Constellation Manifest Unlock", menuName = "Game/Skill Command/Constellation Manifest Unlock")]
public class SC_ConstellationManifestUnlock : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.UnlockConstellationManifest(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.UnlockConstellationManifest(false);
    }
}
