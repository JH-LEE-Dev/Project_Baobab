using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 카드 도감의 칸 한 장(36x36). 판정(이 오브젝트, 움직이지 않는다) / 그림(visual, 정수 픽셀로 움직인다)으로 나뉜다.
///   앞면 = 칸 그림(CardSlot_Normal · CardSlot_Max · CardSlot_Prism · CardSlot_Ruby, 36x36, 카드 앞면과 같은 금 모서리 · 파인 홈 문법) + 아이콘(원래 크기, 정중앙 = 18 - 폭/2) + MAX 배지(SlotLevel_MAX) 또는 루비 배지(SlotLevel_Ruby, 오른쪽 아래 3px 안쪽).
///   뒷면 = Slot_Back(36x36 그림). 뒷면 칸은 착지한 순간 가운데 밝은 금 마름모(d ≤ 2)가 2프레임 흰색으로 반짝인다(backSparkImage).
///   NEW 배지(records/BadgeNew)는 칸 왼쪽 위에서 (+20, -2)에 붙는다.
/// 뒤집힘 폭(36 → … → 2 → … → 36)은 face의 가로 배율로 만든다(가운데 기준, 크기 변화 연출은 이것뿐이다).
/// E9 최초 완성 축하의 칸 반응(흰 실루엣 · 금빛 · 금 테두리)은 도감 창이 판 전체를 어둡게 한 위에 얹는 별도 층(flashImage · outlineImage)에 그린다.
/// 모든 값은 도감 창이 프레임(1/60초)마다 계산해 넣고, 이 클래스는 지난 값과 같으면 아무것도 하지 않는다(재그리기 방지).
/// 마우스 · 패드 선택은 이벤트로 도감 창에 알린다(호버는 마우스 모드에서만 반응 · 패드 모드에서는 기록만은 창이 정한다).
/// </summary>
public class UI_RoguelikeCollectionSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    public const int SlotSize = 36;
    private const int BadgeInset = 3;
    public const int NewBadgeX = 20;
    public const int NewBadgeY = -2;
    private const int IconCenter = 18;

    /// <summary>(칸, 마우스가 올라왔는가) 마우스가 올라왔거나(true) 패드 선택이 옮겨 왔다(false).</summary>
    public event Action<UI_RoguelikeCollectionSlot, bool> FocusedEvent;
    public event Action<UI_RoguelikeCollectionSlot> ExitedEvent;
    public event Action<UI_RoguelikeCollectionSlot> ClickedEvent;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Collection)
    [SerializeField] private RectTransform visual;
    [SerializeField] private CanvasGroup visualGroup;
    [SerializeField] private RectTransform face;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image levelImage;
    [SerializeField] private Image backSparkImage;
    [SerializeField] private Image newBadgeImage;
    [SerializeField, Tooltip("E9 흰 실루엣 · 금빛(판 위 별도 층에 있다).")] private Image flashImage;
    [SerializeField, Tooltip("E9 금 테두리 1px(판 위 별도 층에 있다).")] private Image outlineImage;
    [SerializeField] private Button button;
    [SerializeField] private Sprite normalFrameSprite;
    [SerializeField] private Sprite maxFrameSprite;
    [SerializeField] private Sprite prismFrameSprite;
    [SerializeField] private Sprite rubyFrameSprite;
    [SerializeField] private Sprite backFrameSprite;
    [SerializeField] private Sprite maxBadgeSprite;
    [SerializeField] private Sprite rubyBadgeSprite;

    //상태 변수
    private RoguelikeCollectionViewData data;
    private bool bFront = true;
    private bool bFaceApplied;
    private int offsetY = int.MinValue;
    private int flipWidth = SlotSize;
    private float alpha = -1f;
    private bool bNewBadge;
    private bool bBackSpark;
    private Color flashColor = Color.clear;
    private Color outlineColor = Color.clear;
    private Vector2 flashPosition = new Vector2(float.NaN, 0f);

    public RectTransform Rect => (RectTransform)transform;
    public RectTransform VisualRect => visual;
    public Selectable SelectButton => button;
    public RoguelikeCollectionViewData Data => data;
    public bool IsFront => bFront;

    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>한 번만 부른다. 클릭은 ClickedEvent로 올린다.</summary>
    public void Initialize()
    {
        if (null != button)
        {
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }

        if (null != flashImage) flashImage.enabled = false;
        if (null != outlineImage) outlineImage.enabled = false;
        if (null != backSparkImage) backSparkImage.enabled = false;
        if (null != newBadgeImage) newBadgeImage.enabled = false;
    }

    /// <summary>칸 데이터를 넣는다. 면(앞 / 뒤)은 SetFront가 정한다(NEW 연출은 뒷면으로 시작해 뒤집히며 앞면이 된다).</summary>
    public void SetData(in RoguelikeCollectionViewData _data, bool _bFront)
    {
        data = _data;
        bFront = _bFront;
        bFaceApplied = false;
        ApplyFace();
    }

    /// <summary>앞 / 뒤 면을 바꾼다(같으면 아무 일도 없다).</summary>
    public void SetFront(bool _bFront)
    {
        if (true == bFaceApplied && _bFront == bFront) return;

        bFront = _bFront;
        ApplyFace();
    }

    /// <summary>그림의 세로 위치(정수, 아래쪽 +). 판정은 움직이지 않는다.</summary>
    public void SetOffsetY(int _offsetY)
    {
        if (_offsetY == offsetY) return;

        offsetY = _offsetY;
        visual.anchoredPosition = new Vector2(0f, -_offsetY);
    }

    /// <summary>뒤집힘 폭(정수 픽셀, 36이 정상). 가운데 기준 가로 배율로 만든다.</summary>
    public void SetFlipWidth(int _width)
    {
        if (_width == flipWidth) return;

        flipWidth = _width;
        face.localScale = new Vector3(_width / (float)SlotSize, 1f, 1f);
    }

    public void SetAlpha(float _alpha)
    {
        if (_alpha == alpha) return;

        alpha = _alpha;
        visualGroup.alpha = _alpha;
    }

    public void SetNewBadge(bool _bShow)
    {
        if (_bShow == bNewBadge) return;

        bNewBadge = _bShow;
        if (null != newBadgeImage) newBadgeImage.enabled = _bShow;
    }

    /// <summary>뒷면 칸 착지 반짝(가운데 d ≤ 2 마름모가 흰색).</summary>
    public void SetBackSpark(bool _bShow)
    {
        if (_bShow == bBackSpark) return;

        bBackSpark = _bShow;
        if (null != backSparkImage) backSparkImage.enabled = _bShow;
    }

    /// <summary>
    /// E9 칸 반응. _flashColor = 칸 실루엣을 덮는 색(알파 0이면 끔), _outlineColor = 1px 테두리 색(알파 0이면 끔).
    /// _position = 칸 그림이 놓인 자리(도감 판 기준, 왼쪽 위, 아래쪽 +)로 도감 창이 매 프레임 넘긴다.
    /// </summary>
    public void SetFlash(Color _flashColor, Color _outlineColor, Vector2 _position)
    {
        if (null != flashImage)
        {
            if (_flashColor != flashColor)
            {
                flashColor = _flashColor;
                flashImage.color = _flashColor;
                flashImage.enabled = 0f < _flashColor.a;
            }
        }

        if (null != outlineImage)
        {
            if (_outlineColor != outlineColor)
            {
                outlineColor = _outlineColor;
                outlineImage.color = _outlineColor;
                outlineImage.enabled = 0f < _outlineColor.a;
            }
        }

        if (_position != flashPosition && (0f < flashColor.a || 0f < outlineColor.a))
        {
            flashPosition = _position;
            if (null != flashImage) flashImage.rectTransform.anchoredPosition = new Vector2(_position.x, -_position.y);
            if (null != outlineImage) outlineImage.rectTransform.anchoredPosition = new Vector2(_position.x, -_position.y);
        }
    }

    /// <summary>창을 다시 열 때 모든 연출 값을 처음으로 되돌린다.</summary>
    public void ResetVisual()
    {
        offsetY = int.MinValue;
        alpha = -1f;
        flipWidth = -1;
        SetFlipWidth(SlotSize);
        SetOffsetY(0);
        SetAlpha(1f);
        SetNewBadge(false);
        SetBackSpark(false);
        SetFlash(Color.clear, Color.clear, Vector2.zero);
    }

    // 일반 비즈니스 로직 및 내부 메서드

    private void ApplyFace()
    {
        bFaceApplied = true;
        bool bShowFront = true == bFront && true == data.bFound;

        if (null != frameImage)
        {
            if (false == bShowFront)
            {
                frameImage.sprite = backFrameSprite;
                frameImage.type = Image.Type.Simple;
            }
            else
            {
                Sprite frame = normalFrameSprite;
                if (true == data.bMaxed) frame = maxFrameSprite;
                else if (ECardKind.Prism == data.kind) frame = prismFrameSprite;
                else if (ECardKind.Ruby == data.kind) frame = rubyFrameSprite;

                frameImage.sprite = frame;
                frameImage.type = Image.Type.Sliced;
            }
        }

        if (null != iconImage)
        {
            Sprite icon = true == bShowFront ? data.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = null != icon;
            if (null != icon)
            {
                Vector2 size = icon.rect.size;
                RectTransform rect = iconImage.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2(IconCenter - ((int)size.x >> 1), -(IconCenter - ((int)size.y >> 1)));
            }
        }

        if (null != levelImage)
        {
            Sprite badge = null;
            if (true == bShowFront)
            {
                if (true == data.bMaxed) badge = maxBadgeSprite;
                else if (ECardKind.Ruby == data.kind) badge = rubyBadgeSprite;
            }

            levelImage.sprite = badge;
            levelImage.enabled = null != badge;
            if (null != badge)
            {
                RectTransform rect = levelImage.rectTransform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = badge.rect.size;
                rect.anchoredPosition = new Vector2(-BadgeInset, BadgeInset);
            }
        }
    }

    private void OnClicked()
    {
        ClickedEvent?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        FocusedEvent?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        ExitedEvent?.Invoke(this);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        FocusedEvent?.Invoke(this, false);
    }

    // 유니티 라이프사이클 이벤트 함수

    private void OnDestroy()
    {
        FocusedEvent = null;
        ExitedEvent = null;
        ClickedEvent = null;
        if (null != button) button.onClick.RemoveListener(OnClicked);
    }
}
