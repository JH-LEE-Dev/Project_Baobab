using GameAnalyticsSDK;
using Sentry.Unity;
using UnityEngine;

/// <summary>
/// 데이터 수집 동의를 실제 SDK 동작으로 옮기는 유일한 지점입니다.
///
/// "동의했는지"는 SettingsData.dataConsent가 답하고, "동의했다는 것이 무슨 뜻인지"는 여기서만
/// 답합니다. Sentry나 GameAnalytics를 켜고 끄는 코드를 다른 곳에 만들면 두 정책이 갈라져서,
/// 한쪽만 고친 채 출시되는 사고가 납니다. (실제로 예전에는 BootStrap의 인스펙터 토글이
/// 동의와 무관하게 SDK를 켜고 있었고, 팝업의 동의 결과는 아무 데도 전달되지 않았습니다)
///
/// == 적용 시점에 대한 규칙 ==
/// 철회(동의 -> 거부)는 두 SDK 모두 즉시 반영됩니다. 유저가 "지금 그만 보내라"고 말한 것이므로
/// 다음 실행까지 미룰 수 없습니다.
///
/// 승인(거부 -> 동의)도 즉시 반영을 시도하지만, Sentry의 네이티브 크래시 핸들러만은 예외입니다.
/// 그 핸들러는 SubsystemRegistration(첫 씬보다도 먼저)에서 설치되므로, 실행 중에 켠 Sentry는
/// 매니지드 예외까지만 확실히 잡습니다. 완전한 크래시 수집은 다음 실행부터입니다.
/// (TryInitSentryMidSession 주석 참고)
///
/// 참고: Sentry는 SentryOptions.asset을 근거로 스스로 초기화되며, 동의하지 않은 유저에게서
/// 아예 시작조차 하지 않게 만드는 것은 SentryConsentOptionsConfiguration의 몫입니다.
/// 이 클래스는 그보다 늦게(BootStrap.Awake) 시작하므로 "시작을 막는" 역할은 할 수 없습니다.
/// </summary>
public static class DataConsentGate
{
    /// <summary>
    /// 빌드 설정(BootStrap의 인스펙터 토글)상 Sentry를 쓸 수 있는지입니다.
    /// 동의와는 별개의 스위치이며, 둘 다 참일 때만 실제로 동작합니다.
    /// </summary>
    private static bool isSentryAllowedByBuild = true;

    private static bool isGameAnalyticsAllowedByBuild = true;

    /// <summary>
    /// GameAnalytics.Initialize()를 이 실행에서 이미 호출했는지입니다.
    ///
    /// GameAnalytics.Initialized를 그대로 쓰지 않는 이유는, 그 플래그가 지원되지 않는
    /// 플랫폼에서도 true가 되기 때문입니다(Initialize의 platformIndex < 0 분기). 여기서
    /// 알아야 하는 것은 "SDK가 살아 있는가"가 아니라 "내가 Initialize를 불렀는가"입니다.
    /// </summary>
    private static bool hasInitializedGameAnalytics = false;

    private static bool isSubscribed = false;

    /// <summary>
    /// 부팅 시 한 번 호출합니다. 저장된 동의 상태를 SDK에 반영하고,
    /// 이후 동의가 바뀌면 자동으로 따라가도록 구독을 걸어둡니다.
    /// </summary>
    public static void ApplyAtStartup(bool _isSentryAllowedByBuild, bool _isGameAnalyticsAllowedByBuild)
    {
        isSentryAllowedByBuild = _isSentryAllowedByBuild;
        isGameAnalyticsAllowedByBuild = _isGameAnalyticsAllowedByBuild;

        EDataConsent _consent = SettingsManager.ReadPersistedConsent();
        bool _isGranted = (EDataConsent.Granted == _consent);

        Debug.Log($"[DataConsentGate] 부팅 시 데이터 수집 동의 상태 = {_consent} " +
            $"(빌드 토글: Sentry={_isSentryAllowedByBuild}, GameAnalytics={_isGameAnalyticsAllowedByBuild})");

        ApplySentry(_isGranted, true);
        ApplyGameAnalytics(_isGranted);

        Subscribe();
    }

    /// <summary>
    /// SettingsManager의 동의 변경 이벤트를 구독합니다.
    /// SettingsManager는 DontDestroyOnLoad 싱글턴이라 한 번만 걸면 됩니다.
    /// </summary>
    private static void Subscribe()
    {
        if (true == isSubscribed) return;

        // 여기서 Instance 게터를 써도 안전하다. ApplyAtStartup은 첫 씬의 Awake 체인에서
        // 호출되므로 씬이 이미 존재하고, DontDestroyOnLoad 보호가 성립한다.
        SettingsManager.Instance.OnDataConsentChangedEvent -= OnConsentChanged;
        SettingsManager.Instance.OnDataConsentChangedEvent += OnConsentChanged;
        isSubscribed = true;
    }

    private static void OnConsentChanged(EDataConsent _consent)
    {
        bool _isGranted = (EDataConsent.Granted == _consent);

        Debug.Log($"[DataConsentGate] 데이터 수집 동의가 {_consent}(으)로 변경되었습니다.");

        ApplySentry(_isGranted, false);
        ApplyGameAnalytics(_isGranted);
    }

