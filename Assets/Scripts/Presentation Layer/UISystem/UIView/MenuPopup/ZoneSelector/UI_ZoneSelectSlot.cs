using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class UI_ZoneSelectSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Components")]
    [SerializeField] private Image unlockedImage;
    [SerializeField] private Image lockedImage;
    [SerializeField] private Image highlightImage;

    private int regionId;
    private int zoneId;
    private bool isLocked;
    
    private Action<int, int> onSelectAction;
    private Action<int, int> onHoverEnterAction;
    private Action onHoverExitAction;

    public void Initialize(int _regionId, int _zoneId, bool _isLocked, Action<int, int> _onSelect, Action<int, int> _onHoverEnter, Action _onHoverExit)
    {
        regionId = _regionId;
        zoneId = _zoneId;
        onSelectAction = _onSelect;
        onHoverEnterAction = _onHoverEnter;
        onHoverExitAction = _onHoverExit;
        
        SetLockStatus(_isLocked);
        SetHighlight(false);
    }

    public void SetLockStatus(bool _locked)
    {
        isLocked = _locked;
        if (unlockedImage != null) unlockedImage.gameObject.SetActive(!isLocked);
        if (lockedImage != null) lockedImage.gameObject.SetActive(isLocked);
    }

    public void SetHighlight(bool _active)
    {
        if (highlightImage != null) highlightImage.gameObject.SetActive(_active);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (isLocked) return;
        onSelectAction?.Invoke(regionId, zoneId);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        // 잠금 여부와 상관없이 호버 정보를 표시하도록 수정 (선택 시 차단은 Selector에서 처리)
        onHoverEnterAction?.Invoke(regionId, zoneId);
    }

    public void OnPointerExit(PointerEventData _eventData) 
    { 
        onHoverExitAction?.Invoke();
    }
}
