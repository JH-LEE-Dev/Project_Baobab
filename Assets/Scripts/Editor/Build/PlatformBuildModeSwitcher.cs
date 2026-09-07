using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// BuildStore 는 런타임 쪽 BuildInfo.cs 에 있습니다. 게임과 에디터가 같은 열거형을 봐야
// 스위처가 만든 설정과 게임이 읽는 값이 어긋나지 않습니다.

/// <summary>배포 형태입니다. 런타임에서는 SaveBuildVariant 가 같은 역할을 합니다.</summary>
public enum BuildRelease
{
    Demo,
    Full,
}

/// <summary>
/// 빌드 구성을 한 번에 전환합니다. 축은 둘입니다 — <b>스토어(Steam/STOVE) × 배포(데모/정식)</b>.
///
/// [왜 스위처가 하나인가]
/// 축마다 메뉴를 따로 두면 "STOVE인데 Steam 디파인이 남은" 같은 어긋난 조합이 생기고,
/// 그 조합을 아무도 검증하지 않습니다. 두 축을 한 곳에서 다뤄야 전환할 때마다 전체가 맞춰집니다.
///
/// [무엇이 딸려 오는가]
/// 디파인에서 자동으로 파생되는 것(세이브 변형, 맵 제한, 세이브 폴더 이름, Steam API 사용 여부)은
/// 신경 쓸 필요가 없습니다. 문제는 <b>코드가 아니라 파일·에셋이라 디파인을 따라오지 않는 것들</b>입니다.
///   - steam_appid.txt        (파일)
///   - Sentry environment     (Resources 아래 에셋 - 빌드에 무조건 실림)
///   - GameAnalytics build    (Resources 아래 에셋 - 빌드에 무조건 실림)
/// 이 셋이 어긋나면 데모에서 올라온 크래시·지표가 정식에 섞이거나, 개발 중 Steam이 엉뚱한 앱으로
/// 인식합니다. 그래서 여기서 함께 씁니다.
///
/// [스위처는 안전장치가 아닙니다]
/// 이건 편의 장치일 뿐이고, 사람이 메뉴 누르는 걸 깜빡하면 그대로 나갑니다.
/// 실제로 막는 것은 PlatformConsistencyGuard입니다. 빌드 직전에 여기 값들과 디파인을 대조해
/// 어긋나면 빌드를 멈춥니다.
///
/// [극성 주의]
/// <b>디파인이 없는 상태가 Steam + 데모입니다.</b> 정식일 때만 BAOBAB_FULL_RELEASE를,
/// STOVE일 때만 BAOBAB_STOVE와 DISABLESTEAMWORKS를 켭니다. (BuildInfo / GamePaths 주석 참고)
/// </summary>
public static class PlatformBuildModeSwitcher
{
#region 디파인

    private const string FULL_RELEASE_DEFINE = "BAOBAB_FULL_RELEASE";
    private const string STOVE_DEFINE = "BAOBAB_STOVE";

    /// <summary>
    /// Steamworks.NET이 자기 파일 전체에서 존중하는 심볼입니다. 켜면 패키지 런타임 108개 파일과
    /// SteamManager·SteamCloudSaveService·SteamLanguageService가 통째로 컴파일에서 빠집니다.
    ///
    /// STOVE 빌드에서 이게 빠지면 SteamAPI.RestartAppIfNecessary가 살아남아, Steam을 켜둔 채
    /// 이 게임을 Steam에서 사지 않은 유저가 게임을 아예 실행하지 못합니다.
    /// 그래서 BAOBAB_STOVE와 <b>반드시 짝</b>이어야 하며, 가드가 그 짝을 검사합니다.
    /// </summary>
    private const string DISABLE_STEAMWORKS_DEFINE = "DISABLESTEAMWORKS";

#endregion

#region 메뉴

