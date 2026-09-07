using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 배포하면 안 되는 산출물을 빌드 출력 폴더에서 직접 지웁니다.
///
/// [왜 필요한가]
/// 같은 목록이 BuildScripts/depot_*.vdf 의 FileExclusion 에도 있지만, 그건 <b>SteamPipe로 올릴 때만</b>
/// 적용됩니다. STOVE는 depot이 아니라 자체 업로더를 쓰므로 그 규칙이 하나도 걸리지 않습니다.
/// 즉 지금까지의 안전장치는 업로드 경로 하나에만 붙어 있었고, 플랫폼이 늘어나는 순간 구멍이 납니다.
///
/// 여기서 지우면 어느 경로로 올리든 애초에 파일이 없습니다. depot 쪽 규칙은 이중 방어로 남겨둡니다.
///
/// [무엇이 위험한가]
/// ProjectSettings의 additionalIl2CppArgs에 --emit-source-mapping 이 켜져 있어,
/// *_BackUpThisFolder_ButDontShipItWithYourGame 폴더에는 IL2CPP가 뱉은 C++ 소스가 원본 구조 그대로
/// 들어갑니다. 이걸 실어 보내면 IL2CPP를 강제해(ReleaseScriptingBackendGuard) 소스 유출을 막아둔
/// 노력이 통째로 무의미해집니다.
///
/// [실행 순서 - 중요]
/// Sentry의 심볼 업로드(Sentry.Unity.Editor.Native.BuildPostProcess)가 <b>바로 그 백업 폴더를 읽어
/// 갑니다.</b> 우리가 먼저 지우면 심볼이 올라가지 않아, 앞으로 들어오는 크래시 리포트가 줄 번호를
/// 잃습니다. 그것도 조용히 일어납니다.
///
/// 그래서 callbackOrder를 크게 잡아 어떤 SDK보다 뒤에 돌게 합니다. Sentry 쪽 값은 DLL로만 배포되어
/// 확정할 수 없었으므로, <b>첫 빌드 때 로그에서 Sentry 업로드 줄이 아래 정리 줄보다 위에 있는지 한 번
/// 눈으로 확인하십시오.</b> (아래 SUMMARY_TAG 로 검색하면 됩니다)
///
/// [개발 빌드는 건드리지 않습니다]
/// Development Build는 프로파일링·디버깅이 목적이라 심볼이 있어야 합니다. 어차피 배포할 수 없는
/// 물건이므로 그대로 두고 로그만 남깁니다. ReleaseScriptingBackendGuard와 같은 관용입니다.
/// </summary>
public class BuildOutputSanitizer : IPostprocessBuildWithReport
{
    /// <summary>
    /// 의도적으로 큰 값입니다. 이 정리는 반드시 <b>모든</b> 빌드 후처리(특히 Sentry 심볼 업로드)가
    /// 끝난 뒤에 돌아야 합니다. 위 주석의 [실행 순서] 참고.
    /// </summary>
    public int callbackOrder => 10000;

    /// <summary>로그에서 이 정리 결과를 찾을 때 쓰는 표식입니다. 순서 확인에도 씁니다.</summary>
    private const string SUMMARY_TAG = "[BuildSanitizer]";

    /// <summary>통째로 지우는 폴더입니다. 이름에 이 조각이 들어가면 대상입니다.</summary>
    private static readonly string[] DIRECTORY_FRAGMENTS =
    {
        "_BackUpThisFolder_ButDontShipItWithYourGame",
        "_BurstDebugInformation_DoNotShip",
    };

    /// <summary>지우는 파일 패턴입니다.</summary>
    private static readonly string[] FILE_PATTERNS =
    {
        "*.pdb",
        "*.log",
        "Thumbs.db",
        "desktop.ini",
    };

    public void OnPostprocessBuild(BuildReport _report)
    {
        if (null == _report) return;
        if (false == IsStandalone(_report.summary.platform)) return;

        // 실패한 빌드의 출력물은 중간 상태다. 원인을 보려면 그대로 남겨두는 편이 낫다.
        if (BuildResult.Succeeded != _report.summary.result)
        {
            return;
        }

        if (0 != (_report.summary.options & BuildOptions.Development))
        {
            Debug.Log($"{SUMMARY_TAG} Development Build라 정리를 건너뜁니다. 심볼과 디버그 정보를 그대로 둡니다.\n" +
                      "배포용 빌드(Development Build 해제)에서만 정리합니다.");
            return;
        }

        string _root = ResolveOutputRoot(_report);

        if (false == IsSafeToClean(_root))
        {
            // 여기서 빌드를 실패시키지는 않는다. 빌드 자체는 멀쩡하고, 정리는 업로드 전에 손으로도 할 수 있다.
            Debug.LogError($"{SUMMARY_TAG} 출력 폴더를 신뢰할 수 없어 정리를 건너뜁니다: {_root}\n" +
                           "업로드 전에 심볼 폴더가 남아있지 않은지 반드시 직접 확인하십시오.");
            return;
        }

        List<string> _removed = new List<string>();
        long _freed = 0;

        try
        {
            _freed += RemoveDirectories(_root, _removed);
            _freed += RemoveFiles(_root, _removed);
        }
        catch (Exception _e)
        {
            Debug.LogError($"{SUMMARY_TAG} 정리 중 오류가 발생했습니다: {_e.Message}\n" +
                           "빌드 자체는 정상입니다. 업로드 전에 남은 항목이 없는지 직접 확인하십시오.");
        }

        Report(_root, _removed, _freed);
    }

#region 정리

