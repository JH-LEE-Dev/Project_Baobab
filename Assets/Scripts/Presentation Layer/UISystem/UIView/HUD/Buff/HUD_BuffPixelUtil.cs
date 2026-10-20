using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프 HUD 전용 픽셀 유틸. 스프라이트 픽셀 읽기(읽기 불가 텍스처도 RT를 거쳐 읽는다)와 임시 아이콘 생성을 담당한다.
/// </summary>
public static class HUD_BuffPixelUtil
{
    public const int TempIconCount = 3;

    // 임시 아이콘(12x12). 팔레트 문자 -> 색, 행은 위에서 아래.
    private static readonly string[][] TempIconRows =
    {
        new string[]
        {
            ".......aaa..", "......fffee.", "....affffeea", "...fffeeeccb", "..afeeecccbb", "..eeeeccccbb",
            ".aeecccccbbb", ".aecccccbbbb", ".dcccccbbba.", "dddaaaaa....", "dd..........", "da..........",
        },
        new string[]
        {
            "......eed...", ".....eeed...", "....eeddc...", "...eedccbaaa", "..eeddccceea", ".aeddccccee.",
            ".....ccdd...", "....accdd...", "....cccb....", "...ccc......", "...ccb......", "...bb.......",
        },
        new string[]
        {
            "..aa....aa..", ".eedd..dddc.", "aeeddcadddcb", "ddddccccccbb", "ddddccccccbb", "dddccccccbbb",
            ".ccccccccbb.", "..ccccccbba.", "...ccccbbb..", "....cccbb...", ".....ccb....", "......b.....",
        },
    };

    private static readonly uint[][] TempIconPalette =
    {
        new uint[] { 0x20380F, 0x3D7A24, 0x68B036, 0x8A5A2C, 0x9EDC54, 0xE3FFA8 },
        new uint[] { 0x3B1D05, 0xC26A0A, 0xF0A81C, 0xFFD84A, 0xFFF7B0 },
        new uint[] { 0x3A0A14, 0xA3182C, 0xE03A4E, 0xFF7A86, 0xFFD0D4 },
    };

    private static readonly Sprite[] tempIconSprites = new Sprite[TempIconCount];
    private static readonly Dictionary<Sprite, Color32[]> spritePixelCache = new Dictionary<Sprite, Color32[]>(16);

