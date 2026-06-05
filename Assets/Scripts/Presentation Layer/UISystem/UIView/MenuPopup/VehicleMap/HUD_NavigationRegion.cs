using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
    [Header("UI References")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject lockObject;
    [SerializeField] private TextMeshProUGUI regionNameText;

    [Header("Color Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectColor = Color.green;
    [SerializeField] private Color lockColor = Color.gray;

    [Header("Motion Tags")]
    [SerializeField] private string hoverTag = "Hover";
    [SerializeField] private string clickTag = "Click";
    [SerializeField] private string unHoverTag = "unHover";

    // //내부 의존성
    private MapType mapType = MapType.None;
    private Action<MapType> onSelectEvent;

    private MotionEntry hoverEntry;
    private MotionEntry clickEntry;
    private MotionEntry unHoverEntry;
    private bool isClicked = false;
    private bool isLocked = false;
    private bool isSelected = false;
    private bool isInitialized = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(MapType _mapType, Action<MapType> _onSelect)
    {
        if (true == isInitialized)
            return;

        mapType = _mapType;
        onSelectEvent = _onSelect;
        isClicked = false;
        isLocked = false;
        isSelected = false;

        if (null != omp)
            omp.Initialize();

        SetSelect(false);
        SetLock(false);

        isInitialized = true;
    }

    public void SetLock(bool _isLock)
    {
        isLocked = _isLock;

        if (null != lockObject)
            lockObject.SetActive(isLocked);

        if (null != regionNameText)
            regionNameText.gameObject.SetActive(!isLocked);

        UpdateColor();
    }

    public void SetSelect(bool _isSelect)
    {
        isSelected = _isSelect;
        UpdateColor();
    }

    public MapType GetMapType()
    {
        return mapType;
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public bool IsSelected()
    {
        return isSelected;
    }


    // //내부 로직

    private void UpdateColor()
    {
        if (null == buttonImage)
            return;

        if (true == isLocked)
            buttonImage.color = lockColor;
        else if (true == isSelected)
            buttonImage.color = selectColor;
        else
            buttonImage.color = normalColor;
    }

    private void OnClickAnimationComplete()
    {
        isClicked = false;
    }


    // //Event System 구현부

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (true == isLocked)
            return;

        isClicked = true;
        onSelectEvent?.Invoke(mapType);

        if (null != omp)
        {
            if (null != hoverEntry)
                omp.SettingEntryMotion(hoverEntry, true, true);
            if (null != unHoverEntry)
                omp.SettingEntryMotion(unHoverEntry, true, true);
            clickEntry = omp.Play(clickTag, bReset: true, _onComplete: OnClickAnimationComplete);
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (true == isLocked)
            return;

        if (true == isClicked)
            return;

        if (null != omp)
        {
            if (null != unHoverEntry)
                omp.SettingEntryMotion(unHoverEntry, true, true);
            if (null != clickEntry)
                omp.SettingEntryMotion(clickEntry, true, true);
            hoverEntry = omp.Play(hoverTag, bReset: true);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (true == isLocked)
            return;

        if (true == isClicked)
            return;

        if (null != omp)
        {
            if (null != hoverEntry)
                omp.SettingEntryMotion(hoverEntry, true, true);
            if (null != clickEntry)
                omp.SettingEntryMotion(clickEntry, true, true);
            unHoverEntry = omp.Play(unHoverTag, bReset: true);
        }
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Awake()
    {
        if (false == isInitialized)
            Initialize(mapType, onSelectEvent);
    }
}
