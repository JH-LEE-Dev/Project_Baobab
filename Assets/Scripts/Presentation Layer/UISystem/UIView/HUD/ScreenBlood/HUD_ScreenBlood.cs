using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 혹은 위험(스태미나 부족 등) 상태일 때 화면 전체에 붉은색 핏빛 깜빡임 연출을 관리하는 독립 컴포넌트입니다.
/// </summary>
public class HUD_ScreenBlood : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Image screenBlood;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Motions")]
    [SerializeField] private string bloodTag = "ScreenBlood";

    [Header("Fade Out Settings")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private Ease fadeOutEase = Ease.OutQuad;

    // 내부 의존성
    private MotionEntry bloodMotion;
    private Tween fadeTween;
    private TweenCallback onFadeCompleteCallback;
    private bool isEffectPlaying = false;
    private bool isInitialized = false;


    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// 컴포넌트를 초기화하고 알파값을 0으로 리셋합니다.
    /// </summary>
    public void Initialize()
    {
        if (true == isInitialized)
            return;

        fadeTween?.Kill();
        onFadeCompleteCallback = OnFadeComplete;

        if (null != motionPlayer)
            motionPlayer.Initialize();

        if (null != screenBlood)
        {
            Color _newColor = Color.white;
            _newColor.a = 0f;
            screenBlood.color = _newColor;
        }

        isInitialized = true;
    }

    /// <summary>
    /// 피격 효과를 재생하거나 중지시킵니다.
    /// </summary>
    public void PlayBloodEffect(bool _active)
    {
        if (_active == isEffectPlaying)
            return;

        isEffectPlaying = _active;

        if (null == motionPlayer)
            return;

        fadeTween?.Kill();

        if (true == isEffectPlaying)
        {
            bloodMotion = motionPlayer.Play(bloodTag, bReset: true);
        }
        else
        {
            motionPlayer.SettingEntryMotion(bloodMotion, true, false);
            
            if (null != screenBlood)
                fadeTween = screenBlood.DOFade(0f, fadeOutDuration)
                    .SetEase(fadeOutEase)
                    .OnComplete(onFadeCompleteCallback);
        }
    }

    /// <summary>
    /// 애니메이션 상태를 정지하고 피격 가시성을 제거합니다.
    /// </summary>
    public void ResetAnimation(bool _immediate = false)
    {
        isEffectPlaying = false;

        fadeTween?.Kill();

        if (true == _immediate)
        {
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(bloodMotion, true, true);
                motionPlayer.ResetAllMotions();
            }

            if (null != screenBlood)
            {
                Color _newColor = screenBlood.color;
                _newColor.a = 0f;
                screenBlood.color = _newColor;
            }
        }
        else
        {
            if (null != motionPlayer)
                motionPlayer.SettingEntryMotion(bloodMotion, true, false);

            if (null != screenBlood)
                fadeTween = screenBlood.DOFade(0f, fadeOutDuration)
                    .SetEase(fadeOutEase)
                    .OnComplete(onFadeCompleteCallback);
        }
    }

    private void OnFadeComplete()
    {
        if (null != motionPlayer)
        {
            motionPlayer.SettingEntryMotion(bloodMotion, false, true);
            motionPlayer.ResetAllMotions();
        }
    }

    private void OnDestroy()
    {
        fadeTween?.Kill();
        onFadeCompleteCallback = null;
    }
}


