using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록 창의 작은 숫자(그림 글꼴 5x7 + 외곽선, 칸 7x9). 언어와 무관한 그림이라 18개 언어에서 폭이 같다(계획서 11-4).
///   정수 · 천 단위 쉼표 · 시간(mm:ss, 1시간 이상 h:mm:ss) · 접두 글자(W · ×) + 정수.
///   색 5종(크림 · 금 · 빨강 · 민트 · 흐린 크림)은 스프라이트 묶음을 바꿔 그린다(물들이지 않는다 - 외곽선까지 곱해지지 않게).
///   글자 Image는 빌더가 MaxChars개 만든다. 이웃 글자는 외곽선 1px를 겹친다(간격 = 칸 폭 - 1). 위치는 모두 정수 픽셀.
///   정렬: 왼쪽이면 상자 왼쪽 끝에서, 오른쪽이면 상자(이 RectTransform) 오른쪽 끝에 외곽선 끝을 맞춘다.
/// </summary>
public class UI_RoguelikeSmallNumber : MonoBehaviour
{
    public const int MaxChars = 10;
    public const int GlyphsPerPalette = 14;
    public const int CodeColon = 10;
    public const int CodeComma = 11;
    public const int CodeTimes = 12;
    public const int CodeW = 13;
    private const int GlyphHeight = 9;

    public enum EPalette
    {
        Cream = 0,
        Gold = 1,
        Red = 2,
        Mint = 3,
        Muted = 4,
    }

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Image[] glyphs = new Image[MaxChars];
    [SerializeField, Tooltip("팔레트 순(크림 · 금 · 빨강 · 민트 · 흐린 크림) x 14(0~9, :, ,, ×, W).")] private Sprite[] sprites = new Sprite[GlyphsPerPalette * 5];
    [SerializeField, Tooltip("참이면 상자 오른쪽 끝에 맞춘다.")] private bool bRightAlign;

    //상태 변수
    private readonly int[] codes = new int[MaxChars];
    private int count;
    private int width;

    /// <summary>마지막으로 그린 숫자의 폭(외곽선 포함 px).</summary>
    public int Width => width;
    public RectTransform Rect => (RectTransform)transform;

    public void SetInteger(int _value, EPalette _palette)
    {
        count = AppendDigits(0 > _value ? 0 : _value, 0);
        Render(_palette);
    }

    public void SetThousands(int _value, EPalette _palette)
    {
        count = AppendThousands(0 > _value ? 0 : _value, 0);
        Render(_palette);
    }

    public void SetTime(int _seconds, EPalette _palette)
    {
        int total = 0 > _seconds ? 0 : _seconds;
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int seconds = total % 60;
        int n = 0;
        if (0 < hours)
        {
            n = AppendDigits(hours, n);
            n = Put(CodeColon, n);
        }
        n = Put(minutes / 10, n);
        n = Put(minutes % 10, n);
        n = Put(CodeColon, n);
        n = Put(seconds / 10, n);
        count = Put(seconds % 10, n);
        Render(_palette);
    }

    /// <summary>접두 글자(CodeW · CodeTimes) + 정수. 예: W27, ×17.</summary>
    public void SetPrefixed(int _prefixCode, int _value, EPalette _palette)
    {
        int n = Put(_prefixCode, 0);
        count = AppendDigits(0 > _value ? 0 : _value, n);
        Render(_palette);
    }

    /// <summary>값을 그리지 않고 폭만 잰다(배치 계산용). 상태는 바꾸지 않는다.</summary>
    public static int MeasureInteger(int _value)
    {
        int digits = 1;
        for (int v = 0 > _value ? 0 : _value; 10 <= v; v /= 10) digits++;
        return digits * 6 + 1;
    }

    private static int GlyphWidth(int _code)
    {
        if (CodeColon == _code) return 3;
        if (CodeComma == _code) return 4;
        return 7;
    }

    private int Put(int _code, int _index)
    {
        if (MaxChars <= _index) return _index;
        codes[_index] = _code;
        return _index + 1;
    }

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

    private int AppendThousands(int _value, int _start)
    {
        if (1000 > _value) return AppendDigits(_value, _start);

        int n = AppendThousands(_value / 1000, _start);
        int rest = _value % 1000;
        n = Put(CodeComma, n);
        n = Put(rest / 100, n);
        n = Put(rest / 10 % 10, n);
        return Put(rest % 10, n);
    }

    private void Render(EPalette _palette)
    {
        // 보이는 폭 = 칸 폭 합 - 겹침(글자 수 - 1)
        width = 0;
        for (int i = 0; count > i; i++) width += GlyphWidth(codes[i]);
        if (0 < count) width -= count - 1;

        int x = true == bRightAlign ? Mathf.RoundToInt(Rect.rect.width) - width : 0;
        int baseIndex = (int)_palette * GlyphsPerPalette;
        for (int i = 0; MaxChars > i; i++)
        {
            Image image = glyphs[i];
            if (null == image) continue;

            bool bUsed = i < count;
            if (image.gameObject.activeSelf != bUsed) image.gameObject.SetActive(bUsed);
            if (false == bUsed) continue;

            int code = codes[i];
            int w = GlyphWidth(code);
            Sprite sprite = sprites[baseIndex + code];
            if (image.sprite != sprite) image.sprite = sprite;
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(w, GlyphHeight);
            rect.anchoredPosition = new Vector2(x, 0f);
            x += w - 1;
        }
    }
}
