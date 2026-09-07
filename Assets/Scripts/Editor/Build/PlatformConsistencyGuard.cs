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
///   · 세이브 폴더  — STOVE에서 디파인을 깜빡 → 런처가 빈 폴더를 동기화. 클라우드만 조용히 안 됨
///   · Steam DRM   — STOVE에서 디파인을 깜빡 → RestartAppIfNecessary가 살아남아
///                    <b>Steam을 켜둔 채 이 게임을 Steam에서 사지 않은 유저가 실행조차 못 함</b>
///
/// 둘이 같은 디파인에 매달려 있고 한쪽 대가가 치명적이라, 사람 기억이 아니라 빌드 중단으로 막습니다.
/// ReleaseScriptingBackendGuard가 Mono 배포를 막는 것과 같은 이유이고 같은 패턴입니다.
///
/// [무엇을 보는가]
/// 어긋나도 <b>아무 에러가 나지 않는</b> 것들만 봅니다. 컴파일이 깨지는 종류는 어차피 빌드가 안 됩니다.
///   1. BAOBAB_STOVE 와 DISABLESTEAMWORKS 의 짝
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
    /// BAOBAB_STOVE 와 DISABLESTEAMWORKS 는 반드시 함께 켜지고 함께 꺼져야 합니다.
    /// 한쪽만 있으면 어느 방향이든 조용히 망가집니다.
    /// </summary>
    private static void CheckDefinePairing(BuildStore _store, List<string> _errors)
    {
        bool _hasDisable = PlatformBuildModeSwitcher.HasDisableSteamworksDefine;

        if (BuildStore.Stove == _store && false == _hasDisable)
        {
            _errors.Add("STOVE 빌드인데 DISABLESTEAMWORKS 가 없습니다. " +
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
        bool _settingsSaysFull = BuildRelease.Full == _release;
        bool _settingsSaysDisable = PlatformBuildModeSwitcher.HasDisableSteamworksDefine;

        if (_settingsSaysStove != COMPILED_AS_STOVE
            || _settingsSaysFull != COMPILED_AS_FULL_RELEASE
            || _settingsSaysDisable != COMPILED_WITHOUT_STEAMWORKS)
        {
            _errors.Add("설정은 바뀌었지만 스크립트가 아직 그 설정으로 컴파일되지 않았습니다. " +
                        "메뉴를 누른 직후 바로 빌드한 경우입니다. " +
                        "에디터가 재컴파일을 끝낼 때까지 기다렸다가 다시 빌드하십시오. " +
                        $"(설정: STOVE={_settingsSaysStove}, 정식={_settingsSaysFull}, DISABLESTEAMWORKS={_settingsSaysDisable} / " +
                        $"컴파일됨: STOVE={COMPILED_AS_STOVE}, 정식={COMPILED_AS_FULL_RELEASE}, DISABLESTEAMWORKS={COMPILED_WITHOUT_STEAMWORKS})");
        }
    }

    /// <summary>
    /// 둘 다 Resources 아래라 빌드에 무조건 실립니다. 어긋나면 STOVE 크래시와 지표가
    /// Steam 데이터에 섞여 나중에 구분할 수 없게 됩니다.
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
