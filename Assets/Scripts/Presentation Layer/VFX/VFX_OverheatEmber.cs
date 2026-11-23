using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 4스테이지(MagmaForest) 불씨 본체 연출. 불씨 프리팹(OverheatEmber) Visual 아래 자식으로 붙어 불씨와 함께 움직인다.
    /// 승인 시뮬레이션(A+B 불덩이)을 옮긴 것이다.
    ///
    /// 구성(전부 한 메쉬, 바닥 붉은 빛만 별도 가산 메쉬)
    /// - 불덩이: OverheatEmberOrb. 짙은 빨강 구체 둘레에서 불길이 핥고 오르고, 2.2~3.4초마다 주황·노랑으로 번쩍인다.
    /// - 맴도는 불티 8개: 기울어진 궤도를 돌며 앞쪽은 밝게(주황~흰 노랑), 뒤쪽은 어둡게 칠하고 불덩이에 가려진다. 꼬리 3칸.
    /// - 위로 튀는 불티(0.35~0.7초마다), 번쩍일 때 4개, 1.4~3초마다 앞쪽 불티 하나가 궤도를 벗어나 솟구침.
    /// - 떨어져 나가는 불길 조각: 0.4~0.9초마다 윗면에서 `+` 조각이 떠오르며 `.`로 줄고 식는다.
    /// - 바닥 붉은 빛: 불씨 발밑 13x5 타원을 가산 혼합(Indicators 레이어).
    ///
    /// 불씨 상태는 읽지 않고 움직임으로 판단한다(Application Layer 코드를 건드리지 않기 위해).
    /// - 크기: 불씨 루트(groundAnchor) 스케일. 튀어나올 때 0에서 자라고, 흡입 막바지에 0.35까지 줄어드는 것을 그대로 따른다.
    /// - 이동 중(초당 30px 이상): 지나간 구간을 따라 `x` `+` `.` 조각을 촘촘히 남겨 꼬리를 만들고,
    ///   불티는 각자 다른 지연으로 불덩이를 뒤쫓아 꼬리처럼 늘어진다. 바닥 빛과 대기 중 입자는 멈추고, 정렬은 FlyingItem 레이어로 올린다.
    /// - 드랍 비행(나무에서 튀어나와 착지할 때까지): 루트 스케일 0.05 미만(Launch 직후) 또는 Visual 높이 > 0(포물선)이면 시작, 루트 스케일 1 + Visual 높이 0이면 끝.
    ///   이 동안에는 위 흡입용 조각 꼬리 대신 OverheatEmberTrail(빛 꼬리 + 픽셀 심선)과 떨어지는 불티(Shed)를 그린다. 착지 뒤 꼬리는 0.3초에 걸쳐 사그라든다.
    ///   분출·착지 순간 연출은 없다(시안 2 승인 범위).
    /// - 날아가던 중에 꺼지면(획득) 남은 꼬리는 OverheatEmberResidue로 넘겨 끝까지 그린다. 바닥에서 꺼질 때의 대기 불티는 버린다. 획득·소멸 터짐은 단발 이펙트(VFX_OverheatEmberOneShot)가 맡는다.
    ///
    /// 갱신: 위치·불티·입자·꼬리는 60Hz 고정 간격(불꽃 격자만 30Hz), 그리기는 매 프레임. 화면 밖이면 계산과 그리기를 모두 멈춘다(맵에 수십 개가 깔린다).
    /// 고정 용량 구조체 배열과 자체 xorshift로 재생 중 힙 할당이 없다. 쿼드 버퍼는 인스턴스끼리 공유한다.
    ///
    /// 픽셀 규칙: 모든 도형은 정수 격자 칸이다. 입자는 `.` `+` `x`만 쓴다. 링·충격파 같은 고리 모양은 쓰지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_OverheatEmber : MonoBehaviour, IPixelVfxDeferred
    {
        [Header("연결 (빌더가 채운다)")]
        [SerializeField, Tooltip("불씨 루트. 크기 배율과 바닥 빛 위치의 기준이다.")]
        private Transform groundAnchor;
        [SerializeField, Tooltip("불씨 Visual의 SpriteRenderer. 바닥에 있을 때 이 정렬 순서를 따른다.")]
        private SpriteRenderer sortingSource;
        [SerializeField, Tooltip("바닥 붉은 빛 메쉬 렌더러(자식). 비우면 그리지 않는다.")]
        private MeshRenderer glowRenderer;

        [Header("발광 / 정렬")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("정점 알파(발광 세기) 배율. 블룸이 과하거나 약할 때 조절한다.")]
        private float glowScale = 1.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("바닥 붉은 빛 가산 알파의 최대값. 0이면 그리지 않는다.")]
        private float groundGlowStrength = 0.6f;
        [SerializeField, Tooltip("바닥에 있을 때 Visual 정렬 순서에 더하는 값")]
        private int sortingOrderOffset = 1;
        [SerializeField, Tooltip("날아가는 동안(튀어나옴·흡입) 올려 그릴 정렬 레이어")]
        private string flyingSortingLayerName = "FlyingItem";
        [SerializeField] private string glowSortingLayerName = "Indicators";
        [SerializeField] private int glowSortingOrder = 0;

        [Header("드랍 트레일 (나무에서 튀어나와 날아가는 동안)")]
        [SerializeField, Tooltip("끄면 드랍 비행도 흡입과 같은 조각 꼬리를 쓴다")]
        private bool bDropTrail = true;
        [SerializeField, Tooltip("빛 꼬리 메쉬 렌더러(자식, EmberTrailAdditive). 비우면 빛 꼬리를 그리지 않는다.")]
        private MeshRenderer trailRenderer;
        [SerializeField, Range(0.1f, 0.4f), Tooltip("빛 꼬리·기록 길이(초)")]
        private float trailDuration = 0.3f;
        [SerializeField, Range(0.0f, 2.0f), Tooltip("빛 꼬리 세기 배율")]
        private float trailIntensity = 1.0f;
        [SerializeField, Range(0.5f, 2.0f), Tooltip("빛 꼬리 폭(반지름) 배율")]
        private float trailWidth = 1.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("60Hz 스텝마다 떨어지는 불티가 나올 확률")]
        private float shedChance = 0.45f;

        private const float PixelsPerUnit = 32.0f;
        // 위치·불티·입자·꼬리는 60Hz로 계산한다. 불꽃 격자(OverheatEmberOrb)는 승인된 불길 모양을 지키려고 자체 30Hz를 유지한다.
        private const float StepInterval = 1.0f / 60.0f;
        // 한 프레임에 따라잡는 최대 스텝 수(0.1초)
        private const int MaxStepsPerFrame = 6;
        private const float MaxDeltaTime = 0.05f;

        // 불씨 원점(루트 위치) 기준 불덩이 중심 높이(px)
        private const int OrbCenterY = 5;
        private const int MoteCount = 8;
        // 불티 꼬리 기록 0.33초(60Hz 20스텝). 꼬리는 6, 12, 18스텝 전 위치
        private const int MoteHistory = 20;
        private const int MoteTrailStep = 6;
        // 한 스텝에 0.5px(초당 30px) 이상 움직이면 이동 중으로 본다
        private const float MovingThresholdPixels = 0.5f;
        private const int ParticleCapacity = 160;
        private const int QuadBudget = 1400;
        private const int GlowQuadBudget = 80;
        private const int GlowRadiusX = 6;
        private const int GlowRadiusY = 2;
        private const int ShedCapacity = 48;
        // 루트 스케일이 이보다 작으면 드랍 비행 시작(EmberItem.Launch 직후 같은 프레임. 제자리 배치·흡입은 0.35 미만으로 내려가지 않는다).
        // 첫 관측 프레임에 스케일이 이미 자라 있는 경우는 Visual 높이(DropHeightEpsilon)로 잡는다.
        private const float DropStartScale = 0.05f;
        // Visual(부모) 높이가 이보다 크면 포물선 비행 중(유닛)
        private const float DropHeightEpsilon = 0.0001f;

        // 판단 경계(px). 불덩이 + 궤도 + 위로 튀는 불티
        private const float CullWidthPixels = 48.0f;
        private const float CullHeightPixels = 64.0f;
        private const float CullCenterYPixels = 14.0f;

        private static readonly int[] TrailShapes = { PixelPainter.ShapeCross, PixelPainter.ShapePlus, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapePlus };
        private static readonly byte[] GlowRGB = { 150, 30, 10 };
        // 바닥 빛의 기본 색(색 공간에 맞춘 값). 상수와 색 공간으로만 정해지므로 한 번 구해 둔다(주 스레드에서, EnsureInitialized).
        private static bool bGlowBaseReady;
        private static Color glowBaseColor;
        private static readonly int TrailIntensityID = Shader.PropertyToID("_Intensity");

        private struct Mote
        {
            public float angle;
            public float speed;
            public float rx;
            public float ry;
            public float bobAmp;
            public float bobSpeed;
            public float bobPhase;
            public float fx;
            public float fy;
            public int head;
            public int filled;
        }

        // 쿼드를 모으는 스레드별 작업 버퍼. 픽셀 채우기가 일꾼 스레드에서 돌 수 있어(PixelVfxWorkQueue) 스레드마다 한 벌씩 둔다.
        // 한 번의 계산 묶음(PixelVfxWorkQueue.Serial) 동안 그 스레드가 맡은 인스턴스들의 그림을 구간으로 이어 담고, 묶음이 바뀌면 비운다.
        private sealed class Work
        {
            [System.ThreadStatic] private static Work current;

            public readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);
            public readonly PixelQuadBuffer glowBuffer = new PixelQuadBuffer(GlowQuadBudget);
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
                    work.buffer.Clear();
                    work.glowBuffer.Clear();
                }

                return work;
            }
        }

        // 내부 의존성
        private readonly OverheatEmberOrb orb = new OverheatEmberOrb();
        private readonly OverheatEmberParticles particles = new OverheatEmberParticles(ParticleCapacity);
        private readonly PixelPainter painter = new PixelPainter();
        private readonly PixelPainter glowPainter = new PixelPainter();
        private readonly OverheatEmberTrail trail = new OverheatEmberTrail();
        private readonly OverheatEmberParticles shedParticles = new OverheatEmberParticles(ShedCapacity);
        private readonly Mote[] motes = new Mote[MoteCount];
        private readonly int[] historyX = new int[MoteCount * MoteHistory];
        private readonly int[] historyY = new int[MoteCount * MoteHistory];
        private readonly float[] historyD = new float[MoteCount * MoteHistory];
        private readonly Color32[] tones = new Color32[OverheatEmberOrb.ToneCount];
        private readonly Color32[] darks = new Color32[OverheatEmberOrb.DarkCount];
        private Color32 smokeColor;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh glowMesh;
        private Mesh trailMesh;
        private float trailMaterialIntensity = 1.0f;
        private Vector3 cullSize;
        // transform / sortingSource.transform 프로퍼티(네이티브 호출)를 매 프레임 부르지 않도록 캐싱(둘 다 바뀌지 않는다)
        private Transform cachedTransform;
        private Transform sortingSourceTransform;
        // 바닥 빛·드랍 꼬리 렌더러의 Transform(렌더러가 바뀌지 않는다)
        private Transform glowTransform;
        private Transform trailTransform;
        // 세 렌더러가 모두 꺼져 있고 그 뒤로 켤 수 있는 그리기를 하지 않았으면 true. 화면 밖에 머무는 동안 HideMeshes를 반복하지 않는다.
        private bool bMeshesHidden;
        private int flyingSortingLayerID;
        private int glowSortingLayerID;

        // 상태 변수
        private bool bInitialized;
        private bool bHasPrevious;
        private bool bMoving;
        private bool bDropFlight;
        private int shedTag;
        private float time;
        private float accumulator;
        private float glowAccumulator;
        private float phase;
        private float flare;
        private float flareTimer;
        private float sparkTimer;
        private float chunkTimer;
        private float breakTimer;
        private float orbX;
        private float orbY;
        private float previousOrbX;
        private float previousOrbY;
        private float moveDirX;
        private float moveDirY;
        private float moveSpeedUnits;
        private uint randomState = 1u;
        private ulong meshHash;
        private int meshVertexCount = -1;
        private ulong glowHash;
        private int glowVertexCount = -1;
        private int appliedSortingLayerID = int.MinValue;
        private int appliedSortingOrder = int.MinValue;
        private Vector3 lastDrawPosition;
        private float lastDrawScale = -1.0f;
        // 마지막 계산 스텝이 쓴 불씨 위치. 한 프레임에 스텝이 여러 번 돌 때 이 위치에서 현재 위치까지 나눠 쓴다.
        private Vector3 lastStepPosition;

        // 미룬 계산·그리기(PixelVfxWorkQueue): LateUpdate가 담아 두는 입력과, ComputeDeferred가 남겨 ApplyDeferred가 메쉬에 올리는 결과
        private int deferredSlot = -1;                  // 대기열 자리. -1이면 미뤄 둔 일이 없다
        private int deferredPendingSteps;               // 이번 프레임에 돌릴 계산 스텝 수
        private bool bDeferredExtraStep;                // 스텝이 없고 이전 위치도 없어 한 번 더 돌린다
        private Vector3 deferredStepFrom;               // 스텝마다 나눠 쓸 이동의 시작 위치
        private Vector3 deferredPosition;               // LateUpdate에서 읽은 현재 위치
        private bool bDeferredDraw;                     // 스텝 뒤에 정렬·그리기까지 한다(부모 스케일이 0에 가까우면 스텝만 돌린다)
        private bool bDeferredRedraw;                   // 불덩이·불티 메쉬를 다시 그릴 프레임이다
        private int deferredSortLayer;                  // 바닥에 있을 때 따를 정렬(LateUpdate에서 읽은 값)
        private int deferredSortOrder;
        private Matrix4x4 deferredTrailWorldToLocal;    // 드랍 꼬리 메쉬의 월드 -> 로컬 변환
        private bool bDeferredHasGlow;                  // 바닥 빛 렌더러가 있다
        private bool bDeferredGlowRendererEnabled;      // LateUpdate 시점에 바닥 빛 렌더러가 켜져 있었다
        private bool bDeferredMesh;                     // 이번 프레임에 불덩이·불티 메쉬를 다시 그린다
        private bool bDeferredGlow;                     // 이번 프레임에 바닥 빛 메쉬를 다시 그린다
        private float deferredScale;
        private float deferredMeshZ;
        private Matrix4x4 deferredMeshWorldToLocal;     // LateUpdate에서 읽은 월드 -> 로컬 변환
        private Matrix4x4 deferredGlowWorldToLocal;
        private float deferredGlowZ;
        private int deferredGlowX;                      // 바닥 빛 중심(월드 px)
        private int deferredGlowY;
        private bool bDeferredGlowEllipse;              // 바닥 빛 타원을 그린다(아니면 빈 그림)
        private Color32 deferredGlowInner;
        private Color32 deferredGlowOuter;
        private PixelPainter.Segment meshSegment;
        private PixelPainter.Segment glowSegment;

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            if (false == bGlowBaseReady)
            {
                bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;
                Color baseColor = new Color32(GlowRGB[0], GlowRGB[1], GlowRGB[2], 255);
                if (true == bLinear) baseColor = baseColor.linear;
                glowBaseColor = baseColor;
                bGlowBaseReady = true;
            }

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            cachedTransform = transform;
            if (null != sortingSource) sortingSourceTransform = sortingSource.transform;

            mesh = new Mesh { name = "VFX_OverheatEmber" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 600);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            flyingSortingLayerID = SortingLayer.NameToID(flyingSortingLayerName);
            glowSortingLayerID = SortingLayer.NameToID(glowSortingLayerName);

            if (null != glowRenderer)
            {
                glowMesh = new Mesh { name = "VFX_OverheatEmberGlow" };
                glowMesh.MarkDynamic();
                glowRenderer.GetComponent<MeshFilter>().sharedMesh = glowMesh;
                PixelQuadBuffer.ReserveMesh(glowMesh, GlowQuadBudget);

                glowTransform = glowRenderer.transform;
                glowRenderer.sortingLayerID = glowSortingLayerID;
                glowRenderer.sortingOrder = glowSortingOrder;
                glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glowRenderer.receiveShadows = false;
                glowRenderer.enabled = false;
            }

            if (null != trailRenderer)
            {
                trailTransform = trailRenderer.transform;
                trailMesh = new Mesh { name = "VFX_OverheatEmberTrail" };
                trailMesh.MarkDynamic();
                trailRenderer.GetComponent<MeshFilter>().sharedMesh = trailMesh;
                trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trailRenderer.receiveShadows = false;
                trailRenderer.enabled = false;

                Material trailMaterial = trailRenderer.sharedMaterial;
                if (null != trailMaterial && trailMaterial.HasProperty(TrailIntensityID)) trailMaterialIntensity = trailMaterial.GetFloat(TrailIntensityID);
            }

            cullSize = new Vector3(CullWidthPixels / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits,
                CullHeightPixels / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);

            BuildTones();
        }

        private void BuildTones()
        {
            OverheatEmberOrb.BuildPalette(tones, darks, glowScale);
            trail.BuildPalette();

            Color smoke = new Color32(0x5d, 0x4b, 0x49, 255);
            if (ColorSpace.Linear == QualitySettings.activeColorSpace) smoke = smoke.linear;
            smoke.a = 0.0f;
            smokeColor = smoke;
        }

        // 풀에서 다시 꺼낼 때마다 처음 상태로 되돌린다.
        private void Restart()
        {
            EnsureInitialized();

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            orb.Reset(randomState ^ 0x9E3779B9u);
            particles.Clear();
            shedParticles.Clear();
            trail.Clear();
            bDropFlight = false;
            shedTag = 0;

            time = 0.0f;
            accumulator = 0.0f;
            glowAccumulator = 0.0f;
            phase = Rand01();
            flare = 0.0f;
            flareTimer = 1.0f + Rand01() * 2.0f;
            sparkTimer = Rand01();
            chunkTimer = Rand01() * 0.6f;
            breakTimer = Rand01() * 2.0f;
            bHasPrevious = false;
            bMoving = false;
            moveSpeedUnits = 0.0f;

            for (int i = 0; i < MoteCount; i++)
            {
                motes[i] = new Mote
                {
                    angle = i / (float)MoteCount * Mathf.PI * 2.0f + Rand01() * 0.4f,
                    speed = 1.5f + Rand01() * 0.9f,
                    rx = 4.0f + Rand01() * 2.5f,
                    ry = 1.6f + Rand01() * 0.8f,
                    bobAmp = 1.0f + Rand01() * 2.0f,
                    bobSpeed = 2.0f + Rand01() * 2.0f,
                    bobPhase = Rand01() * Mathf.PI * 2.0f,
                };
            }

            meshVertexCount = -1;
            glowVertexCount = -1;
            bMeshesHidden = false;
            appliedSortingLayerID = int.MinValue;
            appliedSortingOrder = int.MinValue;
        }

        private float Rand01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0xFFFFFF) / 16777216.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        private float AnchorScale()
        {
            float scale = null != groundAnchor ? groundAnchor.lossyScale.x : cachedTransform.lossyScale.x;
            return Mathf.Clamp(scale, 0.0f, 1.0f);
        }

        // ---------- 60Hz 계산 ----------

        // _position: 이 스텝 시점의 불씨 위치. 한 프레임에 스텝이 여러 번 돌면 LateUpdate가 지난 스텝 위치와 현재 위치 사이를 나눠 넘긴다
        // (모두 현재 위치를 읽으면 두 번째 스텝부터 이동량이 0이라 날아가는 중인데도 바닥 상태로 판정된다).
        private void StepOnce(float _scale, Vector3 _position)
        {
            time += StepInterval;

            // 불덩이 중심(월드 px). 위아래로 1px 떠 있다.
            Vector3 pos = _position;
            lastStepPosition = _position;
            float bob = Mathf.Round(Mathf.Sin(time * 2.4f + phase * 6.0f));
            orbX = pos.x * PixelsPerUnit;
            orbY = pos.y * PixelsPerUnit + OrbCenterY + bob;

            float segment = 0.0f;
            if (true == bHasPrevious)
            {
                float dx = orbX - previousOrbX;
                float dy = orbY - previousOrbY - (bob - Mathf.Round(Mathf.Sin((time - StepInterval) * 2.4f + phase * 6.0f)));
                segment = Mathf.Sqrt(dx * dx + dy * dy);
                if (0.0f < segment)
                {
                    moveDirX = dx / segment;
                    moveDirY = dy / segment;
                }
            }

            bMoving = MovingThresholdPixels <= segment;
            moveSpeedUnits = segment / StepInterval / PixelsPerUnit;

            flare = Mathf.Max(0.0f, flare - StepInterval / 0.35f);
            flareTimer -= StepInterval;
            if (0.0f >= flareTimer)
            {
                flareTimer = RandRange(2.2f, 3.4f);
                if (false == bMoving) flare = 1.0f;
            }

            orb.Step(StepInterval, time, phase, 1.0f, flare);
            StepMotes(_scale);

            if (true == bMoving && true == bHasPrevious)
            {
                if (true == bDropFlight) EmitShed(_scale);
                else EmitTrail(segment);
            }
            else if (0.9f < _scale) EmitIdle();

            particles.Step(StepInterval, time);
            shedParticles.Step(StepInterval, time);
            trail.Step(StepInterval, bDropFlight, orbX, orbY, _scale, trailDuration);

            previousOrbX = orbX;
            previousOrbY = orbY;
            bHasPrevious = true;
        }

        // 바닥에서는 궤도를 돌고, 날아가는 중에는 각자 다른 지연으로 불덩이를 뒤쫓는다.
        private void StepMotes(float _scale)
        {
            float k = (1.45f + flare * 0.35f) * Mathf.Max(0.35f, _scale);

            for (int i = 0; i < MoteCount; i++)
            {
                ref Mote m = ref motes[i];
                m.angle += m.speed * StepInterval * (1.0f + flare * 0.8f);

                float sin = Mathf.Sin(m.angle);
                float cos = Mathf.Cos(m.angle);
                // 위아래 흔들림은 바닥에 있을 때만 쓴다(날아가는 중에는 0)
                float lift = true == bMoving ? 0.0f : Mathf.Sin(time * m.bobSpeed + m.bobPhase) * m.bobAmp;

                if (true == bMoving)
                {
                    float follow = Mathf.Min(1.0f, (4.0f + i * 1.6f) * StepInterval * (1.0f + moveSpeedUnits * 0.12f));
                    m.fx += (orbX - m.fx) * follow;
                    m.fy += (orbY - m.fy) * follow;
                }
                else
                {
                    m.fx = orbX;
                    m.fy = orbY;
                }

                float spread = true == bMoving ? 0.5f : 1.0f;
                int x = Mathf.RoundToInt(m.fx + cos * m.rx * k * spread);
                int y = Mathf.RoundToInt(m.fy - sin * m.ry * k * spread + (true == bMoving ? 0.0f : lift));

                m.head = (m.head + 1) % MoteHistory;
                int slot = i * MoteHistory + m.head;
                historyX[slot] = x;
                historyY[slot] = y;
                historyD[slot] = sin;
                if (m.filled < MoteHistory) m.filled++;
            }
        }

        // 흡입·튀어나옴: 이번 스텝에 지나간 구간을 따라 조각을 촘촘히 남긴다.
        private void EmitTrail(float _segment)
        {
            int count = Mathf.Min(14, Mathf.Max(1, Mathf.CeilToInt(_segment / 2.5f)));
            for (int i = 0; i < count; i++)
            {
                float t = (i + Rand01()) / count;
                float x = previousOrbX + (orbX - previousOrbX) * t + RandRange(-1.5f, 1.5f);
                float y = previousOrbY + (orbY - previousOrbY) * t + RandRange(-1.5f, 1.5f);
                int shape = TrailShapes[Mathf.Min(TrailShapes.Length - 1, (int)(Rand01() * TrailShapes.Length))];
                particles.SpawnChunk(x, y, -moveDirX * 6.0f + RandRange(-3.0f, 3.0f), -moveDirY * 6.0f + RandRange(-3.0f, 3.0f),
                    RandRange(0.3f, 0.5f), shape, true, 5.0f, 14.0f, Rand01() * 0.08f, Rand01() * 6.28f);
            }
        }

        // 드랍 비행: 꼬리에서 떨어져 나가 처지며 식는 불티(시안 2 공식). 불씨가 아직 작을 때(팝업 초반)는 내지 않는다.
        private void EmitShed(float _scale)
        {
            if (0.3f >= _scale || Rand01() >= shedChance) return;

            bool bBig = Rand01() < 0.25f;
            int shape = true == bBig ? PixelPainter.ShapePlus : (Rand01() < 0.5f ? PixelPainter.ShapeCross : PixelPainter.ShapeDot);
            float x = orbX + RandRange(-2.0f, 2.0f);
            float y = orbY + RandRange(-2.0f, 2.0f);
            float vx = -moveDirX * RandRange(10.0f, 24.0f) + RandRange(-8.0f, 8.0f);
            float vy = -moveDirY * RandRange(10.0f, 24.0f) + RandRange(-4.0f, 10.0f);
            shedParticles.SpawnShed(x, y, vx, vy, RandRange(0.35f, 0.6f), shape, shedTag);
            shedTag++;
        }

        // 바닥 대기: 위로 튀는 불티, 번쩍임 불티, 떨어져 나가는 조각, 궤도를 벗어나는 불티
        private void EmitIdle()
        {
            float topY = orbY + 7.0f;

            sparkTimer -= StepInterval;
            if (0.0f >= sparkTimer)
            {
                sparkTimer = RandRange(0.35f, 0.7f);
                SpawnRisingSpark(orbX, topY, false);
            }

            if (0.97f < flare)
            {
                for (int i = 0; i < 4; i++) SpawnRisingSpark(orbX, topY - 1.0f, true);
            }

            chunkTimer -= StepInterval;
            if (0.0f >= chunkTimer)
            {
                chunkTimer = RandRange(0.4f, 0.9f);
                particles.SpawnChunk(orbX + Mathf.Round(RandRange(-3.0f, 3.0f)), orbY + 5.0f + Mathf.Round(Rand01() * 2.0f), RandRange(-3.0f, 3.0f),
                    RandRange(10.0f, 17.0f), RandRange(0.5f, 0.75f), PixelPainter.ShapePlus, false, 0.0f, 6.0f, 0.0f, Rand01() * 6.28f);
            }

            breakTimer -= StepInterval;
            if (0.0f >= breakTimer)
            {
                breakTimer = RandRange(1.4f, 3.0f);
                for (int i = 0; i < MoteCount; i++)
                {
                    int slot = i * MoteHistory + motes[i].head;
                    if (0.4f < historyD[slot])
                    {
                        SpawnRisingSpark(historyX[slot], historyY[slot], true);
                        break;
                    }
                }
            }
        }

        private void SpawnRisingSpark(float _x, float _y, bool _bStrong)
        {
            int shape = true == _bStrong || Rand01() < 0.4f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot;
            float vy = true == _bStrong ? RandRange(11.0f, 16.0f) : RandRange(6.0f, 9.0f);
            float life = true == _bStrong ? RandRange(0.7f, 0.9f) : RandRange(0.55f, 0.85f);
            particles.SpawnSpark(_x + Mathf.Round(RandRange(-1.0f, 1.0f)), _y, RandRange(-2.0f, 2.0f), vy, life, shape, false, 0.0f, 0.0f, Rand01() * 6.28f);
        }

        // ---------- 그리기 ----------

        // 불덩이·불티 메쉬를 이번 프레임에 다시 그리도록 걸어 둔다. 픽셀 채우기는 미뤄서 일꾼 스레드에서 돌리고(DrawMeshSegment),
        // 그리기가 읽을 변환은 예전과 같은 시점인 여기서 읽어 둔다.
        private void RebuildMesh(float _scale, float _meshZ)
        {
            bDeferredMesh = true;
            deferredScale = _scale;
            // _meshZ는 이번 LateUpdate에서 읽은 위치의 z(그 뒤로 위치를 바꾸는 코드가 없다)
            deferredMeshZ = _meshZ;
            deferredMeshWorldToLocal = cachedTransform.worldToLocalMatrix;
        }

        // 미뤄 둔 불덩이·불티 그리기: 작업 버퍼의 새 구간에 그린다(일꾼 스레드에서 불릴 수 있다). 예전 RebuildMesh에서 메쉬에 올리는 부분만 뺀 것이다.
        private void DrawMeshSegment(Work _work)
        {
            float scale = deferredScale;
            int cx = Mathf.RoundToInt(orbX);
            int cy = Mathf.RoundToInt(orbY);
            float pulse = OverheatEmberOrb.Pulse(time, phase);

            painter.BeginSegment(_work.buffer, deferredMeshWorldToLocal, deferredMeshZ);

            // 드랍 꼬리 심선과 떨어지는 불티는 불덩이·불티 뒤에 깐다(시안과 같은 순서)
            if (true == trail.bHasPoints) trail.DrawCore(painter, tones);
            shedParticles.Draw(painter, tones, darks, smokeColor);

            if (0.05f < scale)
            {
                DrawMotes(false);
                orb.Draw(painter, cx, cy, time, phase, 1.0f, pulse, flare, scale, tones, darks);
                DrawMotes(true);
            }

            particles.Draw(painter, tones, darks, smokeColor);
            painter.EndSegment(out meshSegment);
        }

        // 뒤쪽(_bFront=false) 불티는 불덩이 전에, 앞쪽 불티는 불덩이 뒤에 그린다. 꼬리는 0.1 / 0.2 / 0.32초 전 위치.
        private void DrawMotes(bool _bFront)
        {
            for (int i = 0; i < MoteCount; i++)
            {
                Mote m = motes[i];
                if (0 == m.filled) continue;

                int head = i * MoteHistory + m.head;
                float d = historyD[head];
                if ((0.0f < d) != _bFront) continue;

                int x = historyX[head];
                int y = historyY[head];

                DrawMoteTrail(i, m, MoteTrailStep, x, y, true == _bFront ? tones[2] : tones[0]);
                if (true == _bFront)
                {
                    DrawMoteTrail(i, m, MoteTrailStep * 2, x, y, tones[1]);
                    DrawMoteTrail(i, m, MoteTrailStep * 3 - 1, x, y, tones[0]);
                }

                Color32 color = true == _bFront
                    ? (0.7f < d && i < 2 ? tones[5] : (0.3f < d ? tones[4] : tones[3]))
                    : (d < -0.6f ? tones[1] : tones[2]);
                painter.Dot(x, y, color);
            }
        }

        private void DrawMoteTrail(int _index, in Mote _m, int _back, int _x, int _y, Color32 _color)
        {
            if (_m.filled <= _back) return;

            int slot = _index * MoteHistory + (_m.head - _back + MoteHistory) % MoteHistory;
            int x = historyX[slot];
            int y = historyY[slot];
            if (x == _x && y == _y) return;

            painter.Dot(x, y, _color);
        }

        // 드랍 빛 꼬리: 기록이 있거나 직전까지 그렸을 때만 다시 만든다. 스텝이 끝난 뒤(반영 때) 부르고, 변환은 LateUpdate에서 읽어 둔 값을 쓴다.
        private void RebuildTrail()
        {
            if (null == trailRenderer || null == trailMesh) return;
            if (false == trail.bHasPoints && false == trailRenderer.enabled) return;

            trail.BuildRibbon(trailMesh, trailRenderer, deferredTrailWorldToLocal, trailDuration, trailIntensity, trailWidth, trailMaterialIntensity);
        }

        // 드랍 비행 판단. 불씨 상태를 읽지 않고 루트 스케일과 Visual(부모) 높이로 정한다.
        private void UpdateDropFlight(float _scale)
        {
            if (false == bDropTrail)
            {
                bDropFlight = false;
                return;
            }

            // 시작: 스케일 0(Launch 직후 같은 프레임) 또는 Visual 높이 > 0(포물선 비행 중. 첫 관측 프레임에 스케일이 이미 자라 있어도 잡힌다).
            // Visual 높이는 드랍 포물선에서만 0보다 크다(착지·흡입·제자리 배치는 0).
            Transform visual = null != sortingSource ? sortingSourceTransform : cachedTransform.parent;
            float height = null != visual ? visual.localPosition.y : 0.0f;
            if (DropStartScale > _scale || DropHeightEpsilon < height)
            {
                bDropFlight = true;
                return;
            }

            // 끝: 스케일 1 + Visual 높이 0(착지)
            if (true == bDropFlight && 0.999f <= _scale) bDropFlight = false;
        }

        // 바닥 붉은 빛: 불씨 발밑 타원. 안쪽이 더 밝고 맥동·번쩍임에 따라 숨 쉰다.
        // 그릴지와 밝기는 스텝 결과(움직임 여부, 시간, 번쩍임)에 달려 있어 스텝 뒤에 정한다(일꾼 스레드에서 불릴 수 있다).
        // 유니티 상태(렌더러 유무·켜짐, 발밑 위치, 변환)는 LateUpdate에서 읽어 둔 값을 쓴다.
        private void PrepareGlow()
        {
            if (false == bDeferredHasGlow) return;
            // 날아가는 중에는 그릴 것이 없다. 직전 그리기도 비어 렌더러가 꺼져 있으면 Commit이 아무것도 바꾸지 않으므로 건너뛴다.
            if (true == bMoving && 0 == glowVertexCount && false == bDeferredGlowRendererEnabled) return;

            float pulse = OverheatEmberOrb.Pulse(time, phase);
            float alpha = (0.3f + 0.3f * pulse + flare * 0.2f) * groundGlowStrength;

            bDeferredGlow = true;
            bDeferredGlowEllipse = false;

            if (false == bMoving && 0.0f < alpha)
            {
                Color baseColor = glowBaseColor;

                Color inner = baseColor;
                inner.a = Mathf.Clamp01(alpha);
                Color outer = baseColor;
                outer.a = Mathf.Clamp01(alpha * 0.6f);
                Color32 innerColor = inner;
                Color32 outerColor = outer;

                bDeferredGlowEllipse = true;
                deferredGlowInner = innerColor;
                deferredGlowOuter = outerColor;
            }
        }

        // 미뤄 둔 바닥 빛 그리기: 작업 버퍼의 새 구간에 타원을 채운다(일꾼 스레드에서 불릴 수 있다). 예전 RebuildGlow의 채우기 부분이다.
        private void DrawGlowSegment(Work _work)
        {
            glowPainter.BeginSegment(_work.glowBuffer, deferredGlowWorldToLocal, deferredGlowZ);

            if (true == bDeferredGlowEllipse)
            {
                int gx = deferredGlowX;
                int gy = deferredGlowY;
                Color32 innerColor = deferredGlowInner;
                Color32 outerColor = deferredGlowOuter;

                for (int dy = -GlowRadiusY; dy <= GlowRadiusY; dy++)
                {
                    for (int dx = -GlowRadiusX; dx <= GlowRadiusX; dx++)
                    {
                        float d = (dx * dx) / (float)(GlowRadiusX * GlowRadiusX) + (dy * dy) / (float)(GlowRadiusY * GlowRadiusY);
                        if (1.0f < d) continue;

                        glowPainter.Dot(gx + dx, gy + dy, d < 0.38f ? innerColor : outerColor);
                    }
                }
            }

            glowPainter.EndSegment(out glowSegment);
        }

        // 미뤄 둔 계산과 그리기(일꾼 스레드에서 불릴 수 있다). 순서는 예전 LateUpdate와 같다: 계산 스텝 -> 바닥 빛 판단 -> 불덩이·불티 -> 바닥 빛.
        void IPixelVfxDeferred.ComputeDeferred()
        {
            // 이번 프레임 이동을 스텝 수만큼 나눠, 스텝마다 그 시점 위치로 계산한다
            int steps = 0;
            while (steps < deferredPendingSteps)
            {
                steps++;
                StepOnce(deferredScale, Vector3.Lerp(deferredStepFrom, deferredPosition, steps / (float)deferredPendingSteps));
            }

            if (true == bDeferredExtraStep) StepOnce(deferredScale, deferredPosition);

            if (false == bDeferredDraw) return;

            if (1.0f / 15.0f <= glowAccumulator || -1 == glowVertexCount || true == bMoving)
            {
                glowAccumulator = 0.0f;
                bMeshesHidden = false;
                PrepareGlow();
            }

            Work work = Work.ForCurrentThread();
            if (true == bDeferredMesh) DrawMeshSegment(work);
            if (true == bDeferredGlow) DrawGlowSegment(work);
        }

        // 미뤄 둔 일의 결과를 반영한다(주 스레드). 예전 LateUpdate와 같은 순서다: 정렬 -> 불덩이·불티 메쉬 -> 드랍 꼬리 -> 바닥 빛.
        void IPixelVfxDeferred.ApplyDeferred()
        {
            deferredSlot = -1;
            if (false == bDeferredDraw) return;

            ApplySorting();

            if (true == bDeferredMesh)
            {
                PixelPainter.CommitSegment(in meshSegment, mesh, meshRenderer, ref meshHash, ref meshVertexCount);
                meshSegment.buffer = null;
            }

            if (true == bDeferredRedraw) RebuildTrail();

            if (true == bDeferredGlow)
            {
                PixelPainter.CommitSegment(in glowSegment, glowMesh, glowRenderer, ref glowHash, ref glowVertexCount);
                glowSegment.buffer = null;
            }

            bDeferredMesh = false;
            bDeferredGlow = false;
        }

        // 미뤄 둔 계산·그리기가 있으면 지금 끝낸다(꺼지거나 색이 바뀌기 전에: 예전에는 LateUpdate에서 이미 끝나 있던 일이다)
        private void CompleteDeferred()
        {
            if (0 <= deferredSlot) PixelVfxWorkQueue.CompleteNow(deferredSlot, this);
        }

        // 바닥에서는 Visual 정렬을 따르고, 날아가는 중에는 FlyingItem 레이어로 올린다.
        private void ApplySorting()
        {
            int layer;
            int order;

            if (true == bMoving)
            {
                layer = flyingSortingLayerID;
                order = 0;
            }
            else
            {
                // 예전에 이 자리에서 읽던 값을 LateUpdate에서 읽어 둔 것(반영이 미뤄져도 예전과 같은 시점의 값)
                layer = deferredSortLayer;
                order = deferredSortOrder;
            }

            if (layer == appliedSortingLayerID && order == appliedSortingOrder) return;

            appliedSortingLayerID = layer;
            appliedSortingOrder = order;
            meshRenderer.sortingLayerID = layer;
            meshRenderer.sortingOrder = order;

            // 빛 꼬리는 불덩이 바로 아래에 깐다
            if (null != trailRenderer)
            {
                trailRenderer.sortingLayerID = layer;
                trailRenderer.sortingOrder = order - 1;
            }
        }

        // 불씨는 루트 스케일(튀어나옴 0 -> 1, 흡입 막바지 0.35)과 Visual 스케일(착지 찌그러짐)을 바꾼다. 픽셀은 월드 격자에 그려야 하므로
        // 이 오브젝트의 월드 스케일을 1로 되돌린다(크기 변화는 AnchorScale로 직접 그린다). 부모 스케일이 0에 가까우면 false.
        private bool CancelParentScale()
        {
            Transform parent = cachedTransform.parent;
            if (null == parent) return true;

            Vector3 parentScale = parent.lossyScale;
            if (0.01f > Mathf.Abs(parentScale.x) || 0.01f > Mathf.Abs(parentScale.y)) return false;

            Vector3 wanted = new Vector3(1.0f / parentScale.x, 1.0f / parentScale.y, 1.0f);
            if (wanted != cachedTransform.localScale) cachedTransform.localScale = wanted;
            return true;
        }

        private void HideMeshes()
        {
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
            if (null != glowRenderer && true == glowRenderer.enabled) glowRenderer.enabled = false;
            if (null != trailRenderer && true == trailRenderer.enabled) trailRenderer.enabled = false;
            meshVertexCount = -1;
            glowVertexCount = -1;
            bMeshesHidden = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bGlowBaseReady = false;
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            // 잔여 드라이버는 여기서 미리 만든다(꺼질 때 만들면 씬 정리 중 오브젝트가 생겨 경고가 난다)
            OverheatEmberResidue.Prepare();
            Restart();
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            float scale = AnchorScale();
            UpdateDropFlight(scale);

            // 화면 밖이면 계산과 그리기를 모두 멈춘다. 다시 들어오면 이전 위치 없이 이어서 시작한다(순간 이동을 이동으로 보지 않게).
            Vector3 pos = cachedTransform.position;
            if (false == PixelVfxCulling.IsVisible(pos + new Vector3(0.0f, CullCenterYPixels / PixelsPerUnit, 0.0f), cullSize))
            {
                // 이미 모두 꺼 둔 상태면 다시 끄지 않는다(그 사이 렌더러를 켤 수 있는 그리기가 없었다).
                if (false == bMeshesHidden) HideMeshes();
                bHasPrevious = false;
                // 다시 들어왔을 때 화면 밖에서 지나간 자리의 드랍 꼬리가 남지 않게 비운다
                trail.Clear();
                return;
            }

            accumulator += dt;
            int pendingSteps = 0;
            float pendingTime = accumulator;
            while (StepInterval <= pendingTime && pendingSteps < MaxStepsPerFrame)
            {
                pendingTime -= StepInterval;
                pendingSteps++;
            }

            // 계산 스텝(불덩이·불티·꼬리 계산)은 유니티 API를 쓰지 않아 미뤄서 일꾼 스레드에서 돌린다(ComputeDeferred).
            // 여기서는 스텝 수와 스텝 뒤의 누적 시간을 예전과 같은 식으로 구하고, 스텝과 그 뒤의 일이 읽을 유니티 상태를 예전과 같은 시점에 읽어 둔다.
            // 이번 프레임 이동을 스텝 수만큼 나눠, 스텝마다 그 시점 위치로 계산한다
            deferredScale = scale;
            deferredPosition = pos;
            deferredStepFrom = true == bHasPrevious ? lastStepPosition : pos;
            deferredPendingSteps = pendingSteps;
            int steps = 0;
            while (steps < pendingSteps)
            {
                accumulator -= StepInterval;
                steps++;
            }

            if (StepInterval <= accumulator) accumulator = 0.0f;
            // 스텝이 한 번이라도 돌면 이전 위치가 생기므로, 이전 위치가 없고 돌릴 스텝도 없을 때만 한 번 돌린다(예전과 같은 조건)
            bDeferredExtraStep = false == bHasPrevious && 0 == pendingSteps;
            if (true == bDeferredExtraStep) steps++;

            bDeferredMesh = false;
            bDeferredGlow = false;
            bDeferredRedraw = false;

            if (false == CancelParentScale())
            {
                HideMeshes();
                // 그리지는 않지만 스텝은 예전처럼 돌린다
                bDeferredDraw = false;
                deferredSlot = PixelVfxWorkQueue.Enqueue(this);
                return;
            }

            bDeferredDraw = true;

            // 정렬(ApplySorting)은 스텝이 정하는 움직임 여부를 읽으므로 스텝 뒤(ApplyDeferred)에 한다. 바닥에 있을 때 따를 값은 여기서 읽어 둔다.
            deferredSortLayer = null != sortingSource ? sortingSource.sortingLayerID : meshRenderer.sortingLayerID;
            deferredSortOrder = (null != sortingSource ? sortingSource.sortingOrder : 0) + sortingOrderOffset;

            // 계산 스텝이 없던 프레임(60Hz보다 빠른 화면)에 제자리·같은 크기면 그림이 같으므로 다시 만들지 않는다.
            // 움직이는 중에는 메쉬가 로컬 좌표라 픽셀 격자에서 어긋나므로 항상 다시 그린다.
            if (0 < steps || -1 == meshVertexCount || pos != lastDrawPosition || scale != lastDrawScale)
            {
                bDeferredRedraw = true;
                bMeshesHidden = false;
                RebuildMesh(scale, pos.z);
                // 드랍 꼬리(RebuildTrail)는 스텝 결과를 읽으므로 스텝 뒤에 만든다. 변환만 여기서 읽어 둔다.
                if (null != trailRenderer && null != trailMesh) deferredTrailWorldToLocal = trailTransform.worldToLocalMatrix;
                lastDrawPosition = pos;
                lastDrawScale = scale;
            }

            // 바닥 빛을 다시 그릴지는 움직임 여부(스텝 결과)에 달려 있어 스텝 뒤에 정한다(PrepareGlow). 그릴 때 쓸 유니티 상태만 여기서 읽어 둔다.
            glowAccumulator += dt;
            bDeferredHasGlow = null != glowRenderer;
            if (true == bDeferredHasGlow)
            {
                bDeferredGlowRendererEnabled = glowRenderer.enabled;
                Vector3 anchor = null != groundAnchor ? groundAnchor.position : cachedTransform.position;
                deferredGlowX = Mathf.RoundToInt(anchor.x * PixelsPerUnit);
                deferredGlowY = Mathf.RoundToInt(anchor.y * PixelsPerUnit) - 1;
                deferredGlowWorldToLocal = glowTransform.worldToLocalMatrix;
                deferredGlowZ = glowTransform.position.z;
            }

            // 모든 LateUpdate가 끝난 직후 스텝이 계산되고, 정렬과 메쉬가 반영된다.
            deferredSlot = PixelVfxWorkQueue.Enqueue(this);
        }

        private void OnDisable()
        {
            // 이번 프레임에 미뤄 둔 그리기가 불티·꼬리를 읽으므로 넘기거나 숨기기 전에 먼저 끝낸다
            CompleteDeferred();

            // 날아가던 중에 꺼지면(획득) 남은 흡입 꼬리는 잔여 드라이버가 끝까지 그린다.
            // 바닥에서 꺼질 때(소멸·회수·씬 정리)의 대기 불티는 버린다. 소멸 연출은 단발 이펙트가 따로 그린다.
            if (true == bInitialized && true == bMoving)
            {
                OverheatEmberResidue.Adopt(particles, meshRenderer.sharedMaterial, meshRenderer.sortingLayerID, meshRenderer.sortingOrder, tones, darks, smokeColor);
            }

            HideMeshes();
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }

            if (null != glowMesh)
            {
                Destroy(glowMesh);
                glowMesh = null;
            }

            if (null != trailMesh)
            {
                Destroy(trailMesh);
                trailMesh = null;
            }
        }

        private void OnValidate()
        {
            glowScale = Mathf.Clamp(glowScale, 0.0f, 2.0f);
            groundGlowStrength = Mathf.Clamp01(groundGlowStrength);

#if UNITY_EDITOR
            if (true == bInitialized)
            {
                CompleteDeferred();
                BuildTones();
            }
#endif
        }
    }
}
