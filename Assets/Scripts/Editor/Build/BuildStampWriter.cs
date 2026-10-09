using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 빌드 출력 폴더 최상위에 <b>이 빌드가 무엇인지</b> 적은 BUILD_STAMP.txt 를 남깁니다.
///
/// [왜 필요한가]
/// STEAM_DEMO 와 STOVE_DEMO 는 폴더를 열어 보면 파일 목록이 거의 같습니다. 이름 한 글자 차이라
/// 업로더의 폴더 선택 창에서 옆 폴더를 집어도 알아챌 방법이 없었고, 실제로 <b>Steam 빌드가
/// STOVE에 올라가 검수에서 반려</b>됐습니다(2026-09-25). 실행하면 Steam 클라이언트가 켜지는
/// 증상이었는데, 파일만 봐서는 어느 스토어 빌드인지 구분할 수 없었기 때문입니다.
///
/// 이 파일이 있으면 폴더가 스스로 말합니다. 사람이 열어 봐도 되고, UploadPreflightCheck 가
/// 업로드 전에 읽어 기대한 스토어와 대조합니다. 업로더의 파일 목록과 STOVE 무결성 검사 목록에도
/// 그대로 보입니다.
///
/// [순서]
/// BuildOutputSanitizer(10000) 뒤에 돕니다. 정리 규칙(*.log 등)에 이 파일이 걸리지는 않지만,
/// 정리가 끝난 최종 상태에 도장을 찍는 편이 뜻이 맞습니다.
///
/// [Development Build도 남깁니다]
/// 정리 스크립트와 달리 개발 빌드에도 씁니다. 대신 DEVELOPMENT=true 를 적어, 검사기가
/// "배포하면 안 되는 빌드"로 걸러낼 수 있게 합니다.
/// </summary>
public class BuildStampWriter : IPostprocessBuildWithReport
{
    public int callbackOrder => 10001;

    public const string FILE_NAME = "BUILD_STAMP.txt";

    private const string TAG = "[BuildStamp]";

    public void OnPostprocessBuild(BuildReport _report)
    {
        if (null == _report) return;
        if (BuildResult.Failed == _report.summary.result || BuildResult.Cancelled == _report.summary.result) return;

        string _root = BuildOutputSanitizer.ResolveOutputRoot(_report);

        if (true == string.IsNullOrEmpty(_root) || false == Directory.Exists(_root))
        {
            Debug.LogWarning($"{TAG} 출력 폴더를 찾지 못해 스탬프를 남기지 않았습니다: {_report.summary.outputPath}");
            return;
        }

        bool _development = 0 != (_report.summary.options & BuildOptions.Development);

        Dictionary<string, string> _stamp = Compose(_development);

        try
        {
            File.WriteAllLines(Path.Combine(_root, FILE_NAME), Serialize(_stamp));
            Debug.Log($"{TAG} {_stamp["STORE"]} / {_stamp["RELEASE"]} / {_stamp["VERSION"]} / {_stamp["GIT"]} → {Path.Combine(_root, FILE_NAME)}");
        }
        catch (Exception _e)
        {
            // 도장을 못 찍어도 빌드는 멀쩡하다. 검사기가 "스탬프 없음"으로 잡아 준다.
            Debug.LogWarning($"{TAG} 스탬프 기록 실패: {_e.Message}");
        }
    }

#region 내용

