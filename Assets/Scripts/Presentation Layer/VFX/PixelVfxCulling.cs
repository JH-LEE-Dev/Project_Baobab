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
        // 평면 값을 관리 코드에서 바로 쓰려고 풀어 둔 것(nx, ny, nz, |nx|, |ny|, |nz|, d 순서로 평면 6개)
        private static readonly float[] planeData = new float[6 * 7];

        // 관리 코드로 먼저 판정할 때 둘 여유(월드 유닛). 평면까지 거리 + 반경이 이보다 확실히 크거나 작을 때만 바로 답하고,
        // 경계 근처(±여유 안)는 원래처럼 TestPlanesAABB에 맡긴다. 좌표가 수백 유닛이어도 float 계산 오차는 1e-3보다 훨씬 작아서,
        // 바로 답하는 경우는 TestPlanesAABB도 반드시 같은 답을 낸다(결과 동일, 대부분 네이티브 호출만 줄어든다).
        private const float ManagedTestSlack = 0.01f;
        private static Camera cachedCamera;
        private static int cachedFrame = -1;
        private static bool bHasCamera;

        /// <summary>
        /// 월드 경계(중심, 크기)가 메인 카메라 절두체와 겹치는지 돌려준다.
        /// </summary>
        public static bool IsVisible(Vector3 _center, Vector3 _size)
        {
            PrepareFrame();

            if (false == bHasCamera) return true;

            // 상자가 어느 평면의 완전히 바깥이면 안 보인다(TestPlanesAABB와 같은 판정: 평면까지 거리 + 반경 < 0).
            float ex = _size.x * 0.5f;
            float ey = _size.y * 0.5f;
            float ez = _size.z * 0.5f;
            bool bNearBoundary = false;
            for (int o = 0; o < 6 * 7; o += 7)
            {
                float reach = planeData[o] * _center.x + planeData[o + 1] * _center.y + planeData[o + 2] * _center.z + planeData[o + 6]
                    + planeData[o + 3] * ex + planeData[o + 4] * ey + planeData[o + 5] * ez;
                if (-ManagedTestSlack > reach) return false;
                if (ManagedTestSlack > reach) bNearBoundary = true;
            }

            if (false == bNearBoundary) return true;
            return GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(_center, _size));
        }

        /// <summary>
        /// 이번 프레임의 절두체를 준비한다(프레임마다 처음 한 번만 계산한다. IsVisible이 처음 불릴 때 하던 일 그대로다).
        /// 일꾼 스레드에서 TestPrepared로 판정할 이펙트는 예전에 IsVisible을 부르던 시점에 주 스레드에서 이것을 불러 둔다.
        /// </summary>
        public static void PrepareFrame()
        {
            int frame = Time.frameCount;
            if (frame != cachedFrame)
            {
                cachedFrame = frame;
                if (null == cachedCamera || false == cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;

                bHasCamera = null != cachedCamera;
                if (true == bHasCamera)
                {
                    GeometryUtility.CalculateFrustumPlanes(cachedCamera, frustumPlanes);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 n = frustumPlanes[i].normal;
                        int o = i * 7;
                        planeData[o] = n.x;
                        planeData[o + 1] = n.y;
                        planeData[o + 2] = n.z;
                        planeData[o + 3] = Mathf.Abs(n.x);
                        planeData[o + 4] = Mathf.Abs(n.y);
                        planeData[o + 5] = Mathf.Abs(n.z);
                        planeData[o + 6] = frustumPlanes[i].distance;
                    }
                }
            }
        }

        /// <summary>
        /// PrepareFrame으로 준비해 둔 절두체로 IsVisible의 관리 코드 판정만 한다(유니티 API를 쓰지 않아 일꾼 스레드에서 불러도 된다).
        /// 1: 보인다, 0: 안 보인다, -1: 경계 근처라 여기서는 정하지 못한다(주 스레드에서 같은 값으로 IsVisible을 불러 확정한다).
        /// 1과 0은 IsVisible이 같은 입력에 돌려주는 값과 같다(IsVisible의 앞부분과 같은 식이다).
        /// </summary>
        public static int TestPrepared(Vector3 _center, Vector3 _size)
        {
            if (false == bHasCamera) return 1;

            float ex = _size.x * 0.5f;
            float ey = _size.y * 0.5f;
            float ez = _size.z * 0.5f;
            bool bNearBoundary = false;
            for (int o = 0; o < 6 * 7; o += 7)
            {
                float reach = planeData[o] * _center.x + planeData[o + 1] * _center.y + planeData[o + 2] * _center.z + planeData[o + 6]
                    + planeData[o + 3] * ex + planeData[o + 4] * ey + planeData[o + 5] * ez;
                if (-ManagedTestSlack > reach) return 0;
                if (ManagedTestSlack > reach) bNearBoundary = true;
            }

            return true == bNearBoundary ? -1 : 1;
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
