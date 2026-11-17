using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using PresentationLayer.VFX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore;
using UnityEngine.UI;

/// <summary>
/// 도전과제 창의 한 칸(시안 B 리본, Docs/Roguelike/_Work/ChallengeUI_B_Ribbon_Plan.md): 상자 없이 투명 바탕 위에 왼쪽 강조 줄 + 아래 점선 구분선 + 제목 + 팁이다.
/// 달성한 칸은 금색 6% 틴트 + 금색 강조 줄 + 제목 뒤 금색 리본(제목은 리본 위 INK 색, 리본이 달성 표시다 - 깃발은 없다), 미달성 칸은 CREAM 제목 + 오른쪽 진행 수치(목표 2 이상일 때)다.
/// 칸 높이 = 6 + 리본 높이 + 4 + 팁 높이 + 7(최소는 화면이 정한다, FitHeight). 리본 높이와 제목 기준선은 화면이 언어별 잉크 높이로 재어 SetRibbonMetrics로 넘긴다.
///
/// 칸 전체가 패드 선택용 대상(Button, 탐색은 Explicit - 화면이 위아래로 잇는다)이다. 눌러도 하는 일은 없다.
/// 마우스 호버와 패드 선택은 FocusedEvent(칸, 마우스인지)로 알리고, 마우스가 나가면 ExitedEvent로 알린다(UI_RoguelikeRecordRow와 같은 방식).
/// 보이는 것은 모두 content 아래에 있어 등장 연출(5px 아래에서 올라오며 나타남)이 content만 움직이고, 칸 자신(레이캐스트 · 커서 대상)은 제자리에 있다.
/// </summary>
public class UI_RoguelikeChallengeRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    // 칸 안쪽 배치(칸 왼쪽 위 기준 px).
    private const int RibbonY = 6;
    private const int RibbonFoldX = 5;
    private const int RibbonBodyX = RibbonFoldX + UI_ChallengeRibbon.FoldWidth;
    // 리본 몸통 폭 = 제목 폭 + 안쪽 왼쪽 8 + 오른쪽 6. 제목은 달성 여부와 상관없이 x 16(= 몸통 x 8 + 안쪽 왼쪽 8)이라 팁 · 미달성 제목과 줄이 맞는다.
    private const int RibbonPaddingLeft = 8;
    private const int RibbonPaddingRight = 6;
    private const int DefaultTitleBaseline = 3;
    private const float TitleX = 16f;
    private const float TipX = 16f;
    private const float TipGap = 4f;
    private const float BottomPadding = 7f;
    private const float ProgressRight = 8f;
    private const float ProgressMinGap = 8f;
    private const float MinTitleHeight = 14f;
    private const float TextWidthSlack = 2f;
    private const float TipMeasureHeight = 10000f;
    private const int DefaultRibbonHeight = 15;

    // 칸 장식(강조 줄 · 구분선 · 달성 틴트)
    private const int AccentWidth = 2;
    private const int AccentInset = 2;
    private const int DividerDotStep = 6;
    private static readonly Color32 AccentNormalColor = new Color32(0x4A, 0x32, 0x26, 255);
    private static readonly Color32 AccentClearedColor = new Color32(0xFF, 0xD4, 0x59, 255);
    private static readonly Color32 DividerColor = new Color32(0x4A, 0x32, 0x26, 255);
    private static readonly Color32 DividerDotColor = new Color32(0x2A, 0x1A, 0x12, 255);
    // 달성 틴트(시안: 게임 감마 rgba(255,196,80,0.06)). 본문 바탕색 위에서 합성 결과가 시안과 같도록 색공간에 맞춰 보정해 쓴다(GetClearedTint).
    private static readonly Color32 ClearedTintColor = new Color32(255, 196, 80, 15);
    private static readonly Color32 PanelBodyColor = new Color32(0x1E, 0x15, 0x0E, 255);

    // 등장 연출(30fps 틱, unscaled): 5px 아래에서 올라오며 0.14초 동안 나타난다(OutCubic).
    private const float EntranceTickRate = 30f;
    private const float EntranceSeconds = 0.14f;
    private const float EntranceOffset = 5f;

    public event Action<UI_RoguelikeChallengeRow, bool> FocusedEvent;
    public event Action<UI_RoguelikeChallengeRow> ExitedEvent;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Challenges)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Button button;
    [SerializeField, Tooltip("칸 바탕 Image(알파 0, 마우스 · 패드 선택을 받는 레이캐스트용).")] private Image background;
    [SerializeField, Tooltip("달성한 칸의 제목 색(리본 위 INK).")] private Color clearedTitleColor = Color.white;
    [SerializeField, Tooltip("달성한 칸의 팁 색.")] private Color clearedTipColor = Color.white;

    [SerializeField, Tooltip("달성 칸 파티클 연출(리본 펼침 · 광택 · 반짝임 · 금가루). 달성한 칸에서만 돈다.")] private UI_ChallengeClearGlow clearGlow;
    [SerializeField, Tooltip("보이는 것 전부의 부모. 등장 연출이 이 오브젝트의 위치와 CanvasGroup 알파를 바꾼다.")] private RectTransform content;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField, Tooltip("강조 줄 · 구분선 · 달성 틴트를 그리는 그래픽(기본 UI 머티리얼).")] private UI_LootGlowParticles decor;
    [SerializeField, Tooltip("달성 칸의 금색 리본.")] private UI_ChallengeRibbon ribbon;
    [SerializeField, Tooltip("미달성 칸의 진행 수치(\"진행 / 목표\"). 목표가 2 이상일 때만 켠다.")] private TMP_Text progressText;

    //상태 변수
    private Color normalTitleColor = Color.white;
    private Color normalTipColor = Color.white;
    private bool bNormalColorCached;
    private bool bCleared;
    private string progressString = string.Empty;
    private int ribbonHeight = DefaultRibbonHeight;
    private int titleBaseline = DefaultTitleBaseline;
    private int ribbonBodyWidth;
    private int decorWidth = -1;
    private int decorHeight = -1;
    private bool bDecorCleared;
    private static Color32 clearedTint;
    private static bool bClearedTintReady;
    private bool bEntrancing;
    private float entranceDelay;
    private float entranceElapsed;
    private int lastEntranceTick;

    public RectTransform Rect => (RectTransform)transform;
    public Button SelectButton => button;
    /// <summary>마지막 SetChallenge의 달성 여부.</summary>
    public bool IsCleared => bCleared;

    /// <summary>
    /// 글자 잉크의 위아래 범위(기준선 기준, 위가 +)를 _top(최댓값) · _bottom(최솟값)에 합친다. 글리프 베어링에 폰트 크기 배율을 곱해 재고(폴백 폰트 포함) 공백 · 태그는 건너뛴다.
    /// 호출 측이 _top = float.MinValue, _bottom = float.MaxValue로 시작해 문구를 이어서 넘기면 언어 전체의 잉크 범위(기준선 위 + 아래)가 된다. 할당 없음.
    /// </summary>
    public static void MeasureInk(TMP_FontAsset _font, float _fontSize, string _text, ref float _top, ref float _bottom)
    {
        if (null == _font || string.IsNullOrEmpty(_text)) return;

        bool bInTag = false;
        for (int i = 0; _text.Length > i; i++)
        {
            char c = _text[i];
            if ('<' == c) { bInTag = true; continue; }
            if ('>' == c && true == bInTag) { bInTag = false; continue; }
            if (true == bInTag || true == char.IsWhiteSpace(c)) continue;

            if (false == TryGetGlyph(_font, c, 0, out TMP_Character character, out TMP_FontAsset owner)) continue;

            GlyphMetrics metrics = character.glyph.metrics;
            if (0f >= metrics.height) continue;

            float scale = _fontSize / owner.faceInfo.pointSize * owner.faceInfo.scale;
            _top = Mathf.Max(_top, metrics.horizontalBearingY * scale);
            _bottom = Mathf.Min(_bottom, (metrics.horizontalBearingY - metrics.height) * scale);
        }
    }

    // 폰트(폴백 포함)에서 글자 하나의 글리프를 찾는다.
    private static bool TryGetGlyph(TMP_FontAsset _font, uint _unicode, int _depth, out TMP_Character _character, out TMP_FontAsset _owner)
    {
        _character = null;
        _owner = null;
        if (null == _font || 4 < _depth) return false;

        if (true == _font.characterLookupTable.TryGetValue(_unicode, out TMP_Character found) && null != found.glyph)
        {
            _character = found;
            _owner = _font;
            return true;
        }

        List<TMP_FontAsset> fallbacks = _font.fallbackFontAssetTable;
        if (null == fallbacks) return false;

        for (int i = 0; fallbacks.Count > i; i++)
        {
            if (true == TryGetGlyph(fallbacks[i], _unicode, _depth + 1, out _character, out _owner)) return true;
        }

        return false;
    }

    /// <summary>칸을 만든 직후 한 번 부른다. 기본 색을 기억하고 바탕 Image를 투명(레이캐스트만)으로 둔다.</summary>
    public void Initialize()
    {
        CacheNormalState();
        if (null != background)
        {
            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = true;
        }
    }

    public void SetChallenge(in RoguelikeChallengeViewData _challenge)
    {
        CacheNormalState();

        bCleared = true == _challenge.bCleared;

        if (null != titleText)
        {
            titleText.text = null != _challenge.title ? _challenge.title : string.Empty;
            titleText.color = true == bCleared ? clearedTitleColor : normalTitleColor;
        }

        if (null != tipText)
        {
            tipText.text = null != _challenge.tip ? _challenge.tip : string.Empty;
            tipText.color = true == bCleared ? clearedTipColor : normalTipColor;
        }

        if (null != ribbon) ribbon.gameObject.SetActive(true == bCleared);

        // 진행 수치: 미달성이고 목표가 2 이상일 때만("1,234 / 5,000", 진행값은 목표를 넘지 않게 자른다).
        bool bProgress = false == bCleared && 1 < _challenge.goal;
        progressString = true == bProgress
            ? string.Concat(Mathf.Clamp(_challenge.progress, 0, _challenge.goal).ToString("N0", CultureInfo.InvariantCulture), " / ", _challenge.goal.ToString("N0", CultureInfo.InvariantCulture))
            : string.Empty;
        if (null != progressText)
        {
            progressText.text = progressString;
            progressText.gameObject.SetActive(true == bProgress);
        }

        if (false == bCleared) StopClearGlow();
    }

    /// <summary>
    /// 이 언어의 리본 높이(1 + 2 + 잉크 높이 + 2 + 1)와 리본 윗변에서 제목 기준선까지의 거리(= 3 + 잉크 위쪽)를 받는다. 화면이 언어 전체 제목 · 머리줄의 잉크 범위로 재어 모든 칸에 같은 값을 넘긴다.
    /// FitHeight 전에 부른다.
    /// </summary>
    public void SetRibbonMetrics(int _ribbonHeight, int _titleBaseline)
    {
        ribbonHeight = Mathf.Max(1, _ribbonHeight);
        titleBaseline = _titleBaseline;
    }

    /// <summary>달성 칸의 리본 몸통 사각형(칸 왼쪽 위 기준 px). 튀는 조각 시작점을 리본 가운데로 맞출 때 쓴다. 마지막 FitHeight 기준.</summary>
    public void GetRibbonRect(out int _x, out int _y, out int _width, out int _height)
    {
        _x = RibbonBodyX;
        _y = RibbonY;
        _width = ribbonBodyWidth;
        _height = ribbonHeight;
    }

    /// <summary>
    /// 현재 폰트 · 문구에 맞춰 제목 · 팁 · 진행 수치 · 리본을 배치하고 칸 높이를 돌려준다(최소 _minHeight).
    /// 칸 높이 = 6 + 리본 높이 + 4 + 팁 높이 + 7(진행 수치가 제목과 8px 이상 떨어지지 못하면 팁 아래 줄 오른쪽으로 내리고 그만큼 한 줄을 더한다).
    /// 제목은 리본 안에서 언어 잉크 기준(titleBaseline)으로 세로 가운데에 놓는다. 칸 자신의 sizeDelta는 건드리지 않는다(배치는 화면이 한다).
    /// 폰트 교체 이후에 불러야 한다. 할당 없음.
    /// </summary>
    public float FitHeight(float _minHeight)
    {
        float ribbonBottom = RibbonY + ribbonHeight;
        float rowWidth = Mathf.Round(Rect.rect.width);

        // 제목: 폭은 글자 폭, 위치는 기준선이 리본 윗변 + titleBaseline에 오도록 맞춘다(TopLeft 정렬이라 첫 줄 기준선 = 상자 윗변 + 글자 위쪽 높이).
        float titleWidth = 0f;
        float titleTop = RibbonY;
        float lineHeight = MinTitleHeight;
        if (null != titleText)
        {
            lineHeight = Mathf.Max(MinTitleHeight, Mathf.Ceil(MeasureLineHeight(titleText)));
            titleWidth = Mathf.Ceil(titleText.GetPreferredValues(titleText.text, TipMeasureHeight, TipMeasureHeight).x);
            titleTop = RibbonY + titleBaseline - Mathf.Round(MeasureAscent(titleText));

            RectTransform titleRect = titleText.rectTransform;
            titleRect.sizeDelta = new Vector2(titleWidth + TextWidthSlack, lineHeight);
            titleRect.anchoredPosition = new Vector2(TitleX, -titleTop);
        }

        // 리본: 몸통 폭 = 제목 폭 + 14, 접힌 끝은 x 5부터(몸통은 x 8부터)
        ribbonBodyWidth = Mathf.CeilToInt(titleWidth) + RibbonPaddingLeft + RibbonPaddingRight;
        if (true == bCleared)
        {
            if (null != ribbon)
            {
                ribbon.SetSize(ribbonBodyWidth, ribbonHeight);
                ribbon.rectTransform.anchoredPosition = new Vector2(RibbonFoldX, -RibbonY);
            }

            if (null != clearGlow) clearGlow.SetRibbonRect(RibbonBodyX, RibbonY, ribbonBodyWidth, ribbonHeight);
        }

        // 팁
        float tipTop = ribbonBottom + TipGap;
        float tipHeight = 0f;
        if (null != tipText)
        {
            RectTransform tipRect = tipText.rectTransform;
            tipHeight = Mathf.Ceil(tipText.GetPreferredValues(tipText.text, tipRect.rect.width, TipMeasureHeight).y);
            tipRect.sizeDelta = new Vector2(tipRect.sizeDelta.x, tipHeight);
            tipRect.anchoredPosition = new Vector2(TipX, -tipTop);
        }

        // 진행 수치: 제목 줄 오른쪽 끝(x 332)에 오른쪽 정렬. 제목과 8px 이상 못 떨어지면 팁 아래 줄 오른쪽으로 내린다.
        float belowHeight = 0f;
        if (null != progressText && false == bCleared && false == string.IsNullOrEmpty(progressString))
        {
            float progressWidth = Mathf.Ceil(progressText.GetPreferredValues(progressText.text, TipMeasureHeight, TipMeasureHeight).x);
            float progressHeight = Mathf.Max(MinTitleHeight, Mathf.Ceil(MeasureLineHeight(progressText)));
            bool bBelow = rowWidth - ProgressRight - progressWidth - (TitleX + titleWidth) < ProgressMinGap;
            float progressTop = true == bBelow ? tipTop + tipHeight : titleTop;
            if (true == bBelow) belowHeight = progressHeight;

            RectTransform progressRect = progressText.rectTransform;
            progressRect.sizeDelta = new Vector2(progressWidth + TextWidthSlack, progressHeight);
            progressRect.anchoredPosition = new Vector2(-ProgressRight, -progressTop);
        }

        return Mathf.Max(_minHeight, ribbonBottom + TipGap + tipHeight + belowHeight + BottomPadding);
    }

    /// <summary>
    /// 화면이 칸 크기(sizeDelta)를 정한 뒤 부른다. 강조 줄 · 아래 구분선(+ 6px마다 점) · 달성 틴트를 칸 크기에 맞춰 다시 그린다(크기나 달성 여부가 바뀔 때만).
    /// </summary>
    public void ApplyLayout(float _width, float _height)
    {
        if (null == decor) return;

        int width = Mathf.RoundToInt(_width);
        int height = Mathf.RoundToInt(_height);
        if (width == decorWidth && height == decorHeight && bCleared == bDecorCleared) return;

        decorWidth = width;
        decorHeight = height;
        bDecorCleared = bCleared;

        // 그래픽 좌표: 원점 = 칸 왼쪽 아래, y 위로 증가
        decor.BeginQuads();
        if (true == bCleared) decor.Rect(0, 0, width, height, GetClearedTint());
        decor.Rect(0, AccentInset, AccentWidth, height - AccentInset, true == bCleared ? AccentClearedColor : AccentNormalColor);
        decor.Rect(AccentWidth, 0, width, 1, DividerColor);
        for (int x = DividerDotStep; width > x; x += DividerDotStep) decor.Dot(x, 0, DividerDotColor);
        decor.Commit();
    }

    /// <summary>
    /// 달성 틴트의 그리기 색. 시안은 감마 공간 알파 블렌드(결과 = 바탕 x (1 - a) + 틴트 x a)인데, 프로젝트는 Linear 색공간이라 같은 알파를 주면 선형 공간에서 섞여 어두운 바탕 위에서 훨씬 밝게 나온다.
    /// 그래서 Linear일 때는 알파는 그대로 두고 틴트 rgb를 "바탕(PanelBodyColor) 위에서 선형 블렌드 결과가 감마 블렌드 결과와 같아지는 값"으로 바꿔 쓴다. 감마 색공간이면 원래 색 그대로다.
    /// </summary>
    private static Color32 GetClearedTint()
    {
        if (true == bClearedTintReady) return clearedTint;

        bClearedTintReady = true;
        clearedTint = ClearedTintColor;
        if (ColorSpace.Linear != QualitySettings.activeColorSpace) return clearedTint;

        float alpha = ClearedTintColor.a / 255f;
        clearedTint.r = CompensateChannel(PanelBodyColor.r, ClearedTintColor.r, alpha);
        clearedTint.g = CompensateChannel(PanelBodyColor.g, ClearedTintColor.g, alpha);
        clearedTint.b = CompensateChannel(PanelBodyColor.b, ClearedTintColor.b, alpha);
        return clearedTint;
    }

    private static byte CompensateChannel(byte _base, byte _tint, float _alpha)
    {
        float baseGamma = _base / 255f;
        float target = Mathf.GammaToLinearSpace(baseGamma * (1f - _alpha) + _tint / 255f * _alpha);
        float baseLinear = Mathf.GammaToLinearSpace(baseGamma);
        float tintLinear = Mathf.Clamp01((target - baseLinear * (1f - _alpha)) / _alpha);
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(tintLinear)) * 255f);
    }

    /// <summary>등장 연출 시작: _delay초 뒤 5px 아래에서 올라오며 0.14초 동안 나타난다. 지연 동안은 보이지 않는다.</summary>
    public void PlayEntrance(float _delay)
    {
        bEntrancing = true;
        entranceDelay = _delay;
        entranceElapsed = 0f;
        lastEntranceTick = int.MinValue;
        ApplyEntrance(0f);
    }

    /// <summary>_deltaTime(unscaled)만큼 등장 연출을 진행한다(30fps 틱 단위, 위치는 정수 픽셀). 연출 중이 아니면 바로 돌아간다.</summary>
    public void TickEntrance(float _deltaTime)
    {
        if (false == bEntrancing) return;

        entranceElapsed += _deltaTime;
        int tick = Mathf.FloorToInt(entranceElapsed * EntranceTickRate);
        if (tick == lastEntranceTick) return;

        lastEntranceTick = tick;
        float ratio = Mathf.Clamp01((tick / EntranceTickRate - entranceDelay) / EntranceSeconds);
        ApplyEntrance(ratio);
        if (1f <= ratio) bEntrancing = false;
    }

    /// <summary>등장 연출을 멈추고 제자리에 완전히 보이게 한다.</summary>
    public void StopEntrance()
    {
        bEntrancing = false;
        ApplyEntrance(1f);
    }

    public void PlayClearGlow(float _delay, int _order, bool _bEntrance)
    {
        if (null != clearGlow) clearGlow.Play(_delay, _order, _bEntrance);
    }

    public void StopClearGlow()
    {
        if (null != clearGlow) clearGlow.Stop();
    }

    public void TickClearGlow(float _deltaTime)
    {
        if (null != clearGlow) clearGlow.Tick(_deltaTime);
    }

    // _ratio(0~1): 알파는 그대로, 위치는 5 x (1 - OutCubic) = 5 x (1 - ratio)^3 을 반올림한 정수 px만큼 아래.
    private void ApplyEntrance(float _ratio)
    {
        if (null != contentGroup) contentGroup.alpha = _ratio;
        if (null == content) return;

        float inverse = 1f - _ratio;
        int offset = Mathf.FloorToInt(EntranceOffset * inverse * inverse * inverse + 0.5f);
        content.anchoredPosition = new Vector2(0f, -offset);
    }

    // 글자 한 줄이 차지하는 높이(올림 전) = (ascentLine - descentLine). faceInfo.lineHeight(줄 간격)는 이보다 커서 쓰지 않는다.
    public static float MeasureLineHeight(TMP_Text _text)
    {
        TMP_FontAsset font = _text.font;
        if (null == font || 0f >= font.faceInfo.pointSize) return MinTitleHeight;

        return (font.faceInfo.ascentLine - font.faceInfo.descentLine) * _text.fontSize / font.faceInfo.pointSize * font.faceInfo.scale;
    }

    // 첫 줄 기준선이 상자 윗변에서 떨어진 거리(TopLeft 정렬) = ascentLine
    public static float MeasureAscent(TMP_Text _text)
    {
        TMP_FontAsset font = _text.font;
        if (null == font || 0f >= font.faceInfo.pointSize) return MinTitleHeight;

        return font.faceInfo.ascentLine * _text.fontSize / font.faceInfo.pointSize * font.faceInfo.scale;
    }

    // 미달성 제목 · 팁 색을 아직 못 잡았으면 잡는다.
    private void CacheNormalState()
    {
        if (true == bNormalColorCached) return;

        if (null != titleText) normalTitleColor = titleText.color;
        if (null != tipText) normalTipColor = tipText.color;
        bNormalColorCached = true;
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

    private void OnDisable()
    {
        if (true == bEntrancing) StopEntrance();
    }

    private void OnDestroy()
    {
        FocusedEvent = null;
        ExitedEvent = null;
    }
}
