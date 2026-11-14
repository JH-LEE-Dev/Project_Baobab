using UnityEngine;

namespace PresentationLayer.VFX
{
    // 도형은 픽셀 캔버스, 글자는 팝 구간에만 서브픽셀 스케일을 허용한다.
    public class TopgradePopup : MonoBehaviour
    {
        private const int Width = 48;
        private const int Height = 48;
        private const float Duration = 0.55f;
        private const float GlyphInterval = 0.04f;
        private const float GlyphFadeInDuration = 0.5f / 6f;
        private const float GlyphSettleDuration = 0.2f;
        private readonly Color32[] pixels = new Color32[Width * Height];
        private SpriteRenderer timesRenderer;
        private SpriteRenderer twoRenderer;
        private int timesWidth;
        private int glyphWidth;
        private int glyphHeight;
        private Texture2D texture;
        private Sprite sprite;
        private SpriteRenderer spriteRenderer;
        private float age;
        private bool playing;
        private bool showCard;
        private float cardAngle;



        public void Initialize(Sprite _times, Sprite _two)
        {
            BuildGlyphs(_times, _two);
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.name = "TopgradePopupPixels";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingLayerName = "FlyingItem";
            spriteRenderer.sortingOrder = 3;
            gameObject.SetActive(false);
        }

        public void Play(Vector3 _position, bool _showCard = true)
        {
            transform.position = new Vector3(Mathf.Round(_position.x * 32f) / 32f, Mathf.Round(_position.y * 32f) / 32f, _position.z);
            age = 0f;
            showCard = _showCard;
            cardAngle = Random.Range(0f, 360f);
            playing = true;
            gameObject.SetActive(true);
            Animate();
        }

        public void Clear()
        {
            playing = false;
            gameObject.SetActive(false);
        }

        private void BuildGlyphs(Sprite _times, Sprite _two)
        {
            if (!HUD_BuffPixelUtil.TryGetSpritePixels(_times, out Color32[] times, out int timesWidth, out int timesHeight)
                || !HUD_BuffPixelUtil.TryGetSpritePixels(_two, out Color32[] two, out int twoWidth, out int twoHeight)) return;

            RectInt timesBounds = GetInkBounds(times, timesWidth, timesHeight);
            RectInt twoBounds = GetInkBounds(two, twoWidth, twoHeight);
            glyphWidth = timesBounds.width + twoBounds.width - 1;
            glyphHeight = Mathf.Max(timesBounds.height, twoBounds.height);
            timesRenderer = CreateGlyph("Times", times, timesWidth, timesBounds);
            twoRenderer = CreateGlyph("Two", two, twoWidth, twoBounds);
            this.timesWidth = timesBounds.width;
        }

        private static RectInt GetInkBounds(Color32[] _pixels, int _width, int _height)
        {
            int minX = _width;
            int minY = _height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_pixels[y * _width + x].a == 0) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            return maxX < 0 ? new RectInt(0, 0, 1, 1) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void Animate()
        {
            System.Array.Clear(pixels, 0, pixels.Length);
            if (showCard) DrawCard();
            float left = Mathf.Floor(-glyphWidth * 0.5f);
            float bottom = Mathf.Floor(-glyphHeight * 0.5f);
            AnimateGlyph(timesRenderer, left, bottom, 0f);
            AnimateGlyph(twoRenderer, left + timesWidth - 1, bottom, GlyphInterval);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            spriteRenderer.color = Color.white;
        }

