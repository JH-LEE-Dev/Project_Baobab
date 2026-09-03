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
#endif

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
