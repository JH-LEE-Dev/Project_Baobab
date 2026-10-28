
// 버프 HUD(스태미나 바 위 아이콘 줄)에 띄울 버프의 식별자. 같은 id면 HUD가 같은 칸으로 본다.
public enum EBuffHudId
{
    AttackRythm = 1,   // 벌목 리듬
    SourceOfSpeed = 2, // 속도의 원천
}

public enum EBuffHudOp
{
    Add,      // 없으면 등장, 있으면 갱신(스택형은 값만 바꾼다)
    SetValue, // 스택형: 현재 값, 지속형: 남은 시간(초)
    Trigger,  // 발동 연출만
    Dispel,   // 퇴장 연출과 함께 제거
    Remove,   // 연출 없이 즉시 제거
    ClearAll, // 전부 즉시 제거
}

// 버프 HUD 갱신 신호. 능력이 켜지고 꺼질 때와 중첩 수가 바뀔 때만 발행한다(매 프레임 X). 발행은 UnitSystem이 한다.
// 아이콘 스프라이트는 싣지 않는다 - UI 쪽이 id로 찾는다.
public struct BuffHudSignal
{
    public EBuffHudId id;
    public EBuffHudOp op;
    public BuffGaugeKind kind;   // Add일 때
    public float value;          // SetValue 값 / Add일 때 시작 값(스택형)
    public float max;            // Add일 때(스택형)
    public float duration;       // Add일 때(Duration)
    public float threshold;      // Add일 때(StackThreshold)
    public float decayDelay;
    public float decayInterval;
    public float decayPerSecond;

    public static BuffHudSignal AddStackHits(EBuffHudId _id, float _value, float _max)
    {
        BuffHudSignal _s = new BuffHudSignal();
        _s.id = _id;
        _s.op = EBuffHudOp.Add;
        _s.kind = BuffGaugeKind.StackHits;
        _s.value = _value;
        _s.max = _max;
        return _s;
    }

    public static BuffHudSignal AddDuration(EBuffHudId _id, float _duration)
    {
        BuffHudSignal _s = new BuffHudSignal();
        _s.id = _id;
        _s.op = EBuffHudOp.Add;
        _s.kind = BuffGaugeKind.Duration;
        _s.duration = _duration;
        return _s;
    }

    public static BuffHudSignal SetValue(EBuffHudId _id, float _value)
    {
        BuffHudSignal _s = new BuffHudSignal();
        _s.id = _id;
        _s.op = EBuffHudOp.SetValue;
        _s.value = _value;
        return _s;
    }

    public static BuffHudSignal Of(EBuffHudId _id, EBuffHudOp _op)
    {
        BuffHudSignal _s = new BuffHudSignal();
        _s.id = _id;
        _s.op = _op;
        return _s;
    }
}
