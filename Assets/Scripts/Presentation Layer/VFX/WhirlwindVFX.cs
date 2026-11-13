using System.Collections.Generic;
using PresentationLayer.VFX;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 회전베기 1회성 VFX. 스프라이트 대신 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬로 매 프레임 다시 그린다
/// (VFX_StellaBurst, VFX_LaserHit와 같은 방식, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더).
/// 면적이 큰 칼날만은 픽셀 판정을 WhirlwindBlade 셰이더(GPU)에서 해, 공격 범위가 커져도 CPU 비용이 늘지 않는다.
///
/// 구성(약 0.7초, 나무 불꽃은 마지막 타격 뒤 0.4초까지)
/// 1. 칼날(바닥): 맞은편에 붙은 초승달 칼날 두 개가 반시계로 돌며 휩쓴다. 머리는 하얗게 빛나고 꼬리로 갈수록 시안 -> 파랑으로 식으며
///    끝은 체커 도트로 흩어진다. 머리 끝에는 반짝이는 십자 광채가 붙는다. 돌기가 끝나면 꼬리가 머리를 따라잡으며 가늘어져 사라진다.
///    조금 늦게 조금 안쪽에서 도트 잔상 칼날이 한 겹 더 따라온다.
/// 2. 바람 먼지(바닥): 칼날이 지나간 테두리에서 먼지가 도는 방향으로 휘날린다.
/// 3. 발동 연출(바닥): 중심에서 빛줄기가 사방으로 뻗는다.
/// 4. 마무리(바닥): 한 바퀴를 다 돈 순간 테두리를 따라 별빛이 차례로 반짝인다.
/// 5. 나무 타격(공중): AttackComponent가 칼날이 지나간 나무마다 AddHit을 부르면 그 자리에서 베인 자국과 섬광이 번쩍이고
///    밑동에 충격 고리가 퍼지며 불꽃이 칼날이 도는 방향으로 튀어 떨어진다. 한 번에 많이 벨수록 뒤에 베인 나무의 섬광이 커진다.
///
/// 칼날 각도(GetBladeAngle)와 타격 시각(GetSweepTime)이 같은 표(BladeAngles)를 쓰므로 칼날 머리가 나무를 지나는 순간 나무가 맞는다.
/// 칼날은 나무·캐릭터 아래(ShockWave), 나무 타격은 나무 위(FlyingItem, 드론 레이저 피격과 같은 레이어)에 그린다.
/// 판정 범위·피해와는 무관한 그림이다. 위치는 매 프레임 경과 시간으로 계산하고 재생 중 힙 할당이 없다.
/// </summary>
public class WhirlwindVFX : MonoBehaviour
{
    // //타이밍 (AttackComponent 순차 타격과 공유)

    // 칼날이 도는 빠르기의 기준. 예전 스프라이트(Whirlwind.png, 8프레임 24fps)의 프레임별 칼날 각도를 그대로 이어받았다.
    public const float FrameRate = 24f;

    // 프레임 i(시각 i / FrameRate)의 칼날 머리 각도(등각 보정 - 화면 y×2, 0°=오른쪽, 반시계 +). 칼날 두 개가 180° 맞은편에 있어
    // 첫 각도부터 180°를 돌면 한 바퀴를 다 덮는다. 처음에 빠르게 휘두르고 끝에서 느려진다.
    private static readonly float[] BladeAngles = { -27f, 29f, 55f, 85f, 114f, 129f, 142f, 152f };

    /// <summary>칼날이 한 바퀴를 다 덮기까지 걸리는 시간(초). 이 안에 범위 안 나무가 모두 맞는다.</summary>
    public static float SweepDuration => (BladeAngles.Length - 1) / FrameRate;

    /// <summary>
    /// 회전베기 중심에서 _offset(월드) 방향을 칼날 머리가 지나가는 시각(초, 발동 순간이 0).
    /// </summary>
    public static float GetSweepTime(Vector2 _offset)
    {
        float angle = 0f;
        if (_offset.sqrMagnitude > 0.000001f) angle = Mathf.Atan2(_offset.y * 2f, _offset.x) * Mathf.Rad2Deg;

        // 칼날 두 개가 맞은편에 있으므로 180° 주기로 접어 첫 각도부터의 구간 [a0, a0 + 180)에 넣는다.
        angle = BladeAngles[0] + Mathf.Repeat(angle - BladeAngles[0], 180f);

        float frameSeconds = 1f / FrameRate;
        for (int i = 0; i < BladeAngles.Length - 1; i++)
        {
            if (angle < BladeAngles[i + 1])
            {
                return (i + Mathf.InverseLerp(BladeAngles[i], BladeAngles[i + 1], angle)) * frameSeconds;
            }
        }

        return (BladeAngles.Length - 1) * frameSeconds;
    }

    /// <summary>시각 _t(초)의 칼날 머리 각도(도, 풀지 않은 값). 돌기가 끝난 뒤에는 천천히 밀려 나간다.</summary>
    private static float GetBladeAngle(float _t)
    {
        if (_t <= 0f) return BladeAngles[0];

        float frame = _t * FrameRate;
        int last = BladeAngles.Length - 1;
        if (frame >= last) return BladeAngles[last] + (_t - SweepDuration) * BladeDriftSpeed;

        int i = (int)frame;
        return Mathf.Lerp(BladeAngles[i], BladeAngles[i + 1], frame - i);
    }

