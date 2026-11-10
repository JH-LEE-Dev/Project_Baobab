using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 3스테이지 배리어가 타격을 막았을 때 껍질 위 맞은 자리에서 튀는 별빛 스파크. 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬
    /// (VFX_RepairSpark와 같은 방식, PixelPainter + PixelQuadBuffer)이고 VFXComponent 풀(태그 TreeBarrierSparkEffect)로 재생한다.
    /// 전체 0.5초.
    ///
    /// 구성(전부 한 메쉬)
    /// - 임팩트 섬광(0~0.09초): 하얀 원판 + 바깥 방향으로 긴 광선, 3단계로 줄어든다.
    /// - 긴 스파크 12개: 바깥 방향(SetDirection) 부채꼴로 튀어 급감속하며 떨어진다. 꼬리는 지난 궤적이고 흰색 -> 하늘색 -> 파랑으로 식는다.
    /// - 잔 스파크 14개: 1px 점 + 짧은 꼬리, 0.08~0.18초 안에 팍 퍼지고 사라진다.
    /// - 별 조각 6개: `+` `x` `.`가 맞은 자리 둘레에 맺혔다가 깜빡이며 사라진다.
    ///
    /// 위치는 경과 시간만으로 정하는 닫힌 식이라 프레임 속도와 무관하고 재생 중 난수를 새로 뽑지 않으며 힙 할당이 없다.
    /// 픽셀 규칙: 꼬리는 Bresenham 경로에서 L자 모서리를 제거해 1px을 유지하고(DOUBLE PIXEL), 방사형이라 나란히 달리지 않는다(HUGGING).
    /// 입자 모양은 PixelPainter.Shape의 다섯 가지만 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_BarrierSpark : MonoBehaviour
    {
        [Header("밀도 / 발광")]
        [SerializeField, Range(0.25f, 2.0f), Tooltip("스파크 개수 배율")] private float density = 1.0f;
        [SerializeField, Range(0.0f, 2.0f), Tooltip("정점 알파(발광 세기) 배율")] private float glowScale = 1.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("루트 렌더러 소팅보다 이 값만큼 위에 그린다")] private int sortingOrderOffset = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float TimelineLength = 0.5f;
        private const float TrailStep = 1.0f / 120.0f;

        private const int SparkCapacity = 24;
        private const int DustCapacity = 28;
        private const int GlintCapacity = 12;
        private const int BaseSparkCount = 12;
        private const int BaseDustCount = 14;
        private const int BaseGlintCount = 6;
        private const int SparkTrailMax = 7;
        private const int DustTrail = 3;

        private const int RawCapacity = 160;
        private const int QuadBudget = 600;

        private const float SparkDrag = 4.2f;
        private const float DustDrag = 9.0f;

        private const int ToneCount = 6;

        // 별빛 색(밝음 -> 어두움). 낙인 각인/별 감쌈과 같은 계열이다. 정점 알파 = 발광 세기(BrandWrapGlow).
        private static readonly byte[] ToneR = { 255, 214, 160, 104, 70, 46 };
        private static readonly byte[] ToneG = { 255, 240, 214, 160, 104, 62 };
        private static readonly byte[] ToneB = { 255, 255, 255, 236, 214, 160 };
        private static readonly float[] ToneGlow = { 0.34f, 0.27f, 0.2f, 0.13f, 0.08f, 0.04f };

        private struct Spark
        {
            public float delay;
            public float life;
            public float vx;
            public float vy;
            public float gravity;
        }

        private struct Dust
        {
            public float delay;
            public float life;
            public float vx;
            public float vy;
        }

        private struct Glint
        {
            public float delay;
            public float life;
            public int x;
            public int y;
            public int shape;
        }

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);
        private static readonly int[] rawX = new int[RawCapacity];
        private static readonly int[] rawY = new int[RawCapacity];
        private static readonly int[] pathX = new int[RawCapacity];
        private static readonly int[] pathY = new int[RawCapacity];

        //내부 의존성
        private readonly PixelPainter painter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly Color32[] toneColors = new Color32[ToneCount];
        private readonly Spark[] sparks = new Spark[SparkCapacity];
        private readonly Dust[] dusts = new Dust[DustCapacity];
        private readonly Glint[] glints = new Glint[GlintCapacity];
        private readonly float[] sampleX = new float[SparkTrailMax];
        private readonly float[] sampleY = new float[SparkTrailMax];

        //상태 변수
        private Vector2 direction = Vector2.up;
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private int sparkTotal;
        private int dustTotal;
        private int glintTotal;
        private int originX;
        private int originY;
        private uint randomState;
        private ulong meshHash;
        private int meshVertexCount = -1;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        /// <summary>스파크가 튀는 바깥 방향(화면상). 풀에서 꺼낸 직후, 첫 LateUpdate 전에 부른다.</summary>
        public void SetDirection(Vector2 _direction)
        {
            direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector2.up;
        }

        // ---------- 초기화 ----------

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_BarrierSpark" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 400);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            BuildTones();
        }

        // 선형 색 공간에서는 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.
        private void BuildTones()
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;

            for (int i = 0; i < ToneCount; i++)
            {
                Color color = new Color32(ToneR[i], ToneG[i], ToneB[i], 255);
                if (true == bLinear) color = color.linear;
                color.a = Mathf.Clamp01(ToneGlow[i] * glowScale);
                toneColors[i] = color;
            }
        }

        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            meshVertexCount = -1;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            float baseAngle = Mathf.Atan2(direction.y, direction.x);
            SetupSparks(baseAngle);
            SetupDusts(baseAngle);
            SetupGlints();
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        // 바깥 방향 기준 ±75도 부채꼴. 가운데로 갈수록 빠르다.
        private void SetupSparks(float _baseAngle)
        {
            sparkTotal = Mathf.Min(SparkCapacity, Mathf.RoundToInt(BaseSparkCount * density));

            for (int i = 0; i < sparkTotal; i++)
            {
                float spread = RandRange(-1.0f, 1.0f);
                float angle = _baseAngle + spread * 1.3f;
                float speed = RandRange(150.0f, 300.0f) * (1.0f - 0.35f * Mathf.Abs(spread));

                sparks[i].vx = Mathf.Cos(angle) * speed;
                sparks[i].vy = Mathf.Sin(angle) * speed + RandRange(10.0f, 50.0f);
                sparks[i].life = RandRange(0.2f, 0.4f);
                sparks[i].delay = RandRange(0.0f, 0.02f);
                sparks[i].gravity = RandRange(260.0f, 380.0f);
            }
        }

        private void SetupDusts(float _baseAngle)
        {
            dustTotal = Mathf.Min(DustCapacity, Mathf.RoundToInt(BaseDustCount * density));

            for (int i = 0; i < dustTotal; i++)
            {
                float angle = _baseAngle + RandRange(-1.7f, 1.7f);
                float speed = RandRange(180.0f, 360.0f);

                dusts[i].vx = Mathf.Cos(angle) * speed;
                dusts[i].vy = Mathf.Sin(angle) * speed;
                dusts[i].life = RandRange(0.08f, 0.18f);
                dusts[i].delay = RandRange(0.0f, 0.01f);
            }
        }

        // 맞은 자리 둘레(바깥쪽으로 치우쳐서)에 맺히는 별 조각
        private void SetupGlints()
        {
            glintTotal = Mathf.Min(GlintCapacity, Mathf.RoundToInt(BaseGlintCount * density));

            for (int i = 0; i < glintTotal; i++)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) + RandRange(-1.9f, 1.9f);
                float radius = RandRange(5.0f, 13.0f);

                glints[i].x = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                glints[i].y = Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                glints[i].delay = RandRange(0.04f, 0.16f);
                glints[i].life = RandRange(0.18f, 0.3f);
                float roll = Rand01();
                glints[i].shape = roll < 0.4f ? PixelPainter.ShapePlus : (roll < 0.7f ? PixelPainter.ShapeCross : PixelPainter.ShapeDot);
            }
        }

        // ---------- 경로 ----------

        private static int AppendLine(int _x0, int _y0, int _x1, int _y1, int _count)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = Mathf.Abs(_y1 - _y0);
            int sx = _x0 < _x1 ? 1 : -1;
            int sy = _y0 < _y1 ? 1 : -1;
            int error = dx - dy;
            int x = _x0;
            int y = _y0;

            while (false == (x == _x1 && y == _y1))
            {
                if (RawCapacity <= _count) return _count;

                int doubled = 2 * error;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += sx;
                }

                if (doubled < dx)
                {
                    error += dx;
                    y += sy;
                }

                rawX[_count] = x;
                rawY[_count] = y;
                _count++;
            }

            return _count;
        }

        // sampleX/sampleY의 앞 _sampleCount개(머리 -> 꼬리)를 이은 1px 경로. L자 모서리는 제거한다.
        private int BuildPath(int _sampleCount)
        {
            int raw = 0;

            for (int i = 0; i < _sampleCount; i++)
            {
                int px = Mathf.RoundToInt(sampleX[i]);
                int py = Mathf.RoundToInt(sampleY[i]);

                if (0 == raw)
                {
                    rawX[0] = px;
                    rawY[0] = py;
                    raw = 1;
                    continue;
                }

                if (rawX[raw - 1] == px && rawY[raw - 1] == py) continue;

                raw = AppendLine(rawX[raw - 1], rawY[raw - 1], px, py, raw);
            }

            int written = 0;
            for (int i = 0; i < raw; i++)
            {
                if (0 < i && i < raw - 1 && 0 < written)
                {
                    int prevX = pathX[written - 1];
                    int prevY = pathY[written - 1];
                    if (Mathf.Abs(rawX[i + 1] - prevX) <= 1 && Mathf.Abs(rawY[i + 1] - prevY) <= 1) continue;
                }

                pathX[written] = rawX[i];
                pathY[written] = rawY[i];
                written++;
            }

            return written;
        }

        private void PaintPath(PixelPainter _p, int _length, float _ageFraction)
        {
            for (int i = 0; i < _length; i++)
            {
                float along = i / (float)_length;
                int tone = Mathf.Clamp(Mathf.FloorToInt(_ageFraction * 3.0f + along * 3.0f), 0, ToneCount - 1);
                _p.Dot(originX + pathX[i], originY + pathY[i], toneColors[tone]);
            }
        }

        // ---------- 그리기 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            originX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            originY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            painter.Begin(buffer, transform.worldToLocalMatrix, pos.z);

            DrawDusts(painter);
            DrawSparks(painter);
            DrawGlints(painter);
            DrawFlash(painter);

            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        // 감속(drag) + 중력의 닫힌 식 위치
        private static void SparkPosition(in Spark _s, float _t, out float _x, out float _y)
        {
            float f = (1.0f - Mathf.Exp(-SparkDrag * _t)) / SparkDrag;
            float gravityTerm = _s.gravity / SparkDrag;
            _x = _s.vx * f;
            _y = (_s.vy + gravityTerm) * f - gravityTerm * _t;
        }

        private void DrawSparks(PixelPainter _p)
        {
            for (int i = 0; i < sparkTotal; i++)
            {
                float age = elapsed - sparks[i].delay;
                if (0.0f > age || sparks[i].life <= age) continue;

                float fraction = age / sparks[i].life;
                int trail = Mathf.Max(2, Mathf.RoundToInt(SparkTrailMax * Mathf.Pow(1.0f - fraction, 0.6f)));

                int count = 0;
                for (int j = 0; j < trail; j++)
                {
                    float t = age - j * TrailStep;
                    if (0.0f > t) break;

                    SparkPosition(sparks[i], t, out sampleX[count], out sampleY[count]);
                    count++;
                }

                if (0 == count) continue;

                int length = BuildPath(count);
                PaintPath(_p, length, fraction);
            }
        }

        private void DrawDusts(PixelPainter _p)
        {
            for (int i = 0; i < dustTotal; i++)
            {
                float age = elapsed - dusts[i].delay;
                if (0.0f > age || dusts[i].life <= age) continue;

                int count = 0;
                for (int j = 0; j < DustTrail; j++)
                {
                    float a = age - j * TrailStep;
                    if (0.0f > a) break;

                    float f = (1.0f - Mathf.Exp(-DustDrag * a)) / DustDrag;
                    sampleX[count] = dusts[i].vx * f;
                    sampleY[count] = dusts[i].vy * f;
                    count++;
                }

                if (0 == count) continue;

                int length = BuildPath(count);
                PaintPath(_p, length, age / dusts[i].life + 0.2f);
            }
        }

        // 별 조각: 처음엔 모양 그대로, 후반에는 `.`으로 줄고 30Hz로 깜빡이다 사라진다.
        private void DrawGlints(PixelPainter _p)
        {
            for (int i = 0; i < glintTotal; i++)
            {
                float age = elapsed - glints[i].delay;
                if (0.0f > age || glints[i].life <= age) continue;

                float fraction = age / glints[i].life;
                int x = originX + glints[i].x;
                int y = originY + glints[i].y;

                if (fraction < 0.5f)
                {
                    _p.SpacedShape(glints[i].shape, x, y, toneColors[fraction < 0.2f ? 0 : 1]);
                }
                else if (0 != (Mathf.FloorToInt(age * 30.0f) & 1))
                {
                    _p.SpacedShape(PixelPainter.ShapeDot, x, y, toneColors[2]);
                }
            }
        }

        // 임팩트 섬광(0~0.09초): 원판 + 바깥 방향 긴 광선과 짧은 측면 광선. 3단계로 줄어든다.
        private void DrawFlash(PixelPainter _p)
        {
            if (0.09f <= elapsed) return;

            int stage = elapsed < 0.03f ? 0 : (elapsed < 0.06f ? 1 : 2);
            int outward = 0 == stage ? 9 : (1 == stage ? 6 : 3);
            int side = 0 == stage ? 5 : (1 == stage ? 3 : 2);
            int radius = 0 == stage ? 2 : 1;

            // 바깥 방향을 8방위로 맞춰 광선을 정수 격자 직선으로 낸다
            int dirX = Mathf.RoundToInt(direction.x * 1.4f);
            int dirY = Mathf.RoundToInt(direction.y * 1.4f);
            if (0 == dirX && 0 == dirY) dirY = 1;
            dirX = Mathf.Clamp(dirX, -1, 1);
            dirY = Mathf.Clamp(dirY, -1, 1);

            DrawRay(_p, dirX, dirY, outward);
            DrawRay(_p, -dirY, dirX, side);
            DrawRay(_p, dirY, -dirX, side);

            float rr = radius * radius + radius * 0.5f;
            for (int y = -radius; y <= radius; y++)
            {
                int w = Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0.0f, rr - y * y)));
                _p.HLine(originX - w, originX + w, originY + y, toneColors[0]);
            }
        }

        private void DrawRay(PixelPainter _p, int _dx, int _dy, int _length)
        {
            for (int i = 1; i <= _length; i++)
            {
                Color32 color = i < _length * 0.6f ? toneColors[0] : toneColors[2];
                _p.Dot(originX + _dx * i, originY + _dy * i, color);
            }
        }

        // ---------- 유니티 이벤트 ----------

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
            bBeginPending = true;
            elapsed = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            if (TimelineLength <= elapsed)
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                return;
            }

            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 루트 렌더러 기준으로 다시 맞춘다
            if (null != rootParticleRenderer)
            {
                int rootLayer = rootParticleRenderer.sortingLayerID;
                int rootOrder = rootParticleRenderer.sortingOrder;
                if (rootLayer != appliedRootSortingLayerID || rootOrder != appliedRootSortingOrder)
                {
                    appliedRootSortingLayerID = rootLayer;
                    appliedRootSortingOrder = rootOrder;
                    meshRenderer.sortingLayerID = rootLayer;
                    meshRenderer.sortingOrder = rootOrder + sortingOrderOffset;
                }
            }

            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 size = new Vector3(120.0f / PixelsPerUnit + margin, 120.0f / PixelsPerUnit + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position, size))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                meshVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }

        private void OnValidate()
        {
            density = Mathf.Clamp(density, 0.25f, 2.0f);
            glowScale = Mathf.Clamp(glowScale, 0.0f, 2.0f);

#if UNITY_EDITOR
            if (true == bInitialized) BuildTones();
#endif
        }
    }
}
