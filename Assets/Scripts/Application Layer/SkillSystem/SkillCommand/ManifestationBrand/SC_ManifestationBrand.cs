using UnityEngine;

[CreateAssetMenu(fileName = "Manifestation Brand", menuName = "Game/Skill Command/Manifestation Brand")]
public class SC_ManifestationBrand : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseManifestationBrandBonus(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseManifestationBrandBonus(-amount);
    }
}
