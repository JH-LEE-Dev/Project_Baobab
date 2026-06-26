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
    [SerializeField] private Image shieldLeafImage;
    [SerializeField] private Image shieldTrunkImage;
    [SerializeField] private Image highlightLeafImage;
    [SerializeField] private Image highlightTrunkImage;
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

    [Header("HDR Intensity Config")]
    [SerializeField] private float shieldHdrIntensityMultiplier = 1.0f;
    [SerializeField] private float highlightHdrIntensityMultiplier = 1.0f;

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
    private Material shieldLeafMat;
    private Material shieldTrunkMat;
    private Material highlightLeafMat;
    private Material highlightTrunkMat;
    private Color originalShieldLeafColor;
    private Color originalShieldTrunkColor;
    private Color originalHighlightLeafColor;
    private Color originalHighlightTrunkColor;

    private static readonly int HdrColorPropertyId = Shader.PropertyToID("_HDRColor");


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

            bool _hasLeaf = (null != visualData.topSprites && 0 < visualData.topSprites.Count);
            if (null != leafImage)
            {
                leafImage.gameObject.SetActive(_hasLeaf);
                if (_hasLeaf)
                    leafImage.sprite = visualData.topSprites[0];
            }

            bool _hasTrunk = (null != visualData.bottomSprites && 0 < visualData.bottomSprites.Count);
            if (null != trunkImage)
            {
                trunkImage.gameObject.SetActive(_hasTrunk);
                if (_hasTrunk)
                    trunkImage.sprite = visualData.bottomSprites[0];
            }

            bool _hasShieldLeaf = (null != visualData.shieldTopSprites && 0 < visualData.shieldTopSprites.Count);
            if (null != shieldLeafImage)
            {
                shieldLeafImage.gameObject.SetActive(_hasShieldLeaf);
                if (_hasShieldLeaf)
                    shieldLeafImage.sprite = visualData.shieldTopSprites[0];
            }

            bool _hasShieldTrunk = (null != visualData.shieldBottomSprites && 0 < visualData.shieldBottomSprites.Count);
            if (null != shieldTrunkImage)
            {
                shieldTrunkImage.gameObject.SetActive(_hasShieldTrunk);
                if (_hasShieldTrunk)
                    shieldTrunkImage.sprite = visualData.shieldBottomSprites[0];
            }

            bool _hasHighlightLeaf = (null != visualData.highlightTopSprites && 0 < visualData.highlightTopSprites.Count);
            if (null != highlightLeafImage)
            {
                highlightLeafImage.gameObject.SetActive(_hasHighlightLeaf);
                if (_hasHighlightLeaf)
                    highlightLeafImage.sprite = visualData.highlightTopSprites[0];
            }

            bool _hasHighlightTrunk = (null != visualData.highlightBottomSprites && 0 < visualData.highlightBottomSprites.Count);
            if (null != highlightTrunkImage)
            {
                highlightTrunkImage.gameObject.SetActive(_hasHighlightTrunk);
                if (_hasHighlightTrunk)
                    highlightTrunkImage.sprite = visualData.highlightBottomSprites[0];
            }

            // Shield 및 Highlight 머테리얼 HDR Intensity 값 적용
            if (null != shieldLeafImage && true == shieldLeafImage.gameObject.activeSelf)
                ApplyHdrIntensity(shieldLeafImage, ref shieldLeafMat, ref originalShieldLeafColor, visualData.shieldHDRIntensity * shieldHdrIntensityMultiplier);

            if (null != shieldTrunkImage && true == shieldTrunkImage.gameObject.activeSelf)
                ApplyHdrIntensity(shieldTrunkImage, ref shieldTrunkMat, ref originalShieldTrunkColor, visualData.shieldHDRIntensity * shieldHdrIntensityMultiplier);

            if (null != highlightLeafImage && true == highlightLeafImage.gameObject.activeSelf)
                ApplyHdrIntensity(highlightLeafImage, ref highlightLeafMat, ref originalHighlightLeafColor, visualData.highlightHDRIntensity * highlightHdrIntensityMultiplier);

            if (null != highlightTrunkImage && true == highlightTrunkImage.gameObject.activeSelf)
                ApplyHdrIntensity(highlightTrunkImage, ref highlightTrunkMat, ref originalHighlightTrunkColor, visualData.highlightHDRIntensity * highlightHdrIntensityMultiplier);
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


    private void ApplyHdrIntensity(Image _image, ref Material _cachedMaterial, ref Color _originalColor, float _intensity)
    {
        if (null == _image)
            return;

        Material _currentMat = _image.material;
        if (null == _currentMat)
            return;

        if (null == _cachedMaterial)
        {
            _cachedMaterial = Instantiate(_currentMat);
            _image.material = _cachedMaterial;
            _originalColor = _cachedMaterial.GetColor(HdrColorPropertyId);
        }

        if (null != _cachedMaterial)
        {
            Color _targetColor = new Color(
                _originalColor.r * _intensity,
                _originalColor.g * _intensity,
                _originalColor.b * _intensity,
                _originalColor.a
            );
            _cachedMaterial.SetColor(HdrColorPropertyId, _targetColor);
        }
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

        if (null != shieldLeafMat)
        {
            Destroy(shieldLeafMat);
            shieldLeafMat = null;
        }

        if (null != shieldTrunkMat)
        {
            Destroy(shieldTrunkMat);
            shieldTrunkMat = null;
        }

        if (null != highlightLeafMat)
        {
            Destroy(highlightLeafMat);
            highlightLeafMat = null;
        }

        if (null != highlightTrunkMat)
        {
            Destroy(highlightTrunkMat);
            highlightTrunkMat = null;
        }
    }
}
