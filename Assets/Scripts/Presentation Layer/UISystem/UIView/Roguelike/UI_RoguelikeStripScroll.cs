using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 기록 창 카드 띠용 가로 ScrollRect. 본문(세로 ScrollRect) 안에 들어 있어 끌기 · 휠을 나눠 처리한다.
///   끌기: 시작할 때의 방향으로 정한다. 세로로 끌기 시작하면(또는 띠가 넘치지 않으면) 상위 본문 ScrollRect에 그대로 넘기고, 가로로 끌기 시작하면 이 띠가 스크롤한다.
///   휠: 띠가 넘칠 때는 한 칸씩(WheelStepEvent -1 / +1)을 화면 스크립트에 알려 칸 맞춤 미끄러짐으로 처리하게 하고, 넘치지 않으면 본문에 넘긴다.
/// 관성은 ScrollRect 기본 동작이고(끝에서는 더 밀리지 않는 Clamped), 칸 맞춤 미끄러짐은 화면 스크립트가 한다.
/// </summary>
public class UI_RoguelikeStripScroll : ScrollRect
{
    private const float OverflowTolerance = 0.5f;

    //내부 의존성
    private ScrollRect parentScroll;

    //상태 변수
    private bool bForwardDrag;
    private bool bDraggingSelf;

    /// <summary>휠 한 칸: 알림 값은 -1(왼쪽으로) 또는 +1(오른쪽으로).</summary>
    public event Action<int> WheelStepEvent;

    /// <summary>이 띠 자신이 끌리는 중이면 참(본문으로 넘긴 끌기는 거짓).</summary>
    public bool IsDragging => bDraggingSelf;

    /// <summary>내용이 보이는 폭보다 넓어 가로로 스크롤할 수 있으면 참.</summary>
    public bool CanScrollHorizontally
    {
        get
        {
            RectTransform view = null != viewport ? viewport : (RectTransform)transform;
            return null != content && content.rect.width - view.rect.width > OverflowTolerance;
        }
    }

    public override void OnInitializePotentialDrag(PointerEventData _eventData)
    {
        base.OnInitializePotentialDrag(_eventData);
        if (null != parentScroll) parentScroll.OnInitializePotentialDrag(_eventData);
    }

    public override void OnBeginDrag(PointerEventData _eventData)
    {
        if (PointerEventData.InputButton.Left != _eventData.button) return;

        Vector2 moved = _eventData.position - _eventData.pressPosition;
        bool bVertical = Mathf.Abs(moved.x) < Mathf.Abs(moved.y);
        bForwardDrag = null != parentScroll && (true == bVertical || false == CanScrollHorizontally);
        if (true == bForwardDrag)
        {
            parentScroll.OnBeginDrag(_eventData);
            return;
        }

        bDraggingSelf = true;
        base.OnBeginDrag(_eventData);
    }

    public override void OnDrag(PointerEventData _eventData)
    {
        if (true == bForwardDrag)
        {
            parentScroll.OnDrag(_eventData);
            return;
        }

        base.OnDrag(_eventData);
    }

    public override void OnEndDrag(PointerEventData _eventData)
    {
        if (true == bForwardDrag)
        {
            bForwardDrag = false;
            parentScroll.OnEndDrag(_eventData);
            return;
        }

        bDraggingSelf = false;
        base.OnEndDrag(_eventData);
    }

    public override void OnScroll(PointerEventData _eventData)
    {
        if (false == CanScrollHorizontally)
        {
            if (null != parentScroll) parentScroll.OnScroll(_eventData);
            return;
        }

        Vector2 delta = _eventData.scrollDelta;
        float amount = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) ? delta.y : delta.x;
        if (0f == amount) return;

        // 휠을 위로 굴리면 왼쪽(처음)으로, 아래로 굴리면 오른쪽으로 한 칸.
        WheelStepEvent?.Invoke(0f < amount ? -1 : 1);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        parentScroll = null != transform.parent ? transform.parent.GetComponentInParent<ScrollRect>() : null;
        bForwardDrag = false;
        bDraggingSelf = false;
    }

    protected override void OnDisable()
    {
        bForwardDrag = false;
        bDraggingSelf = false;
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        WheelStepEvent = null;
        base.OnDestroy();
    }
}
