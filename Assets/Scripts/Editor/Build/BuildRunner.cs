using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 배포용 빌드를 메뉴 하나로 실행합니다.
///
/// [왜 필요한가]
/// 유니티의 Build 버튼은 압축 방식과 출력 경로를 <b>Build Profile</b>에서 읽습니다.
/// 그 프로필은 Library 아래에 있어 <b>저장소에 올라가지 않습니다.</b> 사람마다 값이 다르고,
/// 새로 클론한 사람은 기본값으로 시작합니다. 실제로 압축이 꺼진 채 itch 빌드가 나가
/// 242MB(정상 151MB)가 된 적이 있습니다. 파일이 낱개로 풀려 에셋을 꺼내기도 쉬워집니다.
///
/// 여기서는 압축과 경로를 코드가 정하므로 프로필 값이 무엇이든 결과가 같습니다.
/// 스토어·배포 전환은 PlatformBuildModeSwitcher가, 정합성 검사는 PlatformConsistencyGuard가
/// 그대로 담당합니다. 이 클래스는 "어떻게 굽는가"만 고정합니다.
///
/// [Development Build는 여기서 만들지 않습니다]
/// 배포용만 다룹니다. 프로파일링용 빌드가 필요하면 평소대로 Build Profile 창을 쓰십시오.
/// 그쪽은 어차피 배포하지 않으므로 압축이 무엇이든 상관없습니다.
/// </summary>
public static class BuildRunner
{
    private const string MENU_BUILD = "Tools/빌드/배포용 빌드 실행";

    /// <summary>
    /// 배포 빌드의 압축 방식입니다. LZ4HC는 <b>무손실</b>이라 텍스처·오디오 품질과 무관하고,
    /// 런타임 로딩은 청크 단위 해제라 비압축과 차이가 없습니다. 빌드 시간만 조금 더 걸립니다.
    /// </summary>
    private const BuildOptions RELEASE_OPTIONS = BuildOptions.CompressWithLz4HC;

    [MenuItem(MENU_BUILD, false, 63)]
    private static void BuildFromMenu()
    {
        BuildStore _store = PlatformBuildModeSwitcher.CurrentStore;
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        string _location = PlatformBuildModeSwitcher.ExpectedBuildLocation(_store, _release);

        if (true == string.IsNullOrEmpty(_location))
        {
            EditorUtility.DisplayDialog(
                "빌드 출력 폴더가 없습니다",
                "Tools > 빌드 > 빌드 출력 폴더 지정 으로 루트를 먼저 정하십시오.",
                "확인");
            return;
        }

        // 스토어를 잘못 고른 채 굽는 것이 가장 비싼 실수라, 굽기 전에 한 번 눈으로 확인시킨다.
        bool _ok = EditorUtility.DisplayDialog(
            "배포용 빌드",
            $"스토어 : {_store}\n" +
            $"배포   : {(BuildRelease.Full == _release ? "정식" : "데모")}\n" +
            $"압축   : LZ4HC\n" +
            $"출력   : {_location}\n\n" +
            "이 구성으로 빌드할까요?",
            "빌드", "취소");

        if (false == _ok) return;

        BuildReport _report = Run();

        if (null == _report || BuildResult.Succeeded != _report.summary.result) return;

        // 빌드가 끝난 폴더를 곧바로 검사하고, 통과하면 경로를 클립보드에 올린다.
        // 업로더의 폴더 선택 창에서 옆 폴더(STEAM_DEMO ↔ STOVE_DEMO)를 집는 사고가 실제로 있었다.
        // 붙여넣을 경로를 손에 쥐어 주는 것이 그 창을 열지 않게 하는 가장 확실한 방법이다.
        UploadPreflightCheck.RunAndShow(_store, _release, System.IO.Path.GetDirectoryName(_location));
    }

