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
