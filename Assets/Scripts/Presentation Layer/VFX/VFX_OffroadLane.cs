using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 로그라이크 오프로드 이동 경로 표시. 오프로드가 소환되기 전 예열 동안 필드 끝의 소환 위치(수축 링 + 게이트 선 + 도는 표식)와
    /// 진행 방향을 가리키는 `&lt;&lt;&lt;&lt;` 화살표 펄스, 차선 가장자리 점선을 보여 준다. 예열이 끝나면 소환 섬광이 터지고
    /// 차량이 지나가는 속도에 맞춰 경로 표시가 소환 지점부터 걷힌다.
    ///
    /// 렌더링은 바닥 메쉬(경로, 마커)와 공중 메쉬(소환 섬광 기둥과 불꽃)로 나뉜다. 바닥 메쉬는 나무와 캐릭터(Objects 레이어)보다
    /// 아래 레이어(ShockWave)에 그려져 나무가 항상 경로 위에 보이고, 공중 메쉬는 루트 렌더러의 레이어에 그려진다.
    ///
    /// 경로는 쿼터뷰 바닥 위의 직선이다. 화살표와 차선 폭은 바닥 평면(세로를 2배로 편 좌표)에서 정의한 뒤 화면으로 투영해서(세로 0.5배)
    /// 아이소 대각선 경로에서도 화살표가 경로 방향을 정확히 가리킨다.
    ///
    /// 수명: Begin(출발점, 도착점, 예열 시간) 뒤 예열 + 걷힘이 끝나면 스스로 루트 ParticleSystem을 멈춰 풀에 반환한다.
    /// 픽셀 규칙: JAGGIES(타원은 midpoint 한 가지), DOUBLE PIXEL(선의 L자 모서리 제거), HUGGING(선 옆에 보조선을 겹치지 않음).
    /// 입자는 `.` `+` `x` `●` `◆`만 쓰고 매 프레임 경과 시간으로 다시 그린다. 정적 버퍼만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_OffroadLane : MonoBehaviour
    {
        [Header("경로")]
        [SerializeField, Tooltip("차선 반폭(월드 유닛). 돌진 피해 폭과 맞춘다. Begin의 인자로 덮어쓸 수 있다.")]
        private float laneHalfWidth = 0.4f;
        [SerializeField, Tooltip("출발점/도착점(차량 위치)에서 바닥 접지점까지 내려가는 높이(월드 유닛). 위치가 이미 바닥이면 0.")]
        private float groundOffset = 0.0f;
        [SerializeField, Tooltip("소환 섬광 뒤 경로 표시가 소환 지점부터 걷히는 데 걸리는 시간(초). 차량이 경로를 지나는 시간에 맞춘다.")]
        private float clearSeconds = 0.8f;

        [Header("정렬")]
        [SerializeField, Tooltip("공중 메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다")]
        private int sortingOrderOffset = 0;

        [Header("바닥 메쉬 (경로가 나무/캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("나무/캐릭터가 있는 Objects보다 아래여야 한다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        public const float GroundSquashY = 0.5f;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const float DefaultWarmSeconds = 1.0f;
        private const float RevealSeconds = 0.5f;
        private const float FlashSeconds = 0.35f;
        private const float FlashColumnSeconds = 0.1f;
        private const float EndLinger = 0.15f;
        private const int ChevronSpacing = 9;
        private const int ChevronDepth = 5;
        private const int ChevronStartOffset = 14;
        private const int WaveLength = 7;
        private const float WaveSpeed = 22.0f;
        private const int SparkCount = 14;
        private const int QuadBudget = 5000;
        private const float GroundGlow = 0.2f;

        // 경고 팔레트(앰버). 별 계열(하늘색)과 구분되는 따뜻한 색이다.
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorAmberDark = new Color32(122, 68, 18, 255);
        private static readonly Color32 ColorAmber = new Color32(217, 130, 43, 255);
        private static readonly Color32 ColorAmberLight = new Color32(255, 191, 77, 255);
        private static readonly Color32 ColorCream = new Color32(255, 240, 200, 255);

        private static readonly Color32[] ChevronColors =
        {
            GlowColor(ColorAmberDark, 0.05f), GlowColor(ColorAmber, 0.18f), GlowColor(ColorAmberLight, 0.3f), GlowColor(ColorCream, 0.4f),
        };

        private static readonly PixelQuadBuffer skyBuffer = new PixelQuadBuffer(QuadBudget);
        private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(QuadBudget);

        // 내부 의존성
        private readonly PixelPainter skyPainter = new PixelPainter();
        private readonly PixelPainter groundPainter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh groundMesh;
        private ParticleSystemRenderer rootParticleRenderer;
        private ParticleSystem rootParticleSystem;

        // 상태 변수
        private float elapsed;
        private float warmSeconds = DefaultWarmSeconds;
        private float halfWidthPx;
        private bool bActive;
        private bool bInitialized;
        private int startX;
        private int startY;
        private float groundDirX;
        private float groundDirY;
        private float groundLength;
        private int chevronCount;
        private Vector3 cullingCenter;
        private Vector3 cullingSize = new Vector3(20.0f, 20.0f, 10.0f);

        private int groundSortingLayerId;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;
        private ulong skyHash;
        private int skyVertexCount = -1;
        private ulong groundHash;
        private int groundVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 경로 표시를 시작한다. _from/_to는 차량의 출발점과 도착점(월드), _warmSeconds는 소환까지의 예열 시간이다.
        /// _halfWidthUnits가 0 이하이면 인스펙터의 laneHalfWidth를 쓴다. 풀에서 꺼내 활성화한 직후에 부른다.
        /// </summary>
        public void Begin(Vector3 _from, Vector3 _to, float _warmSeconds, float _halfWidthUnits = 0.0f)
        {
            EnsureInitialized();

            Vector3 from = _from + new Vector3(0.0f, -groundOffset, 0.0f);
            Vector3 to = _to + new Vector3(0.0f, -groundOffset, 0.0f);

            startX = Mathf.FloorToInt(from.x * PixelsPerUnit);
            startY = Mathf.FloorToInt(from.y * PixelsPerUnit);
            float dx = (to.x - from.x) * PixelsPerUnit;
            float dy = (to.y - from.y) * PixelsPerUnit / GroundSquashY;

            groundLength = Mathf.Sqrt(dx * dx + dy * dy);
            if (groundLength < 1.0f)
            {
                groundDirX = 1.0f;
                groundDirY = 0.0f;
            }
            else
            {
                groundDirX = dx / groundLength;
                groundDirY = dy / groundLength;
            }

            warmSeconds = 0.0f < _warmSeconds ? _warmSeconds : DefaultWarmSeconds;
            halfWidthPx = Mathf.Max(0.5f, 0.0f < _halfWidthUnits ? _halfWidthUnits : laneHalfWidth) * PixelsPerUnit;
            chevronCount = Mathf.Max(1, Mathf.FloorToInt((groundLength - ChevronStartOffset) / ChevronSpacing));

            // 경계: 출발점~도착점 + 차선 폭과 섬광 높이 여유
            Vector3 mid = (from + to) * 0.5f;
            float spanX = Mathf.Abs(to.x - from.x) + (halfWidthPx * 2.0f + 80.0f) / PixelsPerUnit;
            float spanY = Mathf.Abs(to.y - from.y) + (halfWidthPx + 100.0f) / PixelsPerUnit;
            cullingCenter = mid;
            cullingSize = new Vector3(spanX + 2.0f * PixelVfxCulling.MarginUnits, spanY + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);

            elapsed = 0.0f;
            bActive = true;
            skyVertexCount = -1;
            groundVertexCount = -1;
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
        }

        /// <summary>총 재생 시간(초): 예열 + 걷힘.</summary>
        public float TotalDuration => warmSeconds + clearSeconds + EndLinger;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;
            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;

            mesh = new Mesh { name = "VFX_OffroadLane" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 300);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = new Mesh { name = "VFX_OffroadLane_Ground" };
                groundMesh.MarkDynamic();
                groundMeshFilter.sharedMesh = groundMesh;
                PixelQuadBuffer.ReserveMesh(groundMesh, 1500);

                groundMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                groundMeshRenderer.receiveShadows = false;
                groundMeshRenderer.enabled = false;

                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }
        }

        // ---------- 계산 ----------

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

        // 바닥 평면 좌표(경로 거리 _s, 옆 방향 _lateral, 단위 px)를 화면 픽셀로 투영한다. 세로는 GroundSquashY배.
        private int ProjectX(float _s, float _lateral)
        {
            return startX + Mathf.RoundToInt(groundDirX * _s - groundDirY * _lateral);
        }

        private int ProjectY(float _s, float _lateral)
        {
            return startY + Mathf.RoundToInt((groundDirY * _s + groundDirX * _lateral) * GroundSquashY);
        }

        // ---------- 그리기 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            bool bSplit = null != groundMesh;

            skyPainter.Begin(skyBuffer, transform.worldToLocalMatrix, pos.z);

            PixelPainter groundTarget = skyPainter;
            if (true == bSplit)
            {
                groundPainter.Begin(groundBuffer, groundMeshRenderer.transform.worldToLocalMatrix, pos.z);
                groundTarget = groundPainter;
            }

            DrawGround(groundTarget);
            DrawFlash(skyPainter);

            skyPainter.Commit(mesh, meshRenderer, ref skyHash, ref skyVertexCount);
            if (true == bSplit) groundPainter.Commit(groundMesh, groundMeshRenderer, ref groundHash, ref groundVertexCount);
        }

        private void DrawGround(PixelPainter _p)
        {
            float clearT = Mathf.Clamp01((elapsed - warmSeconds) / Mathf.Max(0.05f, clearSeconds));
            float clearS = clearT * groundLength;

            DrawEdges(_p, clearS);
            DrawChevrons(_p, clearS);
            DrawMarker(_p);
        }

        // 차선 가장자리 점선 - 걷힌 구간은 그리지 않는다. 점선이 경로 방향으로 천천히 흐른다.
        private void DrawEdges(PixelPainter _p, float _clearS)
        {
            float reveal = Mathf.Clamp01(elapsed / RevealSeconds);
            float endS = groundLength * reveal;
            if (endS <= _clearS) return;

            int phase = Mathf.FloorToInt(elapsed * 30.0f);
            Color32 color = GlowColor(ColorAmberDark, 0.08f);

            for (int side = -1; side <= 1; side += 2)
            {
                float lateral = halfWidthPx * side;
                _p.Line(ProjectX(_clearS, lateral), ProjectY(_clearS, lateral), ProjectX(endS, lateral), ProjectY(endS, lateral), 3, 5, phase, color);
            }
        }

        // `<<<<` 화살표 - 소환 위치에서 경로 끝을 향해 늘어서고, 밝은 파동 두 줄이 소환 지점에서 끝으로 달려간다.
        private void DrawChevrons(PixelPainter _p, float _clearS)
        {
            float reveal = Mathf.Clamp01(elapsed / RevealSeconds);
            int revealCount = Mathf.CeilToInt(chevronCount * reveal);
            bool bSpawning = warmSeconds <= elapsed;
            int basePulse = 0 != (Mathf.FloorToInt(elapsed * 4.0f) & 1) ? 1 : 0;

            float cycle = chevronCount + 12.0f;
            float head1 = Mathf.Repeat(elapsed * WaveSpeed, cycle);
            float head2 = Mathf.Repeat(elapsed * WaveSpeed + cycle * 0.5f, cycle);

            for (int n = 0; n < revealCount; n++)
            {
                float s = ChevronStartOffset + n * ChevronSpacing;
                if (s < _clearS) continue;
                if (true == bSpawning && n < 4) continue;

                int level = basePulse;
                float behind1 = head1 - n;
                float behind2 = head2 - n;
                if (0.0f <= behind1 && behind1 < WaveLength) level = Mathf.Max(level, 3 - Mathf.FloorToInt(behind1 / 2.4f));
                if (0.0f <= behind2 && behind2 < WaveLength) level = Mathf.Max(level, 3 - Mathf.FloorToInt(behind2 / 2.4f));

                // 경로 끝 쪽 화살표는 점멸하며 사라지는 느낌을 낸다
                if (chevronCount - 4 < n && 0 != ((Mathf.FloorToInt(elapsed * 12.0f) + n) & 1)) level = Mathf.Max(0, level - 1);

                float halfWidth = halfWidthPx * 0.55f + (3 <= level ? 1.0f : 0.0f);
                float half = ChevronDepth * 0.5f;
                int tipX = ProjectX(s + half, 0.0f);
                int tipY = ProjectY(s + half, 0.0f);
                Color32 color = ChevronColors[Mathf.Clamp(level, 0, 3)];

                _p.Line(tipX, tipY, ProjectX(s - half, halfWidth), ProjectY(s - half, halfWidth), 0, 0, 0, color);
                _p.Line(tipX, tipY, ProjectX(s - half, -halfWidth), ProjectY(s - half, -halfWidth), 0, 0, 0, color);
            }
        }

        // 소환 위치 마커 - 수축하는 링이 소환이 임박했음을 알리고, 마지막 0.5초는 흰색으로 빠르게 깜빡인다.
        private void DrawMarker(PixelPainter _p)
        {
            float fade = elapsed < warmSeconds + FlashSeconds ? 1.0f : 1.0f - Mathf.Clamp01((elapsed - warmSeconds - FlashSeconds) / 0.3f);
            if (fade <= 0.0f) return;

            bool bBlink = warmSeconds - 0.5f < elapsed && elapsed < warmSeconds && 0 != (Mathf.FloorToInt(elapsed * 8.0f) & 1);
            Color32 ringColor = true == bBlink ? GlowColor(ColorWhite, 0.3f) : (0 != (Mathf.FloorToInt(elapsed * 6.0f) & 1) ? GlowColor(ColorCream, GroundGlow) : GlowColor(ColorAmberLight, GroundGlow));
            int phase = Mathf.FloorToInt(elapsed * 30.0f);

            _p.Ring(startX, startY, 17, 8, 2, 2, phase, ringColor);

            if (elapsed < warmSeconds)
            {
                float contract = Mathf.Repeat(elapsed * 1.25f, 1.0f);
                int radius = Mathf.RoundToInt(28.0f - 16.0f * contract);
                if (3 <= Mathf.Abs(radius - 17)) _p.Ring(startX, startY, radius, Mathf.Max(1, Mathf.RoundToInt(radius * GroundSquashY)), 1, 2, -Mathf.FloorToInt(elapsed * 20.0f), GlowColor(contract < 0.5f ? ColorAmberLight : ColorAmber, GroundGlow * 0.7f));
            }

            // 게이트 선: 소환 지점에서 차선 폭만큼 가로지르는 점선
            _p.Line(ProjectX(0.0f, -halfWidthPx), ProjectY(0.0f, -halfWidthPx), ProjectX(0.0f, halfWidthPx), ProjectY(0.0f, halfWidthPx), 2, 2, Mathf.FloorToInt(elapsed * 20.0f), GlowColor(ColorAmber, 0.15f));

            // 링을 도는 표식 4개와 중심 별
            for (int j = 0; j < 4; j++)
            {
                float angle = TwoPi * j / 4.0f + elapsed * 1.1f;
                int mx = startX + Mathf.RoundToInt(Mathf.Cos(angle) * 17.0f);
                int my = startY + Mathf.RoundToInt(-Mathf.Sin(angle) * 8.5f);
                _p.Shape(0 != (j & 1) ? PixelPainter.ShapePlus : PixelPainter.ShapeDiamond, mx, my, GlowColor(0 != (j & 1) ? ColorCream : ColorAmberLight, 0.25f));
            }

            _p.Shape(0 != (Mathf.FloorToInt(elapsed * 8.0f) & 1) ? PixelPainter.ShapeDiamond : PixelPainter.ShapePlus, startX, startY, GlowColor(true == bBlink ? ColorWhite : ColorCream, 0.3f));

            // 소환 순간 퍼지는 고리
            float flashT = (elapsed - warmSeconds) / FlashSeconds;
            if (0.0f <= flashT && flashT < 1.0f)
            {
                int radius = Mathf.RoundToInt(17.0f + 32.0f * EaseOutCubic(flashT));
                _p.Ring(startX, startY, radius, Mathf.Max(1, Mathf.RoundToInt(radius * GroundSquashY)), 2, 1, Mathf.FloorToInt(elapsed * 50.0f), GlowColor(flashT < 0.4f ? ColorWhite : (flashT < 0.7f ? ColorCream : ColorAmberLight), 0.3f * (1.0f - flashT * 0.6f)));
            }
        }

        // 소환 섬광(공중) - 아주 짧은 빛기둥과 사방으로 튀는 불꽃. 나무 위에서 보인다.
        private void DrawFlash(PixelPainter _p)
        {
            float since = elapsed - warmSeconds;
            if (0.0f > since || FlashSeconds + 0.2f <= since) return;

            if (since < FlashColumnSeconds)
            {
                int height = Mathf.RoundToInt(44.0f * (1.0f - since / FlashColumnSeconds * 0.6f));
                _p.Rect(startX - 1, startY, startX + 2, startY + height, GlowColor(ColorWhite, 0.3f));
                _p.Rect(startX - 3, startY, startX + 4, startY + 8, GlowColor(ColorAmberLight, 0.25f));
            }

            float u = since / (FlashSeconds + 0.2f);
            for (int k = 0; k < SparkCount; k++)
            {
                float angle = TwoPi * k / SparkCount + 0.3f;
                float speed = 30.0f + (k * 37) % 40;
                float distance = speed * (1.0f - Mathf.Exp(-4.0f * since)) / 4.0f;
                int x = startX + Mathf.RoundToInt(Mathf.Cos(angle) * distance);
                int y = startY + Mathf.RoundToInt(Mathf.Sin(angle) * distance * GroundSquashY + since * 30.0f * (1.0f + (k % 3) * 0.3f) - since * since * 60.0f);
                int shape = 0 == (k % 3) ? PixelPainter.ShapeDot : (1 == (k % 3) ? PixelPainter.ShapePlus : PixelPainter.ShapeCross);
                Color32 color = u < 0.4f ? GlowColor(ColorCream, 0.3f) : (u < 0.7f ? GlowColor(ColorAmberLight, 0.22f) : GlowColor(ColorAmber, 0.14f));
                _p.Shape(u < 0.75f ? shape : PixelPainter.ShapeDot, x, y, color);
            }
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            bActive = false;
            elapsed = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (false == bActive) return;

            elapsed += Time.deltaTime;

            if (TotalDuration <= elapsed)
            {
                bActive = false;
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;

                // 루트 ParticleSystem을 멈추면 VFXComponent가 풀에 반환한다
                if (null != rootParticleSystem && true == rootParticleSystem.isPlaying) rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 공중 메쉬는 루트 기준으로 / 바닥 메쉬는 자기 레이어로 다시 맞춘다
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

            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            if (false == PixelVfxCulling.IsVisible(cullingCenter, cullingSize))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;
                skyVertexCount = -1;
                groundVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            bActive = false;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }

        private void OnValidate()
        {
            laneHalfWidth = Mathf.Max(0.1f, laneHalfWidth);
            clearSeconds = Mathf.Max(0.1f, clearSeconds);
        }
    }
}