        private void DrawCard()
        {
            float remaining = 1f - Mathf.Clamp01(age / Duration);
            float scale = Mathf.Lerp(0.2f, 1.1f, 1f - remaining * remaining * remaining);
            float halfSize = (Mathf.Sqrt(glyphWidth * glyphWidth + glyphHeight * glyphHeight) * 0.5f + 1.5f) * scale;
            byte alpha = (byte)Mathf.RoundToInt(255f * (1f - Mathf.InverseLerp(0.47f, Duration, age)));
            Color32 color = new Color32(255, 112, 104, alpha);
            float cos = Mathf.Cos(cardAngle * Mathf.Deg2Rad);
            float sin = Mathf.Sin(cardAngle * Mathf.Deg2Rad);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float dx = x + 0.5f - Width * 0.5f;
                    float dy = y + 0.5f - (Height * 0.5f - 1f);
                    float localX = dx * cos + dy * sin;
                    float localY = -dx * sin + dy * cos;
                    if (Mathf.Abs(localX) <= halfSize && Mathf.Abs(localY) <= halfSize)
                        pixels[y * Width + x] = color;
                }
            }
        }

        private SpriteRenderer CreateGlyph(string _name, Color32[] _pixels, int _stride, RectInt _bounds)
        {
            Texture2D glyphTexture = new Texture2D(_bounds.width, _bounds.height, TextureFormat.RGBA32, false);
            glyphTexture.name = "Topgrade" + _name;
            glyphTexture.filterMode = FilterMode.Point;
            glyphTexture.wrapMode = TextureWrapMode.Clamp;
            Color32[] cropped = new Color32[_bounds.width * _bounds.height];
            for (int y = 0; y < _bounds.height; y++)
                for (int x = 0; x < _bounds.width; x++)
                    cropped[y * _bounds.width + x] = _pixels[(y + _bounds.y) * _stride + x + _bounds.x];
            glyphTexture.SetPixels32(cropped);
            glyphTexture.Apply(false, true);
            GameObject child = new GameObject(_name);
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(glyphTexture, new Rect(0, 0, _bounds.width, _bounds.height), Vector2.zero, 32f, 0, SpriteMeshType.FullRect);
            renderer.sortingLayerName = "FlyingItem";
            renderer.sortingOrder = 4;
            renderer.enabled = false;
            return renderer;
        }

        private void AnimateGlyph(SpriteRenderer _renderer, float _left, float _bottom, float _delay)
        {
            if (_renderer == null) return;
            _renderer.enabled = age >= _delay;
            if (!_renderer.enabled) return;
            float elapsed = age - _delay;
            float exitStart = 0.4f + _delay;
            float exitProgress = Mathf.InverseLerp(exitStart, exitStart + 0.1f, age);
            float alpha = Mathf.Clamp01(elapsed / GlyphFadeInDuration)
                * (1f - Mathf.InverseLerp(exitStart + 0.05f, exitStart + 0.1f, age));
            _renderer.color = new Color(1f, 1f, 1f, alpha);
            float remaining = 1f - Mathf.Clamp01(elapsed / GlyphSettleDuration);
            float scale = 1f + remaining * remaining * remaining;
            scale *= 1f - Mathf.SmoothStep(0f, 1f, exitProgress);
            Vector2 size = _renderer.sprite.rect.size;
            Vector3 position = new Vector3(
                (_left - size.x * (scale - 1f) * 0.5f) / 32f,
                (_bottom - size.y * (scale - 1f) * 0.5f) / 32f, 0f);
            if (elapsed >= GlyphSettleDuration && age <= exitStart)
            {
                scale = 1f;
                // 원본 크기에 도착한 순간 월드 좌표 기준으로 정확히 픽셀 스냅한다.
                Vector3 world = transform.TransformPoint(position);
                world.x = Mathf.Round(world.x * 32f) / 32f;
                world.y = Mathf.Round(world.y * 32f) / 32f;
                position = transform.InverseTransformPoint(world);
            }
            _renderer.transform.localPosition = position;
            _renderer.transform.localScale = Vector3.one * scale;
        }

        private void DestroyGlyph(SpriteRenderer _renderer)
        {
            if (_renderer == null || _renderer.sprite == null) return;
            Destroy(_renderer.sprite.texture);
            Destroy(_renderer.sprite);
        }

        private void Update()
        {
            if (!playing) return;
            age += Time.deltaTime;
            if (age >= Duration) Clear();
            else Animate();
        }

        private void OnDestroy()
        {
            DestroyGlyph(timesRenderer);
            DestroyGlyph(twoRenderer);
            Destroy(sprite);
            Destroy(texture);
        }
    }
}
