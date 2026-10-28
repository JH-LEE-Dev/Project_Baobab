using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 휴식처 모닥불의 불꽃 연출. 열이 위로 퍼지며 식는 1px 격자 열확산 모델로 불꽃 몸통을 만들고,
    /// 장작 위 숯불, 점화 섬광, 날아오르는 불씨, 혀처럼 튀는 불똥, 장작 튀김, 연기, 바닥 불빛을 함께 그린다.
    /// 점화 시퀀스(0.9초)는 SetOpen(true) 뒤 지연 시간이 지나면 시작하고, SetOpen(false)는 즉시 멈춘다(소화 연출 없음).
    ///
    /// 구성
    /// - 불꽃 몸통: 21x36 열 격자를 30Hz 고정 간격으로 갱신한다. 열이 4단계 색(적갈 -> 주황 -> 노랑 -> 밝은 노랑)으로 끊겨 그려진다.
    ///   바닥 4줄은 타원 마스크로 둥글게 깎고, 맨 아랫줄은 어두운 색으로 한 칸 걸러 찍어(디더) 잘린 단면 대신 장작에 스며들게 한다.
    /// - 숯불 18픽셀: 장작 위에서 값 노이즈로 깜빡인다.
    /// - 점화: 별 섬광(0.06~0.22초), 불똥 18개 + 불씨 8개, 불꽃이 EaseOutBack으로 자란다.
    /// - 불씨: 초당 약 8개. `.` `+` `x` `●` `◆` 다섯 모양이고 나이가 들수록 식으며 모양이 `+` -> `.`로 줄고 30Hz로 깜빡인다.
    /// - 불똥(lick): 불꽃 윗부분에서 떨어져 나가는 `+` -> `.`. 장작 튀김(pop): 1.6~4초마다 불똥이 포물선으로 튄다.
    /// - 연기: `.` -> `●`가 바이어 디더로 흐려지며 올라간다.
    /// - 바닥 불빛: 모닥불 주변 타원을 4단계 디더로 가산 혼합한다(Indicators 레이어, 사물 밑).
    ///
    /// 위치 계산: 모든 입자는 구조체 배열에 고정 용량으로 들고, 재생 중 힙 할당이 없다. 난수는 자체 xorshift다.
    ///
    /// 픽셀 규칙
    /// - JAGGIES/DOUBLE PIXEL: 모든 도형은 정수 격자 칸(셀)이다. 곡선 윤곽은 타원 마스크의 칸 판정으로만 만든다.
    /// - HUGGING: 불꽃 세로 가장자리는 열 칸 단위라 1~2px로 나란히 달리는 선이 없다.
    /// - 입자는 `.` `+` `x` `●` `◆` 다섯 가지만 PixelPainter.SpacedShape로 그린다. 입자끼리 겹치거나 맞닿으면 `.`로 줄이거나 생략해서
    ///   `x`와 `+`가 이어 붙은 뚱뚱한 덩어리가 생기지 않게 한다(연기는 `.`와 `●`만, 같은 모양을 디더로 깎아 그린다).
    ///
    /// 위치 기준: 이 오브젝트의 월드 위치가 모닥불(CampFire) 원점이다. 모닥불 그림(32x32, 피벗 중앙)은 원점 위 11px에 놓이므로
    /// 불꽃 밑줄은 원점 위 8px, 중심 열은 원점 열이다.
    /// 정렬: 몸통 메쉬는 Back 조각의 정렬 순서 + sortingOrderOffset(Back 0 / 불꽃 +1 / Front +2). 바닥 불빛은 Indicators 레이어다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_CampfireFlame : MonoBehaviour
    {
        [Header("발광 / 바닥 불빛")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("정점 알파(발광 세기)에 곱하는 배율. 블룸이 과하거나 약할 때 조절한다.")]
        private float glowScale = 1.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("바닥 불빛 가산 알파의 최대값(가장 밝은 단계). 0이면 바닥 불빛이 없다.")]
        private float groundGlowStrength = 0.22f;

        [Header("정렬")]
        [SerializeField] private string sortingLayerName = "Objects";
        [SerializeField, Tooltip("Back 조각 정렬 순서보다 이 값만큼 위에 그린다. Back 0 / 불꽃 +1 / Front +2.")]
        private int sortingOrderOffset = 1;
        [SerializeField, Tooltip("바닥 불빛 메쉬 렌더러(자식). 비우면 바닥 불빛을 그리지 않는다.")]
        private MeshRenderer glowRenderer;
        [SerializeField] private string glowSortingLayerName = "Indicators";
        [SerializeField] private int glowSortingOrder = 0;

        private const float PixelsPerUnit = 32.0f;

        // 원점(CampFire) 기준 불꽃 위치(px)
        private const int FlameBaseY = 8;
        private const int FlashCenterY = 13;
        private const int PopY = 10;
        private const int IgniteBurstY = 12;
        private const int EmberMinY = 11;
        private const int SmokeMinY = 32;
        private const int GlowCenterY = 5;

        // 열 격자
        private const int GridWidth = 21;
        private const int GridHeight = 36;
        private const int GridCenter = 10;
        private const float FireStepInterval = 1.0f / 30.0f;
        private const float MaxDeltaTime = 0.05f;

        // 입자 / 숯불 / 바닥 불빛
        private const int ParticleCapacity = 112;
        private const int CoalCount = 18;
        private const int GlowRadiusX = 46;
        private const int GlowRadiusY = 23;
        private const float GlowInterval = 1.0f / 15.0f;

        private const int FlameQuadBudget = 1500;
        private const int GlowQuadBudget = 3600;

        // 판단 경계(px). 불씨와 연기가 올라가는 범위까지 덮는다.
        private const float CullWidthPixels = 200.0f;
        private const float CullHeightPixels = 220.0f;
        private const float CullCenterYPixels = 60.0f;

        private const int StateOff = 0;
        private const int StateWaiting = 1;
        private const int StateIgniting = 2;
        private const int StateLit = 3;

        private const int KindEmber = 0;
        private const int KindLick = 1;
        private const int KindPop = 2;
        private const int KindSmoke = 3;

        // 기본 색 번호
        private const int ColorWhite = 0;
        private const int ColorCore = 1;
        private const int ColorYellow = 2;
        private const int ColorOrange = 3;
        private const int ColorRed = 4;
        private const int ColorDark = 5;
        private const int ColorCount = 6;

        // 발광 등급(정점 알파 = 발광 세기). 시뮬레이션 값 0.3/0.35/0.45/0.55/0.6/0.8/0.85/0.9/1을 5단계로 묶었다.
        private const int GlowDim = 0;
        private const int GlowSoft = 1;
        private const int GlowMid = 2;
        private const int GlowHigh = 3;
        private const int GlowMax = 4;
        private const int GlowClassCount = 5;

        private const int SmokeToneCount = 3;
        private const int SmokeWarmSteps = 4;

        private static readonly byte[] BaseR = { 255, 255, 252, 238, 200, 140 };
        private static readonly byte[] BaseG = { 248, 236, 184, 112, 52, 34 };
        private static readonly byte[] BaseB = { 214, 168, 52, 18, 14, 10 };
        private static readonly float[] GlowClassValue = { 0.30f, 0.45f, 0.60f, 0.85f, 1.0f };

        // 불꽃 4단계(어두움 -> 밝음)
        private static readonly int[] FireColor = { ColorRed, ColorOrange, ColorYellow, ColorCore };
        private static readonly int[] FireGlow = { GlowDim, GlowMid, GlowHigh, GlowMax };

        // 연기 기본색(낮 기준)과 따뜻한 빛 번짐
        private static readonly float[] SmokeToneR = { 0.78f, 0.90f, 1.00f };
        private static readonly float[] SmokeToneG = { 0.82f, 0.92f, 1.00f };
        private static readonly float[] SmokeToneB = { 0.90f, 0.96f, 1.00f };

        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        private struct Particle
        {
            public bool bAlive;
            public int kind;
            public int shape;
            public int tone;
            public int id;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
        }

        // 쿼드 버퍼는 인스턴스끼리 공유한다(메인 스레드 단일 실행, 그리는 동안에만 쓴다).
        private static readonly PixelQuadBuffer flameBuffer = new PixelQuadBuffer(FlameQuadBudget);
        private static readonly PixelQuadBuffer glowBuffer = new PixelQuadBuffer(GlowQuadBudget);

        // 외부 의존성
        private CustomSortable sortable;

        // 내부 의존성
        private readonly PixelPainter flamePainter = new PixelPainter();
        private readonly PixelPainter glowPainter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshFilter glowFilter;
        private Mesh flameMesh;
        private Mesh glowMesh;

        private readonly float[] heat = new float[GridWidth * GridHeight];
        private readonly float[] edgeCool = new float[GridWidth];
        private readonly float[] sourceMask = new float[GridWidth];
        private readonly Particle[] particles = new Particle[ParticleCapacity];
        private readonly int[] coalX = new int[CoalCount];
        private readonly int[] coalY = new int[CoalCount];
        private readonly float[] glowFalloff = new float[(2 * GlowRadiusX + 1) * (2 * GlowRadiusY + 1)];
        private readonly Color32[] tones = new Color32[ColorCount * GlowClassCount];
        private readonly Color32[] smokeTones = new Color32[SmokeToneCount * SmokeWarmSteps];
        private readonly Color32[] glowTones = new Color32[5];
        private Vector3 cullSize;

        // 상태 변수
        private bool bInitialized;
        private bool bOpen;
        private bool bBurstDone;
        private int state;
        private float pendingDelay;
        private float delayLeft;
        private float stateTime;
        private float time;
        private float wind;
        private float flare;
        private float ign;
        private float coalLevel;
        private float groundLight;
        private float fireAccumulator;
        private float emberAccumulator;
        private float smokeAccumulator;
        private float glowAccumulator;
        private float nextPop;
        private int particleId;
        private uint randomState = 1u;
        private int originX;
        private int originY;
        private ulong flameHash;
        private int flameVertexCount = -1;
        private ulong glowHash;
        private int glowVertexCount = -1;
        private int appliedSortingOrder = int.MinValue;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>정렬 기준이 되는 모닥불 Back 조각을 넘긴다(offset 0인 조각).</summary>
        public void Initialize(CustomSortable _sortable)
        {
            sortable = _sortable;
        }

        /// <summary>
        /// 열리면 _igniteDelay초 뒤 점화 시퀀스를 시작하고, 닫히면 즉시 멈춘다. 모닥불 그림이 튀어나오는 시간에 맞추려고 지연을 받는다.
        /// </summary>
        public void SetOpen(bool _bOpen, float _igniteDelay)
        {
            bOpen = _bOpen;
            pendingDelay = Mathf.Max(0.0f, _igniteDelay);

            if (false == bOpen)
            {
                Stop();
                return;
            }

            if (true == isActiveAndEnabled) BeginWaiting();
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            flameMesh = new Mesh { name = "VFX_CampfireFlame" };
            flameMesh.MarkDynamic();
            meshFilter.sharedMesh = flameMesh;
            PixelQuadBuffer.ReserveMesh(flameMesh, FlameQuadBudget);

            meshRenderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            if (null != glowRenderer)
            {
                glowFilter = glowRenderer.GetComponent<MeshFilter>();
                glowMesh = new Mesh { name = "VFX_CampfireGlow" };
                glowMesh.MarkDynamic();
                glowFilter.sharedMesh = glowMesh;
                PixelQuadBuffer.ReserveMesh(glowMesh, GlowQuadBudget);

                glowRenderer.sortingLayerID = SortingLayer.NameToID(glowSortingLayerName);
                glowRenderer.sortingOrder = glowSortingOrder;
                glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glowRenderer.receiveShadows = false;
                glowRenderer.enabled = false;
            }

            cullSize = new Vector3(CullWidthPixels / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits,
                CullHeightPixels / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);

            BuildTables();
            BuildTones();
        }

        // 격자 가장자리 냉각, 불씨 원천 마스크, 숯불 위치, 바닥 불빛 감쇠를 한 번만 계산한다.
        private void BuildTables()
        {
            for (int x = 0; x < GridWidth; x++)
            {
                float dx = Mathf.Abs(x - GridCenter);
                edgeCool[x] = Mathf.Pow(dx / 10.5f, 2.2f) * 0.1f;
                sourceMask[x] = Mathf.Max(0.0f, 1.0f - Mathf.Pow(dx / 7.6f, 2.4f));
            }

            for (int i = 0; i < CoalCount; i++)
            {
                float angle = Hash(i * 2 + 11) * 6.283f;
                float radius = Mathf.Sqrt(Hash(i * 5 + 101));
                coalX[i] = Mathf.RoundToInt(Mathf.Cos(angle) * radius * 7.0f);
                coalY[i] = Mathf.RoundToInt(8.0f - Mathf.Sin(angle) * radius * 3.2f);
            }

            int width = 2 * GlowRadiusX + 1;
            for (int y = -GlowRadiusY; y <= GlowRadiusY; y++)
            {
                for (int x = -GlowRadiusX; x <= GlowRadiusX; x++)
                {
                    float nx = x / (float)GlowRadiusX;
                    float ny = y / (float)GlowRadiusY;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    glowFalloff[(y + GlowRadiusY) * width + (x + GlowRadiusX)] = 1.0f <= d ? 0.0f : Mathf.Pow(1.0f - d, 1.3f) * 1.3f;
                }
            }
        }

        // 정점 색은 셰이더가 그대로 출력하므로, 선형 색 공간에서는 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.
        private void BuildTones()
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;

            for (int c = 0; c < ColorCount; c++)
            {
                for (int g = 0; g < GlowClassCount; g++)
                {
                    Color color = new Color32(BaseR[c], BaseG[c], BaseB[c], 255);
                    if (true == bLinear) color = color.linear;
                    color.a = Mathf.Clamp01(GlowClassValue[g] * glowScale);
                    tones[c * GlowClassCount + g] = color;
                }
            }

            for (int t = 0; t < SmokeToneCount; t++)
            {
                for (int w = 0; w < SmokeWarmSteps; w++)
                {
                    float warm = (w + 0.5f) / SmokeWarmSteps * 0.165f;
                    float r = Mathf.Min(255.0f, 205.0f * SmokeToneR[t] + 200.0f * warm);
                    float g = Mathf.Min(255.0f, 208.0f * SmokeToneG[t] + 110.0f * warm);
                    float b = Mathf.Min(255.0f, 208.0f * SmokeToneB[t] + 50.0f * warm);

                    Color color = new Color(r / 255.0f, g / 255.0f, b / 255.0f, 0.0f);
                    if (true == bLinear) color = color.linear;
                    color.a = 0.0f;
                    smokeTones[t * SmokeWarmSteps + w] = color;
                }
            }

            // 바닥 불빛: 따뜻한 주황. 단계 1~4가 알파 1/4~1배
            for (int k = 1; k < glowTones.Length; k++)
            {
                Color color = new Color32(255, 138, 48, 255);
                if (true == bLinear) color = color.linear;
                color.a = groundGlowStrength * k / 4.0f;
                glowTones[k] = color;
            }

            glowVertexCount = -1;
        }

        private Color32 Tone(int _color, int _glow)
        {
            return tones[_color * GlowClassCount + _glow];
        }

        // ---------- 난수 / 노이즈 ----------

        private float Rand01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0xFFFFFFu) / 16777216.0f;
        }

        private static float Hash(int _n)
        {
            uint h = unchecked((uint)_n * 2654435761u);
            h ^= h >> 15;
            h = unchecked(h * 2246822519u);
            h ^= h >> 13;
            return (h & 0xFFFFFFu) / 16777216.0f;
        }

        private static float ValueNoise(float _x)
        {
            int i = Mathf.FloorToInt(_x);
            float f = _x - i;
            float u = f * f * (3.0f - 2.0f * f);
            return Hash(i) * (1.0f - u) + Hash(i + 1) * u;
        }

        private static float EaseOutBack(float _t, float _overshoot)
        {
            float c3 = _overshoot + 1.0f;
            float u = _t - 1.0f;
            return 1.0f + c3 * u * u * u + _overshoot * u * u;
        }

        // ---------- 시작 / 정지 ----------

        private void BeginWaiting()
        {
            state = StateWaiting;
            delayLeft = pendingDelay;
            ClearSimulation();
            HideMeshes();
        }

        private void StartIgnition()
        {
            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            ClearSimulation();
            state = StateIgniting;
            stateTime = 0.0f;
            bBurstDone = false;
            nextPop = 2.6f;
        }

        private void Stop()
        {
            state = StateOff;
            ClearSimulation();
            HideMeshes();
        }

        private void ClearSimulation()
        {
            System.Array.Clear(heat, 0, heat.Length);
            for (int i = 0; i < particles.Length; i++) particles[i].bAlive = false;

            ign = 0.0f;
            coalLevel = 0.0f;
            groundLight = 0.0f;
            flare = 0.0f;
            fireAccumulator = 0.0f;
            emberAccumulator = 0.0f;
            smokeAccumulator = 0.0f;
            glowAccumulator = 0.0f;
        }

        private void HideMeshes()
        {
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
            if (null != glowRenderer && true == glowRenderer.enabled) glowRenderer.enabled = false;
            flameVertexCount = -1;
            glowVertexCount = -1;
        }

        // ---------- 시뮬레이션 ----------

        private void Step(float _dt)
        {
            time += _dt;
            stateTime += _dt;
            wind = (ValueNoise(time * 0.33f) - 0.5f) * 2.0f;
            flare *= Mathf.Exp(-_dt * 5.0f);

            if (StateIgniting == state)
            {
                ign = 0.1f > stateTime ? 0.0f : Mathf.Min(1.3f, EaseOutBack(Mathf.Min(1.0f, (stateTime - 0.1f) / 0.62f), 1.7f));
                coalLevel = Mathf.Min(1.0f, stateTime / 0.25f) * 0.8f;

                if (false == bBurstDone && 0.08f <= stateTime)
                {
                    bBurstDone = true;
                    SpawnIgniteBurst();
                    flare = 1.0f;
                }

                if (0.9f <= stateTime)
                {
                    state = StateLit;
                    stateTime = 0.0f;
                }
            }
            else
            {
                ign = 1.0f + 0.05f * (ValueNoise(time * 0.8f) - 0.5f);
                coalLevel = 1.0f;
            }

            groundLight = ign * (0.8f + 0.28f * ValueNoise(time * 5.3f) + 0.08f * Mathf.Sin(time * 17.0f));

            fireAccumulator += _dt;
            while (FireStepInterval <= fireAccumulator)
            {
                fireAccumulator -= FireStepInterval;
                FireStep();
                TrySpawnLick();
            }

            if (0.3f < ign)
            {
                emberAccumulator += _dt * 8.0f * ign;
                while (1.0f <= emberAccumulator)
                {
                    SpawnEmber();
                    emberAccumulator -= 1.0f;
                }

                smokeAccumulator += _dt;
                while (1.0f <= smokeAccumulator)
                {
                    SpawnSmoke();
                    smokeAccumulator -= 1.0f;
                }
            }

            if (StateLit == state)
            {
                nextPop -= _dt;
                if (0.0f >= nextPop)
                {
                    SpawnPopBurst();
                    nextPop = 1.6f + Rand01() * 2.4f;
                }
            }

            StepParticles(_dt);
        }

        // 아래 줄의 열이 위로 올라오며(좌우로 조금 흔들리며) 식는다. 맨 아랫줄은 중앙이 센 원천이다.
        private void FireStep()
        {
            float windBias = wind * 0.22f;

            for (int y = GridHeight - 1; y >= 1; y--)
            {
                float rowCool = 0.009f + y * 0.0009f;
                for (int x = 0; x < GridWidth; x++)
                {
                    float roll = Rand01();
                    int shift = 0.1f + windBias > roll ? -1 : (0.9f + windBias < roll ? 1 : 0);
                    int sourceX = Mathf.Clamp(x + shift, 0, GridWidth - 1);

                    float cool = rowCool + Rand01() * 0.026f + edgeCool[x];
                    float value = heat[(y - 1) * GridWidth + sourceX] - cool;
                    heat[y * GridWidth + x] = 0.0f < value ? value : 0.0f;
                }
            }

            float flicker = 1.0f + flare * 0.35f + 0.1f * ValueNoise(time * 3.1f);
            for (int x = 0; x < GridWidth; x++)
            {
                float source = ign * sourceMask[x] * flicker * (0.82f + Rand01() * 0.3f);
                heat[x] = 1.15f < source ? 1.15f : source;
            }
        }

        private void TrySpawnLick()
        {
            if (0.5f > ign || 0.06f < Rand01()) return;

            int x = GridCenter + Mathf.FloorToInt((Rand01() - 0.5f) * 7.0f);
            for (int y = GridHeight - 1; y > 6; y--)
            {
                if (0.22f < heat[y * GridWidth + x])
                {
                    SpawnLick(x - GridCenter, FlameBaseY + y);
                    return;
                }
            }
        }

        // ---------- 입자 생성 ----------

        private int AcquireParticle()
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (false == particles[i].bAlive) return i;
            }

            return -1;
        }

        private void SpawnEmber()
        {
            int i = AcquireParticle();
            if (0 > i) return;

            float roll = Rand01();
            particles[i].bAlive = true;
            particles[i].kind = KindEmber;
            particles[i].shape = 0.5f > roll ? PixelPainter.ShapeDot : (0.7f > roll ? PixelPainter.ShapePlus : (0.87f > roll ? PixelPainter.ShapeCross : (0.94f > roll ? PixelPainter.ShapeCircle : PixelPainter.ShapeDiamond)));
            particles[i].id = particleId++;
            particles[i].x = (Rand01() - 0.5f) * 9.0f;
            particles[i].y = EmberMinY + Rand01() * 9.0f;
            particles[i].vx = (Rand01() - 0.5f) * 8.0f;
            particles[i].vy = 14.0f + Rand01() * 20.0f;
            particles[i].age = 0.0f;
            particles[i].life = 1.6f + Rand01() * 1.8f;
        }

        private void SpawnLick(float _x, float _y)
        {
            int i = AcquireParticle();
            if (0 > i) return;

            particles[i].bAlive = true;
            particles[i].kind = KindLick;
            particles[i].shape = PixelPainter.ShapePlus;
            particles[i].id = particleId++;
            particles[i].x = _x;
            particles[i].y = _y;
            particles[i].vx = (Rand01() - 0.5f) * 6.0f;
            particles[i].vy = 26.0f + Rand01() * 16.0f;
            particles[i].age = 0.0f;
            particles[i].life = 0.45f + Rand01() * 0.3f;
        }

        // 위쪽으로 _spread 라디안 안에서 튀는 불똥 하나
        private void SpawnPop(float _x, float _y, float _spread)
        {
            int i = AcquireParticle();
            if (0 > i) return;

            float deviation = (Rand01() - 0.5f) * _spread;
            float speed = 55.0f + Rand01() * 55.0f;
            float roll = Rand01();

            particles[i].bAlive = true;
            particles[i].kind = KindPop;
            particles[i].shape = 0.25f > roll ? PixelPainter.ShapeDot : (0.5f > roll ? PixelPainter.ShapeCross : (0.75f > roll ? PixelPainter.ShapePlus : PixelPainter.ShapeDot));
            particles[i].id = particleId++;
            particles[i].x = _x;
            particles[i].y = _y;
            particles[i].vx = Mathf.Sin(deviation) * speed * 0.6f;
            particles[i].vy = Mathf.Cos(deviation) * speed;
            particles[i].age = 0.0f;
            particles[i].life = 0.5f + Rand01() * 0.45f;
        }

        private void SpawnSmoke()
        {
            int i = AcquireParticle();
            if (0 > i) return;

            particles[i].bAlive = true;
            particles[i].kind = KindSmoke;
            particles[i].shape = PixelPainter.ShapeDot;
            particles[i].tone = Mathf.Min(SmokeToneCount - 1, Mathf.FloorToInt(Rand01() * SmokeToneCount));
            particles[i].id = particleId++;
            particles[i].x = (Rand01() - 0.5f) * 8.0f;
            particles[i].y = SmokeMinY + Rand01() * 8.0f;
            particles[i].vx = (Rand01() - 0.5f) * 4.0f;
            particles[i].vy = 9.0f + Rand01() * 7.0f;
            particles[i].age = 0.0f;
            particles[i].life = 2.0f + Rand01() * 1.2f;
        }

        private void SpawnPopBurst()
        {
            int count = 5 + Mathf.FloorToInt(Rand01() * 4.0f);
            for (int i = 0; i < count; i++) SpawnPop((Rand01() - 0.5f) * 6.0f, PopY, 1.5f);

            flare = Mathf.Max(flare, 0.8f);
        }

        // 점화 순간: 위쪽 반원으로 퍼지는 불똥 18개와 불씨 8개
        private void SpawnIgniteBurst()
        {
            for (int i = 0; i < 18; i++)
            {
                int slot = AcquireParticle();
                if (0 > slot) break;

                float angle = Mathf.PI * (0.04f + 0.92f * i / 17.0f) + (Rand01() - 0.5f) * 0.2f;
                float speed = 55.0f + Rand01() * 70.0f;
                float roll = Rand01();

                particles[slot].bAlive = true;
                particles[slot].kind = KindPop;
                particles[slot].shape = 0.4f > roll ? PixelPainter.ShapeDot : (0.6f > roll ? PixelPainter.ShapeCross : (0.8f > roll ? PixelPainter.ShapePlus : PixelPainter.ShapeDiamond));
                particles[slot].id = particleId++;
                particles[slot].x = (Rand01() - 0.5f) * 4.0f;
                particles[slot].y = IgniteBurstY;
                particles[slot].vx = Mathf.Cos(angle) * speed;
                particles[slot].vy = Mathf.Sin(angle) * speed;
                particles[slot].age = 0.0f;
                particles[slot].life = 0.55f + Rand01() * 0.5f;
            }

            for (int i = 0; i < 8; i++) SpawnEmber();
        }

        private void StepParticles(float _dt)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (false == particles[i].bAlive) continue;

                particles[i].age += _dt;
                if (particles[i].life <= particles[i].age)
                {
                    particles[i].bAlive = false;
                    continue;
                }

                if (KindPop == particles[i].kind)
                {
                    particles[i].vy -= 150.0f * _dt;
                    particles[i].vx *= 1.0f - 0.6f * _dt;
                }
                else
                {
                    float sway = (ValueNoise(particles[i].y * 0.045f + time * 0.55f) - 0.5f) * (KindSmoke == particles[i].kind ? 30.0f : 42.0f) + wind * 10.0f;
                    particles[i].vx += (sway - particles[i].vx) * Mathf.Min(1.0f, 1.3f * _dt);
                    if (KindEmber == particles[i].kind) particles[i].vy += 10.0f * _dt;
                    particles[i].vy *= 1.0f - 0.25f * _dt;
                }

                particles[i].x += particles[i].vx * _dt;
                particles[i].y += particles[i].vy * _dt;
            }
        }

        // ---------- 그리기 ----------

        private void RebuildFlame()
        {
            flamePainter.Begin(flameBuffer, transform.worldToLocalMatrix, transform.position.z);

            DrawSmoke(flamePainter);
            DrawCoals(flamePainter);
            DrawFire(flamePainter);
            DrawParticles(flamePainter);
            DrawFlash(flamePainter);

            flamePainter.Commit(flameMesh, meshRenderer, ref flameHash, ref flameVertexCount);
        }

        // 불꽃 몸통: 열 칸마다 4단계 색. 아래에 불이 없는 칸은 건너뛰고(공중에 뜬 조각 방지) 바닥 4줄은 타원으로 깎는다.
        private void DrawFire(PixelPainter _p)
        {
            int ditherShift = Mathf.FloorToInt(time * 15.0f);

            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    float value = heat[y * GridWidth + x];
                    if (0.1f > value) continue;

                    int tier = 0.82f < value ? 3 : (0.55f < value ? 2 : (0.3f < value ? 1 : 0));
                    if (0 < y && 0.1f > heat[(y - 1) * GridWidth + x]) continue;

                    if (4 > y)
                    {
                        float ex = (x - GridCenter) / 7.4f;
                        float ey = (3.4f - y) / 3.4f;
                        if (1.0f < ex * ex + ey * ey) continue;

                        if (0 == y)
                        {
                            if (0 == ((x + ditherShift) & 1)) continue;
                            tier = 0;
                        }
                        else if (1 == y && 1 < tier)
                        {
                            tier = 1;
                        }
                        else if (2 == y && 2 < tier)
                        {
                            tier = 2;
                        }
                    }

                    _p.Dot(originX + x - GridCenter, originY + FlameBaseY + y, Tone(FireColor[tier], FireGlow[tier]));
                }
            }
        }

        private void DrawCoals(PixelPainter _p)
        {
            if (0.01f >= coalLevel) return;

            for (int i = 0; i < CoalCount; i++)
            {
                float n = ValueNoise(time * 4.2f + i * 7.7f) * coalLevel;
                int color;
                int glow;

                if (0.7f < n)
                {
                    color = ColorYellow;
                    glow = GlowHigh;
                }
                else if (0.5f < n)
                {
                    color = ColorOrange;
                    glow = GlowMid;
                }
                else if (0.34f < n)
                {
                    color = ColorRed;
                    glow = GlowDim;
                }
                else
                {
                    continue;
                }

                _p.Dot(originX + coalX[i], originY + coalY[i], Tone(color, glow));
            }
        }

        // 연기: 어릴 때는 점, 이후 4x4 원. 수명 끝으로 갈수록 바이어 디더로 성글어진다.
        private void DrawSmoke(PixelPainter _p)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (false == particles[i].bAlive || KindSmoke != particles[i].kind) continue;

                float u = particles[i].age / particles[i].life;
                float fade = 0.45f < u ? (u - 0.45f) / 0.55f : (0.1f > u ? (0.1f - u) / 0.1f : 0.0f);
                fade = Mathf.Min(0.92f, fade * 0.9f + 0.12f);

                int x = originX + Mathf.RoundToInt(particles[i].x);
                int y = originY + Mathf.RoundToInt(particles[i].y);

                float warm = Mathf.Max(0.0f, 1.0f - (particles[i].y - FlameBaseY) / 64.0f) * 0.55f * 0.3f;
                int warmStep = Mathf.Clamp(Mathf.FloorToInt(warm / 0.165f * SmokeWarmSteps), 0, SmokeWarmSteps - 1);
                Color32 color = smokeTones[particles[i].tone * SmokeWarmSteps + warmStep];

                if (0.2f > u)
                {
                    DitherDot(_p, x, y, color, fade);
                }
                else
                {
                    DitherRow(_p, x - 1, x, y - 2, color, fade);
                    DitherRow(_p, x - 2, x + 1, y - 1, color, fade);
                    DitherRow(_p, x - 2, x + 1, y, color, fade);
                    DitherRow(_p, x - 1, x, y + 1, color, fade);
                }
            }
        }

        private static void DitherDot(PixelPainter _p, int _x, int _y, Color32 _color, float _fade)
        {
            if (Bayer[(_x & 3) + ((_y & 3) << 2)] / 16.0f < _fade) return;
            _p.Dot(_x, _y, _color);
        }

        private static void DitherRow(PixelPainter _p, int _x0, int _x1, int _y, Color32 _color, float _fade)
        {
            for (int x = _x0; x <= _x1; x++) DitherDot(_p, x, _y, _color, _fade);
        }

        // 불씨, 불똥, 장작 튀김. 나이에 따라 색이 식고 모양이 줄며 후반에 30Hz로 깜빡인다.
        private void DrawParticles(PixelPainter _p)
        {
            int blinkFrame = Mathf.FloorToInt(time * 30.0f);

            for (int i = 0; i < particles.Length; i++)
            {
                if (false == particles[i].bAlive || KindSmoke == particles[i].kind) continue;

                float u = particles[i].age / particles[i].life;
                int shape = particles[i].shape;
                int color;
                int glow;

                if (KindEmber == particles[i].kind)
                {
                    color = 0.1f > u ? ColorCore : (0.32f > u ? ColorYellow : (0.58f > u ? ColorOrange : (0.8f > u ? ColorRed : ColorDark)));
                    glow = 0.58f > u ? GlowHigh : GlowSoft;
                    if (0.5f < u && PixelPainter.ShapeCircle <= shape) shape = PixelPainter.ShapePlus;
                    if (0.75f < u && PixelPainter.ShapeDot != shape) shape = PixelPainter.ShapeDot;
                    if (0.7f < u && 0 != ((blinkFrame + particles[i].id) & 1)) continue;
                }
                else if (KindLick == particles[i].kind)
                {
                    color = 0.3f > u ? ColorYellow : (0.8f > u ? ColorOrange : ColorRed);
                    glow = GlowHigh;
                    shape = 0.6f > u ? PixelPainter.ShapePlus : PixelPainter.ShapeDot;
                }
                else
                {
                    color = 0.35f > u ? ColorWhite : (0.7f > u ? ColorYellow : ColorOrange);
                    glow = GlowHigh;
                    if (0.7f < u && 0 != ((blinkFrame + particles[i].id) & 1)) continue;
                }

                _p.SpacedShape(shape, originX + Mathf.RoundToInt(particles[i].x), originY + Mathf.RoundToInt(particles[i].y), Tone(color, glow));
            }
        }

        // 점화 섬광(0.06~0.22초): 흰 사각 원판 + 가로/세로 광선 + 대각선 짧은 광선. 시간이 갈수록 줄어든다.
        private void DrawFlash(PixelPainter _p)
        {
            if (StateIgniting != state || 0.06f > stateTime || 0.22f < stateTime) return;

            float k = 1.0f - (stateTime - 0.06f) / 0.16f;
            int cx = originX;
            int cy = originY + FlashCenterY;
            int length = Mathf.RoundToInt(3.0f + 10.0f * k);
            int r = Mathf.Max(1, Mathf.RoundToInt(1.0f + 2.5f * k));

            Color32 white = Tone(ColorWhite, GlowMax);
            Color32 core = Tone(ColorCore, GlowMax);
            Color32 coreSoft = Tone(ColorCore, GlowHigh);

            _p.Rect(cx - r, cy - r, cx + r + 1, cy + r + 1, white);

            for (int i = 1; i <= length; i++)
            {
                Color32 color = i > length * 0.6f ? core : white;
                _p.Dot(cx - r - i, cy, color);
                _p.Dot(cx + r + i, cy, color);
            }

            int vertical = Mathf.RoundToInt(length * 0.8f);
            for (int i = 1; i <= vertical; i++)
            {
                Color32 color = i > vertical * 0.6f ? core : white;
                _p.Dot(cx, cy - r - i, color);
                _p.Dot(cx, cy + r + i, color);
            }

            int diagonal = Mathf.RoundToInt(length * 0.45f);
            for (int i = 1; i <= diagonal; i++)
            {
                _p.Dot(cx - r - i, cy - r - i, coreSoft);
                _p.Dot(cx + r + i, cy - r - i, coreSoft);
                _p.Dot(cx - r - i, cy + r + i, coreSoft);
                _p.Dot(cx + r + i, cy + r + i, coreSoft);
            }
        }

        // 바닥 불빛: 타원 감쇠를 4단계로 나눠 바이어 디더로 경계를 흩뿌린다. 15Hz로만 다시 만든다.
        private void RebuildGlow()
        {
            if (null == glowRenderer || 0.0f >= groundGlowStrength) return;

            glowPainter.Begin(glowBuffer, glowRenderer.transform.worldToLocalMatrix, glowRenderer.transform.position.z);

            float light = Mathf.Round(groundLight * 20.0f) / 20.0f;
            int width = 2 * GlowRadiusX + 1;

            if (0.01f < light)
            {
                for (int y = -GlowRadiusY; y <= GlowRadiusY; y++)
                {
                    int wy = originY + GlowCenterY + y;
                    int row = (y + GlowRadiusY) * width;

                    for (int x = -GlowRadiusX; x <= GlowRadiusX; x++)
                    {
                        float falloff = glowFalloff[row + x + GlowRadiusX];
                        if (0.0f >= falloff) continue;

                        float level = Mathf.Min(1.0f, falloff * light) * 4.0f;
                        int whole = Mathf.FloorToInt(level);
                        int wx = originX + x;
                        float threshold = (Bayer[(wx & 3) + ((wy & 3) << 2)] + 0.5f) / 16.0f;
                        int step = whole + (level - whole > threshold ? 1 : 0);
                        if (0 == step) continue;

                        glowPainter.Dot(wx, wy, glowTones[step]);
                    }
                }
            }

            glowPainter.Commit(glowMesh, glowRenderer, ref glowHash, ref glowVertexCount);
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedSortingOrder = int.MinValue;
            flameVertexCount = -1;
            glowVertexCount = -1;

            if (true == bOpen) BeginWaiting();
        }

        private void LateUpdate()
        {
            if (StateOff == state) return;

            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);

            if (StateWaiting == state)
            {
                delayLeft -= dt;
                if (0.0f < delayLeft) return;

                StartIgnition();
                dt = 0.0f;
            }

            Step(dt);

            Vector3 pos = transform.position;
            originX = Mathf.RoundToInt(pos.x * PixelsPerUnit);
            originY = Mathf.RoundToInt(pos.y * PixelsPerUnit);

            // 화면 밖이면 그리기만 건너뛴다(시뮬레이션은 가벼워서 계속 돌려 다시 들어와도 불꽃이 이어진다)
            if (false == PixelVfxCulling.IsVisible(pos + new Vector3(0.0f, CullCenterYPixels / PixelsPerUnit, 0.0f), cullSize))
            {
                HideMeshes();
                return;
            }

            int order = (null != sortable ? sortable.ComputeSortingOrder(pos.y) : 0) + sortingOrderOffset;
            if (order != appliedSortingOrder)
            {
                appliedSortingOrder = order;
                meshRenderer.sortingOrder = order;
            }

            RebuildFlame();

            glowAccumulator += dt;
            if (GlowInterval <= glowAccumulator || -1 == glowVertexCount)
            {
                glowAccumulator = 0.0f;
                RebuildGlow();
            }
        }

        private void OnDisable()
        {
            HideMeshes();
        }

        private void OnDestroy()
        {
            if (null != flameMesh)
            {
                Destroy(flameMesh);
                flameMesh = null;
            }

            if (null != glowMesh)
            {
                Destroy(glowMesh);
                glowMesh = null;
            }
        }

        private void OnValidate()
        {
            glowScale = Mathf.Clamp(glowScale, 0.0f, 2.0f);
            groundGlowStrength = Mathf.Clamp01(groundGlowStrength);

#if UNITY_EDITOR
            if (true == bInitialized) BuildTones();
#endif
        }
    }
}
