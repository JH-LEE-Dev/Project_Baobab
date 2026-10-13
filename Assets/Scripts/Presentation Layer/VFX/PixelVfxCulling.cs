using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 절차적 픽셀 이펙트가 메쉬 재구성을 건너뛸지 판단하는 화면 안/밖 검사. Renderer.isVisible은 지난 프레임 렌더 결과라 한 프레임 늦고
    /// 씬 뷰 카메라도 포함하므로, 이번 프레임의 메인 카메라 절두체와 이펙트가 그릴 수 있는 경계(지금 그린 내용 + 움직이는 입자)를 직접 비교한다.
    /// 절두체 평면은 프레임마다 한 번만 계산해서 모든 인스턴스가 공유한다(할당 없음). 메인 카메라가 없으면 항상 보이는 것으로 본다.
    /// </summary>
    public static class PixelVfxCulling
    {
        /// <summary>
        /// 화면 안/밖 판단에 더하는 여유(월드 유닛, 경계의 각 변에). 이번 프레임에 새로 생기거나 움직이는 조각과, 절두체가 카메라
        /// 갱신보다 먼저 계산돼 한 프레임 늦는 만큼을 덮는다. 이 클래스를 쓰는 이펙트가 같은 값을 쓰도록 여기 한 곳에 둔다.
        /// </summary>
        public const float MarginUnits = 1.0f;

        /// <summary>판단 경계의 깊이(z) 크기. 2D 이펙트라 카메라 절두체의 깊이 범위에 걸치기만 하면 된다.</summary>
        public const float BoundsDepth = 10.0f;

        private static readonly Plane[] frustumPlanes = new Plane[6];
        private static Camera cachedCamera;
        private static int cachedFrame = -1;
        private static bool bHasCamera;

        /// <summary>
        /// 월드 경계(중심, 크기)가 메인 카메라 절두체와 겹치는지 돌려준다.
        /// </summary>
        public static bool IsVisible(Vector3 _center, Vector3 _size)
        {
            int frame = Time.frameCount;
            if (frame != cachedFrame)
            {
                cachedFrame = frame;
                if (null == cachedCamera || false == cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;

                bHasCamera = null != cachedCamera;
                if (true == bHasCamera) GeometryUtility.CalculateFrustumPlanes(cachedCamera, frustumPlanes);
            }

            if (false == bHasCamera) return true;
            return GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(_center, _size));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cachedCamera = null;
            cachedFrame = -1;
            bHasCamera = false;
        }
    }
}
