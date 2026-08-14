using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public enum TreeVisualState
{
    Locked,
    Unlocked_Idle,
    Unlocked_Hover
}

public class HUD_PopupNav_TreeProp : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("?�무 ???��?지")]
    [SerializeField] private Image leafImage;
    [Tooltip("?�무 기둥 ?��?지")]
    [SerializeField] private Image trunkImage;
    [Tooltip("?�드 ???��?지")]
    [SerializeField] private Image shieldLeafImage;
    [Tooltip("?�드 기둥 ?��?지")]
    [SerializeField] private Image shieldTrunkImage;
    [Tooltip("?�이?�이?????��?지")]
    [SerializeField] private Image highlightLeafImage;
    [Tooltip("?�이?�이??기둥 ?��?지")]
    [SerializeField] private Image highlightTrunkImage;

    [Header("HDR Material Support")]
    [Tooltip("?�드 ??기둥???�용???�텐?�티 �?(머테리얼 Float ?�로?�티???�이?�트 반영)")]
    [SerializeField] private float shieldHdrIntensity = 1.0f;
    [Tooltip("?�이?�이????기둥???�용???�텐?�티 �?(머테리얼 Float ?�로?�티???�이?�트 반영)")]
    [SerializeField] private float highlightHdrIntensity = 1.0f;

    [Header("Hover Effect")]
    [Tooltip("호버 시 재생할 파티클 이펙트")]
    [SerializeField] private ParticleSystem hoverEffectParticle;

    [Header("Appear Animation")]
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private Ease appearEase = Ease.OutBack;

    private Material shieldLeafMat;
    private Material shieldTrunkMat;
    private Material highlightLeafMat;
    private Material highlightTrunkMat;

    private Color originalShieldLeafColor = Color.white;
    private Color originalShieldTrunkColor = Color.white;
    private Color originalHighlightLeafColor = Color.white;
    private Color originalHighlightTrunkColor = Color.white;

    private static readonly int HdrColorPropertyId = Shader.PropertyToID("_HDRColor_1");
    private static readonly int BaseHdrColorPropertyId = Shader.PropertyToID("_HDRColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private ParticleSystem[] childParticles;
    private Tween appearTween;

    private Color dimmedColorMultiplier = Color.gray;
    private Tween[] colorTweens = new Tween[6];

    private ParticleSystem[] hoverChildParticles;

    private TweenCallback cachedPlayChildParticles;
    private bool isInitialized = false;

    public void Initialize()
    {
        if (true == isInitialized)
        {
            return;
        }

        childParticles = GetComponentsInChildren<ParticleSystem>(true);
        if (null != hoverEffectParticle)
        {
            hoverChildParticles = hoverEffectParticle.GetComponentsInChildren<ParticleSystem>(true);
        }

        CacheOriginalMaterialsAndColors();
        
        // 그림???기?? ?해 EnvironmentSystem 찾기
        if (null == cachedPlayChildParticles) cachedPlayChildParticles = PlayChildParticles;
        isInitialized = true;
    }

    private void CacheOriginalMaterialsAndColors()
    {
        if (null != shieldLeafImage && null != shieldLeafImage.material)
        {
            shieldLeafMat = Instantiate(shieldLeafImage.material);
            shieldLeafImage.material = shieldLeafMat;
            originalShieldLeafColor = shieldLeafMat.HasProperty(BaseHdrColorPropertyId) ? shieldLeafMat.GetColor(BaseHdrColorPropertyId) : shieldLeafImage.color;
        }

        if (null != shieldTrunkImage && null != shieldTrunkImage.material)
        {
            shieldTrunkMat = Instantiate(shieldTrunkImage.material);
            shieldTrunkImage.material = shieldTrunkMat;
            originalShieldTrunkColor = shieldTrunkMat.HasProperty(BaseHdrColorPropertyId) ? shieldTrunkMat.GetColor(BaseHdrColorPropertyId) : shieldTrunkImage.color;
        }

        if (null != highlightLeafImage && null != highlightLeafImage.material)
        {
            highlightLeafMat = Instantiate(highlightLeafImage.material);
            highlightLeafImage.material = highlightLeafMat;
            originalHighlightLeafColor = highlightLeafMat.HasProperty(BaseHdrColorPropertyId) ? highlightLeafMat.GetColor(BaseHdrColorPropertyId) : highlightLeafImage.color;
        }

        if (null != highlightTrunkImage && null != highlightTrunkImage.material)
        {
            highlightTrunkMat = Instantiate(highlightTrunkImage.material);
            highlightTrunkImage.material = highlightTrunkMat;
            originalHighlightTrunkColor = highlightTrunkMat.HasProperty(BaseHdrColorPropertyId) ? highlightTrunkMat.GetColor(BaseHdrColorPropertyId) : highlightTrunkImage.color;
        }
    }

    public void Setup(TreeVisualData _visualData)
    {
        if (false == isInitialized)
        {
            Initialize();
        }

        gameObject.SetActive(true);

        SetupParticleColor(_visualData);
        SetupSprites(_visualData);
    }

    private void SetupParticleColor(TreeVisualData _visualData)
    {
        if (null != hoverEffectParticle)
        {
            ParticleSystem.MainModule _main = hoverEffectParticle.main;
            _main.startColor = _visualData.topVfxColor.startColor;
            
            if (true == _visualData.topVfxColor.overrideChildrenColor && null != hoverChildParticles)
            {
                for (int i = 0; i < hoverChildParticles.Length; i++)
                {
                    if (hoverEffectParticle != hoverChildParticles[i])
                    {
                        ParticleSystem.MainModule _childMain = hoverChildParticles[i].main;
                        _childMain.startColor = _visualData.topVfxColor.startColor;
                    }
                }
            }
        }
    }

    private void SetupSprites(TreeVisualData _visualData)
    {
        if (null != leafImage)
        {
            Sprite _spr = (null != _visualData.topSprites && 0 < _visualData.topSprites.Count) ? _visualData.topSprites[0] : null;
            bool _isValid = (null != _spr);
            leafImage.gameObject.SetActive(_isValid);
            if (true == _isValid)
            {
                leafImage.sprite = _spr;
            }
            else
            {
                Debug.LogWarning($"[TreeProp] '{_visualData.treeType}' ?무??Top Sprite가 비어?어 ??지?출력?????습?다.");
            }
        }

        if (null != trunkImage)
        {
            Sprite _spr = (null != _visualData.bottomSprites && 0 < _visualData.bottomSprites.Count) ? _visualData.bottomSprites[0] : null;
            bool _isValid = (null != _spr);
            trunkImage.gameObject.SetActive(_isValid);
            if (true == _isValid)
            {
                trunkImage.sprite = _spr;
            }
        }

        if (null != shieldLeafImage)
        {
            Sprite _spr = (null != _visualData.shieldTopSprites && 0 < _visualData.shieldTopSprites.Count) ? _visualData.shieldTopSprites[0] : null;
            bool _isValid = (null != _spr);
            shieldLeafImage.gameObject.SetActive(_isValid);
            if (true == _isValid)
            {
                shieldLeafImage.sprite = _spr;
            }
        }

        if (null != shieldTrunkImage)
        {
            Sprite _spr = (null != _visualData.shieldBottomSprites && 0 < _visualData.shieldBottomSprites.Count) ? _visualData.shieldBottomSprites[0] : null;
            bool _isValid = (null != _spr);
            shieldTrunkImage.gameObject.SetActive(_isValid);
            if (true == _isValid)
            {
                shieldTrunkImage.sprite = _spr;
            }
        }

        if (null != highlightLeafImage)
        {
            Sprite _spr = (null != _visualData.highlightTopSprites && 0 < _visualData.highlightTopSprites.Count) ? _visualData.highlightTopSprites[0] : null;
            bool _isValid = (null != _spr);
            highlightLeafImage.gameObject.SetActive(_isValid);
            if (true == _isValid)
            {
                highlightLeafImage.sprite = _spr;
            }
        }

        if (null != highlightTrunkImage)
        {
            Sprite _spr = (null != _visualData.highlightBottomSprites && 0 < _visualData.highlightBottomSprites.Count) ? _visualData.highlightBottomSprites[0] : null;
            bool _isValid = (null != _spr);
            highlightTrunkImage.gameObject.SetActive(_isValid);
            if (true == _isValid)
            {
                highlightTrunkImage.sprite = _spr;
            }
        }
    }

    public void SetDimColor(Color _dimColor)
    {
        dimmedColorMultiplier = _dimColor;
    }

    public void SetVisualState(TreeVisualState _state, float _duration = 0f)
    {
        if (false == isInitialized) Initialize();

        Color _baseTargetColor = Color.white;
        if (TreeVisualState.Locked == _state)
        {
            _baseTargetColor = Color.black;
        }
        else if (TreeVisualState.Unlocked_Idle == _state)
        {
            _baseTargetColor = dimmedColorMultiplier;
        }

        ApplyStateColorToImage(leafImage, null, Color.white, _baseTargetColor, _duration, ref colorTweens[0], 1f);
        ApplyStateColorToImage(trunkImage, null, Color.white, _baseTargetColor, _duration, ref colorTweens[1], 1f);
        
        ApplyStateColorToImage(shieldLeafImage, shieldLeafMat, originalShieldLeafColor, _baseTargetColor, _duration, ref colorTweens[2], shieldHdrIntensity);
        ApplyStateColorToImage(shieldTrunkImage, shieldTrunkMat, originalShieldTrunkColor, _baseTargetColor, _duration, ref colorTweens[3], shieldHdrIntensity);
        
        ApplyStateColorToImage(highlightLeafImage, highlightLeafMat, originalHighlightLeafColor, _baseTargetColor, _duration, ref colorTweens[4], highlightHdrIntensity);
        ApplyStateColorToImage(highlightTrunkImage, highlightTrunkMat, originalHighlightTrunkColor, _baseTargetColor, _duration, ref colorTweens[5], highlightHdrIntensity);
    }



    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
            appearTween = null;
        }

        transform.localScale = new Vector3(1f, 0.01f, 1f);

        Sequence _seq = DOTween.Sequence();
        if (0f < _delay)
        {
            _seq.SetDelay(_delay);
        }

        _seq.Append(transform.DOScaleY(1f, appearDuration).SetEase(appearEase));

        _seq.AppendCallback(cachedPlayChildParticles);

        appearTween = _seq;
    }

    private void PlayChildParticles()
    {
        if (null != childParticles)
        {
            for (int i = 0; i < childParticles.Length; i++)
            {
                childParticles[i].Play();
            }
        }
    }

    public void PlayHoverEffect()
    {
        if (null != hoverEffectParticle)
        {
            hoverEffectParticle.Play();
        }
    }

    public void StopHoverEffect()
    {
        if (null != hoverEffectParticle)
        {
            hoverEffectParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ApplyStateColorToImage(Image _image, Material _mat, Color _originalColor, Color _stateMultiplier, float _duration, ref Tween _tween, float _intensity)
    {
        if (null == _image || false == _image.gameObject.activeSelf) return;

        if (null != _tween && true == _tween.IsActive())
        {
            _tween.Kill();
            _tween = null;
        }

        Color _targetColor;

        if (null != _mat)
        {
            float _maxColorComponent = Mathf.Max(_originalColor.r, Mathf.Max(_originalColor.g, _originalColor.b));
            Color _ldrColor = _originalColor;
            
            if (0.001f < _maxColorComponent)
            {
                _ldrColor.r = Mathf.Clamp01(_originalColor.r / _maxColorComponent);
                _ldrColor.g = Mathf.Clamp01(_originalColor.g / _maxColorComponent);
                _ldrColor.b = Mathf.Clamp01(_originalColor.b / _maxColorComponent);
            }

            float _factor = Mathf.Pow(2, _intensity);
            Color _hdrColor = new Color(
                _ldrColor.r * _factor, 
                _ldrColor.g * _factor, 
                _ldrColor.b * _factor, 
                _ldrColor.a
            );

            _targetColor = _hdrColor * _stateMultiplier;

            if (_mat.HasProperty(HdrColorPropertyId))
            {
                _tween = _mat.DOColor(_targetColor, HdrColorPropertyId, _duration);
            }
            else if (_mat.HasProperty(BaseHdrColorPropertyId))
            {
                _tween = _mat.DOColor(_targetColor, BaseHdrColorPropertyId, _duration);
            }
            else if (_mat.HasProperty(ColorPropertyId))
            {
                _tween = _mat.DOColor(_targetColor, ColorPropertyId, _duration);
            }
            else
            {
                _mat.color = _targetColor;
            }
        }
        else
        {
            _targetColor = _originalColor * _stateMultiplier;
            _tween = _image.DOColor(_targetColor, _duration);
        }
    }

    private void OnDestroy()
    {
        if (null != appearTween && true == appearTween.IsActive()) { appearTween.Kill(); appearTween = null; }
        for (int i = 0; i < colorTweens.Length; i++)
        {
            if (null != colorTweens[i] && true == colorTweens[i].IsActive())
            {
                colorTweens[i].Kill();
                colorTweens[i] = null;
            }
        }

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
