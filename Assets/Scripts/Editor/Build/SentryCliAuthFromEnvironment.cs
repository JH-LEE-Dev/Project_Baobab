using System;
using UnityEditor;
using UnityEngine;
using Sentry.Unity;
using Sentry.Unity.Editor;

/// <summary>
/// Sentry 심볼 업로드용 인증 토큰을 환경변수에서 읽어 빌드 시점에 주입합니다.
///
/// [왜 이렇게 하는가]
/// Sentry 설정 창(Tools > Sentry)에 토큰을 직접 입력하면 Assets/Plugins/Sentry/SentryCliOptions.asset에
/// 평문으로 저장됩니다. 이 파일은 git이 추적하고 있어서 다음 커밋에 토큰이 그대로 딸려 들어갑니다.
/// Sentry 문서도 이 파일을 공개하지 말라고 명시합니다.
///
/// 이 훅을 물려두면 토큰이 에셋에 남지 않습니다. 에셋에는 organization/project 같은 비밀이 아닌
/// 값만 남으므로 그대로 커밋해도 됩니다.
///
/// [쓰는 법]
///   1. Tools > Baobab > Sentry - 환경변수 토큰 연결   (에셋을 만들어 슬롯에 물려줍니다)
///   2. 시스템 환경변수 SENTRY_AUTH_TOKEN 에 조직 토큰(org:ci) 등록
///   3. Unity 재시작 (에디터가 켜질 때의 환경변수를 그대로 물고 갑니다)
///
/// [비어 있을 때]
/// 환경변수가 없으면 기존 값을 지우지 않고 그대로 둔 채 경고만 남깁니다. 실수로 설정을 날리는 것보다
/// 빌드가 업로드 단계에서 멈추고 이유를 알려주는 편이 낫기 때문입니다.
/// (Sentry 설정의 Ignore CLI Errors를 켜두면 조용히 넘어가므로, 꺼둔 채로 쓰시길 권합니다)
/// </summary>
[CreateAssetMenu(
    fileName = ASSET_FILE_NAME,
    menuName = "Baobab/Sentry CLI Auth From Environment")]
public class SentryCliAuthFromEnvironment : SentryCliOptionsConfiguration
{
    public const string ASSET_FILE_NAME = "SentryCliAuthFromEnvironment";

    /// <summary>sentry-cli가 쓰는 표준 이름이라 그대로 따릅니다.</summary>
    public const string AUTH_TOKEN_ENV = "SENTRY_AUTH_TOKEN";

    /// <summary>비밀이 아니지만, CI에서 조직·프로젝트를 갈아끼울 수 있게 열어둡니다.</summary>
    public const string ORGANIZATION_ENV = "SENTRY_ORG";
    public const string PROJECT_ENV = "SENTRY_PROJECT";

    public override void Configure(SentryCliOptions _options)
    {
        if (null == _options) return;

        string _token = Read(AUTH_TOKEN_ENV);

        if (true == string.IsNullOrEmpty(_token))
        {
            Debug.LogWarning(
                $"[Sentry] 환경변수 {AUTH_TOKEN_ENV}가 비어 있어 인증 토큰을 넣지 못했습니다.\n" +
                "심볼 업로드가 실패하면 IL2CPP 스택 트레이스에 줄 번호가 붙지 않습니다.\n" +
                $"setx {AUTH_TOKEN_ENV} \"...\" 로 등록하면 재시작 없이도 바로 잡힙니다.");
        }
        else
        {
            _options.Auth = _token;
        }

        // 아래 둘은 있을 때만 덮어쓴다. 없으면 설정 창에 입력해 둔 값을 그대로 쓴다.
        string _organization = Read(ORGANIZATION_ENV);
        if (false == string.IsNullOrEmpty(_organization)) _options.Organization = _organization;

        string _project = Read(PROJECT_ENV);
        if (false == string.IsNullOrEmpty(_project)) _options.Project = _project;
    }