    // 스프라이트의 텍스처 영역을 RGBA32 픽셀(아래에서 위 순서)로 읽는다. 결과는 스프라이트별로 캐싱한다.
    public static bool TryGetSpritePixels(Sprite _sprite, out Color32[] _pixels, out int _width, out int _height)
    {
        _pixels = null;
        _width = 0;
        _height = 0;

        if (null == _sprite || null == _sprite.texture)
        {
            return false;
        }

        Rect _rect = _sprite.textureRect;
        _width = Mathf.RoundToInt(_rect.width);
        _height = Mathf.RoundToInt(_rect.height);

        if (true == spritePixelCache.TryGetValue(_sprite, out _pixels))
        {
            return true;
        }

        Texture2D _source = _sprite.texture;
        RenderTexture _previous = RenderTexture.active;
        RenderTexture _rt = RenderTexture.GetTemporary(_source.width, _source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D _readback = null;

        try
        {
            Graphics.Blit(_source, _rt);
            RenderTexture.active = _rt;
            _readback = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _readback.ReadPixels(new Rect(_rect.x, _rect.y, _width, _height), 0, 0);
            _pixels = _readback.GetPixels32();
        }
        finally
        {
            RenderTexture.active = _previous;
            RenderTexture.ReleaseTemporary(_rt);

            if (null != _readback)
            {
                Object.Destroy(_readback);
            }
        }

        spritePixelCache[_sprite] = _pixels;
        return true;
    }

    // 임시 아이콘 스프라이트(Point 필터, 12x12). 실제 스프라이트가 준비되면 BuffDisplayInfo.icon에 넣어 대체한다.
    public static Sprite GetTempIcon(int _index)
    {
        int _i = Mathf.Abs(_index) % TempIconCount;

        if (null != tempIconSprites[_i])
        {
            return tempIconSprites[_i];
        }

        string[] _rows = TempIconRows[_i];
        uint[] _palette = TempIconPalette[_i];
        Texture2D _tex = new Texture2D(12, 12, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Point;
        _tex.wrapMode = TextureWrapMode.Clamp;
        _tex.name = "BuffTempIcon_" + _i;
        Color32[] _px = new Color32[144];

        for (int y = 0; y < 12; y++)
        {
            string _row = _rows[y];

            for (int x = 0; x < 12; x++)
            {
                char _c = _row[x];
                Color32 _col = new Color32(0, 0, 0, 0);

                if ('.' != _c)
                {
                    uint _rgb = _palette[_c - 'a'];
                    _col = new Color32((byte)((_rgb >> 16) & 0xFF), (byte)((_rgb >> 8) & 0xFF), (byte)(_rgb & 0xFF), 255);
                }

                // 위에서 아래 행 -> 텍스처는 아래에서 위
                _px[(11 - y) * 12 + x] = _col;
            }
        }

        _tex.SetPixels32(_px);
        _tex.Apply(false, false);
        Sprite _sprite = Sprite.Create(_tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 32f);
        _sprite.name = _tex.name;
        tempIconSprites[_i] = _sprite;
        return _sprite;
    }
}

/// <summary>
/// 숫자 글리프 합성기. 시트의 8x8 셀에서 글자 폭만 잘라 쓰고, 글자 사이 검은 외곽선 1px만 겹친다(흰 획은 겹치지 않는다).
/// </summary>
public class HUD_BuffNumberFont
{
    public const int GlyphHeight = 8;
    private const int PlusSize = 5;

    private readonly Color32[][] digitPixels = new Color32[10][];
    private readonly int[] digitWidth = new int[10];
    private readonly Color32[] plusPixels = new Color32[PlusSize * GlyphHeight];
    private readonly int[] digitBuffer = new int[4];
    private bool bReady = false;

    public bool IsReady
    {
        get { return bReady; }
    }

    // _digitSprites[d]가 숫자 d의 스프라이트(0~9 순서).
    public bool Build(Sprite[] _digitSprites)
    {
        bReady = false;

        if (null == _digitSprites || 10 > _digitSprites.Length)
        {
            return false;
        }

        for (int d = 0; d < 10; d++)
        {
            if (false == HUD_BuffPixelUtil.TryGetSpritePixels(_digitSprites[d], out Color32[] _pixels, out int _w, out int _h))
            {
                return false;
            }

            int _first = _w;
            int _last = -1;

            for (int x = 0; x < _w; x++)
            {
                for (int y = 0; y < _h; y++)
                {
                    if (0 < _pixels[y * _w + x].a)
                    {
                        if (x < _first)
                        {
                            _first = x;
                        }

                        _last = x;
                        break;
                    }
                }
            }

            if (_last < _first)
            {
                return false;
            }

            int _gw = _last - _first + 1;
            Color32[] _glyph = new Color32[_gw * GlyphHeight];

            for (int y = 0; y < GlyphHeight && y < _h; y++)
            {
                for (int x = 0; x < _gw; x++)
                {
                    _glyph[y * _gw + x] = _pixels[y * _w + _first + x];
                }
            }

            digitPixels[d] = _glyph;
            digitWidth[d] = _gw;
        }

        // 플러스 글리프: 흰 십자 3x3 + 검은 외곽(8방향 이웃). 8행 중 위에서 2~6행에 둔다.
        Color32 _white = new Color32(255, 255, 255, 255);
        Color32 _black = new Color32(0, 0, 0, 255);
        Color32 _clear = new Color32(0, 0, 0, 0);

        for (int ty = 0; ty < PlusSize; ty++)
        {
            for (int tx = 0; tx < PlusSize; tx++)
            {
                bool _bWhite = IsPlusWhite(tx, ty);
                bool _bNear = false;

                if (false == _bWhite)
                {
                    for (int dy = -1; dy <= 1 && false == _bNear; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (true == IsPlusWhite(tx + dx, ty + dy))
                            {
                                _bNear = true;
                                break;
                            }
                        }
                    }
                }

                int _row = 7 - (2 + ty);
                plusPixels[_row * PlusSize + tx] = true == _bWhite ? _white : (true == _bNear ? _black : _clear);
            }
        }

        bReady = true;
        return true;
    }

    // _dst(폭 _dstWidth x 8)에 숫자를 합성하고 실제 폭을 돌려준다. 아래에서 위 순서의 픽셀 배열. 3자리까지.
    public int Compose(int _value, bool _bPlusPrefix, Color32[] _dst, int _dstWidth)
    {
        System.Array.Clear(_dst, 0, _dst.Length);

        if (false == bReady)
        {
            return 0;
        }

        int _v = Mathf.Clamp(_value, 0, 999);
        int _count = 0;

        if (100 <= _v)
        {
            digitBuffer[_count++] = _v / 100;
            digitBuffer[_count++] = (_v / 10) % 10;
            digitBuffer[_count++] = _v % 10;
        }
        else if (10 <= _v)
        {
            digitBuffer[_count++] = _v / 10;
            digitBuffer[_count++] = _v % 10;
        }
        else
        {
            digitBuffer[_count++] = _v;
        }

        int _x = 0;

        if (true == _bPlusPrefix)
        {
            Blit(plusPixels, PlusSize, _dst, _dstWidth, _x);
            _x += PlusSize - 1;
        }

        for (int i = 0; i < _count; i++)
        {
            int _d = digitBuffer[i];
            Blit(digitPixels[_d], digitWidth[_d], _dst, _dstWidth, _x);
            _x += digitWidth[_d] - 1;
        }

        return _x + 1;
    }

    private static bool IsPlusWhite(int _x, int _y)
    {
        if (_x < 1 || 3 < _x || _y < 1 || 3 < _y)
        {
            return false;
        }

        return 2 == _x || 2 == _y;
    }

    private static void Blit(Color32[] _glyph, int _gw, Color32[] _dst, int _dstWidth, int _ox)
    {
        for (int y = 0; y < GlyphHeight; y++)
        {
            for (int x = 0; x < _gw; x++)
            {
                int _dx = _ox + x;

                if (_dstWidth <= _dx)
                {
                    continue;
                }

                Color32 _p = _glyph[y * _gw + x];

                if (0 == _p.a)
                {
                    continue;
                }

                Color32 _cur = _dst[y * _dstWidth + _dx];

                // 이미 흰 획이 그려진 칸은 건드리지 않는다(검은 외곽선만 겹침 허용)
                if (0 < _cur.a && 255 == _cur.r)
                {
                    continue;
                }

                _dst[y * _dstWidth + _dx] = _p;
            }
        }
    }
}