    public static Dictionary<string, string> Compose(bool _development)
    {
        BuildStore _store = PlatformBuildModeSwitcher.CurrentStore;
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;

        // 빌드 중에는 스트리퍼가 에셋을 고쳤다 되돌리고 유니티가 일부 에셋을 다시 저장해, 끝에서 보면
        // 늘 "변경 있음"으로 나온다. 그래서 빌드 시작 시점(BuildStampGitSnapshot)에 찍어 둔 값을 우선 쓴다.
        string _commit;
        bool _dirty;

        if (false == BuildStampGitSnapshot.TryTake(out _commit, out _dirty))
        {
            TryGetGitInfo(out _commit, out _dirty);
        }

        Dictionary<string, string> _d = new Dictionary<string, string>();

        _d["STORE"] = _store.ToString();
        _d["RELEASE"] = _release.ToString();
        bool _trailer = BuildRunner.IsTrailerBuildInProgress;

        _d["PURPOSE"] = true == _trailer ? "TRAILER" : "RELEASE";
        _d["FOLDER"] = (true == _trailer ? BuildRunner.TRAILER_FOLDER_PREFIX : string.Empty) + PlatformBuildModeSwitcher.BuildFolderName(_store, _release);
        _d["VERSION"] = PlayerSettings.bundleVersion;
        _d["PRODUCT"] = PlayerSettings.productName;
        _d["DEFINES"] = DescribeStoreDefines();
        _d["DEVELOPMENT"] = _development ? "true" : "false";
        _d["GIT"] = _commit;
        _d["GIT_DIRTY"] = _dirty ? "true" : "false";
        _d["UNITY"] = Application.unityVersion;
        _d["BUILT_AT"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

        return _d;
    }

    /// <summary>우리가 다루는 디파인만 골라 적습니다. DOTWEEN 같은 것은 스토어 판별에 무관합니다.</summary>
    private static string DescribeStoreDefines()
    {
        PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out string[] _all);

        List<string> _ours = new List<string>();

        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i].StartsWith("BAOBAB_", StringComparison.Ordinal) || "DISABLESTEAMWORKS" == _all[i])
            {
                _ours.Add(_all[i]);
            }
        }

        return (0 == _ours.Count) ? "(없음 = Steam 데모)" : string.Join(";", _ours);
    }

    private static string[] Serialize(Dictionary<string, string> _d)
    {
        List<string> _lines = new List<string>(_d.Count + 2);

        _lines.Add("# LumberBoy 빌드 스탬프. 이 폴더가 어느 스토어·배포용인지 적혀 있습니다. 업로드 전에 STORE 를 확인하십시오.");

        foreach (KeyValuePair<string, string> _kv in _d)
        {
            _lines.Add(_kv.Key + "=" + _kv.Value);
        }

        return _lines.ToArray();
    }

    /// <summary>빌드 폴더의 스탬프를 읽습니다. 없거나 깨졌으면 null 입니다.</summary>
    public static Dictionary<string, string> Read(string _root)
    {
        string _path = Path.Combine(_root, FILE_NAME);

        if (false == File.Exists(_path)) return null;

        try
        {
            Dictionary<string, string> _d = new Dictionary<string, string>();

            foreach (string _line in File.ReadAllLines(_path))
            {
                if (true == string.IsNullOrWhiteSpace(_line) || _line.StartsWith("#")) continue;

                int _eq = _line.IndexOf('=');

                if (_eq <= 0) continue;

                _d[_line.Substring(0, _eq).Trim()] = _line.Substring(_eq + 1).Trim();
            }

            return _d;
        }
        catch (Exception)
        {
            return null;
        }
    }

#endregion

