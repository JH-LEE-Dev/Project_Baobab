using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 드론 레이저 충전 이펙트. 총구 중앙의 픽셀 원(기 덩어리)과, 사방에서 그 원으로 빨려 들어가는 작고 얇은 혜성형 입자를
    /// 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬로 그린다(VFX_OverheatAura, VFX_BrandStarWrap과 같은 방식).
    /// VFX_DroneChargeVortex 프리팹의 자식으로 붙어서 풀에서 꺼내져 활성화될 때(OnEnable) 시작하고, 루트 ParticleSystem의 방출이 멈추면
    /// (Drone.StopChargingVfx 페이드) 남은 입자를 빠르게 흡수하는 소화 연출 뒤 스스로 사라진다.
    ///
    /// 동작
    /// 1. 입자는 평면에서 사방으로부터 중심을 향해 곧장 모인다(회오리 없음, 아이소메트릭 기울임/앞뒤 층 분리 없음). 반지름은 시간이 갈수록 빠르게 줄어든다(가속).
    ///    생성 각도는 황금각으로 흩뿌려서 한쪽으로 몰리지 않는다.
    /// 2. 입자는 꼬리 없는 작은 픽셀 모양이다. 모양은 `.`, `+`, `x` 세 가지이고 크기는 입자마다 다르다(1x1, 2x2, 3x3). `+`와 `x` 일부는 이동 중에
    ///    서로 번갈아 바뀌어 회전하는 것처럼 보인다. 중심에 가까워지면(bigDotRadiusPx 안쪽) 모두 1x1 점으로 줄어든다. 위치는 매 프레임 닫힌 식으로 계산해 정수 픽셀에 스냅하므로 프레임 속도로 매끄럽게 움직이면서
    ///    픽셀 격자를 유지한다. 중심에 닿으면 코어와 겹쳐 사라지고 코어가 번쩍인다.
    /// 4. 중앙 원은 정수 픽셀 반지름의 원판(밝은 중심 -> 중간 색 -> 진한 외곽선)이고 상하좌우로 1px 빛줄기가 뻗는다. 충전이 진행될수록(SetChargeDuration의
    ///    시간 동안) 커지고 입자가 늘어난다. 입자가 도착할 때마다 한 칸 커졌다 돌아온다.
    /// 5. 발사(Release)되면 새 입자를 만들지 않고, 남은 입자가 원래 궤도를 따라 빠르게(시간 배율 rushSpeedScale) 중심으로 빨려 들어가 사라진다.
    ///    중앙 원은 한 번 부풀었다 줄어든다(발사 임팩트는 별도 이펙트가 맡는다).
    ///    취소(방출만 멈춤)되면 입자가 온 길을 거꾸로 되짚어 바깥으로 물러나며 작아지고 깜빡이다 사라지고, 중앙 원은 줄어들며 깜빡인다(기가 풀려 취소되는 느낌).
    /// 6. 과열 상태(SetOverheat)에서는 노랑 계열 대신 파랑 계열을 쓴다(드론 레이저/과열 아우라 색과 같은 계열). 색은 상태마다 딱 3가지(진한 색, 중간 색, 밝은 색)만 쓰고
    ///    밝기 차이는 발광 세기로 낸다.
    ///
    /// 이펙트 오브젝트는 정확한 월드 위치(총구, 소수점 단위)에 놓이고 내부 도트만 그 위치 기준 정수 격자에 맞춘다. 그래서 드론이 소수점 단위로
    /// 움직여도 이펙트가 픽셀 단위로 끊겨 따라오지 않는다. 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로만 낸다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)] // 드론의 LateUpdate(총구 위치, 정렬 순서 갱신)가 끝난 뒤의 값을 쓰도록 늦게 실행한다
    public class VFX_ChargeVortex : MonoBehaviour
    {
        [Header("렌더링")]
        [SerializeField] private Material vortexMaterial;
        [SerializeField, Tooltip("루트 렌더러(드론 총구 기준 정렬 순서) 기준 상대 소팅 오더")] private int frontSortingOffset = 1;

        [Header("충전")]
        [SerializeField, Tooltip("충전이 끝나기까지의 기본 시간(초). Drone이 임팩트까지 남은 시간을 SetChargeDuration으로 덮어쓴다")] private float defaultChargeDuration = 0.6f;

        [Header("중앙 원")]
        [SerializeField, Tooltip("충전 시작 때 원의 반지름(px, 정수로 반올림)")] private float coreMinRadiusPx = 3.0f;
        [SerializeField, Tooltip("충전이 끝날 때 원의 반지름(px, 정수로 반올림)")] private float coreMaxRadiusPx = 6.0f;
        [SerializeField, Tooltip("원 바깥을 도는 2x2 점의 개수(16칸 중 등간격에 가깝게 배치). 0이면 없음")] private int haloDotCount = 5;
        [SerializeField, Tooltip("점 고리가 원 바깥에서 떨어진 거리(px)")] private float haloGapPx = 3.0f;
        [SerializeField, Tooltip("상하좌우로 뻗는 빛줄기의 최대 길이(px). 0이면 없음")] private int rayLengthPx = 3;
        [SerializeField, Tooltip("원과 빛줄기가 도트 애니메이션으로 바뀌는 프레임 속도(fps). 낮을수록 뚝뚝 끊긴다")] private float flickerFps = 12.0f;

        [Header("입자")]
        [SerializeField, Tooltip("입자가 생기는 거리의 최솟값(px)")] private float spawnRadiusMinPx = 10.0f;
        [SerializeField, Tooltip("입자가 생기는 거리의 최댓값(px)")] private float spawnRadiusMaxPx = 17.0f;
        [SerializeField, Tooltip("입자가 중심까지 빨려 드는 데 걸리는 시간의 최솟값(초)")] private float lifeMin = 0.15f;
        [SerializeField, Tooltip("입자가 중심까지 빨려 드는 데 걸리는 시간의 최댓값(초)")] private float lifeMax = 0.25f;
        [SerializeField, Tooltip("충전 시작 때 초당 입자 생성 수")] private float spawnRateStart = 36.0f;
        [SerializeField, Tooltip("충전이 끝날 때 초당 입자 생성 수")] private float spawnRateEnd = 120.0f;
        [SerializeField, Range(1.0f, 4.0f), Tooltip("안쪽으로 갈수록 빨라지는 정도. 클수록 마지막에 확 빨려 든다")] private float inflowPower = 2.2f;
        [SerializeField, Tooltip("중심에서 이 거리(px) 안쪽으로 들어오면 입자가 모양과 상관없이 1x1 점으로 줄어든다")] private float bigDotRadiusPx = 8.0f;

        [Header("발사 / 소화 (새 입자 중단, 남은 입자는 빠르게 흡수)")]
        [SerializeField, Range(1.0f, 8.0f), Tooltip("발사/취소 뒤 남은 입자의 시간 배율. 클수록 더 빨리 중심으로 빨려 든다")] private float rushSpeedScale = 3.5f;
        [SerializeField, Tooltip("발사 때 중앙 원이 부풀었다 줄어드는 시간(초). 남은 입자는 이 시간과 무관하게 모두 흡수될 때까지 남는다")] private float releaseDuration = 0.25f;
        [SerializeField, Tooltip("취소 때 중앙 원이 줄어들며 깜빡이는 시간(초)")] private float extinguishDuration = 0.4f;
        [SerializeField, Range(0.5f, 3.0f), Tooltip("취소 때 입자가 온 길을 되짚어 물러나는 속도 배율(1이면 모일 때와 같은 속도)")] private float retreatSpeedScale = 1.2f;

        [Header("밝기 (정점 알파 = HDR 발광 세기 0~1)")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("전체 발광 배율. 1이면 기준값")] private float glowScale = 1.0f;

        private const float TwoPi = Mathf.PI * 2.0f;
        private const float GoldenAngle = 2.39996323f;
        private const int ParticleCapacity = 72;
        private const int MaxSpawnPerFrame = 6;
        private const float SwapInterval = 0.09f;    // `+` <-> `x`가 번갈아 바뀌는 간격(초)
        private const float AbsorbRadiusPx = 1.5f;   // 이 안쪽에 들어오면 중심에 흡수된 것으로 본다
        private const int CoreFlashSteps = 1;        // 입자가 도착했을 때 원이 커지는 픽셀 수
        private const float FlashPerAbsorb = 0.35f;
        private const float FlashDecayPerSecond = 4.0f;
        private const float MinChargeDuration = 0.15f;
        private const float PopDuration = 0.06f;     // 발사 순간 원이 부푸는 시간
        private const float PopScale = 1.4f;
        private const int HaloSlots = 16;
        private static readonly Vector3 MeshBoundsSize = new Vector3(100.0f, 100.0f, 10.0f);

        private enum VortexPhase
        {
            Charging,
            Extinguishing,
            Releasing,
        }

        private struct Particle
        {
            public bool bActive;
            public bool bBright;    // 밝은 색을 쓰는 입자(아니면 중간 색)
            public bool bSpin;      // `+`와 `x`를 번갈아 바꿔 회전하는 것처럼 보이는 입자
            public int shape;       // 0: `.`, 1: `+`, 2: `x`
            public int size;        // 1, 2, 3(px)
            public int seed;        // 번갈아 바뀌는 위상
            public float radius0;
            public float angle0;
            public float age;
            public float life;
        }

        // 색은 상태마다 3가지만 쓴다[과열 여부][진한 색, 중간 색, 밝은 색]. 밝기 차이는 발광 세기로 낸다.
        // HDR 배율이 곱해져도 흰색으로 날아가지 않도록 기본 색은 어둡게 잡았다.
        private const int ToneDeep = 0;
        private const int ToneMid = 1;
        private const int ToneLight = 2;
        private static readonly Color32[][] Palettes =
        {
            new Color32[] { new Color32(120, 52, 2, 255), new Color32(190, 112, 6, 255), new Color32(240, 190, 90, 255) },
            new Color32[] { new Color32(12, 50, 170, 255), new Color32(20, 115, 220, 255), new Color32(110, 200, 240, 255) },
        };

        // 화면에 그릴 쿼드는 이펙트 종류별 공유 버퍼에 모은다(매 프레임 Clear 후 채우고, 메인 스레드 단일 실행이라 인스턴스끼리 공유해도 안전하다)
        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(512);

        //내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private readonly Particle[] particles = new Particle[ParticleCapacity];
        private ParticleSystem rootParticleSystem;
        private ParticleSystemRenderer rootParticleRenderer;

        //상태 변수
        private VortexPhase phase;
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bOverheat;
        private bool bMeshHasGeometry;
        private float elapsed;
        private float extinguishElapsed;
        private float releaseElapsed;
        private float chargeDuration;
        private float spawnAccumulator;
        private float spawnAngle;
        private float coreFlash;
        private int particleCursor;
        private uint randomState;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            GameObject child = new GameObject("ChargeVortex");
            child.transform.SetParent(transform, false);

            meshFilter = child.AddComponent<MeshFilter>();
            meshRenderer = child.AddComponent<MeshRenderer>();

            mesh = new Mesh { name = "VFX_ChargeVortex" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            meshRenderer.sharedMaterial = vortexMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        private void Begin()
        {
            EnsureInitialized();

            phase = VortexPhase.Charging;
            elapsed = 0.0f;
            extinguishElapsed = 0.0f;
            releaseElapsed = 0.0f;
            chargeDuration = defaultChargeDuration;
            spawnAccumulator = 0.0f;
            coreFlash = 0.0f;
            particleCursor = 0;
            bFirstFrame = true;
            bOverheat = false;

            for (int i = 0; i < ParticleCapacity; i++) particles[i].bActive = false;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f));
            if (0u == randomState) randomState = 1u;
            spawnAngle = Rand01() * TwoPi;
        }

        /// <summary>
        /// 임팩트(레이저 발사)까지 남은 시간을 알려 준다. 충전 진행도(원 크기, 입자 수)가 이 시간에 맞춰 0에서 1이 된다.
        /// </summary>
        public void SetChargeDuration(float _seconds)
        {
            chargeDuration = Mathf.Max(MinChargeDuration, _seconds);
        }

        /// <summary>
        /// 과열 상태면 파랑 계열, 아니면 노랑 계열 색을 쓴다. 색은 그릴 때 정해서 충전 도중에 바꿔도 바로 반영된다.
        /// </summary>
        public void SetOverheat(bool _isOverheat)
        {
            bOverheat = _isOverheat;
        }

        /// <summary>
        /// 레이저가 발사되는 순간 호출한다. 새 입자를 만들지 않고 남은 입자가 빠르게 중심으로 빨려 들며, 중앙 원이 부풀었다 줄어든다.
        /// 충전 중이 아니면(이미 발사/소화 중) 아무것도 하지 않는다. 호출 뒤 Drone이 방출을 멈추고 풀 반환은 기존 페이드 타이머가 처리한다.
        /// </summary>
        public void Release()
        {
            if (VortexPhase.Charging != phase) return;

            phase = VortexPhase.Releasing;
            releaseElapsed = 0.0f;
            coreFlash = 1.0f;
        }

        // ---------- 난수 / 유틸 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        private static float SafeInverse(float _value)
        {
            return 0.0001f < Mathf.Abs(_value) ? 1.0f / _value : 1.0f;
        }

        private float GetCharge01()
        {
            return Mathf.Clamp01(elapsed / chargeDuration);
        }

        // ---------- 입자 ----------

        private void SpawnParticles(float _dt)
        {
            if (VortexPhase.Charging != phase) return;

            float charge = GetCharge01();
            spawnAccumulator += Mathf.Lerp(spawnRateStart, spawnRateEnd, charge) * _dt;
            int count = Mathf.Min(MaxSpawnPerFrame, (int)spawnAccumulator);
            spawnAccumulator -= (int)spawnAccumulator;

            for (int n = 0; n < count; n++)
            {
                // 황금각으로 각도를 흩뿌려서 사방에서 고르게 들어온다(약간의 무작위를 섞는다)
                spawnAngle += GoldenAngle + (Rand01() - 0.5f) * 0.6f;

                int slot = particleCursor;
                particleCursor = (particleCursor + 1) % ParticleCapacity;

                Particle particle = new Particle();
                particle.bActive = true;
                particle.radius0 = Mathf.Lerp(spawnRadiusMinPx, spawnRadiusMaxPx, Rand01());
                particle.angle0 = spawnAngle;
                particle.bBright = Rand01() < 0.35f;
                particle.seed = (int)(Rand01() * 1000.0f);
                particle.bSpin = Rand01() < 0.5f;

                // 모양과 크기: 작은 것을 많이 섞는다
                float shapeRoll = Rand01();
                if (shapeRoll < 0.55f) { particle.shape = 0; particle.size = 1; }
                else if (shapeRoll < 0.65f) { particle.shape = 0; particle.size = 2; }
                else if (shapeRoll < 0.82f) { particle.shape = 1; particle.size = 3; }
                else { particle.shape = 2; particle.size = 3; }
                particle.age = 0.0f;
                particle.life = Mathf.Lerp(lifeMin, lifeMax, Rand01());
                particles[slot] = particle;
            }
        }

        private void UpdateParticles(float _dt)
        {
            // 발사 중에는 시간이 빨라져서 남은 입자가 원래 궤도를 따라 빠르게 중심으로 빨려 들고, 취소(소화) 중에는 시간이 거꾸로 흘러 온 길을 되짚어 물러난다
            float ageDt = _dt;
            if (VortexPhase.Releasing == phase) ageDt = _dt * rushSpeedScale;
            else if (VortexPhase.Extinguishing == phase) ageDt = -_dt * retreatSpeedScale;

            for (int i = 0; i < ParticleCapacity; i++)
            {
                if (false == particles[i].bActive) continue;

                particles[i].age += ageDt;

                // 물러나던 입자가 출발점까지 돌아가면 사라진다(코어는 번쩍이지 않는다)
                if (0.0f >= particles[i].age)
                {
                    particles[i].bActive = false;
                    continue;
                }

                float u = particles[i].age / particles[i].life;

                // 중심에 닿으면 사라지고 코어가 번쩍인다
                if (1.0f <= u || AbsorbRadiusPx >= GetRadius(in particles[i], u))
                {
                    particles[i].bActive = false;
                    coreFlash = Mathf.Min(1.0f, coreFlash + FlashPerAbsorb);
                }
            }

            coreFlash = Mathf.Max(0.0f, coreFlash - FlashDecayPerSecond * _dt);
        }

        // 나이(u: 0~1)로 정해지는 중심을 향한 직선 위의 위치. 반지름은 처음엔 천천히, 끝으로 갈수록 빠르게 줄어든다(빨려 듦). 각도는 생성 때 정해져 변하지 않는다.
        private float GetRadius(in Particle _particle, float _u)
        {
            return _particle.radius0 * (1.0f - Mathf.Pow(Mathf.Clamp01(_u), inflowPower));
        }

        private void GetOffset(in Particle _particle, float _u, out float _x, out float _y, out float _radius)
        {
            _radius = GetRadius(in _particle, _u);
            _x = Mathf.Cos(_particle.angle0) * _radius;
            _y = Mathf.Sin(_particle.angle0) * _radius;
        }

        // ---------- 그리기 ----------

        private byte GlowAlpha(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow * glowScale) * 255.0f);
        }

        private Color32 Tint(int _tone, float _glow)
        {
            Color32 color = Palettes[true == bOverheat ? 1 : 0][_tone];
            return new Color32(color.r, color.g, color.b, GlowAlpha(_glow));
        }

        private static void AddRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            quadBuffer.AddLocalRect(_x0, _y0, _x1, _y1, _color);
        }

        private void RebuildMesh()
        {
            quadBuffer.Clear();

            float fps = Mathf.Max(1.0f, flickerFps);
            int flickerFrame = (int)(elapsed * fps);

            // 중앙 원을 먼저 넣어서 입자가 원 위에 겹쳐 그려지게 한다
            int coreRadius = GetCoreRadius();

            // 취소 때는 원이 줄어드는 후반에 깜빡인다(기가 불안정하게 풀리는 느낌)
            bool bCoreHidden = VortexPhase.Extinguishing == phase && 0.45f < extinguishElapsed / Mathf.Max(0.01f, extinguishDuration) && 0 != (flickerFrame & 1);
            if (false == bCoreHidden) DrawCore(coreRadius, flickerFrame);
            DrawRays(coreRadius, flickerFrame);
            DrawHalo(coreRadius, flickerFrame);
            DrawParticles();

            // 이펙트 오브젝트는 이미 정확한 위치에 있다. 부모(드론 Visual)의 스케일/회전은 지우고 도트 크기가 항상 1px이 되게 한다.
            Vector3 parentScale = transform.lossyScale;
            Transform meshTransform = meshRenderer.transform;
            meshTransform.rotation = Quaternion.identity;
            meshTransform.localScale = new Vector3(SafeInverse(parentScale.x), SafeInverse(parentScale.y), 1.0f);

            if (null != rootParticleRenderer)
            {
                meshRenderer.sortingLayerID = rootParticleRenderer.sortingLayerID;
                meshRenderer.sortingOrder = rootParticleRenderer.sortingOrder + frontSortingOffset;
            }

            // 그릴 것이 없고 메쉬도 이미 비어 있으면 올리지 않는다
            bool bEmpty = 0 == quadBuffer.VertexCount;
            if (false == bEmpty || true == bMeshHasGeometry)
            {
                quadBuffer.Upload(mesh, MeshBoundsSize);
                bMeshHasGeometry = false == bEmpty;
            }

            meshRenderer.enabled = true;
        }

        // 중앙 원의 정수 반지름(px). 충전으로 커지고, 입자가 도착하면 한 칸 커졌다 돌아오며, 발사 때는 부풀었다 줄어들고, 소화 때는 줄어든다.
        private int GetCoreRadius()
        {
            float radius = Mathf.Lerp(coreMinRadiusPx, coreMaxRadiusPx, EaseOut(GetCharge01()));
            if (VortexPhase.Extinguishing == phase)
            {
                radius *= 1.0f - Mathf.Clamp01(extinguishElapsed / Mathf.Max(0.01f, extinguishDuration));
            }
            else if (VortexPhase.Releasing == phase)
            {
                float duration = Mathf.Max(0.05f, releaseDuration);
                float pop = Mathf.Clamp01(releaseElapsed / PopDuration);
                float fall = Mathf.Clamp01((releaseElapsed - PopDuration) / Mathf.Max(0.01f, duration - PopDuration));
                radius *= Mathf.Lerp(1.0f, PopScale, pop) * (1.0f - fall);
            }

            int rounded = Mathf.RoundToInt(radius);
            if (VortexPhase.Charging == phase && 0.4f < coreFlash) rounded += CoreFlashSteps;
            return rounded;
        }

        // 중앙 원: 정수 반지름의 원판. 진한 외곽선 1px -> 중간 색 몸통 -> 밝은 중심. 중심은 (0,0) 칸이라 위아래 좌우가 대칭이다.
        // 같은 줄에서 이어지는 같은 색 칸은 하나의 사각형으로 합친다.
        private void DrawCore(int _radius, int _flickerFrame)
        {
            if (1 > _radius) return;

            float outer = _radius + 0.35f;
            float bodyEdge = _radius - 0.75f;
            float lightEdge = _radius * 0.45f + (0 == (_flickerFrame & 2) ? 0.0f : 0.5f);

            for (int cy = -_radius - 1; cy <= _radius + 1; cy++)
            {
                int runStart = 0;
                int runTone = -1;
                float runGlow = 0.0f;

                for (int cx = -_radius - 1; cx <= _radius + 2; cx++)
                {
                    int tone = -1;
                    float glow = 0.0f;
                    if (cx <= _radius + 1)
                    {
                        float d = Mathf.Sqrt(cx * cx + cy * cy);
                        if (d > outer) { }
                        else if (d > bodyEdge && 1 < _radius) { tone = ToneDeep; glow = 0.5f; }
                        else if (d > lightEdge) { tone = ToneMid; glow = 0.8f; }
                        else { tone = ToneLight; glow = 1.0f; }
                    }

                    bool bSame = tone == runTone && runGlow == glow;
                    if (false == bSame)
                    {
                        if (0 <= runTone) AddRect(runStart, cy, cx, cy + 1, Tint(runTone, runGlow));

                        runStart = cx;
                        runTone = tone;
                        runGlow = glow;
                    }
                }
            }
        }

        // 원에서 상하좌우로 뻗는 1px 빛줄기. 길이가 12fps로 뚝뚝 바뀐다. 충전이 어느 정도 진행된 뒤부터 나온다.
        private void DrawRays(int _radius, int _flickerFrame)
        {
            if (0 >= rayLengthPx || 2 > _radius || VortexPhase.Charging != phase) return;
            if (0.25f > GetCharge01()) return;

            int step = _flickerFrame % 4;
            int length = 1 == step || 3 == step ? Mathf.Max(1, rayLengthPx - 1) : (2 == step ? rayLengthPx : 1);
            Color32 color = Tint(ToneLight, 0.9f);
            int start = _radius + 1;
            AddRect(start, 0, start + length, 1, color);
            AddRect(-start - length + 1, 0, -start + 1, 1, color);
            AddRect(0, start, 1, start + length, color);
            AddRect(0, -start - length + 1, 1, -start + 1, color);
        }

        // 원 바깥의 2x2 점 고리: 16칸 중 등간격에 가깝게 배치하고 계단식(6fps)으로 한 칸씩 돈다.
        private void DrawHalo(int _radius, int _flickerFrame)
        {
            if (0 >= haloDotCount || 2 > _radius || VortexPhase.Charging != phase) return;

            float haloRadius = _radius + haloGapPx;
            int rotation = _flickerFrame / 2;
            for (int i = 0; i < haloDotCount; i++)
            {
                int slot = (i * HaloSlots / haloDotCount + rotation) % HaloSlots;
                float angle = TwoPi * slot / HaloSlots;
                int px = Mathf.RoundToInt(Mathf.Cos(angle) * haloRadius);
                int py = Mathf.RoundToInt(Mathf.Sin(angle) * haloRadius);
                AddRect(px, py, px + 2, py + 2, Tint(ToneMid, 0.7f));
            }
        }

        // 입자: 꼬리 없는 작은 픽셀 모양(`.`, `+`, `x`). 위치는 매 프레임 계산해 정수 픽셀에 스냅한다. 중심에 가까워지면 모두 1x1 점이 된다.
        private void DrawParticles()
        {
            for (int i = 0; i < ParticleCapacity; i++)
            {
                if (false == particles[i].bActive) continue;

                float x;
                float y;
                float radius;
                GetOffset(in particles[i], particles[i].age / particles[i].life, out x, out y, out radius);
                int px = Mathf.RoundToInt(x);
                int py = Mathf.RoundToInt(y);

                // 취소로 물러나는 중에는 1px로 작아진 채 후반에 깜빡이다 사라진다
                bool bRetreating = VortexPhase.Extinguishing == phase;
                if (true == bRetreating && particles[i].age < particles[i].life * 0.4f && 0 != ((int)(elapsed * 30.0f) & 1)) continue;

                int shape = particles[i].shape;
                int size = particles[i].size;
                if (true == bRetreating || radius <= bigDotRadiusPx)
                {
                    shape = 0;
                    size = 1;
                }
                else if (true == particles[i].bSpin && 0 != shape)
                {
                    // 0.09초마다 `+` <-> `x`로 바뀌어 회전하는 것처럼 보인다
                    shape = 0 == (((int)(particles[i].age / SwapInterval) + particles[i].seed) & 1) ? 1 : 2;
                }

                Color32 color = Tint(true == particles[i].bBright ? ToneLight : ToneMid, 1 == size ? 1.0f : 0.85f);
                DrawShape(px, py, shape, size, color);
            }
        }

        // 픽셀 모양 하나. (_px, _py)가 중심 칸이다. `.`은 1x1 또는 2x2 사각, `+`/`x`는 3x3(팔 길이 1)이다.
        private static void DrawShape(int _px, int _py, int _shape, int _size, Color32 _color)
        {
            if (0 == _shape)
            {
                AddRect(_px, _py, _px + _size, _py + _size, _color);
                return;
            }

            const int arm = 1;
            if (1 == _shape)
            {
                AddRect(_px - arm, _py, _px + arm + 1, _py + 1, _color);
                AddRect(_px, _py - arm, _px + 1, _py + arm + 1, _color);
                return;
            }

            AddRect(_px, _py, _px + 1, _py + 1, _color);
            for (int k = 1; k <= arm; k++)
            {
                AddRect(_px - k, _py - k, _px - k + 1, _py - k + 1, _color);
                AddRect(_px + k, _py - k, _px + k + 1, _py - k + 1, _color);
                AddRect(_px - k, _py + k, _px - k + 1, _py + k + 1, _color);
                AddRect(_px + k, _py + k, _px + k + 1, _py + k + 1, _color);
            }
        }

        private void HideRenderer()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private bool HasActiveParticles()
        {
            for (int i = 0; i < ParticleCapacity; i++)
            {
                if (true == particles[i].bActive) return true;
            }

            return false;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            Begin();
        }

        private void LateUpdate()
        {
            // 첫 프레임은 시간을 더하지 않아서 t=0의 첫 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            // 루트 ParticleSystem의 방출이 멈추면(Drone.StopChargingVfx 페이드 - 취소) 남은 입자를 빠르게 흡수하는 소화 연출을 시작한다. 발사(Release)는 이미 Releasing 상태다.
            if (null != rootParticleSystem && 0.1f < elapsed && VortexPhase.Charging == phase && false == rootParticleSystem.isEmitting)
            {
                phase = VortexPhase.Extinguishing;
                extinguishElapsed = 0.0f;
            }

            if (VortexPhase.Extinguishing == phase) extinguishElapsed += dt;
            if (VortexPhase.Releasing == phase) releaseElapsed += dt;

            bool bEnded = (VortexPhase.Extinguishing == phase && extinguishElapsed >= extinguishDuration) ||
                          (VortexPhase.Releasing == phase && releaseElapsed >= releaseDuration);
            if (true == bEnded && false == HasActiveParticles())
            {
                HideRenderer();
                return;
            }

            SpawnParticles(dt);
            UpdateParticles(dt);
            RebuildMesh();
        }

        private void OnDisable()
        {
            HideRenderer();
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }
    }
}
