using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UI_OptionKeyBindRow : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI actionNameText;     // 액션 한글 이름
    [SerializeField] private Image keyIconImage;                  // 키 아이콘 이미지
    [SerializeField] private TextMeshProUGUI keyFallbackText;    // 아이콘 없을 때 폴백 텍스트
    [SerializeField] private UI_OptionButton rebindButton;        // "변경" 버튼
    [SerializeField] private UI_OptionButton resetButton;         // "초기화" 버튼
    [SerializeField] private UI_OptionButton iconRebindButton;    // "아이콘 이미지" 클릭 버튼 (선택 사항)

    public UI_OptionButton RebindButton => rebindButton;
    public UI_OptionButton ResetButton => resetButton;

    [Header("Conflict Warning")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color conflictColor = Color.red;

    private ERebindableAction boundAction;
    private KeyIconDatabase iconDatabase;
    private Action<ERebindableAction> onRebindRequested;
    private Action<ERebindableAction> onResetRequested;

    // 캐싱된 델리게이트 (GC 할당 방지)
    private Action cachedOnRebindClicked;
    private Action cachedOnResetClicked;

    [Header("Focus Visual Settings")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Color normalBgColor = Color.white;
    [SerializeField] private Color hoverBgColor = Color.white;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.white;

    private InputManager inputManager;
    private Action<bool> cachedHandleButtonFocusChanged;

    private void Awake()
    {
        if (null == backgroundImage)
        {
            backgroundImage = GetComponent<Image>();
            if (null == backgroundImage)
            {
                backgroundImage = GetComponentInChildren<Image>();
            }
        }
        if (null != backgroundImage && null == normalSprite)
        {
            normalSprite = backgroundImage.sprite;
        }

        if (null == cachedHandleButtonFocusChanged)
        {
            cachedHandleButtonFocusChanged = HandleButtonFocusChanged;
        }
    }

    public void SetRowFocus(bool _isFocused)
    {
        if (true == _isFocused)
        {
            if (null == inputManager || false == inputManager.IsGamepadMode) return;
        }

        if (null != backgroundImage)
        {
            if (true == _isFocused && null != hoverSprite)
            {
                backgroundImage.sprite = hoverSprite;
            }
            else if (null != normalSprite)
            {
                backgroundImage.sprite = normalSprite;
            }
            backgroundImage.color = (true == _isFocused) ? hoverBgColor : normalBgColor;
        }

        if (null != actionNameText)
        {
            actionNameText.color = (true == _isFocused) ? hoverTextColor : normalTextColor;
        }
    }

    private void HandleButtonFocusChanged(bool _isFocused)
    {
        SetRowFocus(_isFocused);
    }

    public void Initialize(
        ERebindableAction _action,
        string _label,
        string _bindingPath,
        string _displayString,
        bool _isConflict,
        KeyIconDatabase _iconDB,
        Action<ERebindableAction> _onRebind,
        Action<ERebindableAction> _onReset)
    {
        boundAction = _action;
        iconDatabase = _iconDB;
        onRebindRequested = _onRebind;
        onResetRequested = _onReset;

        if (null == cachedOnRebindClicked) cachedOnRebindClicked = OnRebindClicked;
        if (null == cachedOnResetClicked) cachedOnResetClicked = OnResetClicked;

        if (null != cachedOnRebindClicked)
        {
            if (null != rebindButton)
            {
                rebindButton.Initialize(cachedOnRebindClicked);
                rebindButton.OnFocusChanged -= cachedHandleButtonFocusChanged;
                rebindButton.OnFocusChanged += cachedHandleButtonFocusChanged;
            }
            if (null != iconRebindButton)
            {
                iconRebindButton.Initialize(cachedOnRebindClicked);
                iconRebindButton.OnFocusChanged -= cachedHandleButtonFocusChanged;
                iconRebindButton.OnFocusChanged += cachedHandleButtonFocusChanged;
            }
        }
        
        if (null != resetButton)
        {
            resetButton.Initialize(cachedOnResetClicked);
            resetButton.OnFocusChanged -= cachedHandleButtonFocusChanged;
            resetButton.OnFocusChanged += cachedHandleButtonFocusChanged;
        }

        if (null != actionNameText) actionNameText.text = _label;

        Refresh(_bindingPath, _displayString, _isConflict);
    }

    /// <summary>
    /// 키 표시 갱신. 아이콘이 있으면 Image, 없으면 텍스트 폴백.
    /// </summary>
    public void Refresh(string _bindingPath, string _displayString, bool _isConflict)
    {
        Sprite _icon = null;
        if (null != iconDatabase)
        {
            _icon = iconDatabase.GetIcon(_bindingPath);
        }

        // 아이콘 모드 vs 텍스트 폴백 모드
        if (null != _icon)
        {
            // 아이콘이 매핑된 경우: 이미지 표시, 텍스트 숨김
            if (null != keyIconImage)
            {
                keyIconImage.sprite = _icon;
                keyIconImage.enabled = true;
                keyIconImage.color = true == _isConflict ? conflictColor : normalColor;
            }
            if (null != keyFallbackText) keyFallbackText.gameObject.SetActive(false);
        }
        else
        {
            // 아이콘이 없는 경우: 이미지 숨김, 텍스트 표시
            if (null != keyIconImage) keyIconImage.enabled = false;
            if (null != keyFallbackText)
            {
                keyFallbackText.gameObject.SetActive(true);
                keyFallbackText.text = _displayString;
                keyFallbackText.color = true == _isConflict ? conflictColor : normalColor;
            }
        }
    }

    public void RefreshLabel(string _label)
    {
        if (null != actionNameText) actionNameText.text = _label;
    }

    private void OnRebindClicked()
    {
        if (null != onRebindRequested) onRebindRequested.Invoke(boundAction);
    }

    private void OnResetClicked()
    {
        if (null != onResetRequested) onResetRequested.Invoke(boundAction);
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        inputManager = _inputManager;
        if (null != rebindButton) rebindButton.SetCursorBoxUI(_cursorBoxUI, _inputManager);
        if (null != resetButton) resetButton.SetCursorBoxUI(_cursorBoxUI, _inputManager);
        if (null != iconRebindButton) iconRebindButton.SetCursorBoxUI(_cursorBoxUI, _inputManager);
    }

    private void OnDisable()
    {
        SetRowFocus(false);
    }

    private void OnDestroy()
    {
        if (null != rebindButton) rebindButton.OnFocusChanged -= cachedHandleButtonFocusChanged;
        if (null != resetButton) resetButton.OnFocusChanged -= cachedHandleButtonFocusChanged;
        if (null != iconRebindButton) iconRebindButton.OnFocusChanged -= cachedHandleButtonFocusChanged;

        onRebindRequested = null;
        onResetRequested = null;
        cachedOnRebindClicked = null;
        cachedOnResetClicked = null;
        cachedHandleButtonFocusChanged = null;
    }
}
