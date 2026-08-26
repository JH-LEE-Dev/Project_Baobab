using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using DG.Tweening.Core;
using PresentationLayer.DOTweenAnimationSystem;
using Coffee.UIEffects;

public class HUD_PopupNav_RegionBtn : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [Tooltip("버튼 클릭 영역 (레이캐스트용 이미지)")]
    [SerializeField] private Image clickImage;
    [Tooltip("배경 이미지를 교체할 대상 Image (비워두면 clickImage 사용)")]
    [SerializeField] private Image bgImage;
    [Tooltip("프레임 이미지 (배경과 동일한 색상 연출이 적용됨)")]
    [SerializeField] private Image frameImage;
    [Tooltip("NEW 뱃지 오브젝트")]
    [SerializeField] private GameObject newIndicatorObj;
    [Tooltip("잠금(Lock) 아이콘 등 상태 비주얼 오브젝트")]
    [SerializeField] private GameObject lockVisualObj;

    [Header("Additional Image Settings")]
    [Tooltip("추가 이미지 (버튼과 동기화됨)")]
    [SerializeField] private Image additionalImage;
    [Tooltip("해금 상태일 때의 추가 이미지 색상")]
    [SerializeField] private Color additionalUnlockedColor = Color.white;
    [Tooltip("잠금 상태일 때의 추가 이미지 색상")]
    [SerializeField] private Color additionalLockedColor = Color.gray;

    [Header("DOTween Settings")]
    [Tooltip("선택 시 연출 시간")]
    [SerializeField] private float selectDuration = 0.2f;

    [Header("UIEffect & Color Settings")]
    [SerializeField] private UIEffect uiEffect;
    [SerializeField] private float colorTransitionDuration = 0.2f;
    [SerializeField] private Ease colorTransitionEase = Ease.Linear;
    
    [Header("Shadow Outline Colors")]
    [ColorUsage(true, true)] [SerializeField] private Color normalShadowColor = Color.white;
    [ColorUsage(true, true)] [SerializeField] private Color hoverShadowColor = Color.white;
    [ColorUsage(true, true)] [SerializeField] private Color clickShadowColor = Color.white;

    [Header("Locked State Settings")]
    [Tooltip("잠금 상태의 기본 섀도우 색상")]
    [ColorUsage(true, true)] [SerializeField] private Color lockedNormalShadowColor = Color.gray;
    [Tooltip("잠금 상태의 호버 섀도우 색상")]
    [ColorUsage(true, true)] [SerializeField] private Color lockedHoverShadowColor = Color.gray;
    [Tooltip("잠금 상태의 클릭 섀도우 색상")]
    [ColorUsage(true, true)] [SerializeField] private Color lockedClickShadowColor = Color.gray;
    
    [Tooltip("잠금 상태의 기본 배경 색상")]
    [SerializeField] private Color lockedNormalBgColor = Color.gray;
    [Tooltip("잠금 상태의 호버 배경 색상")]
    [SerializeField] private Color lockedHoverBgColor = Color.gray;
    [Tooltip("잠금 상태의 클릭 배경 색상")]
    [SerializeField] private Color lockedClickBgColor = Color.gray;

    [Header("Background Colors")]
    [SerializeField] private Color normalBgColor = Color.white;
    [SerializeField] private Color hoverBgColor = Color.white;
    [SerializeField] private Color clickBgColor = Color.white;
    
    [Header("Select Animation")]
    [Tooltip("선택(클릭) 시 흔들림 강도")]
    [SerializeField] private float selectPunchStrength = 1.1f;

    [Header("Unlock Animation Settings")]
    [Tooltip("해금 연출 중 자물쇠 파괴(스케일 0) 시 재생할 파티클 시스템")]
    [SerializeField] private ParticleSystem unlockDestructionParticle;
    [Tooltip("해금 파티클에 적용할 HDR 색상")]
    [ColorUsage(true, true)] [SerializeField] private Color unlockParticleColor = Color.white;
    [Tooltip("자물쇠가 흔들리는 연출 시간")]
    [SerializeField] private float unlockShakeDuration = 0.8f;
    [Tooltip("자물쇠가 흔들리는 강도 (지진 느낌)")]
    [SerializeField] private float unlockShakeStrength = 15f;
    [Tooltip("자물쇠가 줄어드는 시간")]
    [SerializeField] private float unlockShrinkDuration = 0.2f;
    [Tooltip("파티클 재생 후 다음 연출까지의 대기 시간")]
    [SerializeField] private float unlockParticleDelay = 0.3f;
    [Tooltip("버튼 비주얼 최종 연출(색상 복구 등) 시간")]
    [SerializeField] private float unlockDuration = 0.5f;

    [Header("Hover Animation (Region)")]
    [Tooltip("호버 시 이동할 X축 거리")]
    [SerializeField] private float hoverMoveX = 20f;
    [Tooltip("호버 이동 연출 시간")]
    [SerializeField] private float hoverMoveDuration = 0.2f;
    [Tooltip("호버 이동 이즈(Ease)")]
    [SerializeField] private Ease hoverMoveEase = Ease.OutQuad;

    [Header("Cursor Settings")]
    [Tooltip("커서가 따라다닐 타겟 트랜스폼 (비워두면 자기 자신)")]
    [SerializeField] private RectTransform cursorTargetTransform;
    [Tooltip("커서 패딩 (여백)")]
    [SerializeField] private Vector2 cursorPadding = new Vector2(10f, 10f);
    [Tooltip("커서 오프셋")]
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;
    [Tooltip("커스텀 커서 크기 사용 여부 (false면 타겟의 rect size 사용)")]
    [SerializeField] private bool useCustomCursorSize = false;
    [Tooltip("커스텀 커서 크기")]
    [SerializeField] private Vector2 customCursorSize = new Vector2(300f, 150f);
    [Tooltip("호버 시 커서 모션 세팅")]
    [SerializeField] private CursorMotionSettings hoverCursorMotion = new CursorMotionSettings();

    private ICursorBoxUI cursorBoxUI;
    private RectTransform cachedRectTransform;
    private RectTransform CachedRectTransform
    {
        get
        {
            if (null == cachedRectTransform)
            {
                cachedRectTransform = GetComponent<RectTransform>();
            }
            return cachedRectTransform;
        }
    }

    private Tween unlockTween;
    private Tween hoverTween;
    private Tween clearNewTween;
    private Tween colorTween;
    private bool isSelected = false;
    
    // 비주얼 연출을 적용할 자식 트랜스폼 목록 (clickImage 제외)
    private System.Collections.Generic.List<Transform> visualChildren = new System.Collections.Generic.List<Transform>();
    private float[] originalLocalX;

    private HUD_PopupNav_Main mainController;
    private MapEnvironmentDataInfo myInfo;
    private int appearSoundIndex = -1;
    
    // 런타임 캐싱 데이터
    private ParticleSystem newIndicatorParticle;
    private Sprite unlockedBgSprite;
    public TweenCallback CachedActivate { get; private set; }
    private TweenCallback cachedPlayParticle;
    private TweenCallback cachedClearNewComplete;
    private TweenCallback cachedUnlockStep1;
    private TweenCallback cachedUnlockStep2;
    private TweenCallback cachedUnlockMotionComplete;
    private TweenCallback cachedUnlockPlayParticle;
    private TweenCallback cachedDisableUIEffectIfUnlocked;
    private DOGetter<Color> cachedGetShadowColor;
    private DOSetter<Color> cachedSetShadowColor;

    private Action pendingClearNewCompleteAction;
    private bool isPointerOver = false;
    private bool isInitialized = false;
    private bool hasInstantiatedParticleMat = false;

    public MapType GetMapType() => (true == isInitialized ? myInfo.mapType : MapType.None);
    public bool IsUnlocked => (true == isInitialized && true == myInfo.isUnlocked);
    public bool IsSelected => isSelected;

    private void Awake()
    {
        if (null == cachedRectTransform)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        CachedActivate = ActivateObject;
        cachedPlayParticle = PlayNewIndicatorParticle;
        cachedClearNewComplete = OnClearNewComplete;
        cachedUnlockStep1 = OnUnlockStep1;
        cachedUnlockStep2 = OnUnlockStep2;
        cachedUnlockMotionComplete = OnUnlockMotionComplete;
        if (null == cachedGetShadowColor) cachedGetShadowColor = GetShadowColor;
        if (null == cachedSetShadowColor) cachedSetShadowColor = SetShadowColor;
        if (null == cachedDisableUIEffectIfUnlocked) cachedDisableUIEffectIfUnlocked = DisableUIEffectIfUnlocked;
        if (null == cachedUnlockPlayParticle) cachedUnlockPlayParticle = PlayUnlockParticle;

        CacheVisualChildren();
    }

    private void CacheVisualChildren()
    {
        if (null == originalLocalX || 0 == originalLocalX.Length)
        {
            visualChildren.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform _child = transform.GetChild(i);
                if (null != clickImage && _child == clickImage.transform)
                {
                    continue;
                }
                visualChildren.Add(_child);
            }

            originalLocalX = new float[visualChildren.Count];
            for (int i = 0; i < visualChildren.Count; i++)
            {
                originalLocalX[i] = visualChildren[i].localPosition.x;
            }
        }
    }

    public void Initialize(HUD_PopupNav_Main _mainController, MapEnvironmentDataInfo _info, LocalizationManager _localizationManager, ICursorBoxUI _cursorBoxUI, Sprite _bgSprite = null, int _appearSoundIndex = -1)
    {
        mainController = _mainController;
        myInfo = _info;
        isInitialized = true;
        cursorBoxUI = _cursorBoxUI;
        appearSoundIndex = _appearSoundIndex;
        unlockedBgSprite = _bgSprite;

        CacheVisualChildren();

        if (null == cachedGetShadowColor) cachedGetShadowColor = GetShadowColor;
        if (null == cachedSetShadowColor) cachedSetShadowColor = SetShadowColor;
        if (null == cachedDisableUIEffectIfUnlocked) cachedDisableUIEffectIfUnlocked = DisableUIEffectIfUnlocked;
        if (null == cachedUnlockPlayParticle) cachedUnlockPlayParticle = PlayUnlockParticle;

        bool _isLocked = (false == _info.isUnlocked);

        SetupVisualState(_isLocked, _info);

        NavParticleHelper.ApplyInstancedColor(unlockDestructionParticle, unlockParticleColor, ref hasInstantiatedParticleMat);

        SetSelectedState(false);
    }

    private void SetupVisualState(bool _isLocked, MapEnvironmentDataInfo _info)
    {
        Image _targetImage = (null != bgImage) ? bgImage : clickImage;
        if (null != _targetImage)
        {
            if (false == _isLocked && null != unlockedBgSprite)
            {
                _targetImage.sprite = unlockedBgSprite;
            }
        }

        if (null != lockVisualObj)
        {
            lockVisualObj.SetActive(_isLocked);
        }

        if (null != newIndicatorObj)
        {
            bool _showNew = (true == isInitialized && true == _info.isNew && false == _isLocked);
            newIndicatorObj.SetActive(_showNew);
            if (true == _showNew)
            {
                newIndicatorObj.transform.localScale = Vector3.one;
            }
        }
    }

    private void ActivateObject()
    {
        gameObject.SetActive(true);

        switch (appearSoundIndex)
        {
            case 0: Sound.PlayUI(SoundID.MainMenuDot01); break;
            case 1: Sound.PlayUI(SoundID.MainMenuDot02); break;
            case 2: Sound.PlayUI(SoundID.MainMenuDot03); break;
            case 3: Sound.PlayUI(SoundID.MainMenuDot04); break;
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        if (false == isInitialized || false == myInfo.isUnlocked)
        {
            Sound.PlayUI(SoundID.NaviLocked);
            return;
        }

        if (true == isSelected)
        {
            return;
        }

        // 클릭 시 색상 연출 (일시적으로 click 색상 -> hover 색상)
        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
            colorTween = null;
        }
        Sequence _seq = DOTween.Sequence();
        float _halfDuration = colorTransitionDuration * 0.5f;
        bool _isUnlocked = (true == isInitialized && true == myInfo.isUnlocked);

        if (null != uiEffect)
        {
            uiEffect.enabled = true;
            Color _targetClickShadow = _isUnlocked ? clickShadowColor : lockedClickShadowColor;
            Color _targetHoverShadow = _isUnlocked ? hoverShadowColor : lockedHoverShadowColor;
            
            if (null == cachedGetShadowColor) cachedGetShadowColor = GetShadowColor;
            if (null == cachedSetShadowColor) cachedSetShadowColor = SetShadowColor;

            _seq.Append(DOTween.To(cachedGetShadowColor, cachedSetShadowColor, _targetClickShadow, _halfDuration).SetEase(colorTransitionEase).SetTarget(uiEffect));
            _seq.Append(DOTween.To(cachedGetShadowColor, cachedSetShadowColor, _targetHoverShadow, _halfDuration).SetEase(colorTransitionEase).SetTarget(uiEffect));
        }

        Image _targetBgImg = (null != bgImage) ? bgImage : clickImage;
        Color _targetClickBg = _isUnlocked ? clickBgColor : lockedClickBgColor;
        Color _targetHoverBg = _isUnlocked ? hoverBgColor : lockedHoverBgColor;

        if (null != _targetBgImg)
        {
            _seq.Insert(0, _targetBgImg.DOColor(_targetClickBg, _halfDuration).SetEase(colorTransitionEase).SetTarget(_targetBgImg));
            _seq.Insert(_halfDuration, _targetBgImg.DOColor(_targetHoverBg, _halfDuration).SetEase(colorTransitionEase).SetTarget(_targetBgImg));
        }
        if (null != frameImage)
        {
            if (true == _isUnlocked)
            {
                _seq.Insert(0, frameImage.DOColor(_targetClickBg, _halfDuration).SetEase(colorTransitionEase).SetTarget(frameImage));
                _seq.Insert(_halfDuration, frameImage.DOColor(_targetHoverBg, _halfDuration).SetEase(colorTransitionEase).SetTarget(frameImage));
            }
        }
        _seq.SetTarget(this);
        colorTween = _seq;

        mainController.HandleRegionSelected(myInfo.mapType);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isPointerOver = true;

        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        if (true == isSelected)
        {
            return;
        }

        Sound.PlayUI(SoundID.NaviMainHover);
        
        TriggerHover();
    }

    public void ForceStopHoverEffect()
    {
        isPointerOver = false;

        if (null != hoverTween && true == hoverTween.IsActive())
        {
            hoverTween.Kill();
            hoverTween = null;
        }

        if (false == isSelected)
        {
            Sequence _seq = DOTween.Sequence();
            for (int i = 0; i < visualChildren.Count; i++)
            {
                if (null != originalLocalX && i < originalLocalX.Length)
                {
                    _seq.Join(visualChildren[i].DOLocalMoveX(originalLocalX[i], hoverMoveDuration).SetEase(hoverMoveEase));
                }
            }
            hoverTween = _seq;

            bool _isUnlocked = (true == isInitialized && true == myInfo.isUnlocked);
            Color _targetNormalShadow = _isUnlocked ? normalShadowColor : lockedNormalShadowColor;
            Color _targetNormalBg = _isUnlocked ? normalBgColor : lockedNormalBgColor;
            TweenColors(_targetNormalShadow, _targetNormalBg, true);
        }
        else
        {
            for (int i = 0; i < visualChildren.Count; i++)
            {
                if (null != originalLocalX && i < originalLocalX.Length)
                {
                    visualChildren[i].localPosition = new Vector3(originalLocalX[i] + hoverMoveX, visualChildren[i].localPosition.y, visualChildren[i].localPosition.z);
                }
            }
        }

        if (null != cursorBoxUI)
        {
            RectTransform _target = null != cursorTargetTransform ? cursorTargetTransform : CachedRectTransform;
            if (cursorBoxUI.IsTarget(_target))
            {
                var _settings = cursorBoxUI.MotionSettings;
                if (null != _settings)
                {
                    float _originalHide = _settings.hideDuration;
                    try
                    {
                        _settings.hideDuration = 0.03f;
                        cursorBoxUI.Hide(_target);
                    }
                    finally
                    {
                        _settings.hideDuration = _originalHide;
                    }
                }
                else
                {
                    cursorBoxUI.Hide(_target);
                }
            }
        }
    }

    public void EvaluateHoverState()
    {
        if (true == isPointerOver && false == mainController.IsInputBlocked)
        {
            TriggerHover();
        }
    }

    public void TriggerHover()
    {
        if (false == isSelected)
        {
            bool _isUnlocked = (true == isInitialized && true == myInfo.isUnlocked);
            Color _targetHoverShadow = _isUnlocked ? hoverShadowColor : lockedHoverShadowColor;
            Color _targetHoverBg = _isUnlocked ? hoverBgColor : lockedHoverBgColor;
            TweenColors(_targetHoverShadow, _targetHoverBg, false);
            
            ClearNewIndicator();

            if (null != hoverTween && true == hoverTween.IsActive())
            {
                hoverTween.Kill();
                hoverTween = null;
            }
            
            // clickImage(Raycast) 영역은 움직이지 않도록 루트 대신 비주얼 자식들만 연출
            Sequence _seq = DOTween.Sequence();
            for (int i = 0; i < visualChildren.Count; i++)
            {
                if (null != originalLocalX && i < originalLocalX.Length)
                {
                    _seq.Join(visualChildren[i].DOLocalMoveX(originalLocalX[i] + hoverMoveX, hoverMoveDuration).SetEase(hoverMoveEase));
                }
            }
            _seq.SetTarget(this);
            hoverTween = _seq;
        }

        if (null != cursorBoxUI)
        {
            RectTransform _target = null != cursorTargetTransform ? cursorTargetTransform : CachedRectTransform;
            Vector2 _size = useCustomCursorSize ? customCursorSize : (_target.rect.size + cursorPadding);
            cursorBoxUI.Show(_target, _size, cursorOffset, hoverCursorMotion);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isPointerOver = false;

        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        if (false == isSelected)
        {
            bool _isUnlocked = (true == isInitialized && true == myInfo.isUnlocked);
            Color _targetNormalShadow = _isUnlocked ? normalShadowColor : lockedNormalShadowColor;
            Color _targetNormalBg = _isUnlocked ? normalBgColor : lockedNormalBgColor;
            TweenColors(_targetNormalShadow, _targetNormalBg, true);

            if (null != hoverTween && true == hoverTween.IsActive())
            {
                hoverTween.Kill();
                hoverTween = null;
            }

            Sequence _seq = DOTween.Sequence();
            for (int i = 0; i < visualChildren.Count; i++)
            {
                if (null != originalLocalX && i < originalLocalX.Length)
                {
                    _seq.Join(visualChildren[i].DOLocalMoveX(originalLocalX[i], hoverMoveDuration).SetEase(hoverMoveEase));
                }
            }
            _seq.SetTarget(this);
            hoverTween = _seq;
        }

        if (null != cursorBoxUI)
        {
            RectTransform _target = null != cursorTargetTransform ? cursorTargetTransform : CachedRectTransform;
            cursorBoxUI.Hide(_target);
        }
    }

    public bool IsMouseOver()
    {
        if (false == gameObject.activeInHierarchy || false == isInitialized) return false;
        if (true == isPointerOver) return true;

        RectTransform _rect = null != clickImage ? clickImage.rectTransform : CachedRectTransform;
        if (null == _rect) return false;

        Vector2 _mousePos = Vector2.zero;
        if (null != UnityEngine.InputSystem.Mouse.current)
        {
            _mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        else
        {
            _mousePos = Input.mousePosition;
        }

        Canvas _canvas = GetComponentInParent<Canvas>();
        Camera _cam = (null != _canvas && RenderMode.ScreenSpaceOverlay != _canvas.renderMode)
            ? _canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    private void TweenColors(Color _targetShadow, Color _targetBg, bool _isIdle = false)
    {
        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
            colorTween = null;
        }

        Sequence _seq = DOTween.Sequence();
        bool _isUnlocked = (true == isInitialized && true == myInfo.isUnlocked);

        if (null != uiEffect)
        {
            if (false == _isIdle || false == _isUnlocked)
            {
                uiEffect.enabled = true;
            }

            if (null == cachedGetShadowColor) cachedGetShadowColor = GetShadowColor;
            if (null == cachedSetShadowColor) cachedSetShadowColor = SetShadowColor;

            _seq.Join(DOTween.To(cachedGetShadowColor, cachedSetShadowColor, _targetShadow, colorTransitionDuration).SetEase(colorTransitionEase).SetTarget(uiEffect));

            if (true == _isIdle && true == _isUnlocked)
            {
                if (null == cachedDisableUIEffectIfUnlocked) cachedDisableUIEffectIfUnlocked = DisableUIEffectIfUnlocked;
                _seq.OnComplete(cachedDisableUIEffectIfUnlocked);
            }
        }

        Image _targetBgImg = (null != bgImage) ? bgImage : clickImage;
        if (null != _targetBgImg)
        {
            _seq.Join(_targetBgImg.DOColor(_targetBg, colorTransitionDuration).SetEase(colorTransitionEase).SetTarget(_targetBgImg));
        }
        if (null != frameImage)
        {
            if (true == _isUnlocked)
            {
                _seq.Join(frameImage.DOColor(_targetBg, colorTransitionDuration).SetEase(colorTransitionEase).SetTarget(frameImage));
            }
        }
        if (null != additionalImage)
        {
            Color _targetAdditionalColor = true == _isUnlocked ? additionalUnlockedColor : additionalLockedColor;
            _seq.Join(additionalImage.DOColor(_targetAdditionalColor, colorTransitionDuration).SetEase(colorTransitionEase).SetTarget(additionalImage));
        }

        _seq.SetTarget(this);
        colorTween = _seq;
    }

    private Action pendingUnlockCompleteAction;

    public void PlayUnlockMotion(Action _onComplete, float _speedRate = 1.0f)
    {
        pendingUnlockCompleteAction = _onComplete;

        if (null != unlockTween && true == unlockTween.IsActive())
        {
            unlockTween.Kill();
            unlockTween = null;
        }

        if (null != mainController)
        {
            mainController.PlayUnlockStartSoundIfNeeded();
        }

        Sequence _seq = DOTween.Sequence();
        bool _isFastMode = 1.0f < _speedRate;
        float _timeScale = 1.0f / _speedRate;

        if (null != lockVisualObj && true == lockVisualObj.activeSelf)
        {
            if (false == _isFastMode)
            {
                _seq.Append(lockVisualObj.transform.DOShakePosition(unlockShakeDuration, new Vector3(unlockShakeStrength, unlockShakeStrength, 0f), 30, 90f));
                _seq.Join(lockVisualObj.transform.DOShakeRotation(unlockShakeDuration, new Vector3(0f, 0f, unlockShakeStrength * 2f), 30, 90f));
                _seq.Append(lockVisualObj.transform.DOScale(Vector3.zero, unlockShrinkDuration).SetEase(Ease.InBack));
            }
            else
            {
                _seq.Append(lockVisualObj.transform.DOScale(Vector3.zero, unlockShrinkDuration * _timeScale).SetEase(Ease.InBack));
            }
            
            float _waitTime = unlockParticleDelay;
            if (null != unlockDestructionParticle)
            {
                _waitTime = unlockDestructionParticle.main.duration + unlockDestructionParticle.main.startLifetime.constantMax;
            }

            _seq.AppendCallback(cachedUnlockPlayParticle);
            
            _seq.AppendInterval(_waitTime * _timeScale);
        }

        _seq.AppendCallback(cachedUnlockStep1);
        _seq.AppendInterval(unlockDuration * _timeScale);
        _seq.AppendCallback(cachedUnlockStep2);

        if (null != newIndicatorObj)
        {
            _seq.Append(newIndicatorObj.transform.DOScale(Vector3.one, 0.4f * _timeScale).SetEase(Ease.OutBack));
        }

        _seq.OnComplete(cachedUnlockMotionComplete);
        unlockTween = _seq;
    }

    private void OnUnlockStep1()
    {
        if (null != lockVisualObj)
        {
            Image _img = lockVisualObj.GetComponent<Image>();
            if (null != _img)
            {
                _img.enabled = false;
            }
            else
            {
                lockVisualObj.SetActive(false);
            }
        }

        myInfo.isUnlocked = true;
        
        Image _targetImage = (null != bgImage) ? bgImage : clickImage;
        if (null != _targetImage && null != unlockedBgSprite)
        {
            _targetImage.sprite = unlockedBgSprite;
        }

        Color _targetNormalShadow = normalShadowColor;
        Color _targetNormalBg = normalBgColor;
        TweenColors(_targetNormalShadow, _targetNormalBg, true);
    }

    private void OnUnlockStep2()
    {
        if (null != newIndicatorObj)
        {
            newIndicatorObj.SetActive(true);
            newIndicatorObj.transform.localScale = Vector3.zero;
        }
    }

    private void OnUnlockMotionComplete()
    {
        myInfo.isUnlocked = true;

        if (null != lockVisualObj)
        {
            lockVisualObj.SetActive(false);
        }
        pendingUnlockCompleteAction?.Invoke();
        pendingUnlockCompleteAction = null;

        if (true == isPointerOver)
        {
            EvaluateHoverState();
        }
    }

    private Tween selectTween;

    public void SetSelectedState(bool _isSelected, bool _playClickAnim = true)
    {
        isSelected = _isSelected;

        if (null != selectTween && true == selectTween.IsActive())
        {
            selectTween.Kill();
            selectTween = null;
        }

        if (null != hoverTween && true == hoverTween.IsActive())
        {
            hoverTween.Kill();
            hoverTween = null;
        }

        if (null != uiEffect)
        {
            uiEffect.edgeMode = _isSelected ? EdgeMode.Shiny : EdgeMode.None;
        }

        bool _isUnlocked = (true == isInitialized && true == myInfo.isUnlocked);

        if (true == _isSelected)
        {
            Color _targetHoverShadow = _isUnlocked ? hoverShadowColor : lockedHoverShadowColor;
            Color _targetHoverBg = _isUnlocked ? hoverBgColor : lockedHoverBgColor;

            if (true == _playClickAnim)
            {
                Sequence _seq = DOTween.Sequence();
                for (int i = 0; i < visualChildren.Count; i++)
                {
                    if (null != originalLocalX && i < originalLocalX.Length)
                    {
                        _seq.Join(visualChildren[i].DOLocalMoveX(originalLocalX[i] + hoverMoveX, hoverMoveDuration).SetEase(hoverMoveEase));
                    }
                    _seq.Join(visualChildren[i].DOPunchScale(new Vector3(selectPunchStrength, selectPunchStrength, 1f) - Vector3.one, selectDuration, 5, 0.5f));
                }
                _seq.SetTarget(this);
                selectTween = _seq;
                TweenColors(_targetHoverShadow, _targetHoverBg, false);
            }
            else
            {
                for (int i = 0; i < visualChildren.Count; i++)
                {
                    visualChildren[i].localScale = Vector3.one;
                    if (null != originalLocalX && i < originalLocalX.Length)
                    {
                        visualChildren[i].localPosition = new Vector3(originalLocalX[i] + hoverMoveX, visualChildren[i].localPosition.y, visualChildren[i].localPosition.z);
                    }
                }
                TweenColors(_targetHoverShadow, _targetHoverBg, false);
            }
        }
        else
        {
            Color _targetNormalShadow = _isUnlocked ? normalShadowColor : lockedNormalShadowColor;
            Color _targetNormalBg = _isUnlocked ? normalBgColor : lockedNormalBgColor;

            if (null != hoverTween && true == hoverTween.IsActive())
            {
                hoverTween.Kill();
                hoverTween = null;
            }

            if (true == _playClickAnim)
            {
                Sequence _seq = DOTween.Sequence();
                for (int i = 0; i < visualChildren.Count; i++)
                {
                    visualChildren[i].localScale = Vector3.one;
                    if (null != originalLocalX && i < originalLocalX.Length)
                    {
                        _seq.Join(visualChildren[i].DOLocalMoveX(originalLocalX[i], hoverMoveDuration).SetEase(hoverMoveEase));
                    }
                }
                _seq.SetTarget(this);
                hoverTween = _seq;
            }
            else
            {
                bool _needMove = false;
                for (int i = 0; i < visualChildren.Count; i++)
                {
                    if (null != originalLocalX && i < originalLocalX.Length && 0.1f < Mathf.Abs(visualChildren[i].localPosition.x - originalLocalX[i]))
                    {
                        _needMove = true;
                        break;
                    }
                }

                if (true == _needMove)
                {
                    Sequence _seq = DOTween.Sequence();
                    for (int i = 0; i < visualChildren.Count; i++)
                    {
                        visualChildren[i].localScale = Vector3.one;
                        if (null != originalLocalX && i < originalLocalX.Length)
                        {
                            _seq.Join(visualChildren[i].DOLocalMoveX(originalLocalX[i], hoverMoveDuration).SetEase(hoverMoveEase));
                        }
                    }
                    _seq.SetTarget(this);
                    hoverTween = _seq;
                }
                else
                {
                    for (int i = 0; i < visualChildren.Count; i++)
                    {
                        visualChildren[i].localScale = Vector3.one;
                        if (null != originalLocalX && i < originalLocalX.Length)
                        {
                            visualChildren[i].localPosition = new Vector3(originalLocalX[i], visualChildren[i].localPosition.y, visualChildren[i].localPosition.z);
                        }
                    }
                }
            }
            TweenColors(_targetNormalShadow, _targetNormalBg, true);
        }
    }

    public void ClearNewIndicator()
    {
        if (null != newIndicatorObj && true == newIndicatorObj.activeSelf)
        {
            if (null != clearNewTween && true == clearNewTween.IsActive())
            {
                return;
            }

            Transform _newTr = newIndicatorObj.transform;
            _newTr.localScale = Vector3.one;

            if (null == newIndicatorParticle)
            {
                newIndicatorParticle = newIndicatorObj.GetComponentInChildren<ParticleSystem>();
            }

            Sequence _seq = DOTween.Sequence();

            // 1. 뽀잉 하면서 눌리는 느낌 (가로는 살짝 퍼지고 세로는 눌림)
            _seq.Append(_newTr.DOScale(new Vector3(1.2f, 0.7f, 1f), 0.1f).SetEase(Ease.OutQuad));

            // 2 & 3. Y축은 원래 스케일로 돌아가면서 X축은 0으로 줄어들고, 동시에 파티클 재생
            _seq.AppendCallback(cachedPlayParticle);
            _seq.Append(_newTr.DOScale(new Vector3(0f, 1f, 1f), 0.15f).SetEase(Ease.InQuad));

            _seq.OnComplete(cachedClearNewComplete);
            _seq.SetTarget(this);

            clearNewTween = _seq;
        }
    }

    private void PlayNewIndicatorParticle()
    {
        if (null != newIndicatorParticle)
        {
            newIndicatorParticle.Play();
        }
    }

    private void PlayUnlockParticle()
    {
        Sound.PlayUI(SoundID.NaviUnLock);
        Sound.PlayUI(SoundID.NaviUnLockEX);

        if (null != unlockDestructionParticle)
        {
            unlockDestructionParticle.Play();
        }
    }

    private void OnClearNewComplete()
    {
        if (null != newIndicatorObj)
        {
            newIndicatorObj.SetActive(false);
            newIndicatorObj.transform.localScale = Vector3.one;
        }
    }

    private Color GetShadowColor() 
    { 
        if (null != uiEffect) return uiEffect.shadowColor; 
        return Color.clear;
    }
    
    private void SetShadowColor(Color c) 
    { 
        if (null != uiEffect) uiEffect.shadowColor = c; 
    }

    private void DisableUIEffectIfUnlocked() 
    { 
        if (null != uiEffect && true == isInitialized && true == myInfo.isUnlocked) 
        {
            uiEffect.enabled = false; 
        }
    }


    private void OnDestroy()
    {
        if (null != colorTween && true == colorTween.IsActive()) { colorTween.Kill(); colorTween = null; }
        if (null != hoverTween && true == hoverTween.IsActive()) { hoverTween.Kill(); hoverTween = null; }
        if (null != unlockTween && true == unlockTween.IsActive()) { unlockTween.Kill(); unlockTween = null; }
        if (null != selectTween && true == selectTween.IsActive()) { selectTween.Kill(); selectTween = null; }
        if (null != clearNewTween && true == clearNewTween.IsActive()) { clearNewTween.Kill(); clearNewTween = null; }
    }
}
