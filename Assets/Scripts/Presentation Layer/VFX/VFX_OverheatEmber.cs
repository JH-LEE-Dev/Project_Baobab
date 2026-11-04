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
    public class VFX_OverheatEmber : MonoBehaviour
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

        // 판단 경계(px). 불덩이 + 궤도 + 위로 튀는 불티
        private const float CullWidthPixels = 48.0f;
        private const float CullHeightPixels = 64.0f;
        private const float CullCenterYPixels = 14.0f;

        private static readonly int[] TrailShapes = { PixelPainter.ShapeCross, PixelPainter.ShapePlus, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapePlus };
        private static readonly byte[] GlowRGB = { 150, 30, 10 };

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

        // 쿼드 버퍼는 인스턴스끼리 공유한다(메인 스레드 단일 실행, 그리는 동안에만 쓴다).
        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);
        private static readonly PixelQuadBuffer glowBuffer = new PixelQuadBuffer(GlowQuadBudget);

        // 내부 의존성
        private readonly OverheatEmberOrb orb = new OverheatEmberOrb();
        private readonly OverheatEmberParticles particles = new OverheatEmberParticles(ParticleCapacity);
        private readonly PixelPainter painter = new PixelPainter();
        private readonly PixelPainter glowPainter = new PixelPainter();
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
        private Vector3 cullSize;
        private int flyingSortingLayerID;
        private int glowSortingLayerID;

        // 상태 변수
        private bool bInitialized;
        private bool bHasPrevious;
        private bool bMoving;
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

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

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

                glowRenderer.sortingLayerID = glowSortingLayerID;
                glowRenderer.sortingOrder = glowSortingOrder;
                glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glowRenderer.receiveShadows = false;
                glowRenderer.enabled = false;
            }

            cullSize = new Vector3(CullWidthPixels / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits,
                CullHeightPixels / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);

            BuildTones();
        }

        private void BuildTones()
        {
            OverheatEmberOrb.BuildPalette(tones, darks, glowScale);

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
            float scale = null != groundAnchor ? groundAnchor.lossyScale.x : transform.lossyScale.x;
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

            if (true == bMoving && true == bHasPrevious) EmitTrail(segment);
            else if (0.9f < _scale) EmitIdle();

            particles.Step(StepInterval, time);

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
                float lift = Mathf.Sin(time * m.bobSpeed + m.bobPhase) * m.bobAmp;

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

        private void RebuildMesh(float _scale)
        {
            int cx = Mathf.RoundToInt(orbX);
            int cy = Mathf.RoundToInt(orbY);
            float pulse = OverheatEmberOrb.Pulse(time, phase);

            painter.Begin(buffer, transform.worldToLocalMatrix, transform.position.z);

            if (0.05f < _scale)
            {
                DrawMotes(false);
                orb.Draw(painter, cx, cy, time, phase, 1.0f, pulse, flare, _scale, tones, darks);
                DrawMotes(true);
            }

            particles.Draw(painter, tones, darks, smokeColor);
            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
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

        // 바닥 붉은 빛: 불씨 발밑 타원. 안쪽이 더 밝고 맥동·번쩍임에 따라 숨 쉰다.
        private void RebuildGlow()
        {
            if (null == glowRenderer) return;

            Vector3 anchor = null != groundAnchor ? groundAnchor.position : transform.position;
            int gx = Mathf.RoundToInt(anchor.x * PixelsPerUnit);
            int gy = Mathf.RoundToInt(anchor.y * PixelsPerUnit) - 1;
            float pulse = OverheatEmberOrb.Pulse(time, phase);
            float alpha = (0.3f + 0.3f * pulse + flare * 0.2f) * groundGlowStrength;

            glowPainter.Begin(glowBuffer, glowRenderer.transform.worldToLocalMatrix, glowRenderer.transform.position.z);

            if (false == bMoving && 0.0f < alpha)
            {
                bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;
                Color baseColor = new Color32(GlowRGB[0], GlowRGB[1], GlowRGB[2], 255);
                if (true == bLinear) baseColor = baseColor.linear;

                Color inner = baseColor;
                inner.a = Mathf.Clamp01(alpha);
                Color outer = baseColor;
                outer.a = Mathf.Clamp01(alpha * 0.6f);
                Color32 innerColor = inner;
                Color32 outerColor = outer;

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

            glowPainter.Commit(glowMesh, glowRenderer, ref glowHash, ref glowVertexCount);
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
                layer = null != sortingSource ? sortingSource.sortingLayerID : meshRenderer.sortingLayerID;
                order = (null != sortingSource ? sortingSource.sortingOrder : 0) + sortingOrderOffset;
            }

            if (layer == appliedSortingLayerID && order == appliedSortingOrder) return;

            appliedSortingLayerID = layer;
            appliedSortingOrder = order;
            meshRenderer.sortingLayerID = layer;
            meshRenderer.sortingOrder = order;
        }

        // 불씨는 루트 스케일(튀어나옴 0 -> 1, 흡입 막바지 0.35)과 Visual 스케일(착지 찌그러짐)을 바꾼다. 픽셀은 월드 격자에 그려야 하므로
        // 이 오브젝트의 월드 스케일을 1로 되돌린다(크기 변화는 AnchorScale로 직접 그린다). 부모 스케일이 0에 가까우면 false.
        private bool CancelParentScale()
        {
            Transform parent = transform.parent;
            if (null == parent) return true;

            Vector3 parentScale = parent.lossyScale;
            if (0.01f > Mathf.Abs(parentScale.x) || 0.01f > Mathf.Abs(parentScale.y)) return false;

            Vector3 wanted = new Vector3(1.0f / parentScale.x, 1.0f / parentScale.y, 1.0f);
            if (wanted != transform.localScale) transform.localScale = wanted;
            return true;
        }

        private void HideMeshes()
        {
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
            if (null != glowRenderer && true == glowRenderer.enabled) glowRenderer.enabled = false;
            meshVertexCount = -1;
            glowVertexCount = -1;
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

            // 화면 밖이면 계산과 그리기를 모두 멈춘다. 다시 들어오면 이전 위치 없이 이어서 시작한다(순간 이동을 이동으로 보지 않게).
            Vector3 pos = transform.position;
            if (false == PixelVfxCulling.IsVisible(pos + new Vector3(0.0f, CullCenterYPixels / PixelsPerUnit, 0.0f), cullSize))
            {
                HideMeshes();
                bHasPrevious = false;
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

            // 이번 프레임 이동을 스텝 수만큼 나눠, 스텝마다 그 시점 위치로 계산한다
            Vector3 stepFrom = true == bHasPrevious ? lastStepPosition : pos;
            int steps = 0;
            while (steps < pendingSteps)
            {
                accumulator -= StepInterval;
                steps++;
                StepOnce(scale, Vector3.Lerp(stepFrom, pos, steps / (float)pendingSteps));
            }

            if (StepInterval <= accumulator) accumulator = 0.0f;
            if (false == bHasPrevious)
            {
                StepOnce(scale, pos);
                steps++;
            }

            if (false == CancelParentScale())
            {
                HideMeshes();
                return;
            }

            ApplySorting();

            // 계산 스텝이 없던 프레임(60Hz보다 빠른 화면)에 제자리·같은 크기면 그림이 같으므로 다시 만들지 않는다.
            // 움직이는 중에는 메쉬가 로컬 좌표라 픽셀 격자에서 어긋나므로 항상 다시 그린다.
            if (0 < steps || -1 == meshVertexCount || pos != lastDrawPosition || scale != lastDrawScale)
            {
                RebuildMesh(scale);
                lastDrawPosition = pos;
                lastDrawScale = scale;
            }

            glowAccumulator += dt;
            if (1.0f / 15.0f <= glowAccumulator || -1 == glowVertexCount || true == bMoving)
            {
                glowAccumulator = 0.0f;
                RebuildGlow();
            }
        }

        private void OnDisable()
        {
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
