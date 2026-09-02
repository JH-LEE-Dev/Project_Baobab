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
    public override void Configure(SentryUnityOptions _options)
    {
        if (null == _options) return;

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
}
