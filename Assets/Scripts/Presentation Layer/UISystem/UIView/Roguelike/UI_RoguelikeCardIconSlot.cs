using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 · 기록 창의 카드 한 칸: CardSlot_* 카드 칸 그림(36x36, 카드 앞면과 같은 금 모서리 · 파인 홈 문법) 안에 카드 아이콘(스프라이트 원본 크기, 가운데)과 오른쪽 아래 레벨 배지 그림.
/// 아이콘은 카드 화면(UI_RoguelikeCard.PlaceCentered)과 같은 규칙으로 1배 크기에 정수 픽셀로 놓는다(홀수 여백은 내림).
/// 레벨은 픽셀 배지 그림(SlotLevel_*)이다 - 글자가 아니라서 언어별 폰트와 무관하고 칸을 벗어나지 않는다.
///   액티브 I~VI · 공통 패시브 I~IV, 마지막 레벨은 MAX, 계약(루비)은 ±, 프리즘은 레벨을 숨긴다(카드 화면과 같다).
/// 칸 테두리 색은 카드 종류로 정한다: 일반(프리팹의 기본 그림) · 프리즘(보라) · 계약(빨강).
/// 크기는 바꾸지 않는다(픽셀 UI 규칙).
/// 기록 창의 카드 띠 칸만 선택 대상(selectable)이 있다 - 패드로 고르면 SelectedEvent를 올리고, 위 / 아래를 누르면 MovedEvent를 올린다(좌 / 우는 올리지 않고 Selectable의 Explicit 탐색이 처리한다).
/// 결과 화면 · 기록 줄의 칸은 selectable이 없어 선택되지 않으므로 두 이벤트가 오지 않는다.
/// </summary>
public class UI_RoguelikeCardIconSlot : MonoBehaviour, ISelectHandler, IMoveHandler
{
    public const float SlotSize = 36f;
    // 레벨 배지: 칸 오른쪽 아래에서 안쪽으로 3px.
    private const float BadgeInset = 3f;

    public event Action<UI_RoguelikeCardIconSlot> SelectedEvent;
    public event Action<UI_RoguelikeCardIconSlot, MoveDirection> MovedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image levelImage;
    [SerializeField, Tooltip("레벨 1~6 배지(I~VI).")] private Sprite[] levelSprites = new Sprite[6];
    [SerializeField] private Sprite maxLevelSprite;
    [SerializeField] private Sprite rubyLevelSprite;
    [SerializeField] private Sprite prismFrameSprite;
    [SerializeField] private Sprite rubyFrameSprite;
    [SerializeField, Tooltip("카드 띠 칸만 연결한다(없으면 선택 대상이 아니다).")] private Selectable selectable;

    //상태 변수
    private Sprite normalFrameSprite;
    private bool bFrameCaptured;

    public RectTransform Rect => (RectTransform)transform;
    public Selectable SelectTarget => selectable;

    public void SetCard(in RoguelikeOwnedCardViewData _card)
    {
        ApplyFrame(in _card);
        ApplyIcon(_card.icon);
        ApplyLevel(in _card);
    }

    private void ApplyFrame(in RoguelikeOwnedCardViewData _card)
    {
        if (null == frameImage) return;

        if (false == bFrameCaptured)
        {
            normalFrameSprite = frameImage.sprite;
            bFrameCaptured = true;
        }

        Sprite frame = normalFrameSprite;
        if (ECardKind.Prism == _card.kind && null != prismFrameSprite) frame = prismFrameSprite;
        else if ((ECardKind.Ruby == _card.kind || true == _card.bRuby) && null != rubyFrameSprite) frame = rubyFrameSprite;

        frameImage.sprite = frame;
    }

    private void ApplyIcon(Sprite _icon)
    {
        if (null == iconImage) return;

        iconImage.sprite = _icon;
        iconImage.enabled = null != _icon;
        if (null == _icon) return;

        Vector2 size = _icon.rect.size;
        PlaceTopLeft(iconImage.rectTransform, Mathf.Floor((SlotSize - size.x) * 0.5f), Mathf.Floor((SlotSize - size.y) * 0.5f), size);
    }

    private void ApplyLevel(in RoguelikeOwnedCardViewData _card)
    {
        if (null == levelImage) return;

        Sprite badge = FindLevelSprite(in _card);
        levelImage.sprite = badge;
        levelImage.enabled = null != badge;
        if (null == badge) return;

        RectTransform rect = levelImage.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = badge.rect.size;
        rect.anchoredPosition = new Vector2(-BadgeInset, BadgeInset);
    }

    private Sprite FindLevelSprite(in RoguelikeOwnedCardViewData _card)
    {
        if (ECardKind.Prism == _card.kind) return null;
        if (ECardKind.Ruby == _card.kind || true == _card.bRuby) return rubyLevelSprite;
        if (0 >= _card.level) return null;
        if (0 < _card.maxLevel && _card.level >= _card.maxLevel) return maxLevelSprite;
        if (levelSprites.Length >= _card.level) return levelSprites[_card.level - 1];
        return null;
    }

    private static void PlaceTopLeft(RectTransform _rect, float _x, float _y, Vector2 _size)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.sizeDelta = _size;
        _rect.anchoredPosition = new Vector2(_x, -_y);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        SelectedEvent?.Invoke(this);
    }

    /// <summary>
    /// 선택된 칸에서 방향 입력이 왔다. 위 / 아래만 알린다(Explicit 탐색의 위 / 아래 링크는 비어 있어 선택은 움직이지 않는다).
    /// 같은 오브젝트의 Selectable.OnMove도 함께 불리므로 좌 / 우는 여기서 처리하지 않고 기존 탐색에 맡긴다.
    /// </summary>
    public void OnMove(AxisEventData _eventData)
    {
        if (MoveDirection.Up != _eventData.moveDir && MoveDirection.Down != _eventData.moveDir) return;

        MovedEvent?.Invoke(this, _eventData.moveDir);
    }

    private void OnDestroy()
    {
        SelectedEvent = null;
        MovedEvent = null;
    }
}
