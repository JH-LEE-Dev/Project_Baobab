// 이 파일은 BenchmarkHarness 전용입니다. 하네스와 컴파일 조건을 반드시 같이 가야 합니다
// (조건이 갈리면 하네스 없는 빌드에 쓰이지 않는 클래스만 남습니다).
// 조건을 바꾸는 이유와 빌드에 넣는 방법은 BenchmarkHarness.cs 맨 위 주석을 보십시오.
#if UNITY_EDITOR || BAOBAB_BENCHMARK

using System;

/// <summary>
/// 한 번의 측정 구간에서 계산된 프레임 타임 통계입니다. 단위는 밀리초와 fps가 섞여 있으니
/// 필드 이름의 접미사를 보고 쓰십시오.
/// </summary>
public struct FrameTimeStats
{
    /// <summary>통계에 실제로 들어간 프레임 수입니다. (워밍업 구간은 제외된 뒤의 수)</summary>
    public int sampleCount;

    /// <summary>통계 구간의 실제 길이(초)입니다.</summary>
    public double durationSeconds;

    public double averageMs;
    public double medianMs;

    /// <summary>하위 1% 경계의 프레임 타임입니다. 이 값의 역수가 흔히 말하는 "1% low"입니다.</summary>
    public double percentile99Ms;

    /// <summary>하위 0.1% 경계의 프레임 타임입니다.</summary>
    public double percentile999Ms;

    public double worstMs;

    public double averageFps;
    public double low1PercentFps;
    public double low01PercentFps;

    /// <summary>목표 프레임 예산을 넘긴 프레임의 비율(0~1)입니다.</summary>
    public double overBudgetRatio;

    /// <summary>목표 프레임 예산을 넘긴 프레임 수입니다.</summary>
    public int overBudgetCount;

    /// <summary>판정 기준이 된 목표 fps입니다.</summary>
    public double targetFps;

    /// <summary>
    /// 최소사양 판정입니다. 평균이 아니라 1% low를 기준으로 삼습니다.
    ///
    /// 평균 60fps는 프레임의 절반이 40fps여도 나옵니다. 최소사양은 "끊기지 않고 돌아간다"는
    /// 약속이므로, 평균으로 판정하면 표기한 사양에서 실제로는 버벅이는 상태를 통과시키게 됩니다.
    /// </summary>
    public bool Passed => low1PercentFps >= targetFps;
}

/// <summary>
/// 프레임 간격을 프레임마다 배열에 적어 두었다가, 측정이 끝난 뒤 한 번에 통계를 냅니다.
///
/// [왜 프레임마다 계산하지 않는가]
/// 측정 코드가 측정 대상을 느리게 만들면 그 수치는 쓸 수 없습니다. 이동 평균이나 정렬을
/// 프레임마다 돌리면 그 비용이 그대로 프레임 타임에 섞여 들어갑니다. 그래서 기록 중에는
/// 배열에 float 하나를 넣는 일만 하고, 정렬과 백분위 계산은 전부 끝난 뒤로 미룹니다.
///
/// [왜 배열을 미리 잡는가]
/// List로 늘려가면 용량이 두 배로 늘어나는 순간마다 큰 배열 할당과 복사가 일어나고, 하필
/// 그 프레임만 튀어서 1% low를 오염시킵니다. 측정이 망가지는 방식 중 가장 알아채기 어려운
/// 종류라, 처음부터 최대 길이만큼 잡아 두고 넘치면 기록을 멈춥니다.
///
/// [왜 float인가]
/// 밀리초 단위 프레임 타임(예: 16.66667)에 float의 유효자릿수 7자리는 충분하고,
/// double의 절반 메모리로 끝납니다. 누적 합처럼 오차가 쌓이는 계산은 전부 double로 합니다.
/// </summary>
public sealed class FrameTimeRecorder
{
    /// <summary>통계를 신뢰할 수 있다고 보는 최소 표본 수입니다.</summary>
    public const int MIN_MEANINGFUL_SAMPLES = 300;

    private readonly float[] samplesMs;

    /// <summary>기록된 프레임 수입니다.</summary>
    public int Count { get; private set; }

    /// <summary>배열을 다 채워 더 이상 기록하지 않는 상태입니다.</summary>
    public bool IsFull => Count >= samplesMs.Length;

    /// <summary>기록 가능한 최대 프레임 수입니다.</summary>
    public int Capacity => samplesMs.Length;

    public FrameTimeRecorder(int _capacity)
    {
        if (1 > _capacity) throw new ArgumentOutOfRangeException(nameof(_capacity));

        samplesMs = new float[_capacity];
    }

    /// <summary>
    /// 프레임 간격 하나를 기록합니다. 배열이 가득 찼으면 false를 돌려주고 아무것도 하지 않습니다.
    /// 이 메서드는 매 프레임 불리므로 할당도 분기도 최소로 유지해야 합니다.
    /// </summary>
    public bool Record(double _seconds)
    {
        if (true == IsFull) return false;

        samplesMs[Count] = (float)(_seconds * 1000.0);
        ++Count;

        return true;
    }

    public void Reset()
    {
        Count = 0;
    }

