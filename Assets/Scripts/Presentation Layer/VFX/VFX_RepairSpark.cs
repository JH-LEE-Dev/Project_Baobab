using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 수리함(모루) 수리 스파크 연출. 수리 상호작용 순간 모루 윗면에서 십자 섬광이 번쩍이고, 먼지 스파크가 팍 터지며,
    /// 긴 스파크가 포물선으로 튀어 모루 윗면에 한 번 튕기고, 불씨가 식으며 흩어지고, 수리가 끝났다는 반짝임이 위로 떠오른다.
    /// 모든 연출은 재생 시작 후 1.2초 안에 끝난다. 링(충격파 고리)은 없다.
    ///
    /// 구성(전부 한 메쉬, 루트 렌더러 소팅 + sortingOrderOffset)
    /// - 별 섬광(0~0.1초): 중심 원판 + 가로/세로 광선 + 대각선 짧은 광선, 3단계로 줄어든다.
    /// - 먼지 스파크 26개: 1px 점 + 3px 꼬리, 0.1~0.26초 안에 급감속하며 사라진다.
    /// - 긴 스파크 16개: 포물선 + 모루 윗면/바닥 튕김. 꼬리는 지난 궤적 그대로이고 흰색 -> 노랑 -> 주황 -> 적갈색으로 식는다.
    /// - 불씨 34개: `.` `+` `x` `●` `◆` 다섯 모양, 색이 식고 후반 30%는 30Hz로 깜빡인다.
    /// - 수리 반짝임 5개: `+`/`◆`가 모루 위에서 떠오르다 `+`/`.`으로 줄어 사라진다.
    ///
    /// 위치 계산: 긴 스파크는 재생 시작 때 120Hz 고정 간격으로 궤적 표를 한 번 만들어 두고(튕김은 닫힌 식이 없다),
    /// 이후에는 경과 시간으로 표를 읽기만 한다. 나머지는 경과 시간만으로 정하는 닫힌 식이다.
    /// 그래서 프레임 속도와 무관하고 재생 중 난수를 새로 뽑지 않으며 힙 할당이 없다.
    ///
    /// 픽셀 규칙
    /// - JAGGIES/DOUBLE PIXEL: 스파크 꼬리는 Bresenham 직선으로 이은 경로에서 L자 모서리 픽셀을 제거해 1px 굵기를 유지한다(PixelPainter.Line과 같은 규칙).
    ///   별 광선과 원판은 정수 격자 사각형이다. 링과 타원은 쓰지 않는다.
    /// - HUGGING: 평행한 선이 없다. 스파크 꼬리는 방사형이라 서로 1~2px로 나란히 달리지 않는다.
    /// - 입자는 `.` `+` `x` `●` `◆` 다섯 가지만 PixelPainter.Shape로 그린다.
    ///
    /// 위치 기준: 이 오브젝트의 월드 위치가 타격 지점(모루 윗면)이다. 풀 인스턴스로 쓰이며 루트 ParticleSystem 수명이 끝나면 풀로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_RepairSpark : MonoBehaviour
    {
        [Header("밀도 / 발광")]
        [SerializeField, Range(0.25f, 2.0f), Tooltip("스파크, 먼지, 불씨 개수에 곱하는 배율. 1이 기본이다.")]
        private float density = 1.0f;
        [SerializeField, Range(0.0f, 2.0f), Tooltip("정점 알파(발광 세기)에 곱하는 배율. 블룸이 과하거나 약할 때 조절한다.")]
        private float glowScale = 1.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다.")]
        private int sortingOrderOffset = 0;

        private const float PixelsPerUnit = 32.0f;

        // 전체 타임라인(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.
        private const float TimelineLength = 1.2f;
        private const float TableStep = 1.0f / 120.0f;
        private const int TableSamples = 96;
        private const int TrailMax = 18;
        private const int DustTrail = 4;

        private const int SparkCapacity = 32;
        private const int DustCapacity = 52;
        private const int EmberCapacity = 68;
        private const int TwinkleCapacity = 10;
        private const int BaseSparkCount = 16;
        private const int BaseDustCount = 26;
        private const int BaseEmberCount = 34;
        private const int BaseTwinkleCount = 5;

        private const int RawCapacity = 320;
        private const int QuadBudget = 2000;

        // 타격 지점(0, 0) 기준 바닥 높이(px). 모루 윗면 안쪽에 떨어지면 윗면에서, 바깥이면 바닥에서 튕긴다.
        private const float TopFloorY = -2.0f;
        private const float GroundFloorY = -19.0f;
        private const float TopFloorMinX = -6.0f;
        private const float TopFloorMaxX = 12.0f;
        private const float BounceRestitution = 0.38f;
        private const float BounceFriction = 0.72f;

        private const float DustDrag = 5.0f;
        private const float EmberDrag = 2.2f;

        private const int ToneCount = 6;

        // 열 단계 색(밝음 -> 어두움)과 발광 세기. 정점 알파 = 발광 세기(BrandWrapGlow 셰이더).
        private static readonly byte[] ToneR = { 255, 255, 255, 255, 217, 122 };
        private static readonly byte[] ToneG = { 253, 233, 184, 138, 80, 42 };
        private static readonly byte[] ToneB = { 240, 138, 58, 30, 15, 16 };
        private static readonly float[] ToneGlow = { 0.30f, 0.25f, 0.20f, 0.14f, 0.08f, 0.04f };

        private struct Spark
        {
            public float delay;
            public float life;
            public float jumpTime;
            public float endTime;
            public int sampleCount;
        }

        private struct Dust
        {
            public float delay;
            public float life;
            public float x0;
            public float y0;
            public float vx;
            public float vy;
        }

        private struct Ember
        {
            public float delay;
            public float life;
            public float x0;
            public float y0;
            public float vx;
            public float vy;
            public float accelY;
            public int shape;
        }

        private struct Twinkle
        {
            public float delay;
            public float life;
            public float x;
            public float y;
            public float rise;
            public int shape;
        }

        // 쿼드 버퍼와 경로 버퍼는 인스턴스끼리 공유한다(메인 스레드 단일 실행, 그리는 동안에만 쓴다).
        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);
        private static readonly int[] rawX = new int[RawCapacity];
        private static readonly int[] rawY = new int[RawCapacity];
        private static readonly int[] pathX = new int[RawCapacity];
        private static readonly int[] pathY = new int[RawCapacity];

        // 내부 의존성
        private readonly PixelPainter painter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly Color32[] toneColors = new Color32[ToneCount];
        private readonly Spark[] sparks = new Spark[SparkCapacity];
        private readonly float[] sparkX = new float[SparkCapacity * TableSamples];
        private readonly float[] sparkY = new float[SparkCapacity * TableSamples];
        private readonly Dust[] dusts = new Dust[DustCapacity];
        private readonly Ember[] embers = new Ember[EmberCapacity];
        private readonly Twinkle[] twinkles = new Twinkle[TwinkleCapacity];
        private readonly float[] sampleX = new float[TrailMax];
        private readonly float[] sampleY = new float[TrailMax];

        // 상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private int sparkTotal;
        private int dustTotal;
        private int emberTotal;
        private int twinkleTotal;
        private int originX;
        private int originY;
        private uint randomState;
        private ulong meshHash;
        private int meshVertexCount = -1;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>전체 재생 시간(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.</summary>
        public static float TotalDuration => TimelineLength;

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_RepairSpark" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 1500);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            BuildTones();
        }

        // 정점 색은 셰이더가 그대로 출력하므로, 선형 색 공간에서는 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.
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

        // 풀에서 꺼내져 활성화되면 위치가 정해질 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            meshVertexCount = -1;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            SetupSparks();
            SetupDusts();
            SetupEmbers();
            SetupTwinkles();
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        // ---------- 준비 ----------

        // 긴 스파크: 120Hz 고정 간격으로 궤적을 한 번 계산해 표에 담는다. 이후에는 표를 읽기만 한다.
        private void SetupSparks()
        {
            sparkTotal = Mathf.Min(SparkCapacity, Mathf.RoundToInt(BaseSparkCount * density));

            for (int i = 0; i < sparkTotal; i++)
            {
                float angle;
                if (Rand01() < 0.72f)
                {
                    angle = -Mathf.PI * RandRange(0.07f, 0.93f);
                }
                else
                {
                    float shallow = RandRange(-0.45f, 0.05f);
                    angle = Rand01() < 0.5f ? shallow : Mathf.PI - shallow;
                }

                float speed = RandRange(95.0f, 250.0f);
                float vx = Mathf.Cos(angle) * speed;
                float vy = -Mathf.Sin(angle) * speed;
                float x = RandRange(-1.5f, 1.5f);
                float y = RandRange(-1.0f, 1.0f);
                float life = RandRange(0.38f, 0.72f);
                float delay = RandRange(0.0f, 0.022f);
                float gravity = RandRange(240.0f, 320.0f);
                float drag = RandRange(1.2f, 1.8f);

                float jumpTime = float.MaxValue;
                float endTime = life;
                int bounces = 0;
                int baseIndex = i * TableSamples;
                int samples = TableSamples;

                for (int k = 0; k < TableSamples; k++)
                {
                    float t = k * TableStep;
                    sparkX[baseIndex + k] = x;
                    sparkY[baseIndex + k] = y;

                    if (endTime <= t)
                    {
                        samples = k + 1;
                        break;
                    }

                    float damping = Mathf.Exp(-drag * TableStep);
                    vx *= damping;
                    vy = vy * damping - gravity * TableStep;
                    x += vx * TableStep;
                    y += vy * TableStep;

                    float floorY = (TopFloorMinX < x && x < TopFloorMaxX) ? TopFloorY : GroundFloorY;
                    if (0.0f > vy && y < floorY)
                    {
                        y = floorY;
                        vy = -vy * BounceRestitution;
                        vx *= BounceFriction;
                        bounces++;

                        if (2 <= bounces && float.MaxValue == jumpTime)
                        {
                            // 두 번째 튕김 뒤에는 남은 수명이 20%로 줄어 빠르게 식으며 사라진다
                            jumpTime = t;
                            endTime = t + Mathf.Max(0.0f, life - Mathf.Max(t, life * 0.8f));
                        }
                    }
                }

                sparks[i].delay = delay;
                sparks[i].life = life;
                sparks[i].jumpTime = jumpTime;
                sparks[i].endTime = endTime;
                sparks[i].sampleCount = samples;
            }
        }

        private void SetupDusts()
        {
            dustTotal = Mathf.Min(DustCapacity, Mathf.RoundToInt(BaseDustCount * density));

            for (int i = 0; i < dustTotal; i++)
            {
                float angle = Rand01() < 0.85f ? -Mathf.PI * RandRange(0.0f, 1.0f) : Mathf.PI * RandRange(0.0f, 0.3f) + (Rand01() < 0.5f ? 0.0f : Mathf.PI * 0.7f);
                float speed = RandRange(150.0f, 330.0f);

                dusts[i].x0 = RandRange(-1.0f, 1.0f);
                dusts[i].y0 = RandRange(-1.0f, 1.0f);
                dusts[i].vx = Mathf.Cos(angle) * speed;
                dusts[i].vy = -Mathf.Sin(angle) * speed;
                dusts[i].life = RandRange(0.1f, 0.26f);
                dusts[i].delay = RandRange(0.0f, 0.012f);
            }
        }

        private void SetupEmbers()
        {
            emberTotal = Mathf.Min(EmberCapacity, Mathf.RoundToInt(BaseEmberCount * density));

            for (int i = 0; i < emberTotal; i++)
            {
                float angle = -Mathf.PI * RandRange(-0.05f, 1.05f);
                float speed = RandRange(30.0f, 140.0f);
                float roll = Rand01();

                embers[i].x0 = RandRange(-2.0f, 2.0f);
                embers[i].y0 = RandRange(-1.0f, 1.5f);
                embers[i].vx = Mathf.Cos(angle) * speed;
                embers[i].vy = -Mathf.Sin(angle) * speed;
                embers[i].life = RandRange(0.5f, 1.1f);
                embers[i].delay = RandRange(0.0f, 0.05f);

                // 35%는 뜨거운 공기를 타고 위로 떠오르고, 나머지는 떨어진다
                embers[i].accelY = Rand01() < 0.35f ? RandRange(20.0f, 50.0f) : -RandRange(60.0f, 110.0f);

                // 큰 모양(●, ◆)은 드물게 쓴다
                embers[i].shape = roll < 0.44f ? PixelPainter.ShapeDot : (roll < 0.64f ? PixelPainter.ShapePlus : (roll < 0.84f ? PixelPainter.ShapeCross : (roll < 0.94f ? PixelPainter.ShapeCircle : PixelPainter.ShapeDiamond)));
            }
        }

        private void SetupTwinkles()
        {
            twinkleTotal = Mathf.Min(TwinkleCapacity, Mathf.RoundToInt(BaseTwinkleCount * density));

            for (int i = 0; i < twinkleTotal; i++)
            {
                twinkles[i].x = RandRange(-4.0f, 13.0f);
                twinkles[i].y = RandRange(-4.0f, 6.0f);
                twinkles[i].delay = RandRange(0.08f, 0.4f);
                twinkles[i].life = RandRange(0.26f, 0.4f);
                twinkles[i].rise = RandRange(6.0f, 10.0f);
                twinkles[i].shape = 0 != (i & 1) ? PixelPainter.ShapeDiamond : PixelPainter.ShapePlus;
            }
        }

        // ---------- 경로 ----------

        // _from에서 _to까지 Bresenham으로 이은 픽셀을 raw 버퍼 _count 뒤에 붙인다(시작 픽셀은 이미 들어 있다고 본다).
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

        // sampleX/sampleY의 앞 _sampleCount개(머리 -> 꼬리 순서)를 이은 1px 경로를 path 버퍼에 만든다. L자 모서리는 제거한다.
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

        // 머리부터 꼬리까지 색이 식도록 칠한다. _ageFraction이 클수록 전체가 어두운 쪽 색에서 시작한다.
        private void PaintPath(PixelPainter _p, int _length, float _ageFraction)
        {
            for (int i = 0; i < _length; i++)
            {
                float along = i / (float)_length;
                int tone = Mathf.Clamp(Mathf.FloorToInt(_ageFraction * 3.0f + along * 3.2f), 0, ToneCount - 1);
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
            DrawEmbers(painter);
            DrawTwinkles(painter);
            DrawStar(painter);

            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        // 먼지 스파크: 1px 점 + 짧은 꼬리. 급감속하며 팍 퍼진다.
        private void DrawDusts(PixelPainter _p)
        {
            for (int i = 0; i < dustTotal; i++)
            {
                float age = elapsed - dusts[i].delay;
                if (0.0f > age || dusts[i].life <= age) continue;

                int count = 0;
                for (int j = 0; j < DustTrail; j++)
                {
                    float a = age - j * TableStep;
                    if (0.0f > a) break;

                    float f = (1.0f - Mathf.Exp(-DustDrag * a)) / DustDrag;
                    sampleX[count] = dusts[i].x0 + dusts[i].vx * f;
                    sampleY[count] = dusts[i].y0 + dusts[i].vy * f;
                    count++;
                }

                if (0 == count) continue;

                int length = BuildPath(count);
                PaintPath(_p, length, age / dusts[i].life);
            }
        }

        // 긴 스파크: 궤적 표를 따라 머리에서 꼬리까지 이어 그린다. 꼬리 길이는 수명이 줄수록 짧아진다.
        private void DrawSparks(PixelPainter _p)
        {
            for (int i = 0; i < sparkTotal; i++)
            {
                float time = elapsed - sparks[i].delay;
                if (0.0f > time || sparks[i].endTime <= time) continue;

                // 두 번째 튕김 뒤에는 나이가 수명의 80%로 건너뛴다(색과 꼬리가 빠르게 식는다)
                float age = time;
                if (sparks[i].jumpTime < time)
                {
                    age = Mathf.Max(sparks[i].jumpTime, sparks[i].life * 0.8f) + (time - sparks[i].jumpTime);
                }

                float fraction = Mathf.Min(1.0f, age / sparks[i].life);
                int head = Mathf.Min(Mathf.FloorToInt(time / TableStep), sparks[i].sampleCount - 1);
                int history = Mathf.Min(head + 1, TrailMax);
                int trail = Mathf.Max(2, Mathf.RoundToInt(history * Mathf.Pow(1.0f - fraction, 0.6f)));

                int baseIndex = i * TableSamples;
                int count = 0;
                for (int j = 0; j < trail; j++)
                {
                    int index = head - j;
                    if (0 > index) break;

                    sampleX[count] = sparkX[baseIndex + index];
                    sampleY[count] = sparkY[baseIndex + index];
                    count++;
                }

                int length = BuildPath(count);
                PaintPath(_p, length, fraction);
            }
        }

        // 불씨: 모양은 다섯 가지뿐. 색이 식고 후반 30%는 30Hz로 깜빡인다.
        private void DrawEmbers(PixelPainter _p)
        {
            for (int i = 0; i < emberTotal; i++)
            {
                float age = elapsed - embers[i].delay;
                if (0.0f > age || embers[i].life <= age) continue;

                float fraction = age / embers[i].life;
                if (0.7f < fraction && 0 == (Mathf.FloorToInt(age * 30.0f) & 1)) continue;

                float f = (1.0f - Mathf.Exp(-EmberDrag * age)) / EmberDrag;
                float buoyTerm = embers[i].accelY / EmberDrag;
                float x = embers[i].x0 + embers[i].vx * f;
                float y = embers[i].y0 + buoyTerm * age + (embers[i].vy - buoyTerm) * f;

                int tone = Mathf.Min(ToneCount - 1, Mathf.FloorToInt(fraction * 5.2f));
                _p.Shape(embers[i].shape, originX + Mathf.RoundToInt(x), originY + Mathf.RoundToInt(y), toneColors[tone]);
            }
        }

        // 수리 반짝임: 처음엔 모양(`+`/`◆`)이 크게, 중반에는 `+`/`.`으로 줄어들고, 끝에는 사라진다.
        private void DrawTwinkles(PixelPainter _p)
        {
            for (int i = 0; i < twinkleTotal; i++)
            {
                float age = elapsed - twinkles[i].delay;
                if (0.0f > age || twinkles[i].life <= age) continue;

                float fraction = age / twinkles[i].life;
                int x = originX + Mathf.RoundToInt(twinkles[i].x);
                int y = originY + Mathf.RoundToInt(twinkles[i].y + twinkles[i].rise * fraction);

                if (fraction < 0.45f)
                {
                    _p.Shape(twinkles[i].shape, x, y, toneColors[0]);
                }
                else if (fraction < 0.85f)
                {
                    _p.Shape(PixelPainter.ShapeDiamond == twinkles[i].shape ? PixelPainter.ShapePlus : PixelPainter.ShapeDot, x, y, toneColors[1]);
                }
            }
        }

        // 타격 별 섬광(0~0.1초). 3단계로 줄어든다. 오른쪽 광선(모루 뿔 쪽)이 왼쪽보다 길다.
        private void DrawStar(PixelPainter _p)
        {
            if (0.1f <= elapsed) return;

            int stage = elapsed < 0.034f ? 0 : (elapsed < 0.067f ? 1 : 2);
            int right = 0 == stage ? 13 : (1 == stage ? 9 : 5);
            int left = Mathf.RoundToInt(right * 0.8f);
            int up = 0 == stage ? 10 : (1 == stage ? 7 : 4);
            int down = 0 == stage ? 4 : (1 == stage ? 3 : 2);
            int diagonal = 0 == stage ? 5 : (1 == stage ? 3 : 0);
            int radius = 0 == stage ? 3 : (1 == stage ? 2 : 1);

            DrawRay(_p, 1, 0, right);
            DrawRay(_p, -1, 0, left);
            DrawRay(_p, 0, 1, up);
            DrawRay(_p, 0, -1, down);

            for (int i = 1; i <= diagonal; i++)
            {
                _p.Dot(originX + i, originY + i, toneColors[1]);
                _p.Dot(originX - i, originY + i, toneColors[1]);
                _p.Dot(originX + i, originY - i, toneColors[1]);
                _p.Dot(originX - i, originY - i, toneColors[1]);
            }

            float rr = radius * radius + radius * 0.5f;
            for (int y = -radius; y <= radius; y++)
            {
                int w = Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0.0f, rr - y * y)));
                _p.HLine(originX - w, originX + w, originY + y, toneColors[0]);
            }
        }

        // 광선 하나: 앞쪽 60%는 흰색, 뒤쪽은 노랑. 뿌리쪽 40%는 3px 굵기로 시작해 가늘어진다.
        private void DrawRay(PixelPainter _p, int _dx, int _dy, int _length)
        {
            for (int i = 1; i <= _length; i++)
            {
                Color32 color = i < _length * 0.6f ? toneColors[0] : toneColors[1];
                int x = originX + _dx * i;
                int y = originY + _dy * i;
                _p.Dot(x, y, color);

                if (i <= _length * 0.4f)
                {
                    if (0 != _dx)
                    {
                        _p.Dot(x, y - 1, color);
                        _p.Dot(x, y + 1, color);
                    }
                    else
                    {
                        _p.Dot(x - 1, y, color);
                        _p.Dot(x + 1, y, color);
                    }
                }
            }
        }

        // 유니티 이벤트 함수

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

            // 첫 프레임은 시간을 더하지 않아서 t=0 장면이 반드시 한 번 그려진다
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

            // 화면 밖이면 메쉬 재구성을 건너뛰고 시간만 흘린다(그림이 경과 시간만으로 정해져 다시 들어와도 같은 그림이다)
            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 size = new Vector3(240.0f / PixelsPerUnit + margin, 180.0f / PixelsPerUnit + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position + new Vector3(0.0f, 12.0f / PixelsPerUnit, 0.0f), size))
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
