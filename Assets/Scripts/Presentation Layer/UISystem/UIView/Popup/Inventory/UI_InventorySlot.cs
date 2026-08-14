using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem.CustomNumber;
using Coffee.UIEffects;

/// <summary>
/// 인벤토리의 개별 아이템 슬롯을 관리하는 클래스입니다.
/// 마우스 오버 시 팝업 이벤트를 발생시키고 수량 및 이미지를 업데이트합니다.
/// </summary>
public class UI_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
    [SerializeField] private Image uiImage;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private UIEffect uiEffect;

    [SerializeField] private Sprite emptySprite;

    [Header("Slot Count Settings")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color maxColor = Color.red;

    [Header("Rarity Effect Colors")]
    [SerializeField] private Color fascinatingColor = new Color(0.2f, 1.0f, 0.2f, 1.0f);
    [SerializeField] private Color advancedColor = new Color(0.2f, 0.6f, 1.0f, 1.0f);
    [SerializeField] private Color perfectColor = new Color(1.0f, 0.85f, 0.0f, 1.0f);

    public Action<UI_InventorySlot, IItemData, Vector2> enterSlot;
    public Action exitSlot;
    public Action<IInventorySlot> deleteItem;

    // //내부 의존성
    private IItemData showItemData;
    private IInventorySlot invSlotRef;
    private int showCnt = 0;
    private CurrencyFontHUD currencyFont;
    private int maxItemCntPerSlot = 99;
    private ShinyEffectComponent shinyEffectComponent;

    public IItemData ShowItemData => showItemData;
    public IInventorySlot InvSlotRef => invSlotRef;
    public int ShowCnt => showCnt;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        shinyEffectComponent = GetComponentInChildren<ShinyEffectComponent>();

        UpdateImage(null, Color.white);
        SetEffectActive(false);
        SetShinyEffectActive(false);
        
        if (null != uiImage && null != uiImage.sprite && true == uiImage.sprite.texture.isReadable)
            uiImage.alphaHitTestMinimumThreshold = 0.1f;

        currencyFont = GetComponentInChildren<CurrencyFontHUD>(true);

        if (null != currencyFont)
        {
            currencyFont.Initialize();
            currencyFont.SetMode(CurrencyFontAlignmentMode.Center);

            if (null != CameraFinder.Instance)
            {
                CameraFinder.Instance.HandleCameraFindingEvent -= ApplySorting;
                CameraFinder.Instance.HandleCameraFindingEvent += ApplySorting;
                
                // 만약 이미 카메라가 할당되어 있다면 즉시 실행
                if (null != CameraFinder.Instance.PPUiCamera)
                {
                    ApplySorting();
                }
            }
        }

        UpdateItemCount(0);
        
        if (null != omp)
            omp.Initialize();
    }

    private bool bNeedSorting = false;

    private void ApplySorting()
    {
        if (null == currencyFont) 
            return;

        if (true == gameObject.activeInHierarchy)
        {
            // 객체가 켜져 있다면 에러 없이 코루틴을 돌릴 수 있으므로 즉시 1프레임 대기 루틴 실행
            StartCoroutine(ApplySortingRoutine());
        }
        else
        {
            // 객체가 꺼져 있다면 코루틴 실행 시 에러가 나므로, 나중에 켜질 때 실행하도록 플래그만 저장
            bNeedSorting = true;
        }
    }

    private void OnEnable()
    {
        if (true == bNeedSorting)
        {
            StartCoroutine(ApplySortingRoutine());
        }
    }

    private System.Collections.IEnumerator ApplySortingRoutine()
    {
        bNeedSorting = false; 
        
        Canvas _canvas = currencyFont.GetComponent<Canvas>();
        
        int retryCount = 0;
        
        while (null != _canvas && 10 > retryCount)
        {
            if (null != _canvas.rootCanvas && null != _canvas.rootCanvas.worldCamera)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 10;
                _canvas.sortingLayerName = "HUD";

                if (true == _canvas.overrideSorting)
                {
                    yield break;
                }
            }
            
            retryCount++;
            yield return null; 
        }
    }

    private void ExecuteSorting()
    {
        if (null == currencyFont) return;
        
        Canvas _canvas = currencyFont.GetComponent<Canvas>();
        if (null != _canvas && null != _canvas.rootCanvas && null != _canvas.rootCanvas.worldCamera)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 10;
            _canvas.sortingLayerName = "HUD";
        }
    }

    public void ResetData()
    {
        if (null != invSlotRef)
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;

        UpdateImage(null, Color.white);
        SetEffectActive(false);
        SetShinyEffectActive(false);
        UpdateItemCount(0);

        invSlotRef = null;
        showItemData = null;
        maxItemCntPerSlot = 99;
    }

    public void UpdateItemCount(int _newCnt)
    {
        if (null == currencyFont)
            return;

        currencyFont.gameObject.SetActive(0 < _newCnt);

        // 아이템 개수가 1개 이상이 되어 텍스트가 켜지는 순간, 소팅 레이어를 적용합니다.
        // 코루틴이 포기한 이후에 활성화되더라도 여기서 확실하게 잡아줍니다.
        if (0 < _newCnt)
        {
            ExecuteSorting();
        }

        if (showCnt != _newCnt)
        {
            showCnt = _newCnt;
            currencyFont.SetNumber(_newCnt);
        }

        UpdateFontColor();
    }

    private void UpdateFontColor()
    {
        if (null == currencyFont)
            return;

        if (maxItemCntPerSlot <= showCnt)
            currencyFont.SetGlyphColor(maxColor);
        else
            currencyFont.SetGlyphColor(defaultColor);
    }

    public void UpdateImage(Sprite _sprite, Color _color)
    {
        if (null == uiImage)
            return;

        uiImage.enabled = null != _sprite;

        if (null == _sprite)
            uiImage.sprite = emptySprite;
        else
        {
            uiImage.sprite = _sprite;
            //uiImage.color = _color;
        }
    }

    public void UpdateBindSlotData(IInventorySlot _newSlot, int _maxCount = 99, bool _playInteraction = false)
    {
        maxItemCntPerSlot = _maxCount;

        if (null == _newSlot || null == _newSlot.itemData)
        {
            ResetData();
            return;
        }

        if (null != invSlotRef)
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;

        invSlotRef = _newSlot;
        showItemData = _newSlot.itemData;

        if (null != invSlotRef)
        {
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;
            invSlotRef.SlotUpdatedEvent += PlayItemInteraction;
        }

        UpdateImage(showItemData.sprite, Color.white);
        UpdateItemCount(invSlotRef.count);
        UpdateRarityEffect(showItemData);

        if (_playInteraction)
            PlayItemInteraction();
    }

    public void DisableRayCast()
    {
        if (null != uiImage)
            uiImage.raycastTarget = false;
    }
 
    public void SetEffectActive(bool _active)
    {
        if (null != uiEffect)
        {
            uiEffect.gameObject.SetActive(_active);
        }
    }

    public void SetShinyEffectActive(bool _active)
    {
        if (null != shinyEffectComponent)
        {
            shinyEffectComponent.UseShinyEffect = _active;
        }
    }
 
    public void SetEdgeColor(Color _color)
    {
        if (null != uiEffect)
        {
            uiEffect.edgeMode = EdgeMode.Shiny;
            uiEffect.edgeColor = _color;
        }
    }

    private void UpdateRarityEffect(IItemData _itemData)
    {
        if (null == _itemData)
        {
            SetEffectActive(false);
            SetShinyEffectActive(false);
            return;
        }

        if (_itemData is ILogItemData _logData)
        {
            LogState _state = _logData.logState;
            switch (_state)
            {
                case LogState.Fascinating:
                    SetEffectActive(true);
                    SetEdgeColor(fascinatingColor);
                    SetShinyEffectActive(true);
                    break;
                case LogState.Advanced:
                    SetEffectActive(true);
                    SetEdgeColor(advancedColor);
                    SetShinyEffectActive(true);
                    break;
                case LogState.Perfect:
                    SetEffectActive(true);
                    SetEdgeColor(perfectColor);
                    SetShinyEffectActive(true);
                    break;
                default:
                    SetEffectActive(false);
                    SetShinyEffectActive(false);
                    break;
            }
        }
        else
        {
            SetEffectActive(false);
            SetShinyEffectActive(false);
        }
    }

    public void SetLayer(string _layerName)
    {
        int _layer = LayerMask.NameToLayer(_layerName);
        if (-1 != _layer)
        {
            SetLayer(_layer);
        }
    }

    public void SetLayer(int _layer)
    {
        SetLayerRecursive(gameObject, _layer);
    }

    private void SetLayerRecursive(GameObject _obj, int _layer)
    {
        if (null == _obj)
        {
            return;
        }

        _obj.layer = _layer;
        int _childCount = _obj.transform.childCount;
        for (int i = 0; i < _childCount; i++)
        {
            Transform _child = _obj.transform.GetChild(i);
            if (null != _child)
            {
                SetLayerRecursive(_child.gameObject, _layer);
            }
        }
    }

    private void PlayItemInteraction()
    {
        if (null != omp)
            omp.Play("ItemInteraction", bReset: true);

        if (null != invSlotRef)
        {
            showItemData = invSlotRef.itemData;
            UpdateItemCount(invSlotRef.count);

            if (null == showItemData || 0 >= invSlotRef.count)
            {
                UpdateImage(null, Color.white);
                SetEffectActive(false);
                SetShinyEffectActive(false);
            }
            else
            {
                UpdateImage(showItemData.sprite, showItemData.color);
                UpdateRarityEffect(showItemData);
            }
        }
    }

    // //유니티 이벤트 함수 및 인터페이스 구현

    public virtual void OnPointerClick(PointerEventData _eventData)
    {
        if (null != deleteItem)
            deleteItem.Invoke(invSlotRef);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (null != enterSlot && null != uiImage)
            enterSlot.Invoke(this, showItemData, uiImage.rectTransform.position);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null != exitSlot)
            exitSlot.Invoke();
    }

    private void OnDestroy()
    {
        if (null != CameraFinder.Instance)
        {
            CameraFinder.Instance.HandleCameraFindingEvent -= ApplySorting;
        }

        ResetData();
    }
}
