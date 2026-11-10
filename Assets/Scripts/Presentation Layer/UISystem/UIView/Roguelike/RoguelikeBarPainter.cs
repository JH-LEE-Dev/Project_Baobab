using UnityEngine;
using UnityEngine.UI;

/// <summary>채움 팔레트(밝은 줄 / 기본 / 그림자). 로그라이크 HUD 막대가 같이 쓴다.</summary>
public readonly struct RoguelikeBarPalette
{
    public readonly Color32 light;
    public readonly Color32 main;
    public readonly Color32 shadow;

    public RoguelikeBarPalette(int _light, int _main, int _shadow)
    {
        light = RoguelikeHudColors.Hex(_light);
        main = RoguelikeHudColors.Hex(_main);
        shadow = RoguelikeHudColors.Hex(_shadow);
    }
}

/// <summary>
/// 로그라이크 HUD 공용 색(RoguelikeHUD_Polish_Plan.md 3장). 모두 불투명이고 반투명 픽셀은 그림자 한 장(Shadow 컴포넌트)뿐이다.
/// 에디터 빌더(RoguelikeHUDBuilder)도 스프라이트를 만들 때 같은 값을 쓴다.
/// </summary>
public static class RoguelikeHudColors
{
    public static readonly Color32 Outline = Hex(0x2A1A12);
    public static readonly Color32 Groove = Hex(0x4B3427);
    public static readonly Color32 GrooveTop = Hex(0x3A281D);
    public static readonly Color32 OverGroove = Hex(0x4C2722);
    public static readonly Color32 OverGrooveTop = Hex(0x3C1D1A);
    public static readonly Color32 White = Hex(0xFFF4DC);
    public static readonly Color32 ShadowColor = new Color32(0x1A, 0x0E, 0x08, 120);

    public static readonly RoguelikeBarPalette Stamina = new RoguelikeBarPalette(0xC8E67A, 0x8FC24A, 0x5F8F34);
    public static readonly RoguelikeBarPalette Amber = new RoguelikeBarPalette(0xFFD27A, 0xF0A43A, 0xB9702A);
    public static readonly RoguelikeBarPalette Red = new RoguelikeBarPalette(0xFF8A6E, 0xD8443A, 0x9A2A28);
    public static readonly RoguelikeBarPalette Kill = new RoguelikeBarPalette(0xF4A0AC, 0xD05A6E, 0x933A4E);
    public static readonly RoguelikeBarPalette Dash = new RoguelikeBarPalette(0xD2F6FF, 0x5CC8E2, 0x2E86A8);
    public static readonly RoguelikeBarPalette Ghost = new RoguelikeBarPalette(0xFFF4DC, 0xF3DCC6, 0xD9BCA4);
    public static readonly RoguelikeBarPalette Invincible = new RoguelikeBarPalette(0xFFF0B8, 0xF2CF5A, 0xB8902E);
    public static readonly RoguelikeBarPalette Flash = new RoguelikeBarPalette(0xFFF4DC, 0xFFF4DC, 0xFFF4DC);
    // 유예 줄 · 쿨다운 줄은 2행(위, 아래)이라 light = 위, main = 아래다.
    public static readonly RoguelikeBarPalette Grace = new RoguelikeBarPalette(0xC9A6FF, 0x9B6EE8, 0x9B6EE8);
    public static readonly RoguelikeBarPalette Cooldown = new RoguelikeBarPalette(0xFFB487, 0xF07F45, 0xF07F45);

    public static readonly Color32 PipPassedTop = Hex(0xE6B450);
    public static readonly Color32 PipPassedBottom = Hex(0xA87A2C);
    public static readonly Color32 PipRemain = Hex(0x5A4334);

    public static readonly Color NumberOver = new Color(1f, 0x9A / 255f, 0x80 / 255f, 1f);
    public static readonly Color NumberUnlimit = new Color(1f, 0xE3 / 255f, 0x9A / 255f, 1f);
    public static readonly Color NumberDone = new Color(1f, 0xD0 / 255f, 0x60 / 255f, 1f);

    public static Color32 Hex(int _rgb)
    {
        return new Color32((byte)((_rgb >> 16) & 0xFF), (byte)((_rgb >> 8) & 0xFF), (byte)(_rgb & 0xFF), 255);
    }
}

