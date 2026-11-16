using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 기록 창의 한 줄: Panel_Info 9-슬라이스 바탕 위에 순위 · 도달(WAVE n / Unlimit-n) · 플레이 시간 · 카드 칸(앞에서부터 최대 SlotCount장, 나머지는 +n - 시간 열과 겹치지 않는 3장).
/// 숫자와 WAVE · Unlimit 표기는 언어와 무관한 고정 문자열이다(글꼴은 언어별 폰트 교체를 따른다). 순위가 없는 최근 기록은 순위 칸에 '-'를 둔다.
///
/// 줄 전체가 하나의 선택 대상(Button, 탐색은 Explicit - 화면이 위아래로 잇는다)이다. 클릭 · 패드 A는 ClickedEvent로 줄을 알린다(전체 카드 띠 열기 · 닫기는 화면이 맡는다).
/// 카드 띠가 열린 줄은 바탕 그림을 금색 테두리(openSprite)로 바꾸고 닫히면 원래 그림으로 되돌린다(SetOpenBackground).
/// 마우스 호버와 패드 선택은 FocusedEvent(줄, 마우스인지)로 알리고, 마우스가 나가면 ExitedEvent로 알린다. 칸(UI_RoguelikeCardIconSlot)은 선택 대상이 아니다.
/// </summary>
public class UI_RoguelikeRecordRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    public const int SlotCount = 3;

    public event Action<UI_RoguelikeRecordRow> ClickedEvent;
    public event Action<UI_RoguelikeRecordRow, bool> FocusedEvent;
    public event Action<UI_RoguelikeRecordRow> ExitedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text moreText;
    [SerializeField] private UI_RoguelikeCardIconSlot[] slots = new UI_RoguelikeCardIconSlot[SlotCount];
    [SerializeField] private Button button;
    [SerializeField, Tooltip("줄 바탕 Image(Panel_Info 9-슬라이스).")] private Image background;
    [SerializeField, Tooltip("카드 띠가 열린 줄의 바탕 그림(Panel_Info_Gold, 가운데는 채워 그린다).")] private Sprite openSprite;

    //내부 의존성
    private readonly StringBuilder builder = new StringBuilder(24);
    private UnityEngine.Events.UnityAction clickHandler;

    //상태 변수
    private List<RoguelikeOwnedCardViewData> cards;
    private Sprite normalSprite;

    public RectTransform Rect => (RectTransform)transform;
    public Button SelectButton => button;
    /// <summary>이 줄 기록의 카드 전체(저장된 순서). 읽기만 한다.</summary>
    public IReadOnlyList<RoguelikeOwnedCardViewData> Cards => cards;
    public int CardCount => null != cards ? cards.Count : 0;

    /// <summary>클릭 · 제출 콜백을 연결한다. 줄을 만든 직후 한 번 부른다.</summary>
    public void Initialize()
    {
        if (null != background && null == normalSprite) normalSprite = background.sprite;
        if (null == button) return;

        if (null == clickHandler) clickHandler = OnClicked;
        button.onClick.RemoveListener(clickHandler);
        button.onClick.AddListener(clickHandler);
    }

    /// <summary>카드 띠가 열린 줄이면 바탕을 금색 테두리 그림으로, 아니면 원래 그림으로 바꾼다.</summary>
    public void SetOpenBackground(bool _bOpen)
    {
        if (null == background) return;
        if (null == normalSprite) normalSprite = background.sprite;

        Sprite target = true == _bOpen && null != openSprite ? openSprite : normalSprite;
        if (background.sprite != target) background.sprite = target;
    }

    /// <summary>_rank가 0 이하면 순위 칸을 비운다(최근 기록).</summary>
    public void SetRecord(int _rank, in RoguelikeRecordViewData _record)
    {
        cards = _record.cards;

        builder.Clear();
        if (0 < _rank) builder.Append(_rank);
        else builder.Append('-');
        if (null != rankText) rankText.text = builder.ToString();

        builder.Clear();
        if (0 < _record.unlimitWave) builder.Append("Unlimit-").Append(_record.unlimitWave);
        else builder.Append("WAVE ").Append(_record.reachedWave);
        if (null != waveText) waveText.text = builder.ToString();

        if (null != timeText) timeText.text = FormatTime(_record.playTimeSeconds);

        int cardCount = CardCount;
        for (int i = 0; slots.Length > i; i++)
        {
            if (null == slots[i]) continue;

            bool bUsed = i < cardCount;
            slots[i].gameObject.SetActive(bUsed);
            if (true == bUsed) slots[i].SetCard(_record.cards[i]);
        }

        int rest = cardCount - slots.Length;
        if (null != moreText)
        {
            moreText.gameObject.SetActive(0 < rest);
            if (0 < rest)
            {
                builder.Clear();
                builder.Append('+').Append(rest);
                moreText.text = builder.ToString();
            }
        }
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

    private void OnClicked()
    {
        ClickedEvent?.Invoke(this);
    }

    private string FormatTime(float _seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(_seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int seconds = total % 60;

        builder.Clear();
        if (0 < hours) builder.Append(hours).Append(':');
        if (10 > minutes) builder.Append('0');
        builder.Append(minutes).Append(':');
        if (10 > seconds) builder.Append('0');
        builder.Append(seconds);
        return builder.ToString();
    }

    private void OnDestroy()
    {
        if (null != button && null != clickHandler) button.onClick.RemoveListener(clickHandler);
        ClickedEvent = null;
        FocusedEvent = null;
        ExitedEvent = null;
    }
}
