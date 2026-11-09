using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 화면의 글자 버튼(결과 화면의 다시 도전 · 메인메뉴 등). BasicButton 9-슬라이스 바탕 + 가운데 글자.
///
/// [포커스 = 호버] 마우스가 올라오거나(IPointerEnter) 패드 선택이 옮겨 오면(ISelect) 같은 FocusedEvent를 올린다.
///   강조는 화면이 SetFocused로 정한다(한 화면에 강조된 버튼은 하나): 강조 = 금색 테두리 + 1px 위로 + 진한 글자, 아니면 어둡게 덮은 바탕 + 흐린 글자.
///   크기는 바꾸지 않는다(픽셀 UI 규칙 - 스케일 1 유지). 움직임은 정수 픽셀이다.
/// [폭] 폭은 화면이 SetWidth로 정한다(18개 언어 중 가장 긴 문구 기준). 글자 높이에 맞춘 높이는 프리팹(구성기)이 정한다.
/// </summary>
public class UI_RoguelikeTextButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    public event Action<UI_RoguelikeTextButton> FocusedEvent;
    public event Action<UI_RoguelikeTextButton> ClickedEvent;

    private static readonly Color FocusedTextColor = new Color(0.23f, 0.14f, 0.09f, 1f);
    private static readonly Color IdleTextColor = new Color(0.42f, 0.32f, 0.22f, 1f);

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Selectable selectable;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image focusFrame;
    [SerializeField] private Image idleShade;
    [SerializeField] private TMP_Text label;

    //상태 변수
    private bool bFocused;
    private bool bInteractable = true;

    public Selectable Selectable => selectable;
    public TMP_Text Label => label;
    public RectTransform Rect => (RectTransform)transform;

    public void SetText(string _text)
    {
        if (null != label) label.text = _text;
    }

    /// <summary>현재 글자의 한 줄 폭(px).</summary>
    public float GetPreferredTextWidth()
    {
        if (null == label) return 0f;
        return Mathf.Ceil(label.GetPreferredValues(label.text, 10000f, 10000f).x);
    }

    public void SetWidth(float _width)
    {
        RectTransform rect = Rect;
        rect.sizeDelta = new Vector2(Mathf.Round(_width), rect.sizeDelta.y);
    }

    public void SetFocused(bool _bFocused)
    {
        bFocused = _bFocused;

        if (null != focusFrame) focusFrame.enabled = bFocused;
        if (null != idleShade) idleShade.enabled = false == bFocused;
        if (null != label) label.color = bFocused ? FocusedTextColor : IdleTextColor;
        if (null != visualRoot) visualRoot.anchoredPosition = bFocused ? new Vector2(0f, 1f) : Vector2.zero;
    }

    /// <summary>등장 연출 중에는 눌리지 않게 막는다(포커스 표시는 유지).</summary>
    public void SetInteractable(bool _bInteractable)
    {
        bInteractable = _bInteractable;
    }

    public void SetNavigation(Selectable _left, Selectable _right)
    {
        if (null == selectable) return;

        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = _left,
            selectOnRight = _right,
        };
        selectable.navigation = navigation;
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        FocusedEvent?.Invoke(this);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        FocusedEvent?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (PointerEventData.InputButton.Left != _eventData.button) return;
        Click();
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        Click();
    }

    private void Click()
    {
        if (false == bInteractable) return;
        ClickedEvent?.Invoke(this);
    }

    private void OnDestroy()
    {
        FocusedEvent = null;
        ClickedEvent = null;
    }
}