    // //연출 상수

    private const float PixelsPerUnit = 32f;

    // 칼날: 바깥 가장자리 반지름(판정 반지름 = 1). 예전 스프라이트처럼 판정 범위보다 조금 크게 그려 가장자리 나무도 칼날 안에 든다.
    private const float BladeOuterRho = 1.08f;
    // 꼬리 쪽 바깥 가장자리가 안으로 말려 드는 양(초승달 모양)
    private const float BladeTailCurl = 0.14f;
    // 칼날 최대 두께(반지름 비율)
    private const float BladeThickness = 0.22f;
    // 꼬리는 이 시간 전의 머리 각도를 따라온다. 빠르게 돌 때 길고 느려지면 짧아진다.
    private const float TrailLag = 0.1f;
    // 막 나왔을 때의 칼날 길이(도)
    private const float TrailMinSpan = 22f;
    // 돌기가 끝난 뒤 머리가 밀려 나가는 빠르기(도/초)와 칼날이 식어 사라지는 시간
    private const float BladeDriftSpeed = 80f;
    private const float BladeFadeSeconds = 0.14f;
    // 등장 팝: 작게 나와 부풀었다가 자리잡는다(그림 반지름 배율)
    private const float PopStartScale = 0.84f;
    private const float PopPeakScale = 1.06f;
    private const float PopRiseSeconds = 0.05f;
    private const float PopSettleSeconds = 0.12f;

    // 잔상 칼날: 늦게 따라오는 시간과 반지름 비율
    private const float EchoLag = 0.04f;
    private const float EchoRadiusRatio = 0.86f;

    // 테두리 별빛의 기준 반지름
    private const float ScarRho = 1.02f;

    // 발동 빛줄기
    private const int BurstRayCount = 12;
    private const float BurstRaySeconds = 0.12f;

    // 마무리 별빛
    private const int FinaleTwinkleCount = 12;
    private const float FinaleTwinkleStep = 0.012f;
    private const float FinaleTwinkleLife = 0.13f;

    // 바람 먼지
    private const int WindMoteCount = 36;
    private const float WindMoteLifeMax = 0.4f;



    // 나무 타격
    private const int HitCapacity = 64;
    private const int SparksPerHit = 8;
    private const int HitRayPx = 7;               // 베는 순간 십자 광선 길이(중심에서)
    private const int HitRayGrowMax = 6;          // 뒤에 베인 나무일수록 광선이 길어지는 최대 px
    private const float HitRingSeconds = 0.1f;    // 밑동 충격 고리
    private const float HitRingMaxPx = 9f;
    private const int SparkCapacity = HitCapacity * SparksPerHit;
    private const float HitRisePx = 10f;          // 나무 밑동(판정점)에서 베인 자국이 그려지는 높이
    private const float HitFlashSeconds = 0.11f;
    private const float HitCutHalfLengthPx = 7f;
    private const float SparkGravity = 260f;      // px/s^2
    private const float SparkDrag = 4f;           // 1/s
    private const float SparkStreakSeconds = 0.03f;
    private const float SparkLifeMin = 0.22f;
    private const float SparkLifeMax = 0.38f;
    private const float HitTailSeconds = SparkLifeMax + 0.02f;

    // 정점 알파 = 발광 세기(0~1 -> GlowMin~GlowMax). 카메라 블룸(임계 1.345, 세기 5, 톤매핑 없음)이 세므로
    // 흰색(칼날 머리·테두리, 불꽃 중심)만 임계를 넘겨 번지게 하고 나머지 색은 임계 아래로 둔다.
    private const float GlowMin = 1f;
    private const float GlowMax = 2.4f;

    // 칼날·바람: 시원한 시안 계열(흰 -> 밝은 시안 -> 시안 -> 파랑)
    private static readonly Color32 SlashWhite = new Color32(255, 255, 255, 255);
    private static readonly Color32 SlashLight = new Color32(186, 244, 255, 40);
    private static readonly Color32 SlashMid = new Color32(84, 204, 242, 0);
    private static readonly Color32 SlashDark = new Color32(38, 118, 204, 0);
    // 나무 타격 불꽃: 따뜻한 계열(흰 -> 밝은 노랑 -> 주황)
    private static readonly Color32 SparkWhite = new Color32(255, 255, 236, 255);
    private static readonly Color32 SparkLight = new Color32(255, 234, 140, 110);
    private static readonly Color32 SparkMid = new Color32(255, 170, 58, 0);

    // 바닥(나무·캐릭터 아래)과 공중(나무 위) 소팅
    private const string GroundSortingLayerName = "ShockWave";
    private const string AirSortingLayerName = "FlyingItem";
    private const int SortingOrder = 0;
    // 바닥층 안에서 베인 자국 < 칼날 < 나머지 순서로 겹친다(칼날 위 연출은 위 SortingOrder 그대로).
    private const int ScarSortingOrder = SortingOrder - 2;
    private const int BladeSortingOrder = SortingOrder - 1;

