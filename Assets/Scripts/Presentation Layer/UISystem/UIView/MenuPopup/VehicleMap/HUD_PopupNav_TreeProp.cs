using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HUD_PopupNav_TreeProp : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("나무 잎 이미지")]
    [SerializeField] private Image leafImage;
    [Tooltip("나무 기둥 이미지")]
    [SerializeField] private Image trunkImage;
    [Tooltip("실드 잎 이미지")]
    [SerializeField] private Image shieldLeafImage;
    [Tooltip("실드 기둥 이미지")]
    [SerializeField] private Image shieldTrunkImage;
    [Tooltip("하이라이트 잎 이미지")]
    [SerializeField] private Image highlightLeafImage;
    [Tooltip("하이라이트 기둥 이미지")]
    [SerializeField] private Image highlightTrunkImage;

    [Header("HDR Material Support")]
    [Tooltip("실드 잎/기둥에 적용할 고정 인텐시티 값 (DB 값 무시하고 이 값으로 덮어씀)")]
    [SerializeField] private float shieldHdrIntensityMultiplier = 1.0f;
    [Tooltip("하이라이트 잎/기둥에 적용할 고정 인텐시티 값 (DB 값 무시하고 이 값으로 덮어씀)")]
    [SerializeField] private float highlightHdrIntensityMultiplier = 1.0f;

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

    private static readonly int HdrColorPropertyId = Shader.PropertyToID("_HDRColor");

    private ParticleSystem[] childParticles;
    private Tween appearTween;

    private bool isInitialized = false;

    public void Initialize()
    {
        if (true == isInitialized)
        {
            return;
        }

        childParticles = GetComponentsInChildren<ParticleSystem>(true);
        CacheOriginalMaterialsAndColors();
        isInitialized = true;
    }

    private void CacheOriginalMaterialsAndColors()
    {
        if (null != shieldLeafImage && null != shieldLeafImage.material)
        {
            shieldLeafMat = Instantiate(shieldLeafImage.material);
            shieldLeafImage.material = shieldLeafMat;
            originalShieldLeafColor = shieldLeafMat.HasProperty(HdrColorPropertyId) ? shieldLeafMat.GetColor(HdrColorPropertyId) : shieldLeafImage.color;
        }

        if (null != shieldTrunkImage && null != shieldTrunkImage.material)
        {
            shieldTrunkMat = Instantiate(shieldTrunkImage.material);
            shieldTrunkImage.material = shieldTrunkMat;
            originalShieldTrunkColor = shieldTrunkMat.HasProperty(HdrColorPropertyId) ? shieldTrunkMat.GetColor(HdrColorPropertyId) : shieldTrunkImage.color;
        }

        if (null != highlightLeafImage && null != highlightLeafImage.material)
        {
            highlightLeafMat = Instantiate(highlightLeafImage.material);
            highlightLeafImage.material = highlightLeafMat;
            originalHighlightLeafColor = highlightLeafMat.HasProperty(HdrColorPropertyId) ? highlightLeafMat.GetColor(HdrColorPropertyId) : highlightLeafImage.color;
        }

        if (null != highlightTrunkImage && null != highlightTrunkImage.material)
        {
            highlightTrunkMat = Instantiate(highlightTrunkImage.material);
            highlightTrunkImage.material = highlightTrunkMat;
            originalHighlightTrunkColor = highlightTrunkMat.HasProperty(HdrColorPropertyId) ? highlightTrunkMat.GetColor(HdrColorPropertyId) : highlightTrunkImage.color;
        }
    }

    public void Setup(TreeVisualData _visualData)
    {
        if (false == isInitialized)
        {
            Initialize();
        }

        gameObject.SetActive(true);

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
                Debug.LogWarning($"[TreeProp] '{_visualData.treeType}' 나무의 Top Sprite가 비어있어 이미지를 출력할 수 없습니다.");
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

        ApplyHdrIntensity(shieldLeafImage, ref shieldLeafMat, ref originalShieldLeafColor, shieldHdrIntensityMultiplier);
        ApplyHdrIntensity(shieldTrunkImage, ref shieldTrunkMat, ref originalShieldTrunkColor, shieldHdrIntensityMultiplier);
        ApplyHdrIntensity(highlightLeafImage, ref highlightLeafMat, ref originalHighlightLeafColor, highlightHdrIntensityMultiplier);
        ApplyHdrIntensity(highlightTrunkImage, ref highlightTrunkMat, ref originalHighlightTrunkColor, highlightHdrIntensityMultiplier);
    }

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
        }

        transform.localScale = new Vector3(1f, 0.01f, 1f);

        Sequence _seq = DOTween.Sequence();
        if (_delay > 0f)
        {
            _seq.SetDelay(_delay);
        }

        _seq.Append(transform.DOScaleY(1f, appearDuration).SetEase(appearEase));

        _seq.AppendCallback(() => {
            if (null != childParticles)
            {
                for (int i = 0; i < childParticles.Length; i++)
                {
                    childParticles[i].Play();
                }
            }
        });

        appearTween = _seq;
    }

    private void ApplyHdrIntensity(Image _image, ref Material _mat, ref Color _originalColor, float _intensity)
    {
        if (null == _image || false == _image.gameObject.activeSelf || null == _mat)
        {
            return;
        }

        Color _hdrColor = new Color(
            _originalColor.r * _intensity, 
            _originalColor.g * _intensity, 
            _originalColor.b * _intensity, 
            _originalColor.a
        );
        
        if (_mat.HasProperty(HdrColorPropertyId))
        {
            _mat.SetColor(HdrColorPropertyId, _hdrColor);
        }
        else
        {
            _mat.SetColor("_Color", _hdrColor);
            _image.color = _hdrColor;
        }
    }

    private void OnDestroy()
    {
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
