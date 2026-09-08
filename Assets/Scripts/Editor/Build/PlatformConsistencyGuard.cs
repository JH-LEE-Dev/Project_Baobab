using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 빌드 구성이 어긋난 채로 배포 빌드가 나가는 것을 막습니다.
///
/// [왜 스위처만으로는 부족한가]
/// PlatformBuildModeSwitcher는 편의 장치입니다. 메뉴 누르는 걸 깜빡하면 그대로 나갑니다.
/// 그런데 이 프로젝트의 플랫폼 축은 <b>실패 방향을 안전한 쪽으로 몰 수가 없습니다.</b>
///
///   · 세이브 폴더  — 스토어 디파인을 깜빡 → Steam 폴더를 그대로 씀. STOVE는 런처가 빈 폴더를
///                    동기화하고, itch는 Steam 클라우드가 그 세이브를 자기 것으로 올려감
///   · Steam DRM   — 스토어 디파인을 깜빡 → RestartAppIfNecessary가 살아남아
///                    <b>Steam을 켜둔 채 이 게임을 Steam에서 사지 않은 유저가 실행조차 못 함</b>
///
/// 둘이 같은 디파인에 매달려 있고 한쪽 대가가 치명적이라, 사람 기억이 아니라 빌드 중단으로 막습니다.
/// ReleaseScriptingBackendGuard가 Mono 배포를 막는 것과 같은 이유이고 같은 패턴입니다.
///
/// [무엇을 보는가]
/// 어긋나도 <b>아무 에러가 나지 않는</b> 것들만 봅니다. 컴파일이 깨지는 종류는 어차피 빌드가 안 됩니다.
///   1. 스토어 디파인(BAOBAB_STOVE / BAOBAB_ITCH)과 DISABLESTEAMWORKS 의 짝
///   2. 스크립트 재컴파일 여부 (메뉴를 누르고 바로 빌드를 누른 경우)
///   3. Sentry environment / GameAnalytics build (Resources 아래라 빌드에 무조건 실림)
///   4. steam_appid.txt (경고만 — 배포물에 실리지 않아 빌드를 막을 이유가 없음)
///
/// [개발 빌드는 통과시킵니다]
/// Development Build는 어차피 배포할 수 없고, 반복 작업 중에 일부러 어긋난 조합을 시험하는 일도
/// 있습니다. 경고만 남기고 통과시킵니다. ReleaseScriptingBackendGuard와 같은 관용입니다.
/// </summary>
public class PlatformConsistencyGuard : IPreprocessBuildWithReport
{
    /// <summary>
    /// ReleaseScriptingBackendGuard(-1000) 다음, DemoContentStripper(0) 앞입니다.
    /// 스트리퍼가 DB 에셋에서 미공개 콘텐츠를 들어낸 <b>뒤에</b> 여기서 막으면,
    /// 빌드는 멈췄는데 에셋은 수정된 채로 남습니다.
    /// </summary>
    public int callbackOrder => -900;

    /// <summary>
    /// 에디터 어셈블리가 <b>실제로 컴파일된</b> 상태입니다. PlayerSettings의 문자열과 달리,
    /// 이 값은 재컴파일이 끝나야 바뀝니다. 둘이 다르면 메뉴를 누르고 스크립트가 다시 컴파일되기 전에
    /// 빌드를 시작한 것이며, 그대로 두면 <b>구버전 코드가 새 설정으로 나갑니다.</b>
    /// </summary>
#if BAOBAB_STOVE
    private const bool COMPILED_AS_STOVE = true;
#else
    private const bool COMPILED_AS_STOVE = false;
#endif

#if BAOBAB_ITCH
    private const bool COMPILED_AS_ITCH = true;
#else
    private const bool COMPILED_AS_ITCH = false;
#endif

#if DISABLESTEAMWORKS
    private const bool COMPILED_WITHOUT_STEAMWORKS = true;
#else
    private const bool COMPILED_WITHOUT_STEAMWORKS = false;
#endif

#if BAOBAB_FULL_RELEASE
    private const bool COMPILED_AS_FULL_RELEASE = true;
#else
    private const bool COMPILED_AS_FULL_RELEASE = false;
#endif

