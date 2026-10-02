using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 과열 상태로 던진 부메랑 전용 "푸른 열기 혜성" 이펙트. 32 PPU 정수 픽셀 쿼드 메쉬를 매 프레임 직접 그린다(VFX_OverheatAura와 같은 방식).
    /// 부메랑이 풀로 회수돼 비활성화돼도 꼬리와 그을림이 끝까지 남아야 하므로 BoomerangAfterimage처럼 부메랑의 자식이 아닌 독립 루트 오브젝트로 만든다.
    ///
    /// 구성
    /// 부메랑은 납작한 V자 칼날이 비스듬히 도는 회전면(가로로 긴 타원)이므로, 모든 방출은 이 타원 기준이고 진행 반대 방향(뒤쪽)에서만 일어난다.
    /// 부메랑 위치에 붙어 같이 움직이거나 도는 요소는 없다. 모든 요소는 월드에 방출되어 지나간 자리에 남는다.
    /// 1. 꼬리 입자: 이동 경로를 보간하며 타원 뒤쪽 중심에 여러 크기(`.`, 반지름 1~6px)의 원 덩어리 입자를 남긴다. 위로 떠오르며 일렁이고 흰색 -> 하늘색 -> 파랑 -> 진파랑 -> 보라로 식다가 점이 빠지며 사라진다.
    /// 2. 열기 불씨: `.`, 작은 원, 대시 불씨가 바깥으로 튀며 바람장에 휘날린다.
    /// 바닥 타일(용암, 물 등)은 종류가 다양하므로 바닥에 남는 자국은 만들지 않는다.
    ///
    /// 크기 대응: 입자 하나의 크기는 고정(반지름 최대 6px)이고, 부메랑 반경(R)에 비례해 방출 개수가 늘어난다.
    /// 최적화: 정적 공유 PixelQuadBuffer, 구조체 배열(GC 0), 해시 업로드 생략, 실제 그린 범위로 좁힌 메쉬 경계, PixelVfxCulling 화면 밖 생략,
    /// 동시 4개 이상이면 저부하 모드(방출 개수 절반, 몸통 30fps). 화면 캡처를 쓰지 않으므로 SortingLayerTextureGate와 무관하다.
    ///
    /// 반투명은 쓰지 않는다. 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로만 낸다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)] // Boomerang.LateUpdate(Feed 호출)가 끝난 뒤에 실행한다
    public class VFX_BoomerangHeatTrail : MonoBehaviour
    {
        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / PixelsPerUnit;
        private const float TwoPi = Mathf.PI * 2.0f;

        private const int PuffCapacity = 400;
        private const int EmberCapacity = 80;
        private const int BodyQuadCapacity = 900;
        private const int LowLoadCount = 4;           // 동시 활성 수가 이 이상이면 저부하 모드
        private const int MaxCircleRadius = 6;        // 원 덩어리 입자의 최대 반지름(px)
        private const int AlwaysVisibleFrames = 3;    // 시작 직후 몇 프레임은 화면 안으로 본다
        private const int MaxEmitPerFrame = 8;

        private const float ClusterRate = 40.0f;      // 초당 방출 묶음 수
        private const float EmberRate = 28.0f;        // 초당 불씨 수(반경 64px 기준)
        private const float PuffBuoyancy = 34.0f;     // 덩어리가 위로 떠오르는 가속(px/s^2)
        private const float PuffWind = 85.0f;         // 바람장 세기(px/s^2)
        private const float EllipseY = 0.42f;         // 회전면 타원의 세로/가로 비율(스프라이트의 비스듬한 회전면과 같다)
        private const float TeleportJumpPx = 400.0f;  // 한 프레임에 이만큼 튀면 이동으로 보지 않는다

        // 톤 인덱스. 불꽃은 파랑 계열(과열 아우라와 같은 팔레트), 그을음은 발광이 없다.
        private const int ToneWhite = 0;
        private const int ToneIce = 1;
        private const int ToneCyan = 2;
        private const int ToneBlue = 3;
        private const int ToneDeep = 4;
        private const int ToneViolet = 5;
        private const int ToneCount = 6;

        private enum TrailState
        {
            Idle,
            Flying,
            Fading,
        }

        // 꼬리 입자. radius 0: `.`(1px), 1~6: 반지름 radius px의 채워진 원. 크기는 반경과 무관하게 고정이고 개수로 커 보이게 한다.
        private struct Puff
        {
            public bool bActive;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
            public int radius;
            public int seed;
            public bool bCyan;
        }

        // 불씨. kind 0: 점, 1: 작은 원(반지름 1~2px), 2: 대시
        private struct Ember
        {
            public bool bActive;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
            public int kind;
            public int seed;
            public int tone;
        }

        // 이펙트 종류별 공유 버퍼. 메인 스레드에서 한 번에 한 인스턴스만 채우고 올린다.
        private static readonly PixelQuadBuffer bodyBuffer = new PixelQuadBuffer(BodyQuadCapacity);
        private static readonly Color32[] toneColors = BuildToneColors();
        private static readonly int[][] circleHalfWidth = BuildCircleTable();
        private static int activeCount;

        //외부 의존성
        private Material material;

        //내부 의존성
        private MeshRenderer bodyRenderer;
        private Mesh bodyMesh;
        private readonly Puff[] puffs = new Puff[PuffCapacity];
        private readonly Ember[] embers = new Ember[EmberCapacity];

        //상태 변수
        private TrailState state;
        private bool bInitialized;
        private bool bPaused;
        private bool bFed;
        private bool bHeadValid;
        private bool bHasLast;
        private Vector3 feedPosition;
        private int feedSortingLayerID;
        private int feedSortingOrder;
                private float radiusPx = 16.0f;
        private float lastX;
        private float lastY;
        private float dirX = 1.0f;
        private float dirY;
        private float speedPx;
        private float elapsed;
        private float emitAccumulator;
        private float emberAccumulator;
        private int beginFrame;
        private int puffCursor;
        private int emberCursor;
        private uint randomState;
        private int appliedBodyLayerID = int.MinValue;
        private int appliedBodyOrder = int.MinValue;
        private int uploadedBodyVertexCount = -1;
        private ulong uploadedBodyHash;
        private bool bRangeValid;
        private float rangeMinX;
        private float rangeMinY;
        private float rangeMaxX;
        private float rangeMaxY;
        private bool bHasLiveParticles;

        /// <summary>
        /// 독립 루트 오브젝트로 이펙트를 만든다. 부메랑의 Awake에서 호출하고 DontDestroyOnLoad는 호출 측이 건다.
        /// </summary>
        public static VFX_BoomerangHeatTrail Create(Material _material)
        {
            GameObject go = new GameObject("BoomerangHeatTrail");
            VFX_BoomerangHeatTrail trail = go.AddComponent<VFX_BoomerangHeatTrail>();
            trail.Initialize(_material);
            return trail;
        }

        private void Initialize(Material _material)
        {
            if (true == bInitialized) return;
            bInitialized = true;

            material = _material;
            randomState = unchecked((uint)GetHashCode() * 2654435761u) | 1u;

            bodyRenderer = CreateMeshChild("HeatTrail_Body", out bodyMesh);
            PixelQuadBuffer.ReserveMesh(bodyMesh, BodyQuadCapacity);

            // 소팅은 Feed가 알려주는 부메랑 소팅을 따라간다.

            enabled = false;
        }

        private MeshRenderer CreateMeshChild(string _name, out Mesh _mesh)
        {
            GameObject child = new GameObject(_name);
            child.transform.SetParent(transform, false);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "VFX_BoomerangHeatTrail_" + _name };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;

            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
            return meshRenderer;
        }

        // ---------- 외부 제어 ----------

        /// <summary>
        /// 비행 시작. 직전 비행의 잔여 입자는 지우지 않고 이어서 소멸시킨다.
        /// </summary>
        public void Begin()
        {
            if (TrailState.Idle == state)
            {
                activeCount++;
                uploadedBodyVertexCount = -1;
            }

            state = TrailState.Flying;
            enabled = true;
            bPaused = false;
            bFed = false;
            bHeadValid = false;
            bHasLast = false;
            speedPx = 0.0f;
            dirX = 1.0f;
            dirY = 0.0f;
            emitAccumulator = 0.0f;
            emberAccumulator = 0.0f;
            beginFrame = Time.frameCount;
            appliedBodyLayerID = int.MinValue;
            appliedBodyOrder = int.MinValue;
            randomState ^= unchecked((uint)Time.frameCount * 2654435761u) | 1u;
        }

        /// <summary>
        /// 이번 프레임 부메랑의 최종 위치와 정렬값, 판정 반경(유닛)을 알린다. 부메랑의 LateUpdate에서 호출한다.
        /// </summary>
        public void Feed(Vector3 _position, int _sortingLayerID, int _sortingOrder, float _radiusUnits)
        {
            if (TrailState.Flying != state) return;

            bFed = true;
            feedPosition = _position;
            feedSortingLayerID = _sortingLayerID;
            feedSortingOrder = _sortingOrder;
            radiusPx = Mathf.Max(4.0f, _radiusUnits * PixelsPerUnit);
        }

        public void SetPaused(bool _bPaused)
        {
            bPaused = _bPaused;
        }

        /// <summary>
        /// 방출을 멈춘다. 남은 입자와 그을림은 끝까지 소멸한 뒤 스스로 꺼진다.
        /// </summary>
        public void End()
        {
            if (TrailState.Flying != state) return;

            state = TrailState.Fading;
            bHeadValid = false;
        }

        /// <summary>
        /// 남은 입자까지 즉시 지운다(강제 회수).
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < PuffCapacity; i++) puffs[i].bActive = false;
            for (int i = 0; i < EmberCapacity; i++) embers[i].bActive = false;

            bHeadValid = false;
            if (TrailState.Idle != state)
            {
                state = TrailState.Idle;
                activeCount = Mathf.Max(0, activeCount - 1);
            }

            HideRenderers();
            enabled = false;
        }

        private void HideRenderers()
        {
            if (null != bodyRenderer) bodyRenderer.enabled = false;
            uploadedBodyVertexCount = -1;
        }

        // ---------- 난수 / 노이즈 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // 정수 해시 기반 0~1(프레임/칸에 고정된 값이 필요한 곳용)
        private static float Hash01(int _a, int _b)
        {
            uint h = unchecked((uint)(_a * 73856093) ^ (uint)(_b * 19349663));
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535.0f;
        }

        private static float ValueNoise(float _x, float _y)
        {
            int ix = Mathf.FloorToInt(_x);
            int iy = Mathf.FloorToInt(_y);
            float fx = _x - ix;
            float fy = _y - iy;
            fx = fx * fx * (3.0f - 2.0f * fx);
            fy = fy * fy * (3.0f - 2.0f * fy);

            float a = Hash01(ix, iy);
            float b = Hash01(ix + 1, iy);
            float c = Hash01(ix, iy + 1);
            float d = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ---------- 정적 테이블 ----------

        private static byte GlowByte(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255.0f);
        }

        // 과열 아우라와 같은 파랑 계열. HDR 배율이 곱해져 한 채널만 1을 넘지 않도록 파랑/보라는 어둡게 잡았다. 알파 = 발광 세기.
        private static Color32[] BuildToneColors()
        {
            Color32[] colors = new Color32[ToneCount];
            colors[ToneWhite] = new Color32(255, 255, 255, GlowByte(1.0f));
            colors[ToneIce] = new Color32(120, 196, 255, GlowByte(0.85f));
            colors[ToneCyan] = new Color32(30, 235, 215, GlowByte(0.9f));
            colors[ToneBlue] = new Color32(14, 64, 200, GlowByte(0.6f));
            colors[ToneDeep] = new Color32(12, 34, 150, GlowByte(0.38f));
            colors[ToneViolet] = new Color32(51, 23, 158, GlowByte(0.25f));
            return colors;
        }

        // 반지름 0~MaxCircleRadius 원의 행별 절반 폭(dy = 0..r)
        private static int[][] BuildCircleTable()
        {
            int[][] table = new int[MaxCircleRadius + 1][];
            for (int r = 0; r <= MaxCircleRadius; r++)
            {
                table[r] = new int[r + 1];
                float outer = (r + 0.5f) * (r + 0.5f);
                for (int dy = 0; dy <= r; dy++)
                {
                    table[r][dy] = Mathf.FloorToInt(Mathf.Sqrt(outer - dy * dy));
                }
            }

            return table;
        }

        // 불꽃이 식는 순서: 흰색 -> 하늘색 -> 파랑 -> 진파랑 -> 보라
        private static int FlameTone(float _u)
        {
            if (_u < 0.03f) return ToneWhite;
            if (_u < 0.12f) return ToneIce;
            if (_u < 0.50f) return ToneBlue;
            if (_u < 0.80f) return ToneDeep;
            return ToneViolet;
        }

        private static Color32 Tint(int _tone, float _glow)
        {
            Color32 c = toneColors[_tone];
            return new Color32(c.r, c.g, c.b, GlowByte(_glow));
        }

        // ---------- 갱신 ----------

        // 머리 위치/속도/방향을 갱신하고 이번 프레임 방출(꼬리 덩어리, 불씨, 그을림)을 한다.
        private void UpdateHead(float _dt, bool _bLowLoad)
        {
            float cx = feedPosition.x * PixelsPerUnit;
            float cy = feedPosition.y * PixelsPerUnit;

            if (false == bHasLast)
            {
                lastX = cx;
                lastY = cy;
                bHasLast = true;
            }

            float dx = cx - lastX;
            float dy = cy - lastY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (TeleportJumpPx < dist)
            {
                lastX = cx;
                lastY = cy;
                dx = 0.0f;
                dy = 0.0f;
                dist = 0.0f;
            }

            bHeadValid = true;

            if (0.0001f < _dt)
            {
                speedPx = Mathf.Lerp(speedPx, dist / _dt, 1.0f - Mathf.Exp(-12.0f * _dt));
            }

            if (0.5f < dist)
            {
                float blend = 1.0f - Mathf.Exp(-14.0f * Mathf.Max(_dt, 0.0001f));
                float nx = Mathf.Lerp(dirX, dx / dist, blend);
                float ny = Mathf.Lerp(dirY, dy / dist, blend);
                float len = Mathf.Sqrt(nx * nx + ny * ny);
                if (0.0001f < len)
                {
                    dirX = nx / len;
                    dirY = ny / len;
                }
            }

            if (0.0f < _dt)
            {
                EmitPuffs(cx, cy, _dt, _bLowLoad);
                EmitEmbers(cx, cy, _dt, _bLowLoad);
            }

            lastX = cx;
            lastY = cy;
        }

        // 입자 개수만 반경에 비례해 늘린다(크기는 고정). 한 프레임 안에서는 지난 위치와 현재 위치 사이를 보간해 고속 이동에도 끊기지 않는다.
        private void EmitPuffs(float _cx, float _cy, float _dt, bool _bLowLoad)
        {
            emitAccumulator += _dt * ClusterRate;
            int clusters = Mathf.Min((int)emitAccumulator, MaxEmitPerFrame);
            emitAccumulator -= (int)emitAccumulator;
            if (0 >= clusters) return;

            int count = Mathf.Clamp(Mathf.RoundToInt(radiusPx / 8.0f), 2, 12);
            if (true == _bLowLoad) count = Mathf.Max(1, count / 2);

            for (int c = 0; c < clusters; c++)
            {
                float t = (c + 1.0f) / clusters;
                float centerX = Mathf.Lerp(lastX, _cx, t);
                float centerY = Mathf.Lerp(lastY, _cy, t);

                for (int j = 0; j < count; j++)
                {
                    // 회전면 타원 안에서 뒤쪽 반쪽(진행 반대 방향)에만 생긴다. 앞쪽 점은 중심 반대편으로 뒤집는다.
                    float angle = Rand01() * TwoPi;
                    float spread = 0.05f + 0.5f * Mathf.Sqrt(Rand01());
                    float offX = Mathf.Cos(angle) * spread * radiusPx;
                    float offY = Mathf.Sin(angle) * spread * radiusPx * EllipseY;
                    if (0.0f < offX * dirX + offY * dirY)
                    {
                        offX = -offX;
                        offY = -offY;
                    }

                    float outward = Mathf.Lerp(3.0f, 14.0f, Rand01());
                    float offLen = Mathf.Max(1.0f, Mathf.Sqrt(offX * offX + offY * offY));

                    Puff puff = new Puff();
                    puff.bActive = true;
                    // 머리 쪽에 몰리는 밀도 구배: 뒤로 밀리는 거리는 rand^2로 분포시키고, 멀리 밀린 입자일수록 수명을 짧게 한다.
                    float behind = Rand01() * Rand01();
                    puff.x = centerX + offX - dirX * radiusPx * 0.45f * behind;
                    puff.y = centerY + offY - dirY * radiusPx * 0.45f * behind;
                    puff.vx = offX / offLen * outward - dirX * speedPx * 0.02f;
                    puff.vy = offY / offLen * outward + Mathf.Lerp(14.0f, 36.0f, Rand01());
                    puff.age = 0.0f;
                    puff.life = Mathf.Lerp(0.45f, 0.9f, Rand01()) * (1.0f - 0.35f * behind);
                    puff.seed = (int)(Rand01() * 100000.0f);
                    puff.bCyan = Rand01() < 0.05f;

                    float roll = Rand01();
                    // 여러 가지 크기: 작은 것이 많고 큰 것은 드물다
                    puff.radius = roll < 0.25f ? 0 : (roll < 0.5f ? 1 : (roll < 0.72f ? 2 : (roll < 0.87f ? 3 : (roll < 0.96f ? 4 : (roll < 0.99f ? 5 : 6)))));

                    puffs[puffCursor] = puff;
                    puffCursor = (puffCursor + 1) % PuffCapacity;
                }
            }
        }

        private void EmitEmbers(float _cx, float _cy, float _dt, bool _bLowLoad)
        {
            float rate = EmberRate * Mathf.Clamp(radiusPx / 64.0f, 0.7f, 2.0f) * (true == _bLowLoad ? 0.5f : 1.0f);
            emberAccumulator += _dt * rate;
            int count = Mathf.Min((int)emberAccumulator, 6);
            emberAccumulator -= (int)emberAccumulator;

            for (int i = 0; i < count; i++)
            {
                float t = Rand01();
                // 회전면 타원 테두리 근처의 뒤쪽 반쪽에서만 튄다
                float angle = Rand01() * TwoPi;
                float reach = Mathf.Lerp(0.5f, 1.0f, Rand01());
                float offX = Mathf.Cos(angle) * reach * radiusPx;
                float offY = Mathf.Sin(angle) * reach * radiusPx * EllipseY;
                if (0.0f < offX * dirX + offY * dirY)
                {
                    offX = -offX;
                    offY = -offY;
                }

                float speed = Mathf.Lerp(12.0f, 40.0f, Rand01());
                float offLen = Mathf.Max(1.0f, Mathf.Sqrt(offX * offX + offY * offY));

                Ember ember = new Ember();
                ember.bActive = true;
                ember.x = Mathf.Lerp(lastX, _cx, t) + offX;
                ember.y = Mathf.Lerp(lastY, _cy, t) + offY;
                ember.vx = offX / offLen * speed - dirX * speedPx * 0.02f;
                ember.vy = offY / offLen * speed + Mathf.Lerp(8.0f, 28.0f, Rand01());
                ember.age = 0.0f;
                ember.life = Mathf.Lerp(0.45f, 1.0f, Rand01());
                ember.seed = (int)(Rand01() * 1000.0f);

                float kindRoll = Rand01();
                ember.kind = kindRoll < 0.5f ? 0 : (kindRoll < 0.8f ? 1 : 2);

                float toneRoll = Rand01();
                ember.tone = toneRoll < 0.08f ? ToneCyan : (toneRoll < 0.3f ? ToneViolet : (toneRoll < 0.65f ? ToneIce : (toneRoll < 0.93f ? ToneBlue : ToneWhite)));

                embers[emberCursor] = ember;
                emberCursor = (emberCursor + 1) % EmberCapacity;
            }
        }

        private void ExpandRange(float _x, float _y, float _margin)
        {
            if (false == bRangeValid)
            {
                bRangeValid = true;
                rangeMinX = _x - _margin;
                rangeMaxX = _x + _margin;
                rangeMinY = _y - _margin;
                rangeMaxY = _y + _margin;
                return;
            }

            if (_x - _margin < rangeMinX) rangeMinX = _x - _margin;
            if (_x + _margin > rangeMaxX) rangeMaxX = _x + _margin;
            if (_y - _margin < rangeMinY) rangeMinY = _y - _margin;
            if (_y + _margin > rangeMaxY) rangeMaxY = _y + _margin;
        }

        // 입자 시뮬레이션. 화면 밖이어도 위치는 계속 갱신하고, 화면 밖 판단용 범위(px)를 함께 모은다.
        private void UpdateParticles(float _dt)
        {
            bHasLiveParticles = false;
            bRangeValid = false;

            if (true == bHeadValid)
            {
                ExpandRange(lastX, lastY, radiusPx * 1.7f + 8.0f);
            }

            float damp = Mathf.Max(0.0f, 1.0f - 2.2f * _dt);

            for (int i = 0; i < PuffCapacity; i++)
            {
                if (false == puffs[i].bActive) continue;

                puffs[i].age += _dt;
                if (puffs[i].age >= puffs[i].life)
                {
                    puffs[i].bActive = false;
                    continue;
                }

                float flow = ValueNoise(puffs[i].x * 0.045f, puffs[i].y * 0.045f - elapsed * 1.6f) - 0.5f;
                puffs[i].vx = puffs[i].vx * damp + flow * PuffWind * _dt;
                puffs[i].vy = puffs[i].vy * damp + PuffBuoyancy * _dt;
                puffs[i].x += puffs[i].vx * _dt;
                puffs[i].y += puffs[i].vy * _dt;

                bHasLiveParticles = true;
                ExpandRange(puffs[i].x, puffs[i].y, 8.0f);
            }

            float emberDamp = Mathf.Max(0.0f, 1.0f - 0.9f * _dt);
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                embers[i].age += _dt;
                if (embers[i].age >= embers[i].life)
                {
                    embers[i].bActive = false;
                    continue;
                }

                float flowX = ValueNoise(embers[i].x * 0.03f, embers[i].y * 0.03f - elapsed * 1.2f) - 0.5f;
                float flowY = ValueNoise(embers[i].x * 0.03f + 41.0f, embers[i].y * 0.03f - elapsed * 1.2f) - 0.5f;
                embers[i].vx = embers[i].vx * emberDamp + flowX * 2.0f * PuffWind * _dt;
                embers[i].vy = embers[i].vy * emberDamp + (flowY * 2.0f * PuffWind * 0.6f + PuffBuoyancy) * _dt;
                embers[i].x += embers[i].vx * _dt;
                embers[i].y += embers[i].vy * _dt;

                bHasLiveParticles = true;
                ExpandRange(embers[i].x, embers[i].y, 4.0f);
            }

        }

        private bool IsRangeVisible()
        {
            if (false == bRangeValid) return false;

            float widthUnits = (rangeMaxX - rangeMinX) * PixelUnit + 2.0f;
            float heightUnits = (rangeMaxY - rangeMinY) * PixelUnit + 2.0f;
            Vector3 center = new Vector3((rangeMinX + rangeMaxX) * 0.5f * PixelUnit, (rangeMinY + rangeMaxY) * 0.5f * PixelUnit, 0.0f);
            return PixelVfxCulling.IsVisible(center, new Vector3(widthUnits, heightUnits, 10.0f));
        }

        // ---------- 그리기 ----------

        private static void AddRect(PixelQuadBuffer _buffer, int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            _buffer.AddLocalRect(_x0, _y0, _x1, _y1, _color);
        }

        private void RebuildBody()
        {
            // 몸통은 부메랑 본체 바로 뒤(잔상과 같은 order - 1)에 깔린다. 소팅은 값이 바뀔 때만 쓴다.
            int bodyOrder = feedSortingOrder - 1;
            if (feedSortingLayerID != appliedBodyLayerID || bodyOrder != appliedBodyOrder)
            {
                appliedBodyLayerID = feedSortingLayerID;
                appliedBodyOrder = bodyOrder;
                bodyRenderer.sortingLayerID = feedSortingLayerID;
                bodyRenderer.sortingOrder = bodyOrder;
            }

            bodyBuffer.Clear();

            DrawPuffs();
            DrawEmbers();

            UploadBuffer(bodyBuffer, bodyMesh, bodyRenderer, ref uploadedBodyVertexCount, ref uploadedBodyHash);
        }

        // 정점 수와 내용 해시가 직전과 같으면 올리지 않는다. 메쉬 경계는 실제 그린 픽셀 범위(+1px)로 좁게 잡는다.
        private static void UploadBuffer(PixelQuadBuffer _buffer, Mesh _mesh, MeshRenderer _renderer, ref int _uploadedVertexCount, ref ulong _uploadedHash)
        {
            if (0 == _buffer.VertexCount)
            {
                if (0 != _uploadedVertexCount)
                {
                    _buffer.Upload(_mesh, 0);
                    _uploadedVertexCount = 0;
                    _uploadedHash = _buffer.ContentHash;
                }

                if (true == _renderer.enabled) _renderer.enabled = false;
                return;
            }

            if (_buffer.VertexCount != _uploadedVertexCount || _buffer.ContentHash != _uploadedHash)
            {
                _buffer.Upload(_mesh, 0);

                Vector2 min;
                Vector2 max;
                if (true == _buffer.TryGetLocalBounds(out min, out max))
                {
                    Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0.0f);
                    Vector3 size = new Vector3(max.x - min.x + 2.0f * PixelUnit, max.y - min.y + 2.0f * PixelUnit, 1.0f);
                    _mesh.bounds = new Bounds(center, size);
                }

                _uploadedVertexCount = _buffer.VertexCount;
                _uploadedHash = _buffer.ContentHash;
            }

            if (false == _renderer.enabled) _renderer.enabled = true;
        }

        // 꼬리 입자: 여러 크기(`.`, 반지름 1~6px)의 채워진 원. 수명에 따라 색이 식고(하늘색 -> 파랑 -> 진파랑 -> 보라),
        // 후반에는 반지름이 줄고 행이 빠지며 흩어지다 점멸하며 사라진다.
        private void DrawPuffs()
        {
            for (int i = 0; i < PuffCapacity; i++)
            {
                if (false == puffs[i].bActive) continue;

                float u = puffs[i].age / puffs[i].life;
                if (0.75f < u && 0 != ((int)(puffs[i].age * 30.0f) & 1)) continue;

                int tone = true == puffs[i].bCyan && u < 0.5f ? ToneCyan : FlameTone(u);
                Color32 color = toneColors[tone];
                int px = Mathf.RoundToInt(puffs[i].x);
                int py = Mathf.RoundToInt(puffs[i].y);

                int rg = Mathf.Min(MaxCircleRadius, Mathf.RoundToInt(puffs[i].radius * (1.0f - 0.4f * u)));
                if (0 >= rg)
                {
                    AddRect(bodyBuffer, px, py, px + 1, py + 1, color);
                    continue;
                }

                DrawCircle(px, py, rg, u, puffs[i].seed, tone, color);
            }
        }

        // 채워진 원(반지름 rg px). 수명 후반에는 행이 통째로 빠지거나 끝이 한 칸씩 깎여 흩어진다. 갓 생긴 큰 원은 한 단계 밝은 코어를 얹는다.
        private static void DrawCircle(int _px, int _py, int _rg, float _u, int _seed, int _tone, Color32 _color)
        {
            int[] table = circleHalfWidth[_rg];
            float dissolve = Mathf.Clamp01((_u - 0.4f) / 0.6f);

            for (int dy = -_rg; dy <= _rg; dy++)
            {
                int hw = table[dy < 0 ? -dy : dy];

                if (0.0f < dissolve)
                {
                    if (Hash01(_seed + dy * 7, 3) < dissolve * 0.8f) continue;
                    if (Hash01(_seed + dy * 13, 5) < dissolve) hw -= 1;
                    if (0 > hw) continue;
                }

                AddRect(bodyBuffer, _px - hw, _py + dy, _px + hw + 1, _py + dy + 1, _color);
            }

            if (3 <= _rg && _u < 0.2f)
            {
                Color32 core = toneColors[Mathf.Max(0, _tone - 1)];
                AddRect(bodyBuffer, _px - 1, _py, _px + 1, _py + 2, core);
            }
        }

        // 불씨: 후반에는 `.`로 작아졌다 점멸하며 소멸한다. kind 1은 작은 원(반지름 1~2px), kind 2는 대시.
        private void DrawEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                float u = embers[i].age / embers[i].life;
                if (0.75f < u && 0 != ((int)(embers[i].age * 30.0f) & 1)) continue;

                int px = Mathf.RoundToInt(embers[i].x);
                int py = Mathf.RoundToInt(embers[i].y);

                int twinkle = ((int)(embers[i].age * 14.0f) + embers[i].seed) % 3;
                float glow = (0 == twinkle ? 0.5f : (1 == twinkle ? 0.7f : 0.9f)) * (1.0f - u * 0.4f);
                int tone = 0.8f < u ? ToneDeep : embers[i].tone;
                Color32 color = Tint(tone, glow);

                if (1 == embers[i].kind && u < 0.55f)
                {
                    DrawCircle(px, py, 1 + (embers[i].seed & 1), u, embers[i].seed, tone, color);
                }
                else if (2 == embers[i].kind && u < 0.55f)
                {
                    if (0 == (embers[i].seed & 1)) AddRect(bodyBuffer, px - 1, py, px + 2, py + 1, color);
                    else AddRect(bodyBuffer, px, py - 1, px + 1, py + 2, color);
                }
                else
                {
                    AddRect(bodyBuffer, px, py, px + 1, py + 1, color);
                }
            }
        }

        // ---------- 유니티 이벤트 ----------

        private void LateUpdate()
        {
            if (TrailState.Idle == state) return;

            float dt = true == bPaused ? 0.0f : Time.deltaTime;
            bool bHasFeed = bFed;
            bFed = false;
            elapsed += dt;

            bool bLowLoad = LowLoadCount <= activeCount;

            if (TrailState.Flying == state && true == bHasFeed)
            {
                UpdateHead(dt, bLowLoad);
            }

            UpdateParticles(dt);

            // 방출이 멈추고 남은 입자가 모두 사라졌으면 꺼진다
            if (TrailState.Fading == state && false == bHasLiveParticles)
            {
                state = TrailState.Idle;
                activeCount = Mathf.Max(0, activeCount - 1);
                HideRenderers();
                enabled = false;
                return;
            }

            bool bVisible = Time.frameCount - beginFrame < AlwaysVisibleFrames || true == IsRangeVisible();
            if (false == bVisible) return;

            // 저부하 모드에서는 몸통을 격 프레임으로만 다시 만든다(30fps)
            if (false == bLowLoad || 0 == (Time.frameCount & 1))
            {
                RebuildBody();
            }
        }

        private void OnDestroy()
        {
            if (null != bodyMesh) Destroy(bodyMesh);

            if (TrailState.Idle != state)
            {
                state = TrailState.Idle;
                activeCount = Mathf.Max(0, activeCount - 1);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeCount = 0;
        }
    }
}
