public enum AutoSaveReason
{
    ArriveTown,        // 숲에서 마을로 돌아왔을 때
    DepartToForest,    // 마을에서 숲으로 출발할 때
    DepartToMainMenu,  // 마을에서 메인메뉴로 나갈 때
    DepartToEnding,    // 마을에서 엔딩으로 떠날 때 (하늘로 올라간 직후)
}

public struct AutoSaveRequestedSignal
{
    public AutoSaveReason reason;
    public AutoSaveRequestedSignal(AutoSaveReason _reason)
    {
        reason = _reason;
    }
}
