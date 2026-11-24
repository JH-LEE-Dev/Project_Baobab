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
    ///
    /// 빛 꼬리(OverheatEmberTrail, 드랍·흡입 꼬리)도 넘겨받는다(AdoptTrail). 꼬리마다 자리(RibbonSlot)를 하나씩 잡아 기록을 복사해 두고,
    /// 드라이버가 매 프레임 늙히며 자기 메쉬에 다시 그린다(불씨에 붙어 있을 때와 같은 공식). 심선 점은 입자 메쉬에 함께 그린다.
    /// 자리의 렌더러 오브젝트는 넘겨받을 때가 아니라 드라이버 LateUpdate에서 처음 쓸 때 만든다(위와 같은 이유).
    /// </summary>
    public static class OverheatEmberResidue
    {
        private const int Capacity = 384;
        private const int QuadBudget = 1600;

        // 동시에 이어 그리는 빛 꼬리 수 상한. 다 차면 돌아가며 덮어쓴다.
        private const int RibbonCapacity = 16;

        private static readonly OverheatEmberParticles particles = new OverheatEmberParticles(Capacity);
        private static readonly System.Collections.Generic.List<RibbonSlot> ribbons = new System.Collections.Generic.List<RibbonSlot>(RibbonCapacity);
        // 다 찼을 때 덮어쓸 자리(돌아가며 쓴다)
        private static int nextRibbonSteal;
        private static ResidueDriver driver;

        // 넘겨받은 빛 꼬리 하나. 기록과 그리기 값은 넘겨받을 때 채우고, 렌더러·메쉬는 드라이버가 처음 그릴 때 만든다.
        private sealed class RibbonSlot
        {
            public readonly OverheatEmberTrail trail = new OverheatEmberTrail();
            public bool bActive;
            public bool bPaletteReady;
            public Material material;
            public int sortingLayerID;
            public int sortingOrder;
            public float duration;
            public float intensity;
            public float width;
            public float materialIntensity;
            public MeshRenderer renderer;
            public Mesh mesh;
        }

        // 도메인 리로드를 끈 Enter Play Mode 설정에서도 이전 세션의 입자/드라이버 참조가 남지 않도록 플레이 시작마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            particles.Clear();
            ribbons.Clear();
            nextRibbonSteal = 0;
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
            if (null == _source) return;

            if (null == driver || false == Application.isPlaying)
            {
                _source.Clear();
                return;
            }

            // 이번 프레임에 미뤄 둔 그리기가 지금 입자와 색을 읽으므로 넘겨받기 전에 먼저 끝낸다(예전에는 LateUpdate에서 이미 끝나 있었다)
            driver.CompleteDeferred();

            // 넘길 입자가 없어도 색 표·머티리얼은 맞춰 둔다(함께 넘겨받는 빛 꼬리의 심선 점을 이 색과 메쉬로 그린다)
            if (0 < _source.Count) _source.TransferTo(particles);
            driver.Configure(_material, _sortingLayerID, _sortingOrder, _tones, _darks, _smoke);
        }

        /// <summary>
        /// _source(꺼지는 불씨의 빛 꼬리)의 기록을 복사해 끝까지 사그라들게 그린다. 머티리얼·정렬·길이·세기·폭은 넘겨준 불씨의 값을 쓴다.
        /// 드라이버가 없으면 넘겨받지 않는다.
        /// </summary>
        public static void AdoptTrail(OverheatEmberTrail _source, Material _material, int _sortingLayerID, int _sortingOrder,
            float _duration, float _intensity, float _width, float _materialIntensity)
        {
            if (null == _source || false == _source.bHasPoints || null == _material) return;
            if (null == driver || false == Application.isPlaying) return;

            // 이번 프레임에 미뤄 둔 그리기가 꼬리 심선을 읽으므로 넘겨받기 전에 먼저 끝낸다
            driver.CompleteDeferred();

            RibbonSlot slot = null;
            for (int i = 0; i < ribbons.Count; i++)
            {
                if (false == ribbons[i].bActive)
                {
                    slot = ribbons[i];
                    break;
                }
            }

            if (null == slot)
            {
                if (ribbons.Count < RibbonCapacity)
                {
                    slot = new RibbonSlot();
                    ribbons.Add(slot);
                }
                else
                {
                    slot = ribbons[nextRibbonSteal];
                    nextRibbonSteal = (nextRibbonSteal + 1) % RibbonCapacity;
                }
            }

            if (false == slot.bPaletteReady)
            {
                slot.trail.BuildPalette();
                slot.bPaletteReady = true;
            }

            _source.CopyTo(slot.trail);
            slot.bActive = true;
            slot.material = _material;
            slot.sortingLayerID = _sortingLayerID;
            slot.sortingOrder = _sortingOrder;
            slot.duration = _duration;
            slot.intensity = _intensity;
            slot.width = _width;
            slot.materialIntensity = _materialIntensity;
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

        private sealed class ResidueDriver : MonoBehaviour, IPixelVfxDeferred
        {
            // 드라이버는 하나뿐이라 버퍼도 하나다. 채우는 것은 일꾼 스레드에서 할 수 있지만(ComputeDeferred) 이 드라이버만 쓴다.
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
            private int deferredSlot = -1; // 대기열 자리. -1이면 미뤄 둔 그리기가 없다

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

                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                bool bHasRibbon = UpdateRibbons(dt);

                if (0 == particles.Count && false == bHasRibbon)
                {
                    if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                    meshVertexCount = -1;
                    return;
                }

                time += dt;
                particles.Step(dt, time);

                // 픽셀 채우기는 유니티 API를 쓰지 않는 계산이라 미뤄서 일꾼 스레드에서 돌린다(ComputeDeferred)
                deferredSlot = PixelVfxWorkQueue.Enqueue(this);
            }

            // 미뤄 둔 그리기: 버퍼를 채운다(일꾼 스레드에서 불릴 수 있다). 예전 LateUpdate의 Begin ~ Draw 그대로다.
            void IPixelVfxDeferred.ComputeDeferred()
            {
                painter.Begin(buffer, Matrix4x4.identity, 0.0f);

                // 꼬리 심선은 입자 뒤에 깐다(불씨 본체와 같은 순서)
                for (int i = 0; i < ribbons.Count; i++)
                {
                    RibbonSlot slot = ribbons[i];
                    if (true == slot.bActive && true == slot.trail.bHasPoints) slot.trail.DrawCore(painter, tones);
                }

                particles.Draw(painter, tones, darks, smoke);
            }

            // 넘겨받은 빛 꼬리를 늙히고 각자 메쉬에 다시 그린다(주 스레드). 다 사그라든 자리는 비운다. 아직 그릴 꼬리가 있으면 true.
            private bool UpdateRibbons(float _dt)
            {
                bool bAny = false;

                for (int i = 0; i < ribbons.Count; i++)
                {
                    RibbonSlot slot = ribbons[i];
                    if (false == slot.bActive) continue;

                    slot.trail.Step(_dt, false, 0.0f, 0.0f, 0.0f, slot.duration);
                    EnsureRibbonRenderer(slot);

                    if (slot.renderer.sharedMaterial != slot.material) slot.renderer.sharedMaterial = slot.material;
                    if (slot.renderer.sortingLayerID != slot.sortingLayerID) slot.renderer.sortingLayerID = slot.sortingLayerID;
                    if (slot.renderer.sortingOrder != slot.sortingOrder) slot.renderer.sortingOrder = slot.sortingOrder;

                    slot.trail.BuildRibbon(slot.mesh, slot.renderer, slot.renderer.transform.worldToLocalMatrix, slot.duration, slot.intensity,
                        slot.width, slot.materialIntensity);

                    if (false == slot.trail.bHasPoints)
                    {
                        slot.bActive = false;
                        continue;
                    }

                    bAny = true;
                }

                return bAny;
            }

            private void EnsureRibbonRenderer(RibbonSlot _slot)
            {
                if (null != _slot.renderer) return;

                GameObject go = new GameObject("Ribbon");
                go.hideFlags = HideFlags.HideInHierarchy;
                go.transform.SetParent(transform, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                _slot.renderer = go.AddComponent<MeshRenderer>();
                _slot.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _slot.renderer.receiveShadows = false;
                _slot.renderer.enabled = false;

                _slot.mesh = new Mesh { name = "OverheatEmberResidueRibbon" };
                _slot.mesh.MarkDynamic();
                filter.sharedMesh = _slot.mesh;
            }

            // 미뤄 둔 그리기의 결과를 메쉬에 올린다(주 스레드)
            void IPixelVfxDeferred.ApplyDeferred()
            {
                deferredSlot = -1;
                painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
            }

            // 미뤄 둔 그리기가 있으면 지금 끝낸다(입자·색이 바뀌거나 꺼지기 전에)
            public void CompleteDeferred()
            {
                if (0 <= deferredSlot) PixelVfxWorkQueue.CompleteNow(deferredSlot, this);
            }

            private void OnDisable()
            {
                CompleteDeferred();
            }

            private void OnDestroy()
            {
                if (null != mesh)
                {
                    Destroy(mesh);
                    mesh = null;
                }

                if (driver == this)
                {
                    // 자리의 렌더러는 이 오브젝트의 자식이라 함께 파괴된다. 메쉬만 지우고 자리를 비운다.
                    for (int i = 0; i < ribbons.Count; i++)
                    {
                        if (null != ribbons[i].mesh) Destroy(ribbons[i].mesh);
                    }
                    ribbons.Clear();
                    nextRibbonSteal = 0;
                    driver = null;
                }
            }
        }
    }
}
