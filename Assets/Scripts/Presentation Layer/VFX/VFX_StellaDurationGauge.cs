using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 스텔라(별) 유지시간 게이지. 별 위에 뜨는 작은 푸른 막대(22x5px)로, 남은 수명 비율만큼 채워진 채 줄어든다.
    /// 막대 전체가 스프링으로 말랑하게 움직인다(젤리 연출).
    ///   등장  가로로 좁게 시작해 위로 튀어 오르며 스프링으로 출렁이다 안착하고, 별가루(`+` `.` `x` `◆`)가 터진다.
    ///   유지  1px 단위로 천천히 떠올랐다 내려앉고(호버), 채움 앞쪽의 젤리 머리가 줄어드는 동안 눌린다. 남은 비율 22% 이하는 흰색으로 점멸한다.
    ///   퇴장  End() 또는 수명 0. 먼저 위로 늘어나는 예비 동작(0.09초) 후 납작하게 눌리며 사라지고 입자(`+` `.` `x` `●` `◆`)가 튄다.
    ///         막대가 사라진 뒤 입자가 끝나면 스스로 루트 ParticleSystem을 멈춰 풀에 반환한다.
    ///
    /// 별 하나당 인스턴스 하나다. 재생 위치는 별의 바닥 위치(VFX_StellaBurst와 같은 규칙)이고, 막대는 gaugeHeightPx만큼 위에 그려진다.
    /// 막대 크기가 작아 늘이고 줄이는 연출은 정수 픽셀로 반올림한 크기를 최근접 표본으로 다시 그린다(번지지 않고 도트가 유지된다).
    ///
    /// 픽셀 규칙: HUGGING(막대 네 모서리 1px는 비워 둥글게), DOUBLE PIXEL(입자는 `PixelPainter` 모양만), 입자는 `.` `+` `x` `●` `◆`만 쓴다.
    /// 모든 그림은 매 프레임 Time.deltaTime으로 다시 계산한다(30fps 이상). 정적 버퍼만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_StellaDurationGauge : MonoBehaviour
    {
        [Header("위치 (재생 위치 = 별의 바닥 기준)")]
        [SerializeField, Tooltip("막대 아래 모서리가 바닥에서 뜬 높이(px). 별 그림 위로 오게 잡는다.")]
        private float gaugeHeightPx = 46.0f;

        [Header("소팅 (별 위에 보여야 한다)")]
        [SerializeField] private string sortingLayerName = "FlyingItem";
        [SerializeField] private int sortingOrder = 0;

        [Header("기본 수명 (Begin을 부르지 않았을 때)")]
        [SerializeField] private float defaultLifetime = 6.0f;

        private const float PixelsPerUnit = 32.0f;
        private const int GaugeWidth = 22;
        private const int GaugeHeight = 5;
        private const int InnerWidth = GaugeWidth - 2;
        private const int QuadBudget = 600;
        private const int ParticleCapacity = 28;
        private const float MaxStep = 0.05f;
        private const int SubSteps = 4;
        private const float SpringK = 230.0f;
        private const float SpringC = 10.5f;
        private const float ExitSpringK = 420.0f;
        private const float ExitSpringC = 26.0f;
        private const float AnticipationTime = 0.09f;
        private const float ExitTime = 0.34f;
        private const float FillTau = 0.08f;
        private const float HeadSpringK = 220.0f;
        private const float HeadSpringC = 14.0f;
        private const float LowRatio = 0.22f;
        private const float BlinkRate = 12.0f;
        private const float ParticleGravity = 40.0f;
        private const int SpawnParticleCount = 7;
        private const int ExitParticleCount = 10;

        private static readonly Color32 ColorFrame = new Color32(31, 42, 74, 255);
        private static readonly Color32 ColorEmpty = new Color32(44, 53, 96, 255);
        private static readonly Color32 ColorBody = new Color32(74, 168, 255, 255);
        private static readonly Color32 ColorHighlight = new Color32(166, 220, 255, 255);
        private static readonly Color32 ColorShade = new Color32(42, 111, 208, 255);
        private static readonly Color32 ColorHead = new Color32(124, 196, 255, 255);
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorSparkBright = new Color32(223, 243, 255, 255);
        private static readonly Color32 ColorSparkDim = new Color32(143, 208, 255, 255);

        private static readonly int[] SpawnShapes = { PixelPainter.ShapePlus, PixelPainter.ShapeDot, PixelPainter.ShapeCross, PixelPainter.ShapeDiamond };
        private static readonly int[] ExitShapes = { PixelPainter.ShapePlus, PixelPainter.ShapeDot, PixelPainter.ShapeCross, PixelPainter.ShapeCircle, PixelPainter.ShapeDiamond };

        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(QuadBudget);

        private enum EGaugeState
        {
            Appearing,
            Live,
            Anticipating,
            Collapsing,
            Finished
        }

        private struct Particle
        {
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float life;
            public int shape;
        }

        // 내부 의존성
        private readonly PixelPainter painter = new PixelPainter();
        private readonly Particle[] particles = new Particle[ParticleCapacity];
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystem rootParticleSystem;

        // 상태 변수
        private EGaugeState gaugeState;
        private float elapsed;
        private float stateElapsed;
        private float totalLifetime;
        private float remaining;
        private float scaleX;
        private float scaleY;
        private float velocityX;
        private float velocityY;
        private float fillShown;
        private float headLength;
        private float headVelocity;
        private float hoverPhase;
        private int sortingLayerId;
        private int particleCount;
        private bool bInitialized;
        private ulong contentHash;
        private int contentVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 이번 별의 총 수명(초)으로 게이지를 시작한다. 풀에서 꺼낸 직후 부른다. 0 이하이면 defaultLifetime.
        /// </summary>
        public void Begin(float _lifetimeSeconds)
        {
            EnsureInitialized();

            totalLifetime = Mathf.Max(0.1f, 0.0f < _lifetimeSeconds ? _lifetimeSeconds : defaultLifetime);
            remaining = totalLifetime;
            fillShown = 1.0f;
        }

        /// <summary>
        /// 남은 수명(초)을 맞춘다. 수명이 늘어나는 효과가 붙어 남은 시간이 총 수명을 넘으면 총 수명도 함께 늘려 채움이 가득 찬 채 시작한다.
        /// 부르지 않아도 게이지가 스스로 줄어든다. 매 프레임 불러도 된다.
        /// </summary>
        public void SetRemaining(float _remainingSeconds)
        {
            remaining = Mathf.Max(0.0f, _remainingSeconds);
            if (totalLifetime < remaining) totalLifetime = remaining;
        }

        /// <summary>퇴장 연출을 시작한다(별이 터지거나 사라질 때). 이미 퇴장 중이면 아무 일도 하지 않는다.</summary>
        public void End()
        {
            if (EGaugeState.Anticipating == gaugeState || EGaugeState.Collapsing == gaugeState || EGaugeState.Finished == gaugeState) return;

            SetState(EGaugeState.Anticipating);
        }

        /// <summary>바닥 소팅 순서(별 위에 그릴 때 별의 소팅 순서보다 큰 값).</summary>
        public void SetSortingOrder(int _order)
        {
            sortingOrder = _order;
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;

            mesh = new Mesh { name = "VFX_StellaDurationGauge" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 200);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            sortingLayerId = SortingLayer.NameToID(sortingLayerName);
        }

        // ---------- 연출 진행 ----------

        private void SetState(EGaugeState _state)
        {
            gaugeState = _state;
            stateElapsed = 0.0f;

            if (EGaugeState.Collapsing == _state)
            {
                velocityY = -8.0f;
                SpawnParticles(ExitParticleCount, ExitShapes, 18.0f, 26.0f, 14.0f, 0.45f, 0.3f);
            }
        }

        private void Step(float _dt)
        {
            elapsed += _dt;
            stateElapsed += _dt;
            hoverPhase += _dt;

            float targetY = 1.0f;
            float targetX = 1.0f;
            float springK = SpringK;
            float springC = SpringC;

            switch (gaugeState)
            {
                case EGaugeState.Appearing:
                    if (0.75f < stateElapsed) SetState(EGaugeState.Live);
                    break;

                case EGaugeState.Live:
                    {
                        float breath = Mathf.Sin(elapsed * 3.2f) * 0.04f;
                        targetX = 1.0f - breath * 1.8f;
                        targetY = 1.0f + breath;
                    }
                    break;

                case EGaugeState.Anticipating:
                    targetY = 1.28f;
                    targetX = 0.8f;
                    if (AnticipationTime < stateElapsed) SetState(EGaugeState.Collapsing);
                    break;

                case EGaugeState.Collapsing:
                    targetY = 0.0f;
                    targetX = 1.5f;
                    springK = ExitSpringK;
                    springC = ExitSpringC;
                    break;
            }

            // 수명은 등장과 유지 중에만 줄어든다. 퇴장에 들어가면 채움은 그 자리에서 멈춘다.
            if (EGaugeState.Appearing == gaugeState || EGaugeState.Live == gaugeState)
            {
                remaining = Mathf.Max(0.0f, remaining - _dt);
                if (remaining <= 0.0f) SetState(EGaugeState.Anticipating);
            }

            float subDt = _dt / SubSteps;

            for (int i = 0; i < SubSteps; i++)
            {
                velocityY += (springK * (targetY - scaleY) - springC * velocityY) * subDt;
                scaleY += velocityY * subDt;
                velocityX += (springK * (targetX - scaleX) - springC * velocityX) * subDt;
                scaleX += velocityX * subDt;
            }

            if (EGaugeState.Collapsing == gaugeState)
            {
                scaleY = Mathf.Max(0.0f, scaleY);
                scaleX = Mathf.Max(0.0f, scaleX);
            }

            float fillVelocity = 0.0f;

            if (EGaugeState.Appearing == gaugeState || EGaugeState.Live == gaugeState)
            {
                float targetFill = Mathf.Clamp01(remaining / totalLifetime);
                float previous = fillShown;
                fillShown += (targetFill - fillShown) * (1.0f - Mathf.Exp(-_dt / FillTau));
                fillVelocity = (fillShown - previous) / Mathf.Max(0.0001f, _dt);
            }

            // 젤리 머리: 줄어드는 동안 눌리고, 평소에는 호흡한다.
            float drain = Mathf.Min(1.0f, Mathf.Max(0.0f, -fillVelocity) * 40.0f);
            float headTarget = 1.8f - 0.7f * drain + Mathf.Sin(elapsed * 5.0f) * 0.25f;

            for (int i = 0; i < SubSteps; i++)
            {
                headVelocity += (HeadSpringK * (headTarget - headLength) - HeadSpringC * headVelocity) * subDt;
                headLength += headVelocity * subDt;
            }

            headLength = Mathf.Clamp(headLength, 0.2f, 3.4f);

            StepParticles(_dt);

            if (EGaugeState.Collapsing == gaugeState && ExitTime < stateElapsed && 0 == particleCount)
            {
                SetState(EGaugeState.Finished);
            }
        }

        private void SpawnParticles(int _count, int[] _shapes, float _speedMin, float _speedRange, float _lift, float _lifeMin, float _lifeRange)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ParticleCapacity <= particleCount) return;

                float angle = Random.value * Mathf.PI * 2.0f;
                float speed = _speedMin + Random.value * _speedRange;

                Particle p = new Particle();
                p.x = 0.0f;
                p.y = 0.0f;
                p.vx = Mathf.Cos(angle) * speed;
                p.vy = Mathf.Sin(angle) * speed + _lift;
                p.life = _lifeMin + Random.value * _lifeRange;
                p.shape = _shapes[i % _shapes.Length];
                particles[particleCount] = p;
                particleCount++;
            }
        }

        private void StepParticles(float _dt)
        {
            for (int i = particleCount - 1; 0 <= i; i--)
            {
                Particle p = particles[i];
                p.life -= _dt;

                if (p.life <= 0.0f)
                {
                    particleCount--;
                    particles[i] = particles[particleCount];
                    continue;
                }

                p.x += p.vx * _dt;
                p.y += p.vy * _dt;
                p.vy -= ParticleGravity * _dt;
                particles[i] = p;
            }
        }

        // ---------- 그리기 ----------

        private static Color32 GlowColor(Color32 _base, float _glow)
        {
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255.0f);
            return new Color32(_base.r, _base.g, _base.b, alpha);
        }

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            int centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            int centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            painter.Begin(quadBuffer, transform.worldToLocalMatrix, pos.z);

            // 호버: 1px 단위로 천천히 떠올랐다 내려앉는다(정수 픽셀).
            int hover = Mathf.RoundToInt(Mathf.Sin(hoverPhase * 2.6f) * 0.8f);
            int baseY = centerY + Mathf.RoundToInt(gaugeHeightPx) + hover;

            if (EGaugeState.Finished != gaugeState) DrawGauge(centerX, baseY);

            // 입자는 게이지 중심(막대 가운데)에서 터진다.
            int particleOriginY = centerY + Mathf.RoundToInt(gaugeHeightPx) + GaugeHeight / 2;

            for (int i = 0; i < particleCount; i++)
            {
                Particle p = particles[i];
                Color32 color = 0.2f < p.life ? GlowColor(ColorSparkBright, 0.45f) : GlowColor(ColorSparkDim, 0.3f);
                painter.Shape(p.shape, centerX + Mathf.RoundToInt(p.x), particleOriginY + Mathf.RoundToInt(p.y), color);
            }

            painter.Commit(mesh, meshRenderer, ref contentHash, ref contentVertexCount);
        }

        /// <summary>
        /// 막대를 (스프링 배율을 반올림한) 정수 크기로 최근접 표본 추출해 그린다. 아래 가운데가 기준점이다.
        /// 원본 격자 22x5: 바깥 1px 테두리(네 모서리는 비움), 안쪽 3줄(윗줄 하이라이트, 가운데 몸통, 아랫줄 그림자).
        /// </summary>
        private void DrawGauge(int _centerX, int _baseY)
        {
            int widthPx = Mathf.RoundToInt(GaugeWidth * scaleX);
            int heightPx = Mathf.RoundToInt(GaugeHeight * scaleY);
            if (widthPx < 1 || heightPx < 1) return;

            int left = _centerX - widthPx / 2;
            int fillPx = Mathf.RoundToInt(InnerWidth * fillShown);
            if (0 == fillPx && 0.02f < fillShown) fillPx = 1;

            int headPx = Mathf.RoundToInt(headLength);
            bool bFlash = fillShown < LowRatio && 0 == (Mathf.FloorToInt(elapsed * BlinkRate) & 1);

            for (int dy = 0; dy < heightPx; dy++)
            {
                int srcRow = Mathf.Min(GaugeHeight - 1, dy * GaugeHeight / heightPx);
                int y = _baseY + (heightPx - 1 - dy);

                for (int dx = 0; dx < widthPx; dx++)
                {
                    int srcCol = Mathf.Min(GaugeWidth - 1, dx * GaugeWidth / widthPx);
                    bool bEdgeX = 0 == srcCol || GaugeWidth - 1 == srcCol;
                    bool bEdgeY = 0 == srcRow || GaugeHeight - 1 == srcRow;
                    Color32 color;

                    if (true == bEdgeX || true == bEdgeY)
                    {
                        // 네 모서리는 비워 둔다(둥근 윤곽)
                        if (true == bEdgeX && true == bEdgeY) continue;

                        color = GlowColor(ColorFrame, 0.0f);
                    }
                    else
                    {
                        int fillColumn = srcCol - 1;

                        if (fillColumn < fillPx)
                        {
                            if (1 == srcRow) color = GlowColor(bFlash ? ColorWhite : ColorHighlight, bFlash ? 0.3f : 0.1f);
                            else if (GaugeHeight - 2 == srcRow) color = GlowColor(bFlash ? ColorHighlight : ColorShade, 0.05f);
                            else color = GlowColor(bFlash ? ColorWhite : ColorBody, bFlash ? 0.3f : 0.08f);
                        }
                        else if (fillColumn < fillPx + headPx && 0 < fillPx && 1 < srcRow)
                        {
                            color = GlowColor(ColorHead, 0.2f);
                        }
                        else
                        {
                            color = GlowColor(ColorEmpty, 0.0f);
                        }
                    }

                    painter.Dot(left + dx, y, color);
                }
            }
        }

        private void ResetState()
        {
            gaugeState = EGaugeState.Appearing;
            elapsed = 0.0f;
            stateElapsed = 0.0f;
            scaleX = 0.2f;
            scaleY = 0.0f;
            velocityX = 3.0f;
            velocityY = 9.0f;
            fillShown = 1.0f;
            headLength = 1.6f;
            headVelocity = 0.0f;
            hoverPhase = Random.value * 6.2832f;
            particleCount = 0;
            contentVertexCount = -1;

            totalLifetime = Mathf.Max(0.1f, defaultLifetime);
            remaining = totalLifetime;
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ResetState();

            meshRenderer.enabled = false;

            // 등장 순간의 별가루
            SpawnParticles(SpawnParticleCount, SpawnShapes, 14.0f, 22.0f, 12.0f, 0.5f, 0.35f);
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxStep);
            Step(dt);

            if (EGaugeState.Finished == gaugeState)
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;

                // 루트 ParticleSystem을 멈추면 VFXComponent가 풀에 반환한다
                if (null != rootParticleSystem && true == rootParticleSystem.isPlaying) rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            // VFXComponent가 재생할 때 자식 렌더러 전체에 같은 소팅을 덮어쓰므로, 별 위 레이어로 다시 맞춘다
            if (meshRenderer.sortingLayerID != sortingLayerId) meshRenderer.sortingLayerID = sortingLayerId;
            if (meshRenderer.sortingOrder != sortingOrder) meshRenderer.sortingOrder = sortingOrder;

            // 막대 + 입자가 닿는 범위(px)로 화면 안/밖을 판단한다
            float reachX = 40.0f;
            float reachY = gaugeHeightPx + 40.0f;
            Vector3 center = transform.position + new Vector3(0.0f, reachY * 0.5f / PixelsPerUnit, 0.0f);
            Vector3 size = new Vector3(reachX * 2.0f / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, reachY / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);

            if (false == PixelVfxCulling.IsVisible(center, size))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                contentVertexCount = -1;
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
            gaugeHeightPx = Mathf.Max(8.0f, gaugeHeightPx);
            defaultLifetime = Mathf.Max(0.5f, defaultLifetime);
        }
    }
}
