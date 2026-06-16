using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationTreeProp : MonoBehaviour, IPointerClickHandler
{
    //외부 의존성
    [SerializeField] private Image leafImage;
    [SerializeField] private Image trunkImage;
    [SerializeField] private TMPro.TextMeshProUGUI nameText;
    [SerializeField] private ParticleSystem leafParticle;
    [SerializeField] private ParticleSystem trunkParticle;
    [SerializeField] private TreeVisualDataBase treeVisualDataBase;
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private string appearTag = "Appear";

    [Header("Shake Config")]
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeStrength = 0.15f;
    [SerializeField] private int shakeVibrato = 10;
    [SerializeField] private float shakeRandomness = 90f;

    [Header("Disappear Config")]
    [SerializeField] private float disappearDuration = 0.2f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    //내부 의존성
    private RectTransform rect;
    private TreeType treeType;
    private Action<TreeType> onClickCallback;
    private Tween appearDelayTween;
    private Tween shakeTween;
    private bool isInitialized = false;
    private bool isDisappearing = false;
    private TweenCallback onAppearDelayCompleteCallback;
    private LocalizationManager localizationManager;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        if (true == isInitialized)
            return;

        rect = GetComponent<RectTransform>();
        onAppearDelayCompleteCallback = OnAppearDelayComplete;
        isInitialized = true;
    }

    public void Setup(TreeType _treeType, Action<TreeType> _onClick, LocalizationManager _localizationManager = null)
    {
        if (false == isInitialized)
            Initialize();

        treeType = _treeType;
        onClickCallback = _onClick;
        isDisappearing = false;
        localizationManager = _localizationManager;

        if (null != nameText)
        {
            string _localizedName = string.Empty;
            if (null != localizationManager)
                _localizedName = localizationManager.GetText(_treeType);

            if (true == string.IsNullOrEmpty(_localizedName))
                nameText.text = _treeType.ToString();
            else
                nameText.text = _localizedName;
        }

        if (null != treeVisualDataBase)
        {
            TreeVisualData visualData = treeVisualDataBase.Get(_treeType);

            if (null != leafImage && null != visualData.topSprites && 0 < visualData.topSprites.Count)
                leafImage.sprite = visualData.topSprites[0];

            if (null != trunkImage && null != visualData.bottomSprites && 0 < visualData.bottomSprites.Count)
                trunkImage.sprite = visualData.bottomSprites[0];

            // if (null != visualData.treeColorSets && 0 < visualData.treeColorSets.Count)
            // {
            //     if (null != leafImage)
            //         leafImage.color = visualData.treeColorSets[0].topColor;

            //     if (null != trunkImage)
            //         trunkImage.color = visualData.treeColorSets[0].bottomColor;
            // }
        }

        ResetAnimation();
    }

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
            transform.localScale = Vector3.zero;

            appearDelayTween = DOVirtual.DelayedCall(_delay, onAppearDelayCompleteCallback).SetEase(Ease.Linear);
        }
    }

    private void OnAppearDelayComplete()
    {
        transform.localScale = Vector3.one;
        motionPlayer.Play(appearTag, bReset: true);
    }

    public void PlayDisappearAnimation(float _delay, TweenCallback _onComplete)
    {
        isDisappearing = true;

        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        appearDelayTween = transform.DOScale(Vector3.zero, disappearDuration)
                                     .SetDelay(_delay)
                                     .SetEase(disappearEase)
                                     .OnComplete(_onComplete);
    }

    public void ResetAnimation()
    {
        isDisappearing = false;

        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != shakeTween && shakeTween.IsActive())
            shakeTween.Kill();

        transform.localScale = Vector3.one;

        if (null != motionPlayer)
            motionPlayer.ResetAllMotions();
    }

    public RectTransform GetRectTransform()
    {
        if (null == rect)
            rect = GetComponent<RectTransform>();

        return rect;
    }


    // Event System 구현부

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (true == isDisappearing)
            return;

        if (null != shakeTween && shakeTween.IsActive())
            shakeTween.Kill();

        // 나무가 흔들리는 연출
        transform.localScale = Vector3.one;
        shakeTween = transform.DOShakeScale(shakeDuration, shakeStrength, shakeVibrato, shakeRandomness);

        // 나뭇잎이 휘날리거나 기둥 파티클 재생
        if (null != leafParticle)
            leafParticle.Play();

        if (null != trunkParticle)
            trunkParticle.Play();

        onClickCallback?.Invoke(treeType);
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        ResetAnimation();
    }

    private void OnDestroy()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != shakeTween && true == shakeTween.IsActive())
            shakeTween.Kill();
    }
}
