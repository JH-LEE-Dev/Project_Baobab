using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;

public class HUD_Stemina : MonoBehaviour
{
    // 외부 의존성
    [Header("UI Ref")]
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Motions")]
    [SerializeField] private string shakeTag = "DangerShake";
    [SerializeField] private string colorTag = "DangerColor";
    [Range(0f, 100f)][SerializeField] private float startPoint = 100f;

    [Tooltip("경고가 켜진 뒤 피로도가 startPoint + 이 값(%p) 위로 회복되면 경고를 끈다. 경계에서 켜졌다 꺼졌다 하지 않도록 두는 여유폭")]
    [Range(0f, 100f)][SerializeField] private float releaseMargin = 5f;

    // 내부 의존성
    private HUD_ScreenBlood screenBloodComponent;
    // 던전에서만 true. 마을에서는 게이지가 숨겨지고 저피로도 경고(게이지 흔들림/붉은 화면)도 판정하지 않는다.
    private bool bActive = false;
    private bool bWarningGauge = false;
    private MotionEntry shakeMotion;
    private MotionEntry colorMotion;

    // 스태미나 무적 원인별 상태(SetInvincible 참고)
    private bool bEntryInvincible = false;
    private bool bRestAreaInvincible = false;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(HUD_ScreenBlood _screenBlood)
    {
        progressBar?.Initialize();
        motionPlayer?.Initialize();

        screenBloodComponent = _screenBlood;
    }

    public void UpdateValue(float _ratio)
    {
        // 경고 상태는 현재 비율을 그대로 따라간다(켜짐: startPoint 이하, 꺼짐: startPoint + releaseMargin 초과).
        // 예전엔 한 번 켜지면 던전 진입 때만 풀리는 래치였는데, 그러면 마을에서 켜져 있어도 끌 방법이 없었고
        // 휴식/포션으로 피로도를 회복해도 붉은 화면이 계속 깜빡였다.
        if (true == bActive)
        {
            float _warnRatio = startPoint * 0.01f;

            if (false == bWarningGauge && _warnRatio >= _ratio)
                SetWarning(true);
            else if (true == bWarningGauge && _warnRatio + releaseMargin * 0.01f < _ratio)
                SetWarning(false);
        }

        progressBar?.UpdateValue(_ratio);
    }

    /// <summary>
    /// 게이지 표시 여부. 던전이면 true, 마을이면 false(UIView_HUD.ChangedActiveStateStemina 참고).
    /// 어느 쪽으로 바뀌든 저피로도 경고는 일단 끈다. 예전엔 던전 진입(true)에서만 끄고 마을 진입(false)에서는
    /// 아무것도 하지 않아, 던전에서 켜진 붉은 화면 깜빡임이 마을까지 이어지면 다음 던전에 들어갈 때까지 남았다.
    /// </summary>
    public void SetActivate(bool _bActive)
    {
        bActive = _bActive;
        progressBar?.SetActivate(_bActive);

        SetWarning(false);

        // 무적 상태도 맵이 바뀔 때마다 비운다. 꺼짐 알림이 어떤 이유로 빠져도 다음 맵까지 연출이 남지 않게 한다.
        // 던전 쪽은 이 호출(DecalreDungeonTypeSignal)이 차량 활성화(ReadyPortal) 직후 같은 프레임에 오므로
        // 휴식 구역 진입 알림(차량의 다음 Update)보다 항상 먼저다. 입장 무적 켜짐은 그보다도 한참 뒤다.
        ResetInvincible();
    }

    /// <summary>
    /// 스태미나가 닳지 않는 무적 구간의 원인별 시작(true) / 종료(false).
    ///   · DungeonEntry: HUD가 올라오기 시작하는 프레임에 켜지고, 실제 소모가 시작되는 프레임에 꺼진다(약 0.7초 간격).
    ///   · RestArea: "휴식" 특성으로 열린 차량 주변 휴식 구역에 들어가고 나갈 때.
    /// 스폰 지점이 휴식 구역 안이라 두 원인은 자주 겹친다. 그래서 원인별로 따로 기억하고, 둘을 합친
    /// 무적 여부가 실제로 바뀔 때만 연출을 켜고 끈다(입장 무적이 끝나도 원 안이면 연출이 이어진다).
    /// </summary>
    public void SetInvincible(EStaminaInvincibleSource _source, bool _bInvincible)
    {
        // 마을에서는 무적 연출을 쓰지 않는다. 마을 차량에도 같은 휴식 구역이 있어 알림은 오지만 기록하지도 않는다.
        if (false == bActive)
            return;

        bool bWasInvincible = IsInvincible;

        if (EStaminaInvincibleSource.DungeonEntry == _source)
            bEntryInvincible = _bInvincible;
        else if (EStaminaInvincibleSource.RestArea == _source)
            bRestAreaInvincible = _bInvincible;

        if (bWasInvincible != IsInvincible)
            PlayInvincibleEffect(IsInvincible);
    }

    private bool IsInvincible => bEntryInvincible || bRestAreaInvincible;

    private void ResetInvincible()
    {
        bool bWasInvincible = IsInvincible;

        bEntryInvincible = false;
        bRestAreaInvincible = false;

        if (true == bWasInvincible)
            PlayInvincibleEffect(false);
    }

    private void PlayInvincibleEffect(bool _bOn)
    {
        // TODO: 무적 연출 (켜짐: 연출 시작 / 꺼짐: 연출 종료)
    }

    private void SetWarning(bool _bOn)
    {
        bWarningGauge = _bOn;

        if (true == _bOn)
        {
            if (null != motionPlayer)
            {
                shakeMotion = motionPlayer.Play(shakeTag, bReset: true);
                colorMotion = motionPlayer.Play(colorTag, bReset: true);
            }
        }
        else
        {
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(shakeMotion, true, true);
                motionPlayer.SettingEntryMotion(colorMotion, true, true);
            }
        }

        // 켜기/끄기 모두 HUD_ScreenBlood 쪽이 중복 호출을 걸러주므로 상태 전이 여부와 무관하게 넘긴다.
        if (null != screenBloodComponent)
            screenBloodComponent.PlayBloodEffect(_bOn);
    }
}
