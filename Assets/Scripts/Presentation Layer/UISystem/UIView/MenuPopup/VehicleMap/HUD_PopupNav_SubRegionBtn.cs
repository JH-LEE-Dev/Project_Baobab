using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;

public class HUD_PopupNav_SubRegionBtn : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [Tooltip("버튼 클릭 영역 (레이캐스트용 이미지)")]
    [SerializeField] private Image clickImage;
    [Tooltip("NEW 뱃지 오브젝트")]
    [SerializeField] private GameObject newIndicatorObj;
    [Tooltip("서브지역을 표시할 나무 비주얼 프랍 리스트 (최대 2개 지원)")]
    [SerializeField] private System.Collections.Generic.List<HUD_PopupNav_TreeProp> treeProps;
    [Tooltip("실제 시각적인 나무 크기를 계산할 때 기준이 되는 오브젝트 (겹침 방지 레이아웃용)")]
    [SerializeField] private RectTransform visualBoundsRef;

    [Header("Lock Icon")]
    [Tooltip("자물쇠 아이콘 오브젝트")]
    [SerializeField] private GameObject lockIconObj;

    [Header("Tree Color Settings")]
    [Tooltip("호버하지 않을 때의 어두운 명도 (Color 틴트)")]
    [SerializeField] private Color nonHoveredColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Tooltip("색상 전환 연출 시간")]
    [SerializeField] private float colorTransitionDuration = 0.2f;

    [Header("Hover Animation (SubRegion)")]
    [Tooltip("호버 시 툭 치는 느낌의 Y축 스케일 바운스(Punch) 강도")]
    [SerializeField] private float hoverPunchStrengthY = 0.15f;
    [Tooltip("호버 바운스 연출 시간")]
    [SerializeField] private float hoverPunchDuration = 0.4f;

    [Header("Locked Interaction Settings (Click)")]
    [Tooltip("잠긴 상태 클릭 거절 시 재생할 파티클 시스템")]
    [SerializeField] private ParticleSystem lockClickParticle;
    [Tooltip("잠긴 상태 클릭 거절 시 자물쇠 변경 색상")]
    [SerializeField] private Color lockClickColor = Color.red;
    [Tooltip("자물쇠 색상 복구 시간")]
    [SerializeField] private float lockClickColorDuration = 0.3f;
    [Tooltip("잠긴 상태 펀치 시 스케일 강도")]
    [SerializeField] private float lockPunchStrength = 0.2f;
    [Tooltip("잠긴 상태 펀치 시 회전 강도 (양수, 좌우 랜덤)")]
    [SerializeField] private float lockPunchRotationStrength = 15f;
    [Tooltip("잠긴 상태 펀치 연출 시간")]
    [SerializeField] private float lockPunchDuration = 0.15f;
    [Tooltip("잠긴 상태 펀치 진동 횟수 (낮을수록 무심하게 툭 치는 느낌)")]
    [SerializeField] private int lockPunchVibrato = 4;
    [Tooltip("잠긴 상태 펀치 탄성 (낮을수록 덜 출렁거림)")]
    [SerializeField] private float lockPunchElasticity = 0.8f;

    [Header("Locked Interaction Settings (Hover)")]
    [Tooltip("잠긴 상태 호버 시 회전 펀치 각도 (어깨방 느낌)")]
    [SerializeField] private float lockHoverPunchRotation = 20f;
    [Tooltip("잠긴 상태 호버 시 회전 연출 시간")]
    [SerializeField] private float lockHoverPunchDuration = 0.6f;
    [Tooltip("잠긴 상태 호버 시 진동 횟수 (덜렁거리는 횟수)")]
    [SerializeField] private int lockHoverPunchVibrato = 6;
    [Tooltip("잠긴 상태 호버 시 탄성 (클수록 부드럽게 원복)")]
    [SerializeField] private float lockHoverPunchElasticity = 1f;

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

    [Header("DOTween Settings")]
    [Tooltip("나무 불 켜짐 / 실루엣 해제 시간")]
    [SerializeField] private float unlockDuration = 0.5f;
    [Tooltip("선택 시 연출 시간")]
    [SerializeField] private float selectDuration = 0.2f;

    [Header("Select Animation")]
    [Tooltip("선택(클릭) 시 흔들림 강도")]
    [SerializeField] private float selectPunchStrength = 1.1f;

    [Header("Cursor Settings")]
    [Tooltip("커서를 표시할 대상 트랜스폼 (비워두면 자기 자신)")]
    [SerializeField] private RectTransform cursorTargetTransform;

    [Tooltip("커서 크기 여백 (기본 크기 + Padding)")]
    [SerializeField] private Vector2 cursorPadding = new Vector2(8f, 8f);

    [Tooltip("커서 표시 위치 오프셋")]
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Tooltip("커스텀 크기 사용 여부")]
    [SerializeField] private bool useCustomCursorSize = false;

    [Tooltip("수동으로 지정할 커서 크기")]
    [SerializeField] private Vector2 customCursorSize = new Vector2(100f, 40f);

    [Tooltip("서브지역 버튼 호버 시 미세한 커서 연출 세팅")]
    [SerializeField] private CursorMotionSettings hoverCursorMotion = new CursorMotionSettings
    {
        enableShowMotion = true,
        showDuration = 0.2f,
        shrinkSizeScale = 0.9f,
        swingCount = 2,
        startAngle = 5f,
        enableIdleMotion = true,
        idleSizeOffset = 0.5f,
        hideDuration = 0.1f
    };

    private ICursorBoxUI cursorBoxUI;
    private Tween appearTween;
    private Tween disappearTween;
    private Tween unlockTween;
    private Tween selectTween;
    private Tween hoverTween;
    private Tween clearNewTween;
    private Tween lockIconTween;
    
    private RectTransform cachedRectTransform;
    private Image lockIconImage;

    public RectTransform CachedRectTransform
    {
        get
        {
            if (null == cachedRectTransform) cachedRectTransform = GetComponent<RectTransform>();
            return cachedRectTransform;
        }
    }
    
    private Vector3 lockIconOriginalLocalPos;
    
    // 비주얼 연출을 적용할 자식 트랜스폼 목록 (clickImage 제외)
    private System.Collections.Generic.List<Transform> visualChildren = new System.Collections.Generic.List<Transform>();

    private HUD_PopupNav_Main mainController;
    private ForestEnvironmentInfo myInfo;
    private MapType parentMapType;
    private int appearSoundIndex = -1;

    // 런타임 캐싱 데이터
    private ParticleSystem newIndicatorParticle;
    public TweenCallback CachedActivate { get; private set; }
    private TweenCallback cachedPlayParticle;
    private TweenCallback cachedClearNewComplete;
    private TweenCallback cachedDisappearComplete;
    private TweenCallback cachedUnlockStep1;
    private TweenCallback cachedUnlockStep2;
    private TweenCallback cachedUnlockMotionComplete;
    private TweenCallback cachedClearNewTweenComplete;
    private TweenCallback cachedUnlockPlayParticle;
    
    private Action cachedOnHoverClearNewComplete;
    private Action pendingDisappearCompleteAction;
    private Action pendingUnlockCompleteAction;
    private Action pendingClearNewCompleteAction;

    private bool isPointerOver = false;
    private bool isSelected = false;
    private bool hasInstantiatedParticleMat = false;

    public ForestType GetForestType() => myInfo.forestType;
    public ForestEnvironmentInfo GetInfo() => myInfo;

    /// <summary>
    /// 레이아웃 배치 시 사용될 실제 시각적 너비를 반환합니다.
    /// 바인딩된 객체가 없다면 자신의 RectTransform 너비를 반환합니다.
    /// </summary>
    public float GetActualVisualWidth()
    {
        if (null != visualBoundsRef)
        {
            return visualBoundsRef.rect.width;
        }
        
        return CachedRectTransform.rect.width;
    }

    /// <summary>
    /// 레이아웃 배치 시 사용될 실제 시각적 높이를 반환합니다.
    /// 바인딩된 객체가 없다면 자신의 RectTransform 높이를 반환합니다.
    /// </summary>
    public float GetActualVisualHeight()
    {
        if (null != visualBoundsRef)
        {
            return visualBoundsRef.rect.height;
        }
        
        return CachedRectTransform.rect.height;
    }

    public void Initialize(HUD_PopupNav_Main _mainController, ForestEnvironmentInfo _info, LocalizationManager _localizationManager, ICursorBoxUI _cursorBoxUI, MapType _parentMapType, System.Collections.Generic.List<TreeVisualData> _visualDatas, int _appearSoundIndex = -1)
    {
        mainController = _mainController;
        cursorBoxUI = _cursorBoxUI;
        myInfo = _info;
        parentMapType = _parentMapType;
        appearSoundIndex = _appearSoundIndex;

        SetupTreeProps(_visualDatas, false == _info.isUnlocked);
        SetupLockState(false == _info.isUnlocked);
        SetupNewIndicator(_info.isNew, false == _info.isUnlocked);
        CollectVisualChildren();
        CacheDelegates();

        NavParticleHelper.ApplyInstancedColor(unlockDestructionParticle, unlockParticleColor, ref hasInstantiatedParticleMat);

        SetSelectedState(false);
    }

    private void SetupTreeProps(System.Collections.Generic.List<TreeVisualData> _visualDatas, bool _isLocked)
    {
        if (null != treeProps)
        {
            for (int i = 0; i < treeProps.Count; i++)
            {
                if (null != _visualDatas && i < _visualDatas.Count)
                {
                    treeProps[i].gameObject.SetActive(true);
                    treeProps[i].Setup(_visualDatas[i]);
                }
                else
                {
                    treeProps[i].gameObject.SetActive(false);
                }
            }
        }
        
        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
            {
                treeProps[i].SetDimColor(nonHoveredColor);
                treeProps[i].SetVisualState(_isLocked ? TreeVisualState.Locked : TreeVisualState.Unlocked_Idle, 0f);
            }
        }
    }

    private void SetupLockState(bool _isLocked)
    {
        if (null != lockIconObj)
        {
            lockIconObj.SetActive(_isLocked);
            lockIconOriginalLocalPos = lockIconObj.transform.localPosition;
            lockIconObj.transform.localScale = Vector3.one;
            lockIconObj.transform.localRotation = Quaternion.identity;
            if (null == lockIconImage) lockIconImage = lockIconObj.GetComponent<Image>();
            if (null != lockIconImage) 
            {
                lockIconImage.color = Color.white;
                lockIconImage.enabled = true;
            }
        }
    }

    private void SetupNewIndicator(bool _isNew, bool _isLocked)
    {
        if (null != clearNewTween && true == clearNewTween.IsActive())
        {
            clearNewTween.Kill();
        }

        if (null != newIndicatorObj)
        {
            bool _showNew = true == _isNew && false == _isLocked;
            newIndicatorObj.SetActive(_showNew);
            if (true == _showNew)
            {
                newIndicatorObj.transform.localScale = Vector3.one;
            }
        }
    }

    private void CollectVisualChildren()
    {
        visualChildren.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform _child = transform.GetChild(i);
            if (null != clickImage && clickImage.transform == _child)
            {
                continue;
            }
            visualChildren.Add(_child);
        }
    }

    private void CacheDelegates()
    {
        CachedActivate = ActivateObject;
        cachedPlayParticle = PlayNewIndicatorParticle;
        cachedClearNewComplete = OnClearNewComplete;
        cachedDisappearComplete = OnDisappearComplete;
        cachedUnlockStep1 = OnUnlockStep1;
        cachedUnlockStep2 = OnUnlockStep2;
        cachedUnlockMotionComplete = OnUnlockMotionComplete;
        cachedClearNewTweenComplete = OnClearNewTweenComplete;
        cachedOnHoverClearNewComplete = OnHoverClearNewComplete;
        if (null == cachedUnlockPlayParticle) cachedUnlockPlayParticle = PlayUnlockParticle;
    }

    private void ActivateObject()
    {
        gameObject.SetActive(true);

        switch (appearSoundIndex)
        {
            case 0: Sound.PlayUI(SoundID.MainMenuButton01); break;
            case 1: Sound.PlayUI(SoundID.MainMenuButton02); break;
            case 2: Sound.PlayUI(SoundID.MainMenuButton03); break;
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked || true == mainController.IsTransitioning)
        {
            return;
        }

        if (false == myInfo.isUnlocked)
        {
            Sound.PlayUI(SoundID.NaviLocked);
            PlayLockedInteraction();
            return;
        }

        Sound.PlayUI(SoundID.MainClick);

        if (null != cursorBoxUI)
        {
            cursorBoxUI.HideImmediately();
        }

        mainController.HandleSubRegionSelected(myInfo.forestType);
    }

    private void PlayLockedInteraction()
    {
        if (null == lockIconObj) return;

        if (null != lockIconTween && true == lockIconTween.IsActive())
        {
            lockIconTween.Kill();
            lockIconTween = null;
        }

        if (null != lockClickParticle)
        {
            lockClickParticle.Play();
        }

        lockIconObj.transform.localScale = Vector3.one;
        lockIconObj.transform.localRotation = Quaternion.identity;
        lockIconObj.transform.localPosition = lockIconOriginalLocalPos;
        if (null != lockIconImage)
        {
            lockIconImage.color = Color.white;
        }

        Sequence _seq = DOTween.Sequence();
        
        float _randomSign = 0.5f < UnityEngine.Random.value ? 1f : -1f;
        Vector3 _rotPunch = new Vector3(0f, 0f, lockPunchRotationStrength * _randomSign);

        _seq.Join(lockIconObj.transform.DOPunchScale(new Vector3(lockPunchStrength, lockPunchStrength, 0f), lockPunchDuration, lockPunchVibrato, lockPunchElasticity));
        _seq.Join(lockIconObj.transform.DOPunchRotation(_rotPunch, lockPunchDuration, lockPunchVibrato, lockPunchElasticity));

        if (null != lockIconImage)
        {
            _seq.Join(lockIconImage.DOColor(lockClickColor, lockClickColorDuration * 0.5f).SetTarget(lockIconImage));
            _seq.Append(lockIconImage.DOColor(Color.white, lockClickColorDuration * 0.5f).SetTarget(lockIconImage));
        }
        
        _seq.SetTarget(this);
        lockIconTween = _seq;
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isPointerOver = true;

        if (null == mainController || true == mainController.IsInputBlocked || true == mainController.IsTransitioning)
        {
            return;
        }

        Sound.PlayUI(true == myInfo.isUnlocked ? SoundID.NaviSubHover : SoundID.NaviLockHover);

        TriggerHover();
    }

    public void EvaluateHoverState()
    {
        if (true == isPointerOver && false == mainController.IsInputBlocked && false == mainController.IsTransitioning)
        {
            TriggerHover();
        }
    }

    private void TriggerHover()
    {
        if (false == myInfo.isUnlocked)
        {
            if (null != lockIconObj)
            {
                if (null != lockIconTween && true == lockIconTween.IsActive()) { lockIconTween.Kill(); lockIconTween = null; }
                lockIconObj.transform.localScale = Vector3.one;
                lockIconObj.transform.localRotation = Quaternion.identity;
                lockIconObj.transform.localPosition = lockIconOriginalLocalPos;
                
                float _randomSign = 0.5f < UnityEngine.Random.value ? 1f : -1f;
                Vector3 _rotPunch = new Vector3(0f, 0f, lockHoverPunchRotation * _randomSign);
                
                lockIconTween = lockIconObj.transform.DOPunchRotation(_rotPunch, lockHoverPunchDuration, lockHoverPunchVibrato, lockHoverPunchElasticity);
            }
            return;
        }

        bool _hasNew = (null != newIndicatorObj && true == newIndicatorObj.activeSelf);
        
        if (true == _hasNew)
        {
            ClearNewIndicator(cachedOnHoverClearNewComplete);
        }
        else
        {
            ClearNewIndicator();
            mainController.HandleSubRegionHovered(myInfo.forestType, transform, myInfo);
        }

        if (false == isSelected)
        {
            if (null != hoverTween && true == hoverTween.IsActive())
            {
                hoverTween.Kill();
                hoverTween = null;
            }
            
            for (int i = 0; i < treeProps.Count; i++)
            {
                if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
                {
                    treeProps[i].SetVisualState(TreeVisualState.Unlocked_Hover, colorTransitionDuration);
                    treeProps[i].PlayHoverEffect();
                }
            }

            Sequence _seq = DOTween.Sequence();
            for (int i = 0; i < visualChildren.Count; i++)
            {
                visualChildren[i].localScale = Vector3.one;
                _seq.Join(visualChildren[i].DOPunchScale(new Vector3(0f, hoverPunchStrengthY, 0f), hoverPunchDuration, 5, 0.5f));
            }
            hoverTween = _seq;
        }

        if (null != cursorBoxUI)
        {
            RectTransform target = null != cursorTargetTransform ? cursorTargetTransform : CachedRectTransform;
            Vector2 size = useCustomCursorSize ? customCursorSize : (target.rect.size + cursorPadding);

            cursorBoxUI.Show(target, size, cursorOffset, hoverCursorMotion);
        }
    }

    private void OnHoverClearNewComplete()
    {
        if (null != mainController && false == mainController.IsInputBlocked && false == mainController.IsTransitioning)
        {
            mainController.HandleSubRegionHovered(myInfo.forestType, transform, myInfo);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isPointerOver = false;

        if (null == mainController || true == mainController.IsInputBlocked || true == mainController.IsTransitioning)
        {
            return;
        }

        if (false == myInfo.isUnlocked)
        {
            return;
        }

        mainController.HandleSubRegionUnhovered();

        if (null != hoverTween && true == hoverTween.IsActive())
        {
            hoverTween.Kill();
            hoverTween = null;
        }
        
        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
            {
                treeProps[i].SetVisualState(TreeVisualState.Unlocked_Idle, colorTransitionDuration);
                treeProps[i].StopHoverEffect();
            }
        }

        Sequence _seq = DOTween.Sequence();
        for (int i = 0; i < visualChildren.Count; i++)
        {
            _seq.Join(visualChildren[i].DOScale(1f, 0.2f).SetEase(Ease.OutBack));
        }
        hoverTween = _seq;

        if (null != cursorBoxUI)
        {
            RectTransform target = null != cursorTargetTransform ? cursorTargetTransform : CachedRectTransform;
            cursorBoxUI.Hide(target);
        }
    }

    public void StopAllTreePropHoverEffects()
    {
        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i])
            {
                treeProps[i].StopHoverEffect();
            }
        }

        if (null != cursorBoxUI)
        {
            RectTransform target = null != cursorTargetTransform ? cursorTargetTransform : CachedRectTransform;
            if (cursorBoxUI.IsTarget(target))
            {
                var settings = cursorBoxUI.MotionSettings;
                if (null != settings)
                {
                    float originalDuration = settings.hideDuration;
                    settings.hideDuration = 0.03f; // 내비게이션 닫힐 때는 더 빠르게 사라짐
                    cursorBoxUI.Hide(target);
                    settings.hideDuration = originalDuration;
                }
                else
                {
                    cursorBoxUI.Hide(target);
                }
            }
        }
    }

    public void ResetState()
    {
        isSelected = false;
        
        if (null != hoverTween && true == hoverTween.IsActive()) { hoverTween.Kill(); hoverTween = null; }
        if (null != appearTween && true == appearTween.IsActive()) { appearTween.Kill(); appearTween = null; }
        if (null != disappearTween && true == disappearTween.IsActive()) { disappearTween.Kill(); disappearTween = null; }
        if (null != clearNewTween && true == clearNewTween.IsActive()) { clearNewTween.Kill(); clearNewTween = null; }
        if (null != unlockTween && true == unlockTween.IsActive()) { unlockTween.Kill(); unlockTween = null; }
        if (null != selectTween && true == selectTween.IsActive()) { selectTween.Kill(); selectTween = null; }
        if (null != lockIconTween && true == lockIconTween.IsActive()) { lockIconTween.Kill(); lockIconTween = null; }

        if (null != lockIconObj)
        {
            lockIconObj.transform.localScale = Vector3.one;
            lockIconObj.transform.localRotation = Quaternion.identity;
            lockIconObj.transform.localPosition = lockIconOriginalLocalPos;
            if (null != lockIconImage) 
            {
                lockIconImage.enabled = true;
            }
        }

        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i])
            {
                treeProps[i].StopHoverEffect();
            }
        }

        for (int i = 0; i < visualChildren.Count; i++)
        {
            visualChildren[i].localScale = Vector3.one;
        }
        
        gameObject.SetActive(false);
    }

    public void PlayAppearMotion()
    {
        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
            appearTween = null;
        }

        // [TODO] 추후 DOTween 연출 작성
        // appearTween = ...
    }

    public void PlayDisappearMotion(Action _onComplete)
    {
        // 퇴장 시에는 빠른 전환을 위해 NEW 뱃지 연출(ClearNewIndicator)을 생략하고 즉시 퇴장 연출만 진행합니다.
        // 어차피 풀링되어 다음 사용 시 Initialize에서 상태에 맞게 켜지거나 꺼집니다.
        ExecuteDisappearMotion(_onComplete);
    }

    private void ExecuteDisappearMotion(Action _onComplete)
    {
        pendingDisappearCompleteAction = _onComplete;

        if (null != disappearTween && true == disappearTween.IsActive())
        {
            disappearTween.Kill();
            disappearTween = null;
        }

        Sequence _seq = DOTween.Sequence();
        
        // 역재생 느낌으로 원래대로 작아짐 (Y축 0.01로 축소)
        _seq.Append(transform.DOScaleY(0.01f, 0.15f).SetEase(Ease.InBack));
        
        _seq.OnComplete(cachedDisappearComplete);

        disappearTween = _seq;
    }

    private void OnDisappearComplete()
    {
        gameObject.SetActive(false);
        pendingDisappearCompleteAction?.Invoke();
        pendingDisappearCompleteAction = null;
    }

    public void PlayUnlockMotion(Action _onComplete)
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

        if (null != lockIconObj && true == lockIconObj.activeSelf)
        {
            // 1. 자물쇠 지진 난 것처럼 엄청 흔들림
            _seq.Append(lockIconObj.transform.DOShakePosition(unlockShakeDuration, new Vector3(unlockShakeStrength, unlockShakeStrength, 0f), 30, 90f));
            _seq.Join(lockIconObj.transform.DOShakeRotation(unlockShakeDuration, new Vector3(0f, 0f, unlockShakeStrength * 2f), 30, 90f));
            
            // 2. 스케일 0으로 수렴
            _seq.Append(lockIconObj.transform.DOScale(Vector3.zero, unlockShrinkDuration).SetEase(Ease.InBack));
            
            // 3. 파티클 재생
            float _waitTime = unlockParticleDelay;
            if (null != unlockDestructionParticle)
            {
                _waitTime = unlockDestructionParticle.main.duration + unlockDestructionParticle.main.startLifetime.constantMax;
            }

            _seq.AppendCallback(cachedUnlockPlayParticle);
            
            // 파티클 터지는 시간 대기 (파티클 재생 시간만큼 동적 대기)
            _seq.AppendInterval(_waitTime);
        }

        // 4. 자물쇠 비활성화 및 나무 실루엣 해제
        _seq.AppendCallback(cachedUnlockStep1);

        _seq.AppendInterval(unlockDuration);

        // 5. NEW 뱃지 등장
        _seq.AppendCallback(cachedUnlockStep2);

        if (null != newIndicatorObj)
        {
            _seq.Append(newIndicatorObj.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
            _seq.AppendCallback(cachedPlayParticle);
        }

        _seq.OnComplete(cachedUnlockMotionComplete);

        unlockTween = _seq;
    }

    private void OnUnlockStep1()
    {
        if (null != lockIconObj)
        {
            lockIconObj.transform.localPosition = lockIconOriginalLocalPos;
            if (null != lockIconImage)
            {
                lockIconImage.enabled = false;
            }
            else
            {
                lockIconObj.SetActive(false);
            }
        }
        
        // 나무 실루엣(검은색) 서서히 벗겨지며 Dim 상태로 돌아오기
        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
            {
                treeProps[i].SetVisualState(TreeVisualState.Unlocked_Idle, unlockDuration);
            }
        }
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

        pendingUnlockCompleteAction?.Invoke();
        pendingUnlockCompleteAction = null;

        if (true == isPointerOver)
        {
            EvaluateHoverState();
        }
    }

    public void SetSelectedState(bool _isSelected)
    {
        isSelected = _isSelected;

        if (null != selectTween && true == selectTween.IsActive())
        {
            selectTween.Kill();
            selectTween = null;
        }

        if (true == _isSelected)
        {
            Sequence _seq = DOTween.Sequence();
            for (int i = 0; i < visualChildren.Count; i++)
            {
                _seq.Join(visualChildren[i].DOPunchScale(new Vector3(selectPunchStrength, selectPunchStrength, 1f) - Vector3.one, selectDuration, 5, 0.5f));
            }
            selectTween = _seq;
        }
        else
        {
            for (int i = 0; i < visualChildren.Count; i++)
            {
                visualChildren[i].localScale = Vector3.one;
            }
        }
    }

    public void ClearNewIndicator(Action _onComplete = null)
    {
        pendingClearNewCompleteAction = _onComplete;

        if (null != newIndicatorObj && true == newIndicatorObj.activeSelf)
        {
            if (null != clearNewTween && true == clearNewTween.IsActive())
            {
                clearNewTween.OnComplete(cachedClearNewTweenComplete);
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

            _seq.OnComplete(cachedClearNewTweenComplete);

            clearNewTween = _seq;
        }
        else
        {
            pendingClearNewCompleteAction?.Invoke();
            pendingClearNewCompleteAction = null;
        }
    }

    private void OnClearNewTweenComplete()
    {
        cachedClearNewComplete?.Invoke();
        pendingClearNewCompleteAction?.Invoke();
        pendingClearNewCompleteAction = null;
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

    private void OnDestroy()
    {
        if (null != appearTween && true == appearTween.IsActive()) { appearTween.Kill(); appearTween = null; }
        if (null != disappearTween && true == disappearTween.IsActive()) { disappearTween.Kill(); disappearTween = null; }
        if (null != unlockTween && true == unlockTween.IsActive()) { unlockTween.Kill(); unlockTween = null; }
        if (null != selectTween && true == selectTween.IsActive()) { selectTween.Kill(); selectTween = null; }
        if (null != hoverTween && true == hoverTween.IsActive()) { hoverTween.Kill(); hoverTween = null; }
        if (null != clearNewTween && true == clearNewTween.IsActive()) { clearNewTween.Kill(); clearNewTween = null; }
        if (null != lockIconTween && true == lockIconTween.IsActive()) { lockIconTween.Kill(); lockIconTween = null; }
    }
}