    private const string MENU_STORE_STEAM = "Tools/빌드/스토어 - Steam";
    private const string MENU_STORE_STOVE = "Tools/빌드/스토어 - STOVE";
    private const string MENU_RELEASE_DEMO = "Tools/빌드/배포 - 데모";
    private const string MENU_RELEASE_FULL = "Tools/빌드/배포 - 정식";
    private const string MENU_SHOW = "Tools/빌드/현재 빌드 설정 확인";
    private const string MENU_SPACEWAR = "Tools/빌드/steam_appid.txt 를 480(Spacewar)으로";

#endregion

#region 동기화 대상

    /// <summary>둘 다 Resources 아래라 빌드에 무조건 포함됩니다.</summary>
    private const string GAME_ANALYTICS_SETTINGS_PATH = "Assets/Resources/GameAnalytics/Settings.asset";
    private const string SENTRY_OPTIONS_PATH = "Assets/Resources/Sentry/SentryOptions.asset";

    /// <summary>
    /// Valve의 공개 테스트 앱. 모든 Steam 계정이 자동으로 소유하므로 SteamAPI.Init()이 항상 성공합니다.
    /// 실제 앱은 라이선스가 계정에 붙기 전까지 초기화가 실패해 에디터 로그가 시끄러우므로,
    /// 앱 설정이 끝나기 전까지 개발용으로 이 값을 씁니다.
    /// </summary>
    private const uint STEAM_APP_ID_SPACEWAR = 480;

#endregion

#region 현재 상태 읽기 (가드도 같이 씁니다)

    public static BuildStore CurrentStore => HasDefine(STOVE_DEFINE) ? BuildStore.Stove : BuildStore.Steam;

    public static BuildRelease CurrentRelease => HasDefine(FULL_RELEASE_DEFINE) ? BuildRelease.Full : BuildRelease.Demo;

    public static bool HasDisableSteamworksDefine => HasDefine(DISABLE_STEAMWORKS_DEFINE);

    /// <summary>
    /// Sentry의 environment 태그입니다. 스토어와 배포를 모두 담아, 크래시가 어느 빌드에서 왔는지
    /// 대시보드에서 바로 갈라 볼 수 있게 합니다. 하나로 뭉치면 STOVE 크래시가 Steam 지표에 섞입니다.
    /// </summary>
    public static string ExpectedSentryEnvironment(BuildStore _store, BuildRelease _release)
    {
        string _storeTag = (BuildStore.Stove == _store) ? "stove" : "steam";
        string _releaseTag = (BuildRelease.Full == _release) ? "production" : "demo";

        return _storeTag + "-" + _releaseTag;
    }

    /// <summary>
    /// GameAnalytics의 build 문자열입니다. 버전은 Player Settings를 따라가므로 버전을 올려도
    /// 여기를 따로 고칠 필요가 없습니다.
    /// </summary>
    public static string ExpectedGameAnalyticsBuild(BuildStore _store, BuildRelease _release)
    {
        string _value = PlayerSettings.bundleVersion + ((BuildStore.Stove == _store) ? "-stove" : "-steam");

        if (BuildRelease.Full != _release) _value += "-demo";

        return _value;
    }

    /// <summary>
    /// steam_appid.txt에 들어갈 값입니다. STOVE 빌드에는 의미가 없어 null을 돌려줍니다.
    /// (데모와 정식은 Steam에서 서로 다른 앱이라 번호가 다릅니다)
    /// </summary>
    public static string ExpectedSteamAppId(BuildStore _store, BuildRelease _release)
    {
        if (BuildStore.Stove == _store) return null;

        uint _id = (BuildRelease.Full == _release) ? BuildInfo.STEAM_APP_ID_RELEASE : BuildInfo.STEAM_APP_ID_DEMO;

        return _id.ToString();
    }

    public static string ReadSentryEnvironment()
    {
        SerializedObject _so = LoadSettingsAsset(SENTRY_OPTIONS_PATH);
        SerializedProperty _env = (null == _so) ? null : _so.FindProperty("<EnvironmentOverride>k__BackingField");

        if (null == _env) return null;

        return _env.stringValue;
    }

    public static string ReadGameAnalyticsBuild()
    {
        SerializedObject _so = LoadSettingsAsset(GAME_ANALYTICS_SETTINGS_PATH);
        SerializedProperty _build = (null == _so) ? null : _so.FindProperty("Build");

        if (null == _build || false == _build.isArray || 0 == _build.arraySize) return null;

        return _build.GetArrayElementAtIndex(0).stringValue;
    }

#endregion

#region 메뉴 동작

