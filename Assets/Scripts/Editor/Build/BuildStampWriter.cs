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

        TryGetGitInfo(out string _commit, out bool _dirty);

        Dictionary<string, string> _d = new Dictionary<string, string>();

        _d["STORE"] = _store.ToString();
        _d["RELEASE"] = _release.ToString();
        _d["FOLDER"] = PlatformBuildModeSwitcher.BuildFolderName(_store, _release);
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

        if (true == TryRunGit(_projectRoot, "status --porcelain", out string _status))
        {
            _dirty = false == string.IsNullOrWhiteSpace(_status);
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