    private static long RemoveDirectories(string _root, List<string> _removed)
    {
        long _freed = 0;

        // 최상위만 보지 않고 전부 훑는다. Burst 폴더는 빌드 설정에 따라 Data 아래로 들어가기도 한다.
        string[] _dirs = Directory.GetDirectories(_root, "*", SearchOption.AllDirectories);

        for (int i = 0; i < _dirs.Length; i++)
        {
            string _dir = _dirs[i];

            // 상위 폴더를 이미 지웠으면 하위는 사라져 있다.
            if (false == Directory.Exists(_dir)) continue;
            if (false == MatchesAnyFragment(Path.GetFileName(_dir))) continue;

            long _size = GetDirectorySize(_dir);

            Directory.Delete(_dir, true);

            _removed.Add(ToRelative(_root, _dir) + Path.DirectorySeparatorChar);
            _freed += _size;
        }

        return _freed;
    }

    private static long RemoveFiles(string _root, List<string> _removed)
    {
        long _freed = 0;

        for (int p = 0; p < FILE_PATTERNS.Length; p++)
        {
            string[] _files = Directory.GetFiles(_root, FILE_PATTERNS[p], SearchOption.AllDirectories);

            for (int i = 0; i < _files.Length; i++)
            {
                string _file = _files[i];

                if (false == File.Exists(_file)) continue;

                long _size = new FileInfo(_file).Length;

                File.Delete(_file);

                _removed.Add(ToRelative(_root, _file));
                _freed += _size;
            }
        }

        return _freed;
    }

    private static bool MatchesAnyFragment(string _directoryName)
    {
        for (int i = 0; i < DIRECTORY_FRAGMENTS.Length; i++)
        {
            if (0 <= _directoryName.IndexOf(DIRECTORY_FRAGMENTS[i], StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

#endregion

#region 안전장치

    /// <summary>
    /// 빌드 출력의 루트 폴더입니다. summary.outputPath는 실행 파일 경로이므로 그 상위를 씁니다.
    /// (플랫폼에 따라 폴더가 넘어오기도 해서 둘 다 처리합니다)
    /// </summary>
    private static string ResolveOutputRoot(BuildReport _report)
    {
        string _output = _report.summary.outputPath;

        if (true == string.IsNullOrEmpty(_output)) return null;

        if (true == Directory.Exists(_output)) return Path.GetFullPath(_output);

        string _parent = Path.GetDirectoryName(Path.GetFullPath(_output));

        return _parent;
    }

    /// <summary>
    /// 재귀 삭제를 하는 코드이므로, 대상이 정말 빌드 출력 폴더인지 먼저 확인합니다.
    /// 드라이브 루트나 프로젝트 폴더 안을 가리키면 무조건 거부합니다.
    /// </summary>
    private static bool IsSafeToClean(string _root)
    {
        if (true == string.IsNullOrEmpty(_root)) return false;
        if (false == Directory.Exists(_root)) return false;

        DirectoryInfo _info = new DirectoryInfo(_root);

        // 드라이브 루트(C:\ 등)는 상위가 없다.
        if (null == _info.Parent) return false;

        // 프로젝트 폴더 안으로 빌드하면 Assets/Library까지 훑게 된다. 실수로도 그러지 않도록 막는다.
        string _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        if (true == _root.StartsWith(_projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"{SUMMARY_TAG} 빌드 출력이 프로젝트 폴더 안에 있습니다. 출력 경로를 프로젝트 밖으로 옮기십시오.\n" +
                           $"  프로젝트: {_projectRoot}\n  출력: {_root}");
            return false;
        }

        return true;
    }

#endregion

#region 보고

    private static void Report(string _root, List<string> _removed, long _freed)
    {
        if (0 == _removed.Count)
        {
            Debug.Log($"{SUMMARY_TAG} 지울 항목이 없었습니다. ({_root})\n" +
                      "이미 깨끗하거나, 이 빌드 구성이 심볼을 만들지 않았습니다.");
            return;
        }

        System.Text.StringBuilder _sb = new System.Text.StringBuilder();

        _sb.AppendLine($"{SUMMARY_TAG} 배포 금지 산출물 {_removed.Count}건을 정리했습니다. ({EditorUtility.FormatBytes(_freed)} 확보)");
        _sb.AppendLine($"  출력: {_root}");

        for (int i = 0; i < _removed.Count; i++)
        {
            _sb.AppendLine("  - " + _removed[i]);
        }

        _sb.AppendLine();
        _sb.AppendLine("이 줄이 Sentry 심볼 업로드 로그보다 **아래**에 있는지 확인하십시오.");
        _sb.Append("위에 있다면 심볼이 올라가기 전에 지워진 것이므로 callbackOrder를 더 키워야 합니다.");

        Debug.Log(_sb.ToString());
    }

    private static string ToRelative(string _root, string _path)
    {
        if (true == _path.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            return _path.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return _path;
    }

    private static long GetDirectorySize(string _dir)
    {
        long _total = 0;

        try
        {
            string[] _files = Directory.GetFiles(_dir, "*", SearchOption.AllDirectories);

            for (int i = 0; i < _files.Length; i++)
            {
                _total += new FileInfo(_files[i]).Length;
            }
        }
        catch (Exception)
        {
            // 크기는 보고용 정보일 뿐이다. 못 재도 삭제는 진행한다.
        }

        return _total;
    }

#endregion

    private static bool IsStandalone(BuildTarget _target)
    {
        return BuildTarget.StandaloneWindows64 == _target
            || BuildTarget.StandaloneWindows == _target
            || BuildTarget.StandaloneOSX == _target
            || BuildTarget.StandaloneLinux64 == _target;
    }
}
