using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 나무가 열기를 뿜을 때(TreeHeatEmitEffect)의 이펙트. 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬(VFX_ChargeVortex, VFX_LaserHit와 같은 방식)와
    /// 화면 왜곡 쿼드(VFX_HeatBurstDistortion 셰이더)로 구성된다. 풀에서 꺼내져 활성화될 때(OnEnable) 재생을 시작하고 끝나면 스스로 렌더러를 끈다(풀 반환은 루트 ParticleSystem).
    ///
    /// 구성 (총 약 1.9초)
    /// 1. 중심 폭발(파편 발산): 꽉 찬 덩어리 없이, 중심에서 가는 조각들이 바깥으로 팡 터진다. 전부 1~2px 두께의 정수 픽셀이다.
    ///    - 방사형 줄기: 중심에서 약간 떨어진 곳에서 시작해 바깥으로 뻗는 가는 선(머리 밝은 색, 몸통 주황, 꼬리 붉은색). 처음엔 빠르게 나가다 꼬리부터 짧아지며 사라진다.
    ///    - 초승달 조각: 원호 모양의 조각이 회전하며 날아가다 작아지고 깜빡이며 사라진다.
    ///    - 작은 조각: 점, 대시, 2x2 덩어리가 감속하며 흩어진다.
    ///    - 중심 섬광: 처음 짧은 시간 동안만 아주 작은 별 모양으로 번쩍인다.
    /// 2. 소용돌이 불꽃: 가는 불꽃 리본이 나무 둘레를 타원 나선으로 감으며 위로 오른다. 나무 앞쪽 반원 구간만 그려서 감았다가 뒤로 사라지는 것처럼 보인다.
    /// 3. 연기: 중심 둘레에서 단색 연기 조각(둥근 덩어리, 길쭉한 덩어리, 구름, 초승달 자락, 작은 알갱이)이 생겨 바깥으로 밀려나며 위로 오르다 작아지며 사라진다.
    ///    입자 하나는 단색이고 입자마다 명도만 다르다(발광 없음). 크기는 작게 다양하다.
    /// 4. 불씨: `.` `+` `x`, 대시, 작은 덩어리가 터진 뒤 노이즈 바람장을 따라 휘날리며 떠올랐다 식어 사라진다(매 프레임 스무스하게 계산해 정수 픽셀에 스냅).
    /// 5. 화면 왜곡: 중심에서 퍼지는 충격파 링과 위로 피어오르는 아지랑이(별도 쿼드, 셰이더가 _CameraSortingLayerTexture를 샘플링).
    ///
    /// 최적화: 몸통(파편, 소용돌이, 연기) 메쉬도 매 프레임 다시 만든다(저사양 모드에서는 30fps로 제한). 모든 조각은 구조체 배열에서 닫힌 식으로 위치를 계산하고
    /// 공유 정적 PixelQuadBuffer에 모아 올리므로 런타임 할당이 없다. 동시 재생 수가 많으면 불씨 수를 줄이고, 화면 밖이면 메쉬 재구성을 건너뛴다.
    /// 색은 불 3가지(진한 색, 중간 색, 밝은 색)와 연기 명도 4단계(입자 하나는 그중 단색 하나)만 쓴다. 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로 내고, 연기는 발광이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class VFX_TreeHeatBurst : MonoBehaviour
    {
        [Header("렌더링")]
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Material distortionMaterial;
        [SerializeField, Tooltip("몸통(파편, 소용돌이, 연기) 메쉬의 루트 렌더러 기준 상대 소팅 오더")] private int bodySortingOffset = 0;
        [SerializeField, Tooltip("불씨 메쉬의 상대 소팅 오더(몸통 위)")] private int emberSortingOffset = 1;
        [SerializeField, Tooltip("왜곡 쿼드의 상대 소팅 오더(불 아래여야 불이 왜곡되지 않는다)")] private int distortionSortingOffset = -1;

        [Header("시간")]
        [SerializeField, Tooltip("이펙트 전체 길이(초). 루트 ParticleSystem 수명보다 짧아야 한다")] private float totalDuration = 1.4f;

        [Header("중심 섬광")]
        [SerializeField, Tooltip("섬광이 지속되는 시간(초)")] private float flashDuration = 0.07f;
        [SerializeField, Tooltip("섬광 중심 원의 최대 반지름(px)")] private float flashRadiusPx = 5.0f;

        [Header("방사형 줄기")]
        [SerializeField, Range(0, 24), Tooltip("중심에서 뻗는 가는 줄기 수")] private int streakCount = 16;
        [SerializeField, Tooltip("줄기가 시작하는 거리의 최솟값(px)")] private float streakStartMinPx = 4.0f;
        [SerializeField, Tooltip("줄기가 시작하는 거리의 최댓값(px)")] private float streakStartMaxPx = 10.0f;
        [SerializeField, Tooltip("줄기 길이의 최솟값(px)")] private float streakLengthMinPx = 8.0f;
        [SerializeField, Tooltip("줄기 길이의 최댓값(px)")] private float streakLengthMaxPx = 40.0f;
        [SerializeField, Tooltip("줄기 머리가 시작점에서 더 나아가는 거리(px)")] private float streakTravelPx = 34.0f;
        [SerializeField, Tooltip("줄기가 나아가고 사라지는 시간(초)")] private float streakDuration = 0.18f;

        [Header("초승달 조각")]
        [SerializeField, Range(0, 16), Tooltip("원호 모양 조각 수")] private int crescentCount = 10;
        [SerializeField, Tooltip("조각 반지름의 최솟값(px)")] private float crescentRadiusMinPx = 3.0f;
        [SerializeField, Tooltip("조각 반지름의 최댓값(px)")] private float crescentRadiusMaxPx = 6.0f;
        [SerializeField, Tooltip("조각이 날아가는 거리의 최솟값(px)")] private float crescentTravelMinPx = 16.0f;
        [SerializeField, Tooltip("조각이 날아가는 거리의 최댓값(px)")] private float crescentTravelMaxPx = 40.0f;
        [SerializeField, Tooltip("조각이 날아가고 사라지는 시간(초)")] private float crescentDuration = 0.26f;

        [Header("작은 조각")]
        [SerializeField, Range(0, 24), Tooltip("점, 대시, 2x2 덩어리 수")] private int chunkCount = 14;
        [SerializeField, Tooltip("작은 조각이 날아가는 거리의 최솟값(px)")] private float chunkTravelMinPx = 14.0f;
        [SerializeField, Tooltip("작은 조각이 날아가는 거리의 최댓값(px)")] private float chunkTravelMaxPx = 36.0f;
        [SerializeField, Tooltip("작은 조각이 날아가고 사라지는 시간(초)")] private float chunkDuration = 0.24f;

        [Header("소용돌이 불꽃")]
        [SerializeField, Range(0, 4), Tooltip("나무를 감는 불꽃 리본 가닥 수")] private int swirlCount = 3;
        [SerializeField, Tooltip("리본이 감는 반지름(px)")] private float swirlRadiusPx = 20.0f;
        [SerializeField, Tooltip("리본 머리가 오르는 높이(px). 이펙트 원점에서 나무 꼭대기까지 약 38px라 12px 보정을 더해 꼭대기 아래에서 멈추게 한다")] private float swirlHeightPx = 22.0f;
        [SerializeField, Range(0.2f, 1.0f), Tooltip("타원의 세로 비율(아이소메트릭 타일처럼 납작하게)")] private float swirlFlatten = 0.45f;
        [SerializeField, Tooltip("리본이 나타나 사라지기까지의 시간(초)")] private float swirlDuration = 0.75f;
        [SerializeField, Tooltip("리본이 나타나기 시작하는 시간(초)")] private float swirlStart = 0.08f;
        [SerializeField, Tooltip("리본 머리가 나무를 도는 바퀴 수")] private float swirlTurns = 1.1f;

        [Header("연기")]
        [SerializeField, Range(0, 16), Tooltip("중심 둘레에 생기는 연기 조각 수")] private int smokeCount = 12;
        [SerializeField, Tooltip("연기 조각 반지름의 최솟값(px)")] private float smokeRadiusMinPx = 3.0f;
        [SerializeField, Tooltip("연기 조각 반지름의 최댓값(px)")] private float smokeRadiusMaxPx = 10.0f;
        [SerializeField, Tooltip("연기가 위로 오르는 높이(px)")] private float smokeRisePx = 34.0f;
        [SerializeField, Tooltip("연기가 처음 생기는 중심으로부터의 거리 범위의 최댓값(px)")] private float smokeRingPx = 22.0f;

        [Header("불씨")]
        [SerializeField, Range(0, 96), Tooltip("처음에 터져 나가는 불씨 수")] private int emberBurstCount = 72;
        [SerializeField, Tooltip("이후 초당 새로 피어오르는 불씨 수")] private float emberTrickleRate = 22.0f;
        [SerializeField, Tooltip("불씨가 터질 때의 초기 속도 범위 최솟값(px/s)")] private float emberSpeedMin = 125.0f;
        [SerializeField, Tooltip("불씨가 터질 때의 초기 속도 범위 최댓값(px/s)")] private float emberSpeedMax = 310.0f;
        [SerializeField, Tooltip("바람(노이즈 흐름장)의 세기(px/s^2)")] private float windStrength = 240.0f;
        [SerializeField, Tooltip("바람 노이즈의 공간 주파수(클수록 잘게 휘날린다)")] private float windScale = 0.035f;
        [SerializeField, Tooltip("불씨가 위로 뜨는 힘(px/s^2)")] private float emberBuoyancy = 38.0f;
        [SerializeField, Tooltip("불씨 감속(초당)")] private float emberDrag = 2.8f;

        [Header("화면 왜곡")]
        [SerializeField, Tooltip("왜곡 쿼드의 월드 크기(유닛, 가로 x 세로)")] private Vector2 distortionSize = new Vector2(5.2f, 5.0f);
        [SerializeField, Tooltip("왜곡 쿼드 중심이 이펙트 원점에서 위로 떨어진 거리(유닛)")] private float distortionCenterY = 1.3f;
        [SerializeField, Tooltip("충격파 링이 퍼지는 시간(초)")] private float ringDuration = 0.35f;
        [SerializeField, Tooltip("아지랑이가 지속되는 시간(초)")] private float hazeDuration = 1.1f;

        [Header("최적화")]
        [SerializeField, Tooltip("동시에 재생 중인 인스턴스가 이 수를 넘으면 저사양 모드(몸통 재구성 30fps 제한, 불씨 수 감소)")] private int heavyLoadCount = 6;

        [Header("밝기 (정점 알파 = HDR 발광 세기 0~1)")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("전체 발광 배율. 1이면 기준값")] private float glowScale = 1.0f;

        private const float TwoPi = Mathf.PI * 2.0f;
        private const int EmberCapacity = 112;
        private const int MaxSmoke = 16;
        private const int MaxSwirl = 4;
        private const int MaxFragments = 64;
        private const int SwirlSamples = 40;
        private const int RowCapacity = 96;
        private const int GroundLevelPx = 14;                  // 이펙트 원점(나무 밑동 + 0.5유닛) 아래로 이 만큼 내려가면 땅
        private const float EmberBurstTime = 0.05f;            // 불씨가 터져 나가는 시간 폭(조각과 같은 타이밍)
        private const float EmberTrickleEnd = 0.8f;
        private const float LowQualityFps = 30.0f;
        private static readonly Vector3 MeshBoundsSize = new Vector3(100.0f, 100.0f, 10.0f);

        // 색 자리: 0~2는 불, 3~6은 연기 명도 4단계(발광 없음, 입자 하나는 그중 단색 하나). HDR 배율이 곱해져도 흰색으로 날아가지 않도록 기본 색은 어둡게 잡았다.
        private const int ToneFireDeep = 0;
        private const int ToneFireMid = 1;
        private const int ToneFireLight = 2;
        private const int ToneSmokeFirst = 3;
        private const int SmokeShades = 4;
        private static readonly Color32[] Palette =
        {
            new Color32(120, 28, 6, 255),
            new Color32(225, 105, 12, 255),
            new Color32(245, 190, 75, 255),
            new Color32(24, 19, 22, 255),
            new Color32(40, 32, 31, 255),
            new Color32(58, 47, 43, 255),
            new Color32(80, 66, 58, 255),
        };

        private enum FragmentType
        {
            Streak,
            Crescent,
            Chunk,
        }

        private struct Fragment
        {
            public FragmentType type;
            public float angle;      // 중심에서 날아가는 방향
            public float startDist;  // 시작 거리
            public float travel;     // 시작점에서 더 나아가는 거리
            public float length;     // 줄기 길이
            public float radius;     // 초승달 반지름
            public float orient;     // 초승달이 향하는 방향(조각 중심 기준)
            public float spin;       // 초승달 회전 속도(rad/초)
            public float spanHalf;   // 초승달 호의 반각(rad)
            public float delay;
            public float duration;
            public int thick;        // 줄기 두께(1 또는 2)
            public int variant;      // 작은 조각 모양
        }

        private struct Ember
        {
            public bool bActive;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
            public int kind;      // 0: `.`, 1: `+`, 2: `x`, 3: 대시, 4: 2x2 덩어리
            public int seed;
        }

        private struct Smoke
        {
            public float spawnTime;
            public float angle;     // 중심 둘레 각도
            public float distance;  // 처음 생기는 중심으로부터의 거리
            public float swayPhase;
            public float radius;
            public float life;
            public float rotation;  // 모양 방향
            public int variant;     // 0: 둥근 덩어리, 1: 길쭉한 덩어리, 2: 구름, 3: 초승달 자락, 4: 작은 알갱이
            public int tone;        // 연기 명도 단계(입자 하나는 단색)
            public int seed;
        }

        private static Mesh sharedQuadMesh;
        private static int activeCount;

        // 화면에 그릴 쿼드를 모으는 공유 버퍼(매 프레임 Clear 후 채우고 곧바로 업로드하므로 인스턴스끼리 겹치지 않는다)
        private static readonly PixelQuadBuffer bodyBuffer = new PixelQuadBuffer(1536);
        private static readonly PixelQuadBuffer emberBuffer = new PixelQuadBuffer(512);
        private static readonly byte[] rowTone = new byte[RowCapacity];
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");

        //내부 의존성
        private readonly Ember[] embers = new Ember[EmberCapacity];
        private readonly Smoke[] smokes = new Smoke[MaxSmoke];
        private readonly Fragment[] fragments = new Fragment[MaxFragments];
        private MeshFilter bodyFilter;
        private MeshRenderer bodyRenderer;
        private Mesh bodyMesh;
        private MeshFilter emberFilter;
        private MeshRenderer emberRenderer;
        private Mesh emberMesh;
        private MeshRenderer distortionRenderer;
        private MaterialPropertyBlock distortionBlock;
        private ParticleSystemRenderer rootParticleRenderer;

        //상태 변수
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bCounted;
        private bool bBodyHasGeometry;
        private bool bEmberHasGeometry;
        private int fragmentTotal;
        private float elapsed;
        private float bodyTimer;
        private float trickleAccumulator;
        private int framesSinceEnable;
        private int emberCursor;
        private int bodySeed;
        private uint randomState;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            bodyMesh = CreateMeshObject("HeatBurst_Body", glowMaterial, out bodyFilter, out bodyRenderer);
            emberMesh = CreateMeshObject("HeatBurst_Embers", glowMaterial, out emberFilter, out emberRenderer);

            // 왜곡 쿼드: 모든 인스턴스가 같은 쿼드 메쉬를 공유하고 인스턴스마다 MaterialPropertyBlock으로 진행도만 다르게 준다
            GameObject distortionObject = new GameObject("HeatBurst_Distortion");
            distortionObject.transform.SetParent(transform, false);
            distortionObject.AddComponent<MeshFilter>().sharedMesh = GetQuadMesh();
            distortionRenderer = distortionObject.AddComponent<MeshRenderer>();
            distortionRenderer.sharedMaterial = distortionMaterial;
            distortionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            distortionRenderer.receiveShadows = false;
            distortionRenderer.enabled = false;
            distortionBlock = new MaterialPropertyBlock();
        }

        private Mesh CreateMeshObject(string _name, Material _material, out MeshFilter _filter, out MeshRenderer _renderer)
        {
            GameObject child = new GameObject(_name);
            child.transform.SetParent(transform, false);

            _filter = child.AddComponent<MeshFilter>();
            _renderer = child.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh { name = "VFX_TreeHeatBurst_" + _name };
            mesh.MarkDynamic();
            _filter.sharedMesh = mesh;

            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        // 모든 인스턴스가 공유하는 1x1 유닛 쿼드(UV 0~1, 중심 기준)
        private static Mesh GetQuadMesh()
        {
            if (null != sharedQuadMesh) return sharedQuadMesh;

            sharedQuadMesh = new Mesh { name = "VFX_TreeHeatBurst_Quad" };
            sharedQuadMesh.vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(-0.5f, 0.5f, 0.0f), new Vector3(0.5f, 0.5f, 0.0f), new Vector3(0.5f, -0.5f, 0.0f) };
            sharedQuadMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 0.0f) };
            sharedQuadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            sharedQuadMesh.bounds = new Bounds(Vector3.zero, new Vector3(1.0f, 1.0f, 0.1f));
            sharedQuadMesh.UploadMeshData(true);
            return sharedQuadMesh;
        }

        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bodyTimer = 1000.0f; // 첫 프레임에 바로 그린다
            trickleAccumulator = 0.0f;
            framesSinceEnable = 0;
            emberCursor = 0;
            bFirstFrame = true;
            bBodyHasGeometry = false;
            bEmberHasGeometry = false;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 131.0f + pos.y * 57.0f));
            if (0u == randomState) randomState = 1u;
            bodySeed = (int)(Rand01() * 10000.0f);

            for (int i = 0; i < EmberCapacity; i++) embers[i].bActive = false;

            BuildFragments();
            BuildSmokes();

            int burst = IsHeavyLoad() ? emberBurstCount / 2 : emberBurstCount;
            for (int i = 0; i < burst; i++) SpawnBurstEmber();

            if (false == bCounted)
            {
                bCounted = true;
                activeCount++;
            }
        }

        // 폭발 조각(줄기, 초승달, 작은 조각)의 방향, 거리, 크기, 타이밍을 재생 시작 때 한 번 뽑는다
        private void BuildFragments()
        {
            fragmentTotal = 0;

            for (int i = 0; i < streakCount && fragmentTotal < MaxFragments; i++)
            {
                Fragment f = new Fragment();
                f.type = FragmentType.Streak;
                f.angle = TwoPi * (i + (Rand01() - 0.5f) * 0.7f) / Mathf.Max(1, streakCount);
                f.startDist = Mathf.Lerp(streakStartMinPx, streakStartMaxPx, Rand01());
                f.travel = streakTravelPx * Mathf.Lerp(0.6f, 1.25f, Rand01());
                f.length = Mathf.Lerp(streakLengthMinPx, streakLengthMaxPx, Rand01() * Rand01() * 0.5f + Rand01() * 0.5f);
                f.delay = Rand01() * 0.012f;
                f.duration = streakDuration * Mathf.Lerp(0.8f, 1.2f, Rand01());
                f.thick = Rand01() < 0.25f ? 2 : 1;
                fragments[fragmentTotal++] = f;
            }

            for (int i = 0; i < crescentCount && fragmentTotal < MaxFragments; i++)
            {
                Fragment f = new Fragment();
                f.type = FragmentType.Crescent;
                f.angle = TwoPi * (i + (Rand01() - 0.5f) * 0.8f) / Mathf.Max(1, crescentCount) + 0.25f;
                f.startDist = Mathf.Lerp(3.0f, 9.0f, Rand01());
                f.travel = Mathf.Lerp(crescentTravelMinPx, crescentTravelMaxPx, Rand01());
                f.radius = Mathf.Lerp(crescentRadiusMinPx, crescentRadiusMaxPx, Rand01());
                f.orient = f.angle + (Rand01() - 0.5f) * 3.2f;
                f.spin = (Rand01() - 0.5f) * 5.0f;
                f.spanHalf = Mathf.Lerp(1.25f, 1.65f, Rand01());
                f.delay = Rand01() * 0.015f;
                f.duration = crescentDuration * Mathf.Lerp(0.8f, 1.2f, Rand01());
                fragments[fragmentTotal++] = f;
            }

            for (int i = 0; i < chunkCount && fragmentTotal < MaxFragments; i++)
            {
                Fragment f = new Fragment();
                f.type = FragmentType.Chunk;
                f.angle = Rand01() * TwoPi;
                f.startDist = Mathf.Lerp(3.0f, 8.0f, Rand01());
                f.travel = Mathf.Lerp(chunkTravelMinPx, chunkTravelMaxPx, Rand01());
                f.delay = Rand01() * 0.015f;
                f.duration = chunkDuration * Mathf.Lerp(0.8f, 1.2f, Rand01());
                float roll = Rand01();
                f.variant = roll < 0.4f ? 0 : (roll < 0.6f ? 1 : (roll < 0.8f ? 2 : 3)); // 점, 가로 대시, 세로 대시, 2x2
                fragments[fragmentTotal++] = f;
            }
        }

        // 연기 조각의 모양, 크기, 명도, 타이밍을 재생 시작 때 한 번 뽑는다. 모양과 크기가 다양하고 입자 하나는 단색이다.
        private void BuildSmokes()
        {
            for (int i = 0; i < MaxSmoke; i++)
            {
                smokes[i].spawnTime = 0.04f + Rand01() * 0.08f;
                smokes[i].angle = TwoPi * (i + Rand01() * 0.6f) / Mathf.Max(1, smokeCount);
                smokes[i].distance = Mathf.Lerp(8.0f, smokeRingPx, Rand01());
                smokes[i].swayPhase = Rand01() * TwoPi;
                smokes[i].radius = Mathf.Lerp(smokeRadiusMinPx, smokeRadiusMaxPx, Rand01() * Rand01() * 0.4f + Rand01() * 0.6f);
                smokes[i].life = Mathf.Lerp(0.6f, 0.95f, Rand01());
                smokes[i].rotation = Rand01() * TwoPi;
                smokes[i].tone = ToneSmokeFirst + Mathf.Min(SmokeShades - 1, (int)(Rand01() * SmokeShades));
                smokes[i].seed = (int)(Rand01() * 1000.0f);

                float roll = Rand01();
                smokes[i].variant = roll < 0.28f ? 0 : (roll < 0.5f ? 1 : (roll < 0.72f ? 2 : (roll < 0.88f ? 3 : 4)));
            }
        }

        // ---------- 난수 / 유틸 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private static float Hash01(int _a, int _b, int _c)
        {
            uint h = unchecked((uint)(_a * 73856093) ^ (uint)(_b * 19349663) ^ (uint)(_c * 83492791));
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535.0f;
        }

        // 격자점 해시를 부드럽게 보간한 값 노이즈(0~1)
        private static float ValueNoise(float _x, float _y, int _seed)
        {
            int ix = Mathf.FloorToInt(_x);
            int iy = Mathf.FloorToInt(_y);
            float fx = _x - ix;
            float fy = _y - iy;
            fx = fx * fx * (3.0f - 2.0f * fx);
            fy = fy * fy * (3.0f - 2.0f * fy);

            float a = Hash01(ix, iy, _seed);
            float b = Hash01(ix + 1, iy, _seed);
            float c = Hash01(ix, iy + 1, _seed);
            float d = Hash01(ix + 1, iy + 1, _seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        // 아주 강한 ease-out(첫 몇 프레임에 대부분의 거리를 날아간다): 1 - (1 - t)^4
        private static float EaseOutQuart(float _t)
        {
            float inverse = 1.0f - _t;
            float squared = inverse * inverse;
            return 1.0f - squared * squared;
        }

        private bool IsHeavyLoad()
        {
            return heavyLoadCount < activeCount;
        }

        // ---------- 불씨 ----------

        private void SpawnBurstEmber()
        {
            float angle = Rand01() * TwoPi;
            float speed = Mathf.Lerp(emberSpeedMin, emberSpeedMax, Rand01());
            int slot = emberCursor;
            emberCursor = (emberCursor + 1) % EmberCapacity;

            embers[slot].bActive = true;
            embers[slot].x = Mathf.Cos(angle) * 6.0f;
            embers[slot].y = Mathf.Sin(angle) * 6.0f;
            embers[slot].vx = Mathf.Cos(angle) * speed;
            embers[slot].vy = Mathf.Sin(angle) * speed * 0.8f + 30.0f; // 위쪽으로 조금 더 튄다
            embers[slot].age = -Rand01() * EmberBurstTime;            // 조금씩 어긋나게 터진다
            embers[slot].life = Mathf.Lerp(0.5f, 1.3f, Rand01());
            embers[slot].kind = ChooseEmberKind();
            embers[slot].seed = (int)(Rand01() * 1000.0f);
        }

        private void SpawnTrickleEmber()
        {
            int slot = emberCursor;
            emberCursor = (emberCursor + 1) % EmberCapacity;

            embers[slot].bActive = true;
            embers[slot].x = (Rand01() - 0.5f) * 30.0f;
            embers[slot].y = Rand01() * 14.0f - 4.0f;
            embers[slot].vx = (Rand01() - 0.5f) * 40.0f;
            embers[slot].vy = Mathf.Lerp(30.0f, 70.0f, Rand01());
            embers[slot].age = 0.0f;
            embers[slot].life = Mathf.Lerp(0.5f, 1.1f, Rand01());
            embers[slot].kind = ChooseEmberKind();
            embers[slot].seed = (int)(Rand01() * 1000.0f);
        }

        // 모양: 점 45%, `+` 15%, `x` 15%, 대시 18%, 2x2 덩어리 7%(전부 3x3 이하)
        private int ChooseEmberKind()
        {
            float roll = Rand01();
            return roll < 0.45f ? 0 : (roll < 0.6f ? 1 : (roll < 0.75f ? 2 : (roll < 0.93f ? 3 : 4)));
        }

        // 불씨 이동: 터져 나가는 속도는 감속되고, 위로 뜨는 힘과 노이즈 흐름장(바람)이 곡선으로 휘날리게 한다
        private void UpdateEmbers(float _dt)
        {
            float drag = Mathf.Max(0.0f, 1.0f - emberDrag * _dt);
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                embers[i].age += _dt;
                if (embers[i].age >= embers[i].life)
                {
                    embers[i].bActive = false;
                    continue;
                }

                if (0.0f > embers[i].age) continue;

                float flowX = ValueNoise(embers[i].x * windScale + elapsed * 0.9f, embers[i].y * windScale, 11) - 0.5f;
                float flowY = ValueNoise(embers[i].x * windScale, embers[i].y * windScale - elapsed * 0.7f, 29) - 0.5f;
                embers[i].vx = embers[i].vx * drag + flowX * 2.0f * windStrength * _dt;
                embers[i].vy = embers[i].vy * drag + (flowY * 2.0f * windStrength * 0.6f + emberBuoyancy) * _dt;
                embers[i].x += embers[i].vx * _dt;
                embers[i].y += embers[i].vy * _dt;
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

        // ---------- 그리기 공통 ----------

        private byte GlowByte(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow * glowScale) * 255.0f);
        }

        private Color32 Tint(int _tone, float _glow)
        {
            Color32 color = Palette[_tone];
            // 연기(3~6)는 발광 0이라 GlowMin 배율만 받는다
            return new Color32(color.r, color.g, color.b, _tone >= ToneSmokeFirst ? (byte)0 : GlowByte(_glow));
        }

        private static float FireGlow(int _tone)
        {
            return ToneFireLight == _tone ? 1.0f : (ToneFireMid == _tone ? 0.75f : 0.45f);
        }

        // 한 줄(행)에 모아 둔 셀 색(rowTone: 255 = 비어 있음)을 같은 색끼리 가로로 합쳐 쿼드로 낸다
        private void FlushRow(PixelQuadBuffer _buffer, int _y, int _xStart, int _count)
        {
            int runStart = 0;
            int runTone = 255;
            for (int i = 0; i <= _count; i++)
            {
                int tone = i < _count ? rowTone[i] : 255;
                if (tone != runTone)
                {
                    if (255 != runTone)
                    {
                        float glow = runTone >= ToneSmokeFirst ? 0.0f : FireGlow(runTone);
                        _buffer.AddLocalRect(_xStart + runStart, _y, _xStart + i, _y + 1, Tint(runTone, glow));
                    }

                    runStart = i;
                    runTone = tone;
                }
            }
        }

        private static void ClearRow(int _count)
        {
            for (int i = 0; i < _count; i++) rowTone[i] = 255;
        }

        private void AddFirePixel(int _x, int _y, int _tone)
        {
            bodyBuffer.AddLocalRect(_x, _y, _x + 1, _y + 1, Tint(_tone, FireGlow(_tone)));
        }

        // ---------- 1. 중심 폭발(파편 발산) ----------

        private void DrawFragments()
        {
            for (int i = 0; i < fragmentTotal; i++)
            {
                float t = (elapsed - fragments[i].delay) / Mathf.Max(0.05f, fragments[i].duration);
                if (0.0f >= t || 1.0f <= t) continue;

                if (FragmentType.Streak == fragments[i].type) DrawStreak(in fragments[i], t);
                else if (FragmentType.Crescent == fragments[i].type) DrawCrescent(in fragments[i], t);
                else DrawChunk(in fragments[i], t);
            }
        }

        // 방사형 줄기: 머리가 빠르게 나가고(아주 강한 ease-out) 꼬리가 뒤따라 짧아진다. 머리는 밝은 색, 몸통은 주황, 꼬리는 붉은색의 가는 선이다.
        private void DrawStreak(in Fragment _f, float _t)
        {
            float head = _f.startDist + _f.travel * EaseOutQuart(_t);
            float tailKeep = Mathf.Pow(Mathf.Clamp01((_t - 0.08f) / 0.92f), 1.5f);
            float length = _f.length * (1.0f - tailKeep);
            float tail = Mathf.Max(_f.startDist, head - length);
            if (0.8f > head - tail) return;

            float dirX = Mathf.Cos(_f.angle);
            float dirY = Mathf.Sin(_f.angle);
            bool bHorizontal = Mathf.Abs(dirX) > Mathf.Abs(dirY);
            int steps = Mathf.CeilToInt((head - tail) / 0.7f);
            int lastX = int.MaxValue;
            int lastY = int.MaxValue;

            for (int k = 0; k <= steps; k++)
            {
                float fraction = (float)k / steps; // 0: 머리, 1: 꼬리
                float distance = head - (head - tail) * fraction;
                int x = Mathf.RoundToInt(dirX * distance);
                int y = Mathf.RoundToInt(dirY * distance);
                if (x == lastX && y == lastY) continue;
                lastX = x;
                lastY = y;

                int tone = fraction < 0.3f ? ToneFireLight : (fraction < 0.65f ? ToneFireMid : ToneFireDeep);
                AddFirePixel(x, y, tone);
                if (2 == _f.thick && fraction < 0.8f)
                {
                    if (true == bHorizontal) AddFirePixel(x, y + 1, tone);
                    else AddFirePixel(x + 1, y, tone);
                }
            }
        }

        // 초승달 조각: 원호(바깥 호와 한 칸 안쪽 호, 두께 2px)가 바깥으로 날아가며 천천히 회전하고, 후반에 작아지며 깜빡이다 사라진다.
        private void DrawCrescent(in Fragment _f, float _t)
        {
            if (0.7f < _t && 0 != ((int)(elapsed * 30.0f) & 1)) return;

            float distance = _f.startDist + _f.travel * EaseOutQuart(_t);
            float centerX = Mathf.Cos(_f.angle) * distance;
            float centerY = Mathf.Sin(_f.angle) * distance;
            float radius = _f.radius * (1.0f - 0.5f * _t * _t);
            float orient = _f.orient + _f.spin * _t;
            int tone = _t < 0.35f ? ToneFireLight : (_t < 0.7f ? ToneFireMid : ToneFireDeep);

            int samples = Mathf.Max(4, Mathf.CeilToInt(_f.spanHalf * 2.0f * radius * 1.3f));
            int lastX = int.MaxValue;
            int lastY = int.MaxValue;
            for (int k = 0; k <= samples; k++)
            {
                float a = orient - _f.spanHalf + 2.0f * _f.spanHalf * k / samples;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                // 호의 양 끝은 한 겹(가늘게), 가운데는 두 겹(굵게)
                float edge = Mathf.Abs(2.0f * k / samples - 1.0f);
                int x = Mathf.RoundToInt(centerX + cos * radius);
                int y = Mathf.RoundToInt(centerY + sin * radius);
                if (x != lastX || y != lastY)
                {
                    AddFirePixel(x, y, tone);
                    lastX = x;
                    lastY = y;
                }

                if (0.55f > edge && 1.5f < radius)
                {
                    int innerX = Mathf.RoundToInt(centerX + cos * (radius - 1.0f));
                    int innerY = Mathf.RoundToInt(centerY + sin * (radius - 1.0f));
                    if (innerX != x || innerY != y) AddFirePixel(innerX, innerY, tone);
                }
            }
        }

        // 작은 조각: 점, 가로/세로 대시, 2x2 덩어리가 감속하며 흩어지고 후반에 깜빡이다 사라진다
        private void DrawChunk(in Fragment _f, float _t)
        {
            if (0.7f < _t && 0 != ((int)(elapsed * 30.0f) & 1)) return;

            float distance = _f.startDist + _f.travel * EaseOutQuart(_t);
            int x = Mathf.RoundToInt(Mathf.Cos(_f.angle) * distance);
            int y = Mathf.RoundToInt(Mathf.Sin(_f.angle) * distance);
            int tone = _t < 0.3f ? ToneFireLight : (_t < 0.65f ? ToneFireMid : ToneFireDeep);
            Color32 color = Tint(tone, FireGlow(tone));

            int variant = _f.variant;
            if (1 == variant) bodyBuffer.AddLocalRect(x, y, x + 2, y + 1, color);
            else if (2 == variant) bodyBuffer.AddLocalRect(x, y, x + 1, y + 2, color);
            else if (3 == variant) bodyBuffer.AddLocalRect(x, y, x + 2, y + 2, color);
            else bodyBuffer.AddLocalRect(x, y, x + 1, y + 1, color);
        }

        // 중심 섬광: 처음 짧은 시간 동안만 번쩍이는 아주 작은 별 모양(작은 원판 + 십자와 X자 가는 줄기)
        private void DrawFlash()
        {
            if (elapsed >= flashDuration) return;

            float t = elapsed / Mathf.Max(0.03f, flashDuration);
            int radius = Mathf.RoundToInt(flashRadiusPx * (1.0f - t));
            int length = Mathf.RoundToInt(13.0f * (1.0f - t * t));

            if (1 <= radius)
            {
                float outer = radius + 0.35f;
                for (int cy = -radius; cy <= radius; cy++)
                {
                    for (int cx = -radius; cx <= radius; cx++)
                    {
                        if (Mathf.Sqrt(cx * cx + cy * cy) > outer) continue;
                        AddFirePixel(cx, cy, ToneFireLight);
                    }
                }
            }

            for (int k = radius + 1; k <= length; k++)
            {
                int tone = k <= length / 2 ? ToneFireLight : ToneFireMid;
                AddFirePixel(k, 0, tone);
                AddFirePixel(-k, 0, tone);
                AddFirePixel(0, k, tone);
                AddFirePixel(0, -k, tone);

                if (k > length * 0.7f) continue;
                AddFirePixel(k, k, ToneFireMid);
                AddFirePixel(-k, k, ToneFireMid);
                AddFirePixel(k, -k, ToneFireMid);
                AddFirePixel(-k, -k, ToneFireMid);
            }
        }

        // ---------- 2. 소용돌이 불꽃 ----------

        // 불꽃 리본이 나무 둘레를 타원 나선으로 감으며 오른다. 나무 앞쪽 반원(타원의 아래쪽) 구간만 그리고 뒤쪽은 그리지 않아서 나무를 감았다가 뒤로 사라지는 것처럼 보인다.
        // 샘플을 촘촘하게 찍고 같은 칸은 건너뛰어 끊기지 않는 연속 곡선이 되며, 폭은 머리 2px에서 꼬리 1px로 줄어든다.
        private void DrawSwirls()
        {
            int count = Mathf.Min(MaxSwirl, swirlCount);
            float duration = Mathf.Max(0.1f, swirlDuration);
            float st = (elapsed - swirlStart) / duration;
            if (0 >= count || 0.0f >= st || 1.0f <= st) return;

            float tailKeep = 1.0f - Mathf.Clamp01((st - 0.7f) / 0.3f); // 끝에서는 꼬리가 줄어들며 사라진다
            int samples = Mathf.RoundToInt(SwirlSamples * tailKeep);
            float headRise = swirlHeightPx * EaseOut(Mathf.Clamp01(st / 0.7f)); // 나무 꼭대기에 닿으면(마지막 30%) 높이를 고정하고 감기만 이어간다
            float fade = 1.0f - Mathf.Clamp01((st - 0.85f) / 0.15f);

            for (int i = 0; i < count; i++)
            {
                float phase = TwoPi * i / count;
                float headTheta = phase + st * TwoPi * swirlTurns;
                int lastX = int.MaxValue;
                int lastY = int.MaxValue;

                for (int k = 0; k < samples; k++)
                {
                    float theta = headTheta - k * 0.075f;
                    float sin = Mathf.Sin(theta);
                    if (0.0f < sin) continue; // 뒤쪽 반원은 나무에 가려진다

                    float rise = headRise + 12.0f - k * 0.6f;
                    float radius = swirlRadiusPx * (1.0f + 0.12f * Mathf.Sin(k * 0.22f + phase)) * (1.0f - 0.2f * st);
                    int x = Mathf.RoundToInt(Mathf.Cos(theta) * radius);
                    int y = Mathf.RoundToInt(rise + sin * radius * swirlFlatten);
                    if (y < -GroundLevelPx) continue; // 나무 밑동(땅) 아래로 내려간 꼬리는 그리지 않는다
                    if (x == lastX && y == lastY) continue;
                    lastX = x;
                    lastY = y;

                    float along = (float)k / SwirlSamples;
                    int size = along < 0.2f ? 2 : 1;
                    int tone = along < 0.2f ? ToneFireLight : (along < 0.6f ? ToneFireMid : ToneFireDeep);
                    bodyBuffer.AddLocalRect(x, y, x + size, y + size, Tint(tone, FireGlow(tone) * fade));
                }
            }
        }

        // ---------- 3. 연기 ----------

        private void DrawSmoke()
        {
            int count = Mathf.Min(MaxSmoke, smokeCount);
            for (int i = 0; i < count; i++)
            {
                float age = elapsed - smokes[i].spawnTime;
                if (0.0f > age || age >= smokes[i].life) continue;

                float u = age / smokes[i].life;

                // 중심 둘레에서 생겨 바깥으로 밀려나고(빠르게 감속), 위로 천천히 오른다
                float outward = smokes[i].distance + 14.0f * (1.0f - Mathf.Exp(-age * 8.0f));
                float rise = smokeRisePx * (1.0f - Mathf.Exp(-age * 1.6f));
                float sway = Mathf.Sin(smokes[i].swayPhase + age * 2.0f) * 3.0f * u;
                float cx = Mathf.Cos(smokes[i].angle) * outward + sway;
                float cy = Mathf.Sin(smokes[i].angle) * outward * 0.8f + rise;

                // 처음에는 부풀고(0.15초) 이후 작아지며, 후반(u > 0.55)에는 본체가 줄어드는 대신 작은 덩어리 둘이 갈라져 나간다
                float radius = smokes[i].radius * EaseOut(Mathf.Min(1.0f, age / 0.15f));
                float split = Mathf.Clamp01((u - 0.55f) / 0.45f);
                float mainRadius = radius * (1.0f - split * 0.8f) * (1.0f - u * 0.3f);
                int tone = smokes[i].tone;

                if (1.5f <= mainRadius) DrawSmokeShape(smokes[i].variant, Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), mainRadius, tone, smokes[i].seed, smokes[i].rotation);

                if (0.0f < split && 4 != smokes[i].variant)
                {
                    float satelliteRadius = radius * 0.5f * (1.0f - split);
                    float drift = split * 14.0f;
                    if (1.5f <= satelliteRadius)
                    {
                        float a = smokes[i].swayPhase;
                        DrawSmokeShape(0, Mathf.RoundToInt(cx + Mathf.Cos(a) * drift), Mathf.RoundToInt(cy + Mathf.Sin(a) * drift * 0.8f + split * 5.0f), satelliteRadius, tone, smokes[i].seed + 1, 0.0f);
                        DrawSmokeShape(0, Mathf.RoundToInt(cx - Mathf.Cos(a) * drift), Mathf.RoundToInt(cy - Mathf.Sin(a) * drift * 0.8f + split * 5.0f), satelliteRadius * 0.8f, tone, smokes[i].seed + 2, 0.0f);
                    }
                }
            }
        }

        // 연기 조각 하나(단색). 모양 5종: 0 둥근 덩어리, 1 길쭉한 덩어리(원 둘), 2 구름(원 셋), 3 초승달 자락, 4 작은 알갱이 셋.
        // 외곽선이나 속 색 구분 없이 한 가지 색으로만 채운다.
        private void DrawSmokeShape(int _variant, int _cx, int _cy, float _radius, int _tone, int _seed, float _rotation)
        {
            int extent = Mathf.Min((RowCapacity - 1) / 2, Mathf.CeilToInt(_radius * 1.6f) + 1);
            int width = extent * 2 + 1;
            float cos = Mathf.Cos(_rotation);
            float sin = Mathf.Sin(_rotation);

            // 모양마다 쓰는 원의 위치와 반지름(원 최대 3개). 초승달 자락은 아래의 각도/반경 조건으로 따로 판정한다.
            float x1 = 0.0f, y1 = 0.0f, r1 = _radius;
            float x2 = 0.0f, y2 = 0.0f, r2 = -1.0f;
            float x3 = 0.0f, y3 = 0.0f, r3 = -1.0f;
            if (1 == _variant)
            {
                r1 = _radius * 0.78f;
                x1 = cos * _radius * 0.55f;
                y1 = sin * _radius * 0.55f;
                r2 = _radius * 0.7f;
                x2 = -cos * _radius * 0.55f;
                y2 = -sin * _radius * 0.55f;
            }
            else if (2 == _variant)
            {
                r2 = _radius * 0.72f;
                x2 = _radius * (0.55f + 0.15f * Hash01(_seed, 1, 3));
                y2 = _radius * 0.2f;
                r3 = _radius * 0.66f;
                x3 = -_radius * (0.5f + 0.15f * Hash01(_seed, 2, 3));
                y3 = _radius * 0.45f;
            }
            else if (4 == _variant)
            {
                r1 = Mathf.Max(1.0f, _radius * 0.42f);
                x1 = _radius * 0.7f;
                y1 = 0.0f;
                r2 = Mathf.Max(1.0f, _radius * 0.38f);
                x2 = -_radius * 0.3f;
                y2 = _radius * 0.65f;
                r3 = Mathf.Max(1.0f, _radius * 0.34f);
                x3 = -_radius * 0.35f;
                y3 = -_radius * 0.6f;
            }

            bool bWisp = 3 == _variant;
            for (int dy = -extent; dy <= extent; dy++)
            {
                ClearRow(width);
                for (int i = 0; i < width; i++)
                {
                    float dx = i - extent;
                    bool bCovered;
                    if (true == bWisp)
                    {
                        // 초승달 자락: 반지름 근처의 얇은 띠 중 회전 방향 쪽 호만(두께 약 2.4px)
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        bCovered = Mathf.Abs(distance - _radius) <= 1.2f && dx * cos + dy * sin >= _radius * 0.25f - 0.5f * (distance - _radius);
                    }
                    else
                    {
                        bCovered = (dx - x1) * (dx - x1) + (dy - y1) * (dy - y1) <= r1 * r1
                            || (0.0f < r2 && (dx - x2) * (dx - x2) + (dy - y2) * (dy - y2) <= r2 * r2)
                            || (0.0f < r3 && (dx - x3) * (dx - x3) + (dy - y3) * (dy - y3) <= r3 * r3);
                    }

                    if (true == bCovered) rowTone[i] = (byte)_tone;
                }

                FlushRow(bodyBuffer, _cy + dy, _cx - extent, width);
            }
        }

        // ---------- 4. 불씨 ----------

        private void DrawEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive || 0.0f >= embers[i].age) continue;

                float u = embers[i].age / embers[i].life;
                if (0.72f < u && 0 != ((int)(embers[i].age * 30.0f) & 1)) continue; // 후반에는 깜빡이며 사라진다

                int tone = u < 0.3f ? ToneFireLight : (u < 0.65f ? ToneFireMid : ToneFireDeep);
                Color32 color = Tint(tone, FireGlow(tone));
                int px = Mathf.RoundToInt(embers[i].x);
                int py = Mathf.RoundToInt(embers[i].y);

                int kind = embers[i].kind;
                if ((1 == kind || 2 == kind) && u < 0.55f)
                {
                    // 0.09초마다 `+` <-> `x`로 바뀌어 회전하는 것처럼 보인다
                    int step = (int)(embers[i].age / 0.09f) + embers[i].seed;
                    bool bCross = 1 == kind ? 0 == (step & 1) : 0 != (step & 1);
                    if (true == bCross)
                    {
                        emberBuffer.AddLocalRect(px - 1, py, px + 2, py + 1, color);
                        emberBuffer.AddLocalRect(px, py - 1, px + 1, py + 2, color);
                    }
                    else
                    {
                        emberBuffer.AddLocalRect(px, py, px + 1, py + 1, color);
                        emberBuffer.AddLocalRect(px - 1, py - 1, px, py, color);
                        emberBuffer.AddLocalRect(px + 1, py - 1, px + 2, py, color);
                        emberBuffer.AddLocalRect(px - 1, py + 1, px, py + 2, color);
                        emberBuffer.AddLocalRect(px + 1, py + 1, px + 2, py + 2, color);
                    }
                }
                else if (3 == kind)
                {
                    // 대시: 이동 방향에 가까운 축으로 2x1 또는 1x2
                    if (Mathf.Abs(embers[i].vx) >= Mathf.Abs(embers[i].vy)) emberBuffer.AddLocalRect(px, py, px + 2, py + 1, color);
                    else emberBuffer.AddLocalRect(px, py, px + 1, py + 2, color);
                }
                else if (4 == kind && u < 0.7f)
                {
                    emberBuffer.AddLocalRect(px, py, px + 2, py + 2, color);
                }
                else
                {
                    emberBuffer.AddLocalRect(px, py, px + 1, py + 1, color);
                }
            }
        }

        // ---------- 메쉬 갱신 ----------

        private void RebuildBody()
        {
            bodyBuffer.Clear();
            DrawSmoke();      // 연기가 가장 뒤
            DrawFragments();
            DrawFlash();
            DrawSwirls();     // 소용돌이 불꽃이 가장 앞
            UploadBuffer(bodyBuffer, bodyMesh, ref bBodyHasGeometry);
        }

        private void RebuildEmbers()
        {
            emberBuffer.Clear();
            DrawEmbers();
            UploadBuffer(emberBuffer, emberMesh, ref bEmberHasGeometry);
        }

        // 그릴 것이 없고 메쉬도 이미 비어 있으면 올리지 않는다
        private static void UploadBuffer(PixelQuadBuffer _buffer, Mesh _mesh, ref bool _bHasGeometry)
        {
            bool bEmpty = 0 == _buffer.VertexCount;
            if (true == bEmpty && false == _bHasGeometry) return;

            _buffer.Upload(_mesh, MeshBoundsSize);
            _bHasGeometry = false == bEmpty;
        }

        // 부모(풀 루트)의 스케일/회전을 지우고 도트 크기가 항상 1px이 되게 하며, 루트 렌더러(정렬 순서)를 따라간다
        private void SyncTransformsAndSorting()
        {
            Vector3 parentScale = transform.lossyScale;
            float inverseX = 0.0001f < Mathf.Abs(parentScale.x) ? 1.0f / parentScale.x : 1.0f;
            float inverseY = 0.0001f < Mathf.Abs(parentScale.y) ? 1.0f / parentScale.y : 1.0f;
            Vector3 inverse = new Vector3(inverseX, inverseY, 1.0f);

            bodyRenderer.transform.rotation = Quaternion.identity;
            bodyRenderer.transform.localScale = inverse;
            emberRenderer.transform.rotation = Quaternion.identity;
            emberRenderer.transform.localScale = inverse;

            // 왜곡 쿼드는 월드 크기(유닛)를 그대로 쓰되 부모 스케일은 지운다
            Transform distortionTransform = distortionRenderer.transform;
            distortionTransform.rotation = Quaternion.identity;
            distortionTransform.localScale = new Vector3(distortionSize.x * inverseX, distortionSize.y * inverseY, 1.0f);
            distortionTransform.localPosition = new Vector3(0.0f, distortionCenterY * inverseY, 0.0f);

            if (null != rootParticleRenderer)
            {
                int layerId = rootParticleRenderer.sortingLayerID;
                int order = rootParticleRenderer.sortingOrder;
                bodyRenderer.sortingLayerID = layerId;
                bodyRenderer.sortingOrder = order + bodySortingOffset;
                emberRenderer.sortingLayerID = layerId;
                emberRenderer.sortingOrder = order + emberSortingOffset;
                distortionRenderer.sortingLayerID = layerId;
                distortionRenderer.sortingOrder = order + distortionSortingOffset;
            }
        }

        // 충격파 링 진행도와 아지랑이 세기를 시간으로 정해 왜곡 쿼드에 넘긴다
        private void UpdateDistortion()
        {
            float ringProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, ringDuration));
            float hazeT = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, hazeDuration));
            // 0.08초 만에 켜졌다가 서서히 사라진다
            float hazeIntensity = Mathf.Clamp01(elapsed / 0.08f) * (1.0f - hazeT * hazeT);

            distortionBlock.SetFloat(ProgressId, ringProgress);
            distortionBlock.SetFloat(IntensityId, hazeIntensity);
            distortionBlock.SetFloat(TimeSeedId, bodySeed * 0.01f);
            distortionRenderer.SetPropertyBlock(distortionBlock);
            distortionRenderer.enabled = elapsed < Mathf.Max(ringDuration, hazeDuration);
        }

        private void HideRenderers()
        {
            if (null != bodyRenderer) bodyRenderer.enabled = false;
            if (null != emberRenderer) emberRenderer.enabled = false;
            if (null != distortionRenderer) distortionRenderer.enabled = false;
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
            framesSinceEnable++;

            if (elapsed >= totalDuration && false == HasActiveEmbers())
            {
                HideRenderers();
                return;
            }

            // 이후에 피어오르는 불씨(저사양 모드에서는 절반)
            if (elapsed < EmberTrickleEnd && 0.0f < emberTrickleRate)
            {
                trickleAccumulator += emberTrickleRate * (true == IsHeavyLoad() ? 0.5f : 1.0f) * dt;
                int spawnCount = Mathf.Min(6, (int)trickleAccumulator);
                trickleAccumulator -= (int)trickleAccumulator;
                for (int i = 0; i < spawnCount; i++) SpawnTrickleEmber();
            }

            UpdateEmbers(dt);

            // 몸통 메쉬는 매 프레임 다시 만들고(저사양 모드에서만 30fps로 제한), 화면 밖이면(첫 몇 프레임은 렌더 전이라 보이는 것으로 본다) 건너뛴다.
            bodyTimer += dt;
            float bodyInterval = true == IsHeavyLoad() ? 1.0f / LowQualityFps : 0.0f;
            bool bVisible = 3 > framesSinceEnable || true == bodyRenderer.isVisible || true == emberRenderer.isVisible || true == distortionRenderer.isVisible;
            if (bodyTimer >= bodyInterval && true == bVisible)
            {
                bodyTimer = 0.0f;
                RebuildBody();
            }

            if (true == bVisible) RebuildEmbers();

            SyncTransformsAndSorting();
            UpdateDistortion();

            bodyRenderer.enabled = true;
            emberRenderer.enabled = true;
        }

        private void OnDisable()
        {
            HideRenderers();
            if (true == bCounted)
            {
                bCounted = false;
                activeCount = Mathf.Max(0, activeCount - 1);
            }
        }

        private void OnDestroy()
        {
            if (null != bodyMesh)
            {
                Destroy(bodyMesh);
                bodyMesh = null;
            }

            if (null != emberMesh)
            {
                Destroy(emberMesh);
                emberMesh = null;
            }
        }
    }
}
