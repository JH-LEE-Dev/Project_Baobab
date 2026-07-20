using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_PopupNav_TreeInfoView : MonoBehaviour
{
    [Header("Data Base")]
    [Tooltip("나무 비주얼 정보를 가져올 데이터베이스")]
    [SerializeField] private TreeVisualDataBase treeVisualDataBase;

    [Header("UI References")]
    [Tooltip("팝업 위치의 기준점이 될 자기 자신의 RectTransform")]
    [SerializeField] private RectTransform rectTransform;
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

    [Header("DOTween Settings (Placeholders)")]
    [Tooltip("추후 트위닝 연출에 사용될 설정값 자리")]
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private float disappearDuration = 0.3f;

    private Tween appearTween;
    private Tween disappearTween;

    [Header("Settings")]
    [Tooltip("서브지역 버튼에서 떨어질 간격 오프셋")]
    [SerializeField] private Vector2 anchorOffset = new Vector2(0f, 150f);

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

    private bool isVisible = false;

    public void Initialize()
    {
        CacheOriginalMaterialsAndColors();
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

    public void SetVisibility(bool _isVisible)
    {
        if (isVisible == _isVisible)
        {
            return;
        }

        isVisible = _isVisible;

        if (true == _isVisible)
        {
            gameObject.SetActive(true);
            
            if (null != appearTween && true == appearTween.IsActive())
            {
                appearTween.Kill();
            }

            // [TODO] 추후 DOTween 연출 작성
            // appearTween = ...
        }
        else
        {
            if (null != disappearTween && true == disappearTween.IsActive())
            {
                disappearTween.Kill();
            }

            // [TODO] 추후 DOTween 연출 작성
            // disappearTween = ...

            // 임시 즉시 완료
            OnDisappearMotionComplete();
        }
    }

    private void OnDisappearMotionComplete()
    {
        gameObject.SetActive(false);
    }

    public void ShowTreeInfo(ForestEnvironmentInfo _info, Transform _subRegionTransform)
    {
        if (null == treeVisualDataBase)
        {
            return;
        }

        if (null == _info.spawnTreeTypes || 0 == _info.spawnTreeTypes.Count)
        {
            return;
        }

        TreeVisualData _visualData = treeVisualDataBase.Get(_info.spawnTreeTypes[0].treeType);

        if (null != rectTransform && null != _subRegionTransform)
        {
            rectTransform.position = _subRegionTransform.position;
            rectTransform.anchoredPosition += anchorOffset;
        }

        UpdateVisuals(_visualData);
        SetVisibility(true);
    }

    private void UpdateVisuals(TreeVisualData _visualData)
    {
        if (null != leafImage)
        {
            bool _hasLeaf = (null != _visualData.topSprites && 0 < _visualData.topSprites.Count);
            leafImage.gameObject.SetActive(_hasLeaf);
            if (true == _hasLeaf)
            {
                leafImage.sprite = _visualData.topSprites[0];
            }
        }

        if (null != trunkImage)
        {
            bool _hasTrunk = (null != _visualData.bottomSprites && 0 < _visualData.bottomSprites.Count);
            trunkImage.gameObject.SetActive(_hasTrunk);
            if (true == _hasTrunk)
            {
                trunkImage.sprite = _visualData.bottomSprites[0];
            }
        }

        if (null != shieldLeafImage)
        {
            bool _hasShieldLeaf = (null != _visualData.shieldTopSprites && 0 < _visualData.shieldTopSprites.Count);
            shieldLeafImage.gameObject.SetActive(_hasShieldLeaf);
            if (true == _hasShieldLeaf)
            {
                shieldLeafImage.sprite = _visualData.shieldTopSprites[0];
            }
        }

        if (null != shieldTrunkImage)
        {
            bool _hasShieldTrunk = (null != _visualData.shieldBottomSprites && 0 < _visualData.shieldBottomSprites.Count);
            shieldTrunkImage.gameObject.SetActive(_hasShieldTrunk);
            if (true == _hasShieldTrunk)
            {
                shieldTrunkImage.sprite = _visualData.shieldBottomSprites[0];
            }
        }

        if (null != highlightLeafImage)
        {
            bool _hasHighlightLeaf = (null != _visualData.highlightTopSprites && 0 < _visualData.highlightTopSprites.Count);
            highlightLeafImage.gameObject.SetActive(_hasHighlightLeaf);
            if (true == _hasHighlightLeaf)
            {
                highlightLeafImage.sprite = _visualData.highlightTopSprites[0];
            }
        }

        if (null != highlightTrunkImage)
        {
            bool _hasHighlightTrunk = (null != _visualData.highlightBottomSprites && 0 < _visualData.highlightBottomSprites.Count);
            highlightTrunkImage.gameObject.SetActive(_hasHighlightTrunk);
            if (true == _hasHighlightTrunk)
            {
                highlightTrunkImage.sprite = _visualData.highlightBottomSprites[0];
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

        float _factor = Mathf.Pow(2f, _intensity);
        Color _hdrColor = new Color(_originalColor.r * _factor, _originalColor.g * _factor, _originalColor.b * _factor, _originalColor.a);
        _mat.SetColor("_Color", _hdrColor);
        _image.color = _hdrColor;
    }
}
