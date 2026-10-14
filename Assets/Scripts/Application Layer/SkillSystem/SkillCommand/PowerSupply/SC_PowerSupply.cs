using UnityEngine;

[CreateAssetMenu(fileName = "Power Supply", menuName = "Game/Skill Command/Power Supply")]
public class SC_PowerSupply : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        // amount는 퍼센트가 아니라 배율 그대로 (예: 5 = 커터 속도 5배)
        _system.cutterCH.SetPowerSupply(true, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.cutterCH.SetPowerSupply(false, 1f);
    }
}
