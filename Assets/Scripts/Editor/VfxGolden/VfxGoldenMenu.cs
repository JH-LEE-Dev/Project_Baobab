using Diagnostics.VfxGolden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// VFX 골든 캡처(결과 불변 검증) 실행 창입니다. 원리는 VfxGoldenRunner.cs 머리 주석을 보세요.
///
/// [흐름]
/// 1) 최적화 전 커밋에서 "기준 기록"(라벨 base) 뒤 "기준 보강"을 두어 번(정렬 동률로 실행마다 바뀌는 그림을 허용 그림으로 추가).
/// 2) 최적화 뒤 "비교"(base 대비). FAIL이면 그림이 달라진 것이므로 되돌리거나 고칩니다.
///    WARN은 그림은 같고 정점/렌더러 상태만 다른 경우라 사람이 판단합니다(정렬이 바뀌면 다른 물체와의 앞뒤가 달라질 수 있음).
///
/// 실행하면 열려 있는 씬은 그대로 두고, 플레이 시작 씬만 잠시 전용 빈 씬으로 바꿔 플레이 모드에 들어갑니다.
/// 끝나면 스스로 플레이 모드를 빠져나오고 시작 씬 설정을 되돌립니다.
/// </summary>
[InitializeOnLoad]
public class VfxGoldenMenu : EditorWindow
{
    private const string PrefsPrefix = "VfxGolden.Window.";
    private const string NoneMarker = "<none>";

    private string label = "base";
    private string baseline = "base";
    private string filter = string.Empty;
    private bool bSaveImages;

    static VfxGoldenMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/VFX 검증/골든 캡처 창", false, 300)]
    private static void Open()
    {
        GetWindow<VfxGoldenMenu>("VFX 골든 캡처");
    }

    [MenuItem("Tools/VFX 검증/기준 기록 (base)", false, 301)]
    private static void RecordBase()
    {
        Launch("record", "base", "base", string.Empty, false);
    }

    [MenuItem("Tools/VFX 검증/기준 보강 (base, 정렬 동률 그림 추가)", false, 302)]
    private static void MergeBase()
    {
        Launch("merge", "merge", "base", string.Empty, false);
    }

    [MenuItem("Tools/VFX 검증/비교 (base 대비)", false, 303)]
    private static void CompareBase()
    {
        Launch("compare", "compare", "base", string.Empty, false);
    }

    /// <summary>
    /// 골든 캡처를 시작합니다. 스크립트(Unity MCP 등)에서도 부를 수 있게 public입니다.
    /// </summary>
    /// <param name="_mode">"record", "merge", "compare", "perf"</param>
    /// <param name="_label">이번 결과를 저장할 폴더 이름</param>
    /// <param name="_baseline">비교 대상 라벨(compare일 때만)</param>
    /// <param name="_filter">쉼표로 구분한 케이스 이름 일부. 비우면 전체</param>
    /// <param name="_bSaveImages">15프레임마다 PNG 저장</param>
    public static bool Launch(string _mode, string _label, string _baseline, string _filter, bool _bSaveImages)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VfxGolden] 플레이 모드 중에는 시작할 수 없습니다.");
            return false;
        }

        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(VfxGoldenRunner.ScenePath);
        if (null == scene)
        {
            Debug.LogError("[VfxGolden] 전용 씬이 없습니다: " + VfxGoldenRunner.ScenePath);
            return false;
        }

        SceneAsset previous = EditorSceneManager.playModeStartScene;
        SessionState.SetString(VfxGoldenRunner.KeyRestoreStartScene, null != previous ? AssetDatabase.GetAssetPath(previous) : NoneMarker);
        SessionState.SetString(VfxGoldenRunner.KeyMode, _mode);
        SessionState.SetString(VfxGoldenRunner.KeyLabel, _label);
        SessionState.SetString(VfxGoldenRunner.KeyBaseline, _baseline);
        SessionState.SetString(VfxGoldenRunner.KeyFilter, _filter ?? string.Empty);
        SessionState.SetBool(VfxGoldenRunner.KeySaveImages, _bSaveImages);

        EditorSceneManager.playModeStartScene = scene;
        EditorApplication.EnterPlaymode();
        return true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange _state)
    {
        if (PlayModeStateChange.EnteredEditMode != _state) return;

        string restore = SessionState.GetString(VfxGoldenRunner.KeyRestoreStartScene, string.Empty);
        if (string.IsNullOrEmpty(restore)) return;

        EditorSceneManager.playModeStartScene = NoneMarker == restore ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(restore);
        SessionState.EraseString(VfxGoldenRunner.KeyRestoreStartScene);
        SessionState.EraseString(VfxGoldenRunner.KeyMode);
    }

    private void OnEnable()
    {
        label = EditorPrefs.GetString(PrefsPrefix + "Label", "compare");
        baseline = EditorPrefs.GetString(PrefsPrefix + "Baseline", "base");
        filter = EditorPrefs.GetString(PrefsPrefix + "Filter", string.Empty);
        bSaveImages = EditorPrefs.GetBool(PrefsPrefix + "SaveImages", false);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("최적화 전 커밋에서 '기록'(라벨 base), 최적화 뒤 '비교'. 결과: Library/VfxGolden/<라벨>/_report.txt", MessageType.Info);
        label = EditorGUILayout.TextField("이번 라벨", label);
        baseline = EditorGUILayout.TextField("비교 기준 라벨", baseline);
        filter = EditorGUILayout.TextField("케이스 필터(쉼표)", filter);
        bSaveImages = EditorGUILayout.Toggle("PNG 저장(15프레임마다)", bSaveImages);

        EditorPrefs.SetString(PrefsPrefix + "Label", label);
        EditorPrefs.SetString(PrefsPrefix + "Baseline", baseline);
        EditorPrefs.SetString(PrefsPrefix + "Filter", filter);
        EditorPrefs.SetBool(PrefsPrefix + "SaveImages", bSaveImages);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("기록")) Launch("record", label, baseline, filter, bSaveImages);
            if (GUILayout.Button("기준 보강(정렬 동률 그림을 기준 라벨에 추가)")) Launch("merge", label, baseline, filter, bSaveImages);
            if (GUILayout.Button("비교")) Launch("compare", label, baseline, filter, bSaveImages);
            if (GUILayout.Button("성능 측정(기준 라벨이 있으면 비율 표시)")) Launch("perf", label, baseline, filter, false);
        }

        if (GUILayout.Button("결과 폴더 열기"))
        {
            System.IO.Directory.CreateDirectory(VfxGoldenRunner.GetRootFolder());
            EditorUtility.RevealInFinder(VfxGoldenRunner.GetRootFolder());
        }
    }
}
