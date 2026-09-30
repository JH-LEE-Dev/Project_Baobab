using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// VFX_OverheatAura가 몸을 감싸는 아우라를 그리는 데 쓰는 "실루엣 격자". 대상(캐릭터, 드론)의 스프라이트 렌더러들을 각자의 Transform
    /// (위치, 회전, 스케일, 반전) 그대로 합쳐서 64x64 픽셀 격자의 불투명 마스크로 만들고, 마스크 바깥 각 칸이 실루엣에서 얼마나 떨어졌는지
    /// (3-4 챔퍼 거리)와 실루엣 가장자리 칸 목록을 계산한다. 아우라 컴포넌트에서 이 계산을 떼어 낸 것이다.
    ///
    /// 성능: 팔이 회전하는 동안에는 프레임마다 다시 계산하므로 다음과 같이 한다.
    /// - 지난번 실루엣이 차지했던 사각형만 지우고, 새 실루엣의 경계 사각형(+아우라가 뻗을 수 있는 여백)에서만 거리 변환과 가장자리 스캔을 한다.
    ///   격자 전체(4,096칸)를 돌던 것이 캐릭터 기준 약 1,200칸으로 줄어든다. 여백 밖 칸의 거리는 갱신하지 않으므로 호출부는 반드시
    ///   RegionMin/Max 안쪽 칸만 읽어야 한다.
    /// - 칸마다 행렬 곱 메서드를 부르는 대신 같은 식(MultiplyPoint3x4와 같은 연산 순서)을 인라인해서 결과가 비트 단위로 같다.
    /// - 스프라이트 마스크를 처음 만들 때 쓰는 읽기용 Texture2D는 크기별로 재사용하고, GetPixels32 배열 대신 NativeArray 뷰를 쓴다.
    /// </summary>
    public sealed class OverheatSilhouette
    {
        // 실루엣 격자: 발밑 피벗 기준 x -32..31, y -16..47 (64x64). 팔을 뻗거나 도끼를 휘두를 때도 잘리지 않도록 여유를 둔다.
        public const int GridWidth = 64;
        public const int GridHeight = 64;
        public const int GridOriginX = 32;
        public const int GridOriginY = 16;
        public const int DistanceInfinity = 100000;
        public const int DistanceUnitsPerPixel = 3; // 3-4 챔퍼 거리 변환(직교 3, 대각 4)

        private const int MaxSources = 16;
        private const int MaxReadbackSize = 512;
        private const byte SolidAlphaThreshold = 76;
        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / PixelsPerUnit;

        // 스프라이트 한 장의 불투명 픽셀 마스크(처음 쓸 때만 만들어 캐시한다)
        private sealed class SpriteMaskData
        {
            public int width;
            public int height;
            public bool[] solid;
        }

        // 스프라이트 마스크 캐시와 읽기용 텍스처는 인스턴스끼리 공유한다(메인 스레드 단일 실행)
        private static readonly Dictionary<Sprite, SpriteMaskData> spriteMaskCache = new Dictionary<Sprite, SpriteMaskData>(64);
        private static readonly Dictionary<int, Texture2D> readbackTextures = new Dictionary<int, Texture2D>(8);
        private static readonly List<SpriteRenderer> collectBuffer = new List<SpriteRenderer>(32);

        private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>(MaxSources);
        private readonly bool[] solid = new bool[GridWidth * GridHeight];
        private readonly int[] distance = new int[GridWidth * GridHeight];
        private readonly int[] edgeCells = new int[GridWidth * GridHeight];
        private readonly bool[] edgeMask = new bool[GridWidth * GridHeight]; // edgeCells에 든 칸 표시
        private readonly float[] cellWorldX = new float[GridWidth];          // 칸 중심의 월드 X(원점 기준, 빌드마다 한 번 계산)

        // 지난 빌드에서 solid가 채워졌던 경계 사각형(다음 빌드에서 이 범위만 지운다)
        private int previousMinX;
        private int previousMaxX = -1;
        private int previousMinY;
        private int previousMaxY = -1;

        public bool[] Solid => solid;
        public int[] Distance => distance;
        public int[] EdgeCells => edgeCells;
        public bool[] EdgeMask => edgeMask;
        public int EdgeCellCount { get; private set; }

        /// <summary>
        /// 실루엣이 하나라도 있는지. false면 Region 값은 의미가 없다.
        /// </summary>
        public bool HasSolid { get; private set; }

        // 거리 변환을 계산한 영역(격자 좌표, 양끝 포함). 아우라가 뻗을 수 있는 여백까지 포함한다.
        public int RegionMinX { get; private set; }
        public int RegionMaxX { get; private set; }
        public int RegionMinY { get; private set; }
        public int RegionMaxY { get; private set; }

        /// <summary>
        /// _root 아래의 _sortingLayerName 레이어 스프라이트 렌더러들을 소스로 모은다(몸, 얼굴, 도끼, 드론 본체 등).
        /// 그림자/사거리 표시/물 위 스프라이트는 레이어가 달라 제외된다. 정적 리스트를 재사용해서 할당하지 않는다.
        /// </summary>
        public void CollectSources(Transform _root, string _sortingLayerName)
        {
            sources.Clear();
            if (null == _root) return;

            _root.GetComponentsInChildren<SpriteRenderer>(true, collectBuffer);
            for (int i = 0; i < collectBuffer.Count && sources.Count < MaxSources; i++)
            {
                if (collectBuffer[i].sortingLayerName != _sortingLayerName) continue;
                sources.Add(collectBuffer[i]);
            }

            collectBuffer.Clear();
        }

        private static bool IsSourceActive(SpriteRenderer _source)
        {
            return null != _source && true == _source.enabled && true == _source.gameObject.activeInHierarchy && null != _source.sprite;
        }

        /// <summary>
        /// 켜져 있는 첫 소스 스프라이트의 정확한 월드 위치. 켜진 소스가 없으면 _fallback.
        /// </summary>
        public Vector3 GetFirstSourcePosition(Vector3 _fallback)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                if (true == IsSourceActive(sources[i])) return sources[i].transform.position;
            }

            return _fallback;
        }

        /// <summary>
        /// 켜져 있는 소스 스프라이트들의 상태(스프라이트, 반전, 원점 기준 위치, 회전, 스케일)를 하나의 키로 묶는다. 키가 바뀌면 Build를 다시 한다.
        /// 팔과 도끼는 조준/휘두르기로 회전하고 좌우 반전은 localScale로 처리되므로 회전과 스케일도 포함한다(각도는 1도 단위로 양자화).
        /// Transform의 네이티브 속성 여러 개(position, lossyScale, eulerAngles) 대신 localToWorldMatrix 하나로 읽는다.
        /// </summary>
        public int ComputeKey(Vector3 _origin)
        {
            int key = 17;

            for (int i = 0; i < sources.Count; i++)
            {
                SpriteRenderer source = sources[i];
                if (false == IsSourceActive(source)) continue;

                Matrix4x4 matrix = source.transform.localToWorldMatrix;
                int offsetX = Mathf.RoundToInt((matrix.m03 - _origin.x) * PixelsPerUnit);
                int offsetY = Mathf.RoundToInt((matrix.m13 - _origin.y) * PixelsPerUnit);
                int angle = Mathf.RoundToInt(Mathf.Atan2(matrix.m10, matrix.m00) * Mathf.Rad2Deg);
                int scaleX = Mathf.RoundToInt(Mathf.Sqrt(matrix.m00 * matrix.m00 + matrix.m10 * matrix.m10) * 8.0f);
                int scaleY = Mathf.RoundToInt(Mathf.Sqrt(matrix.m01 * matrix.m01 + matrix.m11 * matrix.m11) * 8.0f);
                bool bMirrored = matrix.m00 * matrix.m11 - matrix.m01 * matrix.m10 < 0.0f;

                key = unchecked(key * 31 + source.sprite.GetHashCode());
                key = unchecked(key * 31 + (true == source.flipX ? 1 : 0) + (true == source.flipY ? 2 : 0) + (true == bMirrored ? 4 : 0));
                key = unchecked(key * 31 + offsetX);
                key = unchecked(key * 31 + offsetY);
                key = unchecked(key * 31 + angle);
                key = unchecked(key * 31 + scaleX);
                key = unchecked(key * 31 + scaleY);
            }

            return key;
        }

        // 스프라이트의 불투명 픽셀 마스크. 텍스처가 읽기 가능(Read/Write)하지 않아도 되도록 GPU에서 작은 RenderTexture로 복사해서 읽는다.
        // 처음 쓰는 스프라이트에서만 실행되고, 이후에는 캐시를 쓴다.
        private static SpriteMaskData GetSpriteMask(Sprite _sprite)
        {
            SpriteMaskData data;
            if (true == spriteMaskCache.TryGetValue(_sprite, out data)) return data;

            Rect rect = _sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            Texture texture = _sprite.texture;

            data = new SpriteMaskData();
            data.width = width;
            data.height = height;
            data.solid = new bool[width * height];

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

                // ReadPixels는 CPU 쪽 복사본을 곧바로 채우므로 GPU로 다시 올리는 Apply는 필요 없다
                NativeArray<Color32> pixels = readback.GetRawTextureData<Color32>();
                for (int i = 0; i < pixels.Length; i++)
                {
                    data.solid[i] = SolidAlphaThreshold < pixels[i].a;
                }

                RenderTexture.ReleaseTemporary(temp);
            }

            spriteMaskCache[_sprite] = data;
            return data;
        }

        // 크기별로 읽기용 텍스처를 하나씩만 만들어 재사용한다(스프라이트마다 만들고 파괴하던 것을 없앤다)
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

        /// <summary>
        /// 소스 스프라이트들을 합쳐 실루엣 격자, 거리장, 가장자리 목록을 다시 만든다. _reachMarginCells는 아우라가 실루엣에서 뻗을 수 있는 최대 거리(칸)로,
        /// 거리 변환은 실루엣 경계 사각형에 이 여백을 더한 영역에서만 계산한다.
        /// </summary>
        public void Build(Vector3 _origin, int _reachMarginCells)
        {
            ClearPrevious();

            // 역방향 샘플링: 격자 칸 중심(원점 기준)을 각 스프라이트 렌더러의 Transform 전체(위치, 회전, 스케일 - 음수 스케일 반전 포함)의
            // 역변환으로 스프라이트 픽셀 좌표로 바꿔 마스크를 조회한다. 팔과 도끼가 회전/반전되어도 구멍 없이 실루엣이 그대로 따라온다.
            for (int gx = 0; gx < GridWidth; gx++)
            {
                cellWorldX[gx] = _origin.x + (gx - GridOriginX + 0.5f) * PixelUnit;
            }

            int minX = GridWidth;
            int maxX = -1;
            int minY = GridHeight;
            int maxY = -1;

            for (int s = 0; s < sources.Count; s++)
            {
                SpriteRenderer source = sources[s];
                if (false == IsSourceActive(source)) continue;

                Sprite sprite = source.sprite;
                SpriteMaskData mask = GetSpriteMask(sprite);
                Transform sourceTransform = source.transform;
                Matrix4x4 worldToSource = sourceTransform.worldToLocalMatrix;
                float pixelsPerUnit = sprite.pixelsPerUnit;
                float pivotX = sprite.pivot.x;
                float pivotY = sprite.pivot.y;
                float flipSignX = true == source.flipX ? -1.0f : 1.0f;
                float flipSignY = true == source.flipY ? -1.0f : 1.0f;
                float sourceZ = sourceTransform.position.z;

                // MultiplyPoint3x4와 같은 항 순서로 x, y만 구한다(z 항은 소스마다 상수)
                float m00 = worldToSource.m00;
                float m01 = worldToSource.m01;
                float zTermX = worldToSource.m02 * sourceZ;
                float m03 = worldToSource.m03;
                float m10 = worldToSource.m10;
                float m11 = worldToSource.m11;
                float zTermY = worldToSource.m12 * sourceZ;
                float m13 = worldToSource.m13;

                // 스프라이트 사각형(로컬 경계)의 네 꼭짓점을 월드로 옮겨서 조회할 격자 범위를 좁힌다
                Bounds bounds = sprite.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                float minCellX = float.MaxValue;
                float maxCellX = float.MinValue;
                float minCellY = float.MaxValue;
                float maxCellY = float.MinValue;
                for (int c = 0; c < 4; c++)
                {
                    float cx = 0 == (c & 1) ? min.x : max.x;
                    float cy = c < 2 ? min.y : max.y;
                    Vector3 corner = sourceTransform.TransformPoint(new Vector3(flipSignX * cx, flipSignY * cy, 0.0f));
                    float cellX = (corner.x - _origin.x) * PixelsPerUnit;
                    float cellY = (corner.y - _origin.y) * PixelsPerUnit;
                    minCellX = Mathf.Min(minCellX, cellX);
                    maxCellX = Mathf.Max(maxCellX, cellX);
                    minCellY = Mathf.Min(minCellY, cellY);
                    maxCellY = Mathf.Max(maxCellY, cellY);
                }

                int startX = Mathf.Max(0, Mathf.FloorToInt(minCellX) + GridOriginX - 1);
                int endX = Mathf.Min(GridWidth - 1, Mathf.CeilToInt(maxCellX) + GridOriginX + 1);
                int startY = Mathf.Max(0, Mathf.FloorToInt(minCellY) + GridOriginY - 1);
                int endY = Mathf.Min(GridHeight - 1, Mathf.CeilToInt(maxCellY) + GridOriginY + 1);

                for (int gy = startY; gy <= endY; gy++)
                {
                    float worldY = _origin.y + (gy - GridOriginY + 0.5f) * PixelUnit;
                    int rowIndex = gy * GridWidth;

                    for (int gx = startX; gx <= endX; gx++)
                    {
                        float worldX = cellWorldX[gx];
                        float localX = m00 * worldX + m01 * worldY + zTermX + m03;
                        float localY = m10 * worldX + m11 * worldY + zTermY + m13;

                        int px = Mathf.FloorToInt(flipSignX * localX * pixelsPerUnit + pivotX);
                        int py = Mathf.FloorToInt(flipSignY * localY * pixelsPerUnit + pivotY);
                        if (0 > px || mask.width <= px || 0 > py || mask.height <= py) continue;

                        if (true == mask.solid[py * mask.width + px])
                        {
                            solid[rowIndex + gx] = true;
                            if (gx < minX) minX = gx;
                            if (gx > maxX) maxX = gx;
                            if (gy < minY) minY = gy;
                            if (gy > maxY) maxY = gy;
                        }
                    }
                }
            }

            HasSolid = maxX >= minX && maxY >= minY;
            previousMinX = HasSolid ? minX : 0;
            previousMaxX = HasSolid ? maxX : -1;
            previousMinY = HasSolid ? minY : 0;
            previousMaxY = HasSolid ? maxY : -1;

            if (false == HasSolid)
            {
                EdgeCellCount = 0;
                return;
            }

            RegionMinX = Mathf.Max(0, minX - _reachMarginCells);
            RegionMaxX = Mathf.Min(GridWidth - 1, maxX + _reachMarginCells);
            RegionMinY = Mathf.Max(0, minY - _reachMarginCells);
            RegionMaxY = Mathf.Min(GridHeight - 1, maxY + _reachMarginCells);

            BuildDistanceField();
            BuildEdgeList(minX, maxX, minY, maxY);
        }

        // 지난 빌드의 실루엣 사각형과 가장자리 표시만 지운다(격자 전체를 초기화하지 않는다)
        private void ClearPrevious()
        {
            for (int y = previousMinY; y <= previousMaxY; y++)
            {
                int rowIndex = y * GridWidth;
                for (int x = previousMinX; x <= previousMaxX; x++)
                {
                    solid[rowIndex + x] = false;
                }
            }

            for (int i = 0; i < EdgeCellCount; i++)
            {
                edgeMask[edgeCells[i]] = false;
            }

            EdgeCellCount = 0;
        }

        // 3-4 챔퍼 거리 변환: 실루엣에서 각 칸까지의 거리(직교 3, 대각 4). Region 안에서만 계산한다.
        private void BuildDistanceField()
        {
            for (int y = RegionMinY; y <= RegionMaxY; y++)
            {
                int rowIndex = y * GridWidth;
                for (int x = RegionMinX; x <= RegionMaxX; x++)
                {
                    distance[rowIndex + x] = true == solid[rowIndex + x] ? 0 : DistanceInfinity;
                }
            }

            for (int y = RegionMinY; y <= RegionMaxY; y++)
            {
                for (int x = RegionMinX; x <= RegionMaxX; x++)
                {
                    int index = y * GridWidth + x;
                    int best = distance[index];
                    if (RegionMinX < x) best = Mathf.Min(best, distance[index - 1] + 3);
                    if (RegionMinY < y) best = Mathf.Min(best, distance[index - GridWidth] + 3);
                    if (RegionMinX < x && RegionMinY < y) best = Mathf.Min(best, distance[index - GridWidth - 1] + 4);
                    if (x < RegionMaxX && RegionMinY < y) best = Mathf.Min(best, distance[index - GridWidth + 1] + 4);
                    distance[index] = best;
                }
            }

            for (int y = RegionMaxY; y >= RegionMinY; y--)
            {
                for (int x = RegionMaxX; x >= RegionMinX; x--)
                {
                    int index = y * GridWidth + x;
                    int best = distance[index];
                    if (x < RegionMaxX) best = Mathf.Min(best, distance[index + 1] + 3);
                    if (y < RegionMaxY) best = Mathf.Min(best, distance[index + GridWidth] + 3);
                    if (x < RegionMaxX && y < RegionMaxY) best = Mathf.Min(best, distance[index + GridWidth + 1] + 4);
                    if (RegionMinX < x && y < RegionMaxY) best = Mathf.Min(best, distance[index + GridWidth - 1] + 4);
                    distance[index] = best;
                }
            }
        }

        // 실루엣 가장자리(불투명 칸 중 상하좌우에 빈 칸이 있는 것) 목록. 격자 테두리 한 줄은 이웃 검사를 위해 제외한다.
        private void BuildEdgeList(int _minX, int _maxX, int _minY, int _maxY)
        {
            int startX = Mathf.Max(1, _minX);
            int endX = Mathf.Min(GridWidth - 2, _maxX);
            int startY = Mathf.Max(1, _minY);
            int endY = Mathf.Min(GridHeight - 2, _maxY);

            EdgeCellCount = 0;
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int index = y * GridWidth + x;
                    if (false == solid[index]) continue;

                    if (false == solid[index - 1] || false == solid[index + 1] || false == solid[index - GridWidth] || false == solid[index + GridWidth])
                    {
                        edgeCells[EdgeCellCount] = index;
                        edgeMask[index] = true;
                        EdgeCellCount++;
                    }
                }
            }
        }
    }
}
