using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 벤치마크 하네스(BenchmarkHarness)를 빌드에 넣을지 정하는 스위치입니다.
///
/// [무엇을 하는가]
/// BAOBAB_BENCHMARK 디파인 하나를 넣고 뺍니다. 하네스 쪽 파일들이 그 디파인으로 통째로
/// 감싸여 있어서, 꺼져 있으면 클래스 자체가 컴파일되지 않습니다. 즉 "동작만 안 하는" 것이
/// 아니라 빌드 결과물에 코드가 존재하지 않습니다.
///
/// [왜 PlatformBuildModeSwitcher에 넣지 않았는가]
/// 그쪽은 "우리가 다루는 넷만 넣고 뺀다"는 규칙을 주석에 명시해 두고 스토어·배포 디파인을
/// 한 덩어리로 다룹니다. 성격이 다른 디파인을 그 안에 섞으면 그 규칙이 깨지고, 스토어를
/// 전환할 때 측정 설정이 함께 따라 움직이는 사고가 생깁니다.
/// 여기서 따로 다루면 스토어/배포 전환에 영향을 주지도, 받지도 않습니다.
/// (PlatformBuildModeSwitcher.Apply는 자기가 관리하지 않는 디파인을 그대로 보존합니다)
///
/// [에디터에는 영향이 없습니다]
/// 하네스는 UNITY_EDITOR에서도 컴파일되므로, 이 스위치가 꺼져 있어도 에디터에서는 F9로
/// 항상 측정할 수 있습니다. 이 스위치는 순수하게 "빌드에 넣을지"만 정합니다.
///
/// [스토어 빌드에서는 반드시 꺼야 합니다]
/// 켜진 채로 빌드하면 OnPreprocessBuild가 빌드 로그에 경고를 남깁니다. 빌드를 실패시키지는
/// 않습니다 - 측정용 빌드를 만드는 것 자체가 정당한 작업이고, 어느 빌드가 스토어로 가는지는
/// 여기서 알 수 없기 때문입니다. 판단은 사람이 하고, 이 클래스는 사실만 남깁니다.
/// </summary>
public class BenchmarkBuildToggle : IPreprocessBuildWithReport
{
    /// <summary>
    /// 하네스를 빌드에 포함시키는 디파인입니다.
    /// 이 문자열은 BenchmarkHarness.cs와 FrameTimeRecorder.cs의 #if 조건과 반드시 같아야 합니다.
    /// (한쪽만 바꾸면 스위치는 켜지는데 코드는 안 들어가는, 알아채기 어려운 상태가 됩니다)
    /// </summary>
    public const string BENCHMARK_DEFINE = "BAOBAB_BENCHMARK";

    private const string MENU_TOGGLE = "Tools/빌드/벤치마크 하네스 빌드에 포함";

    /// <summary>
    /// PlatformConsistencyGuard(-900)보다 뒤에 돌립니다. 그쪽은 빌드를 실패시킬 수 있는
    /// 정합성 검사라 먼저 통과해야 하고, 이 경고는 빌드가 실제로 진행될 때만 의미가 있습니다.
    /// </summary>
    public int callbackOrder => -800;

    /// <summary>현재 활성 빌드 타깃에 디파인이 걸려 있는지 여부입니다.</summary>
    public static bool IsEnabled => HasDefine(BENCHMARK_DEFINE);

    [MenuItem(MENU_TOGGLE, false, 81)]
    private static void Toggle()
    {
        bool _next = false == IsEnabled;

        SetDefine(BENCHMARK_DEFINE, _next);

        if (true == _next)
        {
            Debug.LogWarning(
                $"[Benchmark] 하네스를 빌드에 포함하도록 켰습니다 ({BENCHMARK_DEFINE}).\n" +
                "  측정이 끝나면 반드시 다시 끄십시오. 스토어에 올릴 빌드에는 들어가면 안 됩니다.\n" +
                "  측정용 빌드는 -benchmark 인자와 함께 실행해야 하네스가 켜집니다.");
        }
        else
        {
            Debug.Log($"[Benchmark] 하네스를 빌드에서 제외했습니다 ({BENCHMARK_DEFINE} 제거). 에디터에서는 F9가 계속 동작합니다.");
        }
    }

    [MenuItem(MENU_TOGGLE, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MENU_TOGGLE, IsEnabled);
        return true;
    }

    public void OnPreprocessBuild(BuildReport _report)
    {
        if (false == IsEnabled) return;

        // 빌드 로그에 남겨야 나중에 "이 빌드가 측정용이었나"를 되짚을 수 있습니다.
        // 콘솔 경고만으로는 배치 빌드에서 아무도 보지 못합니다.
        Debug.LogWarning(
            $"[Benchmark] 이 빌드에는 벤치마크 하네스가 포함됩니다 ({BENCHMARK_DEFINE} 켜짐).\n" +
            "  스토어에 올릴 빌드라면 지금 중단하고 Tools > 빌드 > \"벤치마크 하네스 빌드에 포함\"을 끄십시오.");
    }

    private static NamedBuildTarget ActiveTarget =>
        NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

    private static bool HasDefine(string _define)
    {
        PlayerSettings.GetScriptingDefineSymbols(ActiveTarget, out string[] _defines);

        for (int i = 0; i < _defines.Length; i++)
        {
            if (_defines[i] == _define) return true;
        }

        return false;
    }

    /// <summary>
    /// 디파인 하나만 넣거나 뺍니다. 다른 디파인(DOTWEEN, STEAMWORKS_NET, BAOBAB_* 등)은
    /// 손대지 않습니다. PlatformBuildModeSwitcher와 같은 방식이라 서로 간섭하지 않습니다.
    /// </summary>
    private static void SetDefine(string _define, bool _enabled)
    {
        NamedBuildTarget _target = ActiveTarget;

        PlayerSettings.GetScriptingDefineSymbols(_target, out string[] _defines);

        List<string> _list = new List<string>(_defines.Length + 1);

        for (int i = 0; i < _defines.Length; i++)
        {
            if (_defines[i] == _define) continue;

            _list.Add(_defines[i]);
        }

        if (true == _enabled) _list.Add(_define);

        PlayerSettings.SetScriptingDefineSymbols(_target, _list.ToArray());
    }
}