    public void OnPreprocessBuild(BuildReport _report)
    {
        if (null == _report) return;
        if (false == IsStandalone(_report.summary.platform)) return;

        BuildStore _store = PlatformBuildModeSwitcher.CurrentStore;
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;

        List<string> _errors = new List<string>();
        List<string> _warnings = new List<string>();

        CheckDefinePairing(_store, _errors);
        CheckRecompiled(_store, _release, _errors);
        CheckAnalytics(_store, _release, _errors);
        CheckStoveDemoBranding(_store, _release, _errors);
        CheckSteamAppId(_store, _release, _warnings);

        for (int i = 0; i < _warnings.Count; i++)
        {
            Debug.LogWarning("[PlatformGuard] " + _warnings[i]);
        }

        if (0 == _errors.Count) return;

        // EditorUserBuildSettings.development는 스크립트로 빌드할 때 실제 값과 어긋난다.
        // 리포트에 담긴 옵션을 본다. (ReleaseScriptingBackendGuard와 같은 이유)
        bool _isDevelopmentBuild = 0 != (_report.summary.options & BuildOptions.Development);

        string _detail = "  · " + string.Join("\n  · ", _errors);

        if (true == _isDevelopmentBuild)
        {
            Debug.LogWarning($"[PlatformGuard] 빌드 구성이 어긋나 있습니다. Development Build라 그대로 진행합니다.\n{_detail}\n" +
                             "배포용으로 낼 때는 Tools > 빌드 에서 스토어와 배포를 다시 선택하십시오.");
            return;
        }

        throw new BuildFailedException(
            $"[PlatformGuard] 배포용 빌드를 중단했습니다. 현재 구성: {_store} / {(BuildRelease.Full == _release ? "정식" : "데모")}\n" +
            "\n" +
            $"{_detail}\n" +
            "\n" +
            "해결: Tools > 빌드 에서 스토어와 배포를 다시 선택한 뒤,\n" +
            "스크립트 재컴파일이 끝나기를 기다렸다가 다시 빌드하십시오.\n" +
            "Tools > 빌드 > 현재 빌드 설정 확인 으로 값을 눈으로 볼 수 있습니다.\n" +
            "\n" +
            "지금 당장 개발용으로 어긋난 조합이 필요하다면 Development Build를 켜면 통과합니다.");
    }

#region 검사

    /// <summary>
    /// 스토어 디파인과 DISABLESTEAMWORKS 는 반드시 함께 켜지고 함께 꺼져야 합니다.
    /// 한쪽만 있으면 어느 방향이든 조용히 망가집니다.
    ///
    /// 판정을 스토어 목록이 아니라 RequiresDisableSteamworks 에 맡깁니다. 스토어를 하나씩
    /// 나열하면 새 스토어를 더할 때 여기를 빠뜨리고, <b>가드가 있는데도 그 빌드만 안 걸립니다.</b>
    /// </summary>
    private static void CheckDefinePairing(BuildStore _store, List<string> _errors)
    {
        bool _hasDisable = PlatformBuildModeSwitcher.HasDisableSteamworksDefine;

        if (true == PlatformBuildModeSwitcher.RequiresDisableSteamworks(_store) && false == _hasDisable)
        {
            _errors.Add($"{_store} 빌드인데 DISABLESTEAMWORKS 가 없습니다. " +
                        "SteamAPI.RestartAppIfNecessary 가 살아남아, Steam을 켜둔 채 이 게임을 Steam에서 " +
                        "사지 않은 유저가 게임을 실행조차 못 합니다.");
        }

        if (BuildStore.Steam == _store && true == _hasDisable)
        {
            _errors.Add("Steam 빌드인데 DISABLESTEAMWORKS 가 켜져 있습니다. " +
                        "클라우드 세이브와 Steam 언어 감지가 통째로 죽은 채 나갑니다. 에러는 나지 않습니다.");
        }
    }

    /// <summary>
    /// 메뉴는 PlayerSettings의 문자열을 바로 바꾸지만, 코드에 반영되려면 재컴파일이 끝나야 합니다.
    /// 그 사이에 빌드를 누르면 설정과 실제 코드가 다른 물건이 나옵니다.
    /// </summary>
    private static void CheckRecompiled(BuildStore _store, BuildRelease _release, List<string> _errors)
    {
        bool _settingsSaysStove = BuildStore.Stove == _store;
        bool _settingsSaysItch = BuildStore.Itch == _store;
        bool _settingsSaysFull = BuildRelease.Full == _release;
        bool _settingsSaysDisable = PlatformBuildModeSwitcher.HasDisableSteamworksDefine;

        if (_settingsSaysStove != COMPILED_AS_STOVE
            || _settingsSaysItch != COMPILED_AS_ITCH
            || _settingsSaysFull != COMPILED_AS_FULL_RELEASE
            || _settingsSaysDisable != COMPILED_WITHOUT_STEAMWORKS)
        {
            _errors.Add("설정은 바뀌었지만 스크립트가 아직 그 설정으로 컴파일되지 않았습니다. " +
                        "메뉴를 누른 직후 바로 빌드한 경우입니다. " +
                        "에디터가 재컴파일을 끝낼 때까지 기다렸다가 다시 빌드하십시오. " +
                        $"(설정: STOVE={_settingsSaysStove}, itch={_settingsSaysItch}, 정식={_settingsSaysFull}, DISABLESTEAMWORKS={_settingsSaysDisable} / " +
                        $"컴파일됨: STOVE={COMPILED_AS_STOVE}, itch={COMPILED_AS_ITCH}, 정식={COMPILED_AS_FULL_RELEASE}, DISABLESTEAMWORKS={COMPILED_WITHOUT_STEAMWORKS})");
        }
    }