    [MenuItem(MENU_STORE_STEAM, false, 1)]
    private static void SwitchToSteam() { Apply(BuildStore.Steam, CurrentRelease); }

    [MenuItem(MENU_STORE_STOVE, false, 2)]
    private static void SwitchToStove() { Apply(BuildStore.Stove, CurrentRelease); }

    [MenuItem(MENU_RELEASE_DEMO, false, 21)]
    private static void SwitchToDemo() { Apply(CurrentStore, BuildRelease.Demo); }

    [MenuItem(MENU_RELEASE_FULL, false, 22)]
    private static void SwitchToFull() { Apply(CurrentStore, BuildRelease.Full); }

    [MenuItem(MENU_STORE_STEAM, true)]
    private static bool ValidateSteam() { Menu.SetChecked(MENU_STORE_STEAM, BuildStore.Steam == CurrentStore); return true; }

    [MenuItem(MENU_STORE_STOVE, true)]
    private static bool ValidateStove() { Menu.SetChecked(MENU_STORE_STOVE, BuildStore.Stove == CurrentStore); return true; }

    [MenuItem(MENU_RELEASE_DEMO, true)]
    private static bool ValidateDemo() { Menu.SetChecked(MENU_RELEASE_DEMO, BuildRelease.Demo == CurrentRelease); return true; }

    [MenuItem(MENU_RELEASE_FULL, true)]
    private static bool ValidateFull() { Menu.SetChecked(MENU_RELEASE_FULL, BuildRelease.Full == CurrentRelease); return true; }

    [MenuItem(MENU_SPACEWAR, false, 41)]
    private static void SwitchAppIdToSpacewar()
    {
        WriteAppIdFile(STEAM_APP_ID_SPACEWAR.ToString());

        Debug.Log("[BuildMode] steam_appid.txt = 480 (Spacewar). 에디터에서 SteamAPI 초기화 실패 로그가 사라집니다.\n" +
                  "Steam 클라우드는 실제 앱 대상으로 시험할 수 없습니다. 앱 설정이 끝나면 스토어를 다시 선택해 되돌리세요.");
    }

    [MenuItem(MENU_SPACEWAR, true)]
    private static bool ValidateSpacewar()
    {
        Menu.SetChecked(MENU_SPACEWAR, ReadAppIdFile() == STEAM_APP_ID_SPACEWAR.ToString());
        return BuildStore.Steam == CurrentStore;
    }

