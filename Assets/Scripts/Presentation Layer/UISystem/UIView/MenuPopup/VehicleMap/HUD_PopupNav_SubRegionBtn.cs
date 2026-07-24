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

    [Header("Locked Interaction Settings")]
    [Tooltip("잠긴 상태 클릭 거절 시 자물쇠 변경 색상")]
    [SerializeField] private Color lockClickColor = Color.red;
    [Tooltip("자물쇠 색상 복구 시간")]
    [SerializeField] private float lockClickColorDuration = 0.3f;
    [Tooltip("잠긴 상태 호버/클릭 시 자물쇠 펀치 강도")]
    [SerializeField] private float lockPunchStrength = 0.2f;

    [Header("DOTween Settings")]
    [Tooltip("해금 연출 시간")]
    [SerializeField] private float unlockDuration = 0.5f;
    [Tooltip("선택 시 연출 시간")]
    [SerializeField] private float selectDuration = 0.2f;

    [Header("Select Animation")]
    [Tooltip("선택(클릭) 시 흔들림 강도")]
    [SerializeField] private float selectPunchStrength = 1.1f;

    private Tween appearTween;
    private Tween disappearTween;
    private Tween unlockTween;
    private Tween selectTween;
    private Tween hoverTween;
    private Tween clearNewTween;
    private Tween lockIconTween;
    
    // 비주얼 연출을 적용할 자식 트랜스폼 목록 (clickImage 제외)
    private System.Collections.Generic.List<Transform> visualChildren = new System.Collections.Generic.List<Transform>();

    private HUD_PopupNav_Main mainController;
    private ForestEnvironmentInfo myInfo;
    private MapType parentMapType;

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
    
    private Action cachedOnHoverClearNewComplete;
    private Action pendingDisappearCompleteAction;
    private Action pendingUnlockCompleteAction;
    private Action pendingClearNewCompleteAction;

    private bool isPointerOver = false;
    private bool isSelected = false;

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
        
        RectTransform _myRect = GetComponent<RectTransform>();
        return null != _myRect ? _myRect.rect.width : 0f;
    }

    public void Initialize(HUD_PopupNav_Main _mainController, ForestEnvironmentInfo _info, LocalizationManager _localizationManager, MapType _parentMapType, System.Collections.Generic.List<TreeVisualData> _visualDatas)
    {
        mainController = _mainController;
        myInfo = _info;
        parentMapType = _parentMapType;

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

        bool _isLocked = false == _info.isUnlocked;

        if (null != lockIconObj)
        {
            lockIconObj.SetActive(_isLocked);
            lockIconObj.transform.localScale = Vector3.one;
            Image _lockImg = lockIconObj.GetComponent<Image>();
            if (null != _lockImg) _lockImg.color = Color.white;
        }

        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
            {
                treeProps[i].SetDimColor(nonHoveredColor);
                treeProps[i].SetVisualState(_isLocked ? TreeVisualState.Locked : TreeVisualState.Unlocked_Idle, 0f);
            }
        }

        if (null != clearNewTween && true == clearNewTween.IsActive())
        {
            clearNewTween.Kill();
        }

        if (null != newIndicatorObj)
        {
            bool _showNew = true == _info.isNew && false == _isLocked;
            newIndicatorObj.SetActive(_showNew);
            if (true == _showNew)
            {
                newIndicatorObj.transform.localScale = Vector3.one;
            }
        }

        // clickImage가 루트에 있지 않고 자식으로 있다면, 연출에서 제외하기 위해 비주얼 자식들만 수집
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

        CachedActivate = ActivateObject;
        cachedPlayParticle = PlayNewIndicatorParticle;
        cachedClearNewComplete = OnClearNewComplete;
        cachedDisappearComplete = OnDisappearComplete;
        cachedUnlockStep1 = OnUnlockStep1;
        cachedUnlockStep2 = OnUnlockStep2;
        cachedUnlockMotionComplete = OnUnlockMotionComplete;
        cachedClearNewTweenComplete = OnClearNewTweenComplete;
        cachedOnHoverClearNewComplete = OnHoverClearNewComplete;

        SetSelectedState(false);
    }

    private void ActivateObject()
    {
        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked || true == mainController.IsTransitioning)
        {
            return;
        }

        if (false == myInfo.isUnlocked)
        {
            PlayLockedInteraction();
            return;
        }

        mainController.HandleSubRegionSelected(myInfo.forestType);
    }

    private void PlayLockedInteraction()
    {
        if (null == lockIconObj) return;

        if (null != lockIconTween && true == lockIconTween.IsActive())
        {
            lockIconTween.Kill();
        }

        lockIconObj.transform.localScale = Vector3.one;
        Image _img = lockIconObj.GetComponent<Image>();

        Sequence _seq = DOTween.Sequence();
        _seq.Join(lockIconObj.transform.DOPunchScale(new Vector3(lockPunchStrength, lockPunchStrength, 0f), 0.3f, 5, 0.5f));

        if (null != _img)
        {
            _seq.Join(_img.DOColor(lockClickColor, lockClickColorDuration * 0.5f));
            _seq.Append(_img.DOColor(Color.white, lockClickColorDuration * 0.5f));
        }
        
        lockIconTween = _seq;
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isPointerOver = true;

        if (null == mainController || true == mainController.IsInputBlocked || true == mainController.IsTransitioning)
        {
            return;
        }

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
                if (null != lockIconTween && true == lockIconTween.IsActive()) lockIconTween.Kill();
                lockIconObj.transform.localScale = Vector3.one;
                lockIconTween = lockIconObj.transform.DOPunchScale(new Vector3(lockPunchStrength, lockPunchStrength, 0f), 0.3f, 5, 0.5f);
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
            }
            
            for (int i = 0; i < treeProps.Count; i++)
            {
                if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
                {
                    treeProps[i].SetVisualState(TreeVisualState.Unlocked_Hover, colorTransitionDuration);
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
        }
        
        for (int i = 0; i < treeProps.Count; i++)
        {
            if (null != treeProps[i] && true == treeProps[i].gameObject.activeSelf)
            {
                treeProps[i].SetVisualState(TreeVisualState.Unlocked_Idle, colorTransitionDuration);
            }
        }

        Sequence _seq = DOTween.Sequence();
        for (int i = 0; i < visualChildren.Count; i++)
        {
            _seq.Join(visualChildren[i].DOScale(1f, 0.2f).SetEase(Ease.OutBack));
        }
        hoverTween = _seq;
    }

    public void ResetState()
    {
        isSelected = false;
        
        if (null != hoverTween && true == hoverTween.IsActive()) hoverTween.Kill();
        if (null != appearTween && true == appearTween.IsActive()) appearTween.Kill();
        if (null != disappearTween && true == disappearTween.IsActive()) disappearTween.Kill();
        if (null != clearNewTween && true == clearNewTween.IsActive()) clearNewTween.Kill();
        if (null != unlockTween && true == unlockTween.IsActive()) unlockTween.Kill();
        if (null != selectTween && true == selectTween.IsActive()) selectTween.Kill();
        if (null != lockIconTween && true == lockIconTween.IsActive()) lockIconTween.Kill();

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
        }

        Sequence _seq = DOTween.Sequence();

        // 1. 자물쇠 연출 (현재는 즉시 비활성화, 추후 연출 추가 예정)
        _seq.AppendCallback(cachedUnlockStep1);

        _seq.AppendInterval(unlockDuration);

        // 2. 자물쇠 연출(및 실루엣 해제)이 끝난 직후 NEW 뱃지 팝업 연출
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
            lockIconObj.SetActive(false);
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
        pendingUnlockCompleteAction?.Invoke();
        pendingUnlockCompleteAction = null;
    }

    public void SetSelectedState(bool _isSelected)
    {
        isSelected = _isSelected;

        if (null != selectTween && true == selectTween.IsActive())
        {
            selectTween.Kill();
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
        if (null != appearTween && true == appearTween.IsActive()) appearTween.Kill();
        if (null != disappearTween && true == disappearTween.IsActive()) disappearTween.Kill();
        if (null != unlockTween && true == unlockTween.IsActive()) unlockTween.Kill();
        if (null != selectTween && true == selectTween.IsActive()) selectTween.Kill();
        if (null != hoverTween && true == hoverTween.IsActive()) hoverTween.Kill();
        if (null != clearNewTween && true == clearNewTween.IsActive()) clearNewTween.Kill();
        if (null != lockIconTween && true == lockIconTween.IsActive()) lockIconTween.Kill();
    }
}
