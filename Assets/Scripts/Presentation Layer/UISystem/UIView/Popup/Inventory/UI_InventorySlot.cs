using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem.CustomNumber;
using Coffee.UIEffects;
using DG.Tweening;
using DG.Tweening.Core;

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
    [SerializeField] private UIEffect slotImgEffect;

    [SerializeField] private Sprite emptySprite;

    [Header("Slot Count Settings")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color maxColor = Color.red;

    [Header("Rarity Effect Colors")]
    [SerializeField] private Color fascinatingColor = new Color(0.2f, 1.0f, 0.2f, 1.0f);
    [SerializeField] private Color advancedColor = new Color(0.2f, 0.6f, 1.0f, 1.0f);
    [SerializeField] private Color perfectColor = new Color(1.0f, 0.85f, 0.0f, 1.0f);

    [Header("Rainbow Effect Settings")]
    [SerializeField] private bool useRainbowCycle = true;
    [SerializeField] private float rainbowSpeed = 0.5f;
    [SerializeField] private float rainbowSaturation = 0.85f;
    [SerializeField] private float rainbowBrightness = 1.0f;

    [Header("Swap Outline Blink Settings")]
    [Tooltip("아웃라인 점멸 시 최소 알파 (0: 완전 소등, 0.25: 은은한 잔상 유지)")]
    [SerializeField] private float outlineBlinkMinAlpha = 0.25f;
    [Tooltip("아웃라인 점멸 1회 주기 시간 (초)")]
    [SerializeField] private float outlineBlinkDuration = 0.65f;

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
    private bool isRainbowActive = false;
    private float currentRainbowHue = 0.0f;

    // 스마트 스왑 아웃라인 점멸 제어 및 무할당(Zero GC) 캐싱
    private Tween outlineBlinkTween = null;
    private bool bOutlineActive = false;
    private Color baseOutlineColor = Color.red;
    private bool isOutlineColorCached = false;
    private DOGetter<Color> getOutlineColor;
    private DOSetter<Color> setOutlineColor;

    private bool bNeedSorting = false;
    private Coroutine sortingCoroutine = null;

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
        SetSlotOutlineActive(false);
        
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

    private void ApplySorting()
    {
        if (null == currencyFont) 
            return;

        if (true == gameObject.activeInHierarchy)
        {
            StopSortingCoroutine();
            sortingCoroutine = StartCoroutine(ApplySortingRoutine());
        }
        else
        {
            bNeedSorting = true;
        }
    }

    private System.Collections.IEnumerator ApplySortingRoutine()
    {
        bNeedSorting = false; 
        
        Canvas _canvas = currencyFont.GetComponent<Canvas>();
        int _retryCount = 0;
        
        while (null != _canvas && 10 > _retryCount)
        {
            ApplyCanvasSortingInternal(_canvas);

            if (true == _canvas.overrideSorting)
            {
                sortingCoroutine = null;
                yield break;
            }
            
            _retryCount++;
            yield return null; 
        }

        sortingCoroutine = null;
    }

    private void ExecuteSorting()
    {
        if (null == currencyFont)
            return;
        
        Canvas _canvas = currencyFont.GetComponent<Canvas>();
        ApplyCanvasSortingInternal(_canvas);
    }

    private static void ApplyCanvasSortingInternal(Canvas _canvas)
    {
        if (null != _canvas && null != _canvas.rootCanvas && null != _canvas.rootCanvas.worldCamera)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 10;
            _canvas.sortingLayerName = "HUD";
        }
    }

    private void StopSortingCoroutine()
    {
        if (null != sortingCoroutine)
        {
            StopCoroutine(sortingCoroutine);
            sortingCoroutine = null;
        }
    }

    public void ResetData()
    {
        if (null != invSlotRef)
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;

        isRainbowActive = false;
        UpdateImage(null, Color.white);
        SetEffectActive(false);
        SetShinyEffectActive(false);
        SetSlotOutlineActive(false);
        UpdateItemCount(0);

        invSlotRef = null;
        showItemData = null;
        maxItemCntPerSlot = 99;
    }

    /// <summary>
    /// 스마트 스왑 대상 슬롯임을 나타내는 SlotImg 아웃라인을 켜거나 끕니다.
    /// (인디케이터는 UI 최상단 단일 공동 프리팹에서 제어되므로 슬롯은 아웃라인만 전담)
    /// </summary>
    /// <param name="_active">노출 여부</param>
    /// <param name="_immediate">애니메이션 없이 즉시 상태를 바꿀지 여부 (아웃라인 즉시 토글)</param>
    public void SetSwapIndicator(bool _active, bool _immediate = false)
    {
        SetSlotOutlineActive(_active, _immediate);
    }

    /// <summary>
    /// SlotImg의 UIEffect 컴포넌트를 활성화하고 부드러운 펄스 점멸(Blink Loop) 애니메이션을 재생하거나 종료합니다.
    /// </summary>
    public void SetSlotOutlineActive(bool _active, bool _immediate = false)
    {
        EnsureSlotImgEffectBound();
        CacheBaseOutlineColorIfNeeded();

        // 이미 같은 상태로 점멸 중이면 아무것도 하지 않는다. 교체 제안은 "지금 보이는 개수"가 바뀔
        // 때마다 갱신되므로(나무가 쓰러지는 동안 초당 여러 번) 매번 트윈을 새로 만들면 알파가 계속
        // 기준색으로 스냅되고, 점멸 주기가 한 번도 완주하지 못한 채 덜컥거린다.
        if (true == _active && true == bOutlineActive && false == _immediate
            && null != outlineBlinkTween && true == outlineBlinkTween.IsActive())
        {
            return;
        }

        KillOutlineBlinkTween();

        if (null == slotImgEffect)
        {
            bOutlineActive = false;
            return;
        }

        bOutlineActive = _active;

        if (true == _active)
        {
            slotImgEffect.enabled = true;
            slotImgEffect.shadowColor = baseOutlineColor;

            if (false == _immediate)
            {
                Color _targetMinColor = baseOutlineColor;
                _targetMinColor.a = outlineBlinkMinAlpha;

                outlineBlinkTween = DOTween.To(getOutlineColor, setOutlineColor, _targetMinColor, outlineBlinkDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(gameObject);
            }
        }
        else
        {
            slotImgEffect.shadowColor = baseOutlineColor;
            slotImgEffect.enabled = false;
        }
    }

    private void CacheBaseOutlineColorIfNeeded()
    {
        if (true == isOutlineColorCached)
            return;

        if (null != slotImgEffect)
        {
            baseOutlineColor = slotImgEffect.shadowColor;
            isOutlineColorCached = true;
        }
    }

    private Color GetOutlineShadowColor()
    {
        if (null != slotImgEffect)
            return slotImgEffect.shadowColor;

        return baseOutlineColor;
    }

    private void SetOutlineShadowColor(Color _color)
    {
        if (null != slotImgEffect)
            slotImgEffect.shadowColor = _color;
    }

    private void KillOutlineBlinkTween()
    {
        if (null != outlineBlinkTween && true == outlineBlinkTween.IsActive())
        {
            outlineBlinkTween.Kill();
        }

        outlineBlinkTween = null;
    }

    private void EnsureSlotImgEffectBound()
    {
        if (null != slotImgEffect)
            return;

        Transform _slotImgTrans = transform.Find("Visual/SlotImg");
        if (null == _slotImgTrans)
            _slotImgTrans = transform.Find("SlotImg");

        if (null != _slotImgTrans)
        {
            slotImgEffect = _slotImgTrans.GetComponent<UIEffect>();
        }
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

        if (_newCnt != showCnt)
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

    public void UpdateImage(Sprite _sprite, Color _color = default)
    {
        if (null == uiImage)
            return;

        uiImage.enabled = null != _sprite;

        if (null == _sprite)
            uiImage.sprite = emptySprite;
        else
            uiImage.sprite = _sprite;
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

        if (true == _playInteraction)
            PlayItemInteraction();
    }

    public void DisableRayCast()
    {
        if (null != uiImage)
            uiImage.raycastTarget = false;
    }

    public void SetEffectActive(bool _active)
    {
        if (false == _active)
        {
            isRainbowActive = false;
        }

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
        isRainbowActive = false;

        if (null != uiEffect)
        {
            uiEffect.edgeMode = EdgeMode.Shiny;
            uiEffect.edgeColor = _color;
        }
    }

    public void SetRainbowEdge(bool _active)
    {
        isRainbowActive = _active;

        if (true == _active && null != uiEffect)
        {
            uiEffect.edgeMode = EdgeMode.Shiny;
            Color rainbowColor = Color.HSVToRGB(currentRainbowHue, rainbowSaturation, rainbowBrightness);
            uiEffect.edgeColor = rainbowColor;
        }
    }

    public void UpdateRarityEffect(IItemData _itemData)
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
                    if (true == useRainbowCycle)
                    {
                        SetRainbowEdge(true);
                    }
                    else
                    {
                        SetEdgeColor(perfectColor);
                    }
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
        for (int _i = 0; _childCount > _i; _i++)
        {
            Transform _child = _obj.transform.GetChild(_i);
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
                // 아이템 스프라이트는 이미 색이 입혀진 그림이라 틴트를 걸지 않는다.
                // (황금/다이아/무지개 원목에 나무 종류 색을 곱하면 색이 죽는다)
                UpdateImage(showItemData.sprite, Color.white);
                UpdateRarityEffect(showItemData);
            }
        }
    }

    // //유니티 이벤트 함수 및 인터페이스 구현 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Awake()
    {
        getOutlineColor = GetOutlineShadowColor;
        setOutlineColor = SetOutlineShadowColor;

        EnsureSlotImgEffectBound();
        CacheBaseOutlineColorIfNeeded();
        SetSlotOutlineActive(false);
    }

    private void OnEnable()
    {
        if (true == bNeedSorting)
        {
            StartCoroutine(ApplySortingRoutine());
        }

        // 비활성화되는 동안 점멸 트윈이 죽었으므로, 아웃라인이 켜져 있던 슬롯이면 다시 이어 붙인다.
        if (true == bOutlineActive)
        {
            SetSlotOutlineActive(true);
        }
    }

    private void Update()
    {
        if (true == isRainbowActive && null != uiEffect)
        {
            currentRainbowHue = Mathf.Repeat(currentRainbowHue + Time.deltaTime * rainbowSpeed, 1.0f);
            Color rainbowColor = Color.HSVToRGB(currentRainbowHue, rainbowSaturation, rainbowBrightness);
            uiEffect.edgeColor = rainbowColor;
        }
    }

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

    private void OnDisable()
    {
        StopSortingCoroutine();
        KillOutlineBlinkTween();

        // 트윈이 중간 알파에서 죽으므로 기준색으로 되돌려 둔다. 그대로 두면 이 슬롯이 다시 켜질 때
        // 흐릿하게 굳은 아웃라인이 남는다. (켜져 있었다는 사실은 bOutlineActive가 들고 OnEnable이 잇는다)
        if (null != slotImgEffect && true == isOutlineColorCached)
        {
            slotImgEffect.shadowColor = baseOutlineColor;
        }
    }

    private void OnDestroy()
    {
        StopSortingCoroutine();
        KillOutlineBlinkTween();

        if (null != CameraFinder.Instance)
        {
            CameraFinder.Instance.HandleCameraFindingEvent -= ApplySorting;
        }

        ResetData();
    }
}
