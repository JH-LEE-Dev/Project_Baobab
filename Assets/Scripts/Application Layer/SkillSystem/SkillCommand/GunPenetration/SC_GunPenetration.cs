using UnityEngine;

[CreateAssetMenu(fileName = "Gun Penetration", menuName = "Game/Skill Command/Gun Penetration")]
public class SC_GunPenetration : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseGunPenetration(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseGunPenetration(-amount);
    }
}
