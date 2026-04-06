using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class UI_ZoneSelectSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    // 외부 의존성
    [Header("Visual Components")]
    [SerializeField] private Image unlockedImage;    // 해금 상태 이미지
    [SerializeField] private Image lockedImage;      // 잠금 상태 이미지
    [SerializeField] private TextMeshProUGUI zoneNameText; // 구역 명칭 (예: 1-1)
    [SerializeField] private Image highlightImage;   // 선택 강조 이미지

    // 내부 의존성
    private int regionId;
    private int zoneId;
    private bool isLocked;
    private Action<int, int> onSelectAction;

    public void Initialize(int _regionId, int _zoneId, string _name, bool _isLocked, Action<int, int> _onSelect)
    {
        regionId = _regionId;
        zoneId = _zoneId;
        onSelectAction = _onSelect;
        SetLockStatus(_isLocked);

        if (zoneNameText != null) 
            zoneNameText.text = _name;
    }

    public void OnShow()
    {
        // 지역 활성화 시 구역 상태에 따른 처리 타이밍
    }

    public void OnHide()
    {
        // 지역 비활성화 시 구역 상태 정리 타이밍
    }

    public void SetLockStatus(bool _locked)
    {
        isLocked = _locked;

        if (unlockedImage != null) 
            unlockedImage.gameObject.SetActive(!isLocked);

        if (lockedImage != null) 
            lockedImage.gameObject.SetActive(isLocked);
    }

    public void SetHighlight(bool _active)
    {
        if (highlightImage != null) 
            highlightImage.gameObject.SetActive(_active && !isLocked);
    }

    // --- 입력 신호 발생 타이밍: 지역ID와 구역ID를 상위로 전달 ---
    public void OnPointerClick(PointerEventData _eventData)
    {
        if (isLocked) 
            return;
        
        onSelectAction?.Invoke(regionId, zoneId);
    }

    public void OnPointerEnter(PointerEventData _eventData) { }
    public void OnPointerExit(PointerEventData _eventData) { }
}
