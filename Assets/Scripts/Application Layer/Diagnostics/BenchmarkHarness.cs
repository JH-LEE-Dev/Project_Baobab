// ─────────────────────────────────────────────────────────────────────────────
// 이 파일 전체는 에디터이거나 BAOBAB_BENCHMARK 디파인이 있을 때만 컴파일됩니다.
//
// [왜 동작 조건이 아니라 컴파일로 끊는가]
// 처음에는 코드를 항상 컴파일해 두고 -benchmark 인자가 없으면 켜지지만 않게 했습니다.
// 그래도 출하 빌드에는 측정용 클래스와 한글 문자열이 그대로 실려 나가고,
// RuntimeInitializeOnLoads.json 에 부트스트랩 항목까지 등록됩니다.
// 유저에게 아무 값도 주지 않는 코드를 배포하지 않기 위해 컴파일 단계에서 끊습니다.
//
// [빌드에 넣는 방법]
// Tools > 빌드 > "벤치마크 하네스 빌드에 포함" 을 켜고 빌드하십시오. (BenchmarkBuildToggle)
// 스토어에 올릴 빌드에서는 반드시 끄십시오. 켜진 채로 빌드하면 빌드 로그에 경고가 남습니다.
//
// 에디터에서는 디파인 없이도 항상 살아 있습니다. F9로 바로 측정할 수 있고,
// 에디터에서 켜져 있다는 사실이 빌드 결과물에는 아무 영향을 주지 않습니다.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR || BAOBAB_BENCHMARK

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// 최소사양 판정을 위한 인게임 측정 하네스입니다. 정해진 길이만큼 프레임 간격을 기록하고,
/// 이 기계의 사양과 함께 리포트 파일로 남깁니다.
///
/// [쓰는 법]
/// 1) 릴리즈 빌드를 -benchmark 인자와 함께 실행합니다.
///      LumberBoy.exe -benchmark -benchmarkSeconds 180 -benchmarkLabel "2core-50pct"
/// 2) 설정에서 FPS를 반드시 Unlimited로 맞춥니다. (아래 [측정 유효성] 참고)
/// 3) 측정할 세이브를 불러와 벤치마크 구간에 선 뒤 F9로 시작합니다.
/// 4) 지정한 시간이 지나면 자동으로 멈추고 리포트를 씁니다. F9로 직접 멈춰도 됩니다.
///
/// 리포트는 persistentDataPath 아래 Benchmarks 폴더에 쌓입니다.
/// (%USERPROFILE%\AppData\LocalLow\HiddenStageGames\LumberBoy\Benchmarks)
///
/// [왜 세이브 폴더에 쓰지 않는가]
/// GamePaths.SaveFolder는 Steam 클라우드와 STOVE 런처가 통째로 동기화하는 폴더입니다.
/// 측정할 때마다 생기는 리포트가 그 안에 쌓이면 유저의 클라우드 용량과 동기화 시간을 갉아먹고,
/// 최악의 경우 동기화가 잡고 있는 동안 세이브 쪽 입출력이 밀립니다. 진행도와 아무 관련이 없는
/// 파일이므로 처음부터 다른 폴더에 씁니다. persistentDataPath는 Player.log와 같은 자리라
/// 유저에게 "로그 폴더"라고 안내하기도 쉽습니다.
///
/// [측정 유효성 - 이 하네스가 FPS 설정을 건드리지 않는 이유]
/// 프레임 상한을 정하는 주체는 SettingsManager 단독입니다(FrameRateLimiter 주석 참고).
/// 여기서 상한을 임의로 풀면 그 값이 유저 설정으로 저장되어 측정이 끝난 뒤에도 남습니다.
/// 그래서 이 하네스는 상한을 고치지 않고 "지금 상한이 걸려 있다"는 사실을 리포트에 크게 적습니다.
///
/// 상한이 걸린 채로 잰 수치는 최소사양 근거로 쓸 수 없습니다. 60fps 상한에서 재면 아무리 느린
/// 기계라도 대부분의 프레임이 16.6ms로 나와서 통과처럼 보이기 때문입니다. 실제로 이 프로젝트의
/// FrameRateLimiter는 메인 스레드를 직접 재우므로, 상한이 걸린 측정은 게임의 성능이 아니라
/// 리미터의 모양을 재는 것이 됩니다.
///
/// [출하 빌드에서의 비용]
/// 없습니다. 파일 맨 위의 #if 때문에 출하 빌드에는 이 클래스가 아예 컴파일되지 않습니다.
/// 벤치마크 빌드에서도 -benchmark 인자가 없으면 컴포넌트가 생성되지 않으므로, 유저가 단축키를
/// 우연히 눌러 측정이 시작되는 일도 없습니다.
/// </summary>
public class BenchmarkHarness : MonoBehaviour
{
    // 명령줄 인자
    private const string ARG_ENABLE = "-benchmark";
    private const string ARG_SECONDS = "-benchmarkSeconds";
    private const string ARG_WARMUP = "-benchmarkWarmup";
    private const string ARG_TARGET = "-benchmarkTarget";
    private const string ARG_LABEL = "-benchmarkLabel";
    private const string ARG_AUTO_START = "-benchmarkAutoStart";
    private const string ARG_QUIT = "-benchmarkQuit";
    private const string ARG_NO_CSV = "-benchmarkNoCsv";
    private const string ARG_NO_HUD = "-benchmarkNoHud";

