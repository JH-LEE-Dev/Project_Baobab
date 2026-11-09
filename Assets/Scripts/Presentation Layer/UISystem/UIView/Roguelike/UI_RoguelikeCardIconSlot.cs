using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 · 기록 창의 카드 한 칸: Panel_Info 9-슬라이스 칸(36x36) 안에 카드 아이콘(32x32, 원본 크기)과 오른쪽 아래 레벨 표기.
/// 레벨 표기는 카드 선택 화면과 같은 규칙이다: 액티브 I~VI · 공통 패시브 I~IV, 마지막 레벨은 MAX, 계약(루비)은 ±.
/// 글자는 1px 어두운 그림자를 깔아 아이콘 위에서도 읽히게 한다. 크기는 바꾸지 않는다(픽셀 UI 규칙).
/// </summary>
public class UI_RoguelikeCardIconSlot : MonoBehaviour
{
    public const float SlotSize = 36f;

    private static readonly string[] LevelLabels = { string.Empty, "I", "II", "III", "IV", "V", "VI" };
    private const string RubyLabel = "±";

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text levelShadowText;

    public RectTransform Rect => (RectTransform)transform;

    /// <summary>_maxLabel은 현지화된 MAX 표기(비어 있으면 "MAX").</summary>
    public void SetCard(in RoguelikeOwnedCardViewData _card, string _maxLabel)
    {
        if (null != iconImage)
        {
            iconImage.sprite = _card.icon;
            iconImage.enabled = null != _card.icon;
        }

        string label = BuildLevelLabel(in _card, _maxLabel);
        if (null != levelText) levelText.text = label;
        if (null != levelShadowText) levelShadowText.text = label;
    }

    private static string BuildLevelLabel(in RoguelikeOwnedCardViewData _card, string _maxLabel)
    {
        if (true == _card.bRuby) return RubyLabel;
        if (0 < _card.maxLevel && _card.level >= _card.maxLevel) return string.IsNullOrEmpty(_maxLabel) ? "MAX" : _maxLabel;
        if (0 < _card.level && LevelLabels.Length > _card.level) return LevelLabels[_card.level];
        return string.Empty;
    }
}
