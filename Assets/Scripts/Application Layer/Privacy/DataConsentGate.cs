using System;
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
    /// 이 실행에서 Sentry 호출이 예외를 던진 적이 있는지입니다.
    ///
    /// GameAnalytics 쪽의 hasGameAnalyticsFailed와 같은 역할입니다.
    /// 참이 되면 실행 중에 다시 켜려 하지 않습니다(TryCallSdk 주석 참고).
    ///
    /// 단 SentrySdk.Close() 실패는 여기에 반영하지 않습니다. 그 호출은 동의 전 유저의 부팅
    /// 경로에서도 불리므로, 거기서 한 번 실패했다고 직후에 동의한 유저의 크래시 수집까지
    /// 막아서는 안 됩니다. (ApplySentry 주석 참고)
    /// </summary>
    private static bool hasSentryFailed = false;

    /// <summary>
    /// GameAnalytics.Initialize()를 이 실행에서 이미 호출했는지입니다.
    ///
    /// GameAnalytics.Initialized를 그대로 쓰지 않는 이유는, 그 플래그가 지원되지 않는
    /// 플랫폼에서도 true가 되기 때문입니다(Initialize의 platformIndex < 0 분기). 여기서
    /// 알아야 하는 것은 "SDK가 살아 있는가"가 아니라 "내가 Initialize를 불렀는가"입니다.
    /// </summary>
    private static bool hasInitializedGameAnalytics = false;

    /// <summary>
    /// 이 실행에서 GameAnalytics 호출이 예외를 던진 적이 있는지입니다.
    ///
    /// 참이 되면 통계 수집을 다시 켜지 않습니다(TryCallSdk 주석 참고).
    /// 빌드 토글(isGameAnalyticsAllowedByBuild)과 구분해 두는 이유는, 그쪽은 "쓰기로 했는가"라는
    /// 설정이고 이쪽은 "쓸 수 있는 상태인가"라는 이번 실행의 사실이기 때문입니다. 한 변수에
    /// 몰아넣으면 다음에 로그를 읽는 사람이 둘 중 어느 이유로 꺼졌는지 알 수 없게 됩니다.
    /// </summary>
    private static bool hasGameAnalyticsFailed = false;

    private static bool isSubscribed = false;

    /// <summary>
    /// 부팅 시 한 번 호출합니다. 저장된 동의 상태를 SDK에 반영하고,
    /// 이후 동의가 바뀌면 자동으로 따라가도록 구독을 걸어둡니다.
    /// </summary>
    public static void ApplyAtStartup(bool _isSentryAllowedByBuild, bool _isGameAnalyticsAllowedByBuild)
    {
        isSentryAllowedByBuild = _isSentryAllowedByBuild;
        isGameAnalyticsAllowedByBuild = _isGameAnalyticsAllowedByBuild;

#if UNITY_EDITOR
        // 에디터 플레이는 GameAnalytics에 보내지 않습니다.
        //
        // GameAnalytics의 build 태그는 GameAnalytics.SettingsGA(= Assets/Resources 아래 에셋)에서
        // 읽습니다. Tools > 빌드 메뉴가 그 값을 "1.0.0-steam"처럼 배포용으로 바꿔놓으므로,
        // 정식 모드로 전환해 검토하는 동안의 세션이 그 배포 빌드의 지표로 집계됩니다.
        //
        // Sentry처럼 "환경만 갈라두기"를 할 수 없습니다. 런타임에 build를 바꾸는 유일한 API인
        // GameAnalytics.SetBuildAllPlatforms()가 그 <b>에셋 자체</b>를 고치기 때문입니다.
        // 에디터에서 부르면 값이 에셋에 남아, 다음 빌드 때 PlatformConsistencyGuard가 어긋난
        // 값을 보고 빌드를 중단시킵니다. 고치려던 것보다 나쁜 결과라 그 길은 쓰지 않습니다.
        //
        // 그래서 여기서는 아예 켜지 않습니다. Sentry와 달리 잃는 것도 적습니다 - 이쪽은
        // 크래시 처리 같은 게임 동작이 아니라 집계 파이프라인이라, 에디터에서 켜둔다고
        // 확인되는 것이 없습니다. 동의 흐름 자체는 아래 로그로 그대로 따라갈 수 있습니다.
        if (true == isGameAnalyticsAllowedByBuild)
        {
            isGameAnalyticsAllowedByBuild = false;
            Debug.Log("[DataConsentGate] 에디터 플레이라 GameAnalytics를 켜지 않습니다. " +
                "(배포 빌드의 지표에 섞이지 않게 하기 위함이며, 빌드에서는 정상 동작합니다)");
        }
#endif

        EDataConsent _consent = SettingsManager.ReadPersistedConsent();
        bool _isGranted = (EDataConsent.Granted == _consent);

        Debug.Log($"[DataConsentGate] 부팅 시 데이터 수집 동의 상태 = {_consent} " +
            $"(빌드 토글: Sentry={isSentryAllowedByBuild}, GameAnalytics={isGameAnalyticsAllowedByBuild})");

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
        bool _shouldRun = (true == _isGranted && true == isSentryAllowedByBuild
            && false == hasSentryFailed);

        if (false == _shouldRun)
        {
            // 이미 꺼져 있어도 Close는 무해하다. 동의 없이 초기화된 경로가 어딘가에 남아 있더라도
            // 여기서 확실히 끊기도록 조건 없이 부른다.
            //
            // 이 호출만은 앞서 실패한 적이 있어도 매번 다시 시도한다. GameAnalytics의 전송 중단과
            // 같은 이유다 - "그만 보내라"는 요구를 SDK에 전달할 유일한 수단이기 때문이다.
            //
            // 나아가 실패해도 "이번 실행에서 Sentry를 포기"로 치지 않는다(_marksSdkUnusable: false).
            // 여기는 아직 동의하지 않은 모든 유저의 부팅 경로이기도 해서, 그 순간의 실패로 플래그를
            // 세우면 몇 초 뒤 팝업에서 "동의"를 누른 유저가 그 세션 내내 크래시 수집 없이 남는다.
            // 첫 실행 -> 동의는 이 게임에서 가장 흔한 흐름이라 그 대가가 너무 크다.
            if (false == TryCallSentry(() => SentrySdk.Close(), "종료", _marksSdkUnusable: false))
            {
                // 이 실패만은 결과를 따로 적어 둔다. 크래시 리포트가 계속 나갈 수 있다는 뜻인데,
                // 공통 경고문만 보면 그 사실이 드러나지 않는다.
                Debug.LogWarning("[DataConsentGate] Sentry를 닫지 못했습니다. 이번 실행 동안 크래시 " +
                    "리포트가 계속 나갈 수 있습니다. 다음 동의 변경 때 다시 시도합니다.");
            }

            return;
        }

        // 부팅 경로에서는 SentryConsentOptionsConfiguration이 이미 판단을 끝냈다.
        if (true == _isStartup) return;

        TryCallSentry(() => TryInitSentryMidSession(), "실행 중 활성화");
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
        // 이미 켜져 있으면(부팅 때 동의 상태였다가 껐다 다시 켠 것이 아니라면) 할 일이 없다.
        // SDK를 건드리는 호출이라 예외 보호 안쪽에 둔다.
        if (true == SentrySdk.IsEnabled) return;

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
        bool _shouldRun = (true == _isGranted && true == isGameAnalyticsAllowedByBuild
            && false == hasGameAnalyticsFailed);

        if (false == _shouldRun)
        {
            // Initialize를 부른 적이 없으면 끌 것도 없다. 초기화 전에 SetEnabledEventSubmission을
            // 부르면 네이티브 쪽 상태가 아직 없어 설정이 그대로 유실될 수 있으므로 건드리지 않는다.
            if (false == hasInitializedGameAnalytics) return;

            // GameAnalytics의 GDPR 권장 경로다. 세션은 건드리지 않는다 - EndSession은 수동 세션
            // 관리 모드를 전제로 한 API라, 자동 세션 모드에서 부르면 SDK 내부 상태가 어긋난다.
            //
            // 이 호출만은 앞서 실패한 적이 있어도 매번 다시 시도한다. "그만 보내라"는 유저의 요구를
            // SDK에 전달하는 유일한 수단이라, 한 번 실패했다고 다음 철회까지 포기할 수는 없다.
            if (false == TryCallGameAnalytics(() => GameAnalytics.SetEnabledEventSubmission(false),
                "이벤트 전송 중단"))
            {
                // Sentry의 Close 실패와 같은 이유로 결과를 따로 적어 둔다.
                Debug.LogWarning("[DataConsentGate] GameAnalytics 전송을 멈추지 못했습니다. 이번 실행 " +
                    "동안 이벤트가 계속 나갈 수 있습니다. 다음 동의 변경 때 다시 시도합니다.");
                return;
            }

            // 동의는 했는데 여기까지 왔다면 동의가 아니라 SDK 쪽 사정으로 끄는 것이다.
            // 로그만 보고 "동의를 철회했구나"로 잘못 읽히지 않도록 문장을 갈라둔다.
            Debug.Log(true == _isGranted
                ? "[DataConsentGate] 동의와 무관하게 GameAnalytics를 쓸 수 없는 상태라 " +
                  "이벤트 전송을 중단했습니다."
                : "[DataConsentGate] GameAnalytics 이벤트 전송을 중단했습니다.");
            return;
        }

        if (false == hasInitializedGameAnalytics)
        {
            if (false == TryCallGameAnalytics(() => GameAnalytics.Initialize(), "초기화")) return;

            hasInitializedGameAnalytics = true;
            Debug.Log("[DataConsentGate] GameAnalytics를 초기화했습니다.");
            return;
        }

        // 실행 중에 껐다가 다시 켠 경우. Initialize를 두 번 부르면 SDK가 세션을 다시 열어
        // 통계가 어긋나므로, 전송 스위치만 되돌린다.
        if (false == TryCallGameAnalytics(() => GameAnalytics.SetEnabledEventSubmission(true),
            "이벤트 전송 재개")) return;

        Debug.Log("[DataConsentGate] GameAnalytics 이벤트 전송을 재개했습니다.");
    }

    private static bool TryCallGameAnalytics(Action _call, string _what)
    {
        return TryCallSdk(_call, "GameAnalytics", _what, ref hasGameAnalyticsFailed, true);
    }

    /// <summary>
    /// Sentry 호출을 예외 보호 안에서 실행합니다. (TryCallSdk 주석 참고)
    /// </summary>
    /// <param name="_marksSdkUnusable">
    /// 실패했을 때 "이번 실행에서는 Sentry를 쓸 수 없다"고 볼지입니다.
    /// SentrySdk.Close()처럼 동의하지 않은 유저의 부팅 경로에서도 불리는 호출은 false로 넘깁니다.
    /// (ApplySentry의 주석 참고)
    /// </param>
    private static bool TryCallSentry(Action _call, string _what, bool _marksSdkUnusable = true)
    {
        return TryCallSdk(_call, "Sentry", _what, ref hasSentryFailed, _marksSdkUnusable);
    }

    /// <summary>
    /// 데이터 수집 SDK 호출을 감싸, 예외가 이 클래스 밖으로 새어 나가지 않게 합니다.
    ///
    /// 이 클래스로 들어오는 길은 두 개뿐이고 둘 다 뒤에 중요한 일이 줄줄이 붙어 있습니다.
    ///   - BootStrap.Awake: 이 호출 아래에서 씬/입력/세이브 매니저와 로컬라이제이션이 초기화됩니다.
    ///     여기서 예외가 올라오면 게임이 통째로 서지 않습니다.
    ///   - 설정 화면의 동의 변경 이벤트: 예외가 나가면 같은 이벤트를 듣는 다른 구독자에게
    ///     동의 변경이 전달되지 않습니다.
    /// 어느 쪽이든 "크래시 리포트"나 "플레이 통계"가 망가뜨릴 자격이 있는 범위를 한참 넘습니다.
    ///
    /// 실제로 GameAnalytics의 윈도우 네이티브 계층은 유저 이름이 비ASCII인 PC(예: C:\Users\병훈\...)
    /// 에서 저장 폴더 생성에 실패하는 것이 확인되었습니다. 그 실패는 GA 자신의 워커 스레드에서
    /// 로그로 끝났지만, 같은 종류의 문제가 매니지드 쪽으로 올라오지 않는다는 보장은 없습니다.
    /// Sentry 쪽도 실행 중 활성화 경로에서 에셋을 읽고 네이티브 계층을 건드리므로 사정이 같습니다.
    ///
    /// _marksSdkUnusable이 참인 호출이 한 번 실패하면 이번 실행에서는 그 SDK를 다시 켜지 않습니다.
    /// 상태를 알 수 없게 된 SDK를 계속 켜려 드는 것보다, 수집을 잃고 게임을 정상으로 두는 편이
    /// 낫습니다. 다음 실행은 평소대로 다시 시도합니다.
    ///
    /// 끄는 호출(GameAnalytics 전송 중단 / SentrySdk.Close)은 실패한 뒤에도 매번 다시 시도합니다.
    /// 그중 SentrySdk.Close는 플래그도 세우지 않습니다 - 동의 전 유저의 부팅 경로에서도 불리는
    /// 호출이라, 거기서의 실패를 "이번 실행은 포기"로 읽으면 직후에 동의한 유저까지 말려듭니다.
    /// (ApplyGameAnalytics, ApplySentry 참고)
    ///
    /// LogError가 아니라 LogWarning으로 남깁니다. 이 실패는 사람이 손댈 것이 없는 종류이고,
    /// 에러로 남기면 Sentry 이벤트와 GameAnalytics 자체 에러 이벤트를 또 만들어냅니다.
    /// </summary>
    /// <param name="_hasFailed">해당 SDK의 "이번 실행에서 실패했다" 플래그입니다.</param>
    /// <param name="_marksSdkUnusable">거짓이면 실패해도 _hasFailed를 건드리지 않습니다.</param>
    /// <returns>호출이 예외 없이 끝났으면 true.</returns>
    private static bool TryCallSdk(Action _call, string _sdkName, string _what, ref bool _hasFailed,
        bool _marksSdkUnusable)
    {
        try
        {
            _call();
            return true;
        }
        catch (Exception _exception)
        {
            // 포기하지 않는 호출에까지 "다시 켜지 않습니다"라고 적으면 로그가 거짓말을 한다.
            string _policy = string.Empty;

            if (true == _marksSdkUnusable)
            {
                _hasFailed = true;
                _policy = $"이번 실행에서는 {_sdkName}를 다시 켜지 않습니다. ";
            }

            // 예외 객체를 통째로 붙인다. 삼킨 예외는 스택 트레이스가 남지 않으면 나중에
            // 어디서 터졌는지 되짚을 방법이 없어서, 메시지만 남기면 조용한 실패가 된다.
            // 끄는 호출이 실패한 경우의 결과(계속 나갈 수 있음)는 이 문장으로 덮이지 않으므로,
            // 그쪽 호출 지점에서 한 줄 더 남긴다.
            Debug.LogWarning($"[DataConsentGate] {_sdkName} {_what}에 실패했습니다. " +
                $"{_policy}게임 동작에는 영향이 없습니다.\n{_exception}");
            return false;
        }
    }
}
