using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

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
        [SerializeField, Tooltip("불투명 전체 화면 UI가 월드를 완전히 가린 동안 쓸, 그리기 레이어 마스크만 비운 Renderer2D 복제본의 URP 렌더러 목록 인덱스. -1이면 이 카메라는 바꾸지 않는다(월드 카메라에만 지정)")] private int worldCoveredRendererIndex = -1;

        private static readonly List<SortingLayerTextureGate> activeGates = new List<SortingLayerTextureGate>(4);
        private static int userCount;
        private static bool isWorldCovered;

        // 덮인 동안 그리기를 끈(forceRenderingOff) 월드 렌더러. 덮개가 걷힐 때 이 목록만 되돌린다.
        private static readonly List<Renderer> suspendedRenderers = new List<Renderer>(2048);
        private static Camera[] otherCameras = new Camera[4];
        private static TileBase[] usedTiles = new TileBase[64];

        //내부 의존성
        private UniversalAdditionalCameraData cameraData;
        private Camera cameraComponent;

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

        /// <summary>
        /// 불투명 전체 화면 UI(텐트 특성 화면 등)가 월드 카메라 출력 전체를 덮고 있는지 알린다.
        /// 덮인 동안 월드 카메라는 컬링은 그대로 하고(파티클 일시정지·CullingGroup 판정 불변) 그리기만 건너뛰는 렌더러를 쓴다.
        /// 덮개 위의 UI는 오버레이 카메라가 그 위에 그리므로 최종 화면은 같다. 렌더 직전(beginContextRendering)에 불러야 같은 프레임에 반영된다.
        /// </summary>
        public static void SetWorldCovered(bool _covered)
        {
            if (_covered == isWorldCovered) return;

            isWorldCovered = _covered;
            ApplyAll();

            if (true == _covered) SuspendWorldRenderers();
            else ResumeWorldRenderers();
        }

        /// <summary>
        /// 덮인 동안 월드 카메라만 그리는 렌더러의 그리기를 끈다(forceRenderingOff). 그리기 레이어 마스크만 비운 렌더러로 바꿔도
        /// 컬링은 렌더러마다 그대로 돌아서(개별 모드 타일맵의 청크 준비 포함) 비용이 남기 때문이다. 화면에는 원래 아무것도 안 나가므로 결과는 같다.
        /// 가시성으로 동작이 바뀌는 것은 건드리지 않는다: 파티클(일시정지 컬링이 자기 렌더러 가시성을 본다), 컬링하는 Animator 아래의 렌더러, 애니메이션 타일이 있는 타일맵,
        /// 다른 켜진 카메라도 그리는 레이어, 이미 꺼져 있던 렌더러. CullingGroup은 렌더러가 아니라 카메라 컬링에 묶여 있어 영향이 없다.
        /// 덮인 동안 새로 켜진 렌더러는 그대로 그려지지만(덮개 아래) 결과에는 영향이 없다.
        /// </summary>
        private static void SuspendWorldRenderers()
        {
            Camera worldCamera = null;
            for (int i = 0; i < activeGates.Count; i++)
            {
                SortingLayerTextureGate gate = activeGates[i];
                if (0 <= gate.worldCoveredRendererIndex && null != gate.cameraComponent && true == gate.cameraComponent.isActiveAndEnabled)
                {
                    worldCamera = gate.cameraComponent;
                    break;
                }
            }
            if (null == worldCamera) return;

            int layerMask = worldCamera.cullingMask;
            if (otherCameras.Length < Camera.allCamerasCount) otherCameras = new Camera[Camera.allCamerasCount];
            int cameraCount = Camera.GetAllCameras(otherCameras);
            for (int i = 0; i < cameraCount; i++)
            {
                if (otherCameras[i] != worldCamera) layerMask &= ~otherCameras[i].cullingMask;
                otherCameras[i] = null;
            }
            if (0 == layerMask) return;

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (true == renderer.forceRenderingOff) continue;
                if (renderer is ParticleSystemRenderer) continue;
                if (0 == (layerMask & (1 << renderer.gameObject.layer))) continue;

                Animator animator = renderer.GetComponentInParent<Animator>();
                if (null != animator && AnimatorCullingMode.AlwaysAnimate != animator.cullingMode) continue;

                TilemapRenderer tilemapRenderer = renderer as TilemapRenderer;
                if (null != tilemapRenderer && false == CanSuspendTilemap(tilemapRenderer)) continue;

                renderer.forceRenderingOff = true;
                suspendedRenderers.Add(renderer);
            }
        }

        /// <summary>
        /// 애니메이션이 없는 기본 Tile만 쓰는 타일맵인지 본다. 애니메이션 타일은 그리지 않는 동안 프레임 갱신이 멈춰서
        /// 다시 켠 첫 프레임에 한 박자 늦은 그림이 나오므로(마을 DecoTilemap) 끄지 않는다.
        /// </summary>
        private static bool CanSuspendTilemap(TilemapRenderer _tilemapRenderer)
        {
            Tilemap tilemap = _tilemapRenderer.GetComponent<Tilemap>();
            if (null == tilemap) return false;

            int tileCount = tilemap.GetUsedTilesCount();
            if (usedTiles.Length < tileCount) usedTiles = new TileBase[tileCount];
            tilemap.GetUsedTilesNonAlloc(usedTiles);

            bool bAllStatic = true;
            for (int i = 0; i < tileCount; i++)
            {
                if (null != usedTiles[i] && typeof(Tile) != usedTiles[i].GetType()) bAllStatic = false;
                usedTiles[i] = null;
            }
            return bAllStatic;
        }

        private static void ResumeWorldRenderers()
        {
            for (int i = 0; i < suspendedRenderers.Count; i++)
            {
                Renderer renderer = suspendedRenderers[i];
                if (null != renderer) renderer.forceRenderingOff = false;
            }
            suspendedRenderers.Clear();
        }

        private static void ApplyAll()
        {
            for (int i = 0; i < activeGates.Count; i++) activeGates[i].Apply();
        }

        private void Apply()
        {
            if (null == cameraData) return;

            int rendererIndex;
            if (true == isWorldCovered && 0 <= worldCoveredRendererIndex) rendererIndex = worldCoveredRendererIndex;
            else rendererIndex = 0 < userCount ? captureRendererIndex : noCaptureRendererIndex;
            if (rendererIndex == appliedRendererIndex) return;

            appliedRendererIndex = rendererIndex;
            cameraData.SetRenderer(rendererIndex);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeGates.Clear();
            userCount = 0;
            isWorldCovered = false;
            suspendedRenderers.Clear();
            System.Array.Clear(otherCameras, 0, otherCameras.Length);
            System.Array.Clear(usedTiles, 0, usedTiles.Length);
        }

        private void Awake()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
            cameraComponent = GetComponent<Camera>();
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
