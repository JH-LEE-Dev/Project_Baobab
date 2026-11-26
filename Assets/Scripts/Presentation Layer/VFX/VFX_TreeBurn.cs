using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 나무의 화상(과열 지속 피해) 상태를 알리는 루프 이펙트. 푸른 불꽃(여우불)이 나무 둘레를 둥실둥실 떠다니며 꼬리를 끌고, 나무 줄기에서는 작은 푸른 불꽃 혀가 이글거리며 불씨가 피어오른다.
    /// 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬(VFX_TreeHeatBurst, VFX_ChargeVortex와 같은 방식)이며 나무 프리팹의 비활성 자식으로 붙어 있다가 TreeObj가 켜고 끈다(풀 불필요).
    ///
    /// 구성
    /// 1. 여우불: 나뭇잎(수관)의 고정된 자리에 붙어서 그 둘레를 둥실둥실 떠다닌다(천천히 위아래로 출렁이고 노이즈로 떠다니며, 흔들림이 가끔 커졌다 잦아든다). 머리는 작은 코어 +
    ///    위로 뾰족한 끝(길이가 프레임마다 이글거림)이고 뚱뚱해지지 않도록 작게 유지한다. 일부는 나무 뒤 층에 두어 잎 사이로 반쯤 가려지게 한다(나무 프리팹 SortingGroup 안의 상대 정렬).
    /// 2. 꼬리(트레일): 지나간 위치를 기록해서 움직임을 따라 휘어지며 이어지는 픽셀 선. 머리에서 멀어질수록 가늘어지고 진해지고 끝은 체크무늬로 사라진다. 그 위에 불꽃 갈래 둘이 위로 일렁인다.
    /// 3. 줄기 불꽃 혀: 나무 줄기 몇 군데에서 날카로운 푸른 불꽃이 길이를 바꾸며 일렁인다(나무에 불이 붙어 타는 느낌).
    /// 4. 불씨: `.` `+` `x` 모양의 작은 불씨가 여우불과 줄기에서 피어올라 노이즈로 휘날리며 식어 사라진다.
    ///
    /// 켜짐(SetBurning true)에는 0.15초에 걸쳐 나타나고, 꺼짐(false)에는 여우불이 위로 떠오르며 0.35초 만에 사라진 뒤 스스로 비활성화된다. 색은 파랑 3가지(진한 색, 중간 색, 밝은 색)만 쓰고
    /// 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로 낸다.
    ///
    /// 최적화: 꺼져 있을 때는 오브젝트가 비활성이라 비용이 0이고 메쉬는 처음 켜질 때 한 번만 만든다. 모든 데이터는 고정 배열/구조체이고 쿼드는 스레드별 작업 버퍼에 모아 올리므로 런타임 할당이 없다.
    /// 여우불·불씨 이동과 픽셀 채우기(유니티 API를 쓰지 않는 계산)는 LateUpdate에서 하지 않고 PixelVfxWorkQueue에 등록해, 모든 LateUpdate가 끝난 직후
    /// 일꾼 스레드에서 나눠 계산한 뒤(ComputeDeferred) 주 스레드에서 메쉬에 올린다(ApplyDeferred). 계산식과 순서는 그대로라 결과는 같다.
    /// 동시에 타는 나무가 많으면 여우불 수와 꼬리 해상도를 줄이고, 화면 밖이면 메쉬 재구성을 건너뛴다.
    /// 움직임 폭이 1~2px이라 앞 프레임과 같은 그림이 자주 나오므로 내용 해시가 같으면 업로드를 건너뛴다. 메쉬 경계는 실제로 그린 픽셀 범위로 좁게 잡고,
    /// 화면 안/밖은 이번 프레임 메인 카메라 절두체로 판단한다(PixelVfxCulling). 고정 발광 세기의 색은 켜질 때 미리 계산한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_TreeBurn : MonoBehaviour, IPixelVfxDeferred
    {
        [Header("렌더링")]
        [SerializeField] private Material burnMaterial;
        [SerializeField, Tooltip("나무와 같은 소팅 레이어 이름(나무 프리팹의 SortingGroup 안에서 상대 정렬된다)")] private string sortingLayerName = "Objects";
        [SerializeField, Tooltip("나무 뒤쪽 반원의 여우불 정렬 순서(나무 스프라이트 0~1보다 낮게)")] private int backSortingOrder = -1;
        [SerializeField, Tooltip("나무 앞쪽 반원의 여우불, 줄기 불꽃 혀, 불씨 정렬 순서(나무 스프라이트보다 높게)")] private int frontSortingOrder = 10;

        [Header("여우불")]
        [SerializeField, Range(1, 6), Tooltip("나뭇잎에 붙어 떠다니는 여우불 수")] private int wispCount = 3;
        [SerializeField, Range(0, 6), Tooltip("그중 나무 뒤 층에 두는 여우불 수(잎 사이로 반쯤 가려진다)")] private int backWispCount = 1;
        [SerializeField, Tooltip("수관(나뭇잎 영역)의 좌우 반폭(나무 중심 기준 px, Tree.prefab 수관 약 ±13px)")] private float canopyHalfWidthPx = 11.0f;
        [SerializeField, Tooltip("수관 아래쪽 높이(나무 밑동 기준 px, Tree.prefab 수관은 약 6~39px)")] private float canopyMinYPx = 12.0f;
        [SerializeField, Tooltip("수관 위쪽 높이(나무 밑동 기준 px)")] private float canopyMaxYPx = 38.0f;
        [SerializeField, Tooltip("앵커 주변에서 노이즈로 떠다니는 범위(px)")] private float hoverRadiusPx = 1.2f;
        [SerializeField, Tooltip("둥실거리는 위아래 출렁임의 크기(px)")] private float bobAmplitudePx = 1.5f;
        [SerializeField, Tooltip("위아래 출렁임 주기의 최솟값(초)")] private float bobPeriodMin = 1.2f;
        [SerializeField, Tooltip("위아래 출렁임 주기의 최댓값(초)")] private float bobPeriodMax = 2.0f;
        [SerializeField, Tooltip("머리 끝(뾰족한 불꽃)의 길이 범위 최솟값(px)")] private float tipMinPx = 4.0f;
        [SerializeField, Tooltip("머리 끝(뾰족한 불꽃)의 길이 범위 최댓값(px)")] private float tipMaxPx = 8.0f;

        [Header("꼬리(트레일)")]
        [SerializeField, Tooltip("꼬리 위치를 기록하는 간격(초). 클수록 꼬리가 길어진다")] private float trailInterval = 0.04f;

        [Header("줄기 불꽃 혀")]
        [SerializeField, Range(0, 6), Tooltip("나무 줄기에서 일렁이는 불꽃 혀 수")] private int tongueCount = 4;
        [SerializeField, Tooltip("불꽃 혀 길이의 최댓값(px)")] private float tongueMaxPx = 9.0f;

        [Header("불씨")]
        [SerializeField, Tooltip("초당 피어오르는 불씨 수")] private float emberRate = 14.0f;

        [Header("켜짐 / 꺼짐")]
        [SerializeField, Tooltip("켜질 때 나타나는 시간(초)")] private float igniteDuration = 0.15f;
        [SerializeField, Tooltip("꺼질 때 사라지는 시간(초)")] private float extinguishDuration = 0.35f;

        [Header("최적화")]
        [SerializeField, Tooltip("동시에 타는 나무가 이 수를 넘으면 여우불 수와 꼬리 해상도를 줄인다")] private int heavyLoadCount = 8;

        [Header("밝기 (정점 알파 = HDR 발광 세기 0~1)")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("전체 발광 배율. 1이면 기준값")] private float glowScale = 1.0f;

        private const float TwoPi = Mathf.PI * 2.0f;
        private const int MaxWisps = 6;
        private const int HistoryLength = 18;        // 여우불마다 기억하는 꼬리 위치 수
        private const int MaxTongues = 6;
        private const int EmberCapacity = 28;
        private const int MaxLinePixels = 48;
        private const float TrunkHalfWidthPx = 5.0f; // 줄기 불꽃 혀가 붙는 가로 범위
        private const float PixelUnit = 1.0f / 32.0f;
        private const float BoundsDepth = 10.0f;
        private const float EmberShapeMarginPx = 2.0f;   // 불씨 모양(최대 3x3)이 위치에서 벗어나는 폭
        private const float CullingMarginUnits = PixelVfxCulling.MarginUnits; // 화면 안/밖 판단 여유(새로 생기거나 움직이는 조각, 카메라 이동)

        // 파랑 3색[진한 색, 중간 색, 밝은 색]. 드론 과열 이펙트와 같은 팔레트이며, HDR 배율이 곱해져도 흰색으로 날아가지 않도록 기본 색은 어둡게 잡았다.
        private const int ToneDeep = 0;
        private const int ToneMid = 1;
        private const int ToneLight = 2;
        private static readonly Color32[] Palette =
        {
            new Color32(12, 50, 170, 255),
            new Color32(20, 115, 220, 255),
            new Color32(110, 200, 240, 255),
        };

        private enum BurnState
        {
            Burning,
            Extinguishing,
        }

        private struct Wisp
        {
            public float anchorX;    // 수관 안의 고정 자리(나무 중심, 밑동 기준 px)
            public float anchorY;
            public float bobPhase;
            public float bobSpeed;   // 출렁임 각속도(rad/s)
            public bool bBehind;     // 나무 뒤 층에 그리는 여우불
            public int seed;
            public int historyHead;
            public float historyAge; // 마지막 기록 이후 지난 시간
        }

        private struct Tongue
        {
            public float x;
            public float y;
            public int seed;
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
            public int kind;   // 0: `.`, 1: `+`, 2: `x`, 3: 대시
            public int seed;
            public bool bFront;
        }

        // 층(뒤/앞) 메쉬마다 마지막으로 올린 내용과 경계
        private struct LayerUploadState
        {
            public int vertexCount;      // -1이면 아직 안 올렸다
            public ulong hash;
            public bool bHasBounds;
            public Vector2 boundsMin;
            public Vector2 boundsMax;
            public Vector3 appliedCenter;
            public Vector3 appliedSize;
        }

        private static int activeCount;

        // 작업 버퍼의 한 구간(이 인스턴스가 이번에 그린 한 층)의 자리와 내용 요약
        private struct LayerSegment
        {
            public int start;
            public int count;
            public ulong hash;
            public bool bHasBounds;
            public Vector2 boundsMin;
            public Vector2 boundsMax;
        }

        // 화면에 그릴 쿼드를 모으는 스레드별 작업 버퍼. 계산이 일꾼 스레드에서 돌 수 있어 스레드마다 한 벌씩 둔다.
        // 한 번의 계산 묶음(PixelVfxWorkQueue.Serial) 동안 그 스레드가 맡은 인스턴스들의 그림을 구간으로 이어 담고, 묶음이 바뀌면 비운다.
        private sealed class Work
        {
            [System.ThreadStatic] private static Work current;

            public readonly PixelQuadBuffer back = new PixelQuadBuffer(512);
            public readonly PixelQuadBuffer front = new PixelQuadBuffer(768);
            private int serial;
            private bool bUsed;

            public static Work ForCurrentThread()
            {
                Work work = current;
                if (null == work)
                {
                    work = new Work();
                    current = work;
                }

                int queueSerial = PixelVfxWorkQueue.Serial;
                if (false == work.bUsed || work.serial != queueSerial)
                {
                    work.bUsed = true;
                    work.serial = queueSerial;
                    work.back.Clear();
                    work.front.Clear();
                }

                return work;
            }
        }

        //내부 의존성
        private readonly Wisp[] wisps = new Wisp[MaxWisps];
        private readonly float[] historyX = new float[MaxWisps * HistoryLength];
        private readonly float[] historyY = new float[MaxWisps * HistoryLength];
        private readonly Tongue[] tongues = new Tongue[MaxTongues];
        private Ember[] embers = new Ember[EmberCapacity]; // 꽉 차면 두 배로 늘어난다(EmberCapacity는 처음 크기)
        private readonly Color32[] toneColors = new Color32[3];   // 톤마다 GlowOf 세기를 실은 색(켜질 때 미리 계산)
        private readonly Color32[] tongueColors = new Color32[3]; // 줄기 불꽃 혀용(GlowOf * 0.85)
        private MeshFilter backFilter;
        private MeshRenderer backRenderer;
        private Mesh backMesh;
        private MeshFilter frontFilter;
        private MeshRenderer frontRenderer;
        private Mesh frontMesh;

        //상태 변수
        private BurnState state;
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bCounted;
        private bool bTransformSynced;
        private bool bEmberSimHasBounds;
        private LayerUploadState backUpload;
        private LayerUploadState frontUpload;
        private Vector2 emberSimMinPx;       // 시뮬레이션 중인 불씨 위치 범위(px). 화면 밖이라 그리지 않은 프레임에도 갱신된다
        private Vector2 emberSimMaxPx;
        private Vector3 syncedParentScale;
        private Quaternion syncedParentRotation;
        private float elapsed;
        private float stateTime;
        private float emberAccumulator;
        private float intensity;
        private int framesSinceEnable;
        private int emberCursor;
        private int seedBase;
        private uint randomState;

        // 미룬 계산(PixelVfxWorkQueue): LateUpdate가 담아 두는 입력과, ComputeDeferred가 남겨 ApplyDeferred가 메쉬에 올리는 결과
        private Work work;                        // 계산 중에 쓰는 작업 버퍼(계산한 스레드의 것). 반영이 끝나면 비운다
        private int deferredSlot = -1;            // 대기열 자리. -1이면 미뤄 둔 계산이 없다
        private float deferredDt;
        private bool bHeavyLoad;                  // 이번 LateUpdate 시점의 저사양 모드 여부(동시에 타는 나무 수는 같은 프레임 안에서도 바뀐다)
        private bool bDeferredForceVisible;       // 처음 몇 프레임은 판정 없이 보이는 것으로 본다
        private bool bDeferredPrepared;           // 절두체를 준비해 뒀다(일꾼 스레드에서 판정해도 된다)
        private Vector3 deferredWorldPosition;    // LateUpdate에서 읽은 위치(화면 안/밖 판정용)
        private Vector3 deferredParentScale;      // LateUpdate에서 읽은 부모 스케일/회전(SyncTransforms용)
        private Quaternion deferredParentRotation;
        private int deferredVisibility;           // 1 보임, 0 안 보임, -1 아직 모름(반영 때 주 스레드에서 확정)
        // 고정 박자(PixelVfxClock): 마지막으로 계산한 박자 번호, 이 인스턴스의 박자 어긋남, 계산을 쉰 프레임 동안 모아 둔 시간,
        // 마지막으로 계산한 프레임에 화면에 보였는지(안 보였으면 더 낮은 박자로 돈다).
        private int lastStepTick;
        private float stepPhase;
        private float pendingDeltaTime;
        private bool bLastStepVisible;
        private Vector3 deferredCullCenter;
        private Vector3 deferredCullSize;
        private LayerSegment backSegment;
        private LayerSegment frontSegment;

        // ---------- 외부 제어 ----------

        /// <summary>
        /// 화상 이펙트를 켜거나 끈다(TreeObj가 화상 지속 피해가 시작/끝날 때 부른다). 꺼져 있던 오브젝트는 켜면서 처음부터 시작하고,
        /// 꺼지는 중에 다시 켜면 되살아난다. false는 위로 떠오르며 사라지는 연출 뒤 스스로 비활성화된다.
        /// </summary>
        public void SetBurning(bool _bBurning)
        {
            // 이번 프레임에 미뤄 둔 계산이 상태(state)를 읽으므로 바꾸기 전에 먼저 끝낸다(예전에는 LateUpdate에서 이미 끝나 있었다)
            CompleteDeferred();

            if (true == _bBurning)
            {
                if (false == gameObject.activeSelf)
                {
                    gameObject.SetActive(true); // OnEnable -> Begin이 Burning 상태로 시작한다
                    return;
                }

                if (BurnState.Extinguishing == state)
                {
                    state = BurnState.Burning;
                    stateTime = 0.0f;
                }

                return;
            }

            if (true == gameObject.activeSelf && BurnState.Burning == state)
            {
                state = BurnState.Extinguishing;
                stateTime = 0.0f;
            }
        }

        /// <summary>
        /// 연출 없이 즉시 끈다(나무가 죽거나 풀로 돌아갈 때).
        /// </summary>
        public void StopImmediate()
        {
            if (true == gameObject.activeSelf) gameObject.SetActive(false);
        }

        // ---------- 초기화 ----------

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            backMesh = CreateMeshObject("TreeBurn_Back", backSortingOrder, out backFilter, out backRenderer);
            frontMesh = CreateMeshObject("TreeBurn_Front", frontSortingOrder, out frontFilter, out frontRenderer);
        }

        private Mesh CreateMeshObject(string _name, int _sortingOrder, out MeshFilter _filter, out MeshRenderer _renderer)
        {
            GameObject child = new GameObject(_name);
            child.transform.SetParent(transform, false);

            _filter = child.AddComponent<MeshFilter>();
            _renderer = child.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh { name = "VFX_TreeBurn_" + _name };
            mesh.MarkDynamic();
            _filter.sharedMesh = mesh;

            _renderer.sharedMaterial = burnMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            _renderer.sortingOrder = _sortingOrder;
            _renderer.enabled = false;
            return mesh;
        }

        private void Begin()
        {
            EnsureInitialized();

            state = BurnState.Burning;
            stateTime = 0.0f;
            elapsed = 0.0f;
            emberAccumulator = 0.0f;
            intensity = 0.0f;
            framesSinceEnable = 0;
            lastStepTick = int.MinValue;
            stepPhase = PixelVfxClock.PhaseFromInstance(GetInstanceID());
            pendingDeltaTime = 0.0f;
            bLastStepVisible = true;
            emberCursor = 0;
            bFirstFrame = true;
            bTransformSynced = false;
            bEmberSimHasBounds = false;
            ResetUploadState(ref backUpload);  // 메쉬에 지난번 그림이 남아 있을 수 있으므로 첫 프레임은 무조건 올린다
            ResetUploadState(ref frontUpload);

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 131.0f + pos.y * 57.0f));
            if (0u == randomState) randomState = 1u;
            seedBase = (int)(Rand01() * 10000.0f);

            BuildToneColors();

            for (int i = 0; i < embers.Length; i++) embers[i].bActive = false;

            BuildWisps();
            BuildTongues();

            if (false == bCounted)
            {
                bCounted = true;
                activeCount++;
            }
        }

        // 여우불이 붙을 수관 안의 자리와 출렁임 위상을 켜질 때 한 번 뽑는다. 자리는 황금비 간격으로 흩뿌려서 서로 겹치지 않게 한다.
        private void BuildWisps()
        {
            float offsetX = Rand01();
            float offsetY = Rand01();
            for (int i = 0; i < MaxWisps; i++)
            {
                float u = Mathf.Repeat(i * 0.618034f + offsetX, 1.0f);
                float v = Mathf.Repeat(i * 0.381966f + offsetY, 1.0f);
                wisps[i].anchorX = Mathf.Lerp(-canopyHalfWidthPx, canopyHalfWidthPx, u);
                wisps[i].anchorY = Mathf.Lerp(canopyMinYPx, canopyMaxYPx, v);
                wisps[i].bobPhase = Rand01() * TwoPi;
                wisps[i].bobSpeed = TwoPi / Mathf.Max(0.3f, Mathf.Lerp(bobPeriodMin, bobPeriodMax, Rand01()));
                wisps[i].bBehind = i < backWispCount;
                wisps[i].seed = (int)(Rand01() * 1000.0f);
                wisps[i].historyHead = 0;
                wisps[i].historyAge = 0.0f;

                // 꼬리 기록을 현재 위치로 채운다(처음에는 머리만 보이다가 움직이며 꼬리가 자란다)
                float x;
                float y;
                GetWispPosition(i, out x, out y);
                for (int k = 0; k < HistoryLength; k++)
                {
                    historyX[i * HistoryLength + k] = x;
                    historyY[i * HistoryLength + k] = y;
                }
            }
        }

        // 줄기 불꽃 혀가 붙는 자리(줄기 가운데 좁은 폭, 여러 높이)를 켜질 때 한 번 뽑는다
        private void BuildTongues()
        {
            for (int i = 0; i < MaxTongues; i++)
            {
                tongues[i].x = (Rand01() - 0.5f) * 2.0f * TrunkHalfWidthPx;
                tongues[i].y = Mathf.Lerp(6.0f, 36.0f, (i + Rand01() * 0.7f) / MaxTongues);
                tongues[i].seed = (int)(Rand01() * 1000.0f);
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

        // 이번 LateUpdate 시점에 정해 둔 값(계산이 미뤄져 돌 때도 LateUpdate 때의 값을 쓴다)
        private bool IsHeavyLoad()
        {
            return bHeavyLoad;
        }

        private byte GlowByte(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow * glowScale) * 255.0f);
        }

        private Color32 Tint(int _tone, float _glow)
        {
            Color32 color = Palette[_tone];
            return new Color32(color.r, color.g, color.b, GlowByte(_glow));
        }

        private static float GlowOf(int _tone)
        {
            return ToneLight == _tone ? 1.0f : (ToneMid == _tone ? 0.8f : 0.5f);
        }

        // 고정 발광 세기 색을 미리 만든다(머리, 불씨: GlowOf / 줄기 불꽃 혀: GlowOf * 0.85). 세기가 거리에 따라 바뀌는 꼬리만 Tint를 직접 부른다.
        private void BuildToneColors()
        {
            for (int tone = 0; tone < toneColors.Length; tone++)
            {
                toneColors[tone] = Tint(tone, GlowOf(tone));
                tongueColors[tone] = Tint(tone, GlowOf(tone) * 0.85f);
            }
        }

        private static void AddPixel(PixelQuadBuffer _buffer, int _x, int _y, Color32 _color)
        {
            _buffer.AddLocalRect(_x, _y, _x + 1, _y + 1, _color);
        }

        // ---------- 여우불 위치 ----------

        // 앵커 주변의 위치: 천천히 위아래로 출렁이고(둥실), 노이즈와 느린 좌우 흔들림으로 떠다닌다. 흔들림의 크기는 느린 펄스로 커졌다 잦아들어서 한 자리에 멈춘 느낌이 들지 않는다.
        private void GetWispPosition(int _index, out float _x, out float _y)
        {
            float t = elapsed;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 0.6f + wisps[_index].bobPhase * 1.7f);
            float amplitude = 1.0f + 0.5f * pulse * pulse;

            float bob = Mathf.Sin(t * wisps[_index].bobSpeed + wisps[_index].bobPhase) * bobAmplitudePx * amplitude;
            float sway = Mathf.Sin(t * wisps[_index].bobSpeed * 0.55f + wisps[_index].bobPhase * 2.1f) * hoverRadiusPx * 0.8f;
            float noiseX = (ValueNoise(t * 0.9f + wisps[_index].seed, 3.1f, 7) - 0.5f) * 2.0f * hoverRadiusPx * amplitude;
            float noiseY = (ValueNoise(t * 0.8f + wisps[_index].seed, 9.7f, 13) - 0.5f) * 2.0f * hoverRadiusPx * 0.8f * amplitude;

            _x = wisps[_index].anchorX + sway + noiseX;
            _y = wisps[_index].anchorY + bob + noiseY;
        }

        // ---------- 갱신 ----------

        private void UpdateWisps(float _dt)
        {
            int count = true == IsHeavyLoad() ? Mathf.Min(3, wispCount) : Mathf.Min(MaxWisps, wispCount);
            for (int i = 0; i < count; i++)
            {
                // 꼬리 위치를 일정 간격으로 기록한다(저사양 모드에서는 간격을 늘려 기록 횟수를 줄인다)
                wisps[i].historyAge += _dt;
                float interval = true == IsHeavyLoad() ? trailInterval * 1.5f : trailInterval;
                if (wisps[i].historyAge >= interval)
                {
                    wisps[i].historyAge = 0.0f;
                    float x;
                    float y;
                    GetWispPosition(i, out x, out y);
                    int head = (wisps[i].historyHead + 1) % HistoryLength;
                    wisps[i].historyHead = head;
                    historyX[i * HistoryLength + head] = x;
                    historyY[i * HistoryLength + head] = y;
                }
            }
        }

        private void SpawnEmbers(float _dt)
        {
            if (BurnState.Burning != state) return;

            emberAccumulator += emberRate * (true == IsHeavyLoad() ? 0.5f : 1.0f) * intensity * _dt;
            int count = Mathf.Min(3, (int)emberAccumulator);
            emberAccumulator -= (int)emberAccumulator;

            for (int n = 0; n < count; n++)
            {
                // 꽉 차면 아직 살아있는 불씨를 덮어쓰지 않고 배열을 두 배로 늘려 늘어난 첫 자리에 넣는다
                if (true == embers[emberCursor].bActive)
                {
                    int oldLength = embers.Length;
                    System.Array.Resize(ref embers, oldLength * 2);
                    emberCursor = oldLength;
                }

                int slot = emberCursor;
                emberCursor = (emberCursor + 1) % embers.Length;

                // 절반은 여우불에서, 절반은 줄기의 불꽃 혀에서 피어오른다
                float x;
                float y;
                if (Rand01() < 0.5f)
                {
                    int w = Mathf.Min(MaxWisps - 1, (int)(Rand01() * wispCount));
                    GetWispPosition(w, out x, out y);
                }
                else
                {
                    int t = Mathf.Min(MaxTongues - 1, (int)(Rand01() * Mathf.Max(1, tongueCount)));
                    x = tongues[t].x;
                    y = tongues[t].y + 3.0f;
                }

                embers[slot].bActive = true;
                embers[slot].x = x + (Rand01() - 0.5f) * 3.0f;
                embers[slot].y = y;
                embers[slot].vx = (Rand01() - 0.5f) * 10.0f;
                embers[slot].vy = Mathf.Lerp(14.0f, 34.0f, Rand01());
                embers[slot].age = 0.0f;
                embers[slot].life = Mathf.Lerp(0.5f, 1.1f, Rand01());
                float roll = Rand01();
                embers[slot].kind = roll < 0.5f ? 0 : (roll < 0.65f ? 1 : (roll < 0.8f ? 2 : 3));
                embers[slot].seed = (int)(Rand01() * 1000.0f);
                embers[slot].bFront = Rand01() < 0.6f;
            }
        }

        // 불씨는 위로 뜨면서 노이즈 바람에 좌우로 휘날린다. 화면 안/밖 판단을 위해 살아 있는 불씨 위치의 범위(px)도 함께 모은다.
        private void UpdateEmbers(float _dt)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < embers.Length; i++)
            {
                if (false == embers[i].bActive) continue;

                embers[i].age += _dt;
                if (embers[i].age >= embers[i].life)
                {
                    embers[i].bActive = false;
                    continue;
                }

                float flow = ValueNoise(embers[i].x * 0.08f + elapsed * 1.1f, embers[i].y * 0.08f, 19) - 0.5f;
                embers[i].vx = embers[i].vx * 0.96f + flow * 40.0f * _dt;
                embers[i].x += embers[i].vx * _dt;
                embers[i].y += embers[i].vy * _dt;

                if (embers[i].x < minX) minX = embers[i].x;
                if (embers[i].y < minY) minY = embers[i].y;
                if (embers[i].x > maxX) maxX = embers[i].x;
                if (embers[i].y > maxY) maxY = embers[i].y;
            }

            bEmberSimHasBounds = minX <= maxX;
            if (true == bEmberSimHasBounds)
            {
                emberSimMinPx = new Vector2(minX - EmberShapeMarginPx, minY - EmberShapeMarginPx);
                emberSimMaxPx = new Vector2(maxX + EmberShapeMarginPx, maxY + EmberShapeMarginPx);
            }
        }

        private bool HasActiveEmbers()
        {
            for (int i = 0; i < embers.Length; i++)
            {
                if (true == embers[i].bActive) return true;
            }

            return false;
        }

        // ---------- 그리기 ----------

        // 두 층을 작업 버퍼의 새 구간에 그린다(계산만 한다. 메쉬에 올리는 것은 UploadLayer).
        private void DrawAll()
        {
            PixelQuadBuffer backBuffer = work.back;
            PixelQuadBuffer frontBuffer = work.front;
            backSegment.start = backBuffer.BeginSegment();
            frontSegment.start = frontBuffer.BeginSegment();

            int wispDraw = true == IsHeavyLoad() ? Mathf.Min(3, wispCount) : Mathf.Min(MaxWisps, wispCount);
            for (int i = 0; i < wispDraw; i++) DrawWisp(i);
            DrawTongues();
            DrawEmbers();

            backSegment.count = backBuffer.VertexCount - backSegment.start;
            backSegment.hash = backBuffer.ContentHash;
            backSegment.bHasBounds = backBuffer.TryGetLocalBounds(out backSegment.boundsMin, out backSegment.boundsMax);
            frontSegment.count = frontBuffer.VertexCount - frontSegment.start;
            frontSegment.hash = frontBuffer.ContentHash;
            frontSegment.bHasBounds = frontBuffer.TryGetLocalBounds(out frontSegment.boundsMin, out frontSegment.boundsMax);
        }

        // 여우불 하나: 꼬리(기록된 위치를 이은 선) -> 머리 순으로 그려서 머리가 꼬리 위에 오게 한다. 나무 뒤 층 여우불이면 뒤 층, 아니면 앞 층에 그린다.
        private void DrawWisp(int _index)
        {
            float x;
            float y;
            GetWispPosition(_index, out x, out y);

            // 꺼지는 중에는 위로 떠오르며 작아진다
            float lift = (1.0f - intensity) * 14.0f;
            int headX = Mathf.RoundToInt(x);
            int headY = Mathf.RoundToInt(y + lift);
            PixelQuadBuffer buffer = true == wisps[_index].bBehind ? work.back : work.front;

            DrawTrail(_index, buffer, headX, headY, lift);
            DrawHead(_index, buffer, headX, headY);
        }

        // 꼬리: 기록된 위치(최신 -> 오래된 순)를 픽셀 선으로 잇는다. 머리 쪽은 굵고 밝고, 뒤로 갈수록 가늘고 진하고, 마지막 구간은 체크무늬로 사라진다.
        private void DrawTrail(int _index, PixelQuadBuffer _buffer, int _headX, int _headY, float _lift)
        {
            int baseIndex = _index * HistoryLength;
            int head = wisps[_index].historyHead;
            int segments = true == IsHeavyLoad() ? HistoryLength / 2 : HistoryLength;
            int keep = Mathf.RoundToInt(segments * intensity);

            int lastX = _headX;
            int lastY = _headY;
            for (int k = 0; k < keep; k++)
            {
                int slot = baseIndex + (head - k + HistoryLength) % HistoryLength;
                int x = Mathf.RoundToInt(historyX[slot]);
                int y = Mathf.RoundToInt(historyY[slot] + _lift);
                if (x == lastX && y == lastY) continue;

                float fraction = (float)k / segments;
                int tone = fraction < 0.25f ? ToneMid : ToneDeep;
                float glow = Mathf.Lerp(0.8f, 0.3f, fraction);
                bool bChecker = fraction > 0.7f;
                bool bThick = fraction < 0.3f;
                DrawLine(_buffer, lastX, lastY, x, y, Tint(tone, glow), bThick, bChecker);
                lastX = x;
                lastY = y;
            }
        }

        // 두 점 사이를 1px 선(브레젠험)으로 채운다(시작점은 이미 그려졌으므로 제외). _bThick이면 옆에 한 칸 더 붙이고 _bChecker이면 체크무늬 칸만 찍는다.
        private static void DrawLine(PixelQuadBuffer _buffer, int _x0, int _y0, int _x1, int _y1, Color32 _color, bool _bThick, bool _bChecker)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = Mathf.Abs(_y1 - _y0);
            int stepX = _x0 < _x1 ? 1 : -1;
            int stepY = _y0 < _y1 ? 1 : -1;
            int error = dx - dy;
            int x = _x0;
            int y = _y0;
            bool bHorizontal = dx >= dy;

            for (int i = 0; i < MaxLinePixels && (x != _x1 || y != _y1); i++)
            {
                int doubled = error * 2;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += stepX;
                }
                if (doubled < dx)
                {
                    error += dx;
                    y += stepY;
                }

                if (true == _bChecker && 0 != ((x + y) & 1)) continue;
                AddPixel(_buffer, x, y, _color);
                if (true == _bThick)
                {
                    if (true == bHorizontal) AddPixel(_buffer, x, y + 1, _color);
                    else AddPixel(_buffer, x + 1, y, _color);
                }
            }
        }

        // 머리: 3x3 밝은 코어, 좌우의 중간 색 테두리 한 점씩, 위로 뾰족하게 솟은 끝(길이가 프레임마다 이글거리고 위쪽으로 갈수록 흔들린다), 끝 옆의 불꽃 갈래 둘.
        private void DrawHead(int _index, PixelQuadBuffer _buffer, int _headX, int _headY)
        {
            float t = elapsed * 18.0f;
            int seed = wisps[_index].seed;
            float scale = Mathf.Clamp01(intensity * 1.2f);
            if (0.1f > scale) return;

            // 코어(3x3 밝은 색)와 테두리
            Color32 light = toneColors[ToneLight]; // GlowOf(ToneLight) = 1.0
            Color32 mid = toneColors[ToneMid];     // GlowOf(ToneMid) = 0.8
            _buffer.AddLocalRect(_headX - 1, _headY - 1, _headX + 2, _headY + 2, light);
            AddPixel(_buffer, _headX - 2, _headY, mid);
            AddPixel(_buffer, _headX + 2, _headY, mid);

            // 뾰족한 끝: 길이는 노이즈로 이글거리고, 끝이 위로 갈수록 한쪽으로 휜다
            float lengthNoise = ValueNoise(t * 0.5f + seed, 1.7f, 3);
            int tip = Mathf.RoundToInt(Mathf.Lerp(tipMinPx, tipMaxPx, lengthNoise) * scale);
            float bend = (ValueNoise(t * 0.35f + seed, 5.3f, 5) - 0.5f) * 3.0f;
            for (int k = 2; k <= tip + 1; k++)
            {
                float fraction = (float)(k - 1) / (tip + 1);
                int offset = Mathf.RoundToInt(bend * fraction * fraction);
                int tone = fraction < 0.45f ? ToneLight : (fraction < 0.8f ? ToneMid : ToneDeep);
                AddPixel(_buffer, _headX + offset, _headY + k, toneColors[tone]);
                if (k <= 3) // 밑동은 두 칸 폭으로 굵게
                {
                    AddPixel(_buffer, _headX + offset - 1, _headY + k, mid);
                    AddPixel(_buffer, _headX + offset + 1, _headY + k, mid);
                }
            }

            // 끝 옆의 불꽃 갈래 둘: 짧고 날카롭게 좌우로 일렁인다
            for (int side = -1; 1 >= side; side += 2)
            {
                float lickNoise = ValueNoise(t * 0.6f + seed * 1.3f + side * 11.0f, 8.1f, 9);
                int lick = Mathf.RoundToInt(Mathf.Lerp(2.0f, 5.0f, lickNoise) * scale);
                int sway = Mathf.RoundToInt((lickNoise - 0.5f) * 2.0f);
                for (int k = 1; k <= lick; k++)
                {
                    int tone = k <= lick / 2 ? ToneMid : ToneDeep;
                    AddPixel(_buffer, _headX + side * 2 + sway * (k / 2), _headY + k, toneColors[tone]);
                }
            }
        }

        // 줄기 불꽃 혀: 줄기의 정해진 자리에서 위로 솟는 날카로운 불꽃(밑동 2칸, 끝 1칸). 길이는 노이즈로 이글거리고 끝이 좌우로 흔들린다. 항상 나무 앞 층이다.
        private void DrawTongues()
        {
            PixelQuadBuffer frontBuffer = work.front;
            int count = Mathf.Min(MaxTongues, tongueCount);
            float t = elapsed * 16.0f;
            for (int i = 0; i < count; i++)
            {
                float lengthNoise = ValueNoise(t * 0.4f + tongues[i].seed, 2.3f, 21);
                int length = Mathf.RoundToInt(Mathf.Lerp(2.0f, tongueMaxPx, lengthNoise) * intensity);
                if (2 > length) continue;

                int baseX = Mathf.RoundToInt(tongues[i].x);
                int baseY = Mathf.RoundToInt(tongues[i].y);
                float sway = (ValueNoise(t * 0.3f + tongues[i].seed, 6.1f, 23) - 0.5f) * 4.0f;
                for (int k = 0; k < length; k++)
                {
                    float fraction = (float)k / length;
                    int offset = Mathf.RoundToInt(sway * fraction * fraction);
                    int tone = fraction < 0.3f ? ToneLight : (fraction < 0.7f ? ToneMid : ToneDeep);
                    Color32 color = tongueColors[tone];
                    AddPixel(frontBuffer, baseX + offset, baseY + k, color);
                    if (fraction < 0.5f) AddPixel(frontBuffer, baseX + offset + 1, baseY + k, color);
                }
            }
        }

        // 불씨: `.`, `+`, `x`, 대시. `+`와 `x`는 0.09초마다 번갈아 바뀌어 회전하는 것처럼 보이고 후반에 깜빡이다 사라진다.
        private void DrawEmbers()
        {
            PixelQuadBuffer backBuffer = work.back;
            PixelQuadBuffer frontBuffer = work.front;
            for (int i = 0; i < embers.Length; i++)
            {
                if (false == embers[i].bActive) continue;

                float u = embers[i].age / embers[i].life;
                if (0.7f < u && 0 != ((int)(embers[i].age * 30.0f) & 1)) continue;

                PixelQuadBuffer buffer = true == embers[i].bFront ? frontBuffer : backBuffer;
                int tone = u < 0.35f ? ToneLight : (u < 0.7f ? ToneMid : ToneDeep);
                Color32 color = toneColors[tone];
                int px = Mathf.RoundToInt(embers[i].x);
                int py = Mathf.RoundToInt(embers[i].y);

                int kind = embers[i].kind;
                if ((1 == kind || 2 == kind) && u < 0.55f)
                {
                    int step = (int)(embers[i].age / 0.09f) + embers[i].seed;
                    bool bCross = 1 == kind ? 0 == (step & 1) : 0 != (step & 1);
                    if (true == bCross)
                    {
                        buffer.AddLocalRect(px - 1, py, px + 2, py + 1, color);
                        buffer.AddLocalRect(px, py - 1, px + 1, py + 2, color);
                    }
                    else
                    {
                        AddPixel(buffer, px, py, color);
                        AddPixel(buffer, px - 1, py - 1, color);
                        AddPixel(buffer, px + 1, py - 1, color);
                        AddPixel(buffer, px - 1, py + 1, color);
                        AddPixel(buffer, px + 1, py + 1, color);
                    }
                }
                else if (3 == kind)
                {
                    buffer.AddLocalRect(px, py, px + 1, py + 2, color); // 위로 뜨는 세로 대시
                }
                else
                {
                    AddPixel(buffer, px, py, color);
                }
            }
        }

        // ---------- 메쉬 갱신 ----------

        private static void ResetUploadState(ref LayerUploadState _state)
        {
            _state.vertexCount = -1;
            _state.hash = 0UL;
            _state.bHasBounds = false;
        }

        // 작업 버퍼에 그려 둔 한 층(구간)을 메쉬에 올린다. 직전에 올린 그림과 같으면(정점 수와 내용 해시가 같으면) 올리지 않는다.
        // 올릴 때는 메쉬 경계를 실제로 그린 픽셀 범위(1px 여유)로 좁게 잡는다.
        private static void UploadLayer(PixelQuadBuffer _buffer, in LayerSegment _segment, Mesh _mesh, ref LayerUploadState _state)
        {
            if (_segment.count == _state.vertexCount && _segment.hash == _state.hash) return;

            _buffer.UploadRange(_mesh, 0, _segment.start, _segment.count);
            _state.vertexCount = _segment.count;
            _state.hash = _segment.hash;
            _state.bHasBounds = _segment.bHasBounds;
            _state.boundsMin = _segment.boundsMin;
            _state.boundsMax = _segment.boundsMax;
            if (false == _state.bHasBounds) return; // 그릴 것이 없으면 경계는 상관없다

            Vector3 center = new Vector3((_state.boundsMin.x + _state.boundsMax.x) * 0.5f, (_state.boundsMin.y + _state.boundsMax.y) * 0.5f, 0.0f);
            Vector3 size = new Vector3(_state.boundsMax.x - _state.boundsMin.x + 2.0f * PixelUnit, _state.boundsMax.y - _state.boundsMin.y + 2.0f * PixelUnit, BoundsDepth);
            if (center == _state.appliedCenter && size == _state.appliedSize) return;

            _state.appliedCenter = center;
            _state.appliedSize = size;
            _mesh.bounds = new Bounds(center, size);
        }

        // 이번 프레임 메인 카메라에 보이는지: 두 층 메쉬에 올라가 있는 그림과 시뮬레이션 중인 불씨 위치를 합친 범위에 여유를 두고 절두체와 비교한다.
        // 범위를 알 수 없으면(아직 아무것도 없으면) 보이는 것으로 본다. 일꾼 스레드에서 불릴 수 있어 위치는 LateUpdate에서 읽어 둔 값을 쓴다.
        // 1 보임, 0 안 보임, -1 아직 모름(ApplyDeferred가 주 스레드에서 IsVisible로 확정). 절두체를 준비해 두지 않았으면 판정하지 않고 -1을 돌려준다.
        private int TestOnScreen()
        {
            bool bHas = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            if (true == backUpload.bHasBounds) Encapsulate(ref bHas, ref min, ref max, backUpload.boundsMin, backUpload.boundsMax);
            if (true == frontUpload.bHasBounds) Encapsulate(ref bHas, ref min, ref max, frontUpload.boundsMin, frontUpload.boundsMax);
            if (true == bEmberSimHasBounds) Encapsulate(ref bHas, ref min, ref max, emberSimMinPx * PixelUnit, emberSimMaxPx * PixelUnit);
            if (false == bHas) return 1;

            Vector3 center = deferredWorldPosition + new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0.0f);
            Vector3 size = new Vector3(max.x - min.x + 2.0f * CullingMarginUnits, max.y - min.y + 2.0f * CullingMarginUnits, BoundsDepth);
            deferredCullCenter = center;
            deferredCullSize = size;
            return true == bDeferredPrepared ? PixelVfxCulling.TestPrepared(center, size) : -1;
        }

        private static void Encapsulate(ref bool _bHas, ref Vector2 _min, ref Vector2 _max, Vector2 _addMin, Vector2 _addMax)
        {
            if (false == _bHas)
            {
                _bHas = true;
                _min = _addMin;
                _max = _addMax;
                return;
            }

            _min = Vector2.Min(_min, _addMin);
            _max = Vector2.Max(_max, _addMax);
        }

        // 부모(나무)의 스케일/회전을 지우고 도트 크기가 항상 1px이 되게 한다. 부모 스케일/회전이 바뀌었을 때만 다시 쓴다.
        private void SyncTransforms()
        {
            // 부모 스케일/회전은 LateUpdate에서 읽어 둔 값을 쓴다(반영이 미뤄져도 예전과 같은 시점의 값)
            Vector3 parentScale = deferredParentScale;
            Quaternion parentRotation = deferredParentRotation;
            bool bChanged = false == bTransformSynced
                || parentScale.x != syncedParentScale.x || parentScale.y != syncedParentScale.y || parentScale.z != syncedParentScale.z
                || parentRotation.x != syncedParentRotation.x || parentRotation.y != syncedParentRotation.y
                || parentRotation.z != syncedParentRotation.z || parentRotation.w != syncedParentRotation.w;
            if (false == bChanged) return;

            bTransformSynced = true;
            syncedParentScale = parentScale;
            syncedParentRotation = parentRotation;

            float inverseX = 0.0001f < Mathf.Abs(parentScale.x) ? 1.0f / parentScale.x : 1.0f;
            float inverseY = 0.0001f < Mathf.Abs(parentScale.y) ? 1.0f / parentScale.y : 1.0f;
            Vector3 inverse = new Vector3(inverseX, inverseY, 1.0f);

            backRenderer.transform.rotation = Quaternion.identity;
            backRenderer.transform.localScale = inverse;
            frontRenderer.transform.rotation = Quaternion.identity;
            frontRenderer.transform.localScale = inverse;
        }

        private void HideRenderers()
        {
            if (null != backRenderer) backRenderer.enabled = false;
            if (null != frontRenderer) frontRenderer.enabled = false;
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
            // 첫 프레임은 시간을 더하지 않아서 t=0의 첫 장면이 반드시 한 번 그려진다.
            // 그 뒤로는 고정 박자(PixelVfxClock)로만 계산한다: 쉬는 프레임에는 지나간 시간만 모아 두고, 계산하는 프레임에 한꺼번에 흘린다.
            if (true == bFirstFrame)
            {
                PixelVfxClock.ShouldStep(ref lastStepTick, stepPhase, PixelVfxClock.Rate);
            }
            else
            {
                pendingDeltaTime += Time.deltaTime;
                if (false == PixelVfxClock.ShouldStep(ref lastStepTick, stepPhase, true == bLastStepVisible ? PixelVfxClock.Rate : PixelVfxClock.OffScreenRate))
                {
                    // 쉬는 프레임에도 나무의 흔들림(스케일·회전)은 따라가 지운다. 이것까지 쉬면 맞은 나무 위에서 불꽃이 한 프레임씩 같이 늘어난다.
                    if (true == bLastStepVisible)
                    {
                        deferredParentScale = transform.lossyScale;
                        deferredParentRotation = transform.rotation;
                        SyncTransforms();
                    }

                    return;
                }
            }

            float dt = pendingDeltaTime;
            pendingDeltaTime = 0.0f;
            bFirstFrame = false;
            elapsed += dt;
            stateTime += dt;
            framesSinceEnable++;

            // 세기(0~1): 켜질 때 0 -> 1(igniteDuration), 꺼질 때 1 -> 0(extinguishDuration). 꺼지는 중에 다시 켜지면 지금 세기에서 이어서 오른다.
            if (BurnState.Burning == state)
            {
                intensity = Mathf.Min(1.0f, intensity + dt / Mathf.Max(0.01f, igniteDuration));
            }
            else
            {
                intensity = Mathf.Clamp01(intensity - dt / Mathf.Max(0.01f, extinguishDuration));
                if (0.0f >= intensity && false == HasActiveEmbers())
                {
                    gameObject.SetActive(false);
                    return;
                }
            }

            // 여우불·불씨 이동, 화면 안/밖 판정, 그리기는 유니티 API를 쓰지 않는 계산이라 미뤄서 일꾼 스레드에서 돌린다(ComputeDeferred).
            // 여기서는 그 계산과 반영이 읽을 유니티 상태를 예전과 같은 시점에 읽어 둔다.
            deferredDt = dt;
            bHeavyLoad = heavyLoadCount < activeCount;
            deferredParentScale = transform.lossyScale;
            deferredParentRotation = transform.rotation;

            // 화면 밖이면(첫 몇 프레임은 보이는 것으로 본다) 메쉬 재구성을 건너뛴다. 시간은 계속 흐르므로 다시 보일 때 어긋나지 않는다.
            // 예전에는 범위를 아는 경우에만 이 자리에서 절두체를 준비했다(PixelVfxCulling.IsVisible). 메쉬에 올린 그림의 범위가 있으면 반드시 그 경우라
            // 지금 준비해 두고 미룬다. 없으면 불씨 이동 결과에 달려 있어 미루지 않고 이 자리에서 예전 순서 그대로 끝낸다(절두체 준비 시점이 달라지지 않게).
            bDeferredForceVisible = 3 > framesSinceEnable;
            bDeferredPrepared = false;
            bool bRunNow = false;
            if (false == bDeferredForceVisible)
            {
                deferredWorldPosition = transform.position;
                bDeferredPrepared = true == backUpload.bHasBounds || true == frontUpload.bHasBounds;
                if (true == bDeferredPrepared) PixelVfxCulling.PrepareFrame();
                else bRunNow = true;
            }

            if (false == backRenderer.enabled) backRenderer.enabled = true;
            if (false == frontRenderer.enabled) frontRenderer.enabled = true;

            if (true == bRunNow) PixelVfxWorkQueue.ExecuteNow(this);
            else deferredSlot = PixelVfxWorkQueue.Enqueue(this);
        }

        // 미뤄 둔 계산: 여우불·불씨 이동 -> 화면 안/밖 판정 -> (보이면) 두 층을 작업 버퍼에 그린다. 일꾼 스레드에서 불릴 수 있다.
        // 예전 LateUpdate의 UpdateWisps ~ DrawAll에서 메쉬에 올리는 부분만 뺀 것이고 순서는 같다.
        void IPixelVfxDeferred.ComputeDeferred()
        {
            work = Work.ForCurrentThread();

            UpdateWisps(deferredDt);
            SpawnEmbers(deferredDt);
            UpdateEmbers(deferredDt);

            deferredVisibility = true == bDeferredForceVisible ? 1 : TestOnScreen();
            if (0 == deferredVisibility) return;

            // 아직 모르는 경우(-1)는 그려 두고, 안 보이는 것으로 확정되면 쓰지 않는다(상태는 반영 때만 바뀐다)
            DrawAll();
        }

        // 미뤄 둔 계산의 결과를 메쉬에 올린다(주 스레드). 예전 LateUpdate의 올리기 부분과 같은 순서다.
        void IPixelVfxDeferred.ApplyDeferred()
        {
            deferredSlot = -1;

            bool bVisible = 1 == deferredVisibility || (-1 == deferredVisibility && true == PixelVfxCulling.IsVisible(deferredCullCenter, deferredCullSize));
            if (true == bVisible)
            {
                UploadLayer(work.back, in backSegment, backMesh, ref backUpload);
                UploadLayer(work.front, in frontSegment, frontMesh, ref frontUpload);
                SyncTransforms();
            }

            bLastStepVisible = bVisible;
            work = null;
        }

        // 미뤄 둔 계산이 있으면 지금 끝낸다(꺼지거나 상태가 바뀌기 전에: 예전에는 LateUpdate에서 이미 끝나 있던 일이다)
        private void CompleteDeferred()
        {
            if (0 <= deferredSlot) PixelVfxWorkQueue.CompleteNow(deferredSlot, this);
        }

        private void OnDisable()
        {
            CompleteDeferred();
            HideRenderers();
            if (true == bCounted)
            {
                bCounted = false;
                activeCount = Mathf.Max(0, activeCount - 1);
            }
        }

        private void OnDestroy()
        {
            if (null != backMesh)
            {
                Destroy(backMesh);
                backMesh = null;
            }

            if (null != frontMesh)
            {
                Destroy(frontMesh);
                frontMesh = null;
            }
        }

#if UNITY_EDITOR
        // 플레이 중 인스펙터에서 밝기를 바꾸면 미리 계산해 둔 색을 다시 만든다
        private void OnValidate()
        {
            if (false == bInitialized) return;

            CompleteDeferred();
            BuildToneColors();
        }
#endif
    }
}