    [MenuItem(MENU_SHOW, false, 61)]
    private static void ShowCurrent()
    {
        BuildStore _store = CurrentStore;
        BuildRelease _release = CurrentRelease;

        string _expectedAppId = ExpectedSteamAppId(_store, _release);
        string _fileAppId = ReadAppIdFile();
        bool _isSpacewar = _fileAppId == STEAM_APP_ID_SPACEWAR.ToString();

        List<string> _problems = new List<string>();

        if (BuildStore.Stove == _store && false == HasDisableSteamworksDefine)
        {
            _problems.Add("STOVE인데 DISABLESTEAMWORKS가 없습니다. Steam DRM이 살아 있어 유저가 게임을 못 켭니다.");
        }

        if (BuildStore.Steam == _store && true == HasDisableSteamworksDefine)
        {
            _problems.Add("Steam인데 DISABLESTEAMWORKS가 켜져 있습니다. 클라우드·언어·실적이 전부 죽습니다.");
        }

        string _expectedEnv = ExpectedSentryEnvironment(_store, _release);
        if (ReadSentryEnvironment() != _expectedEnv) _problems.Add($"Sentry environment가 어긋납니다. (기대: {_expectedEnv})");

        string _expectedBuild = ExpectedGameAnalyticsBuild(_store, _release);
        if (ReadGameAnalyticsBuild() != _expectedBuild) _problems.Add($"GameAnalytics build가 어긋납니다. (기대: {_expectedBuild})");

        if (null != _expectedAppId && _fileAppId != _expectedAppId && false == _isSpacewar)
        {
            _problems.Add($"steam_appid.txt가 어긋납니다. (기대: {_expectedAppId})");
        }

        string _verdict;

        if (0 == _problems.Count)
        {
            _verdict = _isSpacewar
                ? "개발용 테스트 앱(Spacewar)으로 설정된 것 외에는 모두 일치합니다.\n배포 빌드 전에 스토어를 다시 선택해 되돌리세요."
                : "모두 일치합니다.";
        }
        else
        {
            _verdict = "⚠ 다음이 어긋나 있습니다. 메뉴에서 스토어와 배포를 다시 선택하세요.\n\n  · " + string.Join("\n  · ", _problems);
        }

        string _message =
            $"스토어         : {_store}\n" +
            $"배포           : {(BuildRelease.Full == _release ? "정식" : "데모")}\n" +
            $"디파인         : {DescribeDefines(_store, _release)}\n" +
            $"세이브 변형     : {BuildInfo.Variant}\n" +
            $"세이브 폴더     : {(BuildStore.Stove == _store ? "LumberBoy_STOVE" : "LumberBoy")}\n" +
            $"기대 앱 ID      : {(null == _expectedAppId ? "(STOVE - 사용 안 함)" : _expectedAppId)}\n" +
            $"steam_appid    : {_fileAppId}\n" +
            $"Sentry env     : {ReadSentryEnvironment() ?? "(읽기 실패)"}\n" +
            $"GA build       : {ReadGameAnalyticsBuild() ?? "(읽기 실패)"}\n\n" + _verdict;

        EditorUtility.DisplayDialog("빌드 설정", _message, "확인");

        if (0 != _problems.Count) Debug.LogWarning("[BuildMode] 설정 불일치:\n  · " + string.Join("\n  · ", _problems));
    }

    private static string DescribeDefines(BuildStore _store, BuildRelease _release)
    {
        List<string> _list = new List<string>();

        if (BuildRelease.Full == _release) _list.Add(FULL_RELEASE_DEFINE);
        if (BuildStore.Stove == _store) _list.Add(STOVE_DEFINE);
        if (true == HasDisableSteamworksDefine) _list.Add(DISABLE_STEAMWORKS_DEFINE);

        return (0 == _list.Count) ? "없음" : string.Join(" + ", _list);
    }

#endregion

#region 적용

    private static void Apply(BuildStore _store, BuildRelease _release)
    {
        NamedBuildTarget _target = ActiveTarget;

        PlayerSettings.GetScriptingDefineSymbols(_target, out string[] _defines);

        // 우리가 다루는 셋만 넣고 뺀다. 다른 디파인(DOTWEEN, STEAMWORKS_NET 등)은 그대로 둔다.
        List<string> _list = new List<string>(_defines.Length + 3);

        for (int i = 0; i < _defines.Length; i++)
        {
            string _d = _defines[i];

            if (_d == FULL_RELEASE_DEFINE) continue;
            if (_d == STOVE_DEFINE) continue;
            if (_d == DISABLE_STEAMWORKS_DEFINE) continue;

            _list.Add(_d);
        }

        if (BuildRelease.Full == _release) _list.Add(FULL_RELEASE_DEFINE);

        if (BuildStore.Stove == _store)
        {
            // 이 둘은 반드시 함께 간다. 하나만 켜면 STOVE 빌드에 Steam DRM이 남거나,
            // Steam 빌드에서 클라우드가 조용히 죽는다.
            _list.Add(STOVE_DEFINE);
            _list.Add(DISABLE_STEAMWORKS_DEFINE);
        }

        PlayerSettings.SetScriptingDefineSymbols(_target, _list.ToArray());

        string _appId = ExpectedSteamAppId(_store, _release);
        if (null != _appId) WriteAppIdFile(_appId);

        SyncAnalyticsAssets(_store, _release);

        Debug.Log($"[BuildMode] {_store} / {(BuildRelease.Full == _release ? "정식" : "데모")} 으로 전환했습니다.\n" +
                  $"  디파인      : {DescribeDefines(_store, _release)}\n" +
                  $"  세이브 폴더  : {(BuildStore.Stove == _store ? "LumberBoy_STOVE" : "LumberBoy")}\n" +
                  $"  Sentry env  : {ExpectedSentryEnvironment(_store, _release)}\n" +
                  $"  GA build    : {ExpectedGameAnalyticsBuild(_store, _release)}\n" +
                  $"  steam_appid : {(null == _appId ? "(STOVE - 건드리지 않음)" : _appId)}\n" +
                  "스크립트 재컴파일 후 적용됩니다.\n" +
                  "주의: 데모↔정식 세이브는 서로 호환되지 않고, Steam↔STOVE는 세이브 폴더 자체가 다릅니다.");
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

#endregion

#region steam_appid.txt

    private static string AppIdFilePath =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "steam_appid.txt");

