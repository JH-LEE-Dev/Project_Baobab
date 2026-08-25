using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// 데모 빌드 ↔ 정식 빌드를 한 번에 전환합니다.
///
/// 이 프로젝트의 데모/정식 구분은 BAOBAB_FULL_RELEASE 디파인 하나로 결정되고,
/// 거기에 세 가지가 딸려 있습니다.
///   1. 세이브 변형   (BuildInfo.Variant — 데모↔정식 세이브는 서로 호환되지 않음)
///   2. Steam 앱 ID   (BuildInfo.SteamAppId — 데모와 정식은 서로 다른 앱)
///   3. 데모 맵 제한  (HUD_PopupNav_Main)
///
/// 1·2·3은 전부 디파인에서 파생되므로 자동으로 맞습니다. 문제는 네 번째입니다.
///   4. steam_appid.txt — 코드가 아니라 파일이라 디파인을 따라오지 않는다
///
/// 4번이 어긋나면 개발 중 Steam이 엉뚱한 앱으로 인식합니다. 이 메뉴가 디파인과 파일을
/// 함께 바꿔서 넷이 항상 일치하게 만듭니다.
///
/// 극성 주의: **디파인이 없는 상태가 데모**입니다. 정식 빌드일 때만 디파인을 켭니다.
/// (BuildInfo 주석 참고 — 실수의 방향을 안전한 쪽으로 몰아둔 의도적인 설계입니다)
/// </summary>
public static class SteamBuildModeSwitcher
{
    private const string FULL_RELEASE_DEFINE = "BAOBAB_FULL_RELEASE";

    private const string MENU_DEMO = "Tools/Steam/빌드 모드 - 데모 (LumberBoy Demo)";
    private const string MENU_FULL = "Tools/Steam/빌드 모드 - 정식 (LumberBoy)";
    private const string MENU_SHOW = "Tools/Steam/현재 빌드 모드 확인";
    private const string MENU_SPACEWAR = "Tools/Steam/steam_appid.txt 를 480(Spacewar)으로";

    /// <summary>
    /// Valve의 공개 테스트 앱. 모든 Steam 계정이 자동으로 소유하므로 SteamAPI.Init()이 항상 성공합니다.
    ///
    /// 실제 앱 ID는 그 앱의 라이선스가 계정에 붙어 있어야(라이브러리에 보여야) Init이 됩니다.
    /// 신규 앱은 기본 패키지 설정 전까지 그렇지 않아서 에디터에서 계속 초기화 실패 로그가 뜹니다.
    /// 게임 동작에는 지장이 없지만(클라우드만 건너뜀) 로그가 시끄러워 진짜 오류를 놓치기 쉬우므로,
    /// 앱 설정이 끝나기 전까지는 이 값을 씁니다.
    /// </summary>
    private const uint STEAM_APP_ID_SPACEWAR = 480;

    [MenuItem(MENU_DEMO, false, 1)]
    private static void SwitchToDemo()
    {
        Apply(false);
    }

    [MenuItem(MENU_FULL, false, 2)]
    private static void SwitchToFull()
    {
        Apply(true);
    }

    [MenuItem(MENU_DEMO, true)]
    private static bool ValidateDemo()
    {
        Menu.SetChecked(MENU_DEMO, false == HasFullReleaseDefine());
        return true;
    }

    [MenuItem(MENU_FULL, true)]
    private static bool ValidateFull()
    {
        Menu.SetChecked(MENU_FULL, true == HasFullReleaseDefine());
        return true;
    }

    [MenuItem(MENU_SPACEWAR, false, 3)]
    private static void SwitchAppIdToSpacewar()
    {
        WriteAppIdFile(STEAM_APP_ID_SPACEWAR);

        Debug.Log("[SteamBuildMode] steam_appid.txt = 480 (Spacewar). 에디터에서 SteamAPI 초기화 실패 로그가 사라집니다.\n" +
                  "Steam 클라우드·실적은 실제 앱 대상으로 시험할 수 없습니다. 앱 설정이 끝나면 원래 모드로 되돌리세요.");
    }

    [MenuItem(MENU_SPACEWAR, true)]
    private static bool ValidateSpacewar()
    {
        Menu.SetChecked(MENU_SPACEWAR, ReadAppIdFile() == STEAM_APP_ID_SPACEWAR.ToString());
        return true;
    }

