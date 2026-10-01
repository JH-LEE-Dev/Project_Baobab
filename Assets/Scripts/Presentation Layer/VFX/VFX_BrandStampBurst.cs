using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 별자리 각인(VFX_BrandStamp)이 찍히는 순간의 폭발 연출. 스프라이트 시트 대신 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다
    /// (VFX_BrandStarWrap, 별자리 레이저와 같은 방식). VFX_BrandStamp 프리팹의 자식으로 붙어서 풀에서 꺼내져 활성화될 때(OnEnable) 시작한다.
    ///
    /// 시간 순서
    /// - 임팩트 프레임: 각인 위치에 큰 십자 섬광이 번쩍 터졌다가 계단식으로 줄어든다(블룸이 가장 크게 터지는 순간).
    /// - 점선 충격파: `.` 점들이 타원 고리로 계단식으로 퍼지고, 바깥 점부터 흩어져 사라진다.
    /// - 별가루 분출: `+`, `x`, `.` 조각이 방사형으로 터져 나가 감속하고, 작아지며 아래로 떨어지다 점멸하며 사라진다.
    /// - 미니 별자리: 큰 별 여섯 개가 흩어져 나가 1px 점선으로 이어져 작은 별자리를 이룬 뒤, 하나씩 작아지며 사라진다. 별은 별가루를 흘린다.
    ///
    /// 색은 흰색/하늘색/파랑/금색 4색만 쓰고 반투명 페이드가 없다. 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로만 낸다.
    /// 정적 버퍼와 고정 크기 구조체 배열만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_BrandStampBurst : MonoBehaviour
    {
        [Header("전체 길이 (루트 ParticleSystem 수명 이하여야 한다)")]
        [SerializeField] private float totalDuration = 0.95f;
        [SerializeField, Tooltip("스탬프 스프라이트보다 이 값만큼 위에 그린다")] private int sortingOrderOffset = 1;
        // 마지막으로 메쉬 렌더러에 적용한 루트 소팅 값. 같으면 매 프레임 세터 호출을 건너뛴다(OnEnable에서 무효화 - 풀 재생 시 VFXComponent가 자식 소팅을 덮어쓰기 때문).
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        [Header("점선 충격파")]
        [SerializeField, Tooltip("고리 최대 반지름(px)")] private float ringMaxRadius = 36.0f;
        [SerializeField, Tooltip("타원 세로 비율(쿼터뷰)")] private float ringSquashY = 0.62f;

        [Header("별가루 분출")]
        [SerializeField] private int firstWaveCount = 26;
        [SerializeField] private int secondWaveCount = 12;
        [SerializeField, Tooltip("분출 초속 최소(px/s)")] private float burstSpeedMin = 70.0f;
        [SerializeField, Tooltip("분출 초속 최대(px/s)")] private float burstSpeedMax = 175.0f;
        [SerializeField, Tooltip("감속 계수(클수록 빨리 멈춘다)")] private float burstDrag = 3.2f;
        [SerializeField, Tooltip("낙하 가속도(px/s^2)")] private float dustGravity = 95.0f;

        [Header("미니 별자리")]
        [SerializeField, Tooltip("별이 퍼지는 반지름 최소(px)")] private float constellationRadiusMin = 24.0f;
        [SerializeField, Tooltip("별이 퍼지는 반지름 최대(px)")] private float constellationRadiusMax = 46.0f;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const int DustCapacity = 96;
        private const int ConstellationStarCount = 6;

        // 임팩트 프레임 단계 경계(초)
        private const float FlashStage0End = 0.025f;
        private const float FlashStage1End = 0.05f;
        private const float FlashEnd = 0.075f;

        // 점선 충격파
        private const float RingStart = 0.02f;
        private const float RingStepTime = 0.04f;
        private const int RingStepCount = 10;
        private const int RingDotCount = 16;

        // 별자리 타임라인
        private const float ConstellationStart = 0.08f;
        private const float ConstellationFlightTime = 0.18f;
        private const float ConstellationExitBase = 0.6f;
        private const float ConstellationExitStagger = 0.04f;
        private const float ConstellationExitTime = 0.2f;
        private const float LineStartBase = 0.2f;
        private const float LineStartStagger = 0.05f;
        private const float LineGrowTime = 0.12f;
        private const float LineDotSpacing = 3.0f;
        private const float StarDustStart = 0.3f;
        private const float StarDustInterval = 0.07f;
        private const float SecondWaveTime = 0.06f;

        // 팔레트 (낙인 각인, 별 감쌈과 같은 계열)
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(170, 220, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(91, 146, 230, 255);
        private static readonly Color32 ColorGold = new Color32(255, 212, 92, 255);

        private struct DustParticle
        {
            public bool bActive;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float drag;
            public float age;
            public float life;
            public int kind;
            public int seed;
            public Color32 color;
        }

        // 메쉬 버퍼는 매 프레임 Clear 후 채우므로 인스턴스끼리 공유해도 안전하다(메인 스레드 단일 실행)
        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(512);
        private static readonly Vector3 MeshBoundsSize = new Vector3(200.0f, 200.0f, 10.0f);

        //내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly DustParticle[] dusts = new DustParticle[DustCapacity];
        private readonly float[] starTargetX = new float[ConstellationStarCount];
        private readonly float[] starTargetY = new float[ConstellationStarCount];
        private readonly float[] starCurrentX = new float[ConstellationStarCount];
        private readonly float[] starCurrentY = new float[ConstellationStarCount];
        private readonly bool[] starAlive = new bool[ConstellationStarCount];
        private readonly int[] starDustTick = new int[ConstellationStarCount];

        //상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bSecondWaveSpawned;
        private bool bInitialized;
        private int dustCursor;
        private uint randomState;
        private int centerX;
        private int centerY;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_BrandStampBurst" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            bSecondWaveSpawned = false;
            dustCursor = 0;

            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            SetupConstellation();
            SpawnBurst(firstWaveCount);
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // ---------- 설정 ----------

        private void SetupConstellation()
        {
            float baseAngle = Rand01() * TwoPi;
            for (int i = 0; i < ConstellationStarCount; i++)
            {
                float angle = baseAngle + i * TwoPi * 0.16f + (Rand01() - 0.5f) * 0.6f;
                float radius = Mathf.Lerp(constellationRadiusMin, constellationRadiusMax, Rand01());
                starTargetX[i] = Mathf.Cos(angle) * radius;
                starTargetY[i] = Mathf.Sin(angle) * radius * 0.75f;
                starCurrentX[i] = 0.0f;
                starCurrentY[i] = 0.0f;
                starAlive[i] = false;
                starDustTick[i] = 0;
            }
        }

        private void SpawnDust(float _x, float _y, float _vx, float _vy, float _drag, float _life, int _kind, Color32 _color)
        {
            DustParticle dust = new DustParticle();
            dust.bActive = true;
            dust.x = _x;
            dust.y = _y;
            dust.vx = _vx;
            dust.vy = _vy;
            dust.drag = _drag;
            dust.age = 0.0f;
            dust.life = _life;
            dust.kind = _kind;
            dust.seed = (int)(Rand01() * 1000.0f);
            dust.color = _color;

            dusts[dustCursor] = dust;
            dustCursor = (dustCursor + 1) % DustCapacity;
        }

        // 각인 위치에서 별가루 조각을 방사형으로 터뜨린다. 각도를 구간별로 나눠서 한쪽으로 몰리지 않게 한다.
        private void SpawnBurst(int _count)
        {
            for (int i = 0; i < _count; i++)
            {
                float angle = TwoPi * (i + Rand01()) / _count;
                float speed = Mathf.Lerp(burstSpeedMin, burstSpeedMax, Mathf.Pow(Rand01(), 0.7f));
                float vx = Mathf.Cos(angle) * speed;
                float vy = Mathf.Sin(angle) * speed * 0.75f;

                float kindRoll = Rand01();
                int kind = kindRoll < 0.35f ? 0 : (kindRoll < 0.65f ? 1 : 2);

                float colorRoll = Rand01();
                Color32 color = colorRoll < 0.4f ? ColorGold : (colorRoll < 0.7f ? ColorWhite : ColorIce);

                SpawnDust(0.0f, 0.0f, vx, vy, burstDrag, Mathf.Lerp(0.45f, 0.85f, Rand01()), kind, color);
            }
        }

        // ---------- 갱신 ----------

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        private void UpdateDust(float _dt)
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                dusts[i].age += _dt;
                if (dusts[i].age >= dusts[i].life)
                {
                    dusts[i].bActive = false;
                    continue;
                }

                float damping = Mathf.Exp(-dusts[i].drag * _dt);
                dusts[i].vx *= damping;
                dusts[i].vy = dusts[i].vy * damping - dustGravity * _dt;
                dusts[i].x += dusts[i].vx * _dt;
                dusts[i].y += dusts[i].vy * _dt;
            }
        }

        private void UpdateConstellation()
        {
            // 별 6개가 같은 비행 진행도를 쓰므로 루프 밖에서 한 번만 계산한다
            float flight = Mathf.Clamp01((elapsed - ConstellationStart) / ConstellationFlightTime);
            float ease = EaseOut(flight);
            for (int i = 0; i < ConstellationStarCount; i++)
            {
                starCurrentX[i] = starTargetX[i] * ease;
                starCurrentY[i] = starTargetY[i] * ease;

                float exitStart = ConstellationExitBase + i * ConstellationExitStagger;
                starAlive[i] = ConstellationStart <= elapsed && elapsed < exitStart + ConstellationExitTime;

                // 별이 남은 동안 별가루를 흘려서 아래로 떨어뜨린다
                if (true == starAlive[i] && StarDustStart <= elapsed)
                {
                    int tick = (int)((elapsed + i * 0.021f) / StarDustInterval);
                    if (tick != starDustTick[i])
                    {
                        starDustTick[i] = tick;
                        float colorRoll = Rand01();
                        Color32 color = colorRoll < 0.5f ? ColorGold : (colorRoll < 0.8f ? ColorWhite : ColorIce);
                        SpawnDust(starCurrentX[i], starCurrentY[i], (Rand01() - 0.5f) * 10.0f, -6.0f, 0.0f, Mathf.Lerp(0.35f, 0.6f, Rand01()), 2, color);
                    }
                }
            }
        }

        // ---------- 그리기 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            quadBuffer.Clear();
            quadBuffer.SetWorldTransform(transform.worldToLocalMatrix, pos.z);
            centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            DrawRing();
            DrawConstellation();
            DrawDust();
            DrawFlash();

            quadBuffer.Upload(mesh, MeshBoundsSize);
            meshRenderer.enabled = true;
        }

        // 임팩트 프레임 - 가장 밝은 순간이라 발광 세기 최대(정점 알파 255)로 그린다
        private void DrawFlash()
        {
            if (FlashEnd <= elapsed) return;

            Color32 white = new Color32(255, 255, 255, 255);
            Color32 gold = new Color32(ColorGold.r, ColorGold.g, ColorGold.b, 230);

            if (elapsed < FlashStage0End)
            {
                AddRect(centerX - 14, centerY - 1, centerX + 15, centerY + 2, white);
                AddRect(centerX - 1, centerY - 14, centerX + 2, centerY + 15, white);
                for (int d = 3; d <= 7; d++)
                {
                    AddRect(centerX + d, centerY + d, centerX + d + 1, centerY + d + 1, gold);
                    AddRect(centerX - d, centerY + d, centerX - d + 1, centerY + d + 1, gold);
                    AddRect(centerX + d, centerY - d, centerX + d + 1, centerY - d + 1, gold);
                    AddRect(centerX - d, centerY - d, centerX - d + 1, centerY - d + 1, gold);
                }
            }
            else if (elapsed < FlashStage1End)
            {
                AddRect(centerX - 9, centerY, centerX + 10, centerY + 1, white);
                AddRect(centerX, centerY - 9, centerX + 1, centerY + 10, white);
                AddRect(centerX - 1, centerY - 1, centerX + 2, centerY + 2, white);
            }
            else
            {
                Color32 dim = new Color32(255, 255, 255, 200);
                AddRect(centerX - 4, centerY, centerX + 5, centerY + 1, dim);
                AddRect(centerX, centerY - 4, centerX + 1, centerY + 5, dim);
            }
        }

        // 점선 충격파 - 계단식으로 커지는 타원 고리, 시간이 지나면 점이 무작위로 빠진다
        private void DrawRing()
        {
            float t = elapsed - RingStart;
            if (t < 0.0f) return;

            int step = (int)(t / RingStepTime);
            if (RingStepCount <= step) return;

            float radius = Mathf.Lerp(6.0f, ringMaxRadius, EaseOut(step / (float)(RingStepCount - 1)));
            float survive = 1.0f - Mathf.Clamp01((elapsed - 0.2f) / 0.22f);

            Color32 baseColor = step < 4 ? ColorWhite : (step < 7 ? ColorIce : ColorBlue);
            byte glowAlpha = (byte)Mathf.RoundToInt((step < 4 ? 0.7f : 0.4f) * 255.0f);
            Color32 color = new Color32(baseColor.r, baseColor.g, baseColor.b, glowAlpha);

            float phase = 0 == (step & 1) ? 0.0f : TwoPi / RingDotCount * 0.5f;
            int size = step < 3 ? 2 : 1;

            for (int i = 0; i < RingDotCount; i++)
            {
                // 점마다 고정된 순서값 - 이 값이 survive보다 크면 그 점은 사라진 것
                float order = ((i * 7) % RingDotCount) / (float)RingDotCount;
                if (survive <= order) continue;

                float angle = phase + TwoPi * i / RingDotCount;
                int px = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int py = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * radius * ringSquashY);
                AddRect(px, py, px + size, py + size, color);
            }
        }

        private void DrawDust()
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                float u = dusts[i].age / dusts[i].life;

                // 후반에는 프레임을 건너뛰며 점멸하다가 사라진다(반투명 없이 픽셀 단위 소멸)
                if (0.8f < u && 0 != ((int)(dusts[i].age * 30.0f) & 1)) continue;

                int px = centerX + Mathf.RoundToInt(dusts[i].x);
                int py = centerY + Mathf.RoundToInt(dusts[i].y);

                // 별마다 위상이 다른 3단계 반짝임(어두움/보통/밝음)
                int twinkle = ((int)(dusts[i].age * 14.0f) + dusts[i].seed) % 3;
                float glow = (0 == twinkle ? 0.45f : (1 == twinkle ? 0.7f : 1.0f)) * (1.0f - u * 0.4f);
                byte glowAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(glow) * 255.0f);

                Color32 baseColor = 0.75f < u ? ColorBlue : dusts[i].color;
                Color32 color = new Color32(baseColor.r, baseColor.g, baseColor.b, glowAlpha);

                // `.`은 계속 점이고, `+`와 `x`는 크기가 계단식으로 줄어들며 서로 번갈아 반짝인다
                int arm = 2 == dusts[i].kind ? 0 : (u < 0.45f ? 2 : (u < 0.75f ? 1 : 0));
                if (0 == arm)
                {
                    int size = (2 == dusts[i].kind && u < 0.2f && true == IsGold(baseColor)) ? 2 : 1;
                    AddRect(px, py, px + size, py + size, color);
                    continue;
                }

                bool bPlus = 0 == (((int)(dusts[i].age * 12.0f) + dusts[i].seed + dusts[i].kind) & 1);
                DrawSparkle(px, py, arm, bPlus, color);
            }
        }

        private static bool IsGold(Color32 _color)
        {
            return _color.r == ColorGold.r && _color.g == ColorGold.g && _color.b == ColorGold.b;
        }

        private void DrawSparkle(int _px, int _py, int _arm, bool _bPlus, Color32 _color)
        {
            if (true == _bPlus)
            {
                AddRect(_px - _arm, _py, _px + _arm + 1, _py + 1, _color);
                AddRect(_px, _py - _arm, _px + 1, _py + _arm + 1, _color);
                return;
            }

            AddRect(_px, _py, _px + 1, _py + 1, _color);
            for (int d = 1; d <= _arm; d++)
            {
                AddRect(_px + d, _py + d, _px + d + 1, _py + d + 1, _color);
                AddRect(_px - d, _py + d, _px - d + 1, _py + d + 1, _color);
                AddRect(_px + d, _py - d, _px + d + 1, _py - d + 1, _color);
                AddRect(_px - d, _py - d, _px - d + 1, _py - d + 1, _color);
            }
        }

        private void DrawConstellation()
        {
            // 1px 점선으로 이어진 선을 먼저 그린다(별이 그 위에 그려진다)
            for (int i = 0; i < ConstellationStarCount - 1; i++)
            {
                float lineStart = LineStartBase + i * LineStartStagger;
                float grow = Mathf.Clamp01((elapsed - lineStart) / LineGrowTime);
                if (0.0f >= grow) continue;

                float exitA = ConstellationExitBase + i * ConstellationExitStagger;
                float exitB = ConstellationExitBase + (i + 1) * ConstellationExitStagger;
                float vanish = Mathf.Clamp01((elapsed - Mathf.Min(exitA, exitB)) / ConstellationExitTime);
                if (1.0f <= vanish) continue;

                float x0 = starCurrentX[i];
                float y0 = starCurrentY[i];
                float dx = starCurrentX[i + 1] - x0;
                float dy = starCurrentY[i + 1] - y0;
                float length = Mathf.Sqrt(dx * dx + dy * dy);
                int dotCount = Mathf.Max(1, Mathf.FloorToInt(length / LineDotSpacing));
                int drawCount = Mathf.CeilToInt(dotCount * grow);

                Color32 lineColor = new Color32(ColorIce.r, ColorIce.g, ColorIce.b, 130);
                for (int k = 1; k <= drawCount; k++)
                {
                    // 사라질 때는 앞쪽(시작 별 쪽) 점부터 빠진다
                    if (k <= dotCount * vanish) continue;

                    float f = k / (float)(dotCount + 1);
                    int px = centerX + Mathf.RoundToInt(x0 + dx * f);
                    int py = centerY + Mathf.RoundToInt(y0 + dy * f);
                    AddRect(px, py, px + 1, py + 1, lineColor);
                }
            }

            float flight = Mathf.Clamp01((elapsed - ConstellationStart) / ConstellationFlightTime);
            for (int i = 0; i < ConstellationStarCount; i++)
            {
                if (false == starAlive[i]) continue;

                float exitStart = ConstellationExitBase + i * ConstellationExitStagger;
                float exitU = Mathf.Clamp01((elapsed - exitStart) / ConstellationExitTime);

                // 등장 초반과 퇴장 후반에 계단식으로 작아진다
                int arm = 3;
                if (flight < 0.3f || 0.75f < exitU) arm = 0;
                else if (flight < 0.6f || 0.5f < exitU) arm = 1;
                else if (0.25f < exitU) arm = 2;

                int px = centerX + Mathf.RoundToInt(starCurrentX[i]);
                int py = centerY + Mathf.RoundToInt(starCurrentY[i]);

                bool bGold = 0 == (i & 1);
                Color32 baseColor = bGold ? ColorGold : ColorWhite;

                // 큰 별은 밝은 순간에 블룸이 크게 터진다
                int twinkle = ((int)(elapsed * 10.0f) + i * 2) % 3;
                float glow = 0 == twinkle ? 0.7f : (1 == twinkle ? 0.85f : 1.0f);
                byte glowAlpha = (byte)Mathf.RoundToInt(glow * 255.0f);
                Color32 color = new Color32(baseColor.r, baseColor.g, baseColor.b, glowAlpha);

                if (0 == arm)
                {
                    AddRect(px, py, px + 1, py + 1, color);
                    continue;
                }

                bool bPlus = 0 == (((int)(elapsed * 9.0f) + i) & 1);
                DrawSparkle(px, py, arm, bPlus, color);
                if (3 == arm)
                {
                    // 큰 별 중심은 흰 점으로 한 번 더 밝힌다
                    AddRect(px, py, px + 1, py + 1, new Color32(255, 255, 255, 255));
                }
            }
        }

        // 월드 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 공용 버퍼에 쿼드로 추가한다.
        private void AddRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            quadBuffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
            Begin();
        }

        private void LateUpdate()
        {
            if (totalDuration <= elapsed)
            {
                meshRenderer.enabled = false;
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0의 임팩트 프레임이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            if (false == bSecondWaveSpawned && SecondWaveTime <= elapsed)
            {
                bSecondWaveSpawned = true;
                SpawnBurst(secondWaveCount);
            }

            UpdateConstellation();
            UpdateDust(dt);

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 스탬프 스프라이트(루트 렌더러) 기준으로 다시 맞춘다
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
    }
}
