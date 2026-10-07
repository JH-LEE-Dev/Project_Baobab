using System;

/// <summary>
/// 캐릭터의 과열 버프 상태를 UI 같은 외부에 알리는 읽기 전용 창구.
/// </summary>
public interface IOverheatComponent
{
    public bool IsActive { get; }
    // true = 과열 시작, false = 과열 종료. ActivateBuff/DeactivateBuff 두 곳에서만 발생한다.
    public event Action<bool> OverheatStateChangedEvent;
}