    /// <param name="_isStartup">
    /// 부팅 경로에서의 호출인지입니다. 부팅 시에는 Sentry가 이미 자기 초기화를 마친 뒤이므로
    /// 여기서 켜려고 시도하지 않습니다. (에디터처럼 애초에 켜지지 않는 환경에서 매 실행마다
    /// 의미 없는 경고를 남기지 않기 위한 구분이기도 합니다)
    /// </param>
    private static void ApplySentry(bool _isGranted, bool _isStartup)
    {
        bool _shouldRun = (true == _isGranted && true == isSentryAllowedByBuild);

        if (false == _shouldRun)
        {
            // 이미 꺼져 있어도 Close는 무해하다. 동의 없이 초기화된 경로가 어딘가에 남아 있더라도
            // 여기서 확실히 끊기도록 조건 없이 부른다.
            SentrySdk.Close();
            return;
        }

        // 부팅 경로에서는 SentryConsentOptionsConfiguration이 이미 판단을 끝냈다.
        if (true == _isStartup) return;

        // 이미 켜져 있으면(부팅 때 동의 상태였다가 껐다 다시 켠 것이 아니라면) 할 일이 없다.
        if (true == SentrySdk.IsEnabled) return;

        TryInitSentryMidSession();
    }

    /// <summary>
    /// 실행 중에 동의로 바뀐 경우 Sentry를 지금 켭니다.
    ///
    /// 이 경로는 "최선의 노력"이며, 부팅 시 초기화와 동등하다고 가정하면 안 됩니다.
    /// SentryInitialization이 SubsystemRegistration에서 SentryPlatformServices를 이미 세팅해
    /// 두었으므로 여기서 Init을 부르면 네이티브 백엔드까지 구성될 여지가 있지만, 그 시점을
    /// 지나서 붙이는 크래시 핸들러가 모든 플랫폼에서 온전하다고 보증할 수는 없습니다.
    /// 확실한 것은 매니지드 예외 수집이 이 순간부터 동작한다는 것과, 다음 실행부터는
    /// 정상 경로로 완전히 켜진다는 것뿐입니다.
    ///
    /// 그럼에도 아무것도 하지 않는 대신 시도하는 이유는, 방금 "동의"를 누른 유저가 그 세션
    /// 내내 아무것도 보고되지 않는 상태로 남는 것보다는 낫기 때문입니다.
    /// </summary>
    private static void TryInitSentryMidSession()
    {
        SentryUnityOptions _options = ScriptableSentryUnityOptions.LoadSentryUnityOptions();

        // LoadSentryUnityOptions는 SentryConsentOptionsConfiguration.Configure를 다시 부른다.
        // 동의는 이 함수에 오기 전에 이미 파일에 기록되었으므로 Enabled로 돌아온다.
        // ShouldInitializeSdk는 에디터 여부(CaptureInEditor)와 DSN 유무까지 함께 판단하므로,
        // 그 판단을 우회하지 않고 그대로 따른다.
        if (null == _options || false == _options.ShouldInitializeSdk())
        {
            Debug.Log("[DataConsentGate] 현재 설정으로는 Sentry를 지금 켤 수 없습니다. " +
                "크래시 수집은 다음 실행부터 시작됩니다.");
            return;
        }

        SentrySdk.Init(_options);
        Debug.Log("[DataConsentGate] Sentry를 실행 중에 활성화했습니다. " +
            "네이티브 크래시 핸들러까지 온전히 붙는 것은 다음 실행부터입니다.");

        // 새로 연 스코프에는 유저 태그가 없으므로 여기서 다시 붙인다.
        SentryUserContextTagger.TagCurrentUser();
    }

    private static void ApplyGameAnalytics(bool _isGranted)
    {
        bool _shouldRun = (true == _isGranted && true == isGameAnalyticsAllowedByBuild);

        if (false == _shouldRun)
        {
            // Initialize를 부른 적이 없으면 끌 것도 없다. 초기화 전에 SetEnabledEventSubmission을
            // 부르면 네이티브 쪽 상태가 아직 없어 설정이 그대로 유실될 수 있으므로 건드리지 않는다.
            if (false == hasInitializedGameAnalytics) return;

            // GameAnalytics의 GDPR 권장 경로다. 세션은 건드리지 않는다 - EndSession은 수동 세션
            // 관리 모드를 전제로 한 API라, 자동 세션 모드에서 부르면 SDK 내부 상태가 어긋난다.
            GameAnalytics.SetEnabledEventSubmission(false);
            Debug.Log("[DataConsentGate] GameAnalytics 이벤트 전송을 중단했습니다.");
            return;
        }

        if (false == hasInitializedGameAnalytics)
        {
            GameAnalytics.Initialize();
            hasInitializedGameAnalytics = true;
            Debug.Log("[DataConsentGate] GameAnalytics를 초기화했습니다.");
            return;
        }

        // 실행 중에 껐다가 다시 켠 경우. Initialize를 두 번 부르면 SDK가 세션을 다시 열어
        // 통계가 어긋나므로, 전송 스위치만 되돌린다.
        GameAnalytics.SetEnabledEventSubmission(true);
        Debug.Log("[DataConsentGate] GameAnalytics 이벤트 전송을 재개했습니다.");
    }
}
