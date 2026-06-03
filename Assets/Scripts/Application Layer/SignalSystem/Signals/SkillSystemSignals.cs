
public struct SkillDispatchedSignal { }

public struct PrestigeLevelIncreasedSignal
{
    public int level;
    public PrestigeLevelIncreasedSignal(int _level)
    {
        level = _level;
    }
}

public struct DeclareSkillAccumulatedValueSignal
{
    public SkillAccumulatedValueData data;
    public DeclareSkillAccumulatedValueSignal(SkillAccumulatedValueData _data)
    {
        data = _data;
    }
}


