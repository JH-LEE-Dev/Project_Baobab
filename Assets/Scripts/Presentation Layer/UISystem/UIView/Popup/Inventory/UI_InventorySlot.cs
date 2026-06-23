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
        
        if (null != uiImage && null != uiImage.sprite && true == uiImage.sprite.texture.isReadable)
            uiImage.alphaHitTestMinimumThreshold = 0.1f;

        currencyFont = GetComponentInChildren<CurrencyFontHUD>();

        if (null != currencyFont)
        {
            currencyFont.Initialize();
            currencyFont.SetMode(CurrencyFontAlignmentMode.Center);
        }

        UpdateItemCount(0);
        
        if (null != omp)
            omp.Initialize();
    }

    public void ResetData()
    {
        if (null != invSlotRef)
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;

        UpdateImage(null, Color.white);
        SetEffectActive(false);
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

        if (showCnt >= maxItemCntPerSlot)
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

        if (null != shinyEffectComponent)
            shinyEffectComponent.UseShinyEffect = null != _sprite;
    }

    public void UpdateBindSlotData(IInventorySlot _newSlot, int _maxCount = 99, bool _playInteraction = false)
    {
        maxItemCntPerSlot = _maxCount;

        if (null == _newSlot.itemData)
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
                    break;
                case LogState.Advanced:
                    SetEffectActive(true);
                    SetEdgeColor(advancedColor);
                    break;
                case LogState.Perfect:
                    SetEffectActive(true);
                    SetEdgeColor(perfectColor);
                    break;
                default:
                    SetEffectActive(false);
                    break;
            }
        }
        else
        {
            SetEffectActive(false);
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
        ResetData();
    }
}
