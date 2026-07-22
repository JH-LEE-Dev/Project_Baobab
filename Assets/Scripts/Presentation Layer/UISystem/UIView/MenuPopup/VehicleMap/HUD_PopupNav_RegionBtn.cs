using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_PopupNav_RegionBtn : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [Tooltip("버튼 클릭 영역 (레이캐스트용 이미지)")]
    [SerializeField] private Image clickImage;
    [Tooltip("배경 이미지를 교체할 대상 Image (비워두면 clickImage 사용)")]
    [SerializeField] private Image bgImage;
    [Tooltip("NEW 뱃지 오브젝트")]
    [SerializeField] private GameObject newIndicatorObj;
    [Tooltip("잠금(Lock) 아이콘 등 상태 비주얼 오브젝트")]
    [SerializeField] private GameObject lockVisualObj;

    [Header("DOTween Settings")]
    [Tooltip("해금 연출 시간")]
    [SerializeField] private float unlockDuration = 0.5f;
    [Tooltip("선택 시 연출 시간")]
    [SerializeField] private float selectDuration = 0.2f;
    
    [Header("Hover Animation (Region)")]
    [Tooltip("호버 시작 시 눌려질 Y축 스케일 (뽀잉 연출용)")]
    [SerializeField] private float hoverStartYScale = 0.8f;
    [Tooltip("원래대로 빡! 하고 돌아오는 연출 시간")]
    [SerializeField] private float hoverSnapDuration = 0.35f;
    [Tooltip("원래대로 돌아오는 이즈(Ease)")]
    [SerializeField] private Ease hoverSnapEase = Ease.OutBack;

    [Header("Select Animation")]
    [Tooltip("선택(클릭) 시 흔들림 강도")]
    [SerializeField] private float selectPunchStrength = 1.1f;

    private Tween unlockTween;
    private Tween hoverTween;
    private Tween clearNewTween;
    
    // 비주얼 연출을 적용할 자식 트랜스폼 목록 (clickImage 제외)
    private System.Collections.Generic.List<Transform> visualChildren = new System.Collections.Generic.List<Transform>();

    private HUD_PopupNav_Main mainController;
    private MapEnvironmentDataInfo myInfo;
    
    // 런타임 캐싱 데이터
    private ParticleSystem newIndicatorParticle;
    public TweenCallback CachedActivate { get; private set; }
    private TweenCallback cachedPlayParticle;
    private TweenCallback cachedClearNewComplete;

    public MapType GetMapType() => myInfo.mapType;

    public void Initialize(HUD_PopupNav_Main _mainController, MapEnvironmentDataInfo _info, LocalizationManager _localizationManager, Sprite _bgSprite = null)
    {
        mainController = _mainController;
        myInfo = _info;

        if (null != _bgSprite)
        {
            Image _targetImage = (null != bgImage) ? bgImage : clickImage;
            if (null != _targetImage)
            {
                _targetImage.sprite = _bgSprite;
            }
        }

        bool _isLocked = !_info.isUnlocked;
        if (null != lockVisualObj)
        {
            lockVisualObj.SetActive(_isLocked);
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

        mainController.HandleRegionSelected(myInfo.mapType);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }
        
        ClearNewIndicator();

        if (null != hoverTween && true == hoverTween.IsActive())
        {
            hoverTween.Kill();
        }
        
        // clickImage(Raycast) 영역이 찌그러지지 않도록 루트 대신 비주얼 자식들만 연출
        Sequence _seq = DOTween.Sequence();
        for (int i = 0; i < visualChildren.Count; i++)
        {
            visualChildren[i].localScale = new Vector3(1f, hoverStartYScale, 1f);
            _seq.Join(visualChildren[i].DOScale(Vector3.one, hoverSnapDuration).SetEase(hoverSnapEase));
        }
        hoverTween = _seq;
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        if (null != hoverTween && true == hoverTween.IsActive())
        {
            hoverTween.Kill();
        }
        
        for (int i = 0; i < visualChildren.Count; i++)
        {
            visualChildren[i].localScale = Vector3.one;
        }
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

        // [TODO] 추후 이곳에 DOTween 연출(예: 흔들림, 커짐 등) 작성
        // unlockTween = transform.DOScale(1.2f, unlockDuration).OnComplete(OnUnlockMotionComplete);
        
        // 임시로 즉시 완료 처리
        OnUnlockMotionComplete();
    }

    private void OnUnlockMotionComplete()
    {
        if (null != lockVisualObj)
        {
            lockVisualObj.SetActive(false);
        }
        pendingUnlockCompleteAction?.Invoke();
        pendingUnlockCompleteAction = null;
    }

    private Tween selectTween;

    public void SetSelectedState(bool _isSelected, bool _playClickAnim = true)
    {
        if (null != selectTween && true == selectTween.IsActive())
        {
            selectTween.Kill();
        }

        if (true == _isSelected)
        {
            if (_playClickAnim)
            {
                // 선택 시 살짝 눌리는(Punch) 연출 (루트 대신 자식들)
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
        else
        {
            for (int i = 0; i < visualChildren.Count; i++)
            {
                visualChildren[i].localScale = Vector3.one;
            }
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

    private void OnClearNewComplete()
    {
        if (null != newIndicatorObj)
        {
            newIndicatorObj.SetActive(false);
            newIndicatorObj.transform.localScale = Vector3.one;
        }
    }
}
