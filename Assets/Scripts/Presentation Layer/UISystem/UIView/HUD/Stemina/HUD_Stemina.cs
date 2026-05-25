using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;

public class HUD_Stemina : MonoBehaviour
{
    [Header("UI Ref")]
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private Image screenBlood;

    [Header("Motions")]
    [SerializeField] private string shakeTag = "DangerShake";
    [SerializeField] private string colorTag = "DangerColor";
    [SerializeField] private string bloodTag = "ScreenBlood";
    [Range(0f, 100f)][SerializeField] private float startPoint = 100f;

    private bool bWarningGauge = false;
    private MotionEntry shakeMotion;
    private MotionEntry colorMotion;
    private MotionEntry bloodMotion;

    public void Initialize()
    {
        progressBar?.Initialize();
        motionPlayer?.Initialize();

        Color newColor = Color.white;   
        newColor.a = 0f;
        screenBlood.color = newColor;
    }

    public void UpdateValue(float _ratio)
    {
        if (null != motionPlayer && false == bWarningGauge && startPoint * 0.01f >= _ratio)
        {
            bWarningGauge = true;
            shakeMotion = motionPlayer.Play(shakeTag, bReset: true);
            colorMotion = motionPlayer.Play(colorTag, bReset: true);
            bloodMotion = motionPlayer.Play(bloodTag, bReset: true);
        }

        progressBar?.UpdateValue(_ratio);
    }

    public void SetActivate(bool _townTrigger)
    {
        progressBar?.SetActivate(_townTrigger);
        screenBlood?.gameObject.SetActive(_townTrigger);
         
        if (true == _townTrigger)
        {
            bWarningGauge = false;
            
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(shakeMotion, true, true);
                motionPlayer.SettingEntryMotion(colorMotion, true, true);
                motionPlayer.SettingEntryMotion(bloodMotion, true, true);
            }
        }
    }
}
