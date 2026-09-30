using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 드론 레이저(전기 스파크 빔)에 맞은 자리의 피격 이펙트. 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬로 그린다(VFX_ChargeVortex, VFX_OverheatAura와 같은 방식).
    /// VFX_DroneLaserHit 프리팹의 자식으로 붙어서 풀에서 꺼내져 활성화될 때(OnEnable) 재생을 시작하고, 끝나면 스스로 렌더러를 끈다(풀 반환은 루트 ParticleSystem이 맡는다).
    ///
    /// 구성(4겹, 약 0.55초)
    /// 1. 중심 충격: 중심에서 X자 대각선과 십자 광선이 뻗었다 줄어들고, 밝은 원판이 번쩍이며, 점선 충격파 고리가 퍼진다.
    /// 2. 찌리릿 전기: 중심에서 방사형으로 뻗는 지그재그 번개(가지 포함). 모양은 boltFps로 다시 뽑고(방향과 길이도 프레임마다 크게 흔들린다), 가닥마다 확률로 켜졌다 꺼지며
    ///    점멸하고 프레임 전체가 잠깐 꺼지기도 한다. 가닥은 각도와 무관한 무작위 순서로 사라지고, 후반에는 마디가 확률로 빠져 끊긴 조각이 흩어지듯 사라진다.
    /// 3. 스파크 산란: 사방으로 튀는 스파크. 감속과 중력이 붙고, 속도에 비례한 짧은 선으로 그려지며 후반에 깜빡이며 사라진다.
    /// 4. 잔불: 중심 주변에서 `.`/`+` 모양의 작은 불씨가 천천히 떠올랐다 사라진다.
    ///
    /// 색은 상태마다 딱 3가지(진한 색, 중간 색, 밝은 색)만 쓴다. 평소는 노랑 계열, 과열(SetOverheat)은 파랑 계열이다(드론 레이저 색과 같은 계열).
    /// 밝기 차이는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로 낸다. 위치는 매 프레임 계산해서 정수 픽셀에 스냅한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_LaserHit : MonoBehaviour
    {
        [Header("렌더링")]
        [SerializeField] private Material hitMaterial;
        [SerializeField, Tooltip("루트 렌더러 기준 상대 소팅 오더")] private int sortingOffset = 0;

        [Header("시간")]
        [SerializeField, Tooltip("이펙트 전체 길이(초)")] private float totalDuration = 0.55f;

        [Header("중심 충격")]
        [SerializeField, Tooltip("충격 광선이 나타나 사라지기까지의 시간(초)")] private float flashDuration = 0.12f;
        [SerializeField, Tooltip("충격 광선(십자, X자)의 최대 길이(px)")] private float rayMaxLengthPx = 16.0f;
        [SerializeField, Tooltip("중앙 원판의 최대 반지름(px)")] private float discRadiusPx = 4.0f;
        [SerializeField, Tooltip("점선 충격파가 퍼지는 최대 반지름(px)")] private float ringRadiusPx = 20.0f;

        [Header("찌리릿 전기")]
        [SerializeField, Tooltip("번개가 지속되는 시간(초)")] private float boltDuration = 0.36f;
        [SerializeField, Tooltip("번개 모양을 다시 뽑는 속도(fps). 낮을수록 뚝뚝 튄다")] private float boltFps = 30.0f;
        [SerializeField, Range(2, 9), Tooltip("처음에 뻗는 번개 가닥 수")] private int boltCount = 9;
        [SerializeField, Tooltip("번개 길이의 최솟값(px)")] private float boltLengthMinPx = 16.0f;
        [SerializeField, Tooltip("번개 길이의 최댓값(px)")] private float boltLengthMaxPx = 32.0f;
        [SerializeField, Tooltip("번개가 지그재그로 흔들리는 폭(px)")] private float boltJitterPx = 4.5f;

        [Header("스파크")]
        [SerializeField, Range(0, 32), Tooltip("튀는 스파크 개수의 최솟값")] private int sparkCountMin = 16;
        [SerializeField, Range(0, 32), Tooltip("튀는 스파크 개수의 최댓값")] private int sparkCountMax = 20;
        [SerializeField, Tooltip("스파크 초기 속도의 최솟값(px/s)")] private float sparkSpeedMin = 60.0f;
        [SerializeField, Tooltip("스파크 초기 속도의 최댓값(px/s)")] private float sparkSpeedMax = 150.0f;
        [SerializeField, Tooltip("스파크 중력(px/s^2, 아래로)")] private float sparkGravity = 140.0f;
        [SerializeField, Tooltip("스파크 감속(초당)")] private float sparkDrag = 2.5f;
        [SerializeField, Tooltip("스파크 수명의 최솟값(초)")] private float sparkLifeMin = 0.25f;
        [SerializeField, Tooltip("스파크 수명의 최댓값(초)")] private float sparkLifeMax = 0.5f;
        [SerializeField, Tooltip("스파크 선 길이 배율(속도 x 이 값 = 길이 px)")] private float sparkStreakTime = 0.018f;

        [Header("잔불")]
        [SerializeField, Range(0, 8), Tooltip("떠오르는 불씨 개수")] private int emberCount = 5;

        [Header("밝기 (정점 알파 = HDR 발광 세기 0~1)")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("전체 발광 배율. 1이면 기준값")] private float glowScale = 1.0f;

        private const float TwoPi = Mathf.PI * 2.0f;
        private const int SparkCapacity = 32;
        private const int EmberCapacity = 8;
        private const int MaxLinePixels = 64;
        private const int BoltSegments = 8;
        private const int ThickSegments = 3;        // 본줄기 시작 부분에서 옆에 중간 색 선을 하나 더 붙여 굵게 보이게 하는 마디 수
        private const int SecondBranchSegment = 5;
        private const float BranchLengthRatio = 0.5f;
        private const int BranchSegments = 3;
        private const float ShockRingStart = 0.02f;
        private const float ShockRingEnd = 0.22f;
        private const int ShockRingDots = 14;
        private const float EmberSwapInterval = 0.09f;
        private static readonly Vector3 MeshBoundsSize = new Vector3(100.0f, 100.0f, 10.0f);

        private struct Spark
        {
            public bool bActive;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
        }

        private struct Ember
        {
            public bool bActive;
            public float x;
            public float y;
            public float vy;
            public float age;
            public float life;
            public int seed;
            public bool bPlus;
        }

        // 색은 상태마다 3가지만 쓴다[과열 여부][진한 색, 중간 색, 밝은 색]. HDR 배율이 곱해져도 흰색으로 날아가지 않도록 기본 색은 어둡게 잡았다.
        private const int ToneDeep = 0;
        private const int ToneMid = 1;
        private const int ToneLight = 2;
        private static readonly Color32[][] Palettes =
        {
            new Color32[] { new Color32(120, 52, 2, 255), new Color32(190, 112, 6, 255), new Color32(240, 190, 90, 255) },
            new Color32[] { new Color32(12, 50, 170, 255), new Color32(20, 115, 220, 255), new Color32(110, 200, 240, 255) },
        };

        // 매 프레임 Clear 후 채우고, 메인 스레드 단일 실행이라 인스턴스끼리 공유해도 안전하다
        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(768);

        //내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private readonly Spark[] sparks = new Spark[SparkCapacity];
        private readonly Ember[] embers = new Ember[EmberCapacity];
        private ParticleSystemRenderer rootParticleRenderer;

        //상태 변수
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bOverheat;
        private bool bMeshHasGeometry;
        private float elapsed;
        private int baseSeed;
        private float boltDropChance;   // 지금 프레임에서 번개 마디 하나가 빠질 확률(후반일수록 커진다)
        private uint randomState;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            GameObject child = new GameObject("LaserHit");
            child.transform.SetParent(transform, false);

            meshFilter = child.AddComponent<MeshFilter>();
            meshRenderer = child.AddComponent<MeshRenderer>();

            mesh = new Mesh { name = "VFX_LaserHit" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            meshRenderer.sharedMaterial = hitMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            bOverheat = false;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 131.0f + pos.y * 57.0f));
            if (0u == randomState) randomState = 1u;
            baseSeed = (int)(Rand01() * 100000.0f);

            for (int i = 0; i < SparkCapacity; i++) sparks[i].bActive = false;
            for (int i = 0; i < EmberCapacity; i++) embers[i].bActive = false;

            SpawnSparks();
            SpawnEmbers();
        }

        /// <summary>
        /// 과열 상태면 파랑 계열, 아니면 노랑 계열 색을 쓴다. 재생 직후(같은 프레임) 호출한다.
        /// </summary>
        public void SetOverheat(bool _isOverheat)
        {
            bOverheat = _isOverheat;
        }

        // ---------- 난수 / 유틸 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // 정수 씨앗에서 0~1 값 하나를 뽑는 해시(번개 모양처럼 프레임에 고정되어야 하는 값용)
        private static float Hash01(int _a, int _b, int _c)
        {
            uint h = unchecked((uint)(_a * 73856093) ^ (uint)(_b * 19349663) ^ (uint)(_c * 83492791));
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535.0f;
        }

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        // ---------- 스파크 / 잔불 ----------

        private void SpawnSparks()
        {
            int count = Mathf.Min(SparkCapacity, Mathf.RoundToInt(Mathf.Lerp(sparkCountMin, sparkCountMax, Rand01())));
            for (int i = 0; i < count; i++)
            {
                // 각도를 등분해서 한쪽으로 몰리지 않게 하고 약간 흔든다. 위쪽으로 조금 더 튀도록 초기 속도에 위 성분을 더한다.
                float angle = TwoPi * (i + Rand01() * 0.8f) / count;
                float speed = Mathf.Lerp(sparkSpeedMin, sparkSpeedMax, Rand01());

                sparks[i].bActive = true;
                sparks[i].x = Mathf.Cos(angle) * 3.0f;
                sparks[i].y = Mathf.Sin(angle) * 3.0f;
                sparks[i].vx = Mathf.Cos(angle) * speed;
                sparks[i].vy = Mathf.Sin(angle) * speed + 25.0f;
                sparks[i].age = 0.0f;
                sparks[i].life = Mathf.Lerp(sparkLifeMin, sparkLifeMax, Rand01());
            }
        }

        private void SpawnEmbers()
        {
            int count = Mathf.Min(EmberCapacity, emberCount);
            for (int i = 0; i < count; i++)
            {
                embers[i].bActive = true;
                embers[i].x = (Rand01() - 0.5f) * 12.0f;
                embers[i].y = (Rand01() - 0.5f) * 8.0f;
                embers[i].vy = Mathf.Lerp(10.0f, 24.0f, Rand01());
                embers[i].age = -Rand01() * 0.12f; // 조금씩 어긋나게 나타난다
                embers[i].life = Mathf.Lerp(0.35f, 0.6f, Rand01());
                embers[i].seed = (int)(Rand01() * 1000.0f);
                embers[i].bPlus = Rand01() < 0.5f;
            }
        }

        private void UpdateSparks(float _dt)
        {
            float drag = Mathf.Max(0.0f, 1.0f - sparkDrag * _dt);
            for (int i = 0; i < SparkCapacity; i++)
            {
                if (false == sparks[i].bActive) continue;

                sparks[i].age += _dt;
                if (sparks[i].age >= sparks[i].life)
                {
                    sparks[i].bActive = false;
                    continue;
                }

                sparks[i].vx *= drag;
                sparks[i].vy = sparks[i].vy * drag - sparkGravity * _dt;
                sparks[i].x += sparks[i].vx * _dt;
                sparks[i].y += sparks[i].vy * _dt;
            }
        }

        private void UpdateEmbers(float _dt)
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                embers[i].age += _dt;
                if (embers[i].age >= embers[i].life)
                {
                    embers[i].bActive = false;
                    continue;
                }

                if (0.0f < embers[i].age) embers[i].y += embers[i].vy * _dt;
            }
        }

        private bool HasActiveParts()
        {
            for (int i = 0; i < SparkCapacity; i++)
            {
                if (true == sparks[i].bActive) return true;
            }

            for (int i = 0; i < EmberCapacity; i++)
            {
                if (true == embers[i].bActive) return true;
            }

            return false;
        }

        // ---------- 그리기 ----------

        private byte GlowAlpha(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow * glowScale) * 255.0f);
        }

        private Color32 Tint(int _tone, float _glow)
        {
            Color32 color = Palettes[true == bOverheat ? 1 : 0][_tone];
            return new Color32(color.r, color.g, color.b, GlowAlpha(_glow));
        }

        private static void AddRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            quadBuffer.AddLocalRect(_x0, _y0, _x1, _y1, _color);
        }

        // 두 점 사이를 1px 선(브레젠험)으로 그린다(시작점 포함)
        private static void DrawLine(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = Mathf.Abs(_y1 - _y0);
            int stepX = _x0 < _x1 ? 1 : -1;
            int stepY = _y0 < _y1 ? 1 : -1;
            int error = dx - dy;
            int x = _x0;
            int y = _y0;

            AddRect(x, y, x + 1, y + 1, _color);
            for (int i = 0; i < MaxLinePixels && (x != _x1 || y != _y1); i++)
            {
                int doubled = error * 2;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += stepX;
                }
                if (doubled < dx)
                {
                    error += dx;
                    y += stepY;
                }

                AddRect(x, y, x + 1, y + 1, _color);
            }
        }

        private void RebuildMesh()
        {
            quadBuffer.Clear();

            DrawImpact();
            DrawBolts();
            DrawSparks();
            DrawEmbers();

            Transform meshTransform = meshRenderer.transform;
            meshTransform.rotation = Quaternion.identity;

            if (null != rootParticleRenderer)
            {
                meshRenderer.sortingLayerID = rootParticleRenderer.sortingLayerID;
                meshRenderer.sortingOrder = rootParticleRenderer.sortingOrder + sortingOffset;
            }

            bool bEmpty = 0 == quadBuffer.VertexCount;
            if (false == bEmpty || true == bMeshHasGeometry)
            {
                quadBuffer.Upload(mesh, MeshBoundsSize);
                bMeshHasGeometry = false == bEmpty;
            }

            meshRenderer.enabled = true;
        }

        // 1. 중심 충격: 십자/X자 광선(길이가 도트 단위로 늘었다 줄어든다), 밝은 원판, 점선 충격파 고리
        private void DrawImpact()
        {
            if (elapsed < flashDuration)
            {
                float t = elapsed / flashDuration;
                float grow = t < 0.35f ? t / 0.35f : 1.0f - (t - 0.35f) / 0.65f;
                int length = Mathf.RoundToInt(rayMaxLengthPx * grow);

                if (0 < length)
                {
                    Color32 light = Tint(ToneLight, 1.0f);
                    Color32 mid = Tint(ToneMid, 0.8f);

                    // 십자 광선: 안쪽 절반은 밝은 색, 바깥 절반은 중간 색
                    int half = Mathf.Max(1, length / 2);
                    AddRect(1, 0, 1 + half, 1, light);
                    AddRect(1 + half, 0, 1 + length, 1, mid);
                    AddRect(-half, 0, 0, 1, light);
                    AddRect(-length, 0, -half, 1, mid);
                    AddRect(0, 1, 1, 1 + half, light);
                    AddRect(0, 1 + half, 1, 1 + length, mid);
                    AddRect(0, -half, 1, 0, light);
                    AddRect(0, -length, 1, -half, mid);

                    // X자 광선: 십자보다 짧게 대각선으로 뻗는다
                    int diagonal = Mathf.Max(1, Mathf.RoundToInt(length * 0.6f));
                    for (int k = 1; k <= diagonal; k++)
                    {
                        Color32 color = k <= diagonal / 2 ? light : mid;
                        AddRect(k, k, k + 1, k + 1, color);
                        AddRect(-k, k, -k + 1, k + 1, color);
                        AddRect(k, -k, k + 1, -k + 1, color);
                        AddRect(-k, -k, -k + 1, -k + 1, color);
                    }
                }

                DrawDisc(Mathf.RoundToInt(discRadiusPx * (1.0f - t)));
            }

            if (elapsed >= ShockRingStart && elapsed < ShockRingEnd)
            {
                float t = (elapsed - ShockRingStart) / (ShockRingEnd - ShockRingStart);
                float radius = 4.0f + ringRadiusPx * EaseOut(t);
                int size = t < 0.4f ? 2 : 1;
                Color32 color = Tint(t < 0.5f ? ToneMid : ToneDeep, Mathf.Lerp(0.85f, 0.35f, t));
                for (int i = 0; i < ShockRingDots; i++)
                {
                    float angle = TwoPi * (i + (t < 0.5f ? 0.0f : 0.5f)) / ShockRingDots;
                    int px = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                    int py = Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                    AddRect(px, py, px + size, py + size, color);
                }
            }
        }

        // 밝은 중심 + 중간 색 가장자리의 원판. 중심은 (0,0) 칸이다.
        private void DrawDisc(int _radius)
        {
            if (1 > _radius) return;

            float outer = _radius + 0.35f;
            float lightEdge = _radius * 0.5f;
            for (int cy = -_radius; cy <= _radius; cy++)
            {
                for (int cx = -_radius; cx <= _radius; cx++)
                {
                    float d = Mathf.Sqrt(cx * cx + cy * cy);
                    if (d > outer) continue;
                    AddRect(cx, cy, cx + 1, cy + 1, d <= lightEdge ? Tint(ToneLight, 1.0f) : Tint(ToneMid, 0.8f));
                }
            }
        }

        // 2. 찌리릿 전기: 중심에서 방사형으로 뻗는 지그재그 번개. 모양은 boltFps마다 다시 뽑고(같은 프레임 안에서는 고정) 방향과 길이를 크게 흔든다.
        // 가닥마다 고유한 수명(무작위 순서로 사라진다)과 프레임마다 켜질 확률(점멸)이 있고, 프레임 전체가 꺼지는 순간도 있다. 후반에는 마디가 확률로 빠져 조각이 흩어진다.
        private void DrawBolts()
        {
            if (elapsed >= boltDuration) return;

            float t = elapsed / boltDuration;
            int frame = (int)(elapsed * Mathf.Max(1.0f, boltFps));

            // 전기가 불안정하게 튀는 느낌: 처음 0.06초 뒤부터 가끔 프레임 전체가 꺼진다
            if (0.06f <= elapsed && Hash01(frame, 4242, baseSeed) < 0.08f) return;

            boltDropChance = Mathf.Clamp01((t - 0.4f) / 0.6f) * 0.45f;
            float onChance = Mathf.Lerp(0.92f, 0.55f, t);

            for (int b = 0; b < boltCount; b++)
            {
                // 각도 순서가 아니라 가닥마다 무작위 수명을 줘서 아무 방향이나 먼저 사라진다
                float lifeEnd = Mathf.Lerp(0.55f, 1.05f, Hash01(b, 9001, baseSeed));
                if (t >= lifeEnd) continue;

                // 점멸: 프레임마다 확률로 켜졌다 꺼진다
                if (Hash01(b, frame + 31, baseSeed) > onChance) continue;

                // 방향: 가닥마다 자리를 정해 두되 프레임마다 크게 흔들고, 가끔은 방향을 통째로 다시 뽑는다
                float angle = TwoPi * (b + Hash01(b, 1, baseSeed) * 0.5f) / boltCount + (Hash01(b, frame + 5, baseSeed) - 0.5f) * 0.7f;
                if (Hash01(b, frame + 61, baseSeed) < 0.25f) angle = TwoPi * Hash01(b, frame + 77, baseSeed);

                float length = Mathf.Lerp(boltLengthMinPx, boltLengthMaxPx, Hash01(b, frame + 17, baseSeed)) * Mathf.Lerp(0.6f, 1.0f, Hash01(b, frame + 43, baseSeed)) * (1.0f - 0.3f * t);
                DrawBoltPath(b, frame, 0.0f, 0.0f, angle, length, BoltSegments, true);
            }
        }

        // 시작점에서 각도 방향으로 뻗는 지그재그 선. 본줄기(_bMain)는 밝은 색이고 중간에서 가지 하나를 친다. 가지는 중간 색이다.
        private void DrawBoltPath(int _bolt, int _frame, float _startX, float _startY, float _angle, float _length, int _segments, bool _bMain)
        {
            float dirX = Mathf.Cos(_angle);
            float dirY = Mathf.Sin(_angle);
            float perpX = -dirY;
            float perpY = dirX;
            float segment = _length / _segments;

            Color32 color = true == _bMain ? Tint(ToneLight, 0.95f) : Tint(ToneMid, 0.7f);
            Color32 thickColor = Tint(ToneMid, 0.8f);

            // 굵게 붙이는 선은 진행 방향에 수직인 쪽으로 1px 어긋나게 놓는다(가로에 가까우면 세로로, 세로에 가까우면 가로로)
            int thickOffsetX = Mathf.Abs(dirY) > Mathf.Abs(dirX) ? 1 : 0;
            int thickOffsetY = 1 - thickOffsetX;

            int lastX = Mathf.RoundToInt(_startX);
            int lastY = Mathf.RoundToInt(_startY);
            for (int k = 1; k <= _segments; k++)
            {
                // 끝으로 갈수록 흔들림이 줄어들어 가늘게 뾰족해진다
                float taper = 1.0f - (float)k / (_segments + 1);
                float jitter = (Hash01(_bolt * 31 + k, _frame, baseSeed + (true == _bMain ? 0 : 977)) - 0.5f) * 2.0f * boltJitterPx * taper;
                float px = _startX + dirX * segment * k + perpX * jitter;
                float py = _startY + dirY * segment * k + perpY * jitter;
                int x = Mathf.RoundToInt(px);
                int y = Mathf.RoundToInt(py);
                // 후반에는 마디가 확률로 빠진다(끊긴 조각이 흩어지듯 사라짐). 선은 이어져 있지 않아도 다음 마디의 위치는 그대로 계산한다.
                bool bDropped = 0.0f < boltDropChance && Hash01(_bolt * 13 + k, _frame, baseSeed + 777) < boltDropChance;
                if (false == bDropped)
                {
                    if (true == _bMain && k <= ThickSegments)
                    {
                        DrawLine(lastX + thickOffsetX, lastY + thickOffsetY, x + thickOffsetX, y + thickOffsetY, thickColor);
                    }
                    DrawLine(lastX, lastY, x, y, color);
                }

                // 가지: 본줄기의 2번째와 5번째 마디에서 갈라진다(가지는 본줄기의 절반 길이)
                if (false == bDropped && true == _bMain && (2 == k || SecondBranchSegment == k) && Hash01(_bolt + k, _frame, baseSeed + 555) < 0.75f)
                {
                    float side = Hash01(_bolt + k, _frame, baseSeed + 333) < 0.5f ? 1.0f : -1.0f;
                    float branchAngle = _angle + side * Mathf.Lerp(0.6f, 1.05f, Hash01(_bolt + k, _frame, baseSeed + 111));
                    DrawBoltPath(_bolt * 7 + k + 50, _frame, px, py, branchAngle, _length * BranchLengthRatio, BranchSegments, false);
                }

                lastX = x;
                lastY = y;
            }
        }

        // 3. 스파크: 머리 1px + 속도에 비례한 짧은 꼬리 선. 수명이 지날수록 밝은 색 -> 중간 색 -> 진한 색이 되고 마지막에는 깜빡인다.
        private void DrawSparks()
        {
            for (int i = 0; i < SparkCapacity; i++)
            {
                if (false == sparks[i].bActive) continue;

                float u = sparks[i].age / sparks[i].life;
                if (0.75f < u && 0 != ((int)(sparks[i].age * 40.0f) & 1)) continue;

                int tone = u < 0.4f ? ToneLight : (u < 0.75f ? ToneMid : ToneDeep);
                float glow = Mathf.Lerp(0.95f, 0.4f, u);
                Color32 color = Tint(tone, glow);

                int headX = Mathf.RoundToInt(sparks[i].x);
                int headY = Mathf.RoundToInt(sparks[i].y);
                int tailX = Mathf.RoundToInt(sparks[i].x - sparks[i].vx * sparkStreakTime);
                int tailY = Mathf.RoundToInt(sparks[i].y - sparks[i].vy * sparkStreakTime);
                DrawLine(tailX, tailY, headX, headY, Tint(tone == ToneLight ? ToneMid : ToneDeep, glow * 0.7f));
                AddRect(headX, headY, headX + 1, headY + 1, color);
            }
        }

        // 4. 잔불: 천천히 떠오르는 `.`/`+` 모양의 작은 불씨. `+`는 0.09초마다 `x`와 번갈아 바뀌어 회전하는 것처럼 보이고 후반에 깜빡인다.
        private void DrawEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive || 0.0f >= embers[i].age) continue;

                float u = embers[i].age / embers[i].life;
                if (0.7f < u && 0 != ((int)(embers[i].age * 30.0f) & 1)) continue;

                int px = Mathf.RoundToInt(embers[i].x);
                int py = Mathf.RoundToInt(embers[i].y);
                Color32 color = Tint(u < 0.6f ? ToneMid : ToneDeep, Mathf.Lerp(0.8f, 0.35f, u));

                if (true == embers[i].bPlus && u < 0.55f)
                {
                    bool bCross = 0 == (((int)(embers[i].age / EmberSwapInterval) + embers[i].seed) & 1);
                    if (true == bCross)
                    {
                        AddRect(px - 1, py, px + 2, py + 1, color);
                        AddRect(px, py - 1, px + 1, py + 2, color);
                    }
                    else
                    {
                        AddRect(px, py, px + 1, py + 1, color);
                        AddRect(px - 1, py - 1, px, py, color);
                        AddRect(px + 1, py - 1, px + 2, py, color);
                        AddRect(px - 1, py + 1, px, py + 2, color);
                        AddRect(px + 1, py + 1, px + 2, py + 2, color);
                    }
                }
                else
                {
                    AddRect(px, py, px + 1, py + 1, color);
                }
            }
        }

        private void HideRenderer()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            Begin();
        }

        private void LateUpdate()
        {
            // 첫 프레임은 시간을 더하지 않아서 t=0의 첫 장면(중심 충격)이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            if (elapsed >= totalDuration && false == HasActiveParts())
            {
                HideRenderer();
                return;
            }

            UpdateSparks(dt);
            UpdateEmbers(dt);
            RebuildMesh();
        }

        private void OnDisable()
        {
            HideRenderer();
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
