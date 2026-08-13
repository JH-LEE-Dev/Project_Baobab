public enum AutoSaveReason
{
    ArriveTown,      // 숲에서 마을로 돌아왔을 때
    DepartToForest,  // 마을에서 숲으로 출발할 때
}

public struct AutoSaveRequestedSignal
{
    public AutoSaveReason reason;
    public AutoSaveRequestedSignal(AutoSaveReason _reason)
    {
        reason = _reason;
    }
}
