using System;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private float shieldHdrIntensityMultiplier = 1.0f;
    [SerializeField] private float highlightHdrIntensityMultiplier = 1.0f;

    private Material shieldLeafMat;
    private Material shieldTrunkMat;
    private Material highlightLeafMat;
    private Material highlightTrunkMat;

    private Color originalShieldLeafColor = Color.white;
    private Color originalShieldTrunkColor = Color.white;
    private Color originalHighlightLeafColor = Color.white;
    private Color originalHighlightTrunkColor = Color.white;

    private bool isInitialized = false;

    public void Initialize()
    {
        if (true == isInitialized)
        {
            return;
        }

        CacheOriginalMaterialsAndColors();
        isInitialized = true;
    }

    private void CacheOriginalMaterialsAndColors()
    {
        if (null != shieldLeafImage)
        {
            shieldLeafMat = Instantiate(shieldLeafImage.material);
            shieldLeafImage.material = shieldLeafMat;
            originalShieldLeafColor = shieldLeafImage.color;
        }

        if (null != shieldTrunkImage)
        {
            shieldTrunkMat = Instantiate(shieldTrunkImage.material);
            shieldTrunkImage.material = shieldTrunkMat;
            originalShieldTrunkColor = shieldTrunkImage.color;
        }

        if (null != highlightLeafImage)
        {
            highlightLeafMat = Instantiate(highlightLeafImage.material);
            highlightLeafImage.material = highlightLeafMat;
            originalHighlightLeafColor = highlightLeafImage.color;
        }

        if (null != highlightTrunkImage)
        {
            highlightTrunkMat = Instantiate(highlightTrunkImage.material);
            highlightTrunkImage.material = highlightTrunkMat;
            originalHighlightTrunkColor = highlightTrunkImage.color;
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

        ApplyHdrIntensity(shieldLeafImage, ref shieldLeafMat, ref originalShieldLeafColor, _visualData.shieldHDRIntensity * shieldHdrIntensityMultiplier);
        ApplyHdrIntensity(shieldTrunkImage, ref shieldTrunkMat, ref originalShieldTrunkColor, _visualData.shieldHDRIntensity * shieldHdrIntensityMultiplier);
        ApplyHdrIntensity(highlightLeafImage, ref highlightLeafMat, ref originalHighlightLeafColor, _visualData.highlightHDRIntensity * highlightHdrIntensityMultiplier);
        ApplyHdrIntensity(highlightTrunkImage, ref highlightTrunkMat, ref originalHighlightTrunkColor, _visualData.highlightHDRIntensity * highlightHdrIntensityMultiplier);
    }

    private void ApplyHdrIntensity(Image _image, ref Material _mat, ref Color _originalColor, float _intensity)
    {
        if (null == _image || false == _image.gameObject.activeSelf || null == _mat)
        {
            return;
        }

        // 빛 번짐이 너무 강해서 하얀 네모로 타버리는 현상(Blowout) 방지 (최대 3까지만 허용)
        float _safeIntensity = Mathf.Clamp(_intensity, 0f, 3f);
        float _factor = Mathf.Pow(2f, _safeIntensity);
        Color _hdrColor = new Color(_originalColor.r * _factor, _originalColor.g * _factor, _originalColor.b * _factor, _originalColor.a);
        _mat.SetColor("_Color", _hdrColor);
        _image.color = _hdrColor;
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
