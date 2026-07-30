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
            if (null != rebindButton) rebindButton.Initialize(cachedOnRebindClicked);
            if (null != iconRebindButton) iconRebindButton.Initialize(cachedOnRebindClicked);
        }
        
        if (null != resetButton) resetButton.Initialize(cachedOnResetClicked);

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

    private void OnDestroy()
    {
        onRebindRequested = null;
        onResetRequested = null;
        cachedOnRebindClicked = null;
        cachedOnResetClicked = null;
    }
}
