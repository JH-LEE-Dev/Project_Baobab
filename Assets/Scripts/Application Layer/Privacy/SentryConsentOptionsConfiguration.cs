using Sentry.Unity;
using UnityEngine;

/// <summary>
/// 데이터 수집에 동의하지 않은 유저에게서 Sentry가 아예 시작조차 하지 않게 만듭니다.
///
/// == 왜 이 방식이어야 하는가 ==
/// Sentry는 SentryOptions.asset을 근거로 RuntimeInitializeLoadType.SubsystemRegistration에서
/// 스스로 초기화합니다. 이는 첫 씬의 Awake보다도 이르므로, BootStrap이나 다른 MonoBehaviour에서
/// 막을 수 있는 것은 "시작 자체"가 아니라 "시작된 뒤에 끄기"뿐입니다. 그 사이에 이미 네이티브
/// 크래시 핸들러가 설치되고 세션이 열립니다.
///
/// 패키지의 SentryInitialization은 초기화 직전에 이 ScriptableObject의 Configure를 호출하고,
/// 여기서 Enabled를 false로 두면 매니지드 SDK를 시작하지 않을 뿐 아니라 이미 자기 초기화를
/// 끝낸 네이티브 계층까지 닫아줍니다. 동의 없는 유저에게서 아무것도 나가지 않게 만들 수 있는
/// 유일한 지점입니다.
///
/// == 연결 방법 ==
/// Assets/Resources/Sentry/SentryOptions.asset의 OptionsConfiguration 필드에
/// Assets/Resources/Sentry/SentryConsentOptionsConfiguration.asset이 물려 있어야 합니다.
/// 이 연결이 끊어지면 동의 여부와 무관하게 Sentry가 켜집니다. 조용히 깨지는 종류의 사고라,
/// 아래 로그가 매 실행마다 한 줄 남도록 해두었습니다.
/// </summary>
[CreateAssetMenu(fileName = "SentryConsentOptionsConfiguration",
    menuName = "LumberBoy/Privacy/Sentry Consent Options Configuration")]
public class SentryConsentOptionsConfiguration : SentryOptionsConfiguration
{
#if UNITY_EDITOR
    /// <summary>
    /// 에디터 플레이 세션이 보고될 환경 이름입니다. 배포 환경(steam-production / steam-demo 등)과
    /// 겹치지 않기만 하면 되며, Sentry에서 environment 로 걸러내기 쉽도록 짧게 둡니다.
    /// </summary>
    private const string EDITOR_ENVIRONMENT = "editor";
#endif

    /// <summary>
    /// GameAnalytics의 윈도우 네이티브 계층이 남기는 로그의 머리말입니다.
    /// </summary>
    private const string GAMEANALYTICS_NATIVE_MARKER = "[GameAnalytics Native]";

    public override void Configure(SentryUnityOptions _options)
    {
        if (null == _options) return;

#if UNITY_EDITOR
        // 이 훅은 런타임뿐 아니라 "빌드 시점"에도 호출됩니다.
        // (Sentry의 빌드 처리기가 ScriptableSentryUnityOptions.LoadSentryUnityOptions로 옵션을 읽습니다)
        //
        // 그때 Enabled를 false로 두면 빌드 처리기가 Sentry를 쓰지 않는 프로젝트로 판단해서
        //   - 네이티브 크래시 핸들러(sentry.dll, crashpad_handler.exe, crashpad_wer.dll)를 빌드에 넣지 않고
        //   - 디버그 심볼 업로드(sentry-cli)도 건너뜁니다.
        // 결과적으로 "빌드한 사람의 개인 동의 설정"이 모든 유저에게 나갈 빌드의 내용을 바꿔버립니다.
        // 동의한 유저조차 네이티브 크래시를 남기지 못하고, 심볼이 없어 IL2CPP 스택 트레이스에
        // 파일·줄 번호가 붙지 않습니다. 에셋 설정은 정상이라 파일만 봐서는 드러나지 않습니다.
        //
        // 동의 여부는 게임이 실제로 실행될 때만 의미가 있으므로, 플레이 중이 아니면 건드리지 않습니다.
        // 플레이어 빌드에서는 이 블록이 컴파일되지 않으므로 동의 차단은 그대로 동작합니다.
        if (false == Application.isPlaying) return;

        // 여기까지 왔다면 "에디터에서 플레이 중"입니다.
        //
        // Tools > 빌드 메뉴는 SentryOptions.asset의 EnvironmentOverride를 배포용 값으로
        // 바꿔놓습니다(steam-production / itch-demo 등). 그 값을 그대로 두고 에디터에서
        // 리포트가 나가면 검토하다 낸 에러가 실제 출시 환경 지표에 섞이고, 한 번 섞이면
        // 갈라낼 수 없습니다(PlatformConsistencyGuard.CheckAnalytics 주석 참고).
        //
        // [지금은 그 일이 일어나지 않습니다 - 그래도 여기서 갈라두는 이유]
        // SentryOptions.asset의 CaptureInEditor가 0이라 ShouldInitializeSdk()가 거짓을 돌려주고,
        // 에디터에서는 SDK 자체가 켜지지 않습니다. 이 훅은 옵션을 만들 때 불릴 뿐입니다.
        // 하지만 그 한 줄은 에셋의 체크박스라 누구든 Sentry 동작을 확인하려고 켤 수 있고,
        // 켜는 사람이 환경 오염까지 같이 떠올릴 것이라고 기대할 수는 없습니다.
        // 그때 자동으로 안전해지도록 미리 갈라둡니다. 켜져 있지 않은 동안은 아무 효과가 없습니다.
        //
        // Sentry를 통째로 끄는 방식은 쓰지 않습니다. 그러면 CaptureInEditor를 켜도 확인이
        // 불가능해져, 이 파일이 막으려는 "에디터에서 한 번도 시험되지 않는 경로"가 됩니다.
        //
        // 에셋 값이 아니라 이번 실행의 옵션만 덮어씁니다. 에셋을 건드리면 빌드 전 정합성
        // 검사가 어긋난 값을 보고 다음 빌드를 중단시킵니다.
        _options.Environment = EDITOR_ENVIRONMENT;
#endif

        // 동의 여부와 무관하게 걸어둡니다. 동의하지 않았다면 어차피 아무것도 나가지 않고,
        // 동의한 유저에게서만 의미가 생기는 필터라 순서를 신경 쓸 필요가 없습니다.
        FilterOutGameAnalyticsNativeNoise(_options);

        // SettingsManager의 인스턴스를 만들지 않고 파일만 읽는다. 이 시점에는 씬이 아직 없어서
        // 여기서 만든 GameObject는 DontDestroyOnLoad 보호를 받지 못한 채 첫 씬 로드에서
        // 파괴될 수 있다. (SettingsManager.ReadPersistedConsent 주석 참고)
        EDataConsent _consent = SettingsManager.ReadPersistedConsent();

        if (EDataConsent.Granted == _consent)
        {
            Debug.Log("[Sentry] 데이터 수집에 동의한 상태입니다. 크래시 리포트를 활성화합니다.");
            return;
        }

        _options.Enabled = false;
        Debug.Log($"[Sentry] 데이터 수집 동의가 없어(상태={_consent}) 크래시 리포트를 비활성화합니다.");
    }