    /// <summary>
    /// 환경변수를 읽습니다. 프로세스 환경을 먼저 보고, 비어 있으면 Windows에 한해 사용자/시스템
    /// 등록값을 직접 읽습니다.
    ///
    /// setx로 등록한 값은 이미 떠 있는 프로세스에는 반영되지 않습니다. 그런데 Unity는 Unity Hub의
    /// 자식으로 실행되고, Hub는 창을 닫아도 트레이에 남아 있어서, Unity만 재시작하면 몇 시간 전
    /// 환경을 그대로 물고 옵니다. "등록했는데 왜 안 보이지"의 대부분이 이 경우라 등록값을 직접
    /// 읽어 함정을 없앱니다.
    ///
    /// 프로세스 환경을 먼저 보는 이유는 CI 때문입니다. 빌드 스크립트가 셸에서 넘긴 값이 있으면
    /// 그쪽이 우선이어야 합니다.
    /// </summary>
    private static string Read(string _name)
    {
        string _value = ReadScoped(_name, EnvironmentVariableTarget.Process);

        if (false == string.IsNullOrEmpty(_value)) return _value;
        if (RuntimePlatform.WindowsEditor != Application.platform) return _value;

        _value = ReadScoped(_name, EnvironmentVariableTarget.User);

        if (false == string.IsNullOrEmpty(_value)) return _value;

        return ReadScoped(_name, EnvironmentVariableTarget.Machine);
    }

