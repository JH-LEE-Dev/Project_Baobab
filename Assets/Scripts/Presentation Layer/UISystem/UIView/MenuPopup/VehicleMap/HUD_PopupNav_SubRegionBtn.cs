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
    [Tooltip("서브지역 이름을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("NEW 뱃지 오브젝트")]
    [SerializeField] private GameObject newIndicatorObj;
    [Tooltip("잠금(Lock) 비주얼 오브젝트")]
    [SerializeField] private GameObject lockVisualObj;

    [Header("DOTween Settings (Placeholders)")]
    [Tooltip("추후 트위닝 연출에 사용될 설정값 자리")]
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private float disappearDuration = 0.3f;
    [SerializeField] private float unlockDuration = 0.5f;
    [SerializeField] private float selectDuration = 0.2f;

    private Tween appearTween;
    private Tween disappearTween;
    private Tween unlockTween;
    private Tween selectTween;

    private HUD_PopupNav_Main mainController;
    private ForestEnvironmentInfo myInfo;
    private MapType parentMapType;

    public ForestType GetForestType() => myInfo.forestType;

    public void Initialize(HUD_PopupNav_Main _mainController, ForestEnvironmentInfo _info, LocalizationManager _localizationManager, MapType _parentMapType)
    {
        mainController = _mainController;
        myInfo = _info;
        parentMapType = _parentMapType;

        if (null != nameText && null != _localizationManager)
        {
            string _localizedName = _localizationManager.GetText(_info.forestType);
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
            mainController.HandleSubRegionHovered(myInfo.forestType, transform, myInfo);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null == mainController || true == mainController.IsInputBlocked)
        {
            return;
        }

        mainController.HandleSubRegionUnhovered();
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
        if (null != disappearTween && true == disappearTween.IsActive())
        {
            disappearTween.Kill();
        }

        // [TODO] 추후 DOTween 연출 작성
        // disappearTween = ...

        // 임시 즉시 완료
        _onComplete?.Invoke();
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

        // [TODO] 추후 DOTween 연출 작성
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
