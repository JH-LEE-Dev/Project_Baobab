using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Performance Testing 패키지가 빌드마다 Resources 에 끼워 넣는 실행 정보 JSON 을 포장 전에 걷어냅니다.
///
/// [무슨 일이 벌어지고 있었나]
/// com.unity.test-framework.performance 의 TestRunBuilder(callbackOrder 0)는 <b>모든 플레이어 빌드</b>의
/// 전처리에서 Assets/Resources/PerformanceTestRunInfo.json 과 PerformanceTestRunSettings.json 을 쓰고,
/// 후처리에서 지웁니다. 그 사이에 빌드가 Resources 를 포장하므로 두 파일이 <b>출시 빌드 안에 그대로
/// 실립니다.</b> 2026-09-30 STOVE 데모 빌드 리포트의 packedAssets 에서 실제로 확인했습니다.
///
/// 내용은 빌드한 PC 의 정체입니다 - DeviceName(컴퓨터 이름), DeviceModel, ProcessorType,
/// GraphicsDeviceName, SystemMemorySizeMB, ScriptingBackend, GraphicsApi, 에디터 버전, 빌드 시각.
/// 게임에는 쓰이지 않고, 에셋 도구로 열면 누구나 읽습니다. 배포물에 들어갈 이유가 없습니다.
///
/// [왜 패키지를 지우지 않는가]
/// 우리 코드는 이 패키지를 참조하지 않지만 com.unity.collections 가 의존하고 있어 manifest 에서 빼도
/// 간접 의존성으로 다시 설치됩니다. 그래서 훅을 막을 수 없고, 훅이 쓴 파일을 우리가 뒤따라 지웁니다.
///
/// [순서]
/// 패키지 훅이 0 이므로 1 로 둡니다. 같은 값(0)은 실행 순서가 보장되지 않아 DemoContentStripper 와 같은
/// 0 을 쓰면 안 됩니다. 파일은 AssetDatabase 를 거치지 않고 직접 쓰였으므로, 지운 뒤 Refresh 로 에셋 DB 가
/// 포장 목록을 다시 보게 합니다. 패키지의 후처리 정리는 파일이 없어도 그냥 넘어갑니다.
///
/// [Development Build 도 지웁니다]
/// 성능 테스트를 실제로 돌릴 때(Test Runner 로 플레이어 빌드)는 이 파일이 필요하지만, 그 경로는
/// IPrebuildSetup 을 거쳐 PlayerPrefs 에 따로 저장하므로 영향이 없습니다. 일반 빌드에는 늘 불필요합니다.
/// </summary>
public class PerformanceTestArtifactStripper : IPreprocessBuildWithReport
{
    public int callbackOrder => 1;

    private const string TAG = "[PerfTestStripper]";

    /// <summary>패키지의 Utils.TestRunInfo / Utils.RunSettings 와 같은 이름입니다. 패키지가 바꾸면 여기도 맞춰야 합니다.</summary>
    public static readonly string[] ARTIFACT_FILES =
    {
        "Assets/Resources/PerformanceTestRunInfo.json",
        "Assets/Resources/PerformanceTestRunSettings.json",
    };

    public void OnPreprocessBuild(BuildReport _report)
    {
        int _removed = 0;

        for (int i = 0; i < ARTIFACT_FILES.Length; i++)
        {
            string _path = ARTIFACT_FILES[i];

            if (false == File.Exists(_path)) continue;

            // AssetDatabase.DeleteAsset 은 아직 임포트되지 않은 파일에는 false 를 돌려줄 수 있어 둘 다 시도한다.
            if (false == AssetDatabase.DeleteAsset(_path))
            {
                File.Delete(_path);
                if (File.Exists(_path + ".meta")) File.Delete(_path + ".meta");
            }

            _removed++;
        }

        if (0 == _removed)
        {
            Debug.Log($"{TAG} 성능 테스트 산출물이 없습니다. (패키지가 더 이상 만들지 않거나 이미 정리됨)");
            return;
        }

        AssetDatabase.Refresh();

        Debug.Log($"{TAG} 성능 테스트 패키지가 Resources 에 넣은 파일 {_removed}건을 포장 전에 걷어냈습니다. " +
                  "빌드 PC 의 이름·사양이 배포물에 실리는 것을 막습니다.");
    }
}
