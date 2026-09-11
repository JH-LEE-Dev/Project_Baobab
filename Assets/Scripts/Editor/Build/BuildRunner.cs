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

        Run();
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

        // Development 플래그를 넣지 않는다. 넣으면 DemoContentStripper와 BuildOutputSanitizer가
        // 둘 다 "배포물이 아니다"라고 판단해 정리를 건너뛴다.
        _options.options = RELEASE_OPTIONS;

        Debug.Log($"[BuildRunner] {_store} / {(BuildRelease.Full == _release ? "정식" : "데모")} 빌드를 시작합니다. " +
                  $"압축 LZ4HC, 씬 {_options.scenes.Length}개\n  출력: {_location}");

        BuildReport _report = BuildPipeline.BuildPlayer(_options);

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
