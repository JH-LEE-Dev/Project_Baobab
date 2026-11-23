using PresentationLayer.VFX;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 나무 지진(흑요 부적 III) 기 모음 연출(EnvironmentSystem이 돌린다). 전조 동안 용암빛 기운이 진원으로 휘몰아쳐 모이고,
/// 히트스톱 동안 한 점에 눌려 담겼다가, 물결이 퍼지는 순간 고리로 터져 나간다. 게임 판정·피해·다른 연출과 상관없는 그림이다.
///
/// [흡수 - 전조(게임 시간)] 굵은 기운 줄기 여덟 가닥이 아이소 타원(세로 절반)을 따라 소용돌이치며 진원으로 빨려 든다.
///   줄기 머리는 하얗게 빛나고(HDR) 꼬리로 갈수록 밝은 노랑 -> 주황 -> 진홍 띠로 식으며 끝은 검붉은 체커 도트로 흩어진다.
///   안쪽에는 주황·진홍 도트 잔상이 늦게 따라온다. 불티는 떠오르며, 흙먼지는 바닥을 끌리며 빨려 든다.
///   바닥 고리가 주황 -> 밝은 노랑 -> 흰색으로 바뀌며 조여 들고, 끝 무렵 진원에 작은 흰 핵이 생긴다. 전조가 진행될수록 빨라지고 밝아진다.
/// [압축 - 히트스톱(실제 시간, 일시정지 중에는 멈춘다)] 핵이 부풀었다가 납작하게 눌린다(찍기 직전). 십자 광채와 바늘 광선이 뻗고,
///   검붉은 체커 고리가 안으로 조여 들어 섬광 속에서도 핵의 윤곽을 잡아 준다.
/// [방출 - 물결 시작(게임 시간)] 물결 앞머리를 따라 굵은 흰 고리가 퍼지고 안쪽은 주황·진홍 띠, 끝은 검붉은 체커 꼬리다. 잠깐 뒤 식어 사라진다.
///   주황·진홍 불꽃이 사방으로 튄다.
///
/// 32 PPU 월드 픽셀 격자에 맞춘 절차적 쿼드 메쉬(PixelPainter, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더 - 회전베기와 같은 방식)다.
/// 바닥 그림(줄기·고리·먼지)은 나무·캐릭터 아래(ShockWave), 핵·광채·불티·불꽃은 나무 위(FlyingItem)에 그린다.
/// 모양을 정하는 난수는 따로 둔 해시라 게임 난수(UnityEngine.Random)의 흐름을 바꾸지 않는다. 재생 중 힙 할당이 없다.
/// </summary>
public sealed class QuakeChargeEffect
{
    private const float PixelsPerUnit = 32f;
    private const float TwoPi = Mathf.PI * 2f;

    // //흡수
    private const int StreakCount = 8;
    // 줄기가 출발하는 반지름(전조 반지름 buildUpRadius 대비, 바닥 평면 거리)
    private const float StreakStartRadiusRatio = 0.65f;
    // 줄기 꼬리 길이(경로 비율)
    private const float StreakTrail = 0.3f;
    // 안쪽 도트 잔상: 반지름 비율, 늦게 따라오는 양(진행 비율), 꼬리 길이
    private const float EchoRadiusRatio = 0.78f;
    private const float EchoLag = 0.12f;
    private const float EchoTrail = 0.2f;
    private const int EmberCount = 18;
    private const int DustCount = 12;
    // 전조 끝 무렵 진원에 생기는 작은 핵(전조 진행 비율 이 값부터)
    private const float PreCoreStart = 0.55f;
    // 바닥 고리가 조여 드는 마지막 반지름(px)
    private const int GatherRingMinPx = 8;
    // 줄기 꼬리 점 수 상한
    private const int MaxTrailSamples = 240;

    // //압축
    // 핵이 땅에서 떠 있는 높이(px)
    private const int CoreLiftPx = 6;
    // 히트스톱이 꺼져 있을 때도 압축이 보이는 최소 시간(초)
    private const float MinCompressSeconds = 0.06f;

    // //방출
    private const float ReleaseRingSeconds = 0.28f;
    // 고리가 핵 가장자리에서 시작하도록 앞머리 반지름에 더하는 px
    private const int ReleaseRingStartPx = 14;
    private const int SparkCount = 14;
    private const float SparkLifeMin = 0.22f;
    private const float SparkLifeMax = 0.4f;
    private const float SparkGravity = 420f;   // px/s^2
    private const float SparkDrag = 5f;        // 1/s
    private const float SparkStreakSeconds = 0.04f;

    // 정점 알파 = 발광 세기(0~1 -> GlowMin~GlowMax). 카메라 블룸(임계 1.345)이 세므로 흰색만 임계를 넘겨 번지게 하고,
    // 밝은 노랑은 살짝 넘기며 나머지는 임계 아래로 둔다(회전베기와 같은 기준).
    private const float GlowMin = 1f;
    private const float GlowMax = 2.4f;

