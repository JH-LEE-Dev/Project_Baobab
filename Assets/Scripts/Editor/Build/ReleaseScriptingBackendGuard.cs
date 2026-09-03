using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 배포용 스탠드얼론 빌드가 Mono로 나가는 것을 막습니다.
///
/// [왜 필요한가]
/// Mono 빌드는 Data/Managed/Assembly-CSharp.dll 을 그대로 싣습니다. 이 파일은 완전한 IL이라
/// 디컴파일러에 넣으면 원본에 가까운 C#이 그대로 나옵니다. 게임 실행에 반드시 필요한 파일이라
/// Steam depot 스크립트의 FileExclusion으로도 뺄 수 없습니다. 즉 Mono로 배포하는 순간
/// 소스 코드가 함께 배포됩니다.
///
/// IL2CPP는 메서드 본문을 네이티브 코드로 바꾸므로 이 경로가 막힙니다. 이 프로젝트의
/// 배포 설정 상당수가 IL2CPP를 전제로 맞춰져 있기도 합니다.
///   - BuildScripts/depot_*.vdf 의 "*_BackUpThisFolder_ButDontShipItWithYourGame*" 제외 규칙
///     (Mono는 이 폴더를 만들지 않아 규칙이 아무것도 거르지 않습니다)
///   - SentryOptions 의 Il2CppLineNumberSupportEnabled 와 --emit-source-mapping
///   - managedStrippingLevel / stripEngineCode 조합
///
/// [왜 사람 기억에 맡기지 않는가]
/// Mono는 빌드가 훨씬 빨라서 작업 중 임시로 바꾸는 일이 흔하고, ProjectSettings.asset은
/// 팀원 모두가 커밋하는 파일이라 무관한 커밋에 한 줄이 딸려 들어가기 쉽습니다.
/// 실제로 이 프로젝트에서도 UI 수정 커밋 하나에 백엔드 한 줄만 바뀌어 되돌아간 적이 있습니다.
/// 그때 눈치채지 못하면 "여기선 잘 됐는데 배포 빌드만 Mono"가 됩니다.
///
/// [개발 중에는 막지 않는다]
/// Development Build가 켜져 있으면 경고만 남기고 통과시킵니다. 반복 작업 중에는 Mono의
/// 빠른 빌드가 실제로 도움이 되고, Development Build가 켜진 물건은 어차피 배포할 수 없기
/// 때문입니다. 배포 후보(Development Build 해제)일 때만 중단합니다.
/// </summary>
public class ReleaseScriptingBackendGuard : IPreprocessBuildWithReport
{
    /// <summary>
    /// DemoContentStripper(0)보다 반드시 먼저 돌아야 합니다. 스트리퍼가 DB 에셋에서 미공개
    /// 콘텐츠를 들어낸 뒤에 여기서 막으면, 빌드는 멈췄는데 에셋은 수정된 채로 남습니다.
    /// </summary>
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport _report)
    {
        if (null == _report) return;
        if (false == IsStandalone(_report.summary.platform)) return;

        ScriptingImplementation _backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);

        if (ScriptingImplementation.IL2CPP == _backend) return;

        // 리포트에 담긴 실제 빌드 옵션을 본다. EditorUserBuildSettings.development는 스크립트로
        // 빌드할 때(BuildPipeline.BuildPlayer에 옵션을 직접 넘기는 경우) 실제 값과 어긋난다.
        bool _isDevelopmentBuild = 0 != (_report.summary.options & BuildOptions.Development);

        if (true == _isDevelopmentBuild)
        {
            Debug.LogWarning(
                $"[BackendGuard] 스크립팅 백엔드가 {_backend}입니다. Development Build라 그대로 진행합니다.\n" +
                "배포용으로 낼 때는 반드시 IL2CPP로 되돌리십시오. Mono 빌드에는 Assembly-CSharp.dll(소스 코드)이 실립니다.");
            return;
        }

        throw new BuildFailedException(
            $"[BackendGuard] 배포용 빌드를 중단했습니다. 스크립팅 백엔드가 {_backend}입니다.\n" +
            "\n" +
            "Mono로 빌드하면 Data/Managed/Assembly-CSharp.dll 이 함께 배포됩니다.\n" +
            "이 파일은 완전한 IL이라 디컴파일하면 원본에 가까운 C#이 그대로 나오고,\n" +
            "게임 실행에 필요한 파일이라 depot 스크립트로도 제외할 수 없습니다.\n" +
            "\n" +
            "해결: Project Settings > Player > Other Settings > Scripting Backend 를 IL2CPP로 바꾸십시오.\n" +
            "\n" +
            "지금 당장 개발용으로 Mono 빌드가 필요하다면 Development Build를 켜면 통과합니다.");
    }

    private static bool IsStandalone(BuildTarget _target)
    {
        return BuildTarget.StandaloneWindows64 == _target
            || BuildTarget.StandaloneWindows == _target
            || BuildTarget.StandaloneOSX == _target
            || BuildTarget.StandaloneLinux64 == _target;
    }
}