#region git

    /// <summary>
    /// 현재 커밋(짧은 해시)과 작업 트리가 더러운지를 git 으로 묻습니다. git 이 없거나 실패하면
    /// "(unknown)" 과 false 를 돌려주고 빌드를 방해하지 않습니다.
    /// </summary>
    public static bool TryGetGitInfo(out string _commit, out bool _dirty)
    {
        _commit = "(unknown)";
        _dirty = false;

        string _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        if (false == TryRunGit(_projectRoot, "rev-parse --short HEAD", out string _head)) return false;

        _commit = _head.Trim();

        // status --porcelain 은 줄바꿈(LF/CRLF)만 다른 파일도 M 으로 올려, 이 프로젝트에서는 빌드마다
        // 프리팹 몇 개가 늘 걸린다. 내용이 실제로 다른 파일만 세기 위해 diff(정규화 후 비교)를 쓴다.
        //   diff --name-only         : 작업 트리 ↔ 인덱스
        //   diff --cached --name-only: 인덱스 ↔ HEAD
        //   ls-files --others        : 추적되지 않은 새 파일
        string _changed = string.Empty;

        if (true == TryRunGit(_projectRoot, "diff --name-only", out string _wt)) _changed += _wt + "\n";
        if (true == TryRunGit(_projectRoot, "diff --cached --name-only", out string _idx)) _changed += _idx + "\n";
        if (true == TryRunGit(_projectRoot, "ls-files --others --exclude-standard", out string _new)) _changed += _new;

        _dirty = HasChangesOutsideSwitcherFiles(_changed, _projectRoot);

        return true;
    }

    /// <summary>
    /// FontMaker(숫자 글리프 UI) 프리팹 guid 입니다. FontMaker 는 [ExecuteAlways] 에디터 미리보기에서 RectTransform 폭을
    /// 미리보기 문자열에 맞춰 바꾸는데, 이 값이 AbilityHUD·TentUI 의 중첩 프리팹 오버라이드로 빌드 중 저장되며
    /// 0 ↔ 23·7 을 오갑니다(2026-10-07~09). 실행 중 SetText 때마다 다시 계산되는 값이라 빌드 내용에 영향이 없습니다.
    /// </summary>
    private const string FONT_MAKER_PREFAB_GUID = "97d3436c2b4a2b04999ff852ad959c92";

    /// <summary>
    /// 스위처(PlatformBuildModeSwitcher)가 스토어·배포에 맞춰 바꾸는 파일은 dirty 로 치지 않습니다.
    /// 이 파일들은 스토어·배포를 전환하면 반드시 바뀌므로, 세면 STOVE·itch 빌드가 전부 "커밋 안 된 변경이 섞였다"로
    /// 나와 경고가 의미를 잃습니다. 그 파일들이 기대값과 맞는지는 PlatformConsistencyGuard 가 따로 봅니다.
    /// </summary>
    private static readonly string[] SWITCHER_MANAGED_FILES =
    {
        "ProjectSettings/ProjectSettings.asset",
        "Assets/Resources/Sentry/SentryOptions.asset",
        "Assets/Resources/GameAnalytics/Settings.asset",
        "steam_appid.txt",
    };

    private static bool HasChangesOutsideSwitcherFiles(string _pathsOnePerLine, string _projectRoot)
    {
        foreach (string _raw in _pathsOnePerLine.Split('\n'))
        {
            string _path = _raw.Trim().Trim('"').Replace('\\', '/');

            if (0 == _path.Length) continue;

            bool _managed = false;

            for (int i = 0; i < SWITCHER_MANAGED_FILES.Length; i++)
            {
                if (string.Equals(_path, SWITCHER_MANAGED_FILES[i], StringComparison.OrdinalIgnoreCase)) { _managed = true; break; }
            }

            if (true == _managed) continue;
            if (true == IsOnlyFontMakerWidthChange(_projectRoot, _path)) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 이 파일의 작업 트리 변경이 "FontMaker 중첩 프리팹의 m_SizeDelta.x 값" 줄뿐인지 봅니다.
    /// 한 줄이라도 다른 변경이 섞여 있으면 false 라서, 실제 변경은 그대로 dirty 로 잡힙니다.
    /// </summary>
    private static bool IsOnlyFontMakerWidthChange(string _projectRoot, string _path)
    {
        if (false == _path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return false;
        if (false == TryRunGit(_projectRoot, $"diff --ignore-cr-at-eol -U3 -- \"{_path}\"", out string _diff)) return false;

        string _lastTarget = string.Empty;
        string _lastProperty = string.Empty;
        int _changedLines = 0;

        foreach (string _raw in _diff.Split('\n'))
        {
            string _line = _raw.TrimEnd('\r');

            if (true == _line.StartsWith("+++") || true == _line.StartsWith("---") || true == _line.StartsWith("diff ") ||
                true == _line.StartsWith("index ") || true == _line.StartsWith("@@")) continue;
            if (0 == _line.Length) continue;

            char _kind = _line[0];
            string _body = _line.Substring(1).Trim();

            if (true == _body.StartsWith("- target:")) _lastTarget = _body;
            else if (true == _body.StartsWith("propertyPath:")) _lastProperty = _body;

            if ('+' != _kind && '-' != _kind) continue;

            _changedLines++;

            bool _isNumber = true == _body.StartsWith("value: ")
                             && true == double.TryParse(_body.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
            bool _isWidthValue = true == _isNumber
                                 && "propertyPath: m_SizeDelta.x" == _lastProperty
                                 && true == _lastTarget.Contains(FONT_MAKER_PREFAB_GUID);

            if (false == _isWidthValue) return false;
        }

        if (0 == _changedLines) return false;

        Debug.Log($"[BuildStamp] {_path} 의 변경은 FontMaker 미리보기 폭(m_SizeDelta.x) {_changedLines}줄뿐이라 GIT_DIRTY 로 치지 않습니다.");
        return true;
    }

    /// <summary>
    /// 두 커밋 사이에 바뀐 파일 경로를 돌려줍니다. 업로드 전 검사가 "빌드 뒤에 들어온 커밋이 desc(VDF)·문서뿐인가"를 볼 때 씁니다.
    /// </summary>
    public static bool TryGetChangedFilesBetween(string _from, string _to, out List<string> _paths)
    {
        _paths = new List<string>();

        string _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        if (false == TryRunGit(_projectRoot, $"diff --name-only {_from} {_to}", out string _out)) return false;

        foreach (string _raw in _out.Split('\n'))
        {
            string _path = _raw.Trim().Trim('"').Replace('\\', '/');
            if (0 < _path.Length) _paths.Add(_path);
        }

        return true;
    }

    private static bool TryRunGit(string _workingDir, string _args, out string _stdout)
    {
        _stdout = string.Empty;

        try
        {
            ProcessStartInfo _psi = new ProcessStartInfo("git", _args)
            {
                WorkingDirectory = _workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (Process _p = Process.Start(_psi))
            {
                if (null == _p) return false;

                _stdout = _p.StandardOutput.ReadToEnd();

                if (false == _p.WaitForExit(5000)) return false;

                return 0 == _p.ExitCode;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

#endregion
}

/// <summary>
/// 빌드가 에셋을 건드리기 전에 git 상태를 찍어 둡니다. BuildStampWriter 가 빌드 끝에 이 값을 씁니다.
///
/// [왜 시작 시점인가]
/// 빌드 중에는 DemoContentStripper 가 에셋을 고쳤다 되돌리고, 유니티가 일부 프리팹·설정을 다시 저장합니다.
/// 끝에서 git 을 보면 그 흔적 때문에 깨끗한 트리에서 빌드해도 GIT_DIRTY=true 가 나옵니다.
/// 그러면 "커밋 안 된 변경이 섞였다"는 경고가 늘 떠서 아무도 믿지 않게 되고, 업로드 전 검사에서 막을 수도 없습니다.
/// 가장 먼저 도는 가드(-1000)보다도 앞에서 찍습니다.
///
/// [찍기 전에 에셋을 저장합니다]
/// 인스펙터에서 바꾸고 저장하지 않은 에셋도 빌드에는 들어갑니다(스트리퍼가 빌드 중에 SaveAssets 를 부르면 그때 디스크에도 써집니다).
/// 저장 전에 git 을 보면 그 변경이 안 보여 GIT_DIRTY=false 인데 내용은 다른 빌드가 나옵니다. 먼저 저장해 git 에 드러나게 합니다.
/// </summary>
public class BuildStampGitSnapshot : IPreprocessBuildWithReport
{
    public int callbackOrder => -2000;

    private static bool hasSnapshot;
    private static string commit;
    private static bool dirty;

    public void OnPreprocessBuild(BuildReport _report)
    {
        AssetDatabase.SaveAssets();
        hasSnapshot = BuildStampWriter.TryGetGitInfo(out commit, out dirty);
    }

    /// <summary>찍어 둔 값을 한 번만 돌려줍니다. 다음 빌드가 이전 값을 쓰지 않도록 꺼낸 뒤 지웁니다.</summary>
    public static bool TryTake(out string _commit, out bool _dirty)
    {
        _commit = commit;
        _dirty = dirty;

        bool _had = hasSnapshot;
        hasSnapshot = false;
        return _had;
    }
}