    private static string ReadAppIdFile()
    {
        try
        {
            string _path = AppIdFilePath;
            if (false == File.Exists(_path)) return "(파일 없음)";

            return File.ReadAllText(_path).Trim();
        }
        catch (System.Exception _e)
        {
            return "(읽기 실패: " + _e.Message + ")";
        }
    }

    private static void WriteAppIdFile(string _appId)
    {
        try
        {
            // 개행 없이 숫자만 있어야 한다. Steam이 파일 내용을 그대로 파싱한다.
            File.WriteAllText(AppIdFilePath, _appId);
        }
        catch (System.Exception _e)
        {
            Debug.LogError("[BuildMode] steam_appid.txt 기록 실패: " + _e.Message);
        }
    }

#endregion

#region 분석 도구 에셋

    private static void SyncAnalyticsAssets(BuildStore _store, BuildRelease _release)
    {
        bool _changed = false;

        // |= 는 단축 평가를 하지 않으므로 둘 다 실행된다.
        _changed |= SyncSentryEnvironment(_store, _release);
        _changed |= SyncGameAnalyticsBuild(_store, _release);

        if (true == _changed) AssetDatabase.SaveAssets();
    }

    private static bool SyncSentryEnvironment(BuildStore _store, BuildRelease _release)
    {
        SerializedObject _so = LoadSettingsAsset(SENTRY_OPTIONS_PATH);
        if (null == _so) return false;

        SerializedProperty _env = _so.FindProperty("<EnvironmentOverride>k__BackingField");

        if (null == _env)
        {
            Debug.LogWarning("[BuildMode] Sentry의 EnvironmentOverride 필드를 찾지 못했습니다. SDK 버전이 바뀌었는지 확인하세요.");
            return false;
        }

        _env.stringValue = ExpectedSentryEnvironment(_store, _release);

        return _so.ApplyModifiedProperties();
    }

    private static bool SyncGameAnalyticsBuild(BuildStore _store, BuildRelease _release)
    {
        SerializedObject _so = LoadSettingsAsset(GAME_ANALYTICS_SETTINGS_PATH);
        if (null == _so) return false;

        SerializedProperty _build = _so.FindProperty("Build");

        if (null == _build || false == _build.isArray)
        {
            Debug.LogWarning("[BuildMode] GameAnalytics의 Build 필드를 찾지 못했습니다. SDK 버전이 바뀌었는지 확인하세요.");
            return false;
        }

        string _value = ExpectedGameAnalyticsBuild(_store, _release);

        // GA는 플랫폼별 배열을 쓰지만 이 프로젝트는 Standalone 하나뿐이라 전부 같은 값으로 맞춘다.
        if (0 == _build.arraySize) _build.arraySize = 1;

        for (int i = 0; i < _build.arraySize; i++)
        {
            _build.GetArrayElementAtIndex(i).stringValue = _value;
        }

        return _so.ApplyModifiedProperties();
    }

    private static SerializedObject LoadSettingsAsset(string _path)
    {
        ScriptableObject _asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(_path);

        if (null == _asset)
        {
            Debug.LogWarning("[BuildMode] 설정 에셋을 찾지 못했습니다: " + _path);
            return null;
        }

        return new SerializedObject(_asset);
    }

#endregion
}
