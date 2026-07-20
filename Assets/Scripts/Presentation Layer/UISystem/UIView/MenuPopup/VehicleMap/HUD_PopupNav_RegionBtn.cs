using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;

public class HUD_PopupNav_RegionBtn : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [Tooltip("버튼 클릭 영역 (레이캐스트용 이미지)")]
    [SerializeField] private Image clickImage;
    [Tooltip("이름을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("NEW 뱃지 오브젝트")]
    [SerializeField] private GameObject newIndicatorObj;
    [Tooltip("잠금(Lock) 아이콘 등 상태 비주얼 오브젝트")]
    [SerializeField] private GameObject lockVisualObj;

    [Header("DOTween Settings (Placeholders)")]
    [Tooltip("추후 트위닝 연출에 사용될 설정값 자리")]
    [SerializeField] private float unlockDuration = 0.5f;
    [SerializeField] private float selectDuration = 0.2f;

    private Tween unlockTween;
    private Tween selectTween;

    private HUD_PopupNav_Main mainController;
    private MapEnvironmentDataInfo myInfo;

    public MapType GetMapType() => myInfo.mapType;

    public void Initialize(HUD_PopupNav_Main _mainController, MapEnvironmentDataInfo _info, LocalizationManager _localizationManager)
    {
        mainController = _mainController;
        myInfo = _info;

        if (null != nameText && null != _localizationManager)
        {
            string _localizedName = _localizationManager.GetText(_info.mapType);
            if (false == string.IsNullOrEmpty(_localizedName))
            {
                nameText.text = _localizedName;
            }
        }

        bool _isLocked = !_info.isUnlocked;
        if (null != lockVisualObj)
        {
            lockVisualObj.SetActive(_isLocked);
        }

        if (null != newIndicatorObj)
        {
            newIndicatorObj.SetActive(_info.isNew && !_isLocked);
        }

        SetSelectedState(false);
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
        // 필요 시 호버 연출 추가
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        // 필요 시 언호버 연출 추가
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

    public void SetSelectedState(bool _isSelected)
    {
        if (null != selectTween && true == selectTween.IsActive())
        {
            selectTween.Kill();
        }

        // [TODO] 추후 이곳에 DOTween 연출(예: 컬러 변경, 스케일 변경 등) 작성
        if (true == _isSelected)
        {
            // selectTween = ...
        }
        else
        {
            // selectTween = ...
        }
    }

    public void ClearNewIndicator()
    {
        if (null != newIndicatorObj && true == newIndicatorObj.activeSelf)
        {
            newIndicatorObj.SetActive(false);
        }
    }
}
