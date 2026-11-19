using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록 창 스포트라이트의 큰 숫자(그림 글자 8x13, 외곽선 포함 10x15). 언어와 무관한 그림이라 18개 언어에서 크기가 같다.
///   정수 또는 시간(mm:ss, 1시간 이상 h:mm:ss). 색 4종(금 · 흰색 · 민트 · 빨강)은 스프라이트 묶음을 바꿔 그린다(물들이지 않는다).
///   굴림: 마지막 자리만 스텐실 마스크(자리 칸, 경계를 칼같이 자른다) 안에서 지금 숫자가 위로 올라가고 다음 숫자가 아래에서 올라온다. 오프셋은 정수 픽셀(14px 간격).
/// 자리 칸(스텐실 Mask) · 글자 Image는 빌더가 만든다. 위치는 왼쪽 위 기준 정수 픽셀이고 크기는 스프라이트 원본 크기다.
/// </summary>
public class UI_RoguelikeBigNumber : MonoBehaviour
{
    public const int MaxChars = 8;
    public const int GlyphsPerPalette = 12;
    private const int ColonCode = 10;
    private const int DigitWidth = 10;
    private const int SymbolWidth = 4;
    private const int GlyphHeight = 15;
    // 다음 글자로 넘어가는 간격: 글리프 13 + 외곽선 겹침 1
    private const int RollStep = 14;

    public enum EPalette
    {
        Gold = 0,
        White = 1,
        Mint = 2,
        Red = 3,
    }

    //외부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("자리 칸(스텐실 Mask). 왼쪽 위 기준.")] private RectTransform[] cells = new RectTransform[MaxChars];
    [SerializeField] private Image[] glyphs = new Image[MaxChars];
    [SerializeField, Tooltip("굴림 때 아래에서 올라오는 다음 숫자.")] private Image[] nextGlyphs = new Image[MaxChars];
    [SerializeField, Tooltip("팔레트 순(금 · 흰색 · 민트 · 빨강) x 12(0~9, :, ,).")] private Sprite[] sprites = new Sprite[GlyphsPerPalette * 4];

    //상태 변수
    private readonly int[] codes = new int[MaxChars];
    private int width;

    /// <summary>마지막으로 그린 숫자의 폭(px, 외곽선 제외).</summary>
    public int Width => width;

    /// <summary>값을 그린다. _roll(0~1)은 마지막 자리의 굴림 정도다(0이면 굴림 없음).</summary>
    public void Show(int _value, bool _bTime, EPalette _palette, float _roll)
    {
        int count = true == _bTime ? FormatTime(_value) : FormatInteger(_value);
        int baseIndex = (int)_palette * GlyphsPerPalette;
        int rollOffset = Mathf.Clamp(Mathf.RoundToInt(_roll * RollStep), 0, RollStep);
        int x = 0;

        for (int i = 0; MaxChars > i; i++)
        {
            bool bUsed = i < count;
            if (null != cells[i] && cells[i].gameObject.activeSelf != bUsed) cells[i].gameObject.SetActive(bUsed);
            if (false == bUsed || null == cells[i]) continue;

            int code = codes[i];
            int glyphWidth = ColonCode > code ? DigitWidth : SymbolWidth;
            cells[i].anchoredPosition = new Vector2(x, 0f);
            cells[i].sizeDelta = new Vector2(glyphWidth, GlyphHeight);

            bool bRolling = i == count - 1 && ColonCode > code && 0 < rollOffset;
            SetGlyph(glyphs[i], sprites[baseIndex + code], glyphWidth, true == bRolling ? rollOffset : 0);
            if (null != nextGlyphs[i])
            {
                if (nextGlyphs[i].gameObject.activeSelf != bRolling) nextGlyphs[i].gameObject.SetActive(bRolling);
                if (true == bRolling) SetGlyph(nextGlyphs[i], sprites[baseIndex + (code + 1) % 10], glyphWidth, rollOffset - RollStep);
            }

            // 글자 간격: 외곽선 1px를 이웃과 겹친다(숫자 8 + 1, 기호 2 + 1).
            x += ColonCode > code ? DigitWidth - 1 : SymbolWidth - 1;
        }

        width = 0 < x ? x - 1 : 0;
    }

    /// <summary>값을 그리지 않고 보이는 폭(외곽선 포함 px)만 잰다(도장 자리 계산용). 다음 Show가 내용을 다시 채우므로 그리기에는 영향이 없다.</summary>
    public int Measure(int _value, bool _bTime)
    {
        int n = true == _bTime ? FormatTime(_value) : FormatInteger(_value);
        int total = 0;
        for (int i = 0; n > i; i++) total += ColonCode > codes[i] ? DigitWidth : SymbolWidth;
        return 0 < n ? total - (n - 1) : 0;
    }

    private static void SetGlyph(Image _image, Sprite _sprite, int _width, int _offsetY)
    {
        if (null == _image) return;

        if (_image.sprite != _sprite) _image.sprite = _sprite;
        RectTransform rect = _image.rectTransform;
        rect.sizeDelta = new Vector2(_width, GlyphHeight);
        rect.anchoredPosition = new Vector2(0f, _offsetY);
    }

    /// <summary>mm:ss 또는 h:mm:ss를 codes에 채우고 글자 수를 돌려준다.</summary>
    private int FormatTime(int _seconds)
    {
        int total = 0 > _seconds ? 0 : _seconds;
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int seconds = total % 60;
        int count = 0;

        if (0 < hours)
        {
            count = AppendDigits(hours, count);
            if (MaxChars > count) codes[count++] = ColonCode;
        }

        if (MaxChars > count) codes[count++] = minutes / 10;
        if (MaxChars > count) codes[count++] = minutes % 10;
        if (MaxChars > count) codes[count++] = ColonCode;
        if (MaxChars > count) codes[count++] = seconds / 10;
        if (MaxChars > count) codes[count++] = seconds % 10;
        return count;
    }

    private int FormatInteger(int _value)
    {
        return AppendDigits(0 > _value ? 0 : _value, 0);
    }

    /// <summary>_value의 자릿수를 _start부터 앞자리 먼저 채우고 새 글자 수를 돌려준다.</summary>
    private int AppendDigits(int _value, int _start)
    {
        int digits = 1;
        for (int v = _value; 10 <= v; v /= 10) digits++;
        digits = Mathf.Min(digits, MaxChars - _start);

        int value = _value;
        for (int i = digits - 1; 0 <= i; i--)
        {
            codes[_start + i] = value % 10;
            value /= 10;
        }
        return _start + digits;
    }
}