    /// <summary>
    /// 둘 다 Resources 아래라 빌드에 무조건 실립니다. 어긋나면 다른 스토어의 크래시와 지표가
    /// Steam 데이터에 섞여 나중에 구분할 수 없게 됩니다. 섞인 뒤에는 되돌릴 방법이 없습니다.
    /// </summary>
    private static void CheckAnalytics(BuildStore _store, BuildRelease _release, List<string> _errors)
    {
        string _expectedEnv = PlatformBuildModeSwitcher.ExpectedSentryEnvironment(_store, _release);
        string _actualEnv = PlatformBuildModeSwitcher.ReadSentryEnvironment();

        if (_actualEnv != _expectedEnv)
        {
            _errors.Add($"Sentry environment 가 어긋납니다. 기대 \"{_expectedEnv}\", 실제 \"{_actualEnv ?? "(읽기 실패)"}\"");
        }

        string _expectedBuild = PlatformBuildModeSwitcher.ExpectedGameAnalyticsBuild(_store, _release);
        string _actualBuild = PlatformBuildModeSwitcher.ReadGameAnalyticsBuild();

        if (_actualBuild != _expectedBuild)
        {
            _errors.Add($"GameAnalytics build 가 어긋납니다. 기대 \"{_expectedBuild}\", 실제 \"{_actualBuild ?? "(읽기 실패)"}\"");
        }
    }

    /// <summary>
    /// 데모 안내 팝업은 데모 빌드에서만 뜹니다. STOVE 데모를 내면 유저가 그 팝업을 실제로 보는데,
    /// 기본값이 Steam 링크 · Steam 로고 · "Steam 찜하기" 문구라 그대로 나가면
    /// <b>STOVE에서 받은 사람에게 Steam 상점으로 가라고 안내하게 됩니다.</b>
    ///
    /// STOVE 데모는 <b>본편 상점으로 보내지 않기로 했습니다.</b> 상점 버튼을 통째로 끄고(코드),
    /// 본문에서도 찜하기 안내를 뺍니다(번역문). 둘은 반드시 함께 맞아야 합니다 - 한쪽만 되면
    /// "없는 버튼을 누르라고 말하는 화면" 또는 "Steam 버튼이 남은 STOVE 빌드"가 됩니다.
    /// 그래서 여기서 두 가지를 봅니다: 버튼을 끌 참조가 있는가, 본문에 상점 안내가 없는가.
    ///
    /// itch에는 같은 검사가 <b>일부러 없습니다.</b> 정식 출시가 Steam이라 itch 데모의 상점 버튼도
    /// Steam으로 보내기로 했고, 팝업의 분기가 전부 IsStove를 보므로 itch는 손대지 않아도 Steam
    /// 쪽으로 떨어집니다. 즉 itch에서는 기본값이 곧 정답이라 막을 것이 없습니다.
    /// itch를 별도 상점으로 안내하기로 방침이 바뀌면 그때 이 검사를 itch에도 넓히십시오.
    ///
    /// 프리팹 값과 번역문이라 디파인을 따라오지 않고, 잘못돼도 에러가 나지 않습니다.
    /// 유저가 바로 보는 종류의 사고라 빌드로 막습니다.
    /// </summary>
    private static void CheckStoveDemoBranding(BuildStore _store, BuildRelease _release, List<string> _errors)
    {
        if (BuildStore.Stove != _store) return;
        if (BuildRelease.Demo != _release) return;

        const string PREFAB_PATH = "Assets/Prefabs/UI/MenuPopup/Map/NewNav/HUD_PopupNav_DemoNotice.prefab";
        const string LOC_PATH = "Assets/Resources/Localization/DemoNoticeUI.json";

        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);

