using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class FlowerVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform topRectTransform;
    [SerializeField] private RectTransform bottomRectTransform;
    [SerializeField] private RectTransform topWhiteRectTransform;
    [SerializeField] private RectTransform bottomWhiteRectTransform;
    [SerializeField] private Image topImage;
    [SerializeField] private Image bottomImage;
    [SerializeField] private Image topWhiteImage;
    [SerializeField] private Image bottomWhiteImage;

    [Header("Sprites")]
    [SerializeField] private Sprite topSprite;
    [SerializeField] private Sprite topWhiteSprite;
    [SerializeField] private Sprite[] bottomSprites = new Sprite[3];
    [SerializeField] private Sprite[] bottomWhiteSprites = new Sprite[3];

    [Header("Idle")]
    [SerializeField] private Vector2 topSize = new Vector2(12.0f, 8.0f);
    [SerializeField] private Vector2 bottomSize = new Vector2(10.0f, 6.0f);
    [SerializeField] private Vector2 topIdleRadius = new Vector2(2.0f, 1.0f);
    [SerializeField] private float idleCycleDuration = 2.0f;
    [SerializeField] private float bottomRotationLimit = 12.0f;
    [SerializeField] private int motionSeed;

    [Header("Dangle")]
    [SerializeField] private float dangleDuration = 0.65f;
    [SerializeField] private float dangleAmplitudeX = 4.0f;
    [SerializeField] private float dangleAmplitudeY = 1.0f;
    [SerializeField] private int dangleBounceCount = 2;
    [SerializeField, Range(0.0f, 1.0f)] private float dangleDamping = 0.18f;

    [Header("Grow")]
    [SerializeField] private float growDuration = 0.38f;
    [SerializeField] private float growOvershootScale = 1.18f;
    [SerializeField] private float whiteFadeDuration = 0.18f;

    private Vector2 topInitialPosition;
    private Vector2 bottomInitialPosition;
    private float bottomInitialRotationZ;
    private float idleTime;
    private float phaseA;
    private float phaseB;
    private float phaseC;
    private float speedA = 1.0f;
    private float speedB = 1.0f;
    private float speedC = 1.0f;
    private Vector2 dangleDirection = Vector2.right;
    private float dangleAmplitudeMultiplier = 1.0f;
    private float dangleElapsedTime;
    private bool isDanglePlaying;
    private bool initialized;
    private RectTransform rectTransform;
    private Vector3 initialScale = Vector3.one;
    private Sequence growSequence;
    private Tween whiteFadeTween;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        ApplySprites();
    }

    private void OnValidate()
    {
        idleCycleDuration = Mathf.Max(0.01f, idleCycleDuration);
        dangleDuration = Mathf.Max(0.01f, dangleDuration);
        dangleBounceCount = Mathf.Max(1, dangleBounceCount);

        BindReferencesIfNeeded();
        ApplySprites();
        ApplyFixedSizes();
    }

    private void Update()
    {
        Initialize();
        TickMotion(GetDeltaTime());
    }

    public void SetBottomVariant(int _variantIndex)
    {
        BindReferencesIfNeeded();

        if (null == bottomImage || null == bottomSprites || 0 == bottomSprites.Length)
            return;

        int _index = Mathf.Abs(_variantIndex) % bottomSprites.Length;
        bottomImage.sprite = bottomSprites[_index];

        if (null != bottomWhiteImage && null != bottomWhiteSprites && _index < bottomWhiteSprites.Length)
            bottomWhiteImage.sprite = bottomWhiteSprites[_index];

        ApplyFixedSizes();
        SetMotionSeed((_variantIndex + 1) * 7919);
    }

    public void SetMotionSeed(int _seed)
    {
        motionSeed = 0 == _seed ? 1 : _seed;
        ApplyMotionSeed();
    }

    public void PlayDangle()
    {
        isDanglePlaying = true;
        dangleElapsedTime = 0.0f;
    }

    public void PlayGrow()
    {
        Initialize();
        growSequence?.Kill(false);
        whiteFadeTween?.Kill(false);

        if (null == rectTransform)
            return;

        rectTransform.localScale = Vector3.zero;
        PlayWhiteFade();

        growSequence = DOTween.Sequence();
        growSequence.Append(rectTransform.DOScale(initialScale * growOvershootScale, growDuration * 0.55f).SetEase(Ease.OutBack));
        growSequence.Append(rectTransform.DOScale(initialScale * 0.94f, growDuration * 0.20f).SetEase(Ease.InOutSine));
        growSequence.Append(rectTransform.DOScale(initialScale, growDuration * 0.25f).SetEase(Ease.OutBack));
        growSequence.OnComplete(() =>
        {
            rectTransform.localScale = initialScale;
            growSequence = null;
        });
    }

    public void PlayWhiteFade()
    {
        whiteFadeTween?.Kill(false);
        SetWhiteOverlayAlpha(1.0f);
        whiteFadeTween = DOVirtual.Float(1.0f, 0.0f, Mathf.Max(0.01f, whiteFadeDuration), SetWhiteOverlayAlpha)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                SetWhiteOverlayAlpha(0.0f);
                whiteFadeTween = null;
            });
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        BindReferencesIfNeeded();
        ApplySprites();
        ApplyFixedSizes();
        ApplyMotionSeed();

        rectTransform = transform as RectTransform;
        if (null != rectTransform)
            initialScale = rectTransform.localScale;

        if (null != topRectTransform)
            topInitialPosition = topRectTransform.anchoredPosition;

        if (null != bottomRectTransform)
        {
            bottomInitialPosition = bottomRectTransform.anchoredPosition;
            bottomInitialRotationZ = bottomRectTransform.localEulerAngles.z;
        }

        idleTime = Random.Range(0.0f, Mathf.Max(0.01f, idleCycleDuration));
    }

    private void TickMotion(float _deltaTime)
    {
        if (null == topRectTransform || null == bottomRectTransform)
            return;

        idleTime += _deltaTime;

        Vector2 _idleOffset = GetIdleOffset();

        Vector2 _dangleOffset = GetDangleOffset(_deltaTime);
        Vector2 _topOffset = _idleOffset + _dangleOffset;

        topRectTransform.anchoredPosition = topInitialPosition + RoundVector(_topOffset);
        topRectTransform.localRotation = Quaternion.identity;

        bottomRectTransform.anchoredPosition = bottomInitialPosition;
        bottomRectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, bottomInitialRotationZ + GetBottomRotationZ(_topOffset));
    }

    private Vector2 GetDangleOffset(float _deltaTime)
    {
        if (false == isDanglePlaying)
            return Vector2.zero;

        dangleElapsedTime += _deltaTime;
        float _progress = Mathf.Clamp01(dangleElapsedTime / Mathf.Max(0.01f, dangleDuration));
        float _decay = Mathf.Pow(Mathf.Clamp01(1.0f - _progress), Mathf.Lerp(1.0f, 5.0f, 1.0f - dangleDamping));
        float _wave = Mathf.Sin(_progress * Mathf.PI * 2.0f * Mathf.Max(1, dangleBounceCount));

        if (_progress >= 1.0f)
        {
            isDanglePlaying = false;
            return Vector2.zero;
        }

        Vector2 _amplitude = new Vector2(dangleAmplitudeX, dangleAmplitudeY) * dangleAmplitudeMultiplier;
        return new Vector2(
            dangleDirection.x * _wave * _amplitude.x * _decay,
            dangleDirection.y * Mathf.Abs(_wave) * _amplitude.y * _decay);
    }

    private Vector2 GetIdleOffset()
    {
        float _baseSpeed = (Mathf.PI * 2.0f) / Mathf.Max(0.01f, idleCycleDuration);
        float _time = idleTime * _baseSpeed;

        float _x =
            Mathf.Sin((_time * speedA) + phaseA) * 0.62f +
            Mathf.Sin((_time * speedB) + phaseB) * 0.28f +
            Mathf.Sin((_time * speedC) + phaseC) * 0.10f;

        float _y =
            Mathf.Cos((_time * (speedA * 0.81f)) + phaseB) * 0.55f +
            Mathf.Sin((_time * (speedC * 1.17f)) + phaseA) * 0.35f +
            Mathf.Cos((_time * (speedB * 0.43f)) + phaseC) * 0.10f;

        return new Vector2(
            Mathf.Clamp(_x, -1.0f, 1.0f) * topIdleRadius.x,
            Mathf.Clamp(_y, -1.0f, 1.0f) * topIdleRadius.y);
    }

    private float GetBottomRotationZ(Vector2 _topOffset)
    {
        float _rotation = -Mathf.Atan2(_topOffset.x, 8.0f + _topOffset.y) * Mathf.Rad2Deg;
        return Mathf.Clamp(_rotation, -bottomRotationLimit, bottomRotationLimit);
    }

    private Vector2 RoundVector(Vector2 _value)
    {
        return new Vector2(Mathf.Round(_value.x), Mathf.Round(_value.y));
    }

    private float GetDeltaTime()
    {
        if (Application.isPlaying)
            return Time.deltaTime;

#if UNITY_EDITOR
        return 1.0f / 30.0f;
#else
        return 0.0f;
#endif
    }

    private void ApplySprites()
    {
        if (null != topImage && null != topSprite)
            topImage.sprite = topSprite;

        if (null != topWhiteImage && null != topWhiteSprite)
            topWhiteImage.sprite = topWhiteSprite;

        if (null != bottomWhiteImage &&
            null != bottomWhiteSprites &&
            bottomWhiteSprites.Length > 0 &&
            null == bottomWhiteImage.sprite)
        {
            bottomWhiteImage.sprite = bottomWhiteSprites[0];
        }
    }

    private void ApplyFixedSizes()
    {
        if (null != topRectTransform)
            topRectTransform.sizeDelta = topSize;

        if (null != bottomRectTransform)
            bottomRectTransform.sizeDelta = bottomSize;

        if (null != topWhiteRectTransform)
        {
            if (null != topRectTransform)
                topWhiteRectTransform.anchoredPosition = topWhiteRectTransform.parent == topRectTransform ? Vector2.zero : topRectTransform.anchoredPosition;

            topWhiteRectTransform.sizeDelta = topSize;
            topWhiteRectTransform.SetAsLastSibling();
        }

        if (null != bottomWhiteRectTransform)
        {
            if (null != bottomRectTransform)
                bottomWhiteRectTransform.anchoredPosition = bottomWhiteRectTransform.parent == bottomRectTransform ? Vector2.zero : bottomRectTransform.anchoredPosition;

            bottomWhiteRectTransform.sizeDelta = bottomSize;
            bottomWhiteRectTransform.SetAsLastSibling();
        }
    }

    private void SetWhiteOverlayAlpha(float _alpha)
    {
        SetImageAlpha(topWhiteImage, _alpha);
        SetImageAlpha(bottomWhiteImage, _alpha);
    }

    private void SetImageAlpha(Image _image, float _alpha)
    {
        if (null == _image)
            return;

        Color _color = _image.color;
        _color.a = Mathf.Clamp01(_alpha);
        _image.color = _color;
    }

    private void ApplyMotionSeed()
    {
        if (0 == motionSeed)
            motionSeed = Random.Range(1, int.MaxValue);

        System.Random _random = new System.Random(motionSeed);
        phaseA = RandomRange(_random, 0.0f, Mathf.PI * 2.0f);
        phaseB = RandomRange(_random, 0.0f, Mathf.PI * 2.0f);
        phaseC = RandomRange(_random, 0.0f, Mathf.PI * 2.0f);
        speedA = RandomRange(_random, 0.72f, 1.28f);
        speedB = RandomRange(_random, 1.34f, 2.15f);
        speedC = RandomRange(_random, 0.31f, 0.68f);

        float _directionX = RandomRange(_random, -1.0f, 1.0f);
        float _directionY = RandomRange(_random, -0.35f, 0.35f);
        dangleDirection = new Vector2(_directionX, _directionY);
        if (dangleDirection.sqrMagnitude < 0.01f)
            dangleDirection = Vector2.right;

        dangleDirection.Normalize();
        dangleAmplitudeMultiplier = RandomRange(_random, 0.85f, 1.2f);
    }

    private float RandomRange(System.Random _random, float _min, float _max)
    {
        return Mathf.Lerp(_min, _max, (float)_random.NextDouble());
    }

    private void BindReferencesIfNeeded()
    {
        if (null == topRectTransform)
            topRectTransform = FindChildRectTransform("Top");

        if (null == bottomRectTransform)
            bottomRectTransform = FindChildRectTransform("Bottom");

        if (null == topWhiteRectTransform)
            topWhiteRectTransform = FindChildRectTransform("TopWhite");

        if (null == bottomWhiteRectTransform)
            bottomWhiteRectTransform = FindChildRectTransform("BottomWhite");

        if (null == topImage && null != topRectTransform)
            topImage = topRectTransform.GetComponent<Image>();

        if (null == bottomImage && null != bottomRectTransform)
            bottomImage = bottomRectTransform.GetComponent<Image>();

        if (null == topWhiteRectTransform && null != topRectTransform)
            topWhiteRectTransform = CreateOverlay("TopWhite", topRectTransform, out topWhiteImage);

        if (null == bottomWhiteRectTransform && null != bottomRectTransform)
            bottomWhiteRectTransform = CreateOverlay("BottomWhite", bottomRectTransform, out bottomWhiteImage);

        if (null == topWhiteImage && null != topWhiteRectTransform)
            topWhiteImage = topWhiteRectTransform.GetComponent<Image>();

        if (null == bottomWhiteImage && null != bottomWhiteRectTransform)
            bottomWhiteImage = bottomWhiteRectTransform.GetComponent<Image>();
    }

    private RectTransform FindChildRectTransform(string _name)
    {
        Transform _child = FindChild(transform, _name);
        return _child as RectTransform;
    }

    private Transform FindChild(Transform _root, string _name)
    {
        if (null == _root)
            return null;

        for (int i = 0; i < _root.childCount; i++)
        {
            Transform _child = _root.GetChild(i);
            if (_child.name == _name)
                return _child;

            Transform _result = FindChild(_child, _name);
            if (null != _result)
                return _result;
        }

        return null;
    }

    private RectTransform CreateOverlay(string _name, RectTransform _source, out Image _image)
    {
        GameObject _overlayObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _overlayObject.layer = gameObject.layer;
        _overlayObject.transform.SetParent(_source, false);

        RectTransform _overlayRectTransform = (RectTransform)_overlayObject.transform;
        _overlayRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _overlayRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _overlayRectTransform.pivot = _source.pivot;
        _overlayRectTransform.anchoredPosition = Vector2.zero;
        _overlayRectTransform.sizeDelta = _source.sizeDelta;
        _overlayRectTransform.SetAsLastSibling();

        _image = _overlayObject.GetComponent<Image>();
        _image.raycastTarget = false;
        SetImageAlpha(_image, 0.0f);
        return _overlayRectTransform;
    }

}
