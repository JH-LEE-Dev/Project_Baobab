using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 업로드 직전에 빌드 폴더가 <b>정말 그 스토어의 배포 빌드인지</b> 검사합니다.
///
/// [왜 필요한가]
/// 2026-09-25, 검증까지 끝난 STOVE 빌드를 두고 업로더의 폴더 선택 창에서 바로 옆의 STEAM_DEMO 를
/// 집어 올렸습니다. Steam 빌드는 시작하자마자 SteamAPI.RestartAppIfNecessary 를 불러 Steam
/// 클라이언트를 띄우므로, STOVE 검수는 "다운로드 클릭 시 Steam 이 활성화됨"으로 반려됐습니다.
///
/// 코드는 전부 맞았습니다. 사람이 폴더 하나를 잘못 골랐고, 그걸 잡아 주는 것이 하나도 없었습니다.
/// 지금까지의 안전장치(PlatformConsistencyGuard, BuildOutputSanitizer)는 <b>빌드가 끝나는
/// 순간까지</b>만 지켜 줍니다. 이 클래스는 그 뒤, 업로더에 넘기기 직전을 지킵니다.
///
/// [무엇을 보는가]
///   1. BUILD_STAMP.txt 의 STORE / RELEASE / VERSION 이 지금 스위처 상태와 같은가
///   2. Steam 이 아닌 빌드에 steam_api*.dll 이나 SteamAPI_RestartAppIfNecessary 심볼이 남아 있지 않은가
///      (Steam 빌드라면 반대로 있어야 합니다)
///   3. 심볼 폴더 · pdb · 세이브 파일 · steam_appid.txt 가 없는가
///   4. 압축(LZ4HC)으로 포장됐는가 - data.unity3d 가 있어야 합니다. 없으면 무압축으로 나간 것입니다
///   5. 스탬프의 커밋이 지금 HEAD 와 같은가 (뒤에 desc·문서 커밋만 있으면 통과, 그 밖의 변경이 있으면 오류)
///   6. 커밋 안 된 변경이 섞인 빌드(GIT_DIRTY)나 벤치마크 하네스가 든 빌드가 아닌가 (오류)
///
/// [통과하면 경로를 클립보드에 복사합니다]
/// 업로더에는 경로를 직접 적는 칸이 있습니다. 폴더 선택 창을 열어 옆 폴더를 집는 대신
/// <b>붙여넣기</b>하도록 유도하는 것이 이 검사의 절반입니다.
///
/// 배포용 빌드 실행(BuildRunner) 메뉴는 성공 직후 이 검사를 자동으로 띄웁니다.
/// </summary>
public static class UploadPreflightCheck
{
    private const string MENU_CHECK = "Tools/빌드/업로드 전 검사";
    private const string MENU_CHECK_PICK = "Tools/빌드/업로드 전 검사 (폴더 선택...)";

    private const string TAG = "[UploadPreflight]";

    /// <summary>Steam 빌드의 GameAssembly.dll 에만 들어 있는 심볼입니다. DISABLESTEAMWORKS 가 켜지면 컴파일에서 빠집니다.</summary>
    private const string STEAM_SYMBOL = "SteamAPI_RestartAppIfNecessary";

    public sealed class Result
    {
        public string Root;
        public BuildStore ExpectedStore;
        public BuildRelease ExpectedRelease;
        public Dictionary<string, string> Stamp;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool Passed => 0 == Errors.Count;
    }

#region 메뉴

    [MenuItem(MENU_CHECK, false, 64)]
    private static void CheckCurrent()
    {
        BuildStore _store = PlatformBuildModeSwitcher.CurrentStore;
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        string _location = PlatformBuildModeSwitcher.ExpectedBuildLocation(_store, _release);

        if (true == string.IsNullOrEmpty(_location))
        {
            EditorUtility.DisplayDialog("업로드 전 검사", "빌드 출력 폴더가 정해지지 않았습니다.\nTools > 빌드 > 빌드 출력 폴더 지정 을 먼저 쓰십시오.", "확인");
            return;
        }

        RunAndShow(_store, _release, Path.GetDirectoryName(_location));
    }