    /// <summary>
    /// 확인 창 없이 곧바로 빌드합니다. 메뉴와 자동화(배치 빌드)가 같은 경로를 쓰도록 분리해 둡니다.
    /// </summary>
    public static BuildReport Run()
    {
        BuildStore _store = PlatformBuildModeSwitcher.CurrentStore;
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        string _location = PlatformBuildModeSwitcher.ExpectedBuildLocation(_store, _release);

        if (true == string.IsNullOrEmpty(_location))
        {
            Debug.LogError("[BuildRunner] 빌드 출력 폴더가 지정되지 않아 빌드하지 않았습니다. " +
                           "Tools > 빌드 > 빌드 출력 폴더 지정 을 먼저 쓰십시오.");
            return null;
        }

        BuildPlayerOptions _options = new BuildPlayerOptions();

        _options.scenes = CollectEnabledScenes();
        _options.locationPathName = _location;
        _options.target = BuildTarget.StandaloneWindows64;
        _options.targetGroup = BuildTargetGroup.Standalone;

        // Development 플래그를 넣지 않는다. 넣으면 BuildOutputSanitizer가 "배포물이 아니다"라고 판단해
        // 정리를 건너뛰고, 빌드 가드(캐릭터 스탯·디버그 스위치·데모 제외)도 막지 않고 경고만 남긴다.
        _options.options = RELEASE_OPTIONS;

        string _outputDir = System.IO.Path.GetDirectoryName(_location);

        if (false == CleanOutputFolder(_outputDir, PlatformBuildModeSwitcher.BuildFolderName(_store, _release), out string _cleanError))
        {
            Debug.LogError($"[BuildRunner] 출력 폴더를 비우지 못해 빌드하지 않았습니다.\n  {_cleanError}");
            return null;
        }

        Debug.Log($"[BuildRunner] {_store} / {(BuildRelease.Full == _release ? "정식" : "데모")} 빌드를 시작합니다. " +
                  $"압축 LZ4HC, 씬 {_options.scenes.Length}개\n  출력: {_location}");

        BuildReport _report = BuildPipeline.BuildPlayer(_options);

        // 빌드가 실패·취소되면 스트리퍼의 후처리(원본 복구)가 불리지 않아, 잘린 에셋이 에디터 재시작 전까지 남습니다.
        // 그 상태로 커밋되지 않도록 결과와 상관없이 여기서 복구합니다. 백업이 없으면 아무 일도 하지 않습니다.
        DemoContentStripper.RestoreIfNeeded(false);
        AbilityBuildVariantStripper.RestoreIfNeeded(false);

        if (null == _report)
        {
            Debug.LogError("[BuildRunner] 빌드 리포트를 받지 못했습니다.");
            return null;
        }

        BuildSummary _summary = _report.summary;

        if (BuildResult.Succeeded == _summary.result)
        {
            Debug.Log($"[BuildRunner] 빌드 성공 ({_summary.totalTime}). " +
                      "정리 결과는 출력 폴더 상위의 _BuildSanitizer.<폴더>.txt 를 보십시오.");
        }
        else
        {
            Debug.LogError($"[BuildRunner] 빌드 {_summary.result}. 에러 {_summary.totalErrors}건.\n" +
                           "빌드가 중간에 멈추면 콘텐츠 DB가 스트립된 채로 남을 수 있습니다. git status 를 한 번 보십시오.");
        }

        return _report;
    }

    /// <summary>
    /// 빌드 전에 출력 폴더를 비웁니다. 유니티는 새 빌드가 만드는 파일만 덮어쓰므로, 이전 빌드에만 있던 파일
    /// (예: 정식판에서만 쓰던 파일, 손으로 넣은 파일)은 그대로 남아 업로드에 섞입니다.
    ///
    /// 엉뚱한 폴더를 지우지 않도록 세 가지를 확인합니다.
    ///   1. 폴더 이름이 스위처가 정한 &lt;스토어&gt;_&lt;배포&gt; 이름과 같은가
    ///   2. 프로젝트 폴더 안이 아닌가
    ///   3. 비어 있지 않다면, 이전 빌드 결과물(BUILD_STAMP.txt, 실행 파일, _Data 폴더)이 보이는가
    /// 하나라도 아니면 지우지 않고 빌드를 멈춥니다.
    /// </summary>
    private static bool CleanOutputFolder(string _dir, string _expectedFolderName, out string _error)
    {
        _error = string.Empty;

        if (true == string.IsNullOrEmpty(_dir) || false == System.IO.Directory.Exists(_dir)) return true;

        string _full = System.IO.Path.GetFullPath(_dir).TrimEnd('\\', '/');
        string _projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');

        if (false == string.Equals(System.IO.Path.GetFileName(_full), _expectedFolderName, System.StringComparison.OrdinalIgnoreCase))
        {
            _error = $"폴더 이름이 {_expectedFolderName} 가 아닙니다: {_full}";
            return false;
        }

        if (true == _full.StartsWith(_projectRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            _error = $"프로젝트 폴더 안이라 비우지 않습니다: {_full}";
            return false;
        }

        string[] _files = System.IO.Directory.GetFiles(_full);
        string[] _dirs = System.IO.Directory.GetDirectories(_full);

        if (0 == _files.Length && 0 == _dirs.Length) return true;

        string _product = PlayerSettings.productName;
        bool _looksLikeBuild = System.IO.File.Exists(System.IO.Path.Combine(_full, "BUILD_STAMP.txt"))
                            || System.IO.File.Exists(System.IO.Path.Combine(_full, _product + ".exe"))
                            || System.IO.Directory.Exists(System.IO.Path.Combine(_full, _product + "_Data"));

        if (false == _looksLikeBuild)
        {
            _error = $"이전 빌드 결과물로 보이지 않는 파일이 있어 비우지 않습니다. 직접 확인하십시오: {_full}";
            return false;
        }

        try
        {
            for (int i = 0; i < _files.Length; i++) System.IO.File.Delete(_files[i]);
            for (int i = 0; i < _dirs.Length; i++) System.IO.Directory.Delete(_dirs[i], true);
        }
        catch (System.Exception _e)
        {
            // 게임을 실행 중이라 exe 가 잠긴 경우가 대부분이다.
            _error = $"지우는 중 실패했습니다(실행 중인 게임이 있으면 끄십시오): {_e.Message}";
            return false;
        }

        Debug.Log($"[BuildRunner] 출력 폴더를 비웠습니다: {_full} (파일 {_files.Length}개, 폴더 {_dirs.Length}개)");
        return true;
    }

    private static string[] CollectEnabledScenes()
    {
        List<string> _scenes = new List<string>();

        EditorBuildSettingsScene[] _all = EditorBuildSettings.scenes;

        for (int i = 0; i < _all.Length; i++)
        {
            if (false == _all[i].enabled) continue;

            _scenes.Add(_all[i].path);
        }

        return _scenes.ToArray();
    }
}
