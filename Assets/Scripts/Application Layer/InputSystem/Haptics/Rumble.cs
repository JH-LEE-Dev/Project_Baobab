using UnityEngine;

/// <summary>
/// 게임 로직이 진동을 요청하는 창구입니다. Sound와 같은 자리, 같은 방식으로 씁니다.
/// 예: <c>Rumble.Play(EHapticEvent.TreeImpact);</c>
///
/// 파형은 HapticPresets가, 실제 모터 제어는 GamepadHaptics가 맡습니다. 호출부는 "무슨 일이
/// 일어났는지"만 알려주면 됩니다. 패드가 없든, 유저가 진동을 껐든, 아직 InputManager가 만들어지기
/// 전이든 그냥 아무 일도 일어나지 않으므로 호출부에서 검사할 필요가 없습니다.
/// </summary>
public static class Rumble
{
    private static GamepadHaptics haptics;

    // 이벤트별로 마지막 요청이 들어온 시각(unscaled). 묶음 간격 판정에 쓴다.
    private static readonly float[] lastRequestTimes = new float[(int)EHapticEvent.Count];

    static Rumble()
    {
        ResetTimes();
    }

    /// <summary>
    /// 진동 서비스를 연결합니다. InputManager가 자기 자신을 초기화하며 호출합니다.
    /// (씬을 넘나들며 InputManager가 새로 생기는 경우를 대비해 항상 마지막 것으로 덮어씁니다)
    /// </summary>
    public static void SetService(GamepadHaptics _haptics)
    {
        haptics = _haptics;
        ResetTimes();
    }

    /// <summary>연결을 끊습니다. 끊긴 뒤의 요청은 조용히 무시됩니다.</summary>
    public static void ClearService(GamepadHaptics _haptics)
    {
        // 이미 다음 InputManager가 자리를 잡은 뒤에 이전 것이 정리되며 호출할 수 있다.
        // 그때 새 서비스를 끊어버리면 그 씬에서 진동이 통째로 사라진다.
        if (haptics != _haptics) return;

        haptics = null;
    }

    /// <summary>
    /// 상황에 맞는 진동을 재생합니다.
    ///
    /// 같은 이벤트가 짧은 간격으로 몰려 들어오면(쇼크웨이브가 나무 수십 그루를 훑는 경우처럼)
    /// 한 번의 사건으로 묶어 첫 요청에만 울립니다. 묶는 시간은 HapticPresets가 이벤트별로 정합니다.
    /// </summary>
    public static void Play(EHapticEvent _event)
    {
        if (null == haptics) return;

        int _index = (int)_event;
        if (_index < 0 || _index >= (int)EHapticEvent.Count) return;

        float _now = Time.unscaledTime;
        float _burstInterval = HapticPresets.GetBurstInterval(_event);

        // 묶음 판정은 "마지막으로 울린 시각"이 아니라 "마지막으로 요청이 들어온 시각" 기준이다.
        // 쇼크웨이브처럼 요청이 끊이지 않고 이어지는 동안은 계속 같은 사건으로 봐야 하는데,
        // 울린 시각을 기준으로 하면 첫 진동이 끝나자마자 다음 요청이 새 사건으로 취급되어
        // 진동이 계속 다시 울린다.
        float _lastRequestTime = lastRequestTimes[_index];
        lastRequestTimes[_index] = _now;

        if (_burstInterval > 0f && _now - _lastRequestTime < _burstInterval) return;

        HapticPattern _pattern = HapticPresets.GetPattern(_event);
        if (null == _pattern) return;

        haptics.PlayPattern(_pattern);
    }

    /// <summary>재생 중인 진동을 즉시 멈춥니다. (연출이 끊기는 등 잔진동을 남기면 안 될 때)</summary>
    public static void Stop()
    {
        haptics?.Stop();
    }

    private static void ResetTimes()
    {
        // 게임을 켜자마자 일어난 이벤트가 "방금 전에도 있었다"로 오판되지 않도록 충분히 과거로 둔다.
        for (int i = 0; i < lastRequestTimes.Length; i++)
        {
            lastRequestTimes[i] = -999f;
        }
    }
}