    // 용암 계열(화면에서 흰 -> #FFE9A0 -> #FF9A3C -> #D9412B -> #5A1A1A). 셰이더가 정점 색을 리니어 값 그대로 내므로
    // 화면 색(sRGB)을 리니어로 바꿔 넣는다.
    private static readonly Color32 LavaWhite = new Color32(255, 255, 222, 255);
    private static readonly Color32 LavaLight = new Color32(255, 206, 90, 90);
    private static readonly Color32 LavaOrange = new Color32(255, 82, 12, 0);
    private static readonly Color32 LavaCrimson = new Color32(177, 13, 6, 0);
    private static readonly Color32 LavaDark = new Color32(26, 3, 3, 0);
    // 방출 고리의 흰 띠·밝은 노랑 띠. 색은 같고 발광 세기만 낮춰 블룸이 LavaWhite·LavaLight의 80%가 되게 했다
    // (URP 블룸 프리필터: 임계 = Mathf.GammaToLinearSpace(1.345) ≈ 1.9195, 무릎 = 임계 × 0.5. 알파 255 -> 227, 90 -> 80).
    private static readonly Color32 ReleaseRingWhite = new Color32(255, 255, 222, 227);
    private static readonly Color32 ReleaseRingLight = new Color32(255, 206, 90, 80);

    private const string GroundSortingLayerName = "ShockWave";
    private const string AirSortingLayerName = "FlyingItem";
    private const int SortingOrder = 0;
    private const string ShaderName = "Custom/VFX/BrandWrapGlow";
    private static readonly int GlowMinID = Shader.PropertyToID("_GlowMin");
    private static readonly int GlowMaxID = Shader.PropertyToID("_GlowMax");

    private enum EPhase
    {
        Idle,
        Absorb,
        Compress,
        Release,
    }

    private QuakeTileRippleSettings settings;
    private GameObject root;
    private Material material;
    private readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(2048);
    private readonly PixelQuadBuffer airBuffer = new PixelQuadBuffer(1024);
    private readonly PixelPainter painter = new PixelPainter();

    private Transform groundTransform;
    private Transform airTransform;
    private Mesh groundMesh;
    private Mesh airMesh;
    private MeshRenderer groundRenderer;
    private MeshRenderer airRenderer;
    private ulong groundHash;
    private ulong airHash;
    private int groundVertexCount = -1;
    private int airVertexCount = -1;

    private EPhase phase = EPhase.Idle;
    private float absorbElapsed;
    private float compressElapsed;
    private float releaseElapsed;
    private QuakeTileRipple ripple;
    private int centerX;
    private int centerY;
    private uint seed = 1u;
    private uint playCounter;

    // 줄기마다 정해 두는 값(재생 시작 때 채운다)
    private readonly float[] streakLaunch = new float[StreakCount];
    private readonly float[] streakEnd = new float[StreakCount];
    private readonly float[] streakAngle = new float[StreakCount];
    private readonly float[] streakTurns = new float[StreakCount];
    private readonly float[] streakRadius = new float[StreakCount];

    // 불티·흙먼지·불꽃마다 정해 두는 값(재생 시작 때 해시·삼각함수를 한 번만 계산해 둔다 - 매 프레임 같은 값이다)
    private readonly float[] emberSpawn = new float[EmberCount];
    private readonly float[] emberLife = new float[EmberCount];
    private readonly float[] emberCos = new float[EmberCount];
    private readonly float[] emberSin = new float[EmberCount];
    private readonly float[] emberRadius = new float[EmberCount];
    private readonly float[] emberRise = new float[EmberCount];
    private readonly int[] emberShape = new int[EmberCount];
    private readonly float[] dustSpawn = new float[DustCount];
    private readonly float[] dustLife = new float[DustCount];
    private readonly float[] dustCos = new float[DustCount];
    private readonly float[] dustSin = new float[DustCount];
    private readonly float[] dustRadius = new float[DustCount];
    private readonly bool[] dustWide = new bool[DustCount];
    private Color32 dustColor;
    private readonly float[] sparkLife = new float[SparkCount];
    private readonly float[] sparkSpeed = new float[SparkCount];
    private readonly float[] sparkUp = new float[SparkCount];
    private readonly float[] sparkDirX = new float[SparkCount];
    private readonly float[] sparkDirY = new float[SparkCount];

    // 줄기 꼬리 점(테두리·몸통 두 번 그릴 때 위치를 한 번만 계산한다). 점 수는 TrailSamples 상한 + 1까지다.
    private readonly int[] trailX = new int[MaxTrailSamples + 1];
    private readonly int[] trailY = new int[MaxTrailSamples + 1];
    private readonly float[] trailS = new float[MaxTrailSamples + 1];

    // 직전에 그린 상태. 일시정지 등으로 진행 시간이 그대로면 같은 그림이라 다시 그리지 않는다.
    private bool bDrawn;
    private EPhase drawnPhase;
    private float drawnTime;
    private float drawnFront;

    public void Initialize(QuakeTileRippleSettings _settings, Transform _parent)
    {
        settings = _settings;
        if (null == settings || null != root) return;

        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogWarning("[QuakeChargeEffect] 셰이더 " + ShaderName + "를 찾지 못해 지진 기 모음 연출을 건너뜁니다.");
            return;
        }

        material = new Material(shader) { name = "M_QuakeCharge (Runtime)" };
        material.SetFloat(GlowMinID, GlowMin);
        material.SetFloat(GlowMaxID, GlowMax);

        root = new GameObject("QuakeCharge");
        root.transform.SetParent(_parent, false);
        root.transform.position = Vector3.zero;

