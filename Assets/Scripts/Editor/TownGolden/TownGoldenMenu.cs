using Diagnostics.TownGolden;
using UnityEditor;

/// <summary>
/// 마을 골든(결과 불변 검증) 메뉴입니다. 원리는 TownGoldenRunner.cs 머리 주석을 보세요.
/// 메인 메뉴 씬(Assets/Scenes/MainMenuScene.unity)을 연 상태에서 실행합니다. 한 번에 대본 하나만 돕니다.
/// 세이브는 이 실행 동안 막혀 있어 디스크에 쓰지 않습니다.
///
/// [흐름]
/// 1) 최적화 전: "기준 기록"(base)과 "기준 기록 2"(base2)를 save·heavy 각각 실행.
///    두 기준이 서로 다른 칸은 실제 시간(unscaledTime)을 쓰는 연출이라 비교에서 가립니다.
/// 2) 최적화 뒤: "비교"를 save·heavy 각각 실행. 결과는 Library/TownGolden/after/_report_&lt;대본&gt;.txt
///    PASS: 차이 없음. WARN: 기준끼리도 흔들린 묶음·프레임(텐트 실제 시간 연출 등)이나 오디오(A)에서만 차이 → 목록 확인.
///    FAIL: 기준끼리 안정적이던 칸이 다름(월드·물리·난수는 기준끼리 항상 같았음).
///    INVALID: 로드·조작 해제 프레임, 화면 크기, 끊김(긴 간격) 프레임 목록이 기준과 달라 비교가 무의미 → 다시 돌립니다.
/// 실행 중에는 스크립트를 고치지 마세요(재컴파일되면 ReloadGuard가 세이브를 막고 플레이를 멈춥니다).
/// </summary>
public static class TownGoldenMenu
{
    private const int EndFrames = 1260;
    private const string Baselines = "base|base2";

    [MenuItem("Tools/마을 검증/기준 기록 - save (사용자 세이브 그대로)", false, 320)]
    private static void RecordSave() { TownGoldenRunner.Launch("record", "base", string.Empty, "save", true, EndFrames); }

    [MenuItem("Tools/마을 검증/기준 기록 - heavy (최대 부하)", false, 321)]
    private static void RecordHeavy() { TownGoldenRunner.Launch("record", "base", string.Empty, "heavy", true, EndFrames); }

    [MenuItem("Tools/마을 검증/기준 기록 2 - save", false, 322)]
    private static void RecordSave2() { TownGoldenRunner.Launch("compare", "base2", "base", "save", true, EndFrames); }

    [MenuItem("Tools/마을 검증/기준 기록 2 - heavy", false, 323)]
    private static void RecordHeavy2() { TownGoldenRunner.Launch("compare", "base2", "base", "heavy", true, EndFrames); }

    [MenuItem("Tools/마을 검증/비교 - save", false, 340)]
    private static void CompareSave() { TownGoldenRunner.Launch("compare", "after", Baselines, "save", true, EndFrames); }

    [MenuItem("Tools/마을 검증/비교 - heavy", false, 341)]
    private static void CompareHeavy() { TownGoldenRunner.Launch("compare", "after", Baselines, "heavy", true, EndFrames); }

    // 골든 실행 중 스크립트가 다시 불리면 하네스(난수·시간 고정, 대본)가 사라진 채 게임만 계속 돈다.
    // 세이브 차단 값도 다시 불리며 풀릴 수 있으므로, 진짜 SaveManager(BootStrap에 붙은 것)를 바로 막고 플레이를 멈춘다.
    [InitializeOnLoadMethod]
    private static void ReloadGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (false == SessionState.GetBool(TownGoldenRunner.KeyRunning, false)) return;
        SessionState.EraseBool(TownGoldenRunner.KeyRunning);
        if (false == EditorApplication.isPlaying) return;

        BootStrap bs = UnityEngine.Object.FindAnyObjectByType<BootStrap>();
        SaveManager sm = null != bs ? bs.GetComponent<SaveManager>() : null;
        if (null != sm) typeof(SaveManager).GetField("bSaveBlockedByUnreadableSave", (System.Reflection.BindingFlags)(4 | 16 | 32)).SetValue(sm, true);
        UnityEngine.Debug.LogError("[TownGolden] 실행 중 스크립트가 다시 불려 하네스가 끊겼습니다. 세이브를 막고 플레이를 멈춥니다. (세이브 차단 " + (null != sm) + ")");
        UnityEngine.Time.captureDeltaTime = 0.0f;
        EditorApplication.isPlaying = false;
    }

    private static void OnPlayModeChanged(PlayModeStateChange _change)
    {
        if (PlayModeStateChange.EnteredEditMode == _change) SessionState.EraseBool(TownGoldenRunner.KeyRunning);
    }
}