    private const string ShaderName = "Custom/VFX/BrandWrapGlow";
    private const string BladeShaderName = "Custom/VFX/WhirlwindBlade";
    private static readonly int GlowMinID = Shader.PropertyToID("_GlowMin");
    private static readonly int GlowMaxID = Shader.PropertyToID("_GlowMax");
    private static readonly int ColorWhiteID = Shader.PropertyToID("_ColorWhite");
    private static readonly int ColorLightID = Shader.PropertyToID("_ColorLight");
    private static readonly int ColorMidID = Shader.PropertyToID("_ColorMid");
    private static readonly int ColorDarkID = Shader.PropertyToID("_ColorDark");
    private static readonly int BladeCenterID = Shader.PropertyToID("_BladeCenter");
    private static readonly int BladeBandID = Shader.PropertyToID("_BladeBand");
    private static readonly int BladeMainID = Shader.PropertyToID("_BladeMain");
    private static readonly int BladeEchoID = Shader.PropertyToID("_BladeEcho");
    private static readonly int BladeShapeID = Shader.PropertyToID("_BladeShape");

    // //공유 자원

    // 3타마다 반복 발동되므로 풀링해서 재사용한다.
    // 풀은 static이라 씬 전환 후에도 살아남으므로, 안에 든 오브젝트도 DontDestroyOnLoad로 함께 살려 둔다
    // (CreateInstance 참고). 그렇지 않으면 던전 → 마을 → 던전 재입장 때 파괴된 인스턴스를 꺼내 쓰다 예외가 난다.
    private static readonly Stack<WhirlwindVFX> pool = new Stack<WhirlwindVFX>();

    // 그리기 버퍼는 인스턴스끼리 공유한다(메인 스레드에서 인스턴스마다 비우고 채워 자기 메쉬에 올린다).
    private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(4096);
    private static readonly PixelQuadBuffer airBuffer = new PixelQuadBuffer(1024);
    private static readonly PixelPainter painter = new PixelPainter();

    private static Material sharedMaterial;
    private static Material bladeMaterial;
    private static Mesh unitQuadMesh;
    private static bool bShaderMissingWarned;
    private static int playCounter;

    // //인스턴스 상태

    private struct Hit
    {
        public float x;          // 베인 자국 중심(월드 px)
        public float y;
        public float tangentX;   // 칼날이 도는 방향(화면, 정규화)
        public float tangentY;
        public float time;       // 맞은 시각(재생 경과 시간)
    }

    private struct Spark
    {
        public float x0;
        public float y0;
        public float vx;
        public float vy;
        public float time;
        public float life;
    }

    private Transform scarTransform;
    private Transform groundTransform;
    private Transform airTransform;
    private Transform bladeTransform;
    private Mesh scarMesh;
    private Mesh groundMesh;
    private Mesh airMesh;
    private MeshRenderer scarRenderer;
    private MeshRenderer bladeRenderer;
    private MeshRenderer groundRenderer;
    private MeshRenderer airRenderer;
    private MaterialPropertyBlock bladeBlock;
    private ulong scarHash;
    private int scarVertexCount;
    private ulong groundHash;
    private int groundVertexCount;
    private ulong airHash;
    private int airVertexCount;

    private readonly Hit[] hits = new Hit[HitCapacity];
    private readonly Spark[] sparks = new Spark[SparkCapacity];
    private int hitCount;
    private int sparkCount;
    private float lastHitTime;
    private uint randomState;

    private int centerX;
    private int centerY;
    private float radiusPx;
    private float elapsed;
    private bool bPlaying;

    /// <summary>재생마다 바뀌는 번호. 풀에서 다시 쓰인 인스턴스에 지난 회전베기의 타격이 섞이지 않도록 AddHit이 대조한다.</summary>
    public int PlayId { get; private set; }

    // //퍼블릭 제어 메서드

    /// <summary>
    /// _position을 중심으로 판정 반지름 _attackRadius(월드 유닛, 등각 타원 가로 반지름)의 회전베기 연출을 한 번 재생한다.
    /// 셰이더를 찾지 못하면 아무것도 하지 않고 null을 돌려준다.
    /// </summary>
    public static WhirlwindVFX Spawn(Vector3 _position, float _attackRadius)
    {
        if (false == EnsureMaterial()) return null;

        // 풀 오브젝트는 DontDestroyOnLoad라 보통 살아있지만, 외부에서 파괴됐을 가능성에 대비해
        // 파괴된 참조는 버리고 살아있는 것만 재사용한다.
        WhirlwindVFX instance = null;
        while (pool.Count > 0)
        {
            WhirlwindVFX pooled = pool.Pop();
            if (pooled != null)
            {
                instance = pooled;
                break;
            }
        }
        if (instance == null) instance = CreateInstance();

        instance.gameObject.SetActive(true);
        instance.Play(_position, _attackRadius);
        return instance;
    }

