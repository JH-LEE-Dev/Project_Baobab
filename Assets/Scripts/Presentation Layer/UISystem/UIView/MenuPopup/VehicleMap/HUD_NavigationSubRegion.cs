using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationSubRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Color Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectColor = Color.green;
    [SerializeField] private Color lockColor = Color.gray;

    [Header("Motion Tags")]
    [SerializeField] private string hoverTag = "Hover";
    [SerializeField] private string hoverOffTag = "HoverOff";
    [SerializeField] private string clickTag = "Click";

    // //내부 의존성
    private RectTransform rect;
    private ForestEnvironmentInfo forestInfo;
    private Action<RectTransform, Vector2> onHoverEnterEvent;
    private Action onHoverExitEvent;
    private Action<int> onSelectEvent;

    private MotionEntry enterMotion;
    private MotionEntry exitMotion;
    private MotionEntry clickMotion;

    private int fieldNumber = 0;
    private bool isSelected = false;
    private bool isLocked = false;
    private bool isInitialized = false;
    private bool isClicked = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Setup(ForestEnvironmentInfo _info, int _number, Action<RectTransform, Vector2> _onHoverEnter, Action _onHoverExit, Action<int> _onSelect)
    {
        forestInfo = _info;
        Initialize(_number);

        SetSelect(false);
        SetNumber(_number);
        SetLock(!_info.bCanAccess);

        onHoverEnterEvent = _onHoverEnter;
        onHoverExitEvent = _onHoverExit;
        onSelectEvent = _onSelect;
    }

    public void Initialize(int _number)
    {
        if (true == isInitialized)
            return;

        rect = GetComponent<RectTransform>();

        SetSelect(false);
        SetLock(false);
        isInitialized = true;
    }

    public void SetLock(bool _isLock)
    {
        isLocked = _isLock;
        UpdateColor();
    }

    public void SetSelect(bool _isSelect)
    {
        isSelected = _isSelect;
        UpdateColor();
    }

    public void SetNumber(int _number)
    {
        fieldNumber = _number;
    }

    public void PlayOpenAnimation()
    {
        gameObject.SetActive(true);
    }

    public void PlayCloseAnimation()
    {
        gameObject.SetActive(false);
    }

    public ForestType GetForestType()
    {
        return forestInfo.forestType;
    }

    public ForestEnvironmentInfo GetForestInfo()
    {
        return forestInfo;
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public int GetNumber()
    {
        return fieldNumber;
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    public RectTransform GetRectTransform()
    {
        if (null == rect)
            rect = GetComponent<RectTransform>();

        return rect;
    }


    // //내부 로직

    private void UpdateColor()
    {
        if (null == iconImage)
            return;

        if (true == isLocked)
            iconImage.color = lockColor;
        else if (true == isSelected)
            iconImage.color = selectColor;
        else
            iconImage.color = normalColor;
    }

    private void OnClickAnimationComplete()
    {
        isClicked = false;
    }


    // //Event System 구현부

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (true == isLocked)
            return;

        RectTransform _targetRect = GetRectTransform();
        onHoverEnterEvent?.Invoke(_targetRect, _targetRect.rect.size);

        if (null != motionPlayer && false == isClicked)
        {
            if (null != exitMotion)
                motionPlayer.SettingEntryMotion(exitMotion, true, true);
            if (null != clickMotion)
                motionPlayer.SettingEntryMotion(clickMotion, true, true);
            enterMotion = motionPlayer.Play(hoverTag, bReset: true);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (true == isLocked)
            return;

        onHoverExitEvent?.Invoke();

        if (null != motionPlayer && false == isClicked)
        {
            if (null != enterMotion)
                motionPlayer.SettingEntryMotion(enterMotion, true, true);
            if (null != clickMotion)
                motionPlayer.SettingEntryMotion(clickMotion, true, true);
            exitMotion = motionPlayer.Play(hoverOffTag, bReset: true);
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (true == isLocked)
            return;

        onSelectEvent?.Invoke(fieldNumber);

        isClicked = true;
        if (null != motionPlayer)
        {
            if (null != enterMotion)
                motionPlayer.SettingEntryMotion(enterMotion, true, true);
            if (null != exitMotion)
                motionPlayer.SettingEntryMotion(exitMotion, true, true);
            clickMotion = motionPlayer.Play(clickTag, bReset: true, _onComplete: OnClickAnimationComplete);
        }
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Awake()
    {
        if (false == isInitialized)
            Initialize(fieldNumber);
    }
}
