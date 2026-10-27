using UnityEngine;

/// <summary>
/// 원격 운반 서비스 - 마을에서 운반 상자에 든 원목이 제재소 원목 보관함으로 알아서 넘어간다.
/// 가치가 높은 원목부터, 보관함에 자리가 나는 만큼 넘어간다.
/// </summary>
[CreateAssetMenu(fileName = "Remote Log Transport", menuName = "Game/Skill Command/Remote Log Transport")]
public class SC_RemoteLogTransport : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.offroadContainerCH.SetRemoteLogTransport(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.SetRemoteLogTransport(false);
    }
}
