using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 기록 창(나이테 기록판)의 순위 한 줄(340x24, Panel_Info 9-슬라이스). 계획서 RecordUI_Implementation_Plan.md 11-2.
///   안쪽 여백 12, 묶음 사이 11: [메달 19(순위를 새긴 그루터기)] [결과 깃발 10 + 기준값] [작은 웨이브 길 30칸(4x5)] [카드 아이콘 + 장수] [나무 / 시계 아이콘 + 값]
///   기준값: 클리어 랭킹 = 클리어 시간(못 깼으면 W + 도달 웨이브), 언리밋 랭킹 = ∞ + 단계. 장수와 마지막 값은 오른쪽 정렬(마지막 값은 끝선 328).
///   숫자는 모두 작은 숫자 그림 글꼴(UI_RoguelikeSmallNumber)이라 언어와 무관하게 폭이 같다.
///   언리밋 탭의 작은 길은 1위 단계 대비 비율(민트)이다. 이번 판(최근 기록과 같은 판)이면 NEW 딱지를 붙인다.
///   기록이 없는 자리는 빈칸 모양(어두운 판 · 빈 메달 · 흐린 길)으로 두고 고를 수 없다.
///
/// 줄 전체가 하나의 선택 대상(Button, 탐색은 Explicit - 화면이 위아래로 잇는다)이다. 클릭 · 패드 A는 ClickedEvent, 마우스 호버와 패드 선택은 FocusedEvent(줄, 마우스인지), 마우스가 나가면 ExitedEvent.
/// 선택된 줄은 바탕을 금색 테두리(openSprite)로 바꾼다(SetOpenBackground). 위치 · 크기는 정수 픽셀이고 등장 연출도 정수 픽셀로만 움직인다.
/// </summary>
public class UI_RoguelikeRecordRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    public const int MedalCount = 5;
    // 등장: 16px 오른쪽에서 0.24초(OutCubic), 도착 뒤 0.18초부터 작은 길 0.4초 점등, NEW 딱지는 0.35초에 톡(3px 위에서 0.08초), 이후 0.5초마다 1px 까딱.
    private const float EnterSeconds = 0.24f;
    private const float EnterDistance = 16f;
    private const float TrailDelay = 0.18f;
    private const float TrailSeconds = 0.4f;
    private const float NewDelay = 0.35f;
    private const float NewPopSeconds = 0.08f;
    private const float NewPopHeight = 3f;
    private const float NewBobSeconds = 0.5f;
    private const float SnapSeconds = 99f;
    // 줄(24) 안 세로 가운데. 아이콘 위쪽 = 12 - 올림(높이 / 2): 높이 10 · 9 -> 7, 높이 8 -> 8
    private const float RowCenter = 12f;

    public event Action<UI_RoguelikeRecordRow> ClickedEvent;
    public event Action<UI_RoguelikeRecordRow, bool> FocusedEvent;
    public event Action<UI_RoguelikeRecordRow> ExitedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Button button;
    [SerializeField, Tooltip("줄 바탕 Image(Panel_Info 9-슬라이스).")] private Image background;
    [SerializeField, Tooltip("선택된 줄의 바탕 그림(Panel_Info_Gold).")] private Sprite openSprite;
    [SerializeField, Tooltip("기록이 없는 자리의 바탕 그림(Panel_Info_Empty).")] private Sprite emptySprite;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image medalImage;
    [SerializeField, Tooltip("순위 1~5를 새긴 19px 메달.")] private Sprite[] medalSprites = new Sprite[MedalCount];
    [SerializeField] private Sprite medalEmptySprite;
    [SerializeField] private Image keyIconImage;
    [SerializeField] private Sprite flagClearSprite;
    [SerializeField] private Sprite flagOverSprite;
    [SerializeField] private Sprite flagRetireSprite;
    [SerializeField] private Sprite unlimitSprite;
    [SerializeField, Tooltip("기준값(클리어 시간 · W + 웨이브 · 언리밋 단계). 왼쪽 정렬.")] private UI_RoguelikeSmallNumber keyNumber;
    [SerializeField] private UI_RoguelikeWaveTrail trail;
    [SerializeField] private GameObject cardIcon;
    [SerializeField, Tooltip("카드 장수. 오른쪽 정렬.")] private UI_RoguelikeSmallNumber cardCountNumber;
    [SerializeField] private Image rightIconImage;
    [SerializeField] private Sprite treeSprite;
    [SerializeField] private Sprite clockSprite;
    [SerializeField, Tooltip("나무 수 · 언리밋 탭은 총 시간. 끝선 328에 오른쪽 정렬.")] private UI_RoguelikeSmallNumber rightNumber;
    [SerializeField] private RectTransform newBadge;

    //내부 의존성
    private UnityEngine.Events.UnityAction clickHandler;

    //상태 변수
    private List<RoguelikeOwnedCardViewData> cards;
    private Sprite normalSprite;
    private bool bHasRecord;
    private bool bNew;
    private bool bNewPopped;
    private bool bMintTrail;
    private bool bSelected;
    private int trailLit;
    private UI_RoguelikeWaveTrail.EEnd trailEnd;
    private Vector2 newBadgeBase;
    private bool bNewBadgeBaseCaptured;

    public RectTransform Rect => (RectTransform)transform;
    public Button SelectButton => button;
    public bool HasRecord => bHasRecord;
    /// <summary>이 줄 기록의 카드 전체(저장된 순서). 읽기만 한다.</summary>
    public IReadOnlyList<RoguelikeOwnedCardViewData> Cards => cards;
    public int CardCount => null != cards ? cards.Count : 0;

    /// <summary>클릭 · 제출 콜백을 연결한다. 줄을 만든 직후 한 번 부른다.</summary>
    public void Initialize()
    {
        if (null != background && null == normalSprite) normalSprite = background.sprite;
        CaptureNewBadgeBase();
        if (null == button) return;

        if (null == clickHandler) clickHandler = OnClicked;
        button.onClick.RemoveListener(clickHandler);
        button.onClick.AddListener(clickHandler);
    }

    /// <summary>선택된 줄이면 바탕을 금색 테두리 그림으로, 아니면 원래 그림으로 바꾼다(빈 자리는 빈칸 그림 유지).</summary>
    public void SetOpenBackground(bool _bOpen)
    {
        if (null == background) return;
        if (null == normalSprite) normalSprite = background.sprite;

        bSelected = _bOpen && bHasRecord;
        Sprite target = false == bHasRecord ? (null != emptySprite ? emptySprite : normalSprite) : (true == bSelected && null != openSprite ? openSprite : normalSprite);
        if (background.sprite != target) background.sprite = target;
    }

    /// <summary>
    /// 기록 한 줄을 채운다. _rank는 0부터, _tab은 1 = 클리어 랭킹 · 2 = 언리밋 랭킹, _bNew는 최근 기록과 같은 판인지,
    /// _unlimitBest는 언리밋 탭 1위의 단계(작은 길 비율의 기준).
    /// </summary>
    public void SetRecord(int _rank, in RoguelikeRecordViewData _record, int _tab, bool _bNew, int _unlimitBest)
    {
        cards = _record.cards;
        bHasRecord = true;
        bNew = _bNew;
        bNewPopped = false;
        if (null != button) button.interactable = true;

        SetMedal(0 <= _rank && medalSprites.Length > _rank ? medalSprites[_rank] : medalEmptySprite);
        SetActive(keyIconImage, true);
        SetActive(keyNumber, true);
        SetActive(cardIcon, true);
        SetActive(cardCountNumber, true);
        SetActive(rightIconImage, true);
        SetActive(rightNumber, true);

        bool bUnlimitTab = 2 == _tab;
        bMintTrail = bUnlimitTab;
        if (true == bUnlimitTab)
        {
            SetIcon(keyIconImage, unlimitSprite);
            if (null != keyNumber) keyNumber.SetInteger(_record.unlimitWave, UI_RoguelikeSmallNumber.EPalette.Mint);
            int best = 0 < _unlimitBest ? _unlimitBest : 1;
            trailLit = Mathf.Clamp(Mathf.RoundToInt((float)_record.unlimitWave / best * UI_RoguelikeWaveTrail.PipCount), 1, UI_RoguelikeWaveTrail.PipCount);
            trailEnd = UI_RoguelikeWaveTrail.EEnd.Clear;
            SetIcon(rightIconImage, clockSprite);
            if (null != rightNumber) rightNumber.SetTime(Mathf.FloorToInt(_record.playTimeSeconds), UI_RoguelikeSmallNumber.EPalette.Muted);
        }
        else
        {
            trailEnd = true == _record.bCleared ? UI_RoguelikeWaveTrail.EEnd.Clear : (true == _record.bAbandoned ? UI_RoguelikeWaveTrail.EEnd.Retire : UI_RoguelikeWaveTrail.EEnd.Over);
            SetIcon(keyIconImage, UI_RoguelikeWaveTrail.EEnd.Clear == trailEnd ? flagClearSprite : (UI_RoguelikeWaveTrail.EEnd.Retire == trailEnd ? flagRetireSprite : flagOverSprite));
            if (null != keyNumber)
            {
                if (true == _record.bCleared) keyNumber.SetTime(Mathf.FloorToInt(_record.rankingTimeSeconds), UI_RoguelikeSmallNumber.EPalette.Gold);
                else keyNumber.SetPrefixed(UI_RoguelikeSmallNumber.CodeW, _record.reachedWave, UI_RoguelikeWaveTrail.EEnd.Retire == trailEnd ? UI_RoguelikeSmallNumber.EPalette.Cream : UI_RoguelikeSmallNumber.EPalette.Red);
            }
            trailLit = true == _record.bCleared ? UI_RoguelikeWaveTrail.PipCount : Mathf.Clamp(_record.reachedWave, 0, UI_RoguelikeWaveTrail.PipCount);
            SetIcon(rightIconImage, treeSprite);
            if (null != rightNumber) rightNumber.SetThousands(_record.treesFelled, UI_RoguelikeSmallNumber.EPalette.Muted);
        }

        if (null != cardCountNumber) cardCountNumber.SetInteger(CardCount, UI_RoguelikeSmallNumber.EPalette.Muted);
        if (null != trail) trail.SetGhost(false);
        if (null != newBadge) newBadge.gameObject.SetActive(false);
        SetOpenBackground(bSelected);
    }

    /// <summary>기록이 없는 자리(_rank는 0부터): 빈 메달 · 흐린 길만 보이고 고를 수 없다.</summary>
    public void SetEmpty(int _rank)
    {
        cards = null;
        bHasRecord = false;
        bNew = false;
        bSelected = false;
        trailLit = 0;
        trailEnd = UI_RoguelikeWaveTrail.EEnd.None;
        if (null != button) button.interactable = false;

        SetMedal(medalEmptySprite);
        SetActive(keyIconImage, false);
        SetActive(keyNumber, false);
        SetActive(cardIcon, false);
        SetActive(cardCountNumber, false);
        SetActive(rightIconImage, false);
        SetActive(rightNumber, false);
        if (null != newBadge) newBadge.gameObject.SetActive(false);
        if (null != trail)
        {
            trail.SetGhost(true);
            trail.Render(0, UI_RoguelikeWaveTrail.EEnd.None, false, -1, false);
        }
        SetOpenBackground(false);
    }

    /// <summary>
    /// 등장 연출 한 프레임. _localTime은 이 줄의 등장 시작부터 지난 시간(unscaled, 음수면 아직), _now는 까딱임용 공용 시계.
    /// 줄은 오른쪽에서 정수 픽셀로 들어오고, 도착하면 작은 길이 켜지고, NEW 딱지가 톡 튀어나온다.
    /// </summary>
    public void RenderIntro(float _localTime, float _now)
    {
        float enter = Mathf.Clamp01(_localTime / EnterSeconds);
        float eased = 1f - (1f - enter) * (1f - enter) * (1f - enter);
        if (null != group) group.alpha = eased;
        Vector2 position = Rect.anchoredPosition;
        position.x = Mathf.Round((1f - eased) * EnterDistance);
        Rect.anchoredPosition = position;

        if (null != trail && true == bHasRecord)
        {
            float fill = Mathf.Clamp01((_localTime - TrailDelay) / TrailSeconds);
            int shown = Mathf.FloorToInt(trailLit * fill);
            trail.Render(shown, 1f <= fill ? trailEnd : UI_RoguelikeWaveTrail.EEnd.Clear, bMintTrail, -1, false);
        }

        if (null == newBadge || false == bNew) return;

        float pop = (_localTime - NewDelay) / NewPopSeconds;
        bool bVisible = 0f < pop;
        if (newBadge.gameObject.activeSelf != bVisible) newBadge.gameObject.SetActive(bVisible);
        if (false == bVisible) return;

        if (false == bNewPopped)
        {
            bNewPopped = true;
            Sound.PlayUI(SoundID.FontPop);
        }

        float lift = 1f > pop ? NewPopHeight : 0f;
        float bob = 1 == Mathf.FloorToInt(_now / NewBobSeconds) % 2 ? 1f : 0f;
        newBadge.anchoredPosition = new Vector2(newBadgeBase.x, newBadgeBase.y + lift - bob);
    }

    /// <summary>등장 연출 없이 끝 상태로 둔다(언어 변경 · QA 툴). NEW 소리는 내지 않는다.</summary>
    public void SnapIntro(float _now)
    {
        bNewPopped = true;
        RenderIntro(SnapSeconds, _now);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (false == bHasRecord) return;
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

    private void OnClicked()
    {
        if (false == bHasRecord) return;
        ClickedEvent?.Invoke(this);
    }

    private void CaptureNewBadgeBase()
    {
        if (true == bNewBadgeBaseCaptured || null == newBadge) return;

        newBadgeBase = newBadge.anchoredPosition;
        bNewBadgeBaseCaptured = true;
    }

    private void SetMedal(Sprite _sprite)
    {
        if (null != medalImage && medalImage.sprite != _sprite) medalImage.sprite = _sprite;
    }

    /// <summary>아이콘(깃발 10x10 · 무한 10x8 · 나무 8x8 · 시계 9x9)을 원본 크기로 바꾸고 줄 세로 가운데에 맞춘다. 가로 위치는 그대로.</summary>
    private static void SetIcon(Image _image, Sprite _sprite)
    {
        if (null == _image || null == _sprite) return;

        _image.sprite = _sprite;
        RectTransform rect = _image.rectTransform;
        Vector2 size = _sprite.rect.size;
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(RowCenter - Mathf.Ceil(size.y * 0.5f)));
    }

    private static void SetActive(Component _component, bool _bActive)
    {
        if (null != _component && _component.gameObject.activeSelf != _bActive) _component.gameObject.SetActive(_bActive);
    }

    private static void SetActive(GameObject _object, bool _bActive)
    {
        if (null != _object && _object.activeSelf != _bActive) _object.SetActive(_bActive);
    }

    private void OnDestroy()
    {
        if (null != button && null != clickHandler) button.onClick.RemoveListener(clickHandler);
        ClickedEvent = null;
        FocusedEvent = null;
        ExitedEvent = null;
    }
}
