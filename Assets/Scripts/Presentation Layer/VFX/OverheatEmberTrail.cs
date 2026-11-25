using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

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
    ///
    /// 빛 꼬리 메쉬는 두 단계로 만든다: ComputeRibbon이 정점을 작업 버퍼(RibbonScratch)에 채우고(유니티 API를 쓰지 않아 일꾼 스레드에서 돌 수 있다),
    /// UploadRibbon이 그 구간을 메쉬에 올린다(주 스레드). 식과 순서는 한 번에 만들던 때와 같다.
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

        // 정점 한 개(위치 + 색 + UV 한 스트림). SetVertices/SetColors(Color32)/SetUVs로 올릴 때 유니티가 만드는 정점 자리·형식
        // (위치 float3, 색 UNorm8 x4, UV float2)과 같아 메쉬 내용은 같고, 한 번의 호출(SetVertexBufferData)로 올린다.
        [StructLayout(LayoutKind.Sequential)]
        internal struct RibbonVertex
        {
            public Vector3 position;
            public Color32 color;
            public Vector2 uv;
        }

        /// <summary>
        /// 빛 꼬리 정점을 모아 두는 작업 버퍼. ComputeRibbon이 뒤에 이어 채우고 UploadRibbon이 자기 구간만 메쉬에 올린다.
        /// 스레드마다(또는 한 번에 한 스레드만 쓰는 소유자마다) 하나씩 두고, 앞서 채운 구간이 모두 올라간 뒤에 Clear한다.
        /// </summary>
        public sealed class RibbonScratch
        {
            // 꽉 차면 두 배로 늘리므로 readonly가 아니다(QuadCapacity는 처음 크기). UV는 쿼드마다 고정값이라 늘릴 때 한 번 채워 둔다.
            internal RibbonVertex[] vertices = new RibbonVertex[QuadCapacity * 4];
            internal int vertexCount;

            public RibbonScratch()
            {
                FillQuadTemplate(0, QuadCapacity);
            }

            public void Clear()
            {
                vertexCount = 0;
            }

            // 빛 꼬리 쿼드가 꽉 차면 원판을 버리지 않고 정점 배열을 두 배로 늘린다
            internal void Grow()
            {
                int oldQuads = vertices.Length / 4;
                int newQuads = oldQuads * 2;
                System.Array.Resize(ref vertices, newQuads * 4);
                FillQuadTemplate(oldQuads, newQuads);
            }

            // [_from, _to) 쿼드의 UV를 채운다(정점 위치·색과 달리 쿼드마다 고정값)
            private void FillQuadTemplate(int _from, int _to)
            {
                for (int q = _from; q < _to; q++)
                {
                    int v = q * 4;
                    vertices[v].uv = new Vector2(-2.0f, -2.0f);
                    vertices[v + 1].uv = new Vector2(-2.0f, 2.0f);
                    vertices[v + 2].uv = new Vector2(2.0f, 2.0f);
                    vertices[v + 3].uv = new Vector2(2.0f, -2.0f);
                }
            }
        }

        // 메쉬 정점 버퍼를 처음 잡을 때의 쿼드 수. 모자라면 두 배씩 늘린다.
        private const int MeshQuadReserve = 64;
        private const int MaxUInt16Vertices = 65535;
        private const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;

        private static readonly VertexAttributeDescriptor[] RibbonLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        // 내부 의존성
        private readonly Point[] points = new Point[HistoryCapacity];
        // 쿼드 q(정점 4q ~ 4q+3)의 인덱스(4q, 4q+1, 4q+2, 4q, 4q+2, 4q+3). 메쉬의 인덱스 버퍼에 용량만큼 한 번 올려 두고 그리는 범위만 바꾼다.
        // UploadRibbon(주 스레드)만 읽고 늘린다.
        private static ushort[] indexPattern16 = new ushort[0];
        private static uint[] indexPattern32 = new uint[0];
        private readonly Color32[] ribbonColors = new Color32[4];

        // 상태 변수
        // points는 오래된 것 -> 새것 순서로 [0, count)에 둔다(최대 24칸이라 앞을 지울 때 당겨도 비용이 작다).
        private int count;
        private int uploadedQuadCount = -1;
        // 정점/인덱스 버퍼를 잡아 둔 메쉬와 그 정점 용량(꼬리 하나는 메쉬 하나에만 그린다)
        private Mesh reservedMesh;
        private int reservedVertices;
        // ComputeRibbon이 채워 둔 구간. UploadRibbon이 올린다.
        private RibbonScratch pendingScratch;
        private int pendingStart;
        private int pendingQuads;
        private float pendingMinX;
        private float pendingMinY;
        private float pendingMaxX;
        private float pendingMaxY;

        public bool bHasPoints => 0 < count;

        // 퍼블릭 초기화 및 제어 메서드

        // 메쉬가 _neededVertices 정점을 담을 수 있게 정점 버퍼와 쿼드 인덱스를 잡는다. 이미 충분하면 아무것도 하지 않는다.
        // 다시 잡으면 기존 정점 내용은 사라지지만 UploadRibbon이 바로 이어서 전부 다시 올린다.
        private void EnsureMesh(Mesh _mesh, int _neededVertices)
        {
            if (true == ReferenceEquals(reservedMesh, _mesh) && reservedVertices >= _neededVertices) return;

            int capacity = true == ReferenceEquals(reservedMesh, _mesh) ? reservedVertices * 2 : MeshQuadReserve * 4;
            while (capacity < _neededVertices) capacity *= 2;
            int indexCount = capacity / 4 * 6;

            _mesh.SetVertexBufferParams(capacity, RibbonLayout);
            // 쿼드가 UInt16 인덱스로 담을 수 없는 양이 되면 이 메쉬만 UInt32 인덱스로 잡는다
            if (MaxUInt16Vertices < capacity)
            {
                if (indexPattern32.Length < indexCount)
                {
                    indexPattern32 = new uint[indexCount];
                    for (int q = 0; q < capacity / 4; q++)
                    {
                        uint v = (uint)(q * 4);
                        int t = q * 6;
                        indexPattern32[t] = v;
                        indexPattern32[t + 1] = v + 1;
                        indexPattern32[t + 2] = v + 2;
                        indexPattern32[t + 3] = v;
                        indexPattern32[t + 4] = v + 2;
                        indexPattern32[t + 5] = v + 3;
                    }
                }

                _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                _mesh.SetIndexBufferData(indexPattern32, 0, 0, indexCount, UploadFlags);
            }
            else
            {
                if (indexPattern16.Length < indexCount)
                {
                    indexPattern16 = new ushort[indexCount];
                    for (int q = 0; q < capacity / 4; q++)
                    {
                        int v = q * 4;
                        int t = q * 6;
                        indexPattern16[t] = (ushort)v;
                        indexPattern16[t + 1] = (ushort)(v + 1);
                        indexPattern16[t + 2] = (ushort)(v + 2);
                        indexPattern16[t + 3] = (ushort)v;
                        indexPattern16[t + 4] = (ushort)(v + 2);
                        indexPattern16[t + 5] = (ushort)(v + 3);
                    }
                }

                _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                _mesh.SetIndexBufferData(indexPattern16, 0, 0, indexCount, UploadFlags);
            }

            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0, MeshTopology.Triangles), UploadFlags);
            reservedMesh = _mesh;
            reservedVertices = capacity;
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

        /// <summary>
        /// 나이 0인 새 점을 머리에 더한다(크기 배율은 직전 머리의 값). 기록이 없으면 아무것도 하지 않는다.
        /// 불씨가 마지막 스텝 뒤에 움직인 채 꺼질 때(획득) 도착 자리까지 꼬리를 잇는 데 쓴다.
        /// </summary>
        public void AddHead(float _x, float _y)
        {
            if (0 == count) return;

            float scale = points[count - 1].scale;
            if (count >= HistoryCapacity)
            {
                for (int i = 1; i < count; i++) points[i - 1] = points[i];
                count--;
            }

            points[count] = new Point { x = _x, y = _y, age = 0.0f, scale = scale };
            count++;
        }

        /// <summary>기록을 _target에 그대로 복사한다(_target의 기존 기록은 지운다). 꺼진 불씨의 꼬리를 잔여 드라이버가 이어 그릴 때 쓴다.</summary>
        public void CopyTo(OverheatEmberTrail _target)
        {
            System.Array.Copy(points, _target.points, count);
            _target.count = count;
            _target.uploadedQuadCount = -1;
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
        /// 빛 꼬리 정점을 _scratch 뒤에 이어 채운다(유니티 API를 쓰지 않아 일꾼 스레드에서 불러도 된다). 메쉬에는 UploadRibbon이 올린다.
        /// _intensityScale은 세기 배율(인스펙터), _widthScale은 반지름 배율, _materialIntensity는 머티리얼 _Intensity
        /// (정점 알파 = 세기 / _materialIntensity).
        /// </summary>
        public void ComputeRibbon(RibbonScratch _scratch, in Matrix4x4 _worldToLocal, float _duration, float _intensityScale,
            float _widthScale, float _materialIntensity)
        {
            int start = _scratch.vertexCount;
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

                    // 꽉 차면 이 원판을 버리지 않고 정점 배열을 두 배로 늘린다
                    int v = start + quads * 4;
                    if (v + 4 > _scratch.vertices.Length) _scratch.Grow();

                    RibbonVertex[] vertices = _scratch.vertices;
                    vertices[v].position = _worldToLocal.MultiplyPoint3x4(new Vector3(x0, y0, 0.0f));
                    vertices[v + 1].position = _worldToLocal.MultiplyPoint3x4(new Vector3(x0, y1, 0.0f));
                    vertices[v + 2].position = _worldToLocal.MultiplyPoint3x4(new Vector3(x1, y1, 0.0f));
                    vertices[v + 3].position = _worldToLocal.MultiplyPoint3x4(new Vector3(x1, y0, 0.0f));
                    vertices[v].color = color;
                    vertices[v + 1].color = color;
                    vertices[v + 2].color = color;
                    vertices[v + 3].color = color;

                    minX = Mathf.Min(minX, vertices[v].position.x);
                    minY = Mathf.Min(minY, vertices[v].position.y);
                    maxX = Mathf.Max(maxX, vertices[v + 2].position.x);
                    maxY = Mathf.Max(maxY, vertices[v + 2].position.y);
                    quads++;
                }
            }

            _scratch.vertexCount = start + quads * 4;
            pendingScratch = _scratch;
            pendingStart = start;
            pendingQuads = quads;
            pendingMinX = minX;
            pendingMinY = minY;
            pendingMaxX = maxX;
            pendingMaxY = maxY;
        }

        /// <summary>
        /// 직전 ComputeRibbon이 채운 구간을 메쉬에 올린다(주 스레드). 그릴 것이 없으면 렌더러를 끈다.
        /// 그 사이에 작업 버퍼를 Clear하면 안 된다.
        /// </summary>
        public void UploadRibbon(Mesh _mesh, MeshRenderer _renderer)
        {
            int quads = pendingQuads;
            bool bHasContent = 0 < quads;
            if (_renderer.enabled != bHasContent) _renderer.enabled = bHasContent;
            if (false == bHasContent)
            {
                if (0 != uploadedQuadCount)
                {
                    // 그리는 범위를 비운다(인덱스 0개). 한 번도 올린 적 없는 메쉬는 이미 비어 있다.
                    if (true == ReferenceEquals(reservedMesh, _mesh)) _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0, MeshTopology.Triangles), UploadFlags);
                    uploadedQuadCount = 0;
                }

                pendingScratch = null;
                return;
            }

            // 정점 [0, 쿼드 수 x 4)를 한 번에 올리고, 미리 올려 둔 쿼드 인덱스에서 그리는 범위만 맞춘다
            float minX = pendingMinX;
            float minY = pendingMinY;
            float maxX = pendingMaxX;
            float maxY = pendingMaxY;
            EnsureMesh(_mesh, quads * 4);
            _mesh.SetVertexBufferData(pendingScratch.vertices, pendingStart, 0, quads * 4, 0, UploadFlags);
            SubMeshDescriptor subMesh = new SubMeshDescriptor(0, quads * 6, MeshTopology.Triangles);
            subMesh.firstVertex = 0;
            subMesh.vertexCount = quads * 4;
            _mesh.SetSubMesh(0, subMesh, UploadFlags);
            _mesh.bounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f), new Vector3(maxX - minX, maxY - minY, 1.0f));
            uploadedQuadCount = quads;
            pendingScratch = null;
        }
    }
}
