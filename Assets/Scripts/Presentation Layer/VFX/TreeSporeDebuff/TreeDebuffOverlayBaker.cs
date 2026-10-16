using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 포자 디버프의 "정적 부분"을 스프라이트로 미리 구워 둔다. 런타임에 나무마다 쿼드를 만들지 않고 스프라이트 렌더러 한 장으로 끝내기 위해서다.
    ///  - 나무 오버레이: 나무 스프라이트와 같은 크기/피벗. 어두운 체커 디더(나무 색을 바꾸지 않고 "탁한 느낌"만 더함), 각진 얼룩, 줄무늬 몸통, 균열 선.
    ///  - 바닥 링: 단계별 1장. 점선 링과 안쪽 체커 디더 면.
    /// 스프라이트(나무 스프라이트, 단계, 변형)마다 처음 쓸 때 한 번 만들고 캐시한다(나무끼리 공유). 텍스처는 점 필터, 읽기 불가로 올린다.
    /// </summary>
    public static class TreeDebuffOverlayBaker
    {
        private const float PixelsPerUnit = 32.0f;

        private static readonly Color32 DitherColor = new Color32(24, 16, 36, 100);
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        private static readonly Dictionary<Sprite, Sprite[]> overlayCache = new Dictionary<Sprite, Sprite[]>(64);
        private static readonly Sprite[] groundSprites = new Sprite[4];

        public static Sprite GetOverlay(Sprite _treeSprite, TreeSilhouetteData _mask, TreeDebuffLayout _layout, int _tier, int _variant)
        {
            Sprite[] slots;
            if (false == overlayCache.TryGetValue(_treeSprite, out slots))
            {
                slots = new Sprite[8];
                overlayCache[_treeSprite] = slots;
            }

            int slot = (_tier - 1) * 2 + (_variant & 1);
            Sprite sprite = slots[slot];
            if (null != sprite) return sprite;

            int w = _mask.width;
            int h = _mask.height;
            Color32[] pixels = new Color32[w * h];
            Color32 deep = TreeDebuffPalette.DeepRaw(_tier);
            Color32 mid = TreeDebuffPalette.MidRaw(_tier);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    pixels[i] = (true == _mask.solid[i] && 0 == ((x + y) & 1)) ? DitherColor : Clear;
                }
            }

            for (int s = 0; s < _layout.patchSpanCount; s++)
            {
                int y = _layout.patchY[s];
                for (int x = _layout.patchX0[s]; x <= _layout.patchX1[s]; x++)
                {
                    if (false == _mask.IsSolid(x, y)) continue;
                    if (0 == ((x + y) & 1)) pixels[y * w + x] = deep;
                    else if (0 == (((x * 3 + y * 5) % 7 + 7) % 7)) pixels[y * w + x] = mid;
                }
            }

            for (int s = 0; s < _layout.streakCount; s++)
            {
                int len = _layout.streakLength[s];
                for (int k = 0; k < len; k++)
                {
                    int x = _layout.streakX[s];
                    int y = _layout.streakTopY[s] - k;
                    if (false == _mask.IsSolid(x, y)) break;
                    pixels[y * w + x] = mid;
                    if (k < len * 0.6f && true == _mask.IsSolid(x + 1, y)) pixels[y * w + x + 1] = deep;
                }
            }

            for (int c = 0; c < _layout.crackCount; c++)
            {
                for (int p = 0; p < _layout.crackPixelCount[c]; p++)
                {
                    pixels[_layout.crackY[c, p] * w + _layout.crackX[c, p]] = deep;
                }
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.name = "TreeDebuffOverlay_" + _treeSprite.name + "_T" + _tier + "_V" + _variant;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Vector2 pivot = new Vector2(_mask.pivot.x / w, _mask.pivot.y / h);
            sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            slots[slot] = sprite;
            return sprite;
        }

        // 바닥 링 스프라이트: 중앙 피벗. 링 점(점선)은 기본색, 안쪽은 체커 디더.
        public static Sprite GetGround(int _tier)
        {
            if (null != groundSprites[_tier]) return groundSprites[_tier];

            int rx = TreeDebuffRing.RadiusX[_tier];
            int ry = TreeDebuffRing.RadiusY(_tier);
            int w = rx * 2 + 3;
            int h = ry * 2 + 3;
            int cx = rx + 1;
            int cy = ry + 1;
            Color32[] pixels = new Color32[w * h];
            Color32 deep = TreeDebuffPalette.DeepRaw(_tier);
            Color32 mid = TreeDebuffPalette.MidRaw(_tier);

            for (int dy = -ry; dy <= ry; dy++)
            {
                for (int dx = -rx; dx <= rx; dx++)
                {
                    float n = (dx * dx) / (float)(rx * rx) + (dy * dy) / (float)(ry * ry);
                    if (1.0f < n) continue;
                    int x = cx + dx;
                    int y = cy + dy;

                    if (0.62f < n)
                    {
                        if (0 == ((x + y) & 1)) pixels[y * w + x] = deep;
                    }
                    else if (0.3f < n)
                    {
                        if (0 == (x & 1) && 0 == (y & 1)) pixels[y * w + x] = deep;
                    }
                }
            }

            int count = TreeDebuffRing.PointCount(_tier);
            for (int i = 0; i < count; i++)
            {
                if (true == TreeDebuffRing.IsGap(i)) continue;
                pixels[(cy + TreeDebuffRing.Y(_tier, i)) * w + cx + TreeDebuffRing.X(_tier, i)] = mid;
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.name = "TreeDebuffGround_T" + _tier;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            groundSprites[_tier] = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(cx / (float)w, cy / (float)h), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            groundSprites[_tier].name = texture.name;
            return groundSprites[_tier];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            overlayCache.Clear();
            for (int i = 0; i < groundSprites.Length; i++) groundSprites[i] = null;
        }
    }

    /// <summary>단계별 3톤 팔레트(독성 초록 / 암자색 / 녹슨 주황). 나무 색과 무관하게 항상 같은 색을 쓴다.</summary>
    public static class TreeDebuffPalette
    {
        // deep, mid, light 순서(단계 1~3)
        private static readonly byte[] Bytes =
        {
            59, 90, 20, 166, 210, 58, 230, 255, 138,
            59, 20, 88, 176, 79, 224, 241, 198, 255,
            92, 42, 8, 240, 138, 28, 255, 224, 138,
        };

        public static Color32 DeepRaw(int _tier) { return Raw(_tier, 0); }
        public static Color32 MidRaw(int _tier) { return Raw(_tier, 1); }
        public static Color32 LightRaw(int _tier) { return Raw(_tier, 2); }

        private static Color32 Raw(int _tier, int _tone)
        {
            int b = ((_tier - 1) * 3 + _tone) * 3;
            return new Color32(Bytes[b], Bytes[b + 1], Bytes[b + 2], 255);
        }

        /// <summary>정점 색용: 선형 색 공간이면 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.</summary>
        public static Color32 Vertex(int _tier, int _tone)
        {
            Color32 raw = Raw(_tier, _tone);
            Color color = new Color(raw.r / 255.0f, raw.g / 255.0f, raw.b / 255.0f, 1.0f);
            if (ColorSpace.Linear == QualitySettings.activeColorSpace) color = color.linear;
            return (Color32)color;
        }

        public static Color32 VertexOutline()
        {
            Color color = new Color(16.0f / 255.0f, 32.0f / 255.0f, 28.0f / 255.0f, 1.0f);
            if (ColorSpace.Linear == QualitySettings.activeColorSpace) color = color.linear;
            return (Color32)color;
        }
    }
}