    /// <summary>
    /// 칼날이 나무를 벤 순간 그 자리(나무 판정점, 월드)에 베인 자국과 불꽃을 낸다. _playId가 이번 재생이 아니면 무시한다.
    /// _offsetFromCenter는 회전베기 중심에서 나무까지(월드)로, 불꽃이 튀는 방향(칼날이 도는 방향)을 정한다.
    /// </summary>
    public void AddHit(int _playId, Vector3 _treePoint, Vector2 _offsetFromCenter)
    {
        if (false == bPlaying || _playId != PlayId || hitCount >= HitCapacity) return;

        float angle = Mathf.Atan2(_offsetFromCenter.y * 2f, _offsetFromCenter.x);
        float tangentX = -Mathf.Sin(angle);
        float tangentY = Mathf.Cos(angle) * 0.5f;
        float tangentLength = Mathf.Sqrt(tangentX * tangentX + tangentY * tangentY);
        tangentX /= tangentLength;
        tangentY /= tangentLength;
        float outwardX = Mathf.Cos(angle);
        float outwardY = Mathf.Sin(angle) * 0.5f;

        float x = _treePoint.x * PixelsPerUnit;
        float y = _treePoint.y * PixelsPerUnit + HitRisePx;

        hits[hitCount++] = new Hit { x = x, y = y, tangentX = tangentX, tangentY = tangentY, time = elapsed };
        lastHitTime = elapsed;

        for (int i = 0; i < SparksPerHit && sparkCount < SparkCapacity; i++)
        {
            // 칼날이 도는 방향으로 세게, 바깥과 위로 조금 튄다. 갈래마다 각도를 흩뜨린다.
            float spread = (NextRandom() - 0.5f) * 1.2f;
            float cos = Mathf.Cos(spread);
            float sin = Mathf.Sin(spread);
            float dirX = tangentX * cos - tangentY * sin;
            float dirY = tangentX * sin + tangentY * cos;
            float speed = Mathf.Lerp(90f, 190f, NextRandom());
            float outward = Mathf.Lerp(10f, 50f, NextRandom());
            float up = Mathf.Lerp(30f, 80f, NextRandom());

            sparks[sparkCount++] = new Spark
            {
                x0 = x,
                y0 = y,
                vx = dirX * speed + outwardX * outward,
                vy = dirY * speed + outwardY * outward + up,
                time = elapsed,
                life = Mathf.Lerp(SparkLifeMin, SparkLifeMax, NextRandom()),
            };
        }
    }

    // //초기화

