using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 선택 화면의 선택 대상(카드 슬롯·건너뛰기·리롤 버튼)이 "여기에 포커스가 왔다"를 화면에 알리는 중계기.
/// 마우스가 올라왔을 때(IPointerEnter)와 패드·키보드 선택이 옮겨 왔을 때(ISelect) 같은 이벤트를 올려서,
/// 호버와 선택 강조가 항상 같은 연출이 되게 한다(UI_WorkList 0장 "마우스 호버와 선택 강조를 같은 연출로").
///
/// 커서 박스가 따라갈 대상은 cursorTarget이다. 비어 있으면 이 오브젝트 자신. 카드 슬롯은 판정 박스(HitBox)가 이 컴포넌트를 갖고,
/// 커서 박스는 호버로 올라가는 카드 그림(Visual)을 따라가게 한다.
/// </summary>
public class UI_RoguelikeSelectRelay : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public event Action<UI_RoguelikeSelectRelay> FocusedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform cursorTarget;

    public RectTransform CursorTarget => null != cursorTarget ? cursorTarget : (RectTransform)transform;

    public void OnPointerEnter(PointerEventData _eventData)
    {
        FocusedEvent?.Invoke(this);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        FocusedEvent?.Invoke(this);
    }

    private void OnDestroy()
    {
        FocusedEvent = null;
    }
}
