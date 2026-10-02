using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 화면 왜곡 셰이더가 샘플링하는 _CameraSortingLayerTexture(Renderer2D가 Objects 레이어까지 그린 화면을 복사한 텍스처)를 쓰는 곳이 없을 때
    /// 카메라를 "캡처를 끈 Renderer2D 복제본"으로 바꿔서 매 프레임 전체 화면 복사와 중간 텍스처 비용을 없앤다.
    /// 왜곡을 그리는 쪽(VFX_TreeHeatBurst 왜곡 쿼드, SortingLayerTextureUser가 붙은 렌더러)이 Acquire/Release로 사용자 수를 알리고,
    /// 사용자가 하나라도 있으면 캡처하는 원래 렌더러로 되돌린다. 등록은 렌더 전에(LateUpdate 이전 또는 그 안에서) 일어나므로 같은 프레임에 반영된다.
    /// 이 컴포넌트가 없는 씬에서는 아무것도 바꾸지 않는다(카메라가 원래 렌더러를 그대로 쓴다).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class SortingLayerTextureGate : MonoBehaviour
    {
        [SerializeField, Tooltip("화면 캡처(_CameraSortingLayerTexture)를 켠 Renderer2D의 URP 렌더러 목록 인덱스")] private int captureRendererIndex = 0;
        [SerializeField, Tooltip("캡처만 끈 Renderer2D 복제본의 URP 렌더러 목록 인덱스")] private int noCaptureRendererIndex = 1;

        private static readonly List<SortingLayerTextureGate> activeGates = new List<SortingLayerTextureGate>(4);
        private static int userCount;

        //내부 의존성
        private UniversalAdditionalCameraData cameraData;

        //상태 변수
        private int appliedRendererIndex = -1;

        /// <summary>
        /// 캡처 텍스처를 쓰기 시작한다(왜곡 렌더러가 켜질 때).
        /// </summary>
        public static void Acquire()
        {
            userCount++;
            if (1 == userCount) ApplyAll();
        }

        /// <summary>
        /// 캡처 텍스처 사용을 끝낸다(왜곡 렌더러가 꺼질 때). Acquire와 짝을 맞춰 부른다.
        /// </summary>
        public static void Release()
        {
            if (0 >= userCount) return;

            userCount--;
            if (0 == userCount) ApplyAll();
        }

        private static void ApplyAll()
        {
            for (int i = 0; i < activeGates.Count; i++) activeGates[i].Apply();
        }

        private void Apply()
        {
            if (null == cameraData) return;

            int rendererIndex = 0 < userCount ? captureRendererIndex : noCaptureRendererIndex;
            if (rendererIndex == appliedRendererIndex) return;

            appliedRendererIndex = rendererIndex;
            cameraData.SetRenderer(rendererIndex);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeGates.Clear();
            userCount = 0;
        }

        private void Awake()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
        }

        private void OnEnable()
        {
            if (false == activeGates.Contains(this)) activeGates.Add(this);
            appliedRendererIndex = -1;
            Apply();
        }

        private void OnDisable()
        {
            activeGates.Remove(this);

            // 꺼질 때는 원래(캡처하는) 렌더러로 되돌려 둔다
            if (null != cameraData && captureRendererIndex != appliedRendererIndex) cameraData.SetRenderer(captureRendererIndex);
            appliedRendererIndex = -1;
        }
    }
}