    private const double DEFAULT_SECONDS = 180.0;
    private const double DEFAULT_WARMUP = 30.0;
    private const double DEFAULT_TARGET_FPS = 60.0;

    /// <summary>
    /// 배열을 미리 잡을 때 가정하는 최대 프레임레이트입니다. 저해상도 렌더 타깃이라 고사양
    /// 기계에서는 수백 fps가 나오므로 넉넉히 잡습니다. 이보다 빨라서 배열이 먼저 차면 그 시점에
    /// 기록을 멈추고 리포트에 그 사실을 적습니다. (그때까지의 수치는 그대로 유효합니다)
    /// </summary>
    private const int MAX_EXPECTED_FPS = 1000;

    /// <summary>float 배열 기준 약 4.8MB입니다. 이 이상은 측정 편의보다 메모리 부담이 큽니다.</summary>
    private const int MAX_CAPACITY = 1200000;

    private const int MIN_CAPACITY = 1000;

    /// <summary>
    /// 측정 시작 직후 버릴 프레임 수입니다. 하네스 자신의 시작 비용(시작 로그, HUD의 첫 IMGUI
    /// 호출)이 첫 프레임들에 몰려서, 버리지 않으면 그 비용이 "최악 프레임"으로 기록됩니다.
    /// 워밍업 구간 안이라 통계에는 안 들어가지만, 전체 구간 수치와 CSV를 오염시킵니다.
    /// </summary>
    private const int SKIP_FRAMES_ON_START = 5;

    /// <summary>HUD 문자열을 다시 만드는 간격(초)입니다. 매 프레임 문자열을 만들면 그 할당이 측정에 섞입니다.</summary>
    private const double HUD_REFRESH_INTERVAL = 0.25;

    /// <summary>리포트를 쓴 뒤 화면에 파일 이름을 띄워 두는 시간(초)입니다.</summary>
    private const double REPORT_TOAST_SECONDS = 10.0;

    private const string OUTPUT_FOLDER_NAME = "Benchmarks";

    /// <summary>
    /// Time.realtimeSinceStartup은 프레임 경계에서만 갱신되고 Time.unscaledDeltaTime은
    /// maximumDeltaTime에 걸려 잘릴 수 있어, 둘 다 프레임 타임 측정에는 쓸 수 없습니다.
    /// FrameRateLimiter가 같은 이유로 Stopwatch를 씁니다.
    /// </summary>
    private static readonly Stopwatch clock = Stopwatch.StartNew();

    private static BenchmarkHarness instance;