    /// <summary>
    /// GameAnalytics 네이티브 계층이 남기는 로그를 Sentry로 보내지 않습니다.
    ///
    /// 유저 이름이 비ASCII인 PC(예: C:\Users\병훈\...)에서 GA의 윈도우 네이티브 계층이 자기
    /// 저장 폴더를 만들지 못하고 Debug.LogError를 남깁니다. GA 자신의 워커 스레드 안에서 끝나는
    /// 실패라 게임 동작에는 영향이 없고(잃는 것은 그 유저의 플레이 통계뿐입니다), 원인이 GA SDK
    /// 내부라 우리가 고칠 수도 없습니다. 그대로 두면 해당 유저가 게임을 켤 때마다 이벤트가 올라와
    /// Sentry 할당량을 갉아먹고, 정작 고쳐야 할 리포트가 그 사이에 묻힙니다.
    ///
    /// 특정 문구(create_directory 등)가 아니라 머리말 전체를 거릅니다. GA 네이티브가 무엇을
    /// 호소하든 우리가 코드로 대응할 수 있는 것은 없기 때문입니다. 대신 GA를 의심해야 할 일이
    /// 생기면 Sentry가 아니라 유저의 player.log를 봐야 한다는 뜻이기도 합니다.
    ///
    /// 문자열 대조라 GA가 머리말을 바꾸면 조용히 다시 새어 들어옵니다. 그 경우 시끄러워질 뿐
    /// 진짜 리포트가 사라지지는 않으므로, 실패하더라도 이쪽으로 실패하게 둡니다.
    ///
    /// BeforeSend는 C# 쪽 이벤트에만 걸립니다. 네이티브 크래시는 네이티브 SDK가 직접 보내 이
    /// 콜백을 거치지 않지만, 여기서 거르려는 것은 Debug.LogError를 타고 올라온 이벤트라 무방합니다.
    /// </summary>
    private static void FilterOutGameAnalyticsNativeNoise(SentryUnityOptions _options)
    {
        _options.SetBeforeSend((Sentry.SentryEvent _event) =>
        {
            if (true == IsGameAnalyticsNativeLog(_event)) return null;

            return _event;
        });
    }

    private static bool IsGameAnalyticsNativeLog(Sentry.SentryEvent _event)
    {
        if (null == _event) return false;

        // Debug.LogError는 버전에 따라 Message로도, 예외로도 올라옵니다. 둘 다 봅니다.
        if (true == ContainsNativeMarker(_event.Message?.Formatted)) return true;
        if (true == ContainsNativeMarker(_event.Message?.Message)) return true;
        if (true == ContainsNativeMarker(_event.Exception?.Message)) return true;

        return false;
    }

    private static bool ContainsNativeMarker(string _text)
    {
        if (true == string.IsNullOrEmpty(_text)) return false;

        return _text.Contains(GAMEANALYTICS_NATIVE_MARKER);
    }
}
