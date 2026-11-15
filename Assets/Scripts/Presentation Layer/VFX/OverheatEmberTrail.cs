using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 4스테이지 불씨가 나무에서 튀어나와 날아가는 동안(드랍 비행) 남기는 꼬리. VFX_OverheatEmber가 60Hz 스텝마다 불덩이 중심을 기록하고 그린다.
    /// 승인 시안 2(빛 꼬리 + 꼬리 심선)의 공식을 그대로 옮겼다. 떨어지는 불티는 OverheatEmberParticles(Shed)가 맡는다.
    ///
    /// - 빛 꼬리: 기록한 위치를 따라 0.6px 간격으로 부드러운 원판 쿼드를 놓는다(EmberTrailAdditive, 가산).
    ///   나이 u(0~1, 꼬리 길이 대비)에 따라 반지름 3.8 -> 0.9px(불씨 크기 배율, 최소 0.7배), 색 흰 노랑 -> 노랑 -> 주황 -> 빨강,
    ///   세기 0.7 x (1 - u)^1.3. 구간 하나에 원판이 n개면 원판 세기는 세기 x min(n, 2.2) / n이다(빠를수록 촘촘해지는 만큼 나눈다).
    ///   부드러운 빛이라 픽셀 격자에 맞추지 않고 소수 위치에 둔다.
    /// - 꼬리 심선: 최근 0.16초 위치에 1px 점(흰 노랑 -> 노랑 -> 주황 -> 빨강). 본체 픽셀 메쉬에 월드 격자로 그린다.
    ///
    /// 좌표는 월드 픽셀(y 위쪽). 미리 잡은 배열을 쓰므로 재생 중 힙 할당이 없다(빛 꼬리 쿼드가 넘칠 때만 배열을 늘린다).
    /// </summary>
    public sealed class OverheatEmberTrail
    {
        // 60Hz x 0.3초 = 18칸. 꼬리 길이를 인스펙터에서 늘려도 0.4초까지 담는다.
        public const int HistoryCapacity = 24;
        private const int QuadCapacity = 192;

        private const float PixelsPerUnit = 32.0f;
        private const float SplatSpacing = 0.6f;
        private const float CoreDuration = 0.16f;
        private const float MinScaleForCore = 0.15f;

        // 빛 꼬리 색(sRGB), 나이 경계 0.12 / 0.35 / 0.65
        private static readonly byte[] RibbonR = { 255, 255, 255, 210 };
        private static readonly byte[] RibbonG = { 238, 176, 104, 44 };
        private static readonly byte[] RibbonB = { 180, 64, 34, 20 };

        private struct Point
        {
            public float x;
            public float y;
            public float age;
            public float scale;
        }

        // 내부 의존성
        private readonly Point[] points = new Point[HistoryCapacity];
        // 빛 꼬리 쿼드 배열은 넘치면 함께 두 배로 늘리므로 readonly가 아니다(QuadCapacity는 처음 크기)
        private Vector3[] vertices = new Vector3[QuadCapacity * 4];
        private Color32[] colors = new Color32[QuadCapacity * 4];
        private Vector2[] uvs = new Vector2[QuadCapacity * 4];
        private int[] triangles = new int[QuadCapacity * 6];
        private readonly Color32[] ribbonColors = new Color32[4];

        // 상태 변수
        // points는 오래된 것 -> 새것 순서로 [0, count)에 둔다(최대 24칸이라 앞을 지울 때 당겨도 비용이 작다).
        private int count;
        private int uploadedQuadCount = -1;

        public bool bHasPoints => 0 < count;

        // 퍼블릭 초기화 및 제어 메서드

        public OverheatEmberTrail()
        {
            FillQuadTemplate(0, QuadCapacity);
        }

        // [_from, _to) 쿼드의 인덱스와 UV를 채운다(정점 위치·색과 달리 쿼드마다 고정값)
        private void FillQuadTemplate(int _from, int _to)
        {
            for (int q = _from; q < _to; q++)
            {
                int v = q * 4;
                int t = q * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;

                uvs[v] = new Vector2(-2.0f, -2.0f);
                uvs[v + 1] = new Vector2(-2.0f, 2.0f);
                uvs[v + 2] = new Vector2(2.0f, 2.0f);
                uvs[v + 3] = new Vector2(2.0f, -2.0f);
            }
        }

        /// <summary>
        /// 정점 색 표를 만든다. 선형 색 공간이면 sRGB 목표 색을 선형으로 바꿔 넣는다(OverheatEmberOrb.BuildPalette와 같은 규칙).
        /// 색 공간 조회는 MonoBehaviour 생성자에서 금지되므로 생성자에서 부르지 않고, 소유자가 Awake 이후(BuildTones)에 부른다.
        /// </summary>
        public void BuildPalette()
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;
            for (int i = 0; i < ribbonColors.Length; i++)
            {
                Color color = new Color32(RibbonR[i], RibbonG[i], RibbonB[i], 255);
                if (true == bLinear) color = color.linear;
                ribbonColors[i] = color;
            }
        }

        // 빛 꼬리 쿼드가 꽉 차면 원판을 버리지 않고 정점·색·UV·인덱스 배열을 함께 두 배로 늘린다
        private void GrowQuads()
        {
            int oldQuads = vertices.Length / 4;
            int newQuads = oldQuads * 2;
            System.Array.Resize(ref vertices, newQuads * 4);
            System.Array.Resize(ref colors, newQuads * 4);
            System.Array.Resize(ref uvs, newQuads * 4);
            System.Array.Resize(ref triangles, newQuads * 6);
            FillQuadTemplate(oldQuads, newQuads);
        }

        public void Clear()
        {
            count = 0;
            uploadedQuadCount = -1;
        }

        /// <summary>
        /// 60Hz 스텝마다 부른다. 기록을 _dt만큼 늙히고 _duration보다 오래된 것을 지운 뒤, _bRecord면 이번 불덩이 중심을 더한다.
        /// </summary>
        public void Step(float _dt, bool _bRecord, float _x, float _y, float _scale, float _duration)
        {
            int keep = 0;
            for (int i = 0; i < count; i++)
            {
                Point p = points[i];
                p.age += _dt;
                if (p.age > _duration) continue;
                points[keep] = p;
                keep++;
            }
            count = keep;

            if (false == _bRecord) return;

            if (count >= HistoryCapacity)
            {
                for (int i = 1; i < count; i++) points[i - 1] = points[i];
                count--;
            }

            points[count] = new Point { x = _x, y = _y, age = 0.0f, scale = _scale };
            count++;
        }

        /// <summary>꼬리 심선: 최근 0.16초 위치에 1px 점을 새것부터 찍는다. 같은 칸이 이어지면 한 번만 찍는다.</summary>
        public void DrawCore(PixelPainter _painter, Color32[] _tones)
        {
            int lastX = int.MinValue;
            int lastY = int.MinValue;

            for (int i = count - 1; i >= 0; i--)
            {
                Point p = points[i];
                if (CoreDuration < p.age || MinScaleForCore > p.scale) continue;

                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.RoundToInt(p.y);
                if (x == lastX && y == lastY) continue;
                lastX = x;
                lastY = y;

                int tone = p.age < 0.04f ? 5 : (p.age < 0.08f ? 4 : (p.age < 0.12f ? 3 : 2));
                _painter.Dot(x, y, _tones[tone]);
            }
        }

        /// <summary>
        /// 빛 꼬리 메쉬를 다시 만든다. _intensityScale은 세기 배율(인스펙터), _widthScale은 반지름 배율, _materialIntensity는 머티리얼 _Intensity
        /// (정점 알파 = 세기 / _materialIntensity). 그릴 것이 없으면 렌더러를 끈다.
        /// </summary>
        public void BuildRibbon(Mesh _mesh, MeshRenderer _renderer, in Matrix4x4 _worldToLocal, float _duration, float _intensityScale,
            float _widthScale, float _materialIntensity)
        {
            int quads = 0;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float invMaterial = 1.0f / Mathf.Max(0.0001f, _materialIntensity);
            float invDuration = 1.0f / Mathf.Max(0.0001f, _duration);

            // 새것 -> 오래된 것 순서로 구간을 따라 원판을 놓는다(시안과 같은 순서·간격)
            // 쿼드가 꽉 차도 멈추지 않고 배열을 늘려 끝까지 놓는다
            for (int i = count - 1; i > 0; i--)
            {
                Point a = points[i];
                Point b = points[i - 1];
                float len = Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
                int n = Mathf.Max(1, Mathf.CeilToInt(len / SplatSpacing));
                float spread = Mathf.Min(n, 2.2f) / n;

                for (int j = 0; j < n; j++)
                {
                    float k = j / (float)n;
                    float x = a.x + (b.x - a.x) * k;
                    float y = a.y + (b.y - a.y) * k;
                    float age = a.age + (b.age - a.age) * k;
                    float s = a.scale + (b.scale - a.scale) * k;

                    float u = Mathf.Min(1.0f, age * invDuration);
                    float intensity = 0.7f * Mathf.Pow(1.0f - u, 1.3f) * spread * _intensityScale;
                    if (0.002f > intensity) continue;

                    float radius = (3.8f - 2.9f * u) * Mathf.Max(0.7f, s) * _widthScale;
                    int tone = u < 0.12f ? 0 : (u < 0.35f ? 1 : (u < 0.65f ? 2 : 3));
                    Color32 color = ribbonColors[tone];
                    color.a = (byte)Mathf.Clamp(Mathf.RoundToInt(intensity * invMaterial * 255.0f), 0, 255);

                    float half = radius * 2.0f;
                    float x0 = (x - half) / PixelsPerUnit;
                    float x1 = (x + half) / PixelsPerUnit;
                    float y0 = (y - half) / PixelsPerUnit;
                    float y1 = (y + half) / PixelsPerUnit;

                    // 꽉 차면 이 원판을 버리지 않고 쿼드 배열을 두 배로 늘린다
                    if (quads * 4 >= vertices.Length) GrowQuads();

                    int v = quads * 4;
                    vertices[v] = _worldToLocal.MultiplyPoint3x4(new Vector3(x0, y0, 0.0f));
                    vertices[v + 1] = _worldToLocal.MultiplyPoint3x4(new Vector3(x0, y1, 0.0f));
                    vertices[v + 2] = _worldToLocal.MultiplyPoint3x4(new Vector3(x1, y1, 0.0f));
                    vertices[v + 3] = _worldToLocal.MultiplyPoint3x4(new Vector3(x1, y0, 0.0f));
                    colors[v] = color;
                    colors[v + 1] = color;
                    colors[v + 2] = color;
                    colors[v + 3] = color;

                    minX = Mathf.Min(minX, vertices[v].x);
                    minY = Mathf.Min(minY, vertices[v].y);
                    maxX = Mathf.Max(maxX, vertices[v + 2].x);
                    maxY = Mathf.Max(maxY, vertices[v + 2].y);
                    quads++;
                }
            }

            bool bHasContent = 0 < quads;
            if (_renderer.enabled != bHasContent) _renderer.enabled = bHasContent;
            if (false == bHasContent)
            {
                if (0 != uploadedQuadCount)
                {
                    _mesh.SetTriangles(triangles, 0, 0, 0, false);
                    uploadedQuadCount = 0;
                }
                return;
            }

            // 정점 수가 줄 때 남은 인덱스가 범위를 넘지 않도록 인덱스를 먼저 비운 뒤 정점을 올린다
            _mesh.SetTriangles(triangles, 0, 0, 0, false);
            // 쿼드 배열이 늘어나 UInt16 인덱스로 담을 수 없는 양이 되면 이 메쉬만 UInt32 인덱스로 바꾼다
            if (65535 < quads * 4 && UnityEngine.Rendering.IndexFormat.UInt16 == _mesh.indexFormat) _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.SetVertices(vertices, 0, quads * 4);
            _mesh.SetColors(colors, 0, quads * 4);
            _mesh.SetUVs(0, uvs, 0, quads * 4);
            _mesh.SetTriangles(triangles, 0, quads * 6, 0, false);
            _mesh.bounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f), new Vector3(maxX - minX, maxY - minY, 1.0f));
            uploadedQuadCount = quads;
        }
    }
}