    private double configuredSeconds = DEFAULT_SECONDS;
    private double configuredWarmup = DEFAULT_WARMUP;
    private double configuredTargetFps = DEFAULT_TARGET_FPS;
    private string configuredLabel = string.Empty;
    private bool bQuitAfterReport;
    private bool bWriteCsv = true;
    private bool bShowHud = true;

    private FrameTimeRecorder recorder;
    private bool bRecording;
    private double runStartTime;
    private double lastFrameTime;
    /// <summary>남은 "버릴 프레임" 수입니다. 0보다 크면 이번 프레임 간격을 기록하지 않습니다.</summary>
    private int skipFrameCount;

    /// <summary>측정 중 창 포커스를 잃었는지 여부입니다. 잃었다면 그 구간의 수치는 신뢰할 수 없습니다.</summary>
    private bool bLostFocusDuringRun;

    /// <summary>측정 중 씬이 바뀐 지점입니다. (경과 초, 씬 이름)</summary>
    private readonly List<KeyValuePair<double, string>> sceneMarks = new List<KeyValuePair<double, string>>(8);

    private string hudText = string.Empty;
    private double hudNextRefreshTime;
    private GUIStyle hudStyle;

    /// <summary>마지막으로 쓴 리포트 경로입니다. 파일 이름만 HUD에 잠깐 띄워 유저가 파일을 찾게 돕습니다.</summary>
    private string lastReportPath = string.Empty;
    private double lastReportShownUntil;

    /// <summary>
    /// 씬에 배치할 필요 없이 스스로 붙습니다. AfterSceneLoad를 쓰는 이유는 SettingsManager와
    /// 같습니다 - BeforeSceneLoad 시점에 만든 오브젝트는 DontDestroyOnLoad 보호가 보장되지 않아
    /// 첫 씬 로드에서 조용히 사라집니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (null != instance) return;
        if (false == ShouldEnable()) return;

