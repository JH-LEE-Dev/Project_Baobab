using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 캐릭터 과열 상태의 "푸른 불꽃 초고열 아우라". 캐릭터의 현재 스프라이트 실루엣을 그대로 따라가는 깔끔한 외곽 빛과 푸른 불꽃 껍질을
    /// 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다(VFX_BrandStarWrap, VFX_BrandStampBurst와 같은 방식).
    /// VFX_OverHeating 프리팹의 자식으로 붙어서, 풀에서 꺼내져 활성화될 때(OnEnable) 시작하고 루트 ParticleSystem의 방출이 멈추면
    /// 소화 연출 뒤 스스로 사라진다.
    ///
    /// 동작
    /// 1. 캐릭터의 Objects 레이어 스프라이트 렌더러들(몸, 얼굴, 도끼 등)을 각자의 Transform(위치, 회전, 스케일, 반전) 그대로 합쳐
    ///    그 프레임의 실루엣 마스크를 만든다(자세가 바뀔 때만 다시 계산).
    /// 2. 마스크 바깥 각 칸이 실루엣에서 얼마나 떨어졌는지(거리)를 구한다.
    /// 3. 거리에 따라 층을 나눠 칠한다: 밝은 열 테두리(하늘색/흰빛, 고정 폭) -> 푸른 불꽃 베이스 -> 진파랑 -> 바깥 끝(보라/청록 포인트, 점이 빠져 흩어짐).
    ///    두께는 살짝만 일렁이고 위쪽이 아주 조금 더 두껍다(12fps 계단식). 진행 방향에 따라 기울거나 회전하지 않는다.
    /// 4. 실루엣 가장자리에서 `.`, `+`, `x` 불씨가 회전하며 아지랑이처럼 흔들려 올라가다 소멸한다.
    /// 5. 점화 때는 두께가 솟았다 안착하며 점선 충격파가 퍼지고, 소화 때는 두께가 줄어들며 사라진다.
    ///
    /// 색은 파랑 계열 3색 + 흰색, 포인트 보라/청록만 쓰고 반투명이 없다. 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로만 낸다.
    /// 스프라이트 마스크는 처음 쓰는 스프라이트에서만 한 번 만들고(GPU 복사 + 읽기) 이후에는 캐시를 쓴다. 아우라 본체는 12fps로만 다시 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)] // 팔/도끼 회전(ArmComponent, AxeAnimation)이 끝난 뒤의 Transform으로 실루엣을 계산하도록 다른 LateUpdate보다 늦게 실행한다
    public class VFX_OverheatAura : MonoBehaviour
    {
        [Header("렌더링")]
        [SerializeField] private Material auraMaterial;
        [SerializeField, Tooltip("캐릭터 뒤쪽 층: 루트 렌더러(캐릭터 +1) 기준 상대 소팅 오더. 아우라 본체가 여기에 그려진다")] private int backSortingOffset = -2;
        [SerializeField, Tooltip("캐릭터 앞쪽 층: 루트 렌더러(캐릭터 +1) 기준 상대 소팅 오더. 표면 불씨와 불씨가 여기에 그려진다")] private int frontSortingOffset = 1;
        [SerializeField, Tooltip("실루엣을 읽을 스프라이트 렌더러의 소팅 레이어 이름(그림자, 사거리 표시 등은 제외)")] private string sourceSortingLayerName = "Objects";

        [Header("원점 / 스냅")]
        [SerializeField, Tooltip("켜면 실루엣의 첫 스프라이트(드론은 본체)의 정확한 월드 위치를 원점으로 쓴다. 본체가 소수점 단위로 움직이거나 떠다니는 대상에 쓴다")] private bool useFirstSourceAsOrigin = false;
        [SerializeField, Tooltip("켜면 아우라 본체를 정수 픽셀 격자에 맞춰 놓는다. 끄면 원점의 정확한 위치에 놓아서 소수점 움직임도 부드럽게 따라간다")] private bool snapToPixelGrid = true;

        [Header("몸 기준 (픽셀, 발밑 피벗 기준)")]
        [SerializeField, Tooltip("몸 중심 높이(px). 이 위쪽일수록 불꽃이 조금 더 두껍다")] private int bodyCenterYPx = 8;

        [Header("외곽 아우라")]
        [SerializeField, Tooltip("몸 전체를 둘러싸는 얇은 파랑 링의 두께(px)")] private float ringThicknessPx = 2.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("링의 발광 세기. 낮을수록 은은하다")] private float ringGlow = 0.3f;
        [SerializeField, Tooltip("어깨선 위쪽에서 링 위로 솟는 불꽃의 최대 추가 두께(px)")] private float crownExtraThicknessPx = 5.5f;
        [SerializeField, Tooltip("불꽃이 시작되는 높이(발밑 기준 px). 이보다 아래는 링만 있다")] private float crownStartYPx = 8.0f;
        [SerializeField, Tooltip("불꽃이 시작 높이에서 최대 두께까지 자라는 구간(px)")] private float crownRampPx = 4.0f;
        [SerializeField, Tooltip("불꽃이 위로 올라가는 속도(노이즈 스크롤)")] private float noiseScrollSpeed = 2.0f;
        [SerializeField, Tooltip("불꽃이 일렁이는 프레임 속도(fps). 낮을수록 도트 애니메이션 느낌")] private float flickerFps = 12.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("불꽃 끝이 점으로 흩어지기 시작하는 지점(두께 대비 비율)")] private float dissolveStart = 0.6f;

        [Header("외곽 색 그라데이션 (푸른색 -> 밝은 보라 -> 밝은 청록)")]
        [SerializeField, Tooltip("색이 몸 둘레를 따라 흐르는 속도(초당 색 순환 횟수)")] private float gradientSpeed = 0.25f;
        [SerializeField, Tooltip("몸 둘레 한 바퀴에 색이 반복되는 횟수")] private float gradientCycles = 1.5f;
        [SerializeField, Range(0.0f, 0.6f), Tooltip("위치마다 색이 어긋나는 랜덤 정도")] private float gradientNoise = 0.2f;
        [SerializeField, Tooltip("색 단계 수. 낮을수록 계단식 도트 색이 뚜렷하다")] private int gradientSteps = 12;
        [SerializeField, Tooltip("밝은 띠가 몸 둘레를 한 바퀴 도는 간격(초). 0이면 없음")] private float sweepInterval = 3.0f;
        [SerializeField, Tooltip("밝은 띠가 한 바퀴 도는 데 걸리는 시간(초)")] private float sweepDuration = 1.1f;
        [SerializeField, Range(0.05f, 0.5f), Tooltip("밝은 띠의 폭(몸 둘레 대비 비율)")] private float sweepWidth = 0.18f;

        [Header("불씨")]
        [SerializeField, Tooltip("실루엣 가장자리에서 프레임마다 튀는 밝은 불씨 개수. 0이면 없음")] private int sparkleCount = 2;
        [SerializeField, Tooltip("몸 주변에서 피어오르는 불씨 간격(초). 작을수록 많다")] private float ambientEmberInterval = 0.055f;
        [SerializeField, Tooltip("불씨가 아지랑이처럼 좌우로 흔들리는 폭(px/s)")] private float emberSwayAmplitude = 9.0f;
        [SerializeField, Tooltip("이 이동 속도(px/s) 이상이면 불씨가 최대로 많이 나오고 수명이 짧아진다(꼬리 길이 제한)")] private float emberTrailFullSpeedPx = 100.0f;
        [SerializeField, Range(0.2f, 1.0f), Tooltip("최대 속도일 때 불씨 수명 배율(작을수록 이동 꼬리가 짧다)")] private float emberTrailLifeScale = 0.6f;

        [Header("점화 (드론처럼 작은 대상은 줄여서 쓴다)")]
        [SerializeField, Tooltip("점화 순간 점선 충격파의 크기 배율")] private float igniteRingScale = 1.0f;
        [SerializeField, Tooltip("점화 순간 사방으로 터지는 불씨 개수")] private int igniteEmberCount = 16;

        [Header("소화")]
        [SerializeField] private float extinguishDuration = 0.45f;

        [Header("밝기 (정점 알파 = HDR 발광 세기 0~1)")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("전체 발광 배율. 1이면 기준값")] private float glowScale = 1.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("캐릭터 뒤쪽 층(아우라 본체)의 발광 배율")] private float backGlowScale = 1.0f;

        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / PixelsPerUnit;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const int BackLayer = 0;
        private const int FrontLayer = 1;
        private const int AuraLayer = 2; // 아우라 본체 전용 메쉬(12fps로만 다시 만든다). 캐릭터 뒤쪽 층과 같은 소팅을 쓴다.
        private const int LayerCount = 3;
        private const int EmberCapacity = 96;
        private const float MaxIntensity = 1.6f;      // GetIntensity의 최댓값(안착 1.0 + 플레어 0.6). 실루엣 거리장의 계산 여백 산정에 쓴다.
        private const float RingNoiseAmplitude = 0.6f; // 링 두께 노이즈 폭(px, +-0.3)
        private const float BodyBleedPx = 0.3f;
        private const int MeshBoundsHalfSize = 100;
        private static readonly Vector3 MeshBoundsSize = new Vector3(MeshBoundsHalfSize, MeshBoundsHalfSize, 10.0f);
        private const int GridWidth = OverheatSilhouette.GridWidth;
        private const int GridHeight = OverheatSilhouette.GridHeight;
        private const int GridOriginX = OverheatSilhouette.GridOriginX;
        private const int GridOriginY = OverheatSilhouette.GridOriginY;
        private const int DistanceUnitsPerPixel = OverheatSilhouette.DistanceUnitsPerPixel;

        private enum AuraPhase
        {
            Burning,
            Extinguishing,
        }

        private struct Ember
        {
            public bool bActive;
            public int layer;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float swayPhase;
            public float swaySpeed;
            public float age;
            public float life;
            public int kind; // 0: `.`, 1: `+`/`x`(번갈아 회전)
            public int seed;
            public Color32 color;
        }

        // 한 프레임의 아우라 그리기에 공통으로 쓰는 값(DrawAura가 한 번 계산해서 칸마다 넘긴다)
        private struct AuraFrame
        {
            public float flickerTime;
            public int flickerFrame;
            public float intensity;
            public float dissolve;
            public float ringThickness;
            public float colorTime;
            public bool bSweep;
            public float sweepFront;
            public Color32 sweepColor;
            public int maxDistanceUnits;
        }

        // 팔레트: 파랑 계열 3색 + 흰색, 포인트는 보라와 청록
        // HDR 배율이 곱해져 한 채널이 1을 넘어도 나머지 채널이 함께 1을 넘어 흰색으로 날아가지 않도록, 파랑/보라는 기본 색을 어둡게 잡았다.
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(120, 196, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(14, 64, 200, 255);
        private static readonly Color32 ColorDeepBlue = new Color32(20, 52, 190, 255);
        private static readonly Color32 ColorViolet = new Color32(51, 23, 158, 255);
        private static readonly Color32 ColorCyan = new Color32(30, 235, 215, 255);

        // 외곽 그라데이션의 순환 팔레트: 푸른색 -> 밝은 보라 -> 밝은 청록 -> (다시 푸른색)
        private static readonly Color32[] GradientAnchors = { ColorBlue, ColorViolet, ColorCyan };

        // 메쉬 버퍼는 매 프레임 Clear 후 채우므로 인스턴스끼리 공유해도 안전하다(BackLayer, FrontLayer, AuraLayer 순)
        private static readonly PixelQuadBuffer[] quadBuffers = { new PixelQuadBuffer(512), new PixelQuadBuffer(512), new PixelQuadBuffer(1024) };

        //내부 의존성
        private readonly MeshFilter[] meshFilters = new MeshFilter[LayerCount];
        private readonly MeshRenderer[] meshRenderers = new MeshRenderer[LayerCount];
        private readonly Mesh[] meshes = new Mesh[LayerCount];
        private readonly bool[] meshHasGeometry = new bool[LayerCount];
        private ParticleSystem rootParticleSystem;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly OverheatSilhouette silhouette = new OverheatSilhouette();
        private readonly Ember[] embers = new Ember[EmberCapacity];

        //상태 변수
        private Transform sourceRootOverride;
        private AuraPhase phase;
        private float elapsed;
        private float extinguishElapsed;
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bIgniteEmbersSpawned;
        private float emberSpawnAccumulator;
        private Vector2 characterWorldPx;     // 캐릭터(이 컴포넌트) 위치(월드 px). 불씨는 월드 좌표로 움직여서 이동하면 뒤에 꼬리처럼 남는다.
        private Vector2 lastCharacterWorldPx;
        private float moveSpeedPx;            // 부드럽게 평균 낸 이동 속도(px/s)
        private int maskKey;
        private bool bMaskBuilt;
        private bool bAuraDirty;
        private int lastAuraFrame;
        private float lastAuraIntensity;
        private int emberCursor;
        private uint randomState;
        private int originX;
        private int originY;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            string[] names = { "OverheatAura_Back", "OverheatAura_Front", "OverheatAura_Body" };
            for (int i = 0; i < LayerCount; i++)
            {
                GameObject child = new GameObject(names[i]);
                child.transform.SetParent(transform, false);

                meshFilters[i] = child.AddComponent<MeshFilter>();
                meshRenderers[i] = child.AddComponent<MeshRenderer>();

                meshes[i] = new Mesh { name = "VFX_OverheatAura_" + names[i] };
                meshes[i].MarkDynamic();
                meshFilters[i].sharedMesh = meshes[i];

                meshRenderers[i].sharedMaterial = auraMaterial;
                meshRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderers[i].receiveShadows = false;
                meshRenderers[i].enabled = false;
            }
        }

        private void Begin()
        {
            EnsureInitialized();

            // 풀에서 재사용되므로 이전에 지정한 대상은 잊는다(다시 SetSourceRoot가 불리거나 부모 계층에서 찾는다)
            sourceRootOverride = null;

            phase = AuraPhase.Burning;
            elapsed = 0.0f;
            extinguishElapsed = 0.0f;
            bFirstFrame = true;
            bIgniteEmbersSpawned = false;
            emberSpawnAccumulator = 0.0f;
            moveSpeedPx = 0.0f;
            emberCursor = 0;
            bMaskBuilt = false;
            bAuraDirty = true;
            lastAuraFrame = -1;
            lastAuraIntensity = -1.0f;

            for (int i = 0; i < EmberCapacity; i++) embers[i].bActive = false;

            Vector3 pos = transform.position;
            characterWorldPx = new Vector2(pos.x * PixelsPerUnit, pos.y * PixelsPerUnit);
            lastCharacterWorldPx = characterWorldPx;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f));
            if (0u == randomState) randomState = 1u;

            CollectSources();
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // 정수 해시 기반 0~1 (프레임에 고정된 값이 필요한 곳용)
        private static float Hash01(int _a, int _b)
        {
            uint h = unchecked((uint)(_a * 73856093) ^ (uint)(_b * 19349663));
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535.0f;
        }

        // 격자점 해시를 부드럽게 보간한 값 노이즈(0~1)
        private static float ValueNoise(float _x, float _y)
        {
            int ix = Mathf.FloorToInt(_x);
            int iy = Mathf.FloorToInt(_y);
            float fx = _x - ix;
            float fy = _y - iy;
            fx = fx * fx * (3.0f - 2.0f * fx);
            fy = fy * fy * (3.0f - 2.0f * fy);

            float a = Hash01(ix, iy);
            float b = Hash01(ix + 1, iy);
            float c = Hash01(ix, iy + 1);
            float d = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ---------- 실루엣 ----------

        /// <summary>
        /// 실루엣을 읽을 대상의 루트를 직접 지정한다. 아우라 이펙트가 대상의 자식이 아니라 월드에서 따로 재생되는 경우(드론 - 드론이 스폰 연출로
        /// 스케일이 바뀌어서 자식으로 붙이지 않는다)에 재생 직후 호출한다. 이미 시작한 뒤라면 소스를 바로 다시 모으고 실루엣을 다시 만든다.
        /// </summary>
        public void SetSourceRoot(Transform _root)
        {
            sourceRootOverride = _root;
            CollectSources();
            bMaskBuilt = false;
        }

        // 대상 아래의 Objects 레이어 스프라이트 렌더러들을 모은다(캐릭터의 몸/얼굴/도끼, 드론 본체 등).
        // 대상은 SetSourceRoot로 지정된 루트, 없으면 가장 가까운 Character, 그것도 없으면 Drone, 마지막으로 최상위 루트다.
        private void CollectSources()
        {
            Transform root = sourceRootOverride;
            if (null == root)
            {
                Character character = GetComponentInParent<Character>();
                Drone drone = null == character ? GetComponentInParent<Drone>() : null;
                root = null != character ? character.transform : (null != drone ? drone.transform : transform.root);
            }

            silhouette.CollectSources(root, sourceSortingLayerName);
        }

        // 아우라의 원점 월드 위치. 기본은 이 컴포넌트의 위치(캐릭터 발밑)이고, useFirstSourceAsOrigin이 켜져 있으면 켜진 첫 소스 스프라이트의 정확한 위치다.
        private Vector3 GetOriginPosition()
        {
            return true == useFirstSourceAsOrigin ? silhouette.GetFirstSourcePosition(transform.position) : transform.position;
        }

        private int ComputeMaskKey()
        {
            return silhouette.ComputeKey(GetOriginPosition());
        }

        // 지금 설정에서 아우라가 실루엣 밖으로 뻗을 수 있는 최대 거리(칸). 거리장은 이 여백 안에서만 계산한다.
        private int GetReachMarginCells()
        {
            float maxReach = (Mathf.Max(1.0f, ringThicknessPx) + BodyBleedPx + crownExtraThicknessPx) * MaxIntensity;
            return Mathf.CeilToInt(maxReach) + 1;
        }

        private void BuildSilhouette()
        {
            silhouette.Build(GetOriginPosition(), GetReachMarginCells());
        }

        // ---------- 갱신 ----------

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        // 불꽃 껍질의 전체 세기(0~1+). 점화 때는 잠깐 솟았다(플레어) 안착하고, 소화 때는 줄어든다. 1/12 단위 계단식이라 도트 애니메이션처럼 보인다.
        private float GetIntensity()
        {
            float rise = EaseOut(Mathf.Clamp01(elapsed / 0.35f));
            float flare = 0.6f * Mathf.Max(0.0f, 1.0f - Mathf.Abs(elapsed - 0.14f) / 0.14f);
            float intensity = rise + flare;

            if (AuraPhase.Extinguishing == phase)
            {
                float u = Mathf.Clamp01(extinguishElapsed / Mathf.Max(0.01f, extinguishDuration));
                intensity *= Mathf.Pow(1.0f - u, 1.4f);
            }

            return Mathf.Floor(intensity * 12.0f) / 12.0f;
        }

        private void SpawnEmber(float _x, float _y, float _vx, float _vy, float _life, int _layer, Color32 _color)
        {
            // 불씨는 월드 좌표(px)로 움직인다. 캐릭터가 달리면 불씨가 그 자리에 남아서 뒤로 꼬리처럼 늘어진다(방향을 따로 계산하지 않는다).
            // 빠르게 달릴수록 수명을 줄여서 꼬리가 너무 길어지지 않게 한다.
            float speedFactor = Mathf.Clamp01(moveSpeedPx / Mathf.Max(1.0f, emberTrailFullSpeedPx));

            Ember ember = new Ember();
            ember.bActive = true;
            ember.layer = _layer;
            ember.x = characterWorldPx.x + _x;
            ember.y = characterWorldPx.y + _y;
            ember.vx = _vx;
            ember.vy = _vy;
            ember.swayPhase = Rand01() * TwoPi;
            ember.swaySpeed = Mathf.Lerp(5.0f, 9.0f, Rand01());
            ember.age = 0.0f;
            ember.life = _life * Mathf.Lerp(1.0f, emberTrailLifeScale, speedFactor);
            ember.kind = Rand01() < 0.45f ? 1 : 0;
            ember.seed = (int)(Rand01() * 1000.0f);
            ember.color = _color;

            embers[emberCursor] = ember;
            emberCursor = (emberCursor + 1) % EmberCapacity;
        }

        private Color32 RandomEmberColor()
        {
            float roll = Rand01();
            if (roll < 0.28f) return ColorCyan;
            if (roll < 0.5f) return ColorViolet;
            if (roll < 0.82f) return ColorIce;
            return ColorWhite;
        }

        // 불씨는 위로 올라가면서 좌우로 아지랑이처럼 흔들린다(사인 흔들림). 위로 갈수록 느려진다.
        private void UpdateEmbers(float _dt)
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                embers[i].age += _dt;
                if (embers[i].age >= embers[i].life)
                {
                    embers[i].bActive = false;
                    continue;
                }

                float sway = Mathf.Sin(embers[i].age * embers[i].swaySpeed + embers[i].swayPhase) * emberSwayAmplitude;
                embers[i].vy *= Mathf.Max(0.0f, 1.0f - 0.9f * _dt);
                embers[i].x += (embers[i].vx + sway) * _dt;
                embers[i].y += embers[i].vy * _dt;
            }
        }

        private void SpawnEmbers(float _dt)
        {
            // 점화 직후 몸에서 사방으로 터지는 불씨
            if (false == bIgniteEmbersSpawned && 0.05f <= elapsed)
            {
                bIgniteEmbersSpawned = true;
                int igniteCount = Mathf.Max(0, igniteEmberCount);
                for (int i = 0; i < igniteCount; i++)
                {
                    float angle = TwoPi * (i + Rand01()) / igniteCount;
                    float speed = Mathf.Lerp(16.0f, 44.0f, Rand01());
                    SpawnEmber(Mathf.Cos(angle) * 4.0f, bodyCenterYPx + Mathf.Sin(angle) * 5.0f, Mathf.Cos(angle) * speed, 10.0f + Mathf.Abs(Mathf.Sin(angle)) * speed, Mathf.Lerp(0.4f, 0.75f, Rand01()), FrontLayer, RandomEmberColor());
                }
            }

            int edgeCellCount = silhouette.EdgeCellCount;
            if (AuraPhase.Extinguishing == phase || 0 >= edgeCellCount) return;
            int[] edgeCells = silhouette.EdgeCells;

            // 실루엣 가장자리에서 피어오르는 불씨(위쪽 가장자리일수록 잘 나오게 한다). 달리는 중에는 더 많이 나와서 꼬리가 촘촘해진다.
            float speedFactor = Mathf.Clamp01(moveSpeedPx / Mathf.Max(1.0f, emberTrailFullSpeedPx));
            float interval = Mathf.Max(0.02f, ambientEmberInterval) / (1.0f + speedFactor * 1.5f);
            emberSpawnAccumulator += _dt / interval;
            int groups = Mathf.Min(6, (int)emberSpawnAccumulator);
            emberSpawnAccumulator -= (int)emberSpawnAccumulator;

            for (int g = 0; g < groups; g++)
            {
                for (int n = 0; n < 2; n++)
                {
                    int cell = edgeCells[(int)(Rand01() * edgeCellCount) % edgeCellCount];
                    int cx = cell % GridWidth - GridOriginX;
                    int cy = cell / GridWidth - GridOriginY;

                    float upward = Mathf.Clamp01((cy - 2.0f) / 14.0f);
                    if (Rand01() > Mathf.Lerp(0.4f, 1.0f, upward)) continue;

                    SpawnEmber(cx, cy + 1.0f, (Rand01() - 0.5f) * 6.0f, Mathf.Lerp(12.0f, 26.0f, Rand01()), Mathf.Lerp(0.5f, 0.95f, Rand01()), Rand01() < 0.5f ? FrontLayer : BackLayer, RandomEmberColor());
                }
            }
        }

        // 캐릭터의 월드 위치와 이동 속도를 갱신한다. 한 프레임에 크게 튄 위치(던전 입장, 텔레포트)는 이동으로 보지 않고 남은 불씨를 지운다.
        private void UpdateMovement(float _dt)
        {
            Vector3 position = GetOriginPosition();
            characterWorldPx = new Vector2(position.x * PixelsPerUnit, position.y * PixelsPerUnit);

            float jump = (characterWorldPx - lastCharacterWorldPx).magnitude;
            if (40.0f < jump)
            {
                for (int i = 0; i < EmberCapacity; i++) embers[i].bActive = false;
                moveSpeedPx = 0.0f;
            }
            else if (0.0001f < _dt)
            {
                float speed = jump / _dt;
                moveSpeedPx = Mathf.Lerp(moveSpeedPx, speed, 1.0f - Mathf.Exp(-10.0f * _dt));
            }

            lastCharacterWorldPx = characterWorldPx;
        }

        // ---------- 그리기 ----------

        private void RebuildMeshes()
        {
            // 불씨/충격파/표면 불씨는 매 프레임 다시 만든다(앞/뒤 두 메쉬)
            Vector3 pos = GetOriginPosition();
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            originX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            originY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            for (int i = 0; i < AuraLayer; i++)
            {
                quadBuffers[i].Clear();
                quadBuffers[i].SetWorldTransform(worldToLocal, pos.z);
            }

            // 프레임 단위 시간(12fps 등) - 불꽃이 부드럽게 흐르지 않고 도트 애니메이션처럼 끊겨서 일렁인다
            float fps = Mathf.Max(1.0f, flickerFps);
            int flickerFrame = (int)(elapsed * fps);
            float flickerTime = flickerFrame / fps;

            DrawIgniteRing();
            DrawSparkles(flickerFrame);
            DrawEmbers();

            // 아우라 본체는 12fps 프레임이 바뀌거나, 실루엣/세기가 바뀔 때만 다시 만든다(움직임에 따른 위치 이동은 오브젝트 위치로 처리한다).
            float intensity = GetIntensity();
            bool bRebuildAura = true == bAuraDirty || flickerFrame != lastAuraFrame || false == Mathf.Approximately(intensity, lastAuraIntensity);
            if (true == bRebuildAura)
            {
                quadBuffers[AuraLayer].Clear();
                DrawAura(flickerTime, flickerFrame, intensity);
                bAuraDirty = false;
                lastAuraFrame = flickerFrame;
                lastAuraIntensity = intensity;
            }

            // 아우라 메쉬 오브젝트는 캐릭터의 픽셀 스냅 위치에 둔다: 정점은 발밑 기준 픽셀 오프셋이라 캐릭터가 서브픽셀로 움직여도 격자에서 벗어나지 않는다.
            Transform auraTransform = meshRenderers[AuraLayer].transform;
            // 스냅을 끄면 원점의 정확한 위치에 둔다(스프라이트가 소수점 단위로 움직일 때 아우라만 1px씩 끊겨 따라오지 않도록)
            auraTransform.position = true == snapToPixelGrid ? new Vector3(originX * PixelUnit, originY * PixelUnit, pos.z) : pos;
            auraTransform.rotation = Quaternion.identity;

            if (null != rootParticleRenderer)
            {
                int rootOrder = rootParticleRenderer.sortingOrder;
                for (int i = 0; i < LayerCount; i++)
                {
                    meshRenderers[i].sortingLayerID = rootParticleRenderer.sortingLayerID;
                    meshRenderers[i].sortingOrder = rootOrder + (FrontLayer == i ? frontSortingOffset : backSortingOffset);
                }
            }

            for (int i = 0; i < LayerCount; i++)
            {
                if (AuraLayer == i && false == bRebuildAura)
                {
                    meshRenderers[i].enabled = true;
                    continue;
                }

                // 그릴 것이 없고 메쉬도 이미 비어 있으면 올리지 않는다
                bool bEmpty = 0 == quadBuffers[i].VertexCount;
                if (false == bEmpty || true == meshHasGeometry[i])
                {
                    quadBuffers[i].Upload(meshes[i], MeshBoundsSize);
                    meshHasGeometry[i] = false == bEmpty;
                }

                meshRenderers[i].enabled = true;
            }
        }

        private byte GlowAlpha(float _glow, int _layer)
        {
            float g = _glow * glowScale * (FrontLayer == _layer ? 1.0f : backGlowScale);
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255.0f);
        }

        private Color32 Tint(Color32 _color, float _glow, int _layer)
        {
            return new Color32(_color.r, _color.g, _color.b, GlowAlpha(_glow, _layer));
        }

        // 순환 팔레트(푸른색 -> 보라 -> 청록 -> 푸른색)에서 위상(0~1, 반복)에 해당하는 색을 고른다. 단계 수(gradientSteps)로 양자화해서
        // 부드러운 그라데이션이 아니라 계단식 도트 색이 되고, 같은 단계가 이어지면 하나의 직사각형으로 합쳐진다. _shade는 어둡게 하는 배율(0~1).
        private Color32 GetGradientColor(float _phase, float _shade)
        {
            float steps = Mathf.Max(2, gradientSteps);
            float wrapped = _phase - Mathf.Floor(_phase);
            float stepped = (Mathf.Floor(wrapped * steps) + 0.5f) / steps * GradientAnchors.Length;

            int index = Mathf.FloorToInt(stepped) % GradientAnchors.Length;
            float blend = stepped - Mathf.Floor(stepped);
            Color32 mixed = Color32.Lerp(GradientAnchors[index], GradientAnchors[(index + 1) % GradientAnchors.Length], blend);

            return new Color32((byte)(mixed.r * _shade), (byte)(mixed.g * _shade), (byte)(mixed.b * _shade), 255);
        }

        // 외곽 아우라 본체: 실루엣 바깥 칸을 두 부분으로 칠한다. 같은 줄에서 이어지는 같은 색 칸은 하나의 직사각형으로 합친다.
        // - 링: 몸 전체를 둘러싸는 얇은 파랑 한 색의 빛(안쪽이 파랑, 바깥이 진파랑, 가장 바깥은 체크무늬 디더로 옅어짐). 흰색 테두리는 쓰지 않는다.
        // - 불꽃: 어깨선 위쪽 칸에만 링 위로 솟는 푸른 불꽃(코어 청록/하늘 -> 파랑 -> 끝 보라 포인트, 순환 그라데이션). 방향은 항상 위쪽이다.
        // 거리장은 실루엣 주변 영역(Region)에서만 계산되므로 그 안쪽 칸만 훑는다. 영역 밖은 어차피 아우라가 닿지 않는 거리다.
        private void DrawAura(float _flickerTime, int _flickerFrame, float _intensity)
        {
            if (_intensity <= 0.0f || false == silhouette.HasSolid) return;

            AuraFrame frame = CreateAuraFrame(_flickerTime, _flickerFrame, _intensity);

            int regionMinX = silhouette.RegionMinX;
            int regionMaxX = silhouette.RegionMaxX;

            for (int gy = silhouette.RegionMinY; gy <= silhouette.RegionMaxY; gy++)
            {
                int cy = gy - GridOriginY;

                // 어깨선 위로 올라갈수록 불꽃 비중(0~1)이 커진다. 어깨선 아래는 링만 있다.
                float crown = Mathf.Clamp01((cy - crownStartYPx) / Mathf.Max(1.0f, crownRampPx));

                int runStart = -1;
                Color32 runColor = default(Color32);
                bool bRunHasColor = false;

                for (int gx = regionMinX; gx <= regionMaxX + 1; gx++)
                {
                    Color32 cellColor = default(Color32);
                    bool bDraw = gx <= regionMaxX && TryGetAuraCellColor(in frame, gx, gy, crown, out cellColor);

                    // 이어지는 같은 색을 하나의 직사각형으로 합친다
                    bool bSameAsRun = true == bDraw && true == bRunHasColor && runColor.r == cellColor.r && runColor.g == cellColor.g && runColor.b == cellColor.b && runColor.a == cellColor.a;
                    if (true == bRunHasColor && false == bSameAsRun)
                    {
                        AddRect(AuraLayer, runStart - GridOriginX, cy, gx - GridOriginX, cy + 1, runColor);
                        bRunHasColor = false;
                    }

                    if (true == bDraw && false == bRunHasColor)
                    {
                        runStart = gx;
                        runColor = cellColor;
                        bRunHasColor = true;
                    }
                }
            }
        }

        // 한 프레임 동안 변하지 않는 그리기 값(색 흐름 시간, 밝은 띠 위치, 닿을 수 있는 최대 거리)을 한 번만 계산한다.
        private AuraFrame CreateAuraFrame(float _flickerTime, int _flickerFrame, float _intensity)
        {
            AuraFrame frame = new AuraFrame();
            frame.flickerTime = _flickerTime;
            frame.flickerFrame = _flickerFrame;
            frame.intensity = _intensity;
            frame.dissolve = Mathf.Clamp(dissolveStart, 0.05f, 0.95f);
            frame.ringThickness = Mathf.Max(1.0f, ringThicknessPx);

            // 불꽃 색 그라데이션: 몸 둘레의 각도 + 시간 + 노이즈로 순환 팔레트(푸른색 -> 보라 -> 청록)에서 색을 고른다.
            // 일정 간격마다 밝은 띠가 불꽃 위로 지나가며 보라/청록을 번갈아 더 진하고 밝게 물들인다.
            frame.colorTime = elapsed * gradientSpeed;
            frame.bSweep = false;
            frame.sweepFront = 0.0f;
            frame.sweepColor = ColorViolet;
            if (0.1f < sweepInterval && 0.05f < sweepDuration)
            {
                float sweepTime = Mathf.Repeat(elapsed, sweepInterval);
                if (sweepTime < sweepDuration)
                {
                    frame.bSweep = true;
                    frame.sweepFront = sweepTime / sweepDuration;
                    frame.sweepColor = 0 == ((int)(elapsed / sweepInterval) & 1) ? ColorViolet : ColorCyan;
                }
            }

            // 지금 세기로는 어느 칸도 닿을 수 없는 거리(링 + 불꽃 최대 두께). 이보다 먼 칸은 노이즈를 계산하지 않고 건너뛴다.
            float maxReach = (frame.ringThickness + BodyBleedPx + crownExtraThicknessPx) * _intensity;
            frame.maxDistanceUnits = Mathf.CeilToInt(maxReach * DistanceUnitsPerPixel);
            return frame;
        }

        // 격자 칸 하나의 아우라 색. 그릴 칸이면 true.
        private bool TryGetAuraCellColor(in AuraFrame _frame, int _gx, int _gy, float _crown, out Color32 _color)
        {
            _color = default(Color32);

            int index = _gy * GridWidth + _gx;
            int dist = silhouette.Distance[index];

            if (0 == dist && true == silhouette.EdgeMask[index])
            {
                // 실루엣 가장자리 안쪽 칸에도 링의 파랑을 깐다. 아우라는 캐릭터 뒤 층이라 스프라이트가 덮으면 가려지고,
                // 회전한 도끼날처럼 계산과 실제 픽셀이 반 픽셀쯤 어긋나 스프라이트가 비는 칸에서는 링이 드러나 빈틈이 메워진다.
                _color = Tint(ColorBlue, ringGlow, AuraLayer);
                return true;
            }

            if (0 >= dist || dist > _frame.maxDistanceUnits) return false;

            float d = dist / (float)DistanceUnitsPerPixel;
            int cx = _gx - GridOriginX;
            int cy = _gy - GridOriginY;

            // 위로 올라가는 노이즈: 큰 덩어리(불꽃 혀) + 잔 일렁임
            float n1 = ValueNoise(cx * 0.34f, cy * 0.21f - _frame.flickerTime * noiseScrollSpeed);
            float n2 = ValueNoise(cx * 0.75f + 31.0f, cy * 0.46f - _frame.flickerTime * noiseScrollSpeed * 2.0f);

            // 링은 거의 고정 두께(노이즈 +-0.3px), 불꽃은 어깨선 위에서만 노이즈만큼 더 뻗는다
            float ring = (_frame.ringThickness + (n2 - 0.5f) * RingNoiseAmplitude) * _frame.intensity;
            float flame = _crown * crownExtraThicknessPx * (0.55f * n1 + 0.45f * n2) * _frame.intensity;
            float thick = ring + flame;
            if (d > thick || 0.0f >= thick) return false;

            if (d <= ring) return TryGetRingColor(d, ring, _gx, _gy, out _color);
            return TryGetFlameColor(in _frame, d, ring, thick, index, cx, cy, n2, out _color);
        }

        // 링: 한 가지 깊은 파랑. 안쪽 1px는 파랑, 그 바깥은 진파랑, 가장 바깥 절반은 체크무늬 디더로 옅어진다.
        private bool TryGetRingColor(float _d, float _ring, int _gx, int _gy, out Color32 _color)
        {
            if (_d <= 1.0f)
            {
                _color = Tint(ColorBlue, ringGlow, AuraLayer);
                return true;
            }

            if (_d <= _ring - 0.5f || 0 == ((_gx + _gy) & 1))
            {
                _color = Tint(ColorDeepBlue, ringGlow * 0.5f, AuraLayer);
                return true;
            }

            _color = default(Color32);
            return false;
        }

        // 불꽃: 링 바깥의 남은 두께에 대한 비율(r2)로 코어/중간/끝을 나눈다. 끝은 점이 무작위로 빠져 흩어진다(반투명 없음).
        private bool TryGetFlameColor(in AuraFrame _frame, float _d, float _ring, float _thick, int _index, int _cx, int _cy, float _n2, out Color32 _color)
        {
            _color = default(Color32);

            float accent = Hash01(_index + 977, _frame.flickerFrame / 2);
            float r2 = (_d - _ring) / Mathf.Max(0.01f, _thick - _ring);
            float dissolve = _frame.dissolve;
            bool bDissolved = r2 > dissolve && Hash01(_index, _frame.flickerFrame) < (r2 - dissolve) / (1.0f - dissolve) * 1.1f;
            if (true == bDissolved) return false;

            // 몸 둘레의 각도(0~1)와 그 칸의 색 위상
            float angle01 = Mathf.Atan2(_cy - bodyCenterYPx, _cx) / TwoPi + 0.5f;
            float phase = angle01 * gradientCycles + _frame.colorTime + (_n2 - 0.5f) * gradientNoise * 2.0f;

            float glow;
            Color32 color;
            if (r2 < 0.3f)
            {
                color = GetGradientColor(phase, 1.0f);
                glow = 0.7f;
            }
            else if (r2 < 0.7f)
            {
                color = GetGradientColor(phase + 0.33f, 0.85f);
                glow = 0.3f;
            }
            else
            {
                // 바깥 끝은 밝은 포인트 색을 띄엄띄엄
                bool bPoint = accent < 0.4f;
                color = GetGradientColor(phase + 0.66f, true == bPoint ? 1.0f : 0.7f);
                glow = true == bPoint ? 0.75f : 0.15f;
            }

            // 밝은 띠: 각도 앞선이 한 바퀴 돌며 지나가는 불꽃 자리를 물들인다
            if (true == _frame.bSweep)
            {
                float delta = Mathf.Abs(angle01 - _frame.sweepFront);
                delta = Mathf.Min(delta, 1.0f - delta);
                float sweepStrength = Mathf.Clamp01(1.0f - delta / sweepWidth);
                if (sweepStrength > 0.0f)
                {
                    color = Color32.Lerp(color, _frame.sweepColor, sweepStrength * 0.85f);
                    glow = Mathf.Min(1.0f, glow + 0.3f * sweepStrength);
                }
            }

            _color = Tint(color, glow, AuraLayer);
            return true;
        }

        // 실루엣 가장자리에서 프레임마다 튀는 밝은 불씨(몸 표면에서 에너지가 튀는 느낌). 앞 층에 그려서 몸 위에 얹는다.
        private void DrawSparkles(int _flickerFrame)
        {
            int edgeCellCount = silhouette.EdgeCellCount;
            if (0 >= edgeCellCount || AuraPhase.Extinguishing == phase || 0 >= sparkleCount) return;
            int[] edgeCells = silhouette.EdgeCells;

            float ramp = Mathf.Clamp01((elapsed - 0.1f) / 0.3f);
            int count = Mathf.RoundToInt(sparkleCount * ramp);
            for (int i = 0; i < count; i++)
            {
                int cell = edgeCells[(int)(Hash01(i * 13 + 7, _flickerFrame) * edgeCellCount) % edgeCellCount];
                int cx = cell % GridWidth - GridOriginX;
                int cy = cell / GridWidth - GridOriginY;

                bool bCyan = Hash01(i, _flickerFrame + 91) < 0.4f;
                AddRect(FrontLayer, cx, cy, cx + 1, cy + 1, Tint(true == bCyan ? ColorCyan : ColorWhite, 0.9f, FrontLayer));
            }
        }

        // 점화 순간의 점선 충격파: 발밑 타원으로 계단식으로 퍼지는 도트 고리
        private void DrawIgniteRing()
        {
            float t = elapsed - 0.02f;
            if (0.0f > t || 0.36f <= t) return;

            int step = (int)(t / 0.06f);
            float radius = (5.0f + step * 4.5f) * igniteRingScale;
            Color32 baseColor = step < 2 ? ColorWhite : (step < 4 ? ColorIce : ColorBlue);
            float glow = step < 2 ? 0.9f : (step < 4 ? 0.65f : 0.35f);
            int size = step < 3 ? 2 : 1;
            int dotCount = 16;
            float phaseShift = 0 == (step & 1) ? 0.0f : TwoPi / dotCount * 0.5f;

            for (int i = 0; i < dotCount; i++)
            {
                float angle = phaseShift + TwoPi * i / dotCount;
                float sin = Mathf.Sin(angle);
                int layer = sin < 0.0f ? FrontLayer : BackLayer;
                int px = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int py = Mathf.RoundToInt(-1.0f + sin * radius * 0.42f);
                AddRect(layer, px, py, px + size, py + size, Tint(baseColor, glow, layer));
            }
        }

        // 불씨: 위로 올라가며 좌우로 흔들리고, `+`와 `x`는 번갈아 바뀌며 회전하는 것처럼 보인다. 후반에는 `.`로 작아졌다가 점멸하며 소멸한다.
        private void DrawEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                float u = embers[i].age / embers[i].life;

                // 후반에는 프레임을 건너뛰며 점멸하다가 사라진다
                if (0.75f < u && 0 != ((int)(embers[i].age * 30.0f) & 1)) continue;

                // 월드 좌표를 캐릭터의 스냅 위치(originX/Y) 기준 픽셀 오프셋으로 바꾼다(격자에 스냅)
                int px = Mathf.RoundToInt(embers[i].x - originX);
                int py = Mathf.RoundToInt(embers[i].y - originY);

                int twinkle = ((int)(embers[i].age * 14.0f) + embers[i].seed) % 3;
                float glow = (0 == twinkle ? 0.5f : (1 == twinkle ? 0.7f : 0.9f)) * (1.0f - u * 0.4f);
                Color32 baseColor = 0.8f < u ? ColorBlue : embers[i].color;
                Color32 color = Tint(baseColor, glow, embers[i].layer);

                if (1 == embers[i].kind && u < 0.55f)
                {
                    // 0.09초마다 `+` <-> `x`로 바뀌어 회전하는 것처럼 보인다
                    bool bPlus = 0 == (((int)(embers[i].age / 0.09f)) + embers[i].seed & 1);
                    if (true == bPlus)
                    {
                        AddRect(embers[i].layer, px - 1, py, px + 2, py + 1, color);
                        AddRect(embers[i].layer, px, py - 1, px + 1, py + 2, color);
                    }
                    else
                    {
                        AddRect(embers[i].layer, px, py, px + 1, py + 1, color);
                        AddRect(embers[i].layer, px - 1, py - 1, px, py, color);
                        AddRect(embers[i].layer, px + 1, py - 1, px + 2, py, color);
                        AddRect(embers[i].layer, px - 1, py + 1, px, py + 2, color);
                        AddRect(embers[i].layer, px + 1, py + 1, px + 2, py + 2, color);
                    }
                }
                else
                {
                    AddRect(embers[i].layer, px, py, px + 1, py + 1, color);
                }
            }
        }

        // 발밑(피벗) 기준 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 쿼드로 추가한다.
        // 아우라 본체 메쉬는 오브젝트가 캐릭터의 스냅 위치에 있어서 발밑 기준 픽셀 오프셋 그대로 쓰고, 나머지는 월드 -> 로컬 변환으로 넣는다.
        private void AddRect(int _layer, int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (AuraLayer == _layer)
            {
                quadBuffers[_layer].AddLocalRect(_x0, _y0, _x1, _y1, _color);
            }
            else
            {
                quadBuffers[_layer].AddWorldRect(originX + _x0, originY + _y0, originX + _x1, originY + _y1, _color);
            }
        }

        private void HideRenderers()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                if (null != meshRenderers[i]) meshRenderers[i].enabled = false;
            }
        }

        private bool HasActiveEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (true == embers[i].bActive) return true;
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

            // 루트 ParticleSystem의 방출이 멈추면(OverheatComponent.Stop) 소화 연출을 시작한다. 소화 중에 다시 켜지면 되살린다.
            if (null != rootParticleSystem && 0.1f < elapsed)
            {
                if (AuraPhase.Burning == phase && false == rootParticleSystem.isEmitting)
                {
                    phase = AuraPhase.Extinguishing;
                    extinguishElapsed = 0.0f;
                }
                else if (AuraPhase.Extinguishing == phase && true == rootParticleSystem.isEmitting)
                {
                    phase = AuraPhase.Burning;
                }
            }

            if (AuraPhase.Extinguishing == phase)
            {
                extinguishElapsed += dt;
                if (extinguishElapsed >= extinguishDuration && false == HasActiveEmbers())
                {
                    HideRenderers();
                    return;
                }
            }

            // 캐릭터 스프라이트가 바뀌었을 때만(걷기/방향/도끼 등) 실루엣을 다시 만든다
            int key = ComputeMaskKey();
            if (false == bMaskBuilt || key != maskKey)
            {
                maskKey = key;
                bMaskBuilt = true;
                BuildSilhouette();
                bAuraDirty = true;
            }

            UpdateMovement(dt);
            SpawnEmbers(dt);
            UpdateEmbers(dt);
            RebuildMeshes();
        }

        private void OnDisable()
        {
            HideRenderers();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                if (null != meshes[i])
                {
                    Destroy(meshes[i]);
                    meshes[i] = null;
                }
            }
        }
    }
}
