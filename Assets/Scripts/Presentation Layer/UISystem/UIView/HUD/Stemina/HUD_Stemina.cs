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

    // 내부 의존성
    private HUD_ScreenBlood screenBloodComponent;
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
        if (null != motionPlayer && false == bWarningGauge && startPoint * 0.01f >= _ratio)
        {
            bWarningGauge = true;
            shakeMotion = motionPlayer.Play(shakeTag, bReset: true);
            colorMotion = motionPlayer.Play(colorTag, bReset: true);

            if (null != screenBloodComponent)
                screenBloodComponent.PlayBloodEffect(true);
        }

        progressBar?.UpdateValue(_ratio);
    }

    public void SetActivate(bool _townTrigger)
    {
        progressBar?.SetActivate(_townTrigger);
         
        if (true == _townTrigger)
        {
            bWarningGauge = false;
            
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(shakeMotion, true, true);
                motionPlayer.SettingEntryMotion(colorMotion, true, true);
            }

            if (null != screenBloodComponent)
                screenBloodComponent.PlayBloodEffect(false);
        }
    }
}
