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

    [Header("Grade Frame / Badge")]
    [Tooltip("슬롯 타일(SlotImg) 위에 겹쳐 그리는 등급 테두리 레이어. 타일의 어두운 테두리 픽셀에만 연출이 입혀지고 안쪽은 투명이다.")]
    [SerializeField] private Image gradeFrameImage;
    [Tooltip("타일 구석에 붙는 등급 배지. 등급마다 실루엣이 달라 색을 못 알아봐도 구분된다.")]
    [SerializeField] private Image gradeBadgeImage;
    [Tooltip("Fascinating = 금")]
    [SerializeField] private Material frameMaterialGold;
    [Tooltip("Advanced = 다이아")]
    [SerializeField] private Material frameMaterialDiamond;
    [Tooltip("Perfect = 프리즘")]
    [SerializeField] private Material frameMaterialPrism;
    [Tooltip("슬롯 타일 위, 원목 아래에 얹는 보석 커팅(테이블 + 면 링) 레이어.")]
    [SerializeField] private Image gradeTileFxImage;
    [SerializeField] private Material tileFxMaterialGold;
    [SerializeField] private Material tileFxMaterialDiamond;
    [SerializeField] private Material tileFxMaterialPrism;
    [SerializeField] private Sprite badgeSpriteGold;
    [SerializeField] private Sprite badgeSpriteDiamond;
    [SerializeField] private Sprite badgeSpritePrism;

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

    // 스마트 스왑 아웃라인 점멸 제어 및 무할당(Zero GC) 캐싱
    private Tween outlineBlinkTween = null;
    private bool bOutlineActive = false;
    private Color baseOutlineColor = Color.red;
    private bool isOutlineColorCached = false;
    private DOGetter<Color> getOutlineColor;
    private DOSetter<Color> setOutlineColor;

    private bool bNeedSorting = false;
    private Coroutine sortingCoroutine = null;

    // 슬롯 갱신(아이템 습득/삭제마다 전체 슬롯이 다시 읽힌다)에서 같은 값을 반복 처리하지 않기 위한 캐시
    private Canvas currencyCanvas;
    private bool isSortingApplied = false;
    private bool isFontColorApplied = false;
    private bool lastFontColorIsMax = false;
    private bool isRarityApplied = false;
    private IItemData lastRarityData;
    private int lastRarityStateKey = -1;
    private Material appliedFrameMaterial;

    // 등급 획득 버스트: 셰이더가 Image.color의 R 채널(1 - 세기)로 읽는다.
    private Tween gradeBurstTween = null;
    private float gradeBurstValue = 0f;
    private DOGetter<float> getGradeBurst;
    private DOSetter<float> setGradeBurst;
    private const float GradeBurstDuration = 0.8f;

    public IItemData ShowItemData => showItemData;
    public IInventorySlot InvSlotRef => invSlotRef;
    public int ShowCnt => showCnt;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        shinyEffectComponent = GetComponentInChildren<ShinyEffectComponent>();

        isRarityApplied = false;
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

        // 카메라가 (다시) 잡혔으므로 이미 적용했다는 기록은 믿지 않고 다시 적용한다.
        isSortingApplied = false;

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

        Canvas _canvas = GetCurrencyCanvas();
        int _retryCount = 0;

        while (null != _canvas && 10 > _retryCount)
        {
            if (true == ApplyCanvasSortingInternal(_canvas))
                isSortingApplied = true;

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

        // 한 번 적용되면 카메라가 다시 잡히기 전(ApplySorting)까지는 같은 값을 또 쓸 필요가 없다.
        if (true == isSortingApplied)
            return;

        if (true == ApplyCanvasSortingInternal(GetCurrencyCanvas()))
            isSortingApplied = true;
    }

    private Canvas GetCurrencyCanvas()
    {
        if (null == currencyCanvas && null != currencyFont)
            currencyCanvas = currencyFont.GetComponent<Canvas>();

        return currencyCanvas;
    }

    private static bool ApplyCanvasSortingInternal(Canvas _canvas)
    {
        if (null != _canvas && null != _canvas.rootCanvas && null != _canvas.rootCanvas.worldCamera)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 10;
            _canvas.sortingLayerName = "HUD";
            return true;
        }

        return false;
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

        isRarityApplied = false;
        SetGradeVisual(null, null);
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

            // SetNumber가 글리프 색을 기본색으로 되돌리므로 아래 UpdateFontColor가 다시 칠해야 한다.
            isFontColorApplied = false;
        }

        UpdateFontColor();
    }

    private void UpdateFontColor()
    {
        if (null == currencyFont)
            return;

        bool _isMax = maxItemCntPerSlot <= showCnt;

        // 개수도, 최대치 여부도 그대로면 글리프 풀 전체를 다시 칠할 필요가 없다.
        if (true == isFontColorApplied && _isMax == lastFontColorIsMax)
            return;

        if (true == _isMax)
            currencyFont.SetGlyphColor(maxColor);
        else
            currencyFont.SetGlyphColor(defaultColor);

        isFontColorApplied = true;
        lastFontColorIsMax = _isMax;
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

    public void UpdateBindSlotData(IInventorySlot _newSlot, int _maxCount = 99, bool _playInteraction = false, bool _allowGradeBurst = false)
    {
        maxItemCntPerSlot = _maxCount;

        if (null == _newSlot || null == _newSlot.itemData)
        {
            ResetData();
            return;
        }

        // 같은 슬롯을 다시 읽는 경우(아이템 습득/삭제마다 전체 슬롯이 갱신된다)에는 이미 구독돼 있다.
        if (false == ReferenceEquals(invSlotRef, _newSlot))
        {
            if (null != invSlotRef)
                invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;

            invSlotRef = _newSlot;
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;
            invSlotRef.SlotUpdatedEvent += PlayItemInteraction;
        }

        // 개수가 0이 된 슬롯은 아이템 데이터가 남아 있어도 비어 있는 슬롯이다. (InventorySlot.TakeOneItem은 개수만 줄이고,
        // 데이터는 호출한 쪽이 ItemDeleted로 비운다. 그 사이에 UI가 전체 갱신을 하면 빠진 아이템과 등급 연출이 되살아난다)
        if (0 >= _newSlot.count)
        {
            showItemData = _newSlot.itemData;
            UpdateItemCount(0);
            ClearEmptyCountVisual();
            return;
        }

        // 인벤토리 갱신은 SlotUpdatedEvent보다 먼저 슬롯을 다시 읽으므로, 버스트 여부는 갱신 전 상태와 비교해 여기서 정한다.
        // (등급 아이템이 새로 들어왔거나 등급이 바뀐 경우, 또는 개수가 늘어난 경우)
        bool _burst = false;
        if (true == _allowGradeBurst)
        {
            bool _gradeChanged = false == isRarityApplied
                || false == ReferenceEquals(lastRarityData, _newSlot.itemData)
                || lastRarityStateKey != GetRarityStateKey(_newSlot.itemData);
            _burst = _gradeChanged || showCnt < _newSlot.count;
        }

        showItemData = _newSlot.itemData;

        UpdateImage(showItemData.sprite, Color.white);
        UpdateItemCount(invSlotRef.count);
        UpdateRarityEffect(showItemData);

        if (true == _burst)
            PlayGradeBurst();

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

    // 등급 연출은 타일 본체를 건드리지 않고, 테두리 레이어와 구석 배지로만 표현한다.
    // 등급 -> 연출 매핑은 나무 보석과 같다(Fascinating = 금, Advanced = 다이아, Perfect = 프리즘).
    // null이면 테두리와 배지를 모두 끈다(일반 원목, 빈 슬롯).
    private void SetGradeVisual(Material _frameMaterial, Sprite _badgeSprite, Material _tileFxMaterial = null)
    {
        if (ReferenceEquals(appliedFrameMaterial, _frameMaterial))
            return;

        appliedFrameMaterial = _frameMaterial;

        if (null != gradeFrameImage)
        {
            if (null != _frameMaterial)
                gradeFrameImage.material = _frameMaterial;

            gradeFrameImage.enabled = null != _frameMaterial;
        }

        if (null != gradeTileFxImage)
        {
            if (null != _tileFxMaterial)
                gradeTileFxImage.material = _tileFxMaterial;

            gradeTileFxImage.enabled = null != _tileFxMaterial;
        }

        if (null != gradeBadgeImage)
        {
            // 배지는 등급마다 보석 점 개수가 달라 폭이 다르므로 스프라이트의 픽셀 크기에 맞춘다.
            // SetNativeSize는 캔버스 기준 PPU에 따라 배율이 곱해지므로 쓰지 않는다(캔버스 1유닛 = 1픽셀).
            if (null != _badgeSprite)
            {
                gradeBadgeImage.sprite = _badgeSprite;
                gradeBadgeImage.rectTransform.sizeDelta = _badgeSprite.rect.size;
            }

            gradeBadgeImage.enabled = null != _badgeSprite;
        }
    }

    private int GetRarityStateKey(IItemData _itemData)
    {
        if (_itemData is ILogItemData _keyLogData)
            return (int)_keyLogData.logState;

        return -1;
    }

    private float GetGradeBurstValue()
    {
        return gradeBurstValue;
    }

    private void SetGradeBurstValue(float _value)
    {
        gradeBurstValue = _value;
        Color _color = new Color(1f - _value, 1f, 1f, 1f);

        if (null != gradeFrameImage)
            gradeFrameImage.color = _color;

        if (null != gradeTileFxImage)
            gradeTileFxImage.color = _color;
    }

    private void KillGradeBurst()
    {
        if (null != gradeBurstTween)
        {
            gradeBurstTween.Kill();
            gradeBurstTween = null;
        }

        SetGradeBurstValue(0f);
    }

    // 등급 로그가 슬롯에 들어온 순간 프레임과 타일이 한 번 번쩍인다.
    private void PlayGradeBurst()
    {
        if (null == gradeFrameImage || false == gradeFrameImage.enabled || false == isActiveAndEnabled)
            return;

        if (null != gradeBurstTween)
            gradeBurstTween.Kill();

        SetGradeBurstValue(1f);
        gradeBurstTween = DOTween.To(getGradeBurst, setGradeBurst, 0f, GradeBurstDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    public void UpdateRarityEffect(IItemData _itemData)
    {
        if (null == _itemData)
        {
            isRarityApplied = false;
            SetGradeVisual(null, null);
            SetEffectActive(false);
            SetShinyEffectActive(false);
            return;
        }

        // 같은 아이템, 같은 등급이면 이미 켜 둔 효과를 다시 쓰지 않는다.
        int _stateKey = GetRarityStateKey(_itemData);

        if (true == isRarityApplied && ReferenceEquals(lastRarityData, _itemData) && lastRarityStateKey == _stateKey)
            return;

        isRarityApplied = true;
        lastRarityData = _itemData;
        lastRarityStateKey = _stateKey;

        if (_itemData is ILogItemData _logData)
        {
            LogState _state = _logData.logState;

            // 기존 RareLine(등급색 모서리선)은 새 테두리가 대신하므로 항상 끈다.
            SetEffectActive(false);

            switch (_state)
            {
                case LogState.Fascinating:
                    SetGradeVisual(frameMaterialGold, badgeSpriteGold, tileFxMaterialGold);
                    SetShinyEffectActive(true);
                    break;
                case LogState.Advanced:
                    SetGradeVisual(frameMaterialDiamond, badgeSpriteDiamond, tileFxMaterialDiamond);
                    SetShinyEffectActive(true);
                    break;
                case LogState.Perfect:
                    SetGradeVisual(frameMaterialPrism, badgeSpritePrism, tileFxMaterialPrism);
                    SetShinyEffectActive(true);
                    break;
                default:
                    SetGradeVisual(null, null);
                    SetShinyEffectActive(false);
                    break;
            }
        }
        else
        {
            SetGradeVisual(null, null);
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

    // 개수가 0인 슬롯의 아이템 이미지와 등급 연출을 모두 끈다. 진행 중이던 등급 버스트도 함께 멈춰,
    // 비워진 뒤에 흰 번쩍임이 남지 않게 한다.
    private void ClearEmptyCountVisual()
    {
        isRarityApplied = false;
        KillGradeBurst();
        SetGradeVisual(null, null);
        UpdateImage(null, Color.white);
        SetEffectActive(false);
        SetShinyEffectActive(false);
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
                ClearEmptyCountVisual();
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
        getGradeBurst = GetGradeBurstValue;
        setGradeBurst = SetGradeBurstValue;

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
        KillGradeBurst();

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
        KillGradeBurst();

        if (null != CameraFinder.Instance)
        {
            CameraFinder.Instance.HandleCameraFindingEvent -= ApplySorting;
        }

        ResetData();
    }
}
