using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 스텔라(별) 폭발 범위 표시. 별이 필드에 있는 동안 별 발치의 바닥에 은은한 점선 링이 깔리고, 링을 따라 밝은 구간과
    /// 별 표식 6개(◆ ● + x)가 천천히 돈다. 모든 그림이 바닥 메쉬 하나라서 나무와 캐릭터(Objects 레이어)보다 아래(ShockWave)에 그려진다.
    ///
    /// 수명 주기: 풀에서 꺼내면 링이 안쪽에서 펼쳐지며 나타나고(0.3초), 별이 사라질 때까지 유지된다(루트 ParticleSystem 수명이 상한).
    /// Activate()는 폭발 직전의 밝은 강조(0.35초), End()는 링이 깜빡이며 사라진 뒤 스스로 풀에 반환한다.
    /// 반지름은 SetRadius로 재생 중에도 바꿀 수 있다(범위 증가 카드 반영).
    ///
    /// 픽셀 규칙: JAGGIES(타원은 midpoint 한 가지), DOUBLE PIXEL(L자 모서리 제거), HUGGING(바깥 보조 링은 3px 띄움).
    /// 입자는 `.` `+` `x` `●` `◆`만 쓰고 매 프레임 경과 시간으로 다시 그린다. 정적 버퍼만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_StellaRange : MonoBehaviour
    {
        [Header("범위 (월드 유닛, 32 PPU 기준 1유닛 = 32px)")]
        [SerializeField, Tooltip("링의 가로 반지름. 재생 직전/재생 중 SetRadius로 덮어쓸 수 있다.")]
        private float maxRadius = 3.0f;

        [Header("바닥 소팅 (나무/캐릭터가 있는 Objects보다 아래여야 한다)")]
        [SerializeField] private string sortingLayerName = "ShockWave";
        [SerializeField] private int sortingOrder = 0;

        public const float GroundSquashY = 0.5f;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const float FadeInTime = 0.3f;
        private const float ActivateTime = 0.35f;
        private const float FadeOutTime = 0.3f;
        private const int OrbitMarkCount = 6;
        private const int QuadBudget = 4000;
        private const float RingGlow = 0.28f;

        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(191, 230, 255, 255);
        private static readonly Color32 ColorIce2 = new Color32(140, 196, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(91, 146, 230, 255);
        private static readonly Color32 ColorBlue2 = new Color32(52, 86, 158, 255);
        private static readonly Color32 ColorBlue3 = new Color32(31, 51, 102, 255);
        private static readonly Color32 ColorGold = new Color32(255, 212, 92, 255);

        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(QuadBudget);

        // 내부 의존성
        private readonly PixelPainter painter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystem rootParticleSystem;

        // 상태 변수
        private float elapsed;
        private float activateRemain;
        private float fadeOutElapsed;
        private bool bEnding;
        private bool bInitialized;
        private float overrideRadius;
        private float currentRadius;
        private int sortingLayerId;
        private ulong contentHash;
        private int contentVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>링의 가로 반지름(월드 유닛). 0 이하이면 maxRadius. 재생 중에 불러도 다음 프레임부터 반영된다.</summary>
        public void SetRadius(float _radiusUnits)
        {
            overrideRadius = _radiusUnits;
            currentRadius = Mathf.Max(0.5f, 0.0f < _radiusUnits ? _radiusUnits : maxRadius);
        }

        /// <summary>폭발 직전의 밝은 강조를 켠다(0.35초). 폭발 연출(VFX_StellaBurst) 시작 직전에 부른다.</summary>
        public void Activate()
        {
            activateRemain = ActivateTime;
        }

        /// <summary>링이 깜빡이며 사라지게 하고, 끝나면 스스로 풀에 반환한다. 이미 끝나는 중이면 아무 일도 하지 않는다.</summary>
        public void End()
        {
            if (true == bEnding) return;

            bEnding = true;
            fadeOutElapsed = 0.0f;
        }

        public float CurrentRadius => 0.0f < currentRadius ? currentRadius : maxRadius;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;

            mesh = new Mesh { name = "VFX_StellaRange" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 1500);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            sortingLayerId = SortingLayer.NameToID(sortingLayerName);
        }

        // ---------- 그리기 ----------

        private static Color32 GlowColor(Color32 _base, float _glow)
        {
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255.0f);
            return new Color32(_base.r, _base.g, _base.b, alpha);
        }

        private static float EaseOutCubic(float _t)
        {
            float inv = 1.0f - _t;
            return 1.0f - inv * inv * inv;
        }

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            int centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            int centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            painter.Begin(quadBuffer, transform.worldToLocalMatrix, pos.z);

            // 밝기 단계: 0 숨김 / 1 어두움(사라지는 중) / 2 보통 / 3 강조(폭발 직전)
            int level = 2;
            if (0.0f < activateRemain) level = 3;

            float radiusScale = 1.0f;
            if (elapsed < FadeInTime) radiusScale = Mathf.Lerp(0.55f, 1.0f, EaseOutCubic(elapsed / FadeInTime));

            if (true == bEnding)
            {
                float outT = fadeOutElapsed / FadeOutTime;
                level = 1;
                radiusScale = 1.0f + 0.06f * outT;

                // 후반에는 프레임을 건너뛰며 점멸하다가 사라진다
                if (0.5f < outT && 0 != (Mathf.FloorToInt(elapsed * 30.0f) & 1)) level = 0;
            }

            if (0 < level) DrawRange(centerX, centerY, level, radiusScale);

            painter.Commit(mesh, meshRenderer, ref contentHash, ref contentVertexCount);
        }

        private void DrawRange(int _cx, int _cy, int _level, float _radiusScale)
        {
            int rx = Mathf.Max(2, Mathf.RoundToInt(CurrentRadius * PixelsPerUnit * _radiusScale));
            int ry = Mathf.Max(1, Mathf.RoundToInt(rx * GroundSquashY));
            bool bPulse = 0.0f < Mathf.Sin(elapsed * 5.2f);

            // 바깥 가장자리 띠에 성긴 도트를 깔아 범위의 면을 은은하게 알린다
            Color32 fieldColor = 1 == _level ? GlowColor(ColorBlue3, 0.0f) : (2 == _level ? GlowColor(bPulse ? ColorBlue2 : ColorBlue3, 0.05f) : GlowColor(ColorBlue, 0.1f));
            painter.LatticeBand(_cx, _cy, rx - 5, 12, GroundSquashY, 5, fieldColor);

            // 점선 링 + 링을 따라 도는 밝은 구간
            int phase = Mathf.FloorToInt(elapsed * 12.0f);
            float head = Mathf.Repeat(elapsed * 0.9f / TwoPi, 1.0f);
            if (1 == _level)
            {
                painter.Ring(_cx, _cy, rx, ry, 3, 3, phase, GlowColor(ColorBlue3, RingGlow * 0.6f));
            }
            else if (3 == _level)
            {
                painter.RingWithHead(_cx, _cy, rx, ry, 3, 3, phase, head, GlowColor(ColorWhite, 0.35f), GlowColor(ColorWhite, 0.3f), GlowColor(ColorIce, RingGlow), GlowColor(ColorIce, RingGlow));
            }
            else
            {
                painter.RingWithHead(_cx, _cy, rx, ry, 3, 3, phase, head, GlowColor(ColorWhite, 0.3f), GlowColor(ColorIce, RingGlow), GlowColor(ColorIce2, RingGlow * 0.6f), GlowColor(bPulse ? ColorBlue : ColorBlue2, RingGlow * 0.3f));
            }

            // 바깥 보조 링(3px 띄움)
            int outerRx = rx + 4;
            painter.Ring(_cx, _cy, outerRx, Mathf.Max(1, Mathf.RoundToInt(outerRx * GroundSquashY)), 1, 7, -phase, GlowColor(1 == _level ? ColorBlue3 : ColorBlue2, 0.05f));

            if (3 == _level)
            {
                // 안쪽에서 바깥으로 퍼지는 강조 고리
                float u = Mathf.Repeat(elapsed * 3.0f, 1.0f);
                int innerRx = Mathf.RoundToInt(rx * u);
                if (2 <= innerRx && 3 <= rx - innerRx) painter.Ring(_cx, _cy, innerRx, Mathf.Max(1, Mathf.RoundToInt(innerRx * GroundSquashY)), 2, 1, phase * 3, GlowColor(ColorWhite, 0.3f));
            }

            if (_level < 2) return;

            // 링을 도는 별 표식 6개: ◆ ● 와 + x 가 번갈아 반짝이고 점 두 개의 꼬리를 끈다
            for (int i = 0; i < OrbitMarkCount; i++)
            {
                float angle = TwoPi * i / OrbitMarkCount + elapsed * 0.5f;
                int mx = _cx + Mathf.RoundToInt(Mathf.Cos(angle) * rx);
                int my = _cy + Mathf.RoundToInt(-Mathf.Sin(angle) * ry);
                bool bTwinkle = 0 != ((Mathf.FloorToInt(elapsed * 8.0f) + i) & 1);

                for (int j = 2; j >= 1; j--)
                {
                    float trail = angle - 0.13f * j;
                    painter.Dot(_cx + Mathf.RoundToInt(Mathf.Cos(trail) * rx), _cy + Mathf.RoundToInt(-Mathf.Sin(trail) * ry), GlowColor(1 == j ? ColorIce : ColorBlue, 0.45f));
                }

                if (0 == (i & 1)) painter.Shape(bTwinkle ? PixelPainter.ShapeDiamond : PixelPainter.ShapeCircle, mx, my, GlowColor(ColorGold, 0.28f));
                else painter.Shape(bTwinkle ? PixelPainter.ShapePlus : PixelPainter.ShapeCross, mx, my, GlowColor(ColorWhite, 0.3f));
            }
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            activateRemain = 0.0f;
            fadeOutElapsed = 0.0f;
            bEnding = false;
            contentVertexCount = -1;
            currentRadius = Mathf.Max(0.5f, 0.0f < overrideRadius ? overrideRadius : maxRadius);

            meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            if (0.0f < activateRemain) activateRemain -= dt;

            if (true == bEnding)
            {
                fadeOutElapsed += dt;
                if (FadeOutTime <= fadeOutElapsed)
                {
                    if (true == meshRenderer.enabled) meshRenderer.enabled = false;

                    // 루트 ParticleSystem을 멈추면 VFXComponent가 풀에 반환한다
                    if (null != rootParticleSystem && true == rootParticleSystem.isPlaying) rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    return;
                }
            }

            // VFXComponent가 재생할 때 자식 렌더러 전체에 같은 소팅을 덮어쓰므로, 바닥 레이어로 다시 맞춘다
            if (meshRenderer.sortingLayerID != sortingLayerId) meshRenderer.sortingLayerID = sortingLayerId;
            if (meshRenderer.sortingOrder != sortingOrder) meshRenderer.sortingOrder = sortingOrder;

            float reachPx = CurrentRadius * PixelsPerUnit * 1.2f + 16.0f;
            Vector3 size = new Vector3(reachPx * 2.0f / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, reachPx * GroundSquashY * 2.0f / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position, size))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                contentVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            overrideRadius = 0.0f;

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
            maxRadius = Mathf.Max(0.5f, maxRadius);
        }

        private void OnDrawGizmosSelected()
        {
            float radius = true == Application.isPlaying && 0.0f < currentRadius ? currentRadius : maxRadius;
            Vector3 center = transform.position;

            Gizmos.color = new Color(0.4f, 0.9f, 1.0f, 0.9f);

            const int Segments = 48;
            Vector3 previous = center + new Vector3(radius, 0.0f, 0.0f);
            for (int i = 1; i <= Segments; i++)
            {
                float angle = TwoPi * i / Segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * GroundSquashY, 0.0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