    /// <summary>
    /// 통계를 계산합니다. 정렬 때문에 비용이 크므로 측정 중에는 부르지 마십시오.
    /// </summary>
    /// <param name="_skipSeconds">
    /// 앞에서 잘라낼 워밍업 길이(초)입니다. 첫 실행의 셰이더 PSO 생성과 에셋 로드로 초반
    /// 프레임이 크게 튀는데, 그대로 두면 1% low가 실제보다 나쁘게 나옵니다.
    /// </param>
    /// <param name="_targetFps">통과/미달을 가르는 목표 fps입니다.</param>
    public FrameTimeStats Analyze(double _skipSeconds, double _targetFps)
    {
        FrameTimeStats _stats = default;
        _stats.targetFps = _targetFps;

        int _start = FindStartIndex(_skipSeconds);
        int _length = Count - _start;

        if (0 >= _length) return _stats;

        // 정렬은 원본을 흐트러뜨리므로 반드시 사본에 대고 합니다. CSV로 내보낼 때
        // 시간 순서가 필요하고, 다른 워밍업 길이로 다시 분석할 수도 있습니다.
        float[] _sorted = new float[_length];
        Array.Copy(samplesMs, _start, _sorted, 0, _length);
        Array.Sort(_sorted);

        double _budgetMs = 1000.0 / _targetFps;
        double _sumMs = 0.0;
        int _over = 0;

        for (int _i = 0; _i < _length; ++_i)
        {
            _sumMs += _sorted[_i];

            if (_sorted[_i] > _budgetMs) ++_over;
        }

        _stats.sampleCount = _length;
        _stats.durationSeconds = _sumMs / 1000.0;
        _stats.averageMs = _sumMs / _length;
        _stats.medianMs = Percentile(_sorted, 0.5);
        _stats.percentile99Ms = Percentile(_sorted, 0.99);
        _stats.percentile999Ms = Percentile(_sorted, 0.999);
        _stats.worstMs = _sorted[_length - 1];
        _stats.overBudgetCount = _over;
        _stats.overBudgetRatio = (double)_over / _length;

        _stats.averageFps = ToFps(_stats.averageMs);
        _stats.low1PercentFps = ToFps(_stats.percentile99Ms);
        _stats.low01PercentFps = ToFps(_stats.percentile999Ms);

        return _stats;
    }

    /// <summary>
    /// 앞에서부터 _skipSeconds 만큼을 건너뛴 지점의 인덱스를 찾습니다.
    /// 전부 잘려나가는 경우(측정 길이보다 워밍업이 긴 경우)에는 Count를 돌려주고,
    /// 호출부는 표본 0으로 처리합니다.
    /// </summary>
    private int FindStartIndex(double _skipSeconds)
    {
        if (0.0 >= _skipSeconds) return 0;

        double _elapsedMs = 0.0;
        double _skipMs = _skipSeconds * 1000.0;

        for (int _i = 0; _i < Count; ++_i)
        {
            _elapsedMs += samplesMs[_i];

            if (_elapsedMs >= _skipMs) return _i + 1;
        }

        return Count;
    }

    /// <summary>
    /// 오름차순 정렬된 배열에서 백분위 값을 꺼냅니다.
    ///
    /// "1% low"에는 두 가지 관행이 있습니다. 하위 1% 프레임들의 평균을 쓰는 방식과,
    /// 99번째 백분위 프레임 타임 하나를 쓰는 방식입니다. 여기서는 후자를 씁니다.
    /// 표본 수에 덜 민감하고, PresentMon 계열 도구와 CapFrameX의 기본 표기와 같아서
    /// 외부 측정값과 나란히 비교할 수 있기 때문입니다.
    /// </summary>
    private static double Percentile(float[] _sortedAscending, double _p)
    {
        int _index = (int)Math.Floor(_sortedAscending.Length * _p);

        if (_index >= _sortedAscending.Length) _index = _sortedAscending.Length - 1;
        if (0 > _index) _index = 0;

        return _sortedAscending[_index];
    }

    /// <summary>0으로 나누는 것을 막습니다. 프레임 타임이 0으로 기록되는 일은 없어야 하지만, 통계 계산이 리포트 전체를 날려서는 안 됩니다.</summary>
    private static double ToFps(double _milliseconds)
    {
        if (0.0 >= _milliseconds) return 0.0;

        return 1000.0 / _milliseconds;
    }

    /// <summary>
    /// 시간 순서대로 (경과 초, 프레임 타임 ms) 쌍을 훑습니다. CSV로 내보낼 때 씁니다.
    /// 배열을 통째로 복사해 돌려주면 수십만 개짜리 사본이 생기므로 콜백으로 흘려보냅니다.
    /// </summary>
    public void ForEachSample(Action<double, float> _onSample)
    {
        if (null == _onSample) return;

        double _elapsedSeconds = 0.0;

        for (int _i = 0; _i < Count; ++_i)
        {
            _elapsedSeconds += samplesMs[_i] / 1000.0;
            _onSample(_elapsedSeconds, samplesMs[_i]);
        }
    }
}

#endif
