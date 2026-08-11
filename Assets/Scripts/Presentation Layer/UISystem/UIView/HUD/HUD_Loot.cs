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
    [SerializeField] private HUD_LootTooltip tooltipUI; // 툴팁 UI 컴포넌트 (마우스 오버 시 설명 텍스트 표시)
    
    [Header("Settings")]
    [SerializeField] private GameObject lootPrefab;
    [SerializeField] private int maxLootCount = 5;
    [SerializeField] private Vector2 imageSize = new Vector2(100f, 100f); // 생성될 이미지의 크기

    [System.Serializable]
    public struct LootSpritePair
    {
        public LootType lootType;
        public Sprite sprite;
        public string tooltipLocKey; // 로컬라이징 텍스트 식별자 키 (예: "ITEM_WOOD_DESC")
    }

    [Header("Loot Sprite Binding")]
    [SerializeField] private LootSpritePair[] lootSpritePairs;
    
    [Header("Acquire Motion Settings")]
    [SerializeField] private float motionDuration = 0.6f;
    [SerializeField] private Vector3 maxScale = new Vector3(1.4f, 1.4f, 1f);
    [SerializeField] private Color flashColor = Color.white;
    
    private List<Image> lootImages = new List<Image>();
    private List<Image> lootOverlays = new List<Image>();
    private List<UI_RedDot> lootRedDots = new List<UI_RedDot>();
    private List<HUD_LootSlotTrigger> lootTriggers = new List<HUD_LootSlotTrigger>();
    private Material flashMaterial;
    private int currentLootIndex = 0;
    private Tween transitionTween;
    private LocalizationManager locManager;

    public void Initialize(LocalizationManager _locManager = null)
    {
        locManager = _locManager;
        
        if (null != tooltipUI)
        {
            tooltipUI.Initialize();
        }
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
            if (null == lootPrefab)
            {
                Debug.LogError("[HUD_Loot] Loot Prefab is not assigned.");
                return;
            }

            // 1. 메인 부모 및 원본 이미지 출력용 Image
            GameObject _newObj = Instantiate(lootPrefab, container);
            _newObj.name = "LootImage_" + i;
            
            Image _newImage = _newObj.GetComponent<Image>();
            if (null == _newImage)
            {
                _newImage = _newObj.AddComponent<Image>();
            }
            _newImage.raycastTarget = true; // 툴팁 마우스 이벤트를 받기 위해 true로 설정
            
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
            
            // 레드닷 찾기
            UI_RedDot _redDot = _newObj.GetComponentInChildren<UI_RedDot>(true);
            
            // 툴팁 호버 이벤트용 트리거 스크립트 자동 부착
            HUD_LootSlotTrigger _trigger = _newObj.GetComponent<HUD_LootSlotTrigger>();
            if (null == _trigger)
            {
                _trigger = _newObj.AddComponent<HUD_LootSlotTrigger>();
            }
            _trigger.Initialize(tooltipUI, _redDot);

            _newObj.SetActive(false);
            lootImages.Add(_newImage);
            lootOverlays.Add(_overlayImage);
            lootRedDots.Add(_redDot);
            lootTriggers.Add(_trigger);
        }
    }

    /// <summary>
    /// 전리품을 획득했을 때 호출되어 이미지를 교체하고 모션을 재생합니다.
    /// </summary>
    public void AcquireLoot(LootType _acquiredType, bool _playAnimation = true)
    {
        if (null == lootSpritePairs || 0 == lootImages.Count)
        {
            return;
        }

        // 바인딩된 배열에서 매칭되는 스프라이트 및 설명 ID 찾기
        Sprite _lootSprite = null;
        string _locKey = string.Empty;
        for (int i = 0; i < lootSpritePairs.Length; i++)
        {
            if (lootSpritePairs[i].lootType == _acquiredType)
            {
                _lootSprite = lootSpritePairs[i].sprite;
                _locKey = lootSpritePairs[i].tooltipLocKey;
                break;
            }
        }

        if (null == _lootSprite) return;

        // 중복 검사: 이미 화면에 켜져 있는 동일한 전리품이 있는지 확인
        for (int i = 0; i < lootImages.Count; i++)
        {
            if (true == lootImages[i].gameObject.activeInHierarchy && _lootSprite == lootImages[i].sprite)
            {
                // 동일한 전리품이 이미 있다면 새로 슬롯을 차지하지 않고, 기존 슬롯의 애니메이션만 다시 튕겨줍니다.
                if (true == _playAnimation)
                {
                    Image _existingOverlay = lootOverlays[i];
                    PlayAcquireMotion(lootImages[i], _existingOverlay);
                    
                    if (null != lootRedDots[i])
                    {
                        lootRedDots[i].Activate();
                    }
                    
                    if (null != lootTriggers[i])
                    {
                        lootTriggers[i].StartPulse();
                    }
                }
                return;
            }
        }

        Image _targetImage = lootImages[currentLootIndex];
        Image _overlayImage = lootOverlays[currentLootIndex];
        UI_RedDot _redDot = lootRedDots[currentLootIndex];
        HUD_LootSlotTrigger _targetTrigger = lootTriggers[currentLootIndex];
        
        if (null != _targetImage)
        {
            _targetImage.sprite = _lootSprite;
            _targetImage.gameObject.SetActive(true);

            // 로컬라이징 텍스트 조회 및 트리거 갱신
            string _descText = string.Empty;
            if (null != locManager && false == string.IsNullOrEmpty(_locKey))
            {
                _descText = locManager.GetText(_locKey);
            }
            
            // 텍스트를 못 찾았을 경우 키값 자체를 띄워 로컬라이징 문제인지 마우스 이벤트 문제인지 구분
            if (string.IsNullOrEmpty(_descText))
            {
                _descText = string.IsNullOrEmpty(_locKey) ? "No Loc Key" : _locKey;
            }
            
            _targetTrigger.SetDescription(_descText);

            // 오버레이 이미지에도 똑같은 스프라이트를 넣어주어 뼈대를 맞춤
            if (null != _overlayImage)
            {
                _overlayImage.sprite = _lootSprite;
            }
            
            if (null != _redDot)
            {
                _redDot.Activate();
            }
            
            if (null != _targetTrigger)
            {
                _targetTrigger.StartPulse();
            }
            
            if (true == _playAnimation)
            {
                PlayAcquireMotion(_targetImage, _overlayImage);
            }
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
    private int testLootIndex = 0;

    [NaughtyAttributes.Button("Test Acquire Loot (순차 획득)")]
    private void TestAcquireLootSequence()
    {
        if (null == lootSpritePairs || 0 == lootSpritePairs.Length)
        {
            Debug.LogWarning("바인딩된 Loot Type이 없습니다.");
            return;
        }

        // 등록된 LootType을 순차적으로 넘겨줍니다.
        LootType _type = lootSpritePairs[testLootIndex].lootType;
        AcquireLoot(_type);

        testLootIndex++;
        
        if (lootSpritePairs.Length <= testLootIndex)
        {
            testLootIndex = 0;
        }
    }
    #endregion
}