    [MenuItem(MENU_SHOW, false, 20)]
    private static void ShowCurrent()
    {
        bool _isFull = HasFullReleaseDefine();
        uint _expected = _isFull ? BuildInfo.STEAM_APP_ID_RELEASE : BuildInfo.STEAM_APP_ID_DEMO;
        string _fileId = ReadAppIdFile();

        bool _isSpacewar = _fileId == STEAM_APP_ID_SPACEWAR.ToString();
        bool _match = _fileId == _expected.ToString();

        string _verdict;

        if (true == _match)
        {
            _verdict = "일치합니다.";
        }
        else if (true == _isSpacewar)
        {
            // 이건 실수가 아니라 의도된 개발 설정일 수 있으므로 경고로 다루지 않는다.
            _verdict = "개발용 테스트 앱(Spacewar)으로 설정되어 있습니다.\n" +
                       "에디터 초기화 실패 로그를 없애기 위한 임시 설정이며,\n" +
                       "배포 빌드를 만들기 전에 원래 모드로 되돌리세요.";
        }
        else
        {
            _verdict = "⚠ steam_appid.txt가 어긋나 있습니다.\n메뉴에서 모드를 다시 선택해 맞추세요.";
        }

        string _message =
            $"모드          : {(_isFull ? "정식" : "데모")}\n" +
            $"디파인        : {(_isFull ? FULL_RELEASE_DEFINE : "없음")}\n" +
            $"세이브 변형    : {BuildInfo.Variant}\n" +
            $"기대 앱 ID     : {_expected}\n" +
            $"steam_appid   : {_fileId}\n\n" + _verdict;

        EditorUtility.DisplayDialog("Steam 빌드 모드", _message, "확인");

        if (false == _match && false == _isSpacewar)
        {
            Debug.LogWarning($"[SteamBuildMode] 앱 ID 불일치: 기대={_expected}, steam_appid.txt={_fileId}");
        }
    }

    private static void Apply(bool _isFullRelease)
    {
        NamedBuildTarget _target = NamedBuildTarget.FromBuildTargetGroup(
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        PlayerSettings.GetScriptingDefineSymbols(_target, out string[] _defines);

        // BAOBAB_FULL_RELEASE만 넣거나 뺀다. 다른 디파인(DOTWEEN, STEAMWORKS_NET 등)은 건드리지 않는다.
        List<string> _list = new List<string>(_defines.Length + 1);

        for (int i = 0; i < _defines.Length; i++)
        {
            if (_defines[i] == FULL_RELEASE_DEFINE) continue;
            _list.Add(_defines[i]);
        }

        if (true == _isFullRelease) _list.Add(FULL_RELEASE_DEFINE);

        PlayerSettings.SetScriptingDefineSymbols(_target, _list.ToArray());

        uint _appId = _isFullRelease ? BuildInfo.STEAM_APP_ID_RELEASE : BuildInfo.STEAM_APP_ID_DEMO;
        WriteAppIdFile(_appId);

        Debug.Log($"[SteamBuildMode] {(_isFullRelease ? "정식" : "데모")} 모드로 전환. 앱 ID={_appId} / 대상={_target.TargetName}\n" +
                  "스크립트 재컴파일 후 적용됩니다.\n" +
                  "주의: 데모↔정식 세이브는 서로 호환되지 않아, 전환 후에는 기존 세이브를 읽지 못합니다.");
    }

    private static bool HasFullReleaseDefine()
    {
        NamedBuildTarget _target = NamedBuildTarget.FromBuildTargetGroup(
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        PlayerSettings.GetScriptingDefineSymbols(_target, out string[] _defines);

        for (int i = 0; i < _defines.Length; i++)
        {
            if (_defines[i] == FULL_RELEASE_DEFINE) return true;
        }

        return false;
    }

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

    private static void WriteAppIdFile(uint _appId)
    {
        try
        {
            // 개행 없이 숫자만 있어야 한다. Steam이 파일 내용을 그대로 파싱한다.
            File.WriteAllText(AppIdFilePath, _appId.ToString());
        }
        catch (System.Exception _e)
        {
            Debug.LogError("[SteamBuildMode] steam_appid.txt 기록 실패: " + _e.Message);
        }
    }
}
