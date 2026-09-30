using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 32 PPU 픽셀 격자 정수 좌표 사각형(쿼드)을 모아 하나의 Mesh로 올리는 공용 버퍼. VFX_BrandStarWrap, VFX_BrandStampBurst,
    /// VFX_OverheatAura가 같은 코드를 세 벌씩 갖고 있던 정점/색/삼각형 리스트와 AddRect를 한곳으로 모은 것이다.
    /// 리스트는 이펙트 종류마다 static 버퍼로 두고 인스턴스끼리 공유한다(메인 스레드 단일 실행, 매 프레임 Clear 후 채운다).
    ///
    /// 정점 좌표는 두 가지로 넣을 수 있다.
    /// - AddWorldRect: 월드 픽셀 좌표를 받아 메쉬 오브젝트의 로컬 좌표로 바꿔 넣는다. 오브젝트가 회전/스케일 없이 위치만 가지면(대부분의 경우)
    ///   행렬 곱 없이 평행이동만 더한다. 이 경로의 결과는 MultiplyPoint3x4와 비트 단위로 같다(1을 곱하고 0을 더하는 항이 사라질 뿐이다).
    /// - AddLocalRect: 메쉬 오브젝트가 이미 정확한 위치에 놓여 있어서 로컬 픽셀 오프셋을 그대로 넣는다(변환 없음).
    /// </summary>
    public sealed class PixelQuadBuffer
    {
        private const float PixelUnit = 1.0f / 32.0f;
        private const int MaxVertices = 60000;

        private readonly List<Vector3> vertices;
        private readonly List<Color32> colors;
        private readonly List<int> triangles;

        private Matrix4x4 worldToLocal;
        private bool bTranslationOnly;
        private float translationX;
        private float translationY;
        private float translationZ;
        private float meshZ;

        public int VertexCount => vertices.Count;

        public PixelQuadBuffer(int _quadCapacity)
        {
            vertices = new List<Vector3>(_quadCapacity * 4);
            colors = new List<Color32>(_quadCapacity * 4);
            triangles = new List<int>(_quadCapacity * 6);
        }

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
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
            if (MaxVertices <= vertices.Count + 4) return;

            float x0 = _x0 * PixelUnit;
            float y0 = _y0 * PixelUnit;
            float x1 = _x1 * PixelUnit;
            float y1 = _y1 * PixelUnit;

            int v = vertices.Count;
            if (true == bTranslationOnly)
            {
                float z = meshZ + translationZ;
                vertices.Add(new Vector3(x0 + translationX, y0 + translationY, z));
                vertices.Add(new Vector3(x0 + translationX, y1 + translationY, z));
                vertices.Add(new Vector3(x1 + translationX, y1 + translationY, z));
                vertices.Add(new Vector3(x1 + translationX, y0 + translationY, z));
            }
            else
            {
                vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(x0, y0, meshZ)));
                vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(x0, y1, meshZ)));
                vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(x1, y1, meshZ)));
                vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(x1, y0, meshZ)));
            }

            AddColorsAndTriangles(v, _color);
        }

        /// <summary>
        /// 로컬 픽셀 오프셋 직사각형 [x0,x1) x [y0,y1)를 변환 없이 쿼드로 추가한다(z = 0).
        /// </summary>
        public void AddLocalRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (MaxVertices <= vertices.Count + 4) return;

            float x0 = _x0 * PixelUnit;
            float y0 = _y0 * PixelUnit;
            float x1 = _x1 * PixelUnit;
            float y1 = _y1 * PixelUnit;

            int v = vertices.Count;
            vertices.Add(new Vector3(x0, y0, 0.0f));
            vertices.Add(new Vector3(x0, y1, 0.0f));
            vertices.Add(new Vector3(x1, y1, 0.0f));
            vertices.Add(new Vector3(x1, y0, 0.0f));

            AddColorsAndTriangles(v, _color);
        }

        private void AddColorsAndTriangles(int _firstVertex, Color32 _color)
        {
            colors.Add(_color);
            colors.Add(_color);
            colors.Add(_color);
            colors.Add(_color);

            triangles.Add(_firstVertex);
            triangles.Add(_firstVertex + 1);
            triangles.Add(_firstVertex + 2);
            triangles.Add(_firstVertex);
            triangles.Add(_firstVertex + 2);
            triangles.Add(_firstVertex + 3);
        }

        /// <summary>
        /// 모은 쿼드를 메쉬에 올린다. 카메라 컬링에서 사라지지 않도록 경계는 넉넉한 고정 크기로 둔다.
        /// (정점 수가 줄어드는 경우가 있어서 Clear로 비운 뒤 다시 채운다 - Set* 만으로는 정점 수가 줄지 않는다.)
        /// </summary>
        public void Upload(Mesh _mesh, Vector3 _boundsSize)
        {
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0, false);
            _mesh.bounds = new Bounds(Vector3.zero, _boundsSize);
        }
    }
}
