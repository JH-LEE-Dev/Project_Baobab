using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 포자 디버프 상단 메쉬용 픽셀 쿼드 버퍼. PixelQuadBuffer처럼 월드 픽셀 사각형을 모아 한 메쉬로 올리되, 쿼드마다 붙은 나무(Top 렌더러) 피벗과
    /// 흔들기 방식을 정점에 함께 넣어 셰이더(Custom/VFX/TreeSporeDebuffSway)가 나무 본체와 같은 바람 흔들림으로 쿼드를 옮기게 한다.
    /// 메쉬 오브젝트는 원점·회전 없음·배율 1이라 월드 좌표를 그대로 넣는다(예전 상단 메쉬와 같은 전제).
    /// SetSway로 정한 값이 그 뒤에 넣는 쿼드들에 붙는다. 메인 스레드에서 매 틱 Clear 후 채운다.
    /// </summary>
    public sealed class TreeSporeDebuffSwayBuffer
    {
        private const float PixelUnit = 1.0f / 32.0f;
        private const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;

        /// <summary>흔들지 않는다(바닥에 떨어진 방울 등).</summary>
        public const float ModeStatic = 0.0f;
        /// <summary>바람은 꺼져 있고 지진 떨림만 따른다(나무 sway가 꺼진 경우).</summary>
        public const float ModeQuakeOnly = 1.0f;
        /// <summary>바람 흔들림과 지진 떨림을 모두 따른다.</summary>
        public const float ModeSway = 2.0f;

        [StructLayout(LayoutKind.Sequential)]
        private struct SwayVertex
        {
            public Vector3 position;
            public Color32 color;
            public Vector4 swayA;
            public Vector4 swayB;
        }

        private static readonly VertexAttributeDescriptor[] VertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        };

        private SwayVertex[] vertices;
        private int vertexCount;
        private int[] indices = new int[0];

        private float pivotX;
        private float pivotY;
        private float pivotZ;
        private float mode;
        private float scaleX = 1.0f;
        private float scaleY = 1.0f;

        public int VertexCount => vertexCount;

        public TreeSporeDebuffSwayBuffer(int _quadCapacity)
        {
            vertices = new SwayVertex[Mathf.Max(4, _quadCapacity * 4)];
        }

        public void Clear()
        {
            vertexCount = 0;
            mode = ModeStatic;
        }

        /// <summary>이후 넣는 쿼드가 따를 나무 피벗(Top 렌더러 월드 위치)·월드 배율·흔들기 방식.</summary>
        public void SetSway(Vector3 _pivot, Vector2 _scale, float _mode)
        {
            pivotX = _pivot.x;
            pivotY = _pivot.y;
            pivotZ = _pivot.z;
            scaleX = _scale.x;
            scaleY = _scale.y;
            mode = _mode;
        }

        /// <summary>흔들기 방식만 바꾼다(피벗·배율은 그대로).</summary>
        public void SetMode(float _mode)
        {
            mode = _mode;
        }

        /// <summary>월드 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 쿼드로 추가한다.</summary>
        public void AddWorldRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            int v = vertexCount;
            if (vertices.Length < v + 4) System.Array.Resize(ref vertices, Mathf.Max(v + 4, vertices.Length * 2));

            float x0 = _x0 * PixelUnit;
            float y0 = _y0 * PixelUnit;
            float x1 = _x1 * PixelUnit;
            float y1 = _y1 * PixelUnit;
            Vector4 a = new Vector4((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, pivotX, pivotY);
            Vector4 b = new Vector4(pivotZ, mode, scaleX, scaleY);

            ref SwayVertex q0 = ref vertices[v];
            ref SwayVertex q1 = ref vertices[v + 1];
            ref SwayVertex q2 = ref vertices[v + 2];
            ref SwayVertex q3 = ref vertices[v + 3];
            q0.position = new Vector3(x0, y0, 0.0f);
            q1.position = new Vector3(x0, y1, 0.0f);
            q2.position = new Vector3(x1, y1, 0.0f);
            q3.position = new Vector3(x1, y0, 0.0f);
            q0.color = _color;
            q1.color = _color;
            q2.color = _color;
            q3.color = _color;
            q0.swayA = a;
            q1.swayA = a;
            q2.swayA = a;
            q3.swayA = a;
            q0.swayB = b;
            q1.swayB = b;
            q2.swayB = b;
            q3.swayB = b;
            vertexCount = v + 4;
        }

        /// <summary>모은 쿼드를 메쉬에 올린다. 경계는 호출 측이 정한다.</summary>
        public void Upload(Mesh _mesh)
        {
            int meshVertexCount = _mesh.vertexCount;
            if (meshVertexCount < vertexCount)
            {
                int capacity = Mathf.Max(256, Mathf.NextPowerOfTwo(vertexCount));
                _mesh.SetVertexBufferParams(capacity, VertexLayout);

                int indexCount = capacity / 4 * 6;
                if (indices.Length < indexCount)
                {
                    indices = new int[indexCount];
                    for (int q = 0, v = 0; q < indexCount; q += 6, v += 4)
                    {
                        indices[q] = v;
                        indices[q + 1] = v + 1;
                        indices[q + 2] = v + 2;
                        indices[q + 3] = v;
                        indices[q + 4] = v + 2;
                        indices[q + 5] = v + 3;
                    }
                }

                _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                _mesh.SetIndexBufferData(indices, 0, 0, indexCount, UploadFlags);
                _mesh.subMeshCount = 1;
                meshVertexCount = capacity;
            }

            if (0 < vertexCount) _mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, UploadFlags);
            if (0 == meshVertexCount) return;

            SubMeshDescriptor subMesh = new SubMeshDescriptor(0, vertexCount / 4 * 6, MeshTopology.Triangles);
            subMesh.firstVertex = 0;
            subMesh.vertexCount = Mathf.Max(vertexCount, 1);
            _mesh.SetSubMesh(0, subMesh, UploadFlags);
        }
    }
}
