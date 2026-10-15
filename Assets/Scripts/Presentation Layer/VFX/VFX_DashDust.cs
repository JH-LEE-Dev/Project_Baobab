using System;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 대쉬를 밟는 순간 발밑에서 한 번 튀는 흰색 반투명 먼지. 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬(VFX_TreeHeatBurst와 같은 방식)이며,
    /// 풀에서 꺼내져 활성화될 때(OnEnable) 재생을 시작하고 끝나면 스스로 렌더러를 끈다(풀 반환은 루트 ParticleSystem).
    /// 재생 직후 SetDirection으로 대쉬 방향(월드)을 넘겨야 한다. 넘기지 않으면 오른쪽 대쉬로 본다.
    ///
    /// 모양: 먼지를 아이소메트릭 바닥면 위의 입체로 정의하고 화면에 투영한다.
    /// - 바닥 좌표 a(대쉬 반대쪽 = 꼬리), b(옆), 높이 z. 바닥 벡터 (x, y)는 화면 (x, y / 2)로 투영(32 x 16 타일, 2:1)되고 높이는 화면 위쪽으로 올라간다.
    /// - 출발점 쪽이 높고 꼬리로 갈수록 낮아지는 쐐기(z = H * (1 - a / L)^p), 바닥 자국은 출발점 쪽이 넓고 꼬리가 좁은 물방울형,
    ///   중반에 부푸는 둥근 머리, 꼬리 방향 1px 바닥 줄, 꼬리 쪽으로 튀는 알갱이 1~3개.
    /// - 시간: 솟음(0~22%) -> 둥근 머리(42% 근처) -> 낮아지며 꼬리 쪽으로 밀림(45~100%) -> 4x4 순서 디더로 흩어짐(78~100%).
    /// 모양 계산은 대쉬 먼지 시뮬레이션(웹 미리보기)과 같은 식, 같은 난수(mulberry32), 같은 반올림(JS Math.round)을 쓰므로 같은 시드에서 같은 픽셀이 나온다.
    ///
    /// 그리기: 바닥 기둥(a, b)마다 높이만큼 세로로 칸을 찍어 정적 마스크(합집합)에 모은 뒤 행마다 이어진 칸을 한 쿼드로 합쳐 올린다.
    /// 칸이 겹치지 않으므로 반투명 알파가 이중으로 쌓이지 않는다. 색은 정점 색 RGBA 그대로(PixelQuadAlpha 셰이더, 발광 없음).
    ///
    /// 최적화: 공유 정적 PixelQuadBuffer, 구조체/고정 배열(런타임 할당 없음), 같은 그림이면 업로드 생략(정점 수 + 내용 해시),
    /// 메쉬 경계는 실제 그린 범위, 화면 밖이면 재구성 생략(PixelVfxCulling), Transform/소팅/enabled는 바뀔 때만 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class VFX_DashDust : MonoBehaviour
    {
        [Header("렌더링")]
        [SerializeField, Tooltip("PixelQuadAlpha 셰이더 머티리얼(정점 알파 = 투명도)")] private Material dustMaterial;
        [SerializeField, Tooltip("먼지 메쉬의 루트 렌더러 기준 상대 소팅 오더")] private int sortingOffset = 0;

        [Header("모양")]
        [SerializeField, Range(0.0f, 1.0f), Tooltip("먼지 알파(거의 보일락 말락: 0.3 안팎)")] private float alpha = 0.3f;
        [SerializeField, Tooltip("먼지 색(알파는 위 값으로 덮어쓴다)")] private Color32 dustColor = new Color32(255, 255, 255, 255);
        [SerializeField, Tooltip("이펙트 전체 길이(초). 루트 ParticleSystem 수명보다 짧아야 한다")] private float duration = 0.36f;

        private const int MaskSize = 96;                 // 출발점 기준 ±48px 범위를 담는 칸 마스크
        private const int MaskOrigin = MaskSize / 2;
        private const int MaxSpecks = 3;
        private const int QuadCapacity = 256;
        private const double Step = 0.5;                 // 바닥 기둥 샘플 간격(px)
        private const double Gravity = 170.0;            // 알갱이 중력(px/s^2)
        private const float PixelUnit = 1.0f / 32.0f;
        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        private struct DustShape
        {
            public double length;       // 꼬리 길이(바닥 px)
            public double height;       // 최고 높이(px)
            public double power;        // 높이 경사 곡률
            public double frontWidth;   // 출발점 쪽 바닥 반폭(물방울 머리)
            public double lean;         // 앞면 기울기 폭
            public double headRadius;   // 둥근 머리 최대 반지름
            public double headOffset;   // 머리 중심(앞에서부터 바닥 거리)
            public double drift;        // 수명 동안 꼬리 쪽으로 밀리는 거리
            public int speckCount;
        }

        private struct Speck
        {
            public double delay;
            public double life;
            public double a0;           // 꼬리 길이 비율
            public double z0;           // 높이 비율
            public double b0;           // 옆 위치
            public double va;           // 꼬리 쪽 바닥 속도(px/s)
            public double vb;           // 옆 속도
            public double vz;           // 위쪽 속도
            public int kind;            // 0 점, 1 가로 대시, 2 대각 2점
        }

        // 화면에 그릴 쿼드를 모으는 공유 버퍼와 칸 마스크(매 프레임 비우고 채운 뒤 곧바로 업로드하므로 인스턴스끼리 겹치지 않는다)
        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(QuadCapacity);
        private static readonly bool[] mask = new bool[MaskSize * MaskSize];

        //내부 의존성
        private readonly Speck[] specks = new Speck[MaxSpecks];
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Transform meshTransform;
        private ParticleSystemRenderer rootParticleRenderer;

        //상태 변수
        private DustShape shape;
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bTransformSynced;
        private bool bSortingSynced;
        private bool bHasBounds;
        private int framesSinceEnable;
        private int uploadedVertexCount;
        private ulong uploadedHash;
        private int syncedSortingLayerId;
        private int syncedSortingOrder;
        private int maskMinX;
        private int maskMinY;
        private int maskMaxX;
        private int maskMaxY;
        private float elapsed;
        private double dissolve;
        private double backX;               // 꼬리(대쉬 반대) 방향 바닥 1px의 화면 투영(화면 y는 아래가 +)
        private double backY;
        private double sideX;               // 옆 방향 바닥 1px의 화면 투영
        private double sideY;
        private uint randomState;
        private Color32 quadColor;
        private Vector2 boundsMin;
        private Vector2 boundsMax;
        private Vector3 appliedBoundsCenter;
        private Vector3 appliedBoundsSize;
        private Vector3 syncedParentScale;
        private Quaternion syncedParentRotation;

        // ---------- 외부 제어 ----------

        /// <summary>
        /// 대쉬 방향(월드, y 위가 +)을 넘긴다. 풀에서 Play한 직후(같은 프레임, LateUpdate 전)에 부른다. 꼬리는 이 방향의 반대쪽 바닥으로 깔린다.
        /// </summary>
        public void SetDirection(Vector2 _worldDashDirection)
        {
            // 월드(y 위) -> 화면(y 아래) -> 바닥(세로 2배)
            double groundX = _worldDashDirection.x;
            double groundY = -_worldDashDirection.y * 2.0;
            double length = Math.Sqrt(groundX * groundX + groundY * groundY);
            if (0.000001 >= length)
            {
                groundX = 1.0;
                groundY = 0.0;
                length = 1.0;
            }

            groundX /= length;
            groundY /= length;

            double tailX = -groundX;
            double tailY = -groundY;
            backX = tailX;
            backY = tailY * 0.5;
            sideX = -tailY;
            sideY = tailX * 0.5;
        }

        /// <summary>
        /// 먼지를 놓을 월드 위치. 발 위치에서 대쉬 반대쪽 바닥으로 _backOffsetUnits(바닥 거리, 유닛)만큼 물러난 곳이다.
        /// 바닥 거리는 등각(세로 2배)으로 재므로, 위/아래 대쉬는 화면상 절반만 물러난다(시뮬레이션과 같은 배치).
        /// </summary>
        public static Vector3 GetSpawnPosition(Vector3 _footPosition, Vector2 _worldDashDirection, float _backOffsetUnits)
        {
            float groundX = _worldDashDirection.x;
            float groundY = _worldDashDirection.y * 2.0f;
            float length = Mathf.Sqrt(groundX * groundX + groundY * groundY);
            if (0.000001f >= length) return _footPosition;

            groundX /= length;
            groundY /= length;
            return new Vector3(_footPosition.x - groundX * _backOffsetUnits, _footPosition.y - groundY * 0.5f * _backOffsetUnits, _footPosition.z);
        }

        // ---------- 초기화 ----------

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            GameObject child = new GameObject("DashDust_Mesh");
            child.transform.SetParent(transform, false);
            meshTransform = child.transform;
            meshFilter = child.AddComponent<MeshFilter>();
            meshRenderer = child.AddComponent<MeshRenderer>();

            mesh = new Mesh { name = "VFX_DashDust_Mesh" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, QuadCapacity);

            meshRenderer.sharedMaterial = dustMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            framesSinceEnable = 0;
            bFirstFrame = true;
            bTransformSynced = false;
            bSortingSynced = false;
            bHasBounds = false;
            uploadedVertexCount = -1; // 메쉬에 지난 재생의 그림이 남아 있을 수 있으므로 첫 프레임은 무조건 올린다
            quadColor = new Color32(dustColor.r, dustColor.g, dustColor.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255.0f));

            SetDirection(Vector2.right);

            Vector3 pos = transform.position;
            uint seed = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 131.0f + pos.y * 57.0f)) & 0x7fffffffu;
            BuildShape(seed);
        }

        // 모양 파라미터와 알갱이를 재생 시작 때 한 번 뽑는다(뽑는 순서는 시뮬레이션과 같다)
        private void BuildShape(uint _seed)
        {
            randomState = _seed;
            shape.length = Lerp(10.0, 13.0, Rand01());
            shape.height = Lerp(6.0, 8.5, Rand01());
            shape.power = Lerp(1.3, 1.8, Rand01());
            shape.frontWidth = Lerp(2.2, 3.2, Rand01());
            shape.lean = Lerp(0.6, 1.6, Rand01());
            shape.headRadius = Lerp(2.3, 3.3, Rand01());
            shape.headOffset = Lerp(1.2, 2.6, Rand01());
            shape.drift = Lerp(3.0, 5.5, Rand01());
            shape.speckCount = Math.Min(MaxSpecks, 1 + (int)Math.Floor(Rand01() * 3.0));

            for (int i = 0; i < shape.speckCount; i++)
            {
                specks[i].delay = Lerp(0.04, 0.12, Rand01());
                specks[i].life = Lerp(0.16, 0.28, Rand01());
                specks[i].a0 = Lerp(0.25, 0.65, Rand01());
                specks[i].z0 = Lerp(0.35, 0.85, Rand01());
                specks[i].b0 = Lerp(-1.5, 1.5, Rand01());
                specks[i].va = Lerp(25.0, 55.0, Rand01());
                specks[i].vb = Lerp(-12.0, 12.0, Rand01());
                specks[i].vz = Lerp(18.0, 42.0, Rand01());
                specks[i].kind = Rand01() < 0.5 ? 0 : (Rand01() < 0.5 ? 1 : 2);
            }
        }

        // ---------- 난수 / 유틸 ----------

        // mulberry32: 시뮬레이션(JS)과 같은 수열
        private double Rand01()
        {
            unchecked
            {
                randomState += 0x6D2B79F5u;
                uint t = randomState;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        private static double Lerp(double _a, double _b, double _t)
        {
            return _a + (_b - _a) * _t;
        }

        private static double Clamp01(double _t)
        {
            return Math.Min(1.0, Math.Max(0.0, _t));
        }

        private static double EaseOutQuart(double _t)
        {
            return 1.0 - Math.Pow(1.0 - _t, 4.0);
        }

        private static double EaseOutCubic(double _t)
        {
            return 1.0 - Math.Pow(1.0 - _t, 3.0);
        }

        private static double Smooth(double _edge0, double _edge1, double _x)
        {
            double t = Clamp01((_x - _edge0) / (_edge1 - _edge0));
            return t * t * (3.0 - 2.0 * t);
        }

        // JS Math.round와 같은 반올림(0.5는 위로). Mathf.RoundToInt는 짝수 반올림이라 쓰지 않는다.
        private static int RoundJs(double _value)
        {
            return (int)Math.Floor(_value + 0.5);
        }

        // ---------- 래스터 (화면 좌표: 출발점 기준 px, y는 아래가 +) ----------

        private void ClearMask()
        {
            if (maskMinX <= maskMaxX)
            {
                for (int y = maskMinY; y <= maskMaxY; y++)
                {
                    int row = y * MaskSize;
                    for (int x = maskMinX; x <= maskMaxX; x++) mask[row + x] = false;
                }
            }

            maskMinX = int.MaxValue;
            maskMinY = int.MaxValue;
            maskMaxX = int.MinValue;
            maskMaxY = int.MinValue;
        }

        private void Mark(int _x, int _y)
        {
            int mx = _x + MaskOrigin;
            int my = _y + MaskOrigin;
            if (0 > mx || 0 > my || MaskSize <= mx || MaskSize <= my) return;

            mask[my * MaskSize + mx] = true;
            if (mx < maskMinX) maskMinX = mx;
            if (my < maskMinY) maskMinY = my;
            if (mx > maskMaxX) maskMaxX = mx;
            if (my > maskMaxY) maskMaxY = my;
        }

        // 소멸 구간에서는 4x4 순서 디더로 칸을 덜어낸다
        private void Put(int _x, int _y)
        {
            if (0.0 < dissolve && Bayer[(_y & 3) * 4 + (_x & 3)] < dissolve * 16.0) return;
            Mark(_x, _y);
        }

        private double HeightAt(double _a, double _height, double _length)
        {
            if (0.0 > _a) return -_a <= shape.lean ? _height * (1.0 + _a / (shape.lean + 1.0)) : 0.0;
            if (_a >= _length) return 0.0;
            return _height * Math.Pow(1.0 - _a / _length, shape.power);
        }

        private double HalfWidthAt(double _a, double _length)
        {
            if (0.0 > _a) return shape.frontWidth * (1.0 + _a / (shape.lean + 1.0));
            return shape.frontWidth * Math.Pow(Math.Max(0.0, 1.0 - _a / _length), 0.6) + 0.4;
        }

        private void Rasterize(double _age, double _flickerTime)
        {
            double totalDuration = Math.Max(0.05, duration);
            double u = _age / totalDuration;
            if (0.0 > u || 1.0 <= u) return;

            double rise = EaseOutQuart(Clamp01(u / 0.22));
            double fall = Smooth(0.45, 1.0, u);
            double height = shape.height * rise * (1.0 - 0.8 * fall);
            double length = shape.length * (0.55 + 0.45 * EaseOutQuart(Clamp01(u / 0.25))) * (1.0 + 0.15 * fall);
            double headT = (u - 0.42) / 0.16;
            double head = shape.headRadius * Math.Exp(-(headT * headT)) * rise;
            double headZ = height * 0.7;
            double drift = shape.drift * EaseOutCubic(u);
            double baseLength = length * 1.15 * (1.0 - fall);
            dissolve = u > 0.78 ? (u - 0.78) / 0.22 : 0.0;

            // 본체: 바닥 기둥(a, b)마다 높이 z만큼 화면 세로로 찍는다. 옆으로 갈수록 둥글게 낮아진다.
            for (double a = -shape.lean; a < length; a += Step)
            {
                double columnHeight = HeightAt(a, height, length);
                double halfWidth = HalfWidthAt(a, length);
                if (0.5 > columnHeight || 0.0 >= halfWidth) continue;

                for (double b = -halfWidth; b <= halfWidth; b += Step)
                {
                    double k = b / halfWidth;
                    double z = columnHeight * Math.Sqrt(Math.Max(0.0, 1.0 - k * k));
                    if (0.5 > z) continue;

                    double ga = a + drift;
                    int px = RoundJs(ga * backX + b * sideX);
                    double groundY = ga * backY + b * sideY;
                    int y0 = RoundJs(groundY);
                    int y1 = RoundJs(groundY - z + 0.5);
                    for (int y = y1; y <= y0; y++) Put(px, y);
                }
            }

            // 둥근 머리(중반에만 부풂): 구는 화면에서 원으로 보인다
            if (0.6 < head)
            {
                double ga = shape.headOffset + drift;
                double cx = ga * backX;
                double cy = ga * backY - headZ;
                double r = Math.Ceiling(head);
                int yStart = (int)Math.Floor(cy - r);
                int yEnd = (int)Math.Ceiling(cy + r);
                int xStart = (int)Math.Floor(cx - r);
                int xEnd = (int)Math.Ceiling(cx + r);
                double headSq = head * head;
                for (int y = yStart; y <= yEnd; y++)
                {
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        double ox = x - cx;
                        double oy = y - cy;
                        if (ox * ox + oy * oy <= headSq) Put(x, y);
                    }
                }
            }

            // 바닥 줄: 꼬리 방향 중심선을 꼬리 끝보다 조금 더 길게 1px로 깐다
            int lastX = int.MaxValue;
            int lastY = int.MaxValue;
            for (double a = 0.0; a < baseLength; a += Step)
            {
                double ga = a + drift;
                int x = RoundJs(ga * backX);
                int y = RoundJs(ga * backY);
                if (x == lastX && y == lastY) continue;
                lastX = x;
                lastY = y;
                Put(x, y);
            }

            // 떨어져 나간 알갱이: 꼬리 쪽 바닥 + 위로 튀고 중력으로 바닥에 내려앉는다. 후반에는 30Hz로 깜빡인다.
            for (int i = 0; i < shape.speckCount; i++)
            {
                double tt = _age - specks[i].delay;
                if (0.0 > tt || tt >= specks[i].life) continue;
                if (0.7 < tt / specks[i].life && 0 != ((int)Math.Floor(_flickerTime * 30.0) & 1)) continue;

                double ga = length * specks[i].a0 + specks[i].va * tt + drift;
                double gb = specks[i].b0 + specks[i].vb * tt;
                double z = Math.Max(0.0, height * specks[i].z0 + specks[i].vz * tt - 0.5 * Gravity * tt * tt);
                int x = RoundJs(ga * backX + gb * sideX);
                int y = RoundJs(ga * backY + gb * sideY - z);
                Mark(x, y);
                if (1 == specks[i].kind) Mark(x + 1, y);
                else if (2 == specks[i].kind) Mark(x + 1, y + 1);
            }
        }

        // 마스크의 각 행에서 이어진 칸을 한 쿼드로 합쳐 버퍼에 넣는다. 화면 행 y(아래가 +)는 로컬 [-y-1, -y)에 놓인다.
        private void EmitMask()
        {
            if (maskMinX > maskMaxX) return;

            for (int my = maskMinY; my <= maskMaxY; my++)
            {
                int row = my * MaskSize;
                int screenY = my - MaskOrigin;
                int runStart = -1;
                for (int mx = maskMinX; mx <= maskMaxX + 1; mx++)
                {
                    bool bCovered = mx <= maskMaxX && true == mask[row + mx];
                    if (true == bCovered)
                    {
                        if (0 > runStart) runStart = mx;
                        continue;
                    }

                    if (0 <= runStart)
                    {
                        quadBuffer.AddLocalRect(runStart - MaskOrigin, -screenY - 1, mx - MaskOrigin, -screenY, quadColor);
                        runStart = -1;
                    }
                }
            }
        }

        // ---------- 메쉬 갱신 ----------

        private void RebuildMesh()
        {
            ClearMask();
            Rasterize(elapsed, elapsed);

            quadBuffer.Clear();
            EmitMask();

            // 직전에 올린 그림과 같으면 올리지 않는다
            if (quadBuffer.VertexCount == uploadedVertexCount && quadBuffer.ContentHash == uploadedHash) return;

            quadBuffer.Upload(mesh, 0);
            uploadedVertexCount = quadBuffer.VertexCount;
            uploadedHash = quadBuffer.ContentHash;
            bHasBounds = quadBuffer.TryGetLocalBounds(out boundsMin, out boundsMax);
            if (false == bHasBounds) return;

            // 메쉬 경계는 실제 그린 픽셀 범위(1px 여유)
            Vector3 center = new Vector3((boundsMin.x + boundsMax.x) * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f, 0.0f);
            Vector3 size = new Vector3(boundsMax.x - boundsMin.x + 2.0f * PixelUnit, boundsMax.y - boundsMin.y + 2.0f * PixelUnit, PixelVfxCulling.BoundsDepth);
            if (center == appliedBoundsCenter && size == appliedBoundsSize) return;

            appliedBoundsCenter = center;
            appliedBoundsSize = size;
            mesh.bounds = new Bounds(center, size);
        }

        // 이번 프레임 메인 카메라에 보이는지(지금 메쉬에 올라가 있는 범위 + 여유). 아직 그린 것이 없으면 보이는 것으로 본다.
        private bool IsOnScreen()
        {
            if (false == bHasBounds) return true;

            Vector3 center = meshTransform.position + new Vector3((boundsMin.x + boundsMax.x) * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f, 0.0f);
            Vector3 size = new Vector3(boundsMax.x - boundsMin.x + 2.0f * PixelVfxCulling.MarginUnits, boundsMax.y - boundsMin.y + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
            return PixelVfxCulling.IsVisible(center, size);
        }

        // 부모(풀 루트)의 스케일/회전을 지우고 도트 크기가 항상 1px이 되게 하며, 루트 렌더러(정렬 순서)를 따라간다. 바뀌었을 때만 쓴다.
        private void SyncTransformAndSorting()
        {
            Vector3 parentScale = transform.lossyScale;
            Quaternion parentRotation = transform.rotation;
            bool bTransformChanged = false == bTransformSynced
                || parentScale.x != syncedParentScale.x || parentScale.y != syncedParentScale.y || parentScale.z != syncedParentScale.z
                || parentRotation.x != syncedParentRotation.x || parentRotation.y != syncedParentRotation.y
                || parentRotation.z != syncedParentRotation.z || parentRotation.w != syncedParentRotation.w;

            if (true == bTransformChanged)
            {
                bTransformSynced = true;
                syncedParentScale = parentScale;
                syncedParentRotation = parentRotation;

                float inverseX = 0.0001f < Mathf.Abs(parentScale.x) ? 1.0f / parentScale.x : 1.0f;
                float inverseY = 0.0001f < Mathf.Abs(parentScale.y) ? 1.0f / parentScale.y : 1.0f;
                meshTransform.rotation = Quaternion.identity;
                meshTransform.localScale = new Vector3(inverseX, inverseY, 1.0f);
            }

            if (null == rootParticleRenderer) return;

            int layerId = rootParticleRenderer.sortingLayerID;
            int order = rootParticleRenderer.sortingOrder;
            if (true == bSortingSynced && layerId == syncedSortingLayerId && order == syncedSortingOrder) return;

            bSortingSynced = true;
            syncedSortingLayerId = layerId;
            syncedSortingOrder = order;
            meshRenderer.sortingLayerID = layerId;
            meshRenderer.sortingOrder = order + sortingOffset;
        }

        private void HideRenderer()
        {
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
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
            // 첫 프레임은 시간을 더하지 않아서 t=0의 첫 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;
            framesSinceEnable++;

            if (elapsed >= duration)
            {
                HideRenderer();
                return;
            }

            if (3 > framesSinceEnable || true == IsOnScreen()) RebuildMesh();

            SyncTransformAndSorting();
            if (false == meshRenderer.enabled) meshRenderer.enabled = true;
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

#if UNITY_EDITOR
        // 플레이 중 인스펙터에서 색이나 알파를 바꾸면 미리 계산해 둔 색을 다시 만든다
        private void OnValidate()
        {
            if (false == bInitialized) return;

            quadColor = new Color32(dustColor.r, dustColor.g, dustColor.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255.0f));
        }
#endif
    }
}
