
public struct SkillDispatchedSignal { }

public struct PrestigeLevelIncreasedSignal
{
    public int level;
    public PrestigeLevelIncreasedSignal(int _level)
    {
        level = _level;
    }
}

public struct ProvideSkillAccumulatedValueChangeSignal
{
    public SkillAccumulatedValueChangeData data;
    public ProvideSkillAccumulatedValueChangeSignal(SkillAccumulatedValueChangeData _data)
    {
        data = _data;
    }
}
