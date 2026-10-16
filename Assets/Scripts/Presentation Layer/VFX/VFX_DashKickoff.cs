using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 대시 박차기 먼지 연출. 대시를 시작하는 순간 발밑에서 흰 먼지가 대시 반대 방향으로 부채꼴로 퍼지고,
    /// 이동 초반에 발밑에서 잔먼지가 몇 개 이어진다. 모든 연출은 발사 후 0.20초 안에 끝난다.
    ///
    /// 구성
    /// - 바닥 메쉬(ShockWave 레이어): 뒤쪽 속도선.
    /// - 뒤쪽 메쉬(루트 렌더러보다 한 칸 아래): 캐릭터보다 안쪽(화면 위쪽)에 있는 먼지.
    /// - 앞쪽 메쉬(루트 렌더러보다 한 칸 위): 캐릭터보다 바깥쪽(화면 아래쪽)에 있는 먼지, 돌 조각, 섬광.
    ///
    /// 픽셀 규칙
    /// - 먼지는 명암 없는 순백 단색 원판이며 알파만 0.92다. 수명 후반에 반지름이 줄어 + 모양, 마지막에 . 한 점으로 끝난다.
    /// - 스프라이트 회전/확대가 없다. 모든 모양은 정수 반지름으로 매 프레임 새로 그린다.
    /// - 속도선은 가로, 세로, 2:1, 1:1 같은 정해진 픽셀 기울기로 맞춰 계단이 깨지지 않는다.
    /// - 돌 조각은 바닥에 닿기 전에 사라진다(바운스 없음).
    ///
    /// 쿼터뷰: 입자는 바닥 좌표(x, y)와 높이(z)로 움직이고 화면 좌표로 투영해서 그린다(화면 x = (x - y) / 2, 화면 y = (z - (x + y) / 2) / 2).
    /// 위치는 경과 시간만으로 계산하는 닫힌 식이라 프레임 속도와 무관하고 상태 저장이 없다. 재생 중 힙 할당이 없다.
    /// 풀 인스턴스로 쓰이며 루트 ParticleSystem 수명이 끝나면 풀로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_DashKickoff : MonoBehaviour
    {
        [Header("밀도 / 크기")]
        [SerializeField, Range(0.25f, 2.0f), Tooltip("먼지와 조각 개수에 곱하는 배율. 1이 기본이다.")]
        private float density = 1.0f;
        [SerializeField, Range(0.5f, 2.0f), Tooltip("먼지 원판 반지름에 곱하는 배율. 캐릭터 스프라이트 크기에 맞출 때 쓴다.")]
        private float sizeScale = 1.0f;

        [Header("기본 대시 값 (SetDash를 부르지 않았을 때)")]
        [SerializeField, Tooltip("대시 이동 거리(월드 유닛)")]
        private float defaultDistance = 3.0f;
        [SerializeField, Tooltip("대시 이동 시간(초)")]
        private float defaultDuration = 0.18f;

        [Header("뒤쪽 메쉬 (캐릭터 뒤에 가려지는 먼지)")]
        [SerializeField] private MeshFilter backMeshFilter;
        [SerializeField] private MeshRenderer backMeshRenderer;

        [Header("바닥 메쉬 (속도선이 나무와 캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("바닥 메쉬 소팅 레이어. 나무/캐릭터가 있는 Objects보다 아래여야 한다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float GroundDivisor = 2.0f;   // 바닥 좌표 -> 화면 픽셀 비율
        private const float TotalLength = 0.20f;     // 모든 연출은 발사 후 이 시간 안에 끝난다
        private const float TrailInterval = 0.04f;
        private const float TrailWindow = 0.10f;     // 이동 중 잔먼지는 이 시간 안에서만 생긴다

        private const int PuffCapacity = 224;
        private const int DebrisCapacity = 32;
        private const int StreakCapacity = 16;
        private const int BasePuffBurst = 8;
        private const int BaseDebris = 6;
        private const int BaseStreak = 4;

        private const float FlashLife = 0.08f;
        private const float DebrisGravity = 900.0f;

        private const byte KindBurst = 0;
        private const byte KindTrail = 1;

        private struct Puff
        {
            public float x0;
            public float y0;
            public float z0;
            public float vx;
            public float vy;
            public float vz;
            public float s0;
            public float grow;
            public float life;
            public float drag;
            public float buoy;
            public float delay;
            public byte kind;
        }

        private struct Debris
        {
            public float vx;
            public float vy;
            public float vz;
            public float z0;
            public float life;
        }

        private struct Streak
        {
            public int x;
            public int y;
            public int length;
            public float life;
        }

        private static readonly Color32 PuffColor = new Color32(255, 255, 255, 235);
        private static readonly Color32 SolidColor = new Color32(255, 255, 255, 255);

        private static readonly PixelQuadBuffer frontBuffer = new PixelQuadBuffer(1500);
        private static readonly PixelQuadBuffer backBuffer = new PixelQuadBuffer(1500);
        private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(800);

        // 내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh frontMesh;
        private Mesh backMesh;
        private Mesh groundMesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly Puff[] puffs = new Puff[PuffCapacity];
        private readonly Debris[] debris = new Debris[DebrisCapacity];
        private readonly Streak[] streaks = new Streak[StreakCapacity];

        // 상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private bool bFinished;
        private bool bPendingFinish;
        private Vector2 pendingFinishPosition;
        private float finishTime;
        private float finishGroundX;
        private float finishGroundY;
        private Vector2 dashDirection = Vector2.right;
        private float dashDistance;
        private float dashDuration;
        private bool bDashSet;
        private float groundDirX;
        private float groundDirY;
        private float groundDistance;
        private float snapFx;
        private float snapFy;
        private Vector3 originPosition;
        private int originPixelX;
        private int originPixelY;
        private int puffCount;
        private int debrisCount;
        private int streakCount;
        private uint randomState;
        private Vector3 meshBoundsSize = new Vector3(10.0f, 8.0f, 10.0f);
        private int groundSortingLayerId;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        private PixelQuadBuffer activeBuffer;
        private int activeBounds;
        private bool bRunActive;
        private int runX0;
        private int runX1;
        private int runY;
        private Color32 runColor;
        private readonly int[] boundsMinX = new int[3];
        private readonly int[] boundsMinY = new int[3];
        private readonly int[] boundsMaxX = new int[3];
        private readonly int[] boundsMaxY = new int[3];

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 이번 재생의 대시 방향/거리/시간을 정한다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// 위치는 이 오브젝트의 월드 위치(캐릭터 발밑)가 대시 시작점이다.
        /// </summary>
        /// <param name="_direction">월드(화면) 기준 대시 방향</param>
        /// <param name="_distance">월드 기준 이동 거리</param>
        /// <param name="_duration">이동 시간(초)</param>
        public void SetDash(Vector2 _direction, float _distance, float _duration)
        {
            dashDirection = _direction;
            dashDistance = Mathf.Max(0.0f, _distance);
            dashDuration = Mathf.Max(0.05f, _duration);
            bDashSet = true;
        }

        /// <summary>
        /// 대시가 정해진 시간보다 일찍 끝났을 때(벽에 막힘 등) 멈춘 위치를 알린다. 이후 이동 중 잔먼지 생성이 멈추고, 앞/뒤 정렬 기준 위치가 그 자리로 고정된다.
        /// 부르지 않아도 동작하며, 이 경우 대시 경로 끝이 기준이 된다.
        /// </summary>
        public void Finish(Vector2 _stopWorldPosition)
        {
            if (true == bBeginPending)
            {
                bPendingFinish = true;
                pendingFinishPosition = _stopWorldPosition;
                return;
            }

            if (true == bFinished) return;

            float x = (_stopWorldPosition.x - originPosition.x) * PixelsPerUnit;
            float yDown = -(_stopWorldPosition.y - originPosition.y) * PixelsPerUnit;
            StartFinish(elapsed, x + 2.0f * yDown, 2.0f * yDown - x);
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            frontMesh = CreateMesh(meshFilter, meshRenderer, "VFX_DashKickoff_Front");

            if (null != backMeshFilter && null != backMeshRenderer)
            {
                backMesh = CreateMesh(backMeshFilter, backMeshRenderer, "VFX_DashKickoff_Back");
            }

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = CreateMesh(groundMeshFilter, groundMeshRenderer, "VFX_DashKickoff_Ground");
                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }
        }

        private static Mesh CreateMesh(MeshFilter _filter, MeshRenderer _renderer, string _name)
        {
            Mesh mesh = new Mesh { name = _name };
            mesh.MarkDynamic();
            _filter.sharedMesh = mesh;

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        // 풀에서 꺼내져 활성화되면 방향이 정해질 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            bFinished = false;
            finishTime = float.MaxValue;

            originPosition = transform.position;
            originPixelX = Mathf.FloorToInt(originPosition.x * PixelsPerUnit);
            originPixelY = Mathf.FloorToInt(originPosition.y * PixelsPerUnit);

            if (false == bDashSet)
            {
                dashDirection = Vector2.right;
                dashDistance = defaultDistance;
                dashDuration = defaultDuration;
            }

            if (0.0001f > dashDirection.sqrMagnitude) dashDirection = Vector2.right;
            dashDirection.Normalize();

            // 화면 방향(위쪽이 양수) -> 바닥 방향. 시뮬레이션과 같은 식이다.
            float screenX = dashDirection.x;
            float screenYDown = -dashDirection.y;
            float gx = (screenX + 2.0f * screenYDown) * 0.5f;
            float gy = (2.0f * screenYDown - screenX) * 0.5f;
            float gl = Mathf.Sqrt(gx * gx + gy * gy);
            groundDirX = 0.0f < gl ? gx / gl : 1.0f;
            groundDirY = 0.0f < gl ? gy / gl : 0.0f;

            float distPx = dashDistance * PixelsPerUnit;
            float dpx = dashDirection.x * distPx;
            float dpyDown = -dashDirection.y * distPx;
            float ex = dpx + 2.0f * dpyDown;
            float ey = 2.0f * dpyDown - dpx;
            groundDistance = Mathf.Sqrt(ex * ex + ey * ey);

            // 속도선 방향은 계단이 깨지지 않는 픽셀 기울기(가로, 2:1, 1:1, 1:2, 세로)로 맞춘다. 등각 이동 방향(2:1)이 그대로 나온다.
            float angle = Mathf.Atan2(Mathf.Abs(dashDirection.y), Mathf.Abs(dashDirection.x)) * Mathf.Rad2Deg;
            int mx;
            int my;
            if (angle < 13.0f) { mx = 1; my = 0; }
            else if (angle < 36.0f) { mx = 2; my = 1; }
            else if (angle < 56.0f) { mx = 1; my = 1; }
            else if (angle < 77.0f) { mx = 1; my = 2; }
            else { mx = 0; my = 1; }

            float major = Mathf.Max(mx, my);
            snapFx = (0.0f <= dashDirection.x ? 1.0f : -1.0f) * mx / major;
            snapFy = (0.0f <= dashDirection.y ? 1.0f : -1.0f) * my / major;

            Vector3 pos = originPosition;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            float halfX = (distPx + 120.0f) / PixelsPerUnit;
            float halfY = (distPx * 0.6f + 100.0f) / PixelsPerUnit;
            meshBoundsSize = new Vector3(halfX * 2.0f, halfY * 2.0f, 10.0f);

            SetupPuffs();
            SetupDebris();
            SetupStreaks();

            if (true == bPendingFinish)
            {
                bPendingFinish = false;
                Finish(pendingFinishPosition);
            }
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        // ---------- 준비 ----------

        private static float EaseOutCubic(float _t)
        {
            float u = 1.0f - Mathf.Clamp01(_t);
            return 1.0f - u * u * u;
        }

        // 대시 시작점 기준 바닥 좌표에서 시각 _t의 머리(발) 위치
        private void HeadGround(float _t, out float _x, out float _y)
        {
            if (true == bFinished && finishTime <= _t)
            {
                _x = finishGroundX;
                _y = finishGroundY;
                return;
            }

            float e = EaseOutCubic(_t / dashDuration);
            _x = groundDirX * groundDistance * e;
            _y = groundDirY * groundDistance * e;
        }

        private void AddPuff(float _x, float _y, float _z, float _vx, float _vy, float _vz, float _s0, float _grow, float _life, float _drag, float _buoy, float _delay, byte _kind)
        {
            if (PuffCapacity <= puffCount) return;

            puffs[puffCount].x0 = _x;
            puffs[puffCount].y0 = _y;
            puffs[puffCount].z0 = _z;
            puffs[puffCount].vx = _vx;
            puffs[puffCount].vy = _vy;
            puffs[puffCount].vz = _vz;
            puffs[puffCount].s0 = _s0;
            puffs[puffCount].grow = _grow;
            puffs[puffCount].life = _life;
            puffs[puffCount].drag = _drag;
            puffs[puffCount].buoy = _buoy;
            puffs[puffCount].delay = _delay;
            puffs[puffCount].kind = _kind;
            puffCount++;
        }

        private void SetupPuffs()
        {
            puffCount = 0;
            float scale = Mathf.Max(0.1f, density);

            float bx = -groundDirX;
            float by = -groundDirY;
            float baseAngle = Mathf.Atan2(by, bx);

            // 발사 순간: 뒤쪽 부채꼴
            int burst = Mathf.RoundToInt(BasePuffBurst * scale);
            for (int i = 0; i < burst; i++)
            {
                float off = (Rand01() + Rand01() - 1.0f) * 0.45f;
                float angle = baseAngle + off;
                float speed = RandRange(110.0f, 300.0f) * (1.0f - Mathf.Abs(off) * 0.35f);
                float back = RandRange(0.0f, 10.0f);
                AddPuff(bx * back, by * back, RandRange(0.0f, 5.0f), Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, RandRange(10.0f, 60.0f),
                    RandRange(16.0f, 32.0f), RandRange(25.0f, 55.0f), RandRange(0.08f, 0.16f), 3.5f, RandRange(15.0f, 50.0f), 0.0f, KindBurst);
            }

            // 이동 중 잔먼지: 정해진 대시 경로를 따라 일정 간격으로 생긴다
            float perpX = -groundDirY;
            float perpY = groundDirX;
            float trailEnd = Mathf.Min(dashDuration, TrailWindow);
            for (float te = 0.0f; te < trailEnd; te += TrailInterval)
            {
                int c = 1;
                float e = EaseOutCubic(te / dashDuration);
                float hx = groundDirX * groundDistance * e;
                float hy = groundDirY * groundDistance * e;

                for (int j = 0; j < c; j++)
                {
                    float l = RandRange(-4.0f, 4.0f);
                    float behind = RandRange(0.0f, 6.0f);
                    float vBack = RandRange(15.0f, 50.0f);
                    AddPuff(hx + perpX * l - groundDirX * behind, hy + perpY * l - groundDirY * behind, RandRange(0.0f, 4.0f),
                        -groundDirX * vBack + perpX * RandRange(-15.0f, 15.0f), -groundDirY * vBack + perpY * RandRange(-15.0f, 15.0f), RandRange(5.0f, 30.0f),
                        RandRange(7.0f, 12.0f), RandRange(15.0f, 30.0f), RandRange(0.05f, 0.08f), 3.5f, RandRange(10.0f, 30.0f), te, KindTrail);
                }
            }
        }

        private void SetupDebris()
        {
            debrisCount = Mathf.Min(DebrisCapacity, Mathf.RoundToInt(BaseDebris * Mathf.Max(0.1f, density)));
            float baseAngle = Mathf.Atan2(-groundDirY, -groundDirX);

            for (int i = 0; i < debrisCount; i++)
            {
                float angle = baseAngle + (Rand01() + Rand01() - 1.0f) * 0.55f;
                float speed = RandRange(80.0f, 200.0f);
                debris[i].vx = Mathf.Cos(angle) * speed;
                debris[i].vy = Mathf.Sin(angle) * speed;
                debris[i].z0 = RandRange(3.0f, 8.0f);
                debris[i].vz = RandRange(110.0f, 260.0f);
                debris[i].life = RandRange(0.10f, 0.18f);
            }
        }

        private void SetupStreaks()
        {
            streakCount = Mathf.Min(StreakCapacity, Mathf.RoundToInt(BaseStreak * Mathf.Max(0.1f, density)));

            bool bMajorX = Mathf.Abs(snapFy) <= Mathf.Abs(snapFx);
            bool bDiagonal = Mathf.Abs(Mathf.Abs(snapFx) - Mathf.Abs(snapFy)) < 0.01f;

            for (int i = 0; i < streakCount; i++)
            {
                int l = Mathf.RoundToInt(RandRange(-7.0f, 7.0f));
                int back = Mathf.RoundToInt(RandRange(-1.0f, 4.0f));
                int ox;
                int oy;

                if (true == bDiagonal)
                {
                    // 직각 방향 (-fy, fx)
                    ox = Mathf.RoundToInt(-snapFy) * l;
                    oy = Mathf.RoundToInt(snapFx) * l;
                }
                else if (true == bMajorX)
                {
                    ox = 0;
                    oy = l;
                }
                else
                {
                    ox = l;
                    oy = 0;
                }

                streaks[i].x = ox + Mathf.FloorToInt(-snapFx * back + 0.5f);
                streaks[i].y = oy + Mathf.FloorToInt(-snapFy * back + 0.5f);
                streaks[i].length = Mathf.Max(4, Mathf.RoundToInt(RandRange(14.0f, 34.0f) * (1.0f - Mathf.Abs(l) / 12.0f)));
                streaks[i].life = RandRange(0.08f, 0.14f);
            }
        }

        private void StartFinish(float _time, float _groundX, float _groundY)
        {
            bFinished = true;
            finishTime = _time;
            finishGroundX = _groundX;
            finishGroundY = _groundY;
        }

        // ---------- 그리기 도우미 ----------

        private void SetActiveBuffer(PixelQuadBuffer _buffer, int _boundsIndex)
        {
            FlushRun();
            activeBuffer = _buffer;
            activeBounds = _boundsIndex;
        }

        private void FlushRun()
        {
            if (false == bRunActive) return;

            bRunActive = false;
            EmitRect(runX0, runY, runX1, runY + 1, runColor);
        }

        private void EmitRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            activeBuffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);

            int b = activeBounds;
            if (_x0 < boundsMinX[b]) boundsMinX[b] = _x0;
            if (_y0 < boundsMinY[b]) boundsMinY[b] = _y0;
            if (boundsMaxX[b] < _x1) boundsMaxX[b] = _x1;
            if (boundsMaxY[b] < _y1) boundsMaxY[b] = _y1;
        }

        private static bool SameColor(Color32 _a, Color32 _b)
        {
            return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
        }

        private void AddRun(int _x0, int _x1Exclusive, int _y, Color32 _color)
        {
            if (true == bRunActive && runY == _y && runX1 == _x0 && true == SameColor(runColor, _color))
            {
                runX1 = _x1Exclusive;
                return;
            }

            FlushRun();
            bRunActive = true;
            runX0 = _x0;
            runX1 = _x1Exclusive;
            runY = _y;
            runColor = _color;
        }

        private void Dot(int _x, int _y, Color32 _color)
        {
            AddRun(_x, _x + 1, _y, _color);
        }

        // 바닥 좌표 -> 화면 픽셀(원점 기준, 위쪽 양수)
        private static int ScreenX(float _gx, float _gy)
        {
            return Mathf.RoundToInt((_gx - _gy) / GroundDivisor);
        }

        private static int ScreenY(float _gx, float _gy, float _z)
        {
            return Mathf.RoundToInt((_z - (_gx + _gy) * 0.5f) / GroundDivisor);
        }

        // 명암 없는 단색 원판. 반지름 0은 한 점(.), 1은 + 모양, 2 이상은 원판.
        private void DrawDisc(int _cx, int _cy, int _r, Color32 _color)
        {
            if (_r <= 0)
            {
                Dot(_cx, _cy, _color);
                return;
            }

            if (1 == _r)
            {
                Dot(_cx, _cy - 1, _color);
                AddRun(_cx - 1, _cx + 2, _cy, _color);
                Dot(_cx, _cy + 1, _color);
                return;
            }

            float rr = _r * _r + _r * 0.8f;
            for (int dy = -_r; dy <= _r; dy++)
            {
                int w = Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0.0f, rr - dy * dy)));
                AddRun(_cx - w, _cx + w + 1, _cy + dy, _color);
            }
        }

        // _bDither가 true이면 체커 무늬로 절반만 그린다(속도선 끝이 점선처럼 사라지게)
        private void PlotDither(int _x, int _y, Color32 _color, bool _bDither)
        {
            if (true == _bDither && 0 != ((_x + _y) & 1)) return;
            Dot(_x, _y, _color);
        }

        // ---------- 한 프레임 ----------

        private void DrawStreaks()
        {
            for (int i = 0; i < streakCount; i++)
            {
                float u = elapsed / streaks[i].life;
                if (1.0f <= u) continue;

                int head = Mathf.FloorToInt(streaks[i].length * (1.0f - Mathf.Pow(1.0f - Mathf.Min(1.0f, u * 1.6f), 3.0f)));
                int tail = Mathf.FloorToInt(streaks[i].length * Mathf.Pow(u, 1.6f));
                bool bDither = 0.55f < u;

                for (int k = tail; k <= head; k++)
                {
                    int x = originPixelX + streaks[i].x + Mathf.FloorToInt(-snapFx * k + 0.5f);
                    int y = originPixelY + streaks[i].y + Mathf.FloorToInt(-snapFy * k + 0.5f);
                    PlotDither(x, y, SolidColor, bDither);
                }
            }
        }

        private void DrawPuffs()
        {
            HeadGround(elapsed, out float headX, out float headY);
            float headKey = headX + headY;

            for (int i = 0; i < puffCount; i++)
            {
                float age = elapsed - puffs[i].delay;
                if (age < 0.0f || puffs[i].life <= age) continue;
                if (KindTrail == puffs[i].kind && true == bFinished && finishTime < puffs[i].delay) continue;

                float u = age / puffs[i].life;
                if (0.96f < u) continue;

                float radius = (puffs[i].s0 + puffs[i].grow * age) * 0.07f * sizeScale;
                if (0.55f < u) radius *= Mathf.Max(0.0f, 1.0f - (u - 0.55f) / 0.45f);
                int r = Mathf.FloorToInt(radius + 0.5f);

                float drag = puffs[i].drag;
                float f = (1.0f - Mathf.Exp(-drag * age)) / drag;
                float gx = puffs[i].x0 + puffs[i].vx * f;
                float gy = puffs[i].y0 + puffs[i].vy * f;
                float buoyTerm = puffs[i].buoy / drag;
                float z = puffs[i].z0 + buoyTerm * age + (puffs[i].vz - buoyTerm) * f;
                if (z < 0.0f) z = 0.0f;

                bool bFront = headKey <= gx + gy;
                if (null == backMesh) bFront = true;
                SetActiveBuffer(true == bFront ? frontBuffer : backBuffer, true == bFront ? 0 : 1);
                DrawDisc(originPixelX + ScreenX(gx, gy), originPixelY + ScreenY(gx, gy, z), r, PuffColor);
            }
        }

        // 돌 조각: 바닥에 닿기 전에 사라진다(바운스 없음)
        private void DrawDebris()
        {
            SetActiveBuffer(frontBuffer, 0);

            for (int i = 0; i < debrisCount; i++)
            {
                float t = elapsed;
                if (debris[i].life <= t) continue;

                float z = debris[i].z0 + debris[i].vz * t - 0.5f * DebrisGravity * t * t;
                if (z <= 0.0f) continue;

                float gx = debris[i].vx * t;
                float gy = debris[i].vy * t;
                Dot(originPixelX + ScreenX(gx, gy), originPixelY + ScreenY(gx, gy, z), SolidColor);
            }
        }

        // 발사 섬광: + 모양 두 프레임
        private void DrawFlash()
        {
            float u = elapsed / FlashLife;
            if (1.0f <= u) return;

            SetActiveBuffer(frontBuffer, 0);
            int cx = originPixelX;
            int cy = originPixelY + 2;

            if (u < 0.4f)
            {
                DrawDisc(cx, cy, 2, SolidColor);
                for (int j = 3; j <= 5; j++)
                {
                    Dot(cx + j, cy, SolidColor);
                    Dot(cx - j, cy, SolidColor);
                    int h = Mathf.RoundToInt(j * 0.5f);
                    Dot(cx, cy + h, SolidColor);
                    Dot(cx, cy - h, SolidColor);
                }
            }
            else if (u < 0.75f)
            {
                Dot(cx + 4, cy, SolidColor);
                Dot(cx - 4, cy, SolidColor);
                Dot(cx + 7, cy, SolidColor);
                Dot(cx - 7, cy, SolidColor);
                Dot(cx + 2, cy + 1, SolidColor);
                Dot(cx - 2, cy + 1, SolidColor);
                Dot(cx + 2, cy - 1, SolidColor);
                Dot(cx - 2, cy - 1, SolidColor);
            }
        }

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;

            frontBuffer.Clear();
            frontBuffer.SetWorldTransform(transform.worldToLocalMatrix, pos.z);

            bool bBack = null != backMesh;
            bool bGround = null != groundMesh;

            if (true == bBack)
            {
                backBuffer.Clear();
                backBuffer.SetWorldTransform(backMeshRenderer.transform.worldToLocalMatrix, pos.z);
            }

            if (true == bGround)
            {
                groundBuffer.Clear();
                groundBuffer.SetWorldTransform(groundMeshRenderer.transform.worldToLocalMatrix, pos.z);
            }

            bRunActive = false;
            for (int b = 0; b < 3; b++)
            {
                boundsMinX[b] = int.MaxValue;
                boundsMinY[b] = int.MaxValue;
                boundsMaxX[b] = int.MinValue;
                boundsMaxY[b] = int.MinValue;
            }

            // 위치 기준은 Begin 때의 시작점(풀 인스턴스가 따라 움직이지 않는다는 전제). 픽셀 격자 기준점도 그때 값을 쓴다.
            SetActiveBuffer(true == bGround ? groundBuffer : frontBuffer, true == bGround ? 2 : 0);
            DrawStreaks();

            DrawPuffs();
            DrawDebris();
            DrawFlash();
            FlushRun();

            frontBuffer.Upload(frontMesh, 0);
            ApplyTightBounds(frontMesh, pos, 0);
            meshRenderer.enabled = 0 < frontBuffer.VertexCount;

            if (true == bBack)
            {
                backBuffer.Upload(backMesh, 0);
                ApplyTightBounds(backMesh, backMeshRenderer.transform.position, 1);
                backMeshRenderer.enabled = 0 < backBuffer.VertexCount;
            }

            if (true == bGround)
            {
                groundBuffer.Upload(groundMesh, 0);
                ApplyTightBounds(groundMesh, groundMeshRenderer.transform.position, 2);
                groundMeshRenderer.enabled = 0 < groundBuffer.VertexCount;
            }
        }

        private void ApplyTightBounds(Mesh _mesh, Vector3 _objectPosition, int _index)
        {
            Bounds bounds;

            if (boundsMaxX[_index] < boundsMinX[_index])
            {
                bounds = new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, PixelVfxCulling.BoundsDepth));
            }
            else
            {
                float minX = boundsMinX[_index] / PixelsPerUnit - _objectPosition.x;
                float minY = boundsMinY[_index] / PixelsPerUnit - _objectPosition.y;
                float maxX = boundsMaxX[_index] / PixelsPerUnit - _objectPosition.x;
                float maxY = boundsMaxY[_index] / PixelsPerUnit - _objectPosition.y;

                bounds = new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f),
                    new Vector3(maxX - minX + 0.1f, maxY - minY + 0.1f, PixelVfxCulling.BoundsDepth));
            }

            Bounds current = _mesh.bounds;
            if (current.center != bounds.center || current.size != bounds.size) _mesh.bounds = bounds;
        }

        private void HideAll()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != backMeshRenderer) backMeshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
            bBeginPending = true;
            bPendingFinish = false;
            elapsed = 0.0f;
            HideAll();
        }

        private void LateUpdate()
        {
            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            if (TotalLength <= elapsed)
            {
                HideAll();
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            // 정해진 시간이 끝나도록 Finish가 불리지 않았으면 대시 경로 끝을 머리 위치로 고정한다(앞/뒤 정렬 기준)
            if (false == bFinished && dashDuration <= elapsed)
            {
                StartFinish(dashDuration, groundDirX * groundDistance, groundDirY * groundDistance);
            }

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 루트 렌더러 기준으로 다시 맞춘다
            if (null != rootParticleRenderer)
            {
                int rootLayer = rootParticleRenderer.sortingLayerID;
                int rootOrder = rootParticleRenderer.sortingOrder;
                if (rootLayer != appliedRootSortingLayerID || rootOrder != appliedRootSortingOrder)
                {
                    appliedRootSortingLayerID = rootLayer;
                    appliedRootSortingOrder = rootOrder;

                    meshRenderer.sortingLayerID = rootLayer;
                    meshRenderer.sortingOrder = rootOrder + 1;

                    if (null != backMeshRenderer)
                    {
                        backMeshRenderer.sortingLayerID = rootLayer;
                        backMeshRenderer.sortingOrder = rootOrder - 1;
                    }
                }
            }

            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 cullingSize = new Vector3(meshBoundsSize.x + margin, meshBoundsSize.y + margin, PixelVfxCulling.BoundsDepth);
            Vector3 cullingCenter = originPosition + new Vector3(dashDirection.x * dashDistance * 0.5f, dashDirection.y * dashDistance * 0.5f, 0.0f);
            if (false == PixelVfxCulling.IsVisible(cullingCenter, cullingSize))
            {
                HideAll();
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            bDashSet = false;
            HideAll();
        }

        private void OnDestroy()
        {
            if (null != frontMesh)
            {
                Destroy(frontMesh);
                frontMesh = null;
            }

            if (null != backMesh)
            {
                Destroy(backMesh);
                backMesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }

        private void OnValidate()
        {
            density = Mathf.Clamp(density, 0.25f, 2.0f);
            sizeScale = Mathf.Clamp(sizeScale, 0.5f, 2.0f);
            defaultDistance = Mathf.Max(0.0f, defaultDistance);
            defaultDuration = Mathf.Max(0.05f, defaultDuration);
        }
    }
}