        groundRenderer = CreateLayer("Ground", GroundSortingLayerName, out groundTransform, out groundMesh);
        airRenderer = CreateLayer("Air", AirSortingLayerName, out airTransform, out airMesh);
    }

    public void Release()
    {
        Stop();
        if (null != groundMesh) DestroyObject(groundMesh);
        if (null != airMesh) DestroyObject(airMesh);
        if (null != root) DestroyObject(root);
        if (null != material) DestroyObject(material);
        root = null;
        material = null;
        groundMesh = null;
        airMesh = null;
        groundRenderer = null;
        airRenderer = null;
    }

    /// <summary>전조가 시작되는 프레임에 부른다. _center(진원, 보통 발동 순간 캐릭터 자리)로 기운이 모여 든다.</summary>
    public void PlayAbsorb(Vector3 _center)
    {
        if (null == root) return;

        BeginPlay(_center);
        phase = EPhase.Absorb;
        absorbElapsed = 0f;
    }

    /// <summary>물결이 퍼지기 시작하는 프레임(히트스톱이 걸리는 순간)에 부른다. 핵을 눌러 담았다가 히트스톱이 끝나면 고리로 터뜨린다.
    /// 흡수 없이 바로 불려도(전조가 0초) 압축부터 낸다.</summary>
    public void Burst(Vector3 _center, QuakeTileRipple _ripple)
    {
        if (null == root) return;

        if (EPhase.Absorb != phase) BeginPlay(_center);
        ripple = _ripple;
        phase = EPhase.Compress;
        compressElapsed = 0f;
        bDrawn = false;
    }

    /// <summary>바로 끝낸다(그림을 지운다). 맵을 새로 깔거나 지진이 끊길 때 부른다.</summary>
    public void Stop()
    {
        phase = EPhase.Idle;
        ripple = null;
        if (null != groundRenderer) groundRenderer.enabled = false;
        if (null != airRenderer) airRenderer.enabled = false;
        groundVertexCount = -1;
        airVertexCount = -1;
        bDrawn = false;
    }

    /// <summary>매 프레임(일시정지 중에도) 부른다. 흡수·방출은 게임 시간, 압축은 실제 시간(히트스톱 중)으로 진행한다.</summary>
    public void Tick(float _deltaTime, float _unscaledDeltaTime)
    {
        if (EPhase.Idle == phase || null == root) return;

        switch (phase)
        {
            case EPhase.Absorb:
                absorbElapsed += _deltaTime;
                break;

            case EPhase.Compress:
                // 히트스톱은 실제 시간으로 돈다. 일시정지(배율 0) 중에는 함께 멈춘다.
                if (Time.timeScale > 0f) compressElapsed += _unscaledDeltaTime;
                if (compressElapsed >= CompressSeconds)
                {
                    phase = EPhase.Release;
                    releaseElapsed = 0f;
                }
                break;

            case EPhase.Release:
                releaseElapsed += _deltaTime;
                if (releaseElapsed >= Mathf.Max(ReleaseRingSeconds, SparkLifeMax))
                {
                    Stop();
                    return;
                }
                break;
        }

        // 진행 시간(방출은 물결 앞머리 반지름까지)이 직전 그림과 같으면 그림도 같으므로 다시 그리지 않는다(일시정지 등).
        float time = EPhase.Absorb == phase ? absorbElapsed : (EPhase.Compress == phase ? compressElapsed : releaseElapsed);
        float front = EPhase.Release == phase ? ReleaseFrontUnits() : 0f;
        if (bDrawn && drawnPhase == phase && drawnTime == time && drawnFront == front) return;

        Draw();
        bDrawn = true;
        drawnPhase = phase;
        drawnTime = time;
        drawnFront = front;
    }

    // 방출 고리가 따라가는 물결 앞머리 반지름(유닛)
    private float ReleaseFrontUnits()
    {
        return null != ripple && ripple.IsPlaying ? ripple.FrontRadius : releaseElapsed * Mathf.Max(0.1f, settings.frontSpeed);
    }

    private float AbsorbSeconds => Mathf.Max(0.01f, settings.buildUpSeconds);

    private float CompressSeconds => Mathf.Max(MinCompressSeconds, settings.hitStopSeconds);

    private float StartRadiusPx => Mathf.Max(0.5f, settings.buildUpRadius) * StreakStartRadiusRatio * PixelsPerUnit;

    private void BeginPlay(Vector3 _center)
    {
        centerX = Mathf.RoundToInt(_center.x * PixelsPerUnit);
        centerY = Mathf.RoundToInt(_center.y * PixelsPerUnit);
        seed = (++playCounter * 747796405u) | 1u;
        ripple = null;
        groundVertexCount = -1;
        airVertexCount = -1;
        bDrawn = false;

        // 앞 줄기는 일찍 나와 길게, 뒤 줄기는 늦게 나와 빠르게 들어온다(전조 끝으로 갈수록 몰아친다). 모두 전조 끝 무렵 진원에 닿는다.
        float baseAngle = Hash01(97, 0) * TwoPi;
        for (int i = 0; i < StreakCount; i++)
        {
            float order = i / (float)StreakCount;
            streakLaunch[i] = 0.5f * order + 0.04f * Hash01(i, 1);
            streakEnd[i] = 0.82f + 0.18f * order;
            streakAngle[i] = baseAngle + i * (TwoPi / StreakCount) + (Hash01(i, 2) - 0.5f) * 0.5f;
            streakTurns[i] = 0.6f + 0.3f * Hash01(i, 3);
            streakRadius[i] = StartRadiusPx * (0.85f + 0.15f * Hash01(i, 4));
        }

        for (int i = 0; i < EmberCount; i++)
        {
            emberSpawn[i] = 0.7f * Hash01(i, 10);
            emberLife[i] = 0.25f + 0.2f * Hash01(i, 11);
            float angle = Hash01(i, 12) * TwoPi;
            emberCos[i] = Mathf.Cos(angle);
            emberSin[i] = Mathf.Sin(angle);
            emberRadius[i] = (1f + 2.3f * Hash01(i, 13)) * PixelsPerUnit;
            emberRise[i] = 8f + 14f * Hash01(i, 14);
            emberShape[i] = Hash01(i, 15) < 0.3f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot;
        }

        Color dust = settings.dustColor.linear;
        dustColor = new Color32((byte)(Mathf.Clamp01(dust.r) * 255f), (byte)(Mathf.Clamp01(dust.g) * 255f), (byte)(Mathf.Clamp01(dust.b) * 255f), 0);
        for (int i = 0; i < DustCount; i++)
        {
            dustSpawn[i] = 0.6f * Hash01(i, 20);
            dustLife[i] = 0.3f + 0.15f * Hash01(i, 21);
            float angle = Hash01(i, 22) * TwoPi;
            dustCos[i] = Mathf.Cos(angle);
            dustSin[i] = Mathf.Sin(angle);
            dustRadius[i] = (1.2f + 2.5f * Hash01(i, 23)) * PixelsPerUnit;
            dustWide[i] = Hash01(i, 24) < 0.5f;
        }

        for (int i = 0; i < SparkCount; i++)
        {
            sparkLife[i] = Mathf.Lerp(SparkLifeMin, SparkLifeMax, Hash01(i, 30));
            float angle = (i + Hash01(i, 31)) * (TwoPi / SparkCount);
            sparkSpeed[i] = 140f + 120f * Hash01(i, 32);
            sparkUp[i] = 60f + 100f * Hash01(i, 33);
            sparkDirX[i] = Mathf.Cos(angle);
            sparkDirY[i] = Mathf.Sin(angle) * 0.5f;
        }
    }

    // //그리기

    private void Draw()
    {
        painter.Begin(groundBuffer, groundTransform.worldToLocalMatrix, 0f);
        switch (phase)
        {
            case EPhase.Absorb: DrawAbsorbGround(AbsorbProgress); break;
            case EPhase.Compress: DrawCompressGround(compressElapsed / CompressSeconds); break;
            case EPhase.Release: DrawReleaseGround(); break;
        }
        painter.Commit(groundMesh, groundRenderer, ref groundHash, ref groundVertexCount);

        painter.Begin(airBuffer, airTransform.worldToLocalMatrix, 0f);
        switch (phase)
        {
            case EPhase.Absorb: DrawAbsorbAir(AbsorbProgress); break;
            case EPhase.Compress: DrawCompressAir(compressElapsed / CompressSeconds); break;
            case EPhase.Release: DrawReleaseAir(); break;
        }
        painter.Commit(airMesh, airRenderer, ref airHash, ref airVertexCount);
    }

    private float AbsorbProgress => Mathf.Clamp01(absorbElapsed / AbsorbSeconds);

    // //흡수

    private void DrawAbsorbGround(float _p)
    {
        // 안쪽 도트 잔상 -> 흙먼지 -> 바닥 고리 -> 줄기 순으로 겹친다(줄기가 맨 위).
        for (int i = 0; i < StreakCount; i++) DrawEcho(i, _p);
        DrawDust(_p);
        DrawGatherRing(_p);
        for (int i = 0; i < StreakCount; i++) DrawStreak(i, _p);
    }

    private void DrawAbsorbAir(float _p)
    {
        DrawEmbers(_p);

        if (_p < PreCoreStart) return;

        // 줄기가 닿기 시작하면 진원에 작은 흰 핵이 맺힌다(압축의 핵으로 이어진다).
        float k = (_p - PreCoreStart) / (1f - PreCoreStart);
        int r = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, k));
        int cy = centerY + CoreLiftPx;
        FillEllipse(centerX, cy, r + 2, Mathf.Max(1, Mathf.RoundToInt((r + 2) * 0.85f)), LavaOrange);
        FillEllipse(centerX, cy, r + 1, Mathf.Max(1, Mathf.RoundToInt((r + 1) * 0.85f)), LavaLight);
        FillEllipse(centerX, cy, r, Mathf.Max(1, Mathf.RoundToInt(r * 0.85f)), LavaWhite);
    }

    /// <summary>줄기 i의 경로 위 점(u: 0 출발 ~ 1 진원). 소용돌이는 진원에 가까울수록 더 감긴다.</summary>
    private void StreakPoint(int _i, float _u, float _radiusScale, float _angleOffset, out float _x, out float _y)
    {
        float r = streakRadius[_i] * _radiusScale * (1f - _u);
        float angle = streakAngle[_i] + _angleOffset + streakTurns[_i] * TwoPi * (_u + 0.35f * _u * _u) / 1.35f;
        _x = centerX + Mathf.Cos(angle) * r;
        _y = centerY + Mathf.Sin(angle) * r * 0.5f;
    }

    /// <summary>전조 진행 _p에서 줄기 i의 머리 위치(경로 비율). 나오기 전이면 -1, 진원에 닿았으면 2.</summary>
    private float StreakHead(int _i, float _p)
    {
        if (_p < streakLaunch[_i]) return -1f;
        if (_p >= streakEnd[_i]) return 2f;

        float tau = (_p - streakLaunch[_i]) / (streakEnd[_i] - streakLaunch[_i]);
        // 처음엔 느리게, 진원에 가까울수록 빨라진다.
        return Mathf.Pow(tau, 1.7f);
    }

    private void DrawStreak(int _i, float _p)
    {
        float u = StreakHead(_i, _p);
        if (u < 0f || u > 1f) return;

        float uTail = Mathf.Max(0f, u - StreakTrail);
        int samples = TrailSamples(_i, u, uTail, 1f, 0f);

        // 전조가 진행될수록 흰 머리가 길어진다(밝아진다).
        float whiteEnd = 0.05f + 0.1f * _p;
        float lightEnd = whiteEnd + 0.15f;

        // 꼬리 점 위치는 두 번(테두리·몸통) 그리므로 한 번만 계산해 둔다.
        for (int k = samples; k >= 0; k--)
        {
            float uu = Mathf.Lerp(u, uTail, k / (float)samples);
            trailS[k] = (u - uu) / StreakTrail;
            StreakPoint(_i, uu, 1f, 0f, out float fx, out float fy);
            trailX[k] = Mathf.RoundToInt(fx);
            trailY[k] = Mathf.RoundToInt(fy);
        }

        // 점 간격이 1px보다 촘촘해 이웃 점이 같은 칸에 떨어지는 일이 많다. 바로 앞에 그린 것과 똑같은 사각형·점(같은 자리·크기·색)은
        // 같은 픽셀을 같은 색으로 다시 덮을 뿐이라 건너뛴다(사이에 다른 것을 그리지 않았을 때만이라 겹침 순서도 그대로다).
        // 1) 테두리: 줄기 둘레를 한 칸 넓게 주황(머리 쪽)·진홍(몸통)으로 감싸 용암 덩어리처럼 보이게 한다.
        //    끝은 검붉은 체커 도트로 흩어진다. 꼬리 -> 머리 순으로 찍어 머리가 위에 온다.
        int lastX = int.MinValue;
        int lastY = int.MinValue;
        int lastHalf = int.MinValue;
        Color32 lastColor = default;
        for (int k = samples; k >= 0; k--)
        {
            float s = trailS[k];
            int x = trailX[k];
            int y = trailY[k];

            if (s >= 0.78f)
            {
                // (x, y)와 (x + 1, y) 중 체커 칸(x + y 짝수)에 드는 한 점만 찍힌다. lastHalf -1은 점 표시다.
                int dotX = 0 == ((x + y) & 1) ? x : x + 1;
                if (-1 == lastHalf && dotX == lastX && y == lastY) continue;

                painter.Dot(dotX, y, LavaDark);
                lastX = dotX;
                lastY = y;
                lastHalf = -1;
                continue;
            }

            int half = StreakHalfWidth(s) + 1;
            Color32 color = s < whiteEnd + 0.1f ? LavaOrange : LavaCrimson;
            if (half == lastHalf && x == lastX && y == lastY && SameColor(color, lastColor)) continue;

            painter.Rect(x - half, y - half, x + half + 1, y + half + 1, color);
            lastX = x;
            lastY = y;
            lastHalf = half;
            lastColor = color;
        }

        // 2) 몸통: 머리는 굵고 꼬리로 갈수록 가늘어지며 흰 -> 밝은 노랑 -> 주황 -> 진홍 띠로 식는다.
        lastHalf = int.MinValue;
        for (int k = samples; k >= 0; k--)
        {
            float s = trailS[k];
            if (s >= 0.66f) continue;

            int x = trailX[k];
            int y = trailY[k];
            int half = StreakHalfWidth(s);
            Color32 color = s < whiteEnd ? LavaWhite : (s < lightEnd ? LavaLight : (s < 0.5f ? LavaOrange : LavaCrimson));
            if (half == lastHalf && x == lastX && y == lastY && SameColor(color, lastColor)) continue;

            painter.Rect(x - half, y - half, x + half + 1, y + half + 1, color);
            lastX = x;
            lastY = y;
            lastHalf = half;
            lastColor = color;
        }
    }

    private static bool SameColor(Color32 _a, Color32 _b)
    {
        return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
    }

    // 줄기 굵기(중심에서 바깥까지 px): 머리 5px -> 3px -> 1px.
    private static int StreakHalfWidth(float _s)
    {
        return _s < 0.25f ? 2 : (_s < 0.5f ? 1 : 0);
    }

    private void DrawEcho(int _i, float _p)
    {
        // 늦게 따라오되 전조 끝에는 함께 닿는다.
        float u = StreakHead(_i, _p * (1f + EchoLag) - EchoLag);
        if (u < 0f || u > 1f) return;

        float uTail = Mathf.Max(0f, u - EchoTrail);
        int samples = TrailSamples(_i, u, uTail, EchoRadiusRatio, -0.4f);
        for (int k = samples; k >= 0; k -= 3)
        {
            float uu = Mathf.Lerp(u, uTail, k / (float)samples);
            float s = (u - uu) / EchoTrail;
            StreakPoint(_i, uu, EchoRadiusRatio, -0.4f, out float fx, out float fy);
            painter.Dot(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy), s < 0.4f ? LavaOrange : LavaCrimson);
        }
    }

    // 꼬리를 대략 1px 간격으로 찍을 점 수(머리·가운데·꼬리 사이 거리로 어림한다).
    private int TrailSamples(int _i, float _u, float _uTail, float _radiusScale, float _angleOffset)
    {
        StreakPoint(_i, _u, _radiusScale, _angleOffset, out float hx, out float hy);
        StreakPoint(_i, (_u + _uTail) * 0.5f, _radiusScale, _angleOffset, out float mx, out float my);
        StreakPoint(_i, _uTail, _radiusScale, _angleOffset, out float tx, out float ty);
        float length = Mathf.Sqrt((hx - mx) * (hx - mx) + (hy - my) * (hy - my)) + Mathf.Sqrt((mx - tx) * (mx - tx) + (my - ty) * (my - ty));
        return Mathf.Clamp(Mathf.CeilToInt(length * 1.2f), 4, MaxTrailSamples);
    }

    private void DrawGatherRing(float _p)
    {
        if (_p < 0.08f) return;

        float k = (_p - 0.08f) / 0.92f;
        int rx = Mathf.RoundToInt(Mathf.Lerp(StartRadiusPx * 1.02f, GatherRingMinPx, 1f - (1f - k) * (1f - k)));
        int ry = Mathf.Max(1, Mathf.RoundToInt(rx * 0.5f));
        Color32 color = k < 0.45f ? LavaOrange : (k < 0.8f ? LavaLight : LavaWhite);
        float shift = _p * 40f;

        painter.RingArc(centerX, centerY, rx + 2, ry + 1, 6, 4, shift, LavaCrimson);
        painter.RingArc(centerX, centerY, rx + 1, ry + 1, 6, 4, shift, LavaCrimson);
        painter.RingArc(centerX, centerY, rx, ry, 6, 4, shift, color);
        painter.RingArc(centerX, centerY, rx - 1, ry, 6, 4, shift, color);
    }

    private void DrawEmbers(float _p)
    {
        for (int i = 0; i < EmberCount; i++)
        {
            float e = (_p - emberSpawn[i]) / emberLife[i];
            if (e < 0f || e >= 1f) continue;

            float r = emberRadius[i] * (1f - 0.9f * Mathf.Pow(e, 1.5f));
            int x = Mathf.RoundToInt(centerX + emberCos[i] * r);
            int y = Mathf.RoundToInt(centerY + emberSin[i] * r * 0.5f + emberRise[i] * e);

            if (e < 0.6f) painter.Shape(emberShape[i], x, y, LavaLight);
            else painter.Dot(x, y, LavaOrange);
        }
    }

    private void DrawDust(float _p)
    {
        for (int i = 0; i < DustCount; i++)
        {
            float e = (_p - dustSpawn[i]) / dustLife[i];
            if (e < 0f || e >= 1f) continue;

            float r = dustRadius[i] * (1f - 0.85f * e * e);
            int x = Mathf.RoundToInt(centerX + dustCos[i] * r);
            int y = Mathf.RoundToInt(centerY + dustSin[i] * r * 0.5f + 2f * e);

            if (dustWide[i]) painter.HLine(x, x + 1, y, dustColor);
            else painter.Dot(x, y, dustColor);
        }
    }

    // //압축

    private void DrawCompressGround(float _q)
    {
        // 검붉은 체커 고리가 핵으로 조여 든다(섬광 속에서 핵의 윤곽을 잡는다). 안쪽 가장자리는 진홍 실선.
        int r = Mathf.RoundToInt(Mathf.Lerp(StartRadiusPx * 0.35f, 14f, Mathf.Pow(_q, 0.7f)));
        FillBand(centerX, centerY, r, Mathf.Max(1, Mathf.RoundToInt(r * 0.5f)), r - 3, (r - 3) * 0.5f, LavaDark, true);
        FillBand(centerX, centerY, r - 3, Mathf.Max(1, Mathf.RoundToInt((r - 3) * 0.5f)), r - 4, (r - 4) * 0.5f, LavaCrimson, false);
    }

    private void DrawCompressAir(float _q)
    {
        GetCoreShape(_q, out int rx, out int ry, out int lift, out float squash);
        int cx = centerX;
        int cy = centerY + lift;

        // 바늘 광선(대각선 여덟 방향, 점선)
        int rayLength = Mathf.RoundToInt(Mathf.Lerp(4f, 14f, _q));
        for (int i = 0; i < 8; i++)
        {
            float angle = (i + 0.5f) * (TwoPi / 8f);
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle) * 0.7f;
            int x0 = cx + Mathf.RoundToInt(c * (rx + 3));
            int y0 = cy + Mathf.RoundToInt(s * (ry + 3));
            int x1 = cx + Mathf.RoundToInt(c * (rx + 3 + rayLength));
            int y1 = cy + Mathf.RoundToInt(s * (ry + 3 + rayLength));
            painter.Line(x0, y0, x1, y1, 2, 1, 0, LavaLight);
        }

        // 십자 광채: 눌릴수록 가로는 길어지고 세로는 짧아진다.
        int halfH = rx + Mathf.RoundToInt(Mathf.Lerp(8f, 22f, _q));
        int halfV = ry + Mathf.RoundToInt(Mathf.Lerp(8f, 16f, _q) * (1f - 0.7f * squash));
        painter.HLine(cx - halfH, cx + halfH, cy, LavaLight);
        painter.Rect(cx, cy - halfV, cx + 1, cy + halfV + 1, LavaLight);
        int whiteH = Mathf.RoundToInt(halfH * 0.6f);
        int whiteV = Mathf.RoundToInt(halfV * 0.6f);
        painter.HLine(cx - whiteH, cx + whiteH, cy, LavaWhite);
        painter.Rect(cx, cy - whiteV, cx + 1, cy + whiteV + 1, LavaWhite);

        // 핵: 주황 테두리 -> 밝은 노랑 후광 -> 흰 덩어리
        FillEllipse(cx, cy, rx + 3, ry + 2, LavaOrange);
        FillEllipse(cx, cy, rx + 1, ry + 1, LavaLight);
        FillEllipse(cx, cy, rx, ry, LavaWhite);
    }

    /// <summary>압축 진행 _q의 핵 크기. 앞 60%는 부풀고, 뒤 40%는 가로로 퍼지며 납작하게 눌려 땅으로 내려온다(_squash 0 ~ 1).</summary>
    private static void GetCoreShape(float _q, out int _rx, out int _ry, out int _lift, out float _squash)
    {
        if (_q < 0.6f)
        {
            float k = _q / 0.6f;
            k = 1f - (1f - k) * (1f - k);
            _rx = Mathf.RoundToInt(Mathf.Lerp(5f, 11f, k));
            _ry = Mathf.Max(1, Mathf.RoundToInt(_rx * 0.85f));
            _lift = CoreLiftPx;
            _squash = 0f;
            return;
        }

        _squash = Mathf.Clamp01((_q - 0.6f) / 0.4f);
        _rx = Mathf.RoundToInt(Mathf.Lerp(11f, 18f, _squash));
        _ry = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(9f, 3f, _squash)));
        _lift = Mathf.RoundToInt(Mathf.Lerp(CoreLiftPx, 2f, _squash));
    }

    // //방출

    private void DrawReleaseGround()
    {
        float f = releaseElapsed / ReleaseRingSeconds;
        if (f >= 1f) return;

        float frontUnits = ReleaseFrontUnits();
        int outer = Mathf.RoundToInt(frontUnits * PixelsPerUnit) + ReleaseRingStartPx;

        // 바깥부터: 흰 띠(식으며 얇아진다) -> 밝은 노랑 -> 주황 -> 진홍 -> 검붉은 체커 꼬리.
        int whiteWidth = f < 0.35f ? 3 : (f < 0.65f ? 2 : 0);
        int lightWidth = f < 0.85f ? 2 : 0;

        int edge = outer;
        int whiteInner = edge - whiteWidth;
        int lightInner = whiteInner - lightWidth;
        int orangeInner = lightInner - 2;
        int crimsonInner = orangeInner - 2;
        int darkInner = crimsonInner - 4;

        // 안쪽 띠부터 그려 바깥 띠가 위에 온다.
        DrawIsoBand(crimsonInner, darkInner, LavaDark, true);
        DrawIsoBand(orangeInner, crimsonInner, LavaCrimson, false);
        DrawIsoBand(lightInner, orangeInner, LavaOrange, false);
        if (lightWidth > 0) DrawIsoBand(whiteInner, lightInner, ReleaseRingLight, false);
        if (whiteWidth > 0) DrawIsoBand(edge, whiteInner, ReleaseRingWhite, false);
    }

    private void DrawReleaseAir()
    {
        // 납작하게 눌린 핵이 땅에 스며들며 잠깐 남는다.
        if (releaseElapsed < 0.06f)
        {
            float k = releaseElapsed / 0.06f;
            int rx = Mathf.RoundToInt(Mathf.Lerp(18f, 10f, k));
            int ry = Mathf.RoundToInt(Mathf.Lerp(3f, 1f, k));
            FillEllipse(centerX, centerY + 2, rx + 2, ry + 1, LavaOrange);
            FillEllipse(centerX, centerY + 2, rx, ry, LavaLight);
        }

        for (int i = 0; i < SparkCount; i++) DrawSpark(i);
    }

    private void DrawSpark(int _i)
    {
        float life = sparkLife[_i];
        float t = releaseElapsed;
        if (t >= life) return;

        float speed = sparkSpeed[_i];
        float up = sparkUp[_i];
        float dirX = sparkDirX[_i];
        float dirY = sparkDirY[_i];

        SparkPosition(t, speed, up, dirX, dirY, out int x1, out int y1);
        SparkPosition(Mathf.Max(0f, t - SparkStreakSeconds), speed, up, dirX, dirY, out int x0, out int y0);

        float age = t / life;
        Color32 body = age < 0.5f ? LavaOrange : LavaCrimson;
        painter.Line(x0, y0, x1, y1, 0, 0, 0, body);
        painter.Line(x0, y0 - 1, x1, y1 - 1, 0, 0, 0, body);
        painter.Rect(x1 - 1, y1 - 1, x1 + 1, y1 + 1, age < 0.4f ? LavaLight : LavaOrange);
    }

    private void SparkPosition(float _t, float _speed, float _up, float _dirX, float _dirY, out int _x, out int _y)
    {
        float travel = (1f - Mathf.Exp(-SparkDrag * _t)) / SparkDrag;
        float height = _up * travel - 0.5f * SparkGravity * _t * _t;
        float startOffset = 10f;
        _x = Mathf.RoundToInt(centerX + _dirX * (startOffset + _speed * travel));
        _y = Mathf.RoundToInt(centerY + _dirY * (startOffset + _speed * travel) + Mathf.Max(0f, height) + 2f);
    }

    // //도형 도우미

    // 바닥 평면 고리 띠(아이소 - 세로 절반). [_inner, _outer] 반지름(px) 사이를 채운다.
    private void DrawIsoBand(int _outer, int _inner, Color32 _color, bool _bChecker)
    {
        if (_outer <= 0) return;
        FillBand(centerX, centerY, _outer, Mathf.Max(1, Mathf.RoundToInt(_outer * 0.5f)), _inner, _inner * 0.5f, _color, _bChecker);
    }

    private void FillEllipse(int _cx, int _cy, int _rx, int _ry, Color32 _color)
    {
        FillBand(_cx, _cy, _rx, _ry, 0, 0f, _color, false);
    }

    /// <summary>
    /// 바깥 타원(_rx, _ry)과 안쪽 타원(_innerRx, _innerRy) 사이를 가로줄로 채운다(안쪽이 0 이하면 속이 찬 타원).
    /// _bChecker면 월드 픽셀 체커(x + y 짝수)만 찍는다. 행마다 가장자리를 PixelPainter.DitherBand와 같은 식으로 잡는다.
    /// </summary>
    private void FillBand(int _cx, int _cy, int _rx, int _ry, int _innerRx, float _innerRy, Color32 _color, bool _bChecker)
    {
        if (_rx <= 0 || _ry <= 0) return;

        for (int y = -_ry; y <= _ry; y++)
        {
            float ny = y / (_ry + 0.5f);
            float rest = 1f - ny * ny;
            if (rest < 0f) continue;

            int outer = Mathf.FloorToInt((_rx + 0.5f) * Mathf.Sqrt(rest));
            int inner = -1;
            if (0 < _innerRx && Mathf.Abs(y) < _innerRy)
            {
                float iny = y / (_innerRy + 0.5f);
                inner = Mathf.Max(0, Mathf.CeilToInt((_innerRx + 0.5f) * Mathf.Sqrt(Mathf.Max(0f, 1f - iny * iny))));
            }

            int py = _cy + y;
            if (_bChecker)
            {
                // 체커 칸(px + py 짝수)만 왼쪽부터 한 칸씩 건너 찍는다. 안쪽 타원 구간(-inner < x < inner)은 건너뛴다.
                if (inner <= 0)
                {
                    CheckerRow(_cx - outer, _cx + outer, py, _color);
                }
                else
                {
                    CheckerRow(_cx - outer, _cx - inner, py, _color);
                    CheckerRow(_cx + inner, _cx + outer, py, _color);
                }
                continue;
            }

            if (inner < 0) painter.HLine(_cx - outer, _cx + outer, py, _color);
            else if (inner <= outer)
            {
                painter.HLine(_cx - outer, _cx - inner, py, _color);
                painter.HLine(_cx + inner, _cx + outer, py, _color);
            }
        }
    }

    // [_x0, _x1] 중 체커 칸(x + _y 짝수)에만 왼쪽부터 점을 찍는다.
    private void CheckerRow(int _x0, int _x1, int _y, Color32 _color)
    {
        int x = 0 == ((_x0 + _y) & 1) ? _x0 : _x0 + 1;
        for (; x <= _x1; x += 2) painter.Dot(x, _y, _color);
    }

    // //공용

    // 재생마다 바뀌는 0 ~ 1 해시(게임 난수와 따로).
    private float Hash01(int _a, int _b)
    {
        uint h = seed ^ ((uint)_a * 0x9E3779B1u) ^ ((uint)_b * 0x85EBCA77u);
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        h *= 0x297A2D39u;
        h ^= h >> 15;
        return (h & 0xFFFFFFu) / 16777216f;
    }

    private MeshRenderer CreateLayer(string _name, string _sortingLayer, out Transform _transform, out Mesh _mesh)
    {
        GameObject child = new GameObject(_name);
        _transform = child.transform;
        _transform.SetParent(root.transform, false);

        _mesh = new Mesh { name = "QuakeCharge_" + _name };
        _mesh.MarkDynamic();
        child.AddComponent<MeshFilter>().sharedMesh = _mesh;

        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingLayerName = _sortingLayer;
        meshRenderer.sortingOrder = SortingOrder;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;
        return meshRenderer;
    }

    private static void DestroyObject(UnityEngine.Object _object)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(_object);
        else UnityEngine.Object.DestroyImmediate(_object);
    }
}