    private static string ReadScoped(string _name, EnvironmentVariableTarget _target)
    {
        try
        {
            string _value = Environment.GetEnvironmentVariable(_name, _target);
            return (null == _value) ? null : _value.Trim();
        }
        catch (Exception _e)
        {
            Debug.LogWarning($"[Sentry] 환경변수 {_name}({_target})을 읽지 못했습니다: {_e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 진단용. 값이 어느 범위에서 발견됐는지 이름만 돌려줍니다. 값 자체는 절대 노출하지 않습니다.
    /// 못 찾으면 null입니다.
    /// </summary>
    public static string DescribeSource(string _name)
    {
        if (false == string.IsNullOrEmpty(ReadScoped(_name, EnvironmentVariableTarget.Process))) return "프로세스 환경";

        if (RuntimePlatform.WindowsEditor == Application.platform)
        {
            if (false == string.IsNullOrEmpty(ReadScoped(_name, EnvironmentVariableTarget.User))) return "사용자 등록값";
            if (false == string.IsNullOrEmpty(ReadScoped(_name, EnvironmentVariableTarget.Machine))) return "시스템 등록값";
        }

        return null;
    }
}

/// <summary>
/// 위 훅을 만들어 Sentry 설정에 물려주는 메뉴입니다. 손으로 에셋을 만들고 슬롯을 찾아 끌어다 놓는
/// 과정을 없애, 연결을 빠뜨리는 실수를 막습니다.
/// </summary>
internal static class SentryCliAuthSetupMenu
{
    private const string MENU_LINK = "Tools/Baobab/Sentry - 환경변수 토큰 연결";
    private const string MENU_STATUS = "Tools/Baobab/Sentry - 심볼 업로드 설정 확인";

    private const string CLI_OPTIONS_PATH = "Assets/Plugins/Sentry/SentryCliOptions.asset";
    private const string CONFIG_ASSET_PATH =
        "Assets/Plugins/Sentry/" + SentryCliAuthFromEnvironment.ASSET_FILE_NAME + ".asset";

    [MenuItem(MENU_LINK, false, 1)]
    private static void Link()
    {
        SentryCliOptions _cliOptions = AssetDatabase.LoadAssetAtPath<SentryCliOptions>(CLI_OPTIONS_PATH);

        if (null == _cliOptions)
        {
            EditorUtility.DisplayDialog("Sentry",
                $"Sentry CLI 설정을 찾지 못했습니다:\n{CLI_OPTIONS_PATH}\n\n" +
                "먼저 Tools > Sentry 를 한 번 열어 설정을 생성하세요.", "확인");
            return;
        }

        SentryCliAuthFromEnvironment _config =
            AssetDatabase.LoadAssetAtPath<SentryCliAuthFromEnvironment>(CONFIG_ASSET_PATH);

        if (null == _config)
        {
            _config = ScriptableObject.CreateInstance<SentryCliAuthFromEnvironment>();
            AssetDatabase.CreateAsset(_config, CONFIG_ASSET_PATH);
        }

        _cliOptions.CliOptionsConfiguration = _config;

        EditorUtility.SetDirty(_cliOptions);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Sentry] 환경변수 토큰 훅을 연결했습니다: {CONFIG_ASSET_PATH}\n" +
                  $"이제 Tools > Sentry 의 Auth Token 칸은 비워두세요. 대신 환경변수 " +
                  $"{SentryCliAuthFromEnvironment.AUTH_TOKEN_ENV} 를 등록하고 Unity를 재시작하면 됩니다.");

        Selection.activeObject = _cliOptions;
    }

    [MenuItem(MENU_STATUS, false, 2)]
    private static void ShowStatus()
    {
        SentryCliOptions _cliOptions = AssetDatabase.LoadAssetAtPath<SentryCliOptions>(CLI_OPTIONS_PATH);

        if (null == _cliOptions)
        {
            EditorUtility.DisplayDialog("Sentry", $"Sentry CLI 설정을 찾지 못했습니다:\n{CLI_OPTIONS_PATH}", "확인");
            return;
        }

        string _tokenSource = SentryCliAuthFromEnvironment.DescribeSource(SentryCliAuthFromEnvironment.AUTH_TOKEN_ENV);
        bool _hasEnvToken = null != _tokenSource;
        bool _hasAssetToken = false == string.IsNullOrEmpty(_cliOptions.Auth);
        bool _linked = _cliOptions.CliOptionsConfiguration is SentryCliAuthFromEnvironment;

        // 토큰 값 자체는 절대 찍지 않는다. 어디서 찾았는지와 있는지 없는지만 본다.
        string _message =
            $"심볼 업로드     : {(_cliOptions.UploadSymbols ? "켜짐" : "꺼짐")}\n" +
            $"Organization    : {Show(_cliOptions.Organization)}\n" +
            $"Project         : {Show(_cliOptions.Project)}\n" +
            $"환경변수 훅     : {(_linked ? "연결됨" : "연결 안 됨")}\n" +
            $"{SentryCliAuthFromEnvironment.AUTH_TOKEN_ENV} : {(_hasEnvToken ? "설정됨 (" + _tokenSource + ")" : "없음")}\n" +
            $"에셋에 저장된 토큰 : {(_hasAssetToken ? "있음 ⚠" : "없음")}\n\n";

        if (true == _hasAssetToken)
        {
            _message += "⚠ SentryCliOptions.asset에 토큰이 저장돼 있습니다. 이 파일은 git이 추적하므로\n" +
                        "   Tools > Sentry 에서 Auth Token 칸을 비우세요.\n\n";
        }

        if (false == _linked) _message += "· 환경변수 훅이 연결되지 않았습니다. 위 메뉴로 연결하세요.\n";
        if (false == _hasEnvToken)
        {
            _message += $"· setx {SentryCliAuthFromEnvironment.AUTH_TOKEN_ENV} \"토큰\" 으로 등록하세요.\n" +
                        "   사용자 등록값을 직접 읽으므로 Unity 재시작은 필요 없습니다.\n";
        }

        EditorUtility.DisplayDialog("Sentry 심볼 업로드", _message, "확인");
    }

    private static string Show(string _value)
    {
        return string.IsNullOrEmpty(_value) ? "(비어 있음)" : _value;
    }
}
