using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 나무 스프라이트 한 장의 불투명 픽셀 마스크와 경계. 포자 디버프 이펙트가 반점, 줄무늬, 균열, 독액 위치를 "실제 나무 모양"에서 고르는 데 쓴다.
    /// 좌표는 스프라이트 텍스처 행 순서 그대로(아래 행이 0, 위쪽이 양수)다.
    /// 텍스처가 읽기 가능(Read/Write)하지 않아도 되도록 GPU에서 작은 RenderTexture로 복사해서 읽는다(OverheatSilhouette와 같은 방식).
    /// 스프라이트마다 처음 쓸 때 한 번만 만들고 캐시한다. 읽기용 텍스처는 크기별로 재사용한다.
    /// </summary>
    public sealed class TreeSilhouetteData
    {
        public int width;
        public int height;
        public bool[] solid;
        public int minX;
        public int maxX;
        public int minY;
        public int maxY;
        public Vector2 pivot;

        public bool IsSolid(int _x, int _y)
        {
            if (_x < 0 || _y < 0 || width <= _x || height <= _y) return false;
            return solid[_y * width + _x];
        }
    }

    public static class TreeSilhouetteMask
    {
        private const byte SolidAlphaThreshold = 127;
        private const int MaxReadbackSize = 256;

        private static readonly Dictionary<Sprite, TreeSilhouetteData> cache = new Dictionary<Sprite, TreeSilhouetteData>(48);
        private static readonly Dictionary<int, Texture2D> readbackTextures = new Dictionary<int, Texture2D>(4);

        public static TreeSilhouetteData Get(Sprite _sprite)
        {
            if (null == _sprite) return null;

            TreeSilhouetteData data;
            if (true == cache.TryGetValue(_sprite, out data)) return data;

            Rect rect = _sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            Texture texture = _sprite.texture;

            data = new TreeSilhouetteData();
            data.width = width;
            data.height = height;
            data.solid = new bool[Mathf.Max(1, width * height)];
            data.pivot = _sprite.pivot;
            data.minX = width;
            data.minY = height;
            data.maxX = -1;
            data.maxY = -1;

            if (null != texture && 0 < width && 0 < height && MaxReadbackSize >= width && MaxReadbackSize >= height)
            {
                RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Vector2 scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
                Vector2 offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
                Graphics.Blit(texture, temp, scale, offset);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = temp;

                Texture2D readback = GetReadbackTexture(width, height);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                RenderTexture.active = previous;

                NativeArray<Color32> pixels = readback.GetRawTextureData<Color32>();
                for (int i = 0; i < pixels.Length; i++)
                {
                    bool bSolid = SolidAlphaThreshold < pixels[i].a;
                    data.solid[i] = bSolid;

                    if (true == bSolid)
                    {
                        int x = i % width;
                        int y = i / width;
                        if (x < data.minX) data.minX = x;
                        if (data.maxX < x) data.maxX = x;
                        if (y < data.minY) data.minY = y;
                        if (data.maxY < y) data.maxY = y;
                    }
                }

                RenderTexture.ReleaseTemporary(temp);
            }

            cache[_sprite] = data;
            return data;
        }

        private static Texture2D GetReadbackTexture(int _width, int _height)
        {
            int key = _width * (MaxReadbackSize + 1) + _height;
            Texture2D texture;
            if (false == readbackTextures.TryGetValue(key, out texture) || null == texture)
            {
                texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                readbackTextures[key] = texture;
            }

            return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
            readbackTextures.Clear();
        }
    }
}
