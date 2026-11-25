// 유성우(별빛 나침반) / 지진(흑요 부적) HUD 갱신 신호.
// InDungeonSystem이 InDungeonObjectManager의 이벤트를 받아 발행하고, GameplayUICoordinator가 UIView_HUD로 넘긴다.

public enum ERelicSkillHudId
{
    MeteorShower = 0,
    Earthquake = 1,
}

public enum ERelicSkillHudOp
{
    SetUnlock = 0,
    SetCharge = 1,
    Trigger = 2,
}

public struct RelicSkillHudSignal
{
    public ERelicSkillHudId id;
    public ERelicSkillHudOp op;
    public bool bUnlocked;
    public int current;
    public int max;
    public float duration;

    public static RelicSkillHudSignal SetUnlock(ERelicSkillHudId _id, bool _bUnlocked)
    {
        RelicSkillHudSignal _signal = new RelicSkillHudSignal();
        _signal.id = _id;
        _signal.op = ERelicSkillHudOp.SetUnlock;
        _signal.bUnlocked = _bUnlocked;
        return _signal;
    }

    public static RelicSkillHudSignal SetCharge(ERelicSkillHudId _id, bool _bUnlocked, int _current, int _max)
    {
        RelicSkillHudSignal _signal = new RelicSkillHudSignal();
        _signal.id = _id;
        _signal.op = ERelicSkillHudOp.SetCharge;
        _signal.bUnlocked = _bUnlocked;
        _signal.current = _current;
        _signal.max = _max;
        return _signal;
    }

    public static RelicSkillHudSignal Trigger(ERelicSkillHudId _id, bool _bUnlocked, float _duration)
    {
        RelicSkillHudSignal _signal = new RelicSkillHudSignal();
        _signal.id = _id;
        _signal.op = ERelicSkillHudOp.Trigger;
        _signal.bUnlocked = _bUnlocked;
        _signal.duration = _duration;
        return _signal;
    }
}
