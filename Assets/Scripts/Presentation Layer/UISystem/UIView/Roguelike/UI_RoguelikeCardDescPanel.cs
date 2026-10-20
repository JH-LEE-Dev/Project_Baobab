using TMPro;
using UnityEngine;

/// <summary>
/// 카드 선택 화면의 설명 패널. 기획서 v2.3 "카드 아래에 넓은 박스 하나. 커서가 올라간 카드의 이름(첫 줄) + 레벨 + 효과".
/// 카드뿐 아니라 건너뛰기·리롤 아이콘 버튼의 이름·설명도 여기서 보여 준다(SetInfo) - 버튼에는 글자가 없다.
///
/// 지금 카드 데이터에는 이름·기본 설명 키만 있다. 레벨별 변화 문구·MAX 효과 문구·루비 디메리트 문구는 키가 없어
/// descriptionKey 한 줄만 보여 준다(UI_WorkList 3-2 - 필드가 생기면 여기에 줄을 더한다).
/// 문구는 LocalizationManager로 읽는다(카드 이름·설명은 문자열 키, MAX는 LocKeys). 데이터가 아직 없어 비어 있으면 카드 키 문자열을 그대로 보여 준다.
///
/// [문구를 자르지 않는다] 이 패널은 카드의 뜻을 확인하는 곳이라 `...`로 줄이지 않는다. 글자가 길면 패널이 넓어지고, 그래도 모자라면 높이가 자란다.
///
/// [줄바꿈은 문구가 정한다] 번역문 안의 `\n`(수동 줄바꿈)을 그대로 따른다. 줄을 어디서 나눠야 자연스러운지는 그 언어를 아는 번역가가 정하는 것이 프로젝트 방침이다
/// (영문 용어집: 긴 설명은 문장 안에서 `\n`으로 직접 나눈다). 패널은 그 줄들 중 가장 긴 줄이 들어가는 폭으로 맞추고, 수동 줄바꿈이 없거나 한 줄이 최대 폭보다 길 때만
/// TMP가 공백에서 자동으로 줄바꿈한다. 자동 줄바꿈은 같은 줄 수를 유지하는 가장 좁은 폭으로 줄여 마지막 줄에 한두 단어만 남는 어색함을 줄인다.
///
/// [화면 전체 폭 통일] 호버를 옮길 때 박스 크기가 달라지면 어지러우므로, 화면이 열릴 때 보일 수 있는 모든 내용을 재 보고(Measure*) 가장 넓은 폭을 SetFixedWidth로 고정한다.
/// </summary>
public class UI_RoguelikeCardDescPanel : MonoBehaviour
{
    private static readonly string[] LevelLabels = { string.Empty, "I", "II", "III", "IV", string.Empty };
    private const string RubyLabel = "±";
    private const int MaxLevel = 5;

    public const float MinPanelWidth = 190f;
    public const float MinPanelHeight = 36f;
    private const float DefaultMaxPanelWidth = 320f;
    private const float PaddingX = 6f;
    private const float PaddingY = 4f;
    private const float RowGap = 2f;
    private const float LevelGap = 6f;
    private const float MinLineHeight = 12f;
    private const float Unbounded = 10000f;
    private const float BalanceMinRatio = 0.6f;
    private const int BalanceSteps = 5;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text descriptionText;

    //상태 변수
    private LocalizationManager localizationManager;
    private float maxPanelWidth = DefaultMaxPanelWidth;
    private float fixedWidth;

    public void Initialize(LocalizationManager _localizationManager)
    {
        localizationManager = _localizationManager;
    }

    /// <summary>패널이 넓어질 수 있는 한계 폭. 화면이 좁으면 화면 폭 안으로 제한한다.</summary>
    public void SetMaxWidth(float _width)
    {
        maxPanelWidth = Mathf.Clamp(_width, MinPanelWidth, DefaultMaxPanelWidth);
    }

    /// <summary>0보다 크면 내용과 관계없이 이 폭으로 그린다. 0이면 내용에 맞춘 폭.</summary>
    public void SetFixedWidth(float _width)
    {
        fixedWidth = _width;
    }

    // // 표시

    public void SetCard(in RoguelikeCardViewData _data)
    {
        ApplyCardTexts(in _data);
        Show();
    }

    /// <summary>카드가 아닌 항목(건너뛰기·리롤 버튼)의 이름과 설명.</summary>
    public void SetInfo(string _name, string _description)
    {
        ApplyTexts(_name, string.Empty, _description);
        Show();
    }

    public void Clear()
    {
        ApplyTexts(string.Empty, string.Empty, string.Empty);
        Show();
    }

    // // 측정 (패널이 그 내용으로 그려진 채 남는다. 부른 쪽이 끝나고 Clear나 다시 그리기로 정리한다)

    public float MeasureCardWidth(in RoguelikeCardViewData _data)
    {
        ApplyCardTexts(in _data);
        return RequiredWidth();
    }

    public float MeasureCardHeight(in RoguelikeCardViewData _data)
    {
        SetCard(in _data);
        return ((RectTransform)transform).sizeDelta.y;
    }

    public float MeasureInfoWidth(string _name, string _description)
    {
        ApplyTexts(_name, string.Empty, _description);
        return RequiredWidth();
    }

    public float MeasureInfoHeight(string _name, string _description)
    {
        SetInfo(_name, _description);
        return ((RectTransform)transform).sizeDelta.y;
    }

    // // 내부

    private void ApplyCardTexts(in RoguelikeCardViewData _data)
    {
        ApplyTexts(LocalizeCard(_data.nameKey), GetLevelLabel(in _data), LocalizeCard(_data.descriptionKey));
    }