    /// <summary>
    /// 아무 폴더나 골라 "이게 어느 스토어 빌드인가"를 물을 때 씁니다. 스탬프가 있으면 그것을,
    /// 없으면 steam_api64.dll 유무로 스토어를 추정해 검사합니다.
    /// </summary>
    [MenuItem(MENU_CHECK_PICK, false, 65)]
    private static void CheckPicked()
    {
        string _start = PlatformBuildModeSwitcher.BuildOutputRoot;
        string _root = EditorUtility.OpenFolderPanel("검사할 빌드 폴더 (LumberBoy.exe 가 있는 폴더)", string.IsNullOrEmpty(_start) ? "" : _start, "");

        if (true == string.IsNullOrEmpty(_root)) return;

        Dictionary<string, string> _stamp = BuildStampWriter.Read(_root);

        BuildStore _store;
        BuildRelease _release;

        if (null != _stamp && _stamp.TryGetValue("STORE", out string _s) && Enum.TryParse(_s, out _store)
                           && _stamp.TryGetValue("RELEASE", out string _r) && Enum.TryParse(_r, out _release))
        {
            // 스탬프가 말하는 대로 검사한다. 스탬프 자체가 거짓일 가능성은 GameAssembly 심볼 검사가 잡는다.
        }
        else
        {
            _store = HasSteamNativeDll(_root) ? BuildStore.Steam : BuildStore.Stove;
            _release = BuildRelease.Demo;
        }

        RunAndShow(_store, _release, _root);
    }

#endregion

