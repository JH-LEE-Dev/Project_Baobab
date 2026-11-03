using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 획득되어 꺼진 불씨가 남긴 흡입 꼬리를 끝까지 그리는 숨김 드라이버. 불씨 본체(VFX_OverheatEmber)는 불씨 오브젝트 자식이라
    /// 획득 순간 같이 꺼지는데, 그대로 두면 0.3~0.5초짜리 꼬리 조각이 도착 순간 뚝 끊긴다. 꺼질 때 남은 입자를 여기로 넘긴다.
    ///
    /// 드라이버는 불씨가 처음 켜질 때(Prepare) 숨김 GameObject + DontDestroyOnLoad로 하나만 만든다(TreeFlashSystem과 같은 방식).
    /// 넘겨받는 쪽(Adopt)은 불씨가 꺼지는 OnDisable에서 불리므로 오브젝트를 만들지 않는다. 씬을 닫는 중에 오브젝트가 생기면
    /// Unity가 "Some objects were not cleaned up when closing the scene" 경고를 띄운다. 드라이버가 없으면 넘겨받지 않고 버린다.
    /// 메쉬는 월드 원점·회전 없음이라 월드 픽셀을 그대로 그린다. 정렬은 마지막으로 넘겨준 불씨의 값을 따른다.
    /// </summary>
    public static class OverheatEmberResidue
    {
        private const int Capacity = 384;
        private const int QuadBudget = 1600;

        private static readonly OverheatEmberParticles particles = new OverheatEmberParticles(Capacity);
        private static ResidueDriver driver;

        // 도메인 리로드를 끈 Enter Play Mode 설정에서도 이전 세션의 입자/드라이버 참조가 남지 않도록 플레이 시작마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            particles.Clear();
            driver = null;
        }

        /// <summary>드라이버를 미리 만든다. 불씨가 켜질 때(평상시 플레이 중) 부른다. 이미 있거나 플레이 중이 아니면 아무것도 하지 않는다.</summary>
        public static void Prepare()
        {
            if (null != driver || false == Application.isPlaying) return;

            CreateDriver();
        }

        /// <summary>
        /// _source의 살아 있는 입자를 넘겨받는다. 색 표와 머티리얼, 정렬은 넘겨준 불씨의 것을 쓴다.
        /// 드라이버가 없으면(씬 정리 중 먼저 파괴된 경우 등) 넘겨받지 않고 _source를 비운다.
        /// </summary>
        public static void Adopt(OverheatEmberParticles _source, Material _material, int _sortingLayerID, int _sortingOrder,
            Color32[] _tones, Color32[] _darks, Color32 _smoke)
        {
            if (null == _source || 0 == _source.Count) return;

            if (null == driver || false == Application.isPlaying)
            {
                _source.Clear();
                return;
            }

            _source.TransferTo(particles);
            driver.Configure(_material, _sortingLayerID, _sortingOrder, _tones, _darks, _smoke);
        }

        private static void CreateDriver()
        {
            GameObject go = new GameObject("OverheatEmberResidue");
            // DontSave 계열은 플레이 종료 뒤에도 남아 다음 세션의 새 드라이버와 함께 두 번 그릴 수 있으므로 숨김만 하고 씬 생존은 DontDestroyOnLoad에 맡긴다.
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            driver = go.AddComponent<ResidueDriver>();
        }

        private sealed class ResidueDriver : MonoBehaviour
        {
            private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);

            // 내부 의존성
            private readonly PixelPainter painter = new PixelPainter();
            private readonly Color32[] tones = new Color32[OverheatEmberOrb.ToneCount];
            private readonly Color32[] darks = new Color32[OverheatEmberOrb.DarkCount];
            private MeshRenderer meshRenderer;
            private Mesh mesh;

            // 상태 변수
            private Color32 smoke;
            private float time;
            private ulong meshHash;
            private int meshVertexCount = -1;

            public void Configure(Material _material, int _sortingLayerID, int _sortingOrder, Color32[] _tones, Color32[] _darks, Color32 _smoke)
            {
                if (meshRenderer.sharedMaterial != _material) meshRenderer.sharedMaterial = _material;
                if (meshRenderer.sortingLayerID != _sortingLayerID) meshRenderer.sortingLayerID = _sortingLayerID;
                if (meshRenderer.sortingOrder != _sortingOrder) meshRenderer.sortingOrder = _sortingOrder;

                System.Array.Copy(_tones, tones, tones.Length);
                System.Array.Copy(_darks, darks, darks.Length);
                smoke = _smoke;
            }

            private void Awake()
            {
                if (null != driver && this != driver)
                {
                    Destroy(gameObject);
                    return;
                }

                meshRenderer = GetComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.enabled = false;

                mesh = new Mesh { name = "OverheatEmberResidue" };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
                PixelQuadBuffer.ReserveMesh(mesh, QuadBudget);
            }

            private void LateUpdate()
            {
                if (driver != this) return;

                if (0 == particles.Count)
                {
                    if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                    meshVertexCount = -1;
                    return;
                }

                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                time += dt;
                particles.Step(dt, time);

                painter.Begin(buffer, Matrix4x4.identity, 0.0f);
                particles.Draw(painter, tones, darks, smoke);
                painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
            }

            private void OnDestroy()
            {
                if (null != mesh)
                {
                    Destroy(mesh);
                    mesh = null;
                }

                if (driver == this) driver = null;
            }
        }
    }
}