/// <summary>
/// 로그라이크 HUD 막대 안쪽(홈 + 채움 + 눈금)을 CPU로 그려 RawImage(Point 필터) 텍스처 하나에 올린다.
/// 좌표는 안쪽 영역 기준 왼쪽 위 원점(위에서 아래)이고 모두 정수 px이다. 버퍼와 텍스처는 한 번만 만들어 재사용한다(GC 없음).
/// 채움 규칙(plan 3장): 안쪽 0행 = 밝은 줄, 마지막 2행(안쪽 높이 6 미만이면 1행) = 그림자, 나머지 = 기본.
///   폭 3px 이상이면 끝 열(b-1)의 1행부터 아래를 그림자 색으로, 폭 8px 이상인 첫 구간만 0행의 a+2, a+3을 WHITE로(반짝임).
/// </summary>
public sealed class RoguelikeBarPainter
{
    /// <summary>HUD 연출 틱(24Hz).</summary>
    public const float TicksPerSecond = 24f;

    //외부 의존성
    private readonly RawImage surface;

    //내부 의존성
    private readonly int width;
    private readonly int height;
    private readonly Color32[] buffer;
    private readonly Texture2D texture;

    public int Width => width;
    public int Height => height;

    public RoguelikeBarPainter(RawImage _surface, int _width, int _height, string _name)
    {
        surface = _surface;
        width = _width;
        height = _height;
        buffer = new Color32[_width * _height];

        texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
        texture.name = _name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (null != surface)
        {
            surface.texture = texture;
            surface.raycastTarget = false;
        }

        Clear();
        Apply();
    }

    /// <summary>전체를 투명으로 비운다.</summary>
    public void Clear()
    {
        System.Array.Clear(buffer, 0, buffer.Length);
    }

    /// <summary>사각형 채우기. _y0 · _y1은 위에서 아래, 끝 값은 포함하지 않는다.</summary>
    public void Fill(int _x0, int _y0, int _x1, int _y1, in Color32 _color)
    {
        int x0 = Mathf.Max(0, _x0);
        int x1 = Mathf.Min(width, _x1);
        int y0 = Mathf.Max(0, _y0);
        int y1 = Mathf.Min(height, _y1);

        for (int y = y0; y < y1; y++)
        {
            int row = (height - 1 - y) * width;
            for (int x = x0; x < x1; x++) buffer[row + x] = _color;
        }
    }

    public void SetPixel(int _x, int _y, in Color32 _color)
    {
        if (_x < 0 || width <= _x || _y < 0 || height <= _y) return;

        buffer[(height - 1 - _y) * width + _x] = _color;
    }

    /// <summary>홈: 안쪽 전체 + 맨 위 1행. _overFrom이 0 이상이면 그 열부터 오른쪽 끝까지 초과 구간 홈 색.</summary>
    public void Groove(int _overFrom)
    {
        Fill(0, 0, width, height, RoguelikeHudColors.Groove);
        Fill(0, 0, width, 1, RoguelikeHudColors.GrooveTop);

        if (0 > _overFrom) return;

        Fill(_overFrom, 0, width, height, RoguelikeHudColors.OverGroove);
        Fill(_overFrom, 0, width, 1, RoguelikeHudColors.OverGrooveTop);
    }

    /// <summary>채움 한 구간 [_a, _b). _bGlint면 폭 8px 이상일 때 반짝임 두 픽셀을 WHITE로 그린다.</summary>
    public void Run(int _a, int _b, in RoguelikeBarPalette _palette, bool _bGlint)
    {
        int a = Mathf.Max(0, _a);
        int b = Mathf.Min(width, _b);
        if (b <= a) return;

        int shadowRows = 6 <= height ? 2 : 1;

        for (int k = 0; k < height; k++)
        {
            Color32 color = 0 == k ? _palette.light : (k >= height - shadowRows ? _palette.shadow : _palette.main);
            Fill(a, k, b, k + 1, color);
        }

        if (3 <= _b - _a)
        {
            for (int k = 1; k < height; k++) SetPixel(_b - 1, k, _palette.shadow);
        }

        if (true == _bGlint && 8 <= _b - _a)
        {
            SetPixel(_a + 2, 0, RoguelikeHudColors.White);
            SetPixel(_a + 3, 0, RoguelikeHudColors.White);
        }
    }

    /// <summary>끝 열(_b - 1) 전체를 한 색으로 덮는다(값 증가 · 회복 순간).</summary>
    public void EndColumn(int _b, in Color32 _color)
    {
        if (_b <= 0) return;

        Fill(_b - 1, 0, _b, height, _color);
    }

    /// <summary>열 하나 전체(T 표식 안쪽 열 등).</summary>
    public void Column(int _x, in Color32 _color)
    {
        Fill(_x, 0, _x + 1, height, _color);
    }

    /// <summary>버퍼를 텍스처에 올린다.</summary>
    public void Apply()
    {
        texture.SetPixelData(buffer, 0);
        texture.Apply(false);
    }

    /// <summary>만든 텍스처를 없앤다(소유 컴포넌트가 파괴될 때).</summary>
    public void Dispose()
    {
        if (null != texture) Object.Destroy(texture);
    }
}
