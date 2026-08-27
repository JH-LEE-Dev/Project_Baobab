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

    // 이벤트별로 마지막으로 실제 진동을 울린 시각(unscaled). 묶음 간격 판정에 쓴다.
    private static readonly float[] lastPlayedTimes = new float[(int)EHapticEvent.Count];

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

        // 묶음 판정의 기준은 "마지막으로 울린 시각"이다. 막힌 요청은 이 시각을 갱신하지 않는다.
        //
        // 처음에는 "마지막으로 요청이 들어온 시각"을 기준으로 삼았는데, 그러면 요청이 끊이지 않고
        // 이어지는 동안 창이 계속 뒤로 밀려 영영 열리지 않는다. 나무를 연속으로 팰 때가 정확히
        // 그 상황이라(도끼 타격 + 쇼크웨이브 판정이 0.04초 간격으로 계속 들어온다) 첫 한 번만
        // 울리고 그 뒤로는 아무리 패도 진동이 오지 않았다.
        //
        // 울린 시각 기준으로 바꾸면 쇼크웨이브가 퍼지는 동안 0.12초마다 한 번씩 끊어 울린다.
        // 같은 프레임에 여러 그루가 맞아도 한 번인 것은 그대로이고, 휘두를 때마다 확실히 울린다.
        // 쇼크웨이브가 훑는 내내 한 번만 울리게 하고 싶다면 HapticPresets에서 이 이벤트의 묶음
        // 간격을 쇼크웨이브 지속시간(기본 0.5초)까지 올리면 되지만, 그만큼 빠른 연타에서 진동이
        // 빠지는 것을 감수해야 한다.
        if (_burstInterval > 0f && _now - lastPlayedTimes[_index] < _burstInterval) return;

        HapticPattern _pattern = HapticPresets.GetPattern(_event);
        if (null == _pattern) return;

        lastPlayedTimes[_index] = _now;
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
        for (int i = 0; i < lastPlayedTimes.Length; i++)
        {
            lastPlayedTimes[i] = -999f;
        }
    }
}