    /// <summary>검사하고 결과 창을 띄웁니다. 통과하면 폴더 경로를 클립보드에 복사합니다.</summary>
    public static void RunAndShow(BuildStore _store, BuildRelease _release, string _root)
    {
        Result _r = Run(_store, _release, _root);

        string _folderName = Path.GetFileName(_root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string _releaseKo = (BuildRelease.Full == _release) ? "정식" : "데모";

        if (_r.Passed)
        {
            string _native = _root.Replace('/', '\\');

            EditorGUIUtility.systemCopyBuffer = _native;

            StringBuilder _sb = new StringBuilder();

            _sb.AppendLine($"✔  {_store} {_releaseKo} 빌드가 맞습니다.");
            _sb.AppendLine();
            _sb.AppendLine($"폴더 :  {_folderName}");
            _sb.AppendLine($"경로 :  {_native}");

            if (null != _r.Stamp)
            {
                _sb.AppendLine($"버전 :  {Get(_r.Stamp, "VERSION")}    커밋 :  {Get(_r.Stamp, "GIT")}{("true" == Get(_r.Stamp, "GIT_DIRTY") ? " (dirty)" : "")}");
                _sb.AppendLine($"빌드 :  {Get(_r.Stamp, "BUILT_AT")}");
            }

            if (0 != _r.Warnings.Count)
            {
                _sb.AppendLine();
                _sb.AppendLine("주의:");
                foreach (string _w in _r.Warnings) _sb.AppendLine("  · " + _w);
            }

            _sb.AppendLine();
            _sb.AppendLine("경로를 클립보드에 복사했습니다.");
            _sb.Append("업로더에서 폴더를 찾아 고르지 말고, 경로 칸에 그대로 붙여넣으십시오.");

            Debug.Log($"{TAG} 통과 — {_store} {_releaseKo} — {_native}");

            if (true == EditorUtility.DisplayDialog("업로드 전 검사 — 통과", _sb.ToString(), "폴더 열기", "닫기"))
            {
                EditorUtility.RevealInFinder(Path.Combine(_root, PlayerSettings.productName + ".exe"));
            }
        }
        else
        {
            StringBuilder _sb = new StringBuilder();

            _sb.AppendLine($"✖  이 폴더는 {_store} {_releaseKo} 배포 빌드로 올릴 수 없습니다.");
            _sb.AppendLine();
            _sb.AppendLine($"폴더 :  {_folderName}");
            _sb.AppendLine($"경로 :  {_root.Replace('/', '\\')}");
            _sb.AppendLine();

            foreach (string _e in _r.Errors) _sb.AppendLine("  ✖ " + _e);
            foreach (string _w in _r.Warnings) _sb.AppendLine("  · " + _w);

            _sb.AppendLine();
            _sb.Append("경로는 복사하지 않았습니다. 올리지 마십시오.");

            Debug.LogError($"{TAG} 실패 — {_store} {_releaseKo} — {_root}\n  " + string.Join("\n  ", _r.Errors));

            EditorUtility.DisplayDialog("업로드 전 검사 — 실패", _sb.ToString(), "확인");
        }
    }

#region 검사

    public static Result Run(BuildStore _expectedStore, BuildRelease _expectedRelease, string _root)
    {
        Result _r = new Result { Root = _root, ExpectedStore = _expectedStore, ExpectedRelease = _expectedRelease };

        if (true == string.IsNullOrEmpty(_root) || false == Directory.Exists(_root))
        {
            _r.Errors.Add($"폴더가 없습니다: {_root}");
            return _r;
        }

        string _exe = Path.Combine(_root, PlayerSettings.productName + ".exe");

        if (false == File.Exists(_exe))
        {
            _r.Errors.Add($"{PlayerSettings.productName}.exe 가 폴더 최상위에 없습니다. 한 단계 위나 아래 폴더를 고른 것 아닙니까?");
            return _r;
        }

        CheckStamp(_r);
        CheckSteamTraces(_r);
        CheckForbiddenFiles(_r);
        CheckCompression(_r);
        CheckPackedDevArtifacts(_r);

        return _r;
    }

    /// <summary>에디터에서 나온 산출물 중 배포물에 들어가면 안 되는 에셋 경로 조각입니다.</summary>
    private static readonly string[] PACKED_DENY_FRAGMENTS =
    {
        "Assets/Resources/PerformanceTestRun",   // Performance Testing 패키지가 빌드 PC 사양을 적어 넣는 JSON
        "Assets/_Recovery/",                     // 에디터 복구 씬
        "/[Test]",                               // 테스트 씬
    };

    /// <summary>
    /// 폴더 안의 파일만 보면 LZ4 로 포장된 data.unity3d 안쪽은 볼 수 없습니다. 대신 이 폴더를 만든
    /// 빌드 리포트(Library/LastBuild.buildreport)의 packedAssets 목록을 봅니다. 리포트가 다른 폴더의
    /// 것이면(마지막 빌드가 이 폴더가 아니면) 판단하지 않고 그 사실만 경고합니다.
    /// </summary>
    private static void CheckPackedDevArtifacts(Result _r)
    {
        UnityEditor.Build.Reporting.BuildReport _report;

        try { _report = UnityEditor.Build.Reporting.BuildReport.GetLatestReport(); }
        catch (Exception) { return; }

        if (null == _report) return;

        string _reportRoot = Path.GetDirectoryName(Path.GetFullPath(_report.summary.outputPath));
        string _thisRoot = Path.GetFullPath(_r.Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (false == string.Equals(_reportRoot, _thisRoot, StringComparison.OrdinalIgnoreCase))
        {
            _r.Warnings.Add("마지막 빌드 리포트가 이 폴더의 것이 아니라, 포장된 에셋 안쪽(개발용 산출물 유무)은 확인하지 못했습니다.");
            return;
        }

        HashSet<string> _hit = new HashSet<string>();

        foreach (UnityEditor.Build.Reporting.PackedAssets _pa in _report.packedAssets)
        {
            foreach (UnityEditor.Build.Reporting.PackedAssetInfo _c in _pa.contents)
            {
                string _p = _c.sourceAssetPath;

                if (true == string.IsNullOrEmpty(_p)) continue;

                for (int i = 0; i < PACKED_DENY_FRAGMENTS.Length; i++)
                {
                    if (0 <= _p.IndexOf(PACKED_DENY_FRAGMENTS[i], StringComparison.OrdinalIgnoreCase)) _hit.Add(_p);
                }
            }
        }

        foreach (string _p in _hit)
        {
            _r.Errors.Add($"개발용 산출물이 빌드 안에 포장돼 있습니다: {_p}");
        }
    }

    private static void CheckStamp(Result _r)
    {
        _r.Stamp = BuildStampWriter.Read(_r.Root);

        if (null == _r.Stamp)
        {
            // 스탬프가 생기기 전의 빌드일 수 있다. 그렇더라도 어느 스토어 빌드인지 모르는 채로 올리게 두지 않는다.
            _r.Errors.Add($"{BuildStampWriter.FILE_NAME} 이 없습니다. 스탬프가 도입된 뒤에 다시 빌드하십시오.");
            return;
        }

        string _store = Get(_r.Stamp, "STORE");
        string _release = Get(_r.Stamp, "RELEASE");
        string _version = Get(_r.Stamp, "VERSION");

        if (_store != _r.ExpectedStore.ToString())
        {
            _r.Errors.Add($"스토어가 다릅니다. 이 폴더는 [{_store}] 빌드인데 지금 올리려는 것은 [{_r.ExpectedStore}] 입니다.");
        }

        if (_release != _r.ExpectedRelease.ToString())
        {
            _r.Errors.Add($"배포 종류가 다릅니다. 이 폴더는 [{_release}] 인데 지금 올리려는 것은 [{_r.ExpectedRelease}] 입니다.");
        }

        if (_version != PlayerSettings.bundleVersion)
        {
            _r.Warnings.Add($"버전이 다릅니다. 빌드 {_version} / 프로젝트 {PlayerSettings.bundleVersion}. 오래된 빌드일 수 있습니다.");
        }

        if ("true" == Get(_r.Stamp, "DEVELOPMENT"))
        {
            _r.Errors.Add("Development Build 입니다. 심볼과 디버그 정보가 그대로 들어 있어 배포할 수 없습니다.");
        }

        // 벤치마크 하네스(-benchmark 인자, F9 측정, 화면 표시)는 측정용 빌드에만 들어가야 한다.
        if (0 <= Get(_r.Stamp, "DEFINES").IndexOf(BenchmarkBuildToggle.BENCHMARK_DEFINE, StringComparison.Ordinal))
        {
            _r.Errors.Add($"{BenchmarkBuildToggle.BENCHMARK_DEFINE} 가 켜진 채 빌드됐습니다. 측정용 빌드라 배포할 수 없습니다. " +
                          "Tools > 빌드 > \"벤치마크 하네스 빌드에 포함\"을 끄고 다시 빌드하십시오.");
        }

        if (BuildStampWriter.TryGetGitInfo(out string _head, out bool _dirtyNow))
        {
            string _built = Get(_r.Stamp, "GIT");

            if (_built != _head)
            {
                CheckCommitsSinceBuild(_r, _built, _head);
            }

            // 스탬프의 GIT_DIRTY 는 빌드 시작 시점 값이고, 스위처가 바꾸는 파일(디파인·Sentry·GA·steam_appid)은 세지 않는다.
            // 그런데도 true 면 커밋 안 된 코드나 에셋이 빌드에 섞였다는 뜻이다. 어떤 커밋으로도 재현되지 않는 빌드는 올리지 않는다.
            if ("true" == Get(_r.Stamp, "GIT_DIRTY"))
            {
                _r.Errors.Add("커밋되지 않은 변경이 섞인 상태에서 빌드됐습니다. 이 빌드는 어떤 커밋으로도 재현되지 않습니다. " +
                              "변경을 커밋하거나 되돌린 뒤 다시 빌드하십시오.");
            }
        }
        else
        {
            _r.Warnings.Add("git 정보를 읽지 못해 빌드된 커밋과 지금 HEAD 를 비교하지 못했습니다.");
        }
    }

    /// <summary>
    /// 빌드 뒤에 들어온 커밋이 빌드 결과와 무관한 파일(Steam desc VDF, 문서)만 바꿨다면 통과시킵니다.
    /// 빌드 → 검증 → desc 커밋 → 업로드 순서에서는 HEAD 가 항상 한 칸 앞서기 때문입니다.
    /// 그 밖의 파일이 바뀌었으면 그 변경이 빌드에 없으므로 막습니다.
    /// </summary>
    private static void CheckCommitsSinceBuild(Result _r, string _built, string _head)
    {
        if (false == BuildStampWriter.TryGetChangedFilesBetween(_built, _head, out List<string> _changed))
        {
            _r.Errors.Add($"빌드된 커밋({_built})을 지금 저장소에서 찾지 못했습니다. 지금 HEAD({_head})로 다시 빌드하십시오.");
            return;
        }

        List<string> _relevant = new List<string>();

        for (int i = 0; i < _changed.Count; i++)
        {
            string _path = _changed[i];

            if (true == _path.StartsWith("BuildScripts/", StringComparison.OrdinalIgnoreCase)) continue;
            if (true == _path.StartsWith("Docs/", StringComparison.OrdinalIgnoreCase)) continue;
            if (true == _path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;

            _relevant.Add(_path);
        }

        if (0 == _relevant.Count)
        {
            _r.Warnings.Add($"빌드된 커밋({_built}) 뒤에 desc·문서 커밋만 있습니다. 빌드 내용은 지금 HEAD({_head})와 같습니다.");
            return;
        }

        string _sample = string.Join(", ", _relevant.GetRange(0, Math.Min(5, _relevant.Count)));
        _r.Errors.Add($"빌드된 커밋({_built}) 뒤에 빌드에 영향을 주는 변경 {_relevant.Count}건이 들어왔습니다(예: {_sample}). " +
                      $"이 빌드에는 그 변경이 없습니다. 지금 HEAD({_head})로 다시 빌드하십시오.");
    }

    /// <summary>
    /// 폴더 이름과 스탬프는 사람이 바꿀 수 있지만, GameAssembly.dll 안의 심볼은 못 바꿉니다.
    /// 스토어 판정의 최종 근거는 이쪽입니다.
    /// </summary>
    private static void CheckSteamTraces(Result _r)
    {
        bool _hasDll = HasSteamNativeDll(_r.Root);
        bool _hasSymbol = GameAssemblyContains(_r.Root, STEAM_SYMBOL);
        bool _hasTxt = File.Exists(Path.Combine(_r.Root, "LumberBoy_Data", "Plugins", "Steamworks.NET.txt"));

        if (BuildStore.Steam == _r.ExpectedStore)
        {
            if (false == _hasDll) _r.Errors.Add("Steam 빌드인데 steam_api64.dll 이 없습니다. Steam API 가 통째로 죽습니다.");
            if (false == _hasSymbol) _r.Errors.Add("Steam 빌드인데 GameAssembly.dll 에 Steam API 가 없습니다. DISABLESTEAMWORKS 가 켜진 채 빌드됐습니다.");
            return;
        }

        if (_hasSymbol)
        {
            _r.Errors.Add($"GameAssembly.dll 에 {STEAM_SYMBOL} 가 들어 있습니다. 이것은 Steam 빌드입니다 — 실행하면 Steam 클라이언트가 켜집니다.");
        }

        if (_hasDll) _r.Errors.Add("steam_api64.dll 이 들어 있습니다. Steam 빌드이거나 정리 스크립트가 돌지 않았습니다.");
        if (_hasTxt) _r.Errors.Add("Plugins/Steamworks.NET.txt 가 들어 있습니다. Steam 빌드의 흔적입니다.");
    }

    private static void CheckForbiddenFiles(Result _r)
    {
        string[] _dirFragments = { "_BackUpThisFolder_ButDontShipItWithYourGame", "_BurstDebugInformation_DoNotShip" };
        string[] _filePatterns = { "*.pdb", "*.log", "SaveData*", "*.cloud-deleted", "steam_appid.txt", "Settings.json", "KeyBindings.json" };

        foreach (string _dir in Directory.GetDirectories(_r.Root, "*", SearchOption.AllDirectories))
        {
            string _name = Path.GetFileName(_dir);

            foreach (string _frag in _dirFragments)
            {
                if (0 <= _name.IndexOf(_frag, StringComparison.OrdinalIgnoreCase))
                {
                    _r.Errors.Add($"심볼 폴더가 남아 있습니다: {Relative(_r.Root, _dir)}");
                }
            }
        }

        foreach (string _pattern in _filePatterns)
        {
            foreach (string _file in Directory.GetFiles(_r.Root, _pattern, SearchOption.AllDirectories))
            {
                _r.Errors.Add($"배포에 실리면 안 되는 파일: {Relative(_r.Root, _file)}");
            }
        }
    }

    /// <summary>
    /// LZ4/LZ4HC 로 포장하면 에셋이 data.unity3d 하나로 묶이고, 무압축이면 sharedassets*.assets 로
    /// 낱개로 풀립니다. 무압축 빌드가 나간 적이 있어(242MB, 정상 151MB) 여기서 잡습니다.
    /// </summary>
    private static void CheckCompression(Result _r)
    {
        string _data = Path.Combine(_r.Root, PlayerSettings.productName + "_Data");

        if (false == Directory.Exists(_data)) return;

        bool _packed = File.Exists(Path.Combine(_data, "data.unity3d"));
        bool _loose = 0 != Directory.GetFiles(_data, "sharedassets*.assets", SearchOption.TopDirectoryOnly).Length;

        if (false == _packed && _loose)
        {
            _r.Warnings.Add("무압축 빌드입니다(data.unity3d 없음, sharedassets 낱개). Tools > 빌드 > 배포용 빌드 실행 으로 다시 빌드하십시오.");
        }
    }

#endregion

#region 도우미

    private static bool HasSteamNativeDll(string _root)
    {
        return 0 != Directory.GetFiles(_root, "steam_api*.dll", SearchOption.AllDirectories).Length;
    }

    /// <summary>GameAssembly.dll 을 통째로 읽어 ASCII 문자열을 찾습니다. 57MB 정도라 에디터에서 문제없습니다.</summary>
    private static bool GameAssemblyContains(string _root, string _ascii)
    {
        string _path = Path.Combine(_root, "GameAssembly.dll");

        if (false == File.Exists(_path)) return false;

        byte[] _needle = Encoding.ASCII.GetBytes(_ascii);
        byte[] _hay;

        try { _hay = File.ReadAllBytes(_path); }
        catch (Exception) { return false; }

        int _last = _hay.Length - _needle.Length;

        for (int i = 0; i <= _last; i++)
        {
            if (_hay[i] != _needle[0]) continue;

            int j = 1;
            while (j < _needle.Length && _hay[i + j] == _needle[j]) j++;

            if (j == _needle.Length) return true;
        }

        return false;
    }

    private static string Get(Dictionary<string, string> _d, string _key)
    {
        return (null != _d && _d.TryGetValue(_key, out string _v)) ? _v : "(없음)";
    }

    private static string Relative(string _root, string _path)
    {
        return _path.StartsWith(_root, StringComparison.OrdinalIgnoreCase)
            ? _path.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : _path;
    }

#endregion
}
