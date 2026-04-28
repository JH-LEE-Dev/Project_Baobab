using UnityEngine;

[CreateAssetMenu(fileName = "Increase Ricochet Cnt", menuName = "Game/Skill Command/Increase Ricochet Cnt")]
public class SC_Ricochet : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRicochetCnt((int)amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRicochetCnt(-(int)amount);
    }
}
