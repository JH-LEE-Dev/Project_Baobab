using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 가는 단색 스크롤바(홈 + 막대). 판의 색감을 쓴다: 홈 #3A261C(어두운 갈색), 막대 #CA8F73(판 테두리의 밝은 색), 누르는 동안 #8F563B(중간 색).
/// 그림 없이 단색 Image이고(홈 색은 빌더가 칠한다), 보유 카드 창과 같은 정수 픽셀 배치에 막대 끌기를 더했다.
/// 이 컴포넌트는 홈(track) 오브젝트에 붙는다. 스크롤 값은 갖지 않는다: 화면 스크립트가 Refresh로 보이는 길이 · 내용 길이 · 현재 스크롤을 넘겨 모양을 그리고,
/// 사용자가 막대를 잡거나 홈을 누르면 ScrollRequestedEvent(0~1 비율, 0 = 맨 위 / 맨 왼쪽)로 원하는 위치를 알린다.
/// 내용이 넘치지 않으면 스스로 숨는다. 마우스 왼쪽 버튼만 받는다.
/// </summary>
public class UI_RoguelikeScrollbar : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float MinThumbLength = 8f;
    private static readonly Color ThumbNormalColor = new Color32(0xCA, 0x8F, 0x73, 255);
    private static readonly Color ThumbPressedColor = new Color32(0x8F, 0x56, 0x3B, 255);

    //외부 의존성
    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform thumb;
    [SerializeField, Tooltip("참이면 가로 막대(왼쪽 -> 오른쪽), 거짓이면 세로 막대(위 -> 아래).")] private bool bHorizontal;

    //상태 변수
    private bool bHeld;
    private float grabOffset;
    private UnityEngine.UI.Image thumbImage;

    public event Action<float> ScrollRequestedEvent;

    /// <summary>막대를 잡고 있는 동안 참(눌렀을 때부터 뗄 때까지).</summary>
    public bool IsHeld => bHeld;

    /// <summary>
    /// 막대 길이는 보이는 비율, 위치는 스크롤 비율로 그린다(정수 픽셀). 스크롤할 내용이 없으면 홈을 숨기고 거짓을 돌려준다.
    /// </summary>
    public bool Refresh(float _viewportLength, float _contentLength, float _scroll)
    {
        float max = _contentLength - _viewportLength;
        bool bScrollable = 0.5f < max;
        if (null == track || null == thumb) return bScrollable;

        if (bScrollable != track.gameObject.activeSelf) track.gameObject.SetActive(bScrollable);
        if (false == bScrollable)
        {
            bHeld = false;
            return false;
        }

        float trackLength = GetLength(track);
        float thumbLength = Mathf.Max(MinThumbLength, Mathf.Floor(trackLength * _viewportLength / _contentLength));
        float ratio = Mathf.Clamp01(_scroll / max);
        float offset = Mathf.Round((trackLength - thumbLength) * ratio);

        thumb.anchorMin = new Vector2(0f, 1f);
        thumb.anchorMax = new Vector2(0f, 1f);
        thumb.pivot = new Vector2(0f, 1f);
        if (true == bHorizontal)
        {
            thumb.sizeDelta = new Vector2(thumbLength, track.rect.height);
            thumb.anchoredPosition = new Vector2(offset, 0f);
        }
        else
        {
            thumb.sizeDelta = new Vector2(track.rect.width, thumbLength);
            thumb.anchoredPosition = new Vector2(0f, -offset);
        }

        return true;
    }

    private void SetThumbColor(Color _color)
    {
        if (null == thumbImage && null != thumb) thumbImage = thumb.GetComponent<UnityEngine.UI.Image>();
        if (null != thumbImage) thumbImage.color = _color;
    }

    private float GetLength(RectTransform _rect)
    {
        return true == bHorizontal ? _rect.rect.width : _rect.rect.height;
    }

    /// <summary>포인터가 홈 위 어디인지를 홈 왼쪽 위 기준 길이 방향 거리(px)로 바꾼다.</summary>
    private bool TryGetAlong(PointerEventData _eventData, out float _along)
    {
        _along = 0f;
        if (null == track) return false;
        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(track, _eventData.position, _eventData.pressEventCamera, out Vector2 local)) return false;

        Rect rect = track.rect;
        _along = true == bHorizontal ? local.x - rect.xMin : rect.yMax - local.y;
        return true;
    }

    private float GetThumbStart()
    {
        return true == bHorizontal ? thumb.anchoredPosition.x : -thumb.anchoredPosition.y;
    }

    private void RequestFrom(PointerEventData _eventData)
    {
        if (false == TryGetAlong(_eventData, out float along)) return;

        float free = GetLength(track) - GetLength(thumb);
        if (0.5f > free) return;

        ScrollRequestedEvent?.Invoke(Mathf.Clamp01((along - grabOffset) / free));
    }

    // // 입력

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (PointerEventData.InputButton.Left != _eventData.button) return;
        if (null == thumb || false == TryGetAlong(_eventData, out float along)) return;

        bHeld = true;
        SetThumbColor(ThumbPressedColor);

        // 막대 위를 누르면 잡은 자리를 유지하고, 홈의 빈 곳을 누르면 막대 가운데가 포인터에 오게 옮긴다.
        float start = GetThumbStart();
        float length = GetLength(thumb);
        grabOffset = along >= start && along <= start + length ? along - start : length * 0.5f;
        RequestFrom(_eventData);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        bHeld = false;
        SetThumbColor(ThumbNormalColor);
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        // 상위 ScrollRect(카드 띠 · 본문)가 이 끌기를 가져가지 않게 이 컴포넌트가 받는다.
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (false == bHeld || PointerEventData.InputButton.Left != _eventData.button) return;

        RequestFrom(_eventData);
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        bHeld = false;
        SetThumbColor(ThumbNormalColor);
    }

    private void OnDisable()
    {
        bHeld = false;
        SetThumbColor(ThumbNormalColor);
    }

    private void OnDestroy()
    {
        ScrollRequestedEvent = null;
    }
}
