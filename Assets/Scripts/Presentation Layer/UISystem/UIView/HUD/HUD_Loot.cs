using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 획득한 전리품을 순차적으로 화면에 노출시켜주는 HUD 컴포넌트입니다.
/// </summary>
public class HUD_Loot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform container;      // Horizontal Layout Group이 적용된 부모 객체
    [SerializeField] private CanvasGroup rootCanvasGroup; // 트랜지션 페이드(Fade) 처리를 위한 캔버스 그룹
    
    [Header("Settings")]
    [SerializeField] private int maxLootCount = 5;
    [SerializeField] private Vector2 imageSize = new Vector2(100f, 100f); // 생성될 이미지의 크기
    
    [Header("Acquire Motion Settings")]
    [SerializeField] private float motionDuration = 0.6f;
    [SerializeField] private Vector3 maxScale = new Vector3(1.4f, 1.4f, 1f);
    [SerializeField] private Color flashColor = Color.white;

    private List<Image> lootImages = new List<Image>();
    private Material flashMaterial;
    private int currentLootIndex = 0;
    
    private Tween transitionTween;

    public void Initialize()
    {
        if (null == container)
        {
            return;
        }

        if (null == rootCanvasGroup)
        {
            rootCanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (null == rootCanvasGroup)
            {
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 이미지의 본연의 색상을 무시하고, 알파(투명도) 실루엣만 따와서 단색(하얀색)으로 칠해주는 셰이더 적용
        Shader _textShader = Shader.Find("GUI/Text Shader");
        if (null != _textShader)
        {
            flashMaterial = new Material(_textShader);
        }

        // 최대 전리품 개수만큼 코드로 메인 이미지와 오버레이 이미지를 직접 생성
        for (int i = 0; i < maxLootCount; i++)
        {
            // 1. 메인 부모 및 원본 이미지 출력용 Image
            GameObject _newObj = new GameObject("LootImage_" + i);
            _newObj.transform.SetParent(container, false);
            
            Image _newImage = _newObj.AddComponent<Image>();
            _newImage.raycastTarget = false; 
            
            RectTransform _rect = _newObj.GetComponent<RectTransform>();
            if (null != _rect)
            {
                _rect.sizeDelta = imageSize;
            }

            // 2. 하얗게 번쩍일 플래시 오버레이용 Image (자식 객체)
            GameObject _overlayObj = new GameObject("FlashOverlay");
            _overlayObj.transform.SetParent(_newObj.transform, false);
            
            Image _overlayImage = _overlayObj.AddComponent<Image>();
            _overlayImage.raycastTarget = false;
            
            if (null != flashMaterial)
            {
                _overlayImage.material = flashMaterial;
            }

            RectTransform _overlayRect = _overlayImage.rectTransform;
            _overlayRect.anchorMin = Vector2.zero;
            _overlayRect.anchorMax = Vector2.one;
            _overlayRect.sizeDelta = Vector2.zero;
            _overlayRect.anchoredPosition = Vector2.zero;

            // 오버레이는 평소에 투명하도록 세팅
            Color _clearColor = flashColor;
            _clearColor.a = 0f;
            _overlayImage.color = _clearColor;
            
            _newObj.SetActive(false);
            lootImages.Add(_newImage);
        }
    }

    /// <summary>
    /// 전리품을 획득했을 때 호출되어 이미지를 교체하고 모션을 재생합니다.
    /// </summary>
    public void AcquireLoot(Sprite _lootSprite)
    {
        if (null == _lootSprite || 0 == lootImages.Count)
        {
            return;
        }

        Image _targetImage = lootImages[currentLootIndex];
        
        if (null != _targetImage)
        {
            _targetImage.sprite = _lootSprite;
            _targetImage.gameObject.SetActive(true);

            // 오버레이 이미지에도 똑같은 스프라이트를 넣어주어 뼈대를 맞춤
            Image _overlayImage = _targetImage.transform.GetChild(0).GetComponent<Image>();
            if (null != _overlayImage)
            {
                _overlayImage.sprite = _lootSprite;
            }
            
            PlayAcquireMotion(_targetImage, _overlayImage);
        }

        currentLootIndex++;
        
        if (maxLootCount <= currentLootIndex)
        {
            currentLootIndex = 0; 
        }
    }
    
    private void PlayAcquireMotion(Image _target, Image _overlay)
    {
        if (null == _target || null == _overlay)
        {
            return;
        }

        // 1. 기존 트윈 강제 종료 및 초기화
        _target.transform.DOKill();
        _overlay.DOKill();
        
        _target.transform.localScale = Vector3.one;
        
        Color _clearColor = flashColor;
        _clearColor.a = 0f;
        _overlay.color = _clearColor; 
        
        Sequence _seq = DOTween.Sequence();
        
        // 2. 쫀득한 뽀잉 스케일 연출 (OutQuad -> OutElastic)
        _seq.Insert(0f, _target.transform.DOScale(maxScale, motionDuration * 0.3f).SetEase(Ease.OutQuad));
        _seq.Insert(motionDuration * 0.3f, _target.transform.DOScale(Vector3.one, motionDuration * 0.7f).SetEase(Ease.OutElastic));
        
        // 3. 메인 이미지 색상을 바꾸는 게 아니라, 위에 덮인 오버레이 이미지의 알파값(투명도)을 Fade In/Out 하여 찰나의 순간 하얗게 빛나게 덮어줌
        _seq.Insert(0f, _overlay.DOFade(1f, motionDuration * 0.2f).SetEase(Ease.OutFlash));
        _seq.Insert(motionDuration * 0.2f, _overlay.DOFade(0f, motionDuration * 0.8f).SetEase(Ease.InQuad));
        
        _seq.Play();
    }

    /// <summary>
    /// UIView_HUD의 HUDGoDown에 대응하는 함수입니다.
    /// </summary>
    public void OnHUDGoDown()
    {
        if (null == rootCanvasGroup)
        {
            return;
        }
        
        if (null != transitionTween && true == transitionTween.IsActive())
        {
            transitionTween.Kill();
        }
        
        // OMP를 대체하여 DOTween으로 페이드아웃 처리
        transitionTween = rootCanvasGroup.DOFade(0f, 0.3f);
    }

    /// <summary>
    /// UIView_HUD의 HUDGoUp에 대응하는 함수입니다.
    /// </summary>
    public void OnHUDGoUp()
    {
        if (null == rootCanvasGroup)
        {
            return;
        }
        
        if (null != transitionTween && true == transitionTween.IsActive())
        {
            transitionTween.Kill();
        }
        
        // OMP를 대체하여 DOTween으로 페이드인 처리
        transitionTween = rootCanvasGroup.DOFade(1f, 0.3f);
    }

    #region Editor Test Logic
    [Header("Test Mode")]
    [SerializeField] private Sprite[] testSprites;
    private int testSpriteIndex = 0;

    [NaughtyAttributes.Button("Test Acquire Loot (순차 획득)")]
    private void TestAcquireLootSequence()
    {
        if (null == testSprites || 0 == testSprites.Length)
        {
            Debug.LogWarning("테스트용 스프라이트(testSprites)를 인스펙터에 하나 이상 등록해주세요.");
            return;
        }

        // 등록된 스프라이트를 순차적으로 넘겨줍니다.
        Sprite _sprite = testSprites[testSpriteIndex];
        AcquireLoot(_sprite);

        testSpriteIndex++;
        
        if (testSprites.Length <= testSpriteIndex)
        {
            testSpriteIndex = 0;
        }
    }
    #endregion
}
