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
    [Tooltip("잠금/해제 상태에 따라 색상을 바꿀 대상 이미지")]
    [SerializeField] private Image lockColorTargetImage;
    [Tooltip("NEW 뱃지 오브젝트")]
    [SerializeField] private GameObject newIndicatorObj;

    [Header("Visual Settings")]
    [Tooltip("해금된 상태일 때의 버튼(Image) 색상")]
    [SerializeField] private Color unlockedColor = Color.white;
    [Tooltip("잠긴 상태일 때의 버튼(Image) 색상")]
    [SerializeField] private Color lockedColor = Color.gray;

    [Header("DOTween Settings")]
    [Tooltip("해금 연출 시간")]
    [SerializeField] private float unlockDuration = 0.5f;
    [Tooltip("선택 시 연출 시간")]
    [SerializeField] private float selectDuration = 0.2f;
    
    [Header("Hover Animation (SubRegion)")]
    [Tooltip("호버 시 커질 목표 X 스케일")]
    [SerializeField] private float hoverScaleX = 1.15f;
    [Tooltip("호버 시 커질 목표 Y 스케일")]
    [SerializeField] private float hoverScaleY = 1.15f;
    [Tooltip("호버 커짐 연출 시간")]
    [SerializeField] private float hoverDuration = 0.25f;
    [Tooltip("호버 커짐 이즈 (뾱 하고 찰지게)")]
    [SerializeField] private Ease hoverEase = Ease.OutBack;
    
    [Tooltip("언호버 되감기 연출 시간")]
    [SerializeField] private float unhoverDuration = 0.2f;
    [Tooltip("언호버 되감기 이즈 (들어갈 때도 찰지게)")]
    [SerializeField] private Ease unhoverEase = Ease.InBack;

    [Header("Select Animation")]
    [Tooltip("선택(클릭) 시 흔들림 강도")]
    [SerializeField] private float selectPunchStrength = 1.1f;

    private Tween appearTween;
    private Tween disappearTween;
    private Tween unlockTween;
    private Tween selectTween;
    private Tween hoverTween;
    private Tween clearNewTween;
    
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

    public ForestType GetForestType() => myInfo.forestType;

    public void Initialize(HUD_PopupNav_Main _mainController, ForestEnvironmentInfo _info, LocalizationManager _localizationManager, MapType _parentMapType)
    {
        mainController = _mainController;
        myInfo = _info;
        parentMapType = _parentMapType;

        bool _isLocked = !_info.isUnlocked;
        
        Image _targetColorImage = (null != lockColorTargetImage) ? lockColorTargetImage : clickImage;
        if (null != _targetColorImage)
        {
            _targetColorImage.color = _isLocked ? lockedColor : unlockedColor;
        }

        if (null != newIndicatorObj)
        {
            bool _showNew = _info.isNew && !_isLocked;
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

        SetSelectedState(false);
    }

    private void ActivateObject()
    {
        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        if (false == myInfo.isUnlocked)
        {
            return;
        }

        mainController.HandleSubRegionSelected(myInfo.forestType);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        if (true == myInfo.isUnlocked)
        {
            bool _hasNew = (null != newIndicatorObj && true == newIndicatorObj.activeSelf);
            
            if (_hasNew)
            {
                ClearNewIndicator(() => {
                    if (null != mainController && false == mainController.IsInputBlocked)
                    {
                        mainController.HandleSubRegionHovered(myInfo.forestType, transform, myInfo);
                    }
                });
            }
            else
            {
                ClearNewIndicator();
                mainController.HandleSubRegionHovered(myInfo.forestType, transform, myInfo);
            }

            if (null != hoverTween && true == hoverTween.IsActive())
            {
                hoverTween.Kill();
            }
            // clickImage(Raycast) 영역이 찌그러지지 않도록 루트 대신 비주얼 자식들만 연출
            Sequence _seq = DOTween.Sequence();
            for (int i = 0; i < visualChildren.Count; i++)
            {
                _seq.Join(visualChildren[i].DOScale(new Vector3(hoverScaleX, hoverScaleY, 1f), hoverDuration).SetEase(hoverEase));
            }
            hoverTween = _seq;
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        mainController.HandleSubRegionUnhovered();

        if (null != hoverTween && true == hoverTween.IsActive())
        {
            hoverTween.Kill();
        }
        // 원래 크기로 찰지게 되감기
        Sequence _seq = DOTween.Sequence();
        for (int i = 0; i < visualChildren.Count; i++)
        {
            _seq.Join(visualChildren[i].DOScale(1f, unhoverDuration).SetEase(unhoverEase));
        }
        hoverTween = _seq;
    }

    public void ResetState()
    {
        if (null != hoverTween && hoverTween.IsActive()) hoverTween.Kill();
        if (null != appearTween && appearTween.IsActive()) appearTween.Kill();
        if (null != disappearTween && disappearTween.IsActive()) disappearTween.Kill();
        if (null != clearNewTween && clearNewTween.IsActive()) clearNewTween.Kill();
        if (null != unlockTween && unlockTween.IsActive()) unlockTween.Kill();
        if (null != selectTween && selectTween.IsActive()) selectTween.Kill();

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
        bool _hasNew = (null != newIndicatorObj && true == newIndicatorObj.activeSelf);
        
        if (true == _hasNew)
        {
            ClearNewIndicator(() => {
                ExecuteDisappearMotion(_onComplete);
            });
        }
        else
        {
            ExecuteDisappearMotion(_onComplete);
        }
    }

    private void ExecuteDisappearMotion(Action _onComplete)
    {
        if (null != disappearTween && true == disappearTween.IsActive())
        {
            disappearTween.Kill();
        }

        Sequence _seq = DOTween.Sequence();
        
        // 역재생 느낌으로 원래대로 작아짐 (Y축 0.01로 축소)
        _seq.Append(transform.DOScaleY(0.01f, 0.15f).SetEase(Ease.InBack));
        
        _seq.OnComplete(() => {
            gameObject.SetActive(false);
            _onComplete?.Invoke();
        });

        disappearTween = _seq;
    }

    private Action pendingUnlockCompleteAction;

    public void PlayUnlockMotion(Action _onComplete)
    {
        if (null != newIndicatorObj)
        {
            newIndicatorObj.SetActive(true);
        }

        pendingUnlockCompleteAction = _onComplete;

        if (null != unlockTween && true == unlockTween.IsActive())
        {
            unlockTween.Kill();
        }

        // [TODO] 추후 DOTween 연출 작성
        // unlockTween = ...
        
        // 임시 즉시 완료
        OnUnlockMotionComplete();
    }

    private void OnUnlockMotionComplete()
    {
        Image _targetColorImage = (null != lockColorTargetImage) ? lockColorTargetImage : clickImage;
        if (null != _targetColorImage)
        {
            _targetColorImage.DOColor(unlockedColor, unlockDuration);
        }
        pendingUnlockCompleteAction?.Invoke();
        pendingUnlockCompleteAction = null;
    }

    public void SetSelectedState(bool _isSelected)
    {
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
        if (null != newIndicatorObj && true == newIndicatorObj.activeSelf)
        {
            if (null != clearNewTween && true == clearNewTween.IsActive())
            {
                clearNewTween.OnComplete(() => {
                    OnClearNewComplete();
                    _onComplete?.Invoke();
                });
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

            _seq.OnComplete(() => {
                OnClearNewComplete();
                _onComplete?.Invoke();
            });

            clearNewTween = _seq;
        }
        else
        {
            _onComplete?.Invoke();
        }
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
}
