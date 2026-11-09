using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 기록 창의 한 줄: Panel_Info 9-슬라이스 바탕 위에 순위 · 도달(WAVE n / Unlimit-n) · 플레이 시간 · 카드 칸(앞에서부터 최대 SlotCount장, 나머지는 +n - 시간 열과 겹치지 않는 3장).
/// 숫자와 WAVE · Unlimit 표기는 언어와 무관한 고정 문자열이다(글꼴은 언어별 폰트 교체를 따른다). 순위가 없는 최근 기록은 순위 칸에 '-'를 둔다.
/// </summary>
public class UI_RoguelikeRecordRow : MonoBehaviour
{
    public const int SlotCount = 3;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text moreText;
    [SerializeField] private UI_RoguelikeCardIconSlot[] slots = new UI_RoguelikeCardIconSlot[SlotCount];

    //내부 의존성
    private readonly StringBuilder builder = new StringBuilder(24);

    public RectTransform Rect => (RectTransform)transform;

    /// <summary>_rank가 0 이하면 순위 칸을 비운다(최근 기록).</summary>
    public void SetRecord(int _rank, in RoguelikeRecordViewData _record, string _maxLabel)
    {
        builder.Clear();
        if (0 < _rank) builder.Append(_rank);
        else builder.Append('-');
        if (null != rankText) rankText.text = builder.ToString();

        builder.Clear();
        if (0 < _record.unlimitWave) builder.Append("Unlimit-").Append(_record.unlimitWave);
        else builder.Append("WAVE ").Append(_record.reachedWave);
        if (null != waveText) waveText.text = builder.ToString();

        if (null != timeText) timeText.text = FormatTime(_record.playTimeSeconds);

        int cardCount = null != _record.cards ? _record.cards.Count : 0;
        for (int i = 0; slots.Length > i; i++)
        {
            if (null == slots[i]) continue;

            bool bUsed = i < cardCount;
            slots[i].gameObject.SetActive(bUsed);
            if (true == bUsed) slots[i].SetCard(_record.cards[i], _maxLabel);
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
}
