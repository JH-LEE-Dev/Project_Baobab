using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 과열 게이지의 숫자("100", "x1.00"). SmallCurrencyNum 시트(8px 높이, 흰 획 + 검은 외곽선)의 숫자 · 점 · x 글리프를
    /// RawImage 하나에 합성한다. 글자 폭만 잘라 쓰고 글자 사이 검은 외곽선 1px만 겹친다(HUD_BuffNumberFont와 같은 규칙).
    /// HUD_BuffNumberFont는 숫자만 지원하고 여러 HUD가 함께 쓰므로 건드리지 않고 여기서 따로 합성한다.
    /// 이 컴포넌트가 붙은 RectTransform이 정렬 기준점이고, 글자 이미지(image)는 그 자식이다. 위치는 항상 정수 픽셀이다.
    /// </summary>
    public class HUD_HeatNumber : MonoBehaviour
    {
        public enum EAlign { Left, Center, Right }

        // 시트 순서: _0~_8이 1~9, _9가 0, _10이 '.', _11이 'x'. glyphSprites는 0~9 숫자 순서 + 점 + x로 넣는다.
        public const int GlyphDot = 10;
        public const int GlyphX = 11;
        public const int GlyphCount = 12;

        private const int TexWidth = 48;
        private const int Height = HUD_BuffNumberFont.GlyphHeight;
        private const int MaxChars = 8;

        // //외부 의존성
        [SerializeField] private RawImage image;
        [SerializeField, Tooltip("인덱스 0~9가 숫자 0~9, 10이 '.', 11이 'x'.")]
        private Sprite[] glyphSprites = new Sprite[GlyphCount];
        [SerializeField, Tooltip("기준점(이 오브젝트 위치)에서 숫자를 어느 쪽으로 늘릴지. Right면 자릿수가 늘어도 오른쪽 끝이 고정된다.")]
        private EAlign align = EAlign.Center;

        // //내부 의존성
        private readonly Color32[][] glyphPixels = new Color32[GlyphCount][];
        private readonly int[] glyphWidth = new int[GlyphCount];
        private readonly int[] charBuffer = new int[MaxChars];
        private Texture2D texture;
        private Color32[] buffer;

        // //상태 변수
        private bool bReady = false;
        private int contentWidth = 0;
        private int shownKey = int.MinValue; // 정수는 값 그대로, 배율은 -1 - 백분율로 구분해 같은 값이면 다시 합성하지 않는다

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            EnsureReady();
        }

        /// <summary>정수 하나를 그린다(0~999).</summary>
        public void SetInt(int _value)
        {
            int _v = Mathf.Clamp(_value, 0, 999);

            if (_v == shownKey)
                return;

            shownKey = _v;
            int _count = AppendDigits(_v, 0);
            Rebuild(_count);
        }

        /// <summary>"x정수.소수 둘째 자리"를 그린다. _hundredths는 배율 × 100 (예: 1.00 → 100).</summary>
        public void SetMultiplier(int _hundredths)
        {
            int _v = Mathf.Clamp(_hundredths, 0, 99999);
            int _key = -1 - _v;

            if (_key == shownKey)
                return;

            shownKey = _key;

            int _count = 0;
            charBuffer[_count++] = GlyphX;
            _count = AppendDigits(_v / 100, _count);
            charBuffer[_count++] = GlyphDot;
            charBuffer[_count++] = (_v / 10) % 10;
            charBuffer[_count++] = _v % 10;
            Rebuild(_count);
        }

        /// <summary>글자 색 곱(흰 획만 물든다, 검은 외곽선은 그대로).</summary>
        public void SetTint(Color _color)
        {
            if (null != image)
                image.color = _color;
        }

        // //일반 비즈니스 로직 및 내부 메서드

        private int AppendDigits(int _value, int _start)
        {
            int _count = _start;

            if (100 <= _value)
            {
                charBuffer[_count++] = _value / 100;
                charBuffer[_count++] = (_value / 10) % 10;
                charBuffer[_count++] = _value % 10;
            }
            else if (10 <= _value)
            {
                charBuffer[_count++] = _value / 10;
                charBuffer[_count++] = _value % 10;
            }
            else
            {
                charBuffer[_count++] = _value;
            }

            return _count;
        }

        private bool EnsureReady()
        {
            if (true == bReady)
                return true;

            if (null == image || null == glyphSprites || GlyphCount > glyphSprites.Length)
                return false;

            for (int g = 0; g < GlyphCount; g++)
            {
                if (false == ExtractGlyph(g))
                    return false;
            }

            texture = new Texture2D(TexWidth, Height, TextureFormat.RGBA32, false);
            texture.name = "HUD_HeatNumber";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            buffer = new Color32[TexWidth * Height];

            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = false;

            bReady = true;
            return true;
        }

        // 8x8 셀에서 칠해진 열만 잘라 둔다(아래에서 위 순서의 픽셀).
        private bool ExtractGlyph(int _index)
        {
            if (false == HUD_BuffPixelUtil.TryGetSpritePixels(glyphSprites[_index], out Color32[] _pixels, out int _w, out int _h))
                return false;

            int _first = _w;
            int _last = -1;

            for (int x = 0; x < _w; x++)
            {
                for (int y = 0; y < _h; y++)
                {
                    if (0 < _pixels[y * _w + x].a)
                    {
                        if (x < _first)
                            _first = x;

                        _last = x;
                        break;
                    }
                }
            }

            if (_last < _first)
                return false;

            int _gw = _last - _first + 1;
            Color32[] _glyph = new Color32[_gw * Height];

            for (int y = 0; y < Height && y < _h; y++)
            {
                for (int x = 0; x < _gw; x++)
                {
                    _glyph[y * _gw + x] = _pixels[y * _w + _first + x];
                }
            }

            glyphPixels[_index] = _glyph;
            glyphWidth[_index] = _gw;
            return true;
        }

        private void Rebuild(int _count)
        {
            if (false == EnsureReady())
                return;

            System.Array.Clear(buffer, 0, buffer.Length);

            int _x = 0;

            for (int i = 0; i < _count; i++)
            {
                int _g = charBuffer[i];
                Blit(glyphPixels[_g], glyphWidth[_g], _x);
                _x += glyphWidth[_g] - 1;
            }

            contentWidth = Mathf.Clamp(_x + 1, 1, TexWidth);

            texture.SetPixelData(buffer, 0);
            texture.Apply(false);
            ApplyRect();
        }

        // 이미 흰 획이 있는 칸은 건드리지 않는다(검은 외곽선만 겹침 허용).
        private void Blit(Color32[] _glyph, int _gw, int _ox)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < _gw; x++)
                {
                    int _dx = _ox + x;

                    if (TexWidth <= _dx)
                        continue;

                    Color32 _p = _glyph[y * _gw + x];

                    if (0 == _p.a)
                        continue;

                    Color32 _cur = buffer[y * TexWidth + _dx];

                    if (0 < _cur.a && 255 == _cur.r)
                        continue;

                    buffer[y * TexWidth + _dx] = _p;
                }
            }
        }

        // 폭이 홀수여도 반 칸 어긋나지 않게 pivot을 왼쪽에 두고 정수 칸만큼 민다.
        private void ApplyRect()
        {
            image.enabled = true;
            image.uvRect = new Rect(0f, 0f, contentWidth / (float)TexWidth, 1f);

            RectTransform _rect = image.rectTransform;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0f, 0.5f);
            _rect.sizeDelta = new Vector2(contentWidth, Height);

            int _anchorX = -(contentWidth / 2);

            if (EAlign.Left == align)
                _anchorX = 0;
            else if (EAlign.Right == align)
                _anchorX = -contentWidth;

            _rect.anchoredPosition = new Vector2(_anchorX, 0f);
        }

        // //유니티 라이프사이클 이벤트 함수

        private void OnDestroy()
        {
            if (null != texture)
                Destroy(texture);
        }
    }
}
