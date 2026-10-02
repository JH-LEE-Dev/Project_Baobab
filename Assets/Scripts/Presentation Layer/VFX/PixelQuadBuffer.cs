using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 32 PPU 픽셀 격자 정수 좌표 사각형(쿼드)을 모아 하나의 Mesh로 올리는 공용 버퍼. VFX_BrandStarWrap, VFX_BrandStampBurst,
    /// VFX_OverheatAura가 같은 코드를 세 벌씩 갖고 있던 정점/색/삼각형 리스트와 AddRect를 한곳으로 모은 것이다.
    /// 버퍼는 이펙트 종류마다 static으로 두고 인스턴스끼리 공유한다(메인 스레드 단일 실행, 매 프레임 Clear 후 채운다).
    ///
    /// 정점 좌표는 두 가지로 넣을 수 있다.
    /// - AddWorldRect: 월드 픽셀 좌표를 받아 메쉬 오브젝트의 로컬 좌표로 바꿔 넣는다. 오브젝트가 회전/스케일 없이 위치만 가지면(대부분의 경우)
    ///   행렬 곱 없이 평행이동만 더한다. 이 경로의 결과는 MultiplyPoint3x4와 비트 단위로 같다(1을 곱하고 0을 더하는 항이 사라질 뿐이다).
    /// - AddLocalRect: 메쉬 오브젝트가 이미 정확한 위치에 놓여 있어서 로컬 픽셀 오프셋을 그대로 넣는다(변환 없음).
    ///
    /// 업로드: 위치와 색을 한 정점 구조체(16바이트)로 묶어 한 번에 올린다. 인덱스는 쿼드마다 같은 패턴(0,1,2,0,2,3)이라 메쉬마다 용량만큼 처음 한 번만 채우고,
    /// 이후에는 정점 데이터와 서브메쉬 인덱스 수만 갱신한다(Mesh.Clear, 인덱스 재생성/검증, 바운드 재계산 없음).
    /// 채운 내용의 해시(ContentHash)와 로컬 픽셀 경계(TryGetLocalBounds)도 함께 모아서, 호출 측이 바뀐 것이 없을 때 업로드를 건너뛰거나 경계를 좁게 잡을 수 있다.
    /// </summary>
    public sealed class PixelQuadBuffer
    {
        private const float PixelUnit = 1.0f / 32.0f;
        private const int MaxVertices = 60000;                  // UInt16 인덱스(65535) 안에 들어가는 4의 배수
        private const int MinMeshVertexCapacity = 256;
        private const ulong HashOffset = 14695981039346656037UL; // FNV-1a 64
        private const ulong HashPrime = 1099511628211UL;
        private const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;

        [StructLayout(LayoutKind.Sequential)]
        private struct QuadVertex
        {
            public Vector3 position;
            public Color32 color;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float value;
            [FieldOffset(0)] public uint bits;
        }

        private static readonly VertexAttributeDescriptor[] VertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        };

        private static ushort[] quadIndexPattern;

        private QuadVertex[] vertices;
        private int vertexCount;
        private ulong contentHash;
        private int minPixelX;
        private int minPixelY;
        private int maxPixelX;
        private int maxPixelY;

        private Matrix4x4 worldToLocal;
        private bool bTranslationOnly;
        private float translationX;
        private float translationY;
        private float translationZ;
        private float meshZ;

        public int VertexCount => vertexCount;

        /// <summary>
        /// 이번에 채운 쿼드(좌표, 색, 순서)의 해시. 직전 업로드 때의 값과 정점 수가 모두 같으면 메쉬 내용도 같다.
        /// </summary>
        public ulong ContentHash => contentHash;

        public PixelQuadBuffer(int _quadCapacity)
        {
            vertices = new QuadVertex[Mathf.Clamp(_quadCapacity * 4, 4, MaxVertices)];
            Clear();
        }

        public void Clear()
        {
            vertexCount = 0;
            contentHash = HashOffset;
            minPixelX = int.MaxValue;
            minPixelY = int.MaxValue;
            maxPixelX = int.MinValue;
            maxPixelY = int.MinValue;
        }

        /// <summary>
        /// AddWorldRect가 쓸 월드 -> 로컬 변환과 사각형의 z값을 정한다. 변환이 순수 평행이동(회전/스케일 없음)이면 빠른 경로를 쓴다.
        /// </summary>
        public void SetWorldTransform(Matrix4x4 _worldToLocal, float _meshZ)
        {
            worldToLocal = _worldToLocal;
            meshZ = _meshZ;

            bTranslationOnly =
                1.0f == _worldToLocal.m00 && 0.0f == _worldToLocal.m01 && 0.0f == _worldToLocal.m02 &&
                0.0f == _worldToLocal.m10 && 1.0f == _worldToLocal.m11 && 0.0f == _worldToLocal.m12 &&
                0.0f == _worldToLocal.m20 && 0.0f == _worldToLocal.m21 && 1.0f == _worldToLocal.m22;

            translationX = _worldToLocal.m03;
            translationY = _worldToLocal.m13;
            translationZ = _worldToLocal.m23;
        }

        /// <summary>
        /// 월드 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 쿼드로 추가한다.
        /// </summary>
        public void AddWorldRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (MaxVertices <= vertexCount + 4) return;
            EnsureCapacity(vertexCount + 4);

            float x0 = _x0 * PixelUnit;
            float y0 = _y0 * PixelUnit;
            float x1 = _x1 * PixelUnit;
            float y1 = _y1 * PixelUnit;

            int v = vertexCount;
            if (true == bTranslationOnly)
            {
                float z = meshZ + translationZ;
                vertices[v].position = new Vector3(x0 + translationX, y0 + translationY, z);
                vertices[v + 1].position = new Vector3(x0 + translationX, y1 + translationY, z);
                vertices[v + 2].position = new Vector3(x1 + translationX, y1 + translationY, z);
                vertices[v + 3].position = new Vector3(x1 + translationX, y0 + translationY, z);
            }
            else
            {
                vertices[v].position = worldToLocal.MultiplyPoint3x4(new Vector3(x0, y0, meshZ));
                vertices[v + 1].position = worldToLocal.MultiplyPoint3x4(new Vector3(x0, y1, meshZ));
                vertices[v + 2].position = worldToLocal.MultiplyPoint3x4(new Vector3(x1, y1, meshZ));
                vertices[v + 3].position = worldToLocal.MultiplyPoint3x4(new Vector3(x1, y0, meshZ));
            }

            // 월드 경로는 변환된 좌표가 바뀌면 내용도 바뀐 것이므로 실제 정점 좌표의 비트를 해시에 섞는다
            MixFloat(vertices[v].position.x);
            MixFloat(vertices[v].position.y);
            MixFloat(vertices[v].position.z);
            MixFloat(vertices[v + 2].position.x);
            MixFloat(vertices[v + 2].position.y);
            FinishQuad(v, _color);
        }

        /// <summary>
        /// 로컬 픽셀 오프셋 직사각형 [x0,x1) x [y0,y1)를 변환 없이 쿼드로 추가한다(z = 0).
        /// </summary>
        public void AddLocalRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (MaxVertices <= vertexCount + 4) return;
            EnsureCapacity(vertexCount + 4);

            float x0 = _x0 * PixelUnit;
            float y0 = _y0 * PixelUnit;
            float x1 = _x1 * PixelUnit;
            float y1 = _y1 * PixelUnit;

            int v = vertexCount;
            vertices[v].position = new Vector3(x0, y0, 0.0f);
            vertices[v + 1].position = new Vector3(x0, y1, 0.0f);
            vertices[v + 2].position = new Vector3(x1, y1, 0.0f);
            vertices[v + 3].position = new Vector3(x1, y0, 0.0f);

            if (_x0 < minPixelX) minPixelX = _x0;
            if (_y0 < minPixelY) minPixelY = _y0;
            if (_x1 > maxPixelX) maxPixelX = _x1;
            if (_y1 > maxPixelY) maxPixelY = _y1;

            Mix(unchecked((uint)_x0));
            Mix(unchecked((uint)_y0));
            Mix(unchecked((uint)_x1));
            Mix(unchecked((uint)_y1));
            FinishQuad(v, _color);
        }

        /// <summary>
        /// AddLocalRect로 채운 쿼드의 로컬 경계(유닛)를 돌려준다. 채운 것이 없으면 false.
        /// </summary>
        public bool TryGetLocalBounds(out Vector2 _min, out Vector2 _max)
        {
            if (minPixelX > maxPixelX)
            {
                _min = Vector2.zero;
                _max = Vector2.zero;
                return false;
            }

            _min = new Vector2(minPixelX * PixelUnit, minPixelY * PixelUnit);
            _max = new Vector2(maxPixelX * PixelUnit, maxPixelY * PixelUnit);
            return true;
        }

        /// <summary>
        /// 모은 쿼드를 메쉬에 올린다. 카메라 컬링에서 사라지지 않도록 경계는 넉넉한 고정 크기로 둔다.
        /// (정점 수가 줄어드는 경우는 서브메쉬 인덱스 수로 그리는 범위만 줄인다.)
        /// </summary>
        public void Upload(Mesh _mesh, Vector3 _boundsSize)
        {
            Upload(_mesh, 0);

            Bounds bounds = _mesh.bounds;
            if (Vector3.zero != bounds.center || _boundsSize != bounds.size) _mesh.bounds = new Bounds(Vector3.zero, _boundsSize);
        }

        /// <summary>
        /// 모은 쿼드를 메쉬의 _vertexOffset 정점 자리부터 올리고, 메쉬가 [0, _vertexOffset + VertexCount) 범위를 그리게 한다(앞쪽 정점은 그대로 둔다).
        /// 경계는 바꾸지 않는다(호출 측이 정한다). 메쉬 용량이 모자라 다시 잡느라 앞쪽(_vertexOffset 이전) 정점이 사라졌으면 false를 돌려준다.
        /// </summary>
        public bool Upload(Mesh _mesh, int _vertexOffset)
        {
            int total = _vertexOffset + vertexCount;
            bool bKept = true;

            if (_mesh.vertexCount < total)
            {
                bKept = 0 == _vertexOffset;
                SetupMesh(_mesh, total);
            }

            if (0 < vertexCount) _mesh.SetVertexBufferData(vertices, 0, _vertexOffset, vertexCount, 0, UploadFlags);
            if (0 == _mesh.vertexCount) return bKept; // 한 번도 채운 적 없는 빈 메쉬는 그릴 것이 없다

            SubMeshDescriptor subMesh = new SubMeshDescriptor(0, total / 4 * 6, MeshTopology.Triangles);
            subMesh.firstVertex = 0;
            subMesh.vertexCount = total;
            _mesh.SetSubMesh(0, subMesh, UploadFlags);
            return bKept;
        }

        /// <summary>
        /// 메쉬가 최소 _quadCount 쿼드를 담을 수 있게 정점/인덱스 버퍼를 미리 잡는다(재생 중에 다시 잡지 않도록).
        /// </summary>
        public static void ReserveMesh(Mesh _mesh, int _quadCount)
        {
            int needed = Mathf.Min(MaxVertices, _quadCount * 4);
            if (_mesh.vertexCount < needed) SetupMesh(_mesh, needed);
        }

        private void EnsureCapacity(int _needed)
        {
            if (_needed <= vertices.Length) return;

            int capacity = Mathf.Min(MaxVertices, Mathf.Max(_needed, vertices.Length * 2));
            QuadVertex[] grown = new QuadVertex[capacity];
            System.Array.Copy(vertices, grown, vertexCount);
            vertices = grown;
        }

        private void FinishQuad(int _firstVertex, Color32 _color)
        {
            vertices[_firstVertex].color = _color;
            vertices[_firstVertex + 1].color = _color;
            vertices[_firstVertex + 2].color = _color;
            vertices[_firstVertex + 3].color = _color;
            vertexCount = _firstVertex + 4;

            Mix((uint)_color.r | ((uint)_color.g << 8) | ((uint)_color.b << 16) | ((uint)_color.a << 24));
        }

        private void Mix(uint _value)
        {
            contentHash = unchecked((contentHash ^ _value) * HashPrime);
        }

        private void MixFloat(float _value)
        {
            FloatBits bits = default;
            bits.value = _value;
            Mix(bits.bits);
        }

        // 정점 버퍼(위치 + 색 한 스트림)와 쿼드 인덱스 패턴을 용량만큼 잡는다. 정점 버퍼를 다시 잡으면 기존 정점 내용은 사라진다.
        private static void SetupMesh(Mesh _mesh, int _neededVertices)
        {
            int capacity = Mathf.Min(MaxVertices, Mathf.Max(_neededVertices, Mathf.Max(MinMeshVertexCapacity, _mesh.vertexCount * 2)));
            capacity -= capacity % 4;
            int indexCount = capacity / 4 * 6;

            ushort[] pattern = GetQuadIndexPattern();
            _mesh.SetVertexBufferParams(capacity, VertexLayout);
            _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
            _mesh.SetIndexBufferData(pattern, 0, 0, indexCount, UploadFlags);
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0, MeshTopology.Triangles), UploadFlags);
        }

        // 쿼드 v(정점 4v ~ 4v+3)의 인덱스(4v, 4v+1, 4v+2, 4v, 4v+2, 4v+3)를 최대 정점 수만큼 한 번 만들어 모든 메쉬가 공유한다
        private static ushort[] GetQuadIndexPattern()
        {
            if (null != quadIndexPattern) return quadIndexPattern;

            int quadCount = MaxVertices / 4;
            quadIndexPattern = new ushort[quadCount * 6];
            for (int q = 0; q < quadCount; q++)
            {
                int v = q * 4;
                int i = q * 6;
                quadIndexPattern[i] = (ushort)v;
                quadIndexPattern[i + 1] = (ushort)(v + 1);
                quadIndexPattern[i + 2] = (ushort)(v + 2);
                quadIndexPattern[i + 3] = (ushort)v;
                quadIndexPattern[i + 4] = (ushort)(v + 2);
                quadIndexPattern[i + 5] = (ushort)(v + 3);
            }

            return quadIndexPattern;
        }
    }
}