        GameObject _go = new GameObject("[BenchmarkHarness]");
        instance = _go.AddComponent<BenchmarkHarness>();
        DontDestroyOnLoad(_go);
    }

    /// <summary>
    /// 이번 실행에서 하네스를 켤지 정합니다.
    ///
    /// 여기까지 왔다는 것은 이미 에디터이거나 BAOBAB_BENCHMARK 빌드라는 뜻입니다(파일 맨 위 #if).
    /// 그래서 "빌드에 넣을지"가 아니라 "이번 실행에서 켤지"만 판단합니다.
    ///
    /// 에디터는 조건 없이 켭니다. 에디터에는 명령줄 인자를 줄 방법이 사실상 없고, F9를 바로 쓰는
    /// 편이 편합니다. 대신 에디터 수치는 최소사양 근거로 쓸 수 없습니다 - 에디터 오버헤드가 섞이고,
    /// PoolSettings.CollectionCheck가 켜져 있어 풀을 격하게 쓰는 구간이 실제보다 훨씬 무겁게
    /// 나옵니다. 그 사실은 리포트의 유효성 항목에 자동으로 적힙니다.
    ///
    /// 벤치마크 빌드에서는 -benchmark 인자를 요구합니다. 같은 빌드 하나로 "평소처럼 실행"과
    /// "측정 실행"을 모두 할 수 있어야, 하네스가 켜져 있다는 것 자체의 영향을 비교할 수 있습니다.
    /// </summary>
    private static bool ShouldEnable()
    {
        if (true == Application.isEditor) return true;

        return true == HasCommandLineFlag(ARG_ENABLE);
    }

    private void Awake()
    {
        ReadCommandLine();

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log($"[Benchmark] 하네스 활성화. F9로 시작/중지. 길이 {configuredSeconds:0}초, 워밍업 {configuredWarmup:0}초, 목표 {configuredTargetFps:0}fps.");

        double _autoStart = ReadCommandLineDouble(ARG_AUTO_START, -1.0);

        if (0.0 <= _autoStart) Invoke(nameof(StartRun), (float)_autoStart);
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this) instance = null;
    }

    /// <summary>
    /// 측정 도중에 창 포커스를 잃으면(다른 창을 띄우거나 alt-tab) 프레임 타임이 통째로 오염됩니다.
    /// runInBackground가 꺼져 있으면 아예 멈추고, 켜져 있어도 OS가 우선순위를 낮춥니다.
    /// 되돌릴 방법이 없으므로 사실만 기록해 리포트에 경고로 남깁니다.
    /// </summary>
    private void OnApplicationFocus(bool _hasFocus)
    {
        if (true == bRecording && false == _hasFocus) bLostFocusDuringRun = true;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene _scene, UnityEngine.SceneManagement.LoadSceneMode _mode)
    {
        if (false == bRecording) return;

        sceneMarks.Add(new KeyValuePair<double, string>(clock.Elapsed.TotalSeconds - runStartTime, _scene.name));

        // 씬 로드에 걸린 시간이 다음 프레임 간격으로 잡히면 최악 프레임이 수백 ms로 튑니다.
        // 그것은 이 기계의 프레임 성능이 아니라 로딩 시간이므로 한 프레임만 버립니다.
        skipFrameCount = 1;
    }

    private void Update()
    {
        PollHotkey();

        if (false == bRecording) return;

        double _now = clock.Elapsed.TotalSeconds;
        double _delta = _now - lastFrameTime;
        lastFrameTime = _now;

        if (0 < skipFrameCount)
        {
            --skipFrameCount;
        }
        else if (false == recorder.Record(_delta))
        {
            // 배열이 찼습니다. 지금까지의 수치는 그대로 유효하므로 정상 종료로 처리합니다.
            StopRun();
            return;
        }

        if (0.0 < configuredSeconds && configuredSeconds <= (_now - runStartTime)) StopRun();
    }

    private void PollHotkey()
    {
        Keyboard _keyboard = Keyboard.current;

        if (null == _keyboard) return;
        if (false == _keyboard.f9Key.wasPressedThisFrame) return;

        if (true == bRecording) StopRun();
        else StartRun();
    }

    /// <summary>측정을 시작합니다. 이미 측정 중이면 아무 일도 하지 않습니다.</summary>
    public void StartRun()
    {
        if (true == bRecording) return;

        recorder = new FrameTimeRecorder(ResolveCapacity());

        sceneMarks.Clear();
        sceneMarks.Add(new KeyValuePair<double, string>(0.0, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));

        bLostFocusDuringRun = false;
        runStartTime = clock.Elapsed.TotalSeconds;
        lastFrameTime = runStartTime;

        // 시작 직후 몇 프레임은 실제 프레임 간격이 아닙니다. 첫 델타는 StartRun이 불린 시점부터
        // 다음 Update까지의 간격이고, 그 다음 프레임에는 시작 로그 출력과 HUD의 첫 OnGUI가 몰립니다.
        // (IMGUI는 첫 호출에서 GUIStyle과 내부 상태를 만드느라 수십 ms를 씁니다. 실측에서 시작
        //  직후 85ms짜리 프레임이 잡혔는데, 그것은 게임이 아니라 이 하네스 자신의 비용이었습니다)
        skipFrameCount = SKIP_FRAMES_ON_START;
        bRecording = true;

        Debug.Log($"[Benchmark] 측정 시작. 최대 {recorder.Capacity:N0} 프레임.");
    }

    /// <summary>측정을 멈추고 리포트를 씁니다.</summary>
    public void StopRun()
    {
        if (false == bRecording) return;

        bRecording = false;
        CancelInvoke(nameof(StartRun));

        FrameTimeStats _warm = recorder.Analyze(configuredWarmup, configuredTargetFps);
        FrameTimeStats _all = recorder.Analyze(0.0, configuredTargetFps);

        string _path = WriteReport(_warm, _all);

        if (false == string.IsNullOrEmpty(_path))
        {
            lastReportPath = _path;
            lastReportShownUntil = clock.Elapsed.TotalSeconds + REPORT_TOAST_SECONDS;
        }

        Debug.Log($"[Benchmark] 측정 종료. 표본 {_warm.sampleCount:N0} / 평균 {_warm.averageFps:0.0} fps / 1% low {_warm.low1PercentFps:0.0} fps / 판정 {(true == _warm.Passed ? "통과" : "미달")}");

        if (true == bQuitAfterReport) Application.Quit();
    }

    /// <summary>
    /// 배열 크기를 정합니다. 길이 제한이 없는(직접 멈추는) 측정은 기본 길이를 기준으로 잡되,
    /// 어떤 경우에도 MAX_CAPACITY를 넘지 않습니다.
    /// </summary>
    private int ResolveCapacity()
    {
        double _seconds = 0.0 < configuredSeconds ? configuredSeconds : DEFAULT_SECONDS;
        double _capacity = _seconds * MAX_EXPECTED_FPS;

        if (MAX_CAPACITY < _capacity) _capacity = MAX_CAPACITY;
        if (MIN_CAPACITY > _capacity) _capacity = MIN_CAPACITY;

        return (int)_capacity;
    }

    // ── 리포트 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 리포트를 파일로 씁니다. 실패해도 게임은 계속 돌아야 하므로 예외를 밖으로 흘리지 않습니다.
    /// 경로를 로그에 실을 때는 반드시 Redact를 통과시킵니다 - 계정 폴더 이름이 그대로 들어가고,
    /// Debug.Log는 Sentry가 브레드크럼으로 주워 갑니다. (GamePaths.Redact 주석 참고)
    /// </summary>
    private string WriteReport(FrameTimeStats _warm, FrameTimeStats _all)
    {
        try
        {
            string _folder = Path.Combine(Application.persistentDataPath, OUTPUT_FOLDER_NAME);
            Directory.CreateDirectory(_folder);

            string _stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string _suffix = string.IsNullOrEmpty(configuredLabel) ? string.Empty : $"_{Sanitize(configuredLabel)}";
            string _textPath = Path.Combine(_folder, $"Benchmark_{_stamp}{_suffix}.txt");

            File.WriteAllText(_textPath, BuildReportText(_warm, _all), new UTF8Encoding(true));

            if (true == bWriteCsv) WriteCsv(Path.Combine(_folder, $"Benchmark_{_stamp}{_suffix}.csv"));

            Debug.Log($"[Benchmark] 리포트 저장: {GamePaths.Redact(_textPath)}");

            return _textPath;
        }
        catch (Exception _e)
        {
            Debug.LogError(GamePaths.Redact($"[Benchmark] 리포트 저장 실패: {_e.Message}"));
            return string.Empty;
        }
    }

    private string BuildReportText(FrameTimeStats _warm, FrameTimeStats _all)
    {
        StringBuilder _sb = new StringBuilder(4096);

        _sb.AppendLine("=== LumberBoy 벤치마크 리포트 ===");
        _sb.AppendLine($"기록 시각         : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _sb.AppendLine($"라벨              : {(string.IsNullOrEmpty(configuredLabel) ? "(없음)" : configuredLabel)}");
        _sb.AppendLine($"목표              : {configuredTargetFps:0} fps");
        _sb.AppendLine();

        AppendValidity(_sb, _warm);

        _sb.AppendLine($"[결과 - 워밍업 {configuredWarmup:0.#}초 제외]");
        AppendStats(_sb, _warm);
        _sb.AppendLine();

        _sb.AppendLine("[참고 - 워밍업 포함 전체 구간]");
        AppendStats(_sb, _all);
        _sb.AppendLine();

        AppendSceneMarks(_sb);

        SystemSpecReport.Append(_sb);

        return _sb.ToString();
    }

    /// <summary>
    /// 이 수치를 최소사양 근거로 써도 되는지를 맨 위에 적습니다.
    /// 리포트를 몇 달 뒤에 다시 열었을 때 어떤 조건에서 잰 것인지 기억에 의존하지 않게 하는 것이
    /// 이 블록의 목적입니다.
    /// </summary>
    private void AppendValidity(StringBuilder _sb, FrameTimeStats _stats)
    {
        List<string> _problems = new List<string>(8);

        if (true == SettingsManager.HasInstance && EFPS.Unlimited != SettingsManager.Instance.Current.fps)
        {
            _problems.Add($"FPS 설정이 {SettingsManager.Instance.Current.fps}입니다. 상한이 걸린 상태의 프레임 타임은 게임 성능이 아니라 리미터의 모양입니다. 설정에서 Unlimited로 바꾸고 다시 재십시오.");
        }

        if (0 != QualitySettings.vSyncCount) _problems.Add($"vSyncCount가 {QualitySettings.vSyncCount}입니다. 주사율이 상한으로 작동합니다.");
        if (0 < Application.targetFrameRate) _problems.Add($"targetFrameRate가 {Application.targetFrameRate}입니다. 상한이 걸려 있습니다.");
        if (true == Application.isEditor) _problems.Add("에디터에서 실행 중입니다. 에디터 오버헤드가 섞여 있어 빌드와 수치가 다릅니다.");
        else if (true == Debug.isDebugBuild) _problems.Add("개발자 빌드입니다. 프로파일러 훅 때문에 CPU가 더 들며, 릴리즈 빌드보다 느리게 나옵니다.");
        if (true == bLostFocusDuringRun) _problems.Add("측정 도중 창 포커스를 잃었습니다. 그 구간의 프레임 타임은 신뢰할 수 없습니다.");
        if (true == recorder.IsFull) _problems.Add($"기록 배열({recorder.Capacity:N0} 프레임)이 가득 차 측정이 조기 종료되었습니다. 구간 자체는 유효합니다.");
        if (FrameTimeRecorder.MIN_MEANINGFUL_SAMPLES > _stats.sampleCount) _problems.Add($"표본이 {_stats.sampleCount:N0} 프레임뿐입니다. 백분위 수치가 흔들립니다. 더 길게 재십시오.");
        if (true == bShowHud) _problems.Add("측정 중 HUD가 켜져 있었습니다. IMGUI 비용이 소량 섞입니다. 엄밀한 측정에는 -benchmarkNoHud를 쓰십시오.");

        if (0 == _problems.Count)
        {
            _sb.AppendLine("[측정 유효성] 유효 - 이 수치를 최소사양 근거로 쓸 수 있습니다.");
            _sb.AppendLine();
            return;
        }

        _sb.AppendLine("[측정 유효성] 주의 - 아래 항목을 확인하십시오.");

        for (int _i = 0; _i < _problems.Count; ++_i) _sb.AppendLine($"  - {_problems[_i]}");

        _sb.AppendLine();
    }

    private void AppendStats(StringBuilder _sb, FrameTimeStats _stats)
    {
        if (0 >= _stats.sampleCount)
        {
            _sb.AppendLine("  (표본 없음 - 측정 길이가 워밍업보다 짧습니다)");
            return;
        }

        _sb.AppendLine($"  표본            : {_stats.sampleCount:N0} 프레임 / {_stats.durationSeconds:0.0} 초");
        _sb.AppendLine($"  평균            : {_stats.averageFps:0.0} fps ({_stats.averageMs:0.00} ms)");
        _sb.AppendLine($"  중앙값          : {1000.0 / _stats.medianMs:0.0} fps ({_stats.medianMs:0.00} ms)");
        _sb.AppendLine($"  1% low          : {_stats.low1PercentFps:0.0} fps ({_stats.percentile99Ms:0.00} ms)   <- 판정 기준");
        _sb.AppendLine($"  0.1% low        : {_stats.low01PercentFps:0.0} fps ({_stats.percentile999Ms:0.00} ms)");
        _sb.AppendLine($"  최악 프레임     : {_stats.worstMs:0.00} ms");
        _sb.AppendLine($"  {_stats.targetFps:0}fps 미달     : {_stats.overBudgetRatio * 100.0:0.00} % ({_stats.overBudgetCount:N0} 프레임)");
        _sb.AppendLine($"  판정            : {(true == _stats.Passed ? "통과" : "미달 - 이 사양을 최소사양으로 표기하면 안 됩니다")}");
    }

    private void AppendSceneMarks(StringBuilder _sb)
    {
        _sb.AppendLine("[씬 구간]");

        for (int _i = 0; _i < sceneMarks.Count; ++_i)
        {
            _sb.AppendLine($"  {sceneMarks[_i].Key,8:0.0}s  {sceneMarks[_i].Value}");
        }

        _sb.AppendLine();
    }

    /// <summary>
    /// 원본 프레임 타임을 CSV로 내보냅니다.
    ///
    /// 열 이름을 PresentMon과 똑같이(TimeInSeconds, msBetweenPresents) 맞춘 것은 의도적입니다.
    /// 외부에서 PresentMon으로 잰 캡처와 이 파일을 같은 분석 스크립트로 돌릴 수 있어, 두 측정이
    /// 서로를 검증하게 됩니다.
    ///
    /// 수십만 줄이 나오므로 문자열을 통째로 만들지 않고 StreamWriter로 흘려보냅니다.
    /// 숫자는 반드시 InvariantCulture로 씁니다 - 소수점이 쉼표인 로캘에서 CSV가 깨집니다.
    /// </summary>
    private void WriteCsv(string _path)
    {
        using (StreamWriter _writer = new StreamWriter(_path, false, new UTF8Encoding(false)))
        {
            _writer.WriteLine("TimeInSeconds,msBetweenPresents");

            recorder.ForEachSample((_seconds, _ms) =>
            {
                _writer.Write(_seconds.ToString("0.00000", CultureInfo.InvariantCulture));
                _writer.Write(',');
                _writer.WriteLine(_ms.ToString("0.000", CultureInfo.InvariantCulture));
            });
        }
    }

    /// <summary>라벨이 그대로 파일 이름에 들어가므로 경로에 못 쓰는 글자를 걸러냅니다.</summary>
    private static string Sanitize(string _text)
    {
        char[] _invalid = Path.GetInvalidFileNameChars();
        StringBuilder _sb = new StringBuilder(_text.Length);

        for (int _i = 0; _i < _text.Length; ++_i)
        {
            _sb.Append(0 <= Array.IndexOf(_invalid, _text[_i]) ? '_' : _text[_i]);
        }

        return _sb.ToString();
    }

    // ── 명령줄 ────────────────────────────────────────────────────────────────

    private void ReadCommandLine()
    {
        configuredSeconds = ReadCommandLineDouble(ARG_SECONDS, DEFAULT_SECONDS);
        configuredWarmup = ReadCommandLineDouble(ARG_WARMUP, DEFAULT_WARMUP);
        configuredTargetFps = ReadCommandLineDouble(ARG_TARGET, DEFAULT_TARGET_FPS);
        configuredLabel = ReadCommandLineValue(ARG_LABEL, string.Empty);
        bQuitAfterReport = HasCommandLineFlag(ARG_QUIT);
        bWriteCsv = false == HasCommandLineFlag(ARG_NO_CSV);
        bShowHud = false == HasCommandLineFlag(ARG_NO_HUD);

        // 길이는 0 이하를 "직접 멈출 때까지"로 해석합니다. 목표 fps는 0이면 예산 계산이
        // 무한대가 되어 모든 프레임이 통과하므로 되돌립니다.
        if (0.0 >= configuredTargetFps) configuredTargetFps = DEFAULT_TARGET_FPS;
        if (0.0 > configuredWarmup) configuredWarmup = 0.0;
    }

    private static bool HasCommandLineFlag(string _name)
    {
        string[] _args = Environment.GetCommandLineArgs();

        for (int _i = 0; _i < _args.Length; ++_i)
        {
            if (true == string.Equals(_args[_i], _name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static string ReadCommandLineValue(string _name, string _fallback)
    {
        string[] _args = Environment.GetCommandLineArgs();

        for (int _i = 0; _i < _args.Length - 1; ++_i)
        {
            if (true == string.Equals(_args[_i], _name, StringComparison.OrdinalIgnoreCase)) return _args[_i + 1];
        }

        return _fallback;
    }

    /// <summary>
    /// 숫자 인자를 읽습니다. 반드시 InvariantCulture로 파싱해야 합니다 - 소수점이 쉼표인
    /// 로캘에서 "180.5"가 조용히 실패해 기본값으로 돌아갑니다.
    /// </summary>
    private static double ReadCommandLineDouble(string _name, double _fallback)
    {
        string _raw = ReadCommandLineValue(_name, null);

        if (true == string.IsNullOrEmpty(_raw)) return _fallback;

        if (false == double.TryParse(_raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double _value))
        {
            Debug.LogWarning($"[Benchmark] {_name} 값을 숫자로 읽지 못했습니다: '{_raw}'. 기본값 {_fallback}을 씁니다.");
            return _fallback;
        }

        return _value;
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 측정 상태를 화면 왼쪽 위에 표시합니다.
    ///
    /// IMGUI는 호출할 때마다 할당이 생기므로, 문자열은 HUD_REFRESH_INTERVAL마다 한 번만 만들고
    /// 나머지 프레임은 만들어 둔 것을 그립니다. 그래도 비용이 0은 아니라서, 켜져 있었다는 사실을
    /// 리포트의 유효성 항목에 남깁니다. 엄밀한 측정에는 -benchmarkNoHud를 쓰십시오.
    /// </summary>
    private void OnGUI()
    {
        if (false == bShowHud) return;

        double _now = clock.Elapsed.TotalSeconds;
        bool _showingReportPath = _now < lastReportShownUntil;

        if (false == bRecording && false == _showingReportPath) return;

        if (null == hudStyle)
        {
            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.alignment = TextAnchor.UpperLeft;
            hudStyle.fontSize = Mathf.Max(12, Screen.height / 40);
        }

        if (_now >= hudNextRefreshTime)
        {
            hudNextRefreshTime = _now + HUD_REFRESH_INTERVAL;
            hudText = BuildHudText(_now);
        }

        Rect _rect = new Rect(12f, 12f, Screen.width - 24f, Screen.height * 0.4f);

        hudStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(_rect.x + 1f, _rect.y + 1f, _rect.width, _rect.height), hudText, hudStyle);

        hudStyle.normal.textColor = true == bRecording ? Color.red : Color.green;
        GUI.Label(_rect, hudText, hudStyle);
    }

    private string BuildHudText(double _now)
    {
        if (false == bRecording)
        {
            // 전체 경로는 계정 폴더 이름을 화면에 띄우게 되므로 파일 이름만 보여줍니다.
            // 유저가 스크린샷을 그대로 올리는 경우를 고려한 것입니다.
            return $"BENCHMARK 저장됨: {Path.GetFileName(lastReportPath)}\n(AppData\\LocalLow 아래 {OUTPUT_FOLDER_NAME} 폴더)";
        }

        double _elapsed = _now - runStartTime;
        string _remain = 0.0 < configuredSeconds ? $" / {configuredSeconds:0}s" : " (F9로 종료)";

        // 워밍업이 끝나기 전에 멈추면 통계 구간의 표본이 0이 됩니다. 그 사실을 멈추고 나서
        // 리포트로 알면 측정을 통째로 다시 해야 하므로, 남은 시간을 진행 중에 보여줍니다.
        double _warmupLeft = configuredWarmup - _elapsed;
        string _phase = 0.0 < _warmupLeft
            ? $"워밍업 {_warmupLeft:0.0}s 남음 (지금 멈추면 통계 표본 없음)"
            : $"워밍업 통과 · 유효 구간 {_elapsed - configuredWarmup:0.0}s";

        return $"● REC {_elapsed:0.0}s{_remain}   {recorder.Count:N0} 프레임\n목표 {configuredTargetFps:0}fps · {_phase} · F9 중지";
    }
}

#endif