    private void ApplyTexts(string _name, string _level, string _description)
    {
        gameObject.SetActive(true);

        nameText.text = _name;
        levelText.text = _level;
        descriptionText.text = _description;
    }

    /// <summary>
    /// 이 내용이 줄바꿈 없이(수동 줄바꿈만으로) 들어가는 가장 좁은 패널 폭. 한계 폭을 넘으면 한계 폭.
    /// 이름 줄은 이름 + 레벨 표기가 한 줄에 놓이고, 설명은 가장 긴 줄이 들어가야 한다.
    /// </summary>
    private float RequiredWidth()
    {
        float levelWidth = LevelWidth();
        float nameWidth = string.IsNullOrEmpty(nameText.text) ? 0f : Mathf.Ceil(nameText.GetPreferredValues(nameText.text, Unbounded, Unbounded).x);
        float descWidth = string.IsNullOrEmpty(descriptionText.text) ? 0f : Mathf.Ceil(descriptionText.GetPreferredValues(descriptionText.text, Unbounded, Unbounded).x);

        float inner = Mathf.Max(descWidth, nameWidth + (0f < levelWidth ? levelWidth + LevelGap : 0f));
        return Mathf.Clamp(inner + PaddingX * 2f, MinPanelWidth, maxPanelWidth);
    }

    private float LevelWidth()
    {
        return string.IsNullOrEmpty(levelText.text) ? 0f : Mathf.Ceil(levelText.GetPreferredValues(levelText.text, Unbounded, Unbounded).x);
    }

    /// <summary>글자 영역과 패널 높이를 다시 정한다(왼쪽 위 기준 정수 픽셀).</summary>
    private void Show()
    {
        RectTransform panel = (RectTransform)transform;
        float panelWidth = 0f < fixedWidth ? fixedWidth : RequiredWidth();
        float innerWidth = panelWidth - PaddingX * 2f;

        float levelWidth = LevelWidth();
        float nameBoxWidth = Mathf.Max(0f, innerWidth - (0f < levelWidth ? levelWidth + LevelGap : 0f));
        float descBoxWidth = BalancedWidth(descriptionText, innerWidth);

        float nameHeight = Mathf.Max(MinLineHeight, MeasureHeight(nameText, nameBoxWidth));
        float descHeight = string.IsNullOrEmpty(descriptionText.text) ? 0f : Mathf.Max(MinLineHeight, MeasureHeight(descriptionText, descBoxWidth));

        SetTopLeft(nameText.rectTransform, PaddingX, PaddingY, nameBoxWidth, nameHeight);
        SetTopLeft(levelText.rectTransform, panelWidth - PaddingX - levelWidth, PaddingY, levelWidth, nameHeight);
        float descTop = PaddingY + nameHeight + RowGap;
        SetTopLeft(descriptionText.rectTransform, PaddingX, descTop, descBoxWidth, descHeight);

        float panelHeight = Mathf.Max(MinPanelHeight, descTop + descHeight + PaddingY);
        panel.sizeDelta = new Vector2(panelWidth, panelHeight);
    }

    private static float MeasureHeight(TMP_Text _text, float _width)
    {
        return Mathf.Ceil(_text.GetPreferredValues(_text.text, _width, Unbounded).y);
    }

    /// <summary>
    /// _limit 폭에서 나오는 줄 수·높이를 그대로 유지하는 가장 좁은 폭. 자동 줄바꿈이 일어나는 문구에서 마지막 줄에 한두 단어만 남는 것을 줄인다.
    /// 더 좁히면 높이가 늘어나는 지점(단어가 쪼개지거나 줄이 하나 더 생기는 지점)에서 멈추므로, 단어가 중간에서 끊기는 폭은 고르지 않는다.
    /// 수동 줄바꿈만 있는 문구는 가장 긴 줄의 폭이 된다(왼쪽 정렬이라 보이는 모양은 같다).
    /// </summary>
    private static float BalancedWidth(TMP_Text _text, float _limit)
    {
        if (string.IsNullOrEmpty(_text.text)) return _limit;

        float full = _text.GetPreferredValues(_text.text, _limit, Unbounded).y;
        float low = _limit * BalanceMinRatio;
        float high = _limit;

        for (int i = 0; i < BalanceSteps; i++)
        {
            float mid = (low + high) * 0.5f;
            if (_text.GetPreferredValues(_text.text, mid, Unbounded).y <= full + 0.01f) high = mid;
            else low = mid;
        }

        return Mathf.Min(_limit, Mathf.Ceil(high));
    }

    /// <summary>앵커·피벗이 왼쪽 위(0, 1)인 자식을 패널 왼쪽 위에서 (_left, _top)만큼 떨어진 곳에 놓는다.</summary>
    private static void SetTopLeft(RectTransform _rect, float _left, float _top, float _width, float _height)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.sizeDelta = new Vector2(_width, _height);
        _rect.anchoredPosition = new Vector2(Mathf.Floor(_left), -Mathf.Floor(_top));
    }

    private string LocalizeCard(string _key)
    {
        if (string.IsNullOrEmpty(_key)) return string.Empty;

        string text = null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
        return string.IsNullOrEmpty(text) ? _key : text;
    }

    private string GetLevelLabel(in RoguelikeCardViewData _data)
    {
        if (_data.bRuby || ECardKind.Ruby == _data.kind) return RubyLabel;
        if (ECardKind.Prism == _data.kind) return string.Empty;

        int level = Mathf.Clamp(_data.levelAfterPurchase, 1, MaxLevel);
        if (MaxLevel == level) return null != localizationManager ? localizationManager.GetText(LocKeys.RoguelikeUI.roguelikeCardLevelMax) : string.Empty;

        return LevelLabels[level];
    }
}
