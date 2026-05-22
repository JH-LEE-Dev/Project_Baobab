using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;

public class HUD_Stemina : MonoBehaviour
{
    [Header("UI Ref")]
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Motions")]
    [SerializeField] private string shakeTag = "DangerShake";
    [SerializeField] private string colorTag = "DangerColor";
    [Range(0f, 100f)][SerializeField] private float startPoint = 100f;

    private bool bWarningGauge = false;
    private MotionEntry shakeMotion;
    private MotionEntry colorMotion;

    public void Initialize()
    {
        progressBar?.Initialize();
        motionPlayer?.Initialize();
    }

    public void UpdateValue(float _ratio)
    {
        if (false == bWarningGauge && startPoint * 0.01f >= _ratio)
        {
            bWarningGauge = true;
            shakeMotion = motionPlayer?.Play(shakeTag, bReset: true);
            colorMotion = motionPlayer?.Play(colorTag, bReset: true);
        }

        progressBar?.UpdateValue(_ratio);
    }

    public void SetActivate(bool _townTrigger)
    {
        progressBar?.SetActivate(_townTrigger);
         
        // 마을로 귀환 하면 원복
        if (true == _townTrigger)
        {
            bWarningGauge = false;
            
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(shakeMotion, true, true);
                motionPlayer.SettingEntryMotion(colorMotion, true, true);
            }
        }
    }
}
