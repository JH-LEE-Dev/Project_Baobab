using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 선택 화면의 선택 대상(카드 슬롯·리롤 버튼)이 입력을 화면과 카드 연출에 알리는 중계기.
///   포커스   마우스가 올라왔을 때(IPointerEnter)와 패드·키보드 선택이 옮겨 왔을 때(ISelect)가 같은 FocusedEvent를 올려서,
///            호버와 선택 강조가 항상 같은 연출이 되게 한다(UI_WorkList 0장 "마우스 호버와 선택 강조를 같은 연출로").
///   누름     마우스를 누르는 동안 PressedEvent(true), 떼거나 대상 밖으로 나가면 PressedEvent(false). 패드·키보드 제출은 SubmittedEvent 한 번(누름과 뗌이 같이 온다).
///
/// 커서 박스가 따라갈 대상은 cursorTarget이다. 비어 있으면 이 오브젝트 자신. 카드 슬롯은 판정 박스(HitBox)가 이 컴포넌트를 갖고,
/// 커서 박스는 호버로 올라가는 카드 그림(Visual)을 따라가게 한다.
/// </summary>
public class UI_RoguelikeSelectRelay : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISubmitHandler
{
    public event Action<UI_RoguelikeSelectRelay> FocusedEvent;
    public event Action<bool> PressedEvent;
    public event Action SubmittedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform cursorTarget;

    //상태 변수
    private bool bPressed;

    public RectTransform CursorTarget => null != cursorTarget ? cursorTarget : (RectTransform)transform;

    /// <summary>
    /// 마지막 포커스가 마우스 호버에서 왔는지(true) 패드·키보드 선택에서 왔는지(false). FocusedEvent 핸들러 안에서 읽는다.
    /// 스크롤하는 화면이 패드 선택일 때만 선택한 칸을 보이게 스크롤하고, 마우스 호버로는 스크롤하지 않게 하려는 것이다
    /// (호버로 스크롤하면 커서 아래 칸이 바뀌어 호버가 계속 옮겨 다닌다).
    /// </summary>
    public bool IsPointerFocus { get; private set; }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        IsPointerFocus = true;
        FocusedEvent?.Invoke(this);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        IsPointerFocus = false;
        FocusedEvent?.Invoke(this);
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        SetPressed(false);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        SetPressed(false);
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        SubmittedEvent?.Invoke();
    }

    private void SetPressed(bool _bPressed)
    {
        if (bPressed == _bPressed) return;

        bPressed = _bPressed;
        PressedEvent?.Invoke(bPressed);
    }

    private void OnDisable()
    {
        SetPressed(false);
    }

    private void OnDestroy()
    {
        FocusedEvent = null;
        PressedEvent = null;
        SubmittedEvent = null;
    }
}