        if (null == _prefab)
        {
            _errors.Add($"데모 안내 팝업 프리팹을 찾지 못했습니다: {PREFAB_PATH}");
        }
        else
        {
            HUD_PopupNav_DemoNotice _notice = _prefab.GetComponentInChildren<HUD_PopupNav_DemoNotice>(true);

            if (null == _notice)
            {
                _errors.Add($"프리팹에서 HUD_PopupNav_DemoNotice 컴포넌트를 찾지 못했습니다: {PREFAB_PATH}");
            }
            else
            {
                SerializedObject _so = new SerializedObject(_notice);

                SerializedProperty _storeBtn = _so.FindProperty("steamWishlistBtn");

                // STOVE에서 상점 버튼을 없애는 일은 코드가 합니다(ApplyStoreBranding). 그 코드는
                // 이 참조를 통해서만 버튼에 닿으므로, 참조가 비면 끄지 못하고 조용히 지나갑니다.
                // 그러면 Steam 로고에 Steam URL이 그대로 붙은 버튼이 STOVE 유저에게 노출됩니다.
                if (null == _storeBtn || null == _storeBtn.objectReferenceValue)
                {
                    _errors.Add("데모 안내 팝업의 상점 버튼 참조(steamWishlistBtn)가 비어 있습니다. " +
                                "STOVE 빌드는 이 참조로 버튼을 끄므로, 비어 있으면 Steam 상점 버튼이 그대로 노출됩니다.");
                }
            }
        }

        TextAsset _loc = AssetDatabase.LoadAssetAtPath<TextAsset>(LOC_PATH);

        if (null == _loc)
        {
            _errors.Add($"데모 안내 번역 파일을 찾지 못했습니다: {LOC_PATH}");
            return;
        }

        string _stoveEntry = ExtractLocalizationEntry(_loc.text, 3);

        if (null == _stoveEntry)
        {
            _errors.Add("DemoNoticeUI.json 에 STOVE용 본문(entry id 3)이 없습니다. " +
                        "없으면 게임이 Steam 문구(\"Steam 찜하기\")를 그대로 보여줍니다.");
            return;
        }

        // 본문에서 상점 안내를 뺀 것은 상점 버튼이 없기 때문입니다. 나중에 누군가 Steam 본문을
        // 복사해 채우면 버튼은 없는데 찜하기를 누르라고 말하는 화면이 됩니다. 번역 파일이라
        // 컴파일도 테스트도 걸러주지 못하므로 여기서 봅니다.
        string[] _banned = { "steam", "wishlist", "찜하기", "愿望单", "願望單", "ウィッシュ" };

        for (int i = 0; i < _banned.Length; i++)
        {
            if (_stoveEntry.IndexOf(_banned[i], System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            _errors.Add($"DemoNoticeUI.json 의 STOVE 본문(entry id 3)에 \"{_banned[i]}\" 가 들어 있습니다. " +
                        "STOVE 데모에는 상점 버튼이 없으므로, 본편 상점·찜하기를 안내하면 안 됩니다.");
        }
    }

    /// <summary>
    /// 번역 JSON에서 해당 id의 엔트리 본문만 잘라냅니다. 다음 "id" 가 나오기 직전까지가 한 엔트리입니다.
    /// 없으면 null을 돌려줍니다.
    ///
    /// JsonUtility로 파싱하지 않는 이유는 이 검사가 <b>번역 파일이 깨졌을 때도</b> 돌아야 하기
    /// 때문입니다. 파싱이 실패하면 검사 자체가 사라져, 정작 막아야 할 상황에서 조용히 통과합니다.
    /// </summary>
    private static string ExtractLocalizationEntry(string _json, int _id)
    {
        if (true == string.IsNullOrEmpty(_json)) return null;

        int _start = _json.IndexOf($"\"id\": {_id}", System.StringComparison.Ordinal);

        if (_start < 0) _start = _json.IndexOf($"\"id\":{_id}", System.StringComparison.Ordinal);
        if (_start < 0) return null;

        int _next = _json.IndexOf("\"id\"", _start + 4, System.StringComparison.Ordinal);

        return (_next < 0) ? _json.Substring(_start) : _json.Substring(_start, _next - _start);
    }

    /// <summary>
    /// steam_appid.txt는 프로젝트 루트에 있어 배포물에 실리지 않습니다. 개발 실행에만 영향을 주므로
    /// 빌드를 막을 이유가 없어 경고만 남깁니다.
    /// </summary>
    private static void CheckSteamAppId(BuildStore _store, BuildRelease _release, List<string> _warnings)
    {
        string _expected = PlatformBuildModeSwitcher.ExpectedSteamAppId(_store, _release);

        if (null == _expected) return;

        string _path = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName, "steam_appid.txt");

        if (false == System.IO.File.Exists(_path)) return;

        string _actual;

        try { _actual = System.IO.File.ReadAllText(_path).Trim(); }
        catch (System.Exception) { return; }

        if (_actual == _expected) return;

        _warnings.Add($"steam_appid.txt 가 {_actual} 입니다. 이 구성의 기대값은 {_expected} 입니다. " +
                      "배포물에는 실리지 않으므로 빌드는 진행합니다. 에디터 실행 시 Steam이 다른 앱으로 인식합니다.");
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