    private static bool EnsureMaterial()
    {
        if (null != sharedMaterial && null != bladeMaterial && null != unitQuadMesh) return true;

        Shader shader = Shader.Find(ShaderName);
        Shader bladeShader = Shader.Find(BladeShaderName);
        if (null == shader || null == bladeShader)
        {
            if (false == bShaderMissingWarned)
            {
                bShaderMissingWarned = true;
                Debug.LogWarning("[WhirlwindVFX] 셰이더 " + (null == shader ? ShaderName : BladeShaderName) + "를 찾지 못해 회전베기 연출을 건너뜁니다.");
            }
            return false;
        }

        if (null == sharedMaterial)
        {
            sharedMaterial = new Material(shader) { name = "M_WhirlwindSlash (Runtime)" };
            sharedMaterial.SetFloat(GlowMinID, GlowMin);
            sharedMaterial.SetFloat(GlowMaxID, GlowMax);
        }

        if (null == bladeMaterial)
        {
            // 색은 정점 색처럼 변환 없이 넘기려고 Vector로 준다(SetColor는 리니어 색 공간에서 변환된다).
            bladeMaterial = new Material(bladeShader) { name = "M_WhirlwindBlade (Runtime)" };
            bladeMaterial.SetFloat(GlowMinID, GlowMin);
            bladeMaterial.SetFloat(GlowMaxID, GlowMax);
            bladeMaterial.SetVector(ColorWhiteID, ToVector(SlashWhite));
            bladeMaterial.SetVector(ColorLightID, ToVector(SlashLight));
            bladeMaterial.SetVector(ColorMidID, ToVector(SlashMid));
            bladeMaterial.SetVector(ColorDarkID, ToVector(SlashDark));
        }

        if (null == unitQuadMesh)
        {
            unitQuadMesh = new Mesh { name = "WhirlwindVFX_BladeQuad" };
            unitQuadMesh.SetVertices(new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f) });
            unitQuadMesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            unitQuadMesh.RecalculateBounds();
        }
        return true;
    }

    private static Vector4 ToVector(Color32 _color)
    {
        return new Vector4(_color.r / 255f, _color.g / 255f, _color.b / 255f, _color.a / 255f);
    }

    private static WhirlwindVFX CreateInstance()
    {
        GameObject go = new GameObject("WhirlwindVFX");
        if (Application.isPlaying) DontDestroyOnLoad(go); // static 풀과 수명을 맞춘다 (pool 필드 주석 참고)
        WhirlwindVFX instance = go.AddComponent<WhirlwindVFX>();
        instance.scarRenderer = CreateLayer(go.transform, "GroundScar", GroundSortingLayerName, ScarSortingOrder, out instance.scarTransform, out instance.scarMesh);
        instance.groundRenderer = CreateLayer(go.transform, "Ground", GroundSortingLayerName, SortingOrder, out instance.groundTransform, out instance.groundMesh);
        instance.airRenderer = CreateLayer(go.transform, "Air", AirSortingLayerName, SortingOrder, out instance.airTransform, out instance.airMesh);
        instance.bladeRenderer = CreateBlade(go.transform, out instance.bladeTransform);
        instance.bladeBlock = new MaterialPropertyBlock();
        return instance;
    }

    private static MeshRenderer CreateBlade(Transform _parent, out Transform _transform)
    {
        GameObject child = new GameObject("GroundBlade");
        _transform = child.transform;
        _transform.SetParent(_parent, false);
        child.AddComponent<MeshFilter>().sharedMesh = unitQuadMesh;

        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = bladeMaterial;
        SetupRenderer(meshRenderer, GroundSortingLayerName, BladeSortingOrder);
        return meshRenderer;
    }

    private static MeshRenderer CreateLayer(Transform _parent, string _name, string _sortingLayer, int _sortingOrder, out Transform _transform, out Mesh _mesh)
    {
        GameObject child = new GameObject(_name);
        _transform = child.transform;
        _transform.SetParent(_parent, false);

        _mesh = new Mesh { name = "WhirlwindVFX_" + _name };
        _mesh.MarkDynamic();
        child.AddComponent<MeshFilter>().sharedMesh = _mesh;

        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedMaterial;
        SetupRenderer(meshRenderer, _sortingLayer, _sortingOrder);
        return meshRenderer;
    }

    private static void SetupRenderer(MeshRenderer _meshRenderer, string _sortingLayer, int _sortingOrder)
    {
        _meshRenderer.sortingLayerName = _sortingLayer;
        _meshRenderer.sortingOrder = _sortingOrder;
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _meshRenderer.enabled = false;
    }

    private void Play(Vector3 _position, float _attackRadius)
    {
        PlayId = ++playCounter;
        randomState = (uint)(PlayId * 747796405) | 1u;

        centerX = Mathf.RoundToInt(_position.x * PixelsPerUnit);
        centerY = Mathf.RoundToInt(_position.y * PixelsPerUnit);
        radiusPx = Mathf.Max(4f, _attackRadius * PixelsPerUnit);

        // 메쉬는 월드 픽셀 좌표로 채우므로 오브젝트는 원점에 둔다(평행이동만 있는 변환).
        transform.position = Vector3.zero;

        elapsed = 0f;
        hitCount = 0;
        sparkCount = 0;
        lastHitTime = -1f;
        scarHash = 0UL;
        scarVertexCount = -1;
        groundHash = 0UL;
        groundVertexCount = -1;
        airHash = 0UL;
        airVertexCount = -1;
        bPlaying = true;

        Draw();
    }

    // DontDestroyOnLoad라 씬이 내려가도 파괴되지 않으므로, 재생 중에 씬이 바뀌면 다음 씬에
    // 이전 씬 좌표로 남아 보이지 않도록 곧바로 풀로 되돌린다. 활성(재생 중)일 때만 구독한다.
    // (프로젝트에 같은 이름의 SceneManager 클래스가 있어 Unity 것은 완전한 이름으로 쓴다.)
    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene _scene)
    {
        ReturnToPool();
    }

    private void Update()
    {
        if (false == bPlaying) return;

        elapsed += Time.deltaTime;

        // 칼날이 식는 시간, 마지막 베인 자국·바람 먼지·별빛이 사라지는 시간 중 가장 늦은 때까지 그린다.
        float tail = Mathf.Max(BladeFadeSeconds, WindMoteLifeMax,
            (FinaleTwinkleCount - 1) * FinaleTwinkleStep + FinaleTwinkleLife);
        float bladeEnd = SweepDuration + tail;
        float hitEnd = lastHitTime >= 0f ? lastHitTime + HitTailSeconds : 0f;
        if (elapsed >= bladeEnd && elapsed >= hitEnd)
        {
            ReturnToPool();
            return;
        }

        Draw();
    }

    private void ReturnToPool()
    {
        if (false == bPlaying && false == gameObject.activeSelf) return;

        bPlaying = false;
        scarRenderer.enabled = false;
        bladeRenderer.enabled = false;
        groundRenderer.enabled = false;
        airRenderer.enabled = false;
        gameObject.SetActive(false);
        pool.Push(this);
    }

    // //그리기

    private void Draw()
    {
        // 바닥은 베인 자국(맨 아래) -> 칼날(셰이더 쿼드) -> 나머지(칼날 위) 순으로 겹친다.
        painter.Begin(groundBuffer, scarTransform.worldToLocalMatrix, 0f);
        painter.Commit(scarMesh, scarRenderer, ref scarHash, ref scarVertexCount);

        UpdateBlades();

        painter.Begin(groundBuffer, groundTransform.worldToLocalMatrix, 0f);
        DrawBladeGlints();
        DrawWindMotes();
        DrawBurstRays();
        DrawFinale();
        painter.Commit(groundMesh, groundRenderer, ref groundHash, ref groundVertexCount);

        painter.Begin(airBuffer, airTransform.worldToLocalMatrix, 0f);
        DrawHits();
        painter.Commit(airMesh, airRenderer, ref airHash, ref airVertexCount);
    }

    // 돌기가 끝나면 1 -> 0으로 식는다.
    private float GetEnergy()
    {
        if (elapsed <= SweepDuration) return 1f;
        return Mathf.Clamp01(1f - (elapsed - SweepDuration) / BladeFadeSeconds);
    }

    private float GetPopScale()
    {
        if (elapsed < PopRiseSeconds)
        {
            float t = elapsed / PopRiseSeconds;
            return Mathf.Lerp(PopStartScale, PopPeakScale, 1f - (1f - t) * (1f - t));
        }

        float settle = Mathf.Clamp01((elapsed - PopRiseSeconds) / PopSettleSeconds);
        return Mathf.Lerp(PopPeakScale, 1f, settle * settle * (3f - 2f * settle));
    }

    /// <summary>
    /// 초승달 칼날 두 개와 잔상 칼날. 칼날을 덮는 사각형 쿼드 하나만 놓고, 픽셀(32 PPU 격자)마다 칼날 안인지와 색은
    /// WhirlwindBlade 셰이더가 GPU에서 정한다. 그래서 공격 범위가 커져도 CPU 비용은 늘지 않는다.
    /// 여기서는 매 프레임 바뀌는 값(각도·길이·두께·밝기·팝 배율)만 넘긴다.
    /// </summary>
    private void UpdateBlades()
    {
        float energy = GetEnergy();
        if (energy <= 0f)
        {
            if (bladeRenderer.enabled) bladeRenderer.enabled = false;
            return;
        }

        float scale = GetPopScale();
        float head = GetBladeAngle(elapsed);
        float tail = Mathf.Min(GetBladeAngle(elapsed - TrailLag), head) - TrailMinSpan;
        float span = Mathf.Clamp(head - tail, 1f, 179f);
        float thickness = BladeThickness * (0.3f + 0.7f * energy);
        float brightness = 0.45f + 0.55f * energy;

        // 잔상 칼날: 조금 늦게, 조금 안쪽에서 같은 모양으로 따라오는 반투명(도트) 칼날. 칼날이 두 겹으로 겹쳐 빠르게 보인다.
        float echoHead = GetBladeAngle(elapsed - EchoLag);
        float echoTail = Mathf.Min(GetBladeAngle(elapsed - EchoLag - TrailLag), echoHead) - TrailMinSpan;
        float echoSpan = Mathf.Clamp(echoHead - echoTail, 1f, 179f) * 0.85f;
        bool bEcho = elapsed >= EchoLag * 0.5f;

        float outer = BladeOuterRho * scale;
        float inner = (BladeOuterRho * EchoRadiusRatio - BladeTailCurl - BladeThickness) * scale;
        float outerSq = outer * outer;
        float innerSq = Mathf.Max(0f, inner) * Mathf.Max(0f, inner);

        int rx = Mathf.CeilToInt(radiusPx * outer) + 1;
        int ry = Mathf.CeilToInt(radiusPx * outer * 0.5f) + 1;
        float invRadius = 1f / radiusPx;
        // 바깥 가장자리 1px 테두리를 반지름 비율로 바꾼 값(가로 기준)
        float rimRho = 1.2f * invRadius;
        float minWidth = 0.5f * invRadius;

        // 중심 픽셀 기준 [-rx, rx] x [-ry, ry] 칸을 덮는다(단위 쿼드를 픽셀 경계에 맞춰 늘린다).
        bladeTransform.position = new Vector3((centerX - rx) / PixelsPerUnit, (centerY - ry) / PixelsPerUnit, 0f);
        bladeTransform.localScale = new Vector3((rx * 2 + 1) / PixelsPerUnit, (ry * 2 + 1) / PixelsPerUnit, 1f);

        bladeBlock.SetVector(BladeCenterID, new Vector4(centerX, centerY, radiusPx, PixelsPerUnit));
        bladeBlock.SetVector(BladeBandID, new Vector4(outerSq, innerSq, rimRho, minWidth));
        bladeBlock.SetVector(BladeMainID, new Vector4(head, span, thickness, brightness));
        bladeBlock.SetVector(BladeEchoID, new Vector4(echoHead, echoSpan, bEcho ? 1f : 0f, energy));
        bladeBlock.SetVector(BladeShapeID, new Vector4(scale, BladeOuterRho, BladeTailCurl, EchoRadiusRatio));
        bladeRenderer.SetPropertyBlock(bladeBlock);
        if (false == bladeRenderer.enabled) bladeRenderer.enabled = true;
    }

    /// <summary>칼날 머리 끝 광채: 돌고 있는 동안 십자가 1/30초마다 길어졌다 짧아진다(칼날 위에 그린다).</summary>
    private void DrawBladeGlints()
    {
        if (elapsed >= SweepDuration) return;

        float scale = GetPopScale();
        float head = GetBladeAngle(elapsed);
        int arm = 0 == ((int)(elapsed * 30f) & 1) ? 3 : 2;
        float headRho = BladeOuterRho * scale - BladeThickness * 0.4f * scale;
        for (int k = 0; k < 2; k++)
        {
            float a = (head + 180f * k) * Mathf.Deg2Rad;
            int gx = centerX + Mathf.RoundToInt(Mathf.Cos(a) * radiusPx * headRho);
            int gy = centerY + Mathf.RoundToInt(Mathf.Sin(a) * radiusPx * headRho * 0.5f);
            painter.HLine(gx - arm, gx + arm, gy, SlashWhite);
            painter.Rect(gx, gy - arm, gx + 1, gy, SlashWhite);
            painter.Rect(gx, gy + 1, gx + 1, gy + arm + 1, SlashWhite);
        }
    }



    /// <summary>발동 순간 중심에서 사방으로 뻗어 나가는 빛줄기.</summary>
    private void DrawBurstRays()
    {
        if (elapsed >= BurstRaySeconds) return;

        float p = elapsed / BurstRaySeconds;
        float eased = 1f - (1f - p) * (1f - p);
        float near = Mathf.Lerp(0.18f, 1.05f, eased);
        float far = near + Mathf.Lerp(0.38f, 0.06f, p);
        Color32 color = p < 0.35f ? SlashWhite : (p < 0.7f ? SlashLight : SlashMid);

        for (int k = 0; k < BurstRayCount; k++)
        {
            // 화면에서 고르게 보이도록 등각 각도로 나눈다(살짝 비틀어 칼날 방향과 겹치지 않게).
            float a = (k * (360f / BurstRayCount) + 11f) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a) * 0.5f;
            int x0 = centerX + Mathf.RoundToInt(cos * radiusPx * near);
            int y0 = centerY + Mathf.RoundToInt(sin * radiusPx * near);
            int x1 = centerX + Mathf.RoundToInt(cos * radiusPx * far);
            int y1 = centerY + Mathf.RoundToInt(sin * radiusPx * far);
            painter.Line(x0, y0, x1, y1, 0, 0, 0, color);
        }
    }

    /// <summary>
    /// 한 바퀴를 다 돈 순간 테두리를 따라 별빛이 차례로 반짝인다.
    /// </summary>
    private void DrawFinale()
    {
        float f = elapsed - SweepDuration;
        if (f < 0f) return;

        for (int k = 0; k < FinaleTwinkleCount; k++)
        {
            float age = f - k * FinaleTwinkleStep;
            if (age < 0f || age >= FinaleTwinkleLife) continue;

            float a = (k * (360f / FinaleTwinkleCount) + 7f * (k % 3)) * Mathf.Deg2Rad;
            float rho = ScarRho + 0.06f + 0.1f * ((k * 7) % 4) / 3f;
            int x = centerX + Mathf.RoundToInt(Mathf.Cos(a) * radiusPx * rho);
            int y = centerY + Mathf.RoundToInt(Mathf.Sin(a) * radiusPx * rho * 0.5f);

            float p = age / FinaleTwinkleLife;
            if (p < 0.35f)
            {
                painter.HLine(x - 2, x + 2, y, SlashWhite);
                painter.Rect(x, y - 2, x + 1, y, SlashWhite);
                painter.Rect(x, y + 1, x + 1, y + 3, SlashWhite);
            }
            else if (p < 0.7f) painter.Shape(PixelPainter.ShapePlus, x, y, SlashLight);
            else painter.Dot(x, y, SlashMid);
        }
    }

    /// <summary>
    /// 칼날 머리가 지나간 테두리에서 바람 먼지가 칼날이 도는 방향으로 휘날려 흩어진다. 위치는 경과 시간만으로 정해진다.
    /// </summary>
    private void DrawWindMotes()
    {
        for (int i = 0; i < WindMoteCount; i++)
        {
            float h0 = Hash01(i, 0);
            float h1 = Hash01(i, 1);
            float h2 = Hash01(i, 2);

            float angleDeg = BladeAngles[0] + (i + h0) * (360f / WindMoteCount);
            float a = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);
            float emit = GetSweepTime(new Vector2(cos, sin * 0.5f));
            float age = elapsed - emit;
            float life = Mathf.Lerp(0.22f, 0.4f, h1);
            if (age < 0f || age >= life) continue;

            float rho = Mathf.Lerp(0.9f, 1.04f, h2);
            float startX = cos * radiusPx * rho;
            float startY = sin * radiusPx * rho * 0.5f;

            float tangentX = -sin;
            float tangentY = cos * 0.5f;
            float tangentLength = Mathf.Sqrt(tangentX * tangentX + tangentY * tangentY);
            float speed = Mathf.Lerp(70f, 140f, h1);
            float vx = tangentX / tangentLength * speed + cos * 30f;
            float vy = tangentY / tangentLength * speed + sin * 15f + 25f;

            float travel = (1f - Mathf.Exp(-3f * age)) / 3f;
            int x = centerX + Mathf.RoundToInt(startX + vx * travel);
            int y = centerY + Mathf.RoundToInt(startY + vy * travel);

            float p = age / life;
            if (p < 0.3f) painter.Shape(PixelPainter.ShapePlus, x, y, SlashLight);
            else if (p < 0.65f) painter.Dot(x, y, SlashLight);
            else if (0 == ((int)(age * 40f) & 1)) painter.Dot(x, y, SlashMid);
        }
    }

    // 먼지마다 고정된 무작위 값(재생마다 달라진다)
    private float Hash01(int _index, int _salt)
    {
        uint h = (uint)(_index * 374761393 + _salt * 668265263) ^ (uint)(PlayId * 2246822519u);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777216f;
    }





    /// <summary>나무마다 베인 자국 번쩍임과 튀는 불꽃.</summary>
    private void DrawHits()
    {
        for (int i = 0; i < hitCount; i++)
        {
            Hit hit = hits[i];
            float age = elapsed - hit.time;
            if (age >= HitRingSeconds && age >= HitFlashSeconds) continue;

            int x = Mathf.RoundToInt(hit.x);
            int y = Mathf.RoundToInt(hit.y);

            // 밑동에서 퍼지는 작은 충격 고리
            if (age < HitRingSeconds)
            {
                float p = age / HitRingSeconds;
                int ringRx = Mathf.RoundToInt(Mathf.Lerp(3f, HitRingMaxPx, 1f - (1f - p) * (1f - p)));
                int ringY = y - Mathf.RoundToInt(HitRisePx);
                painter.RingArc(x, ringY, ringRx, Mathf.Max(1, ringRx / 2), 2, p < 0.5f ? 0 : 2, 0f, p < 0.4f ? SparkLight : SparkMid);
            }

            if (age >= HitFlashSeconds) continue;

            int cutX = Mathf.RoundToInt(hit.tangentX * HitCutHalfLengthPx);
            int cutY = Mathf.RoundToInt(hit.tangentY * HitCutHalfLengthPx);

            if (age < 0.04f)
            {
                // 베는 순간: 베인 자국 + 다이아몬드 섬광 + 십자 광선. 한 번에 많이 벨수록 뒤에 베인 나무의 광선이 길어진다.
                int ray = HitRayPx + Mathf.Min(i, HitRayGrowMax);
                painter.Line(x - cutX, y - cutY, x + cutX, y + cutY, 0, 0, 0, SparkWhite);
                painter.Shape(PixelPainter.ShapeDiamond, x, y, SparkWhite);
                painter.HLine(x - ray, x - 3, y, SparkLight);
                painter.HLine(x + 3, x + ray, y, SparkLight);
                painter.Rect(x, y + 3, x + 1, y + ray + 1, SparkLight);
                painter.Rect(x, y - ray, x + 1, y - 2, SparkLight);

                // 네 번째 그루부터는 대각선 광선까지 더해 별 모양으로 커진다.
                if (i >= 3)
                {
                    int diag = ray / 2;
                    painter.Line(x - diag, y - diag, x - 2, y - 2, 0, 0, 0, SparkMid);
                    painter.Line(x + 2, y + 2, x + diag, y + diag, 0, 0, 0, SparkMid);
                    painter.Line(x - diag, y + diag, x - 2, y + 2, 0, 0, 0, SparkMid);
                    painter.Line(x + 2, y - 2, x + diag, y - diag, 0, 0, 0, SparkMid);
                }
            }
            else if (age < 0.075f)
            {
                painter.Line(x - cutX, y - cutY, x + cutX, y + cutY, 0, 0, 0, SparkLight);
                painter.Shape(PixelPainter.ShapePlus, x, y, SparkWhite);
            }
            else
            {
                painter.Line(x - cutX, y - cutY, x + cutX, y + cutY, 2, 2, 0, SparkMid);
                painter.Dot(x, y, SparkLight);
            }
        }

        for (int i = 0; i < sparkCount; i++)
        {
            Spark spark = sparks[i];
            float age = elapsed - spark.time;
            if (age < 0f || age >= spark.life) continue;

            float lifeRatio = age / spark.life;
            GetSparkPosition(spark, age, out float x, out float y);
            int x0 = Mathf.RoundToInt(x);
            int y0 = Mathf.RoundToInt(y);

            Color32 color = lifeRatio < 0.18f ? SparkWhite : (lifeRatio < 0.5f ? SparkLight : SparkMid);
            if (lifeRatio > 0.8f)
            {
                // 마지막에는 점 하나로 깜빡이다 사라진다.
                if (0 == ((int)(age * 40f) & 1)) painter.Dot(x0, y0, color);
                continue;
            }

            GetSparkPosition(spark, Mathf.Max(0f, age - SparkStreakSeconds), out float tailX, out float tailY);
            painter.Line(Mathf.RoundToInt(tailX), Mathf.RoundToInt(tailY), x0, y0, 0, 0, 0, color);
        }
    }

    private static void GetSparkPosition(in Spark _spark, float _age, out float _x, out float _y)
    {
        float travel = (1f - Mathf.Exp(-SparkDrag * _age)) / SparkDrag;
        _x = _spark.x0 + _spark.vx * travel;
        _y = _spark.y0 + _spark.vy * travel - 0.5f * SparkGravity * _age * _age;
    }

    private float NextRandom()
    {
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        return (randomState & 0xFFFFFF) / 16777216f;
    }
}
