using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 드론 연쇄 공격용 픽셀 번개 레이저. VFX_LightningZap과 같은 호출부(SetColor / PlayZap)를 그대로 쓰지만,
    /// LineRenderer 대신 32 PPU 픽셀 격자에 스냅된 절차적 쿼드 메쉬로 그린다.
    ///
    /// 연출은 짧고 강렬한 3단계다.
    ///  1) 파지직: 굵고 각진 지그재그 빔 + 흰 코어 + 보조 가닥/가지 스파크 + 양 끝 플래시
    ///  2) 실선화: 굵기가 정수 픽셀 단위로 계단식으로 빠르게 가늘어지고 지그재그도 잦아든다
    ///  3) 점선 소멸: 1픽셀 실선이 도트로 쪼개지고, 총구 쪽부터 무작위로 도트가 탈락하며 꺼지기 직전 어두워진다
    /// 마지막 도트가 하나씩 꺼지므로 한 프레임에 "띡" 하고 사라지지 않는다.
    ///
    /// 경로(지그재그)는 jitterInterval마다 새로 뽑고, 굵기/소멸 진행은 매 프레임 갱신한다. 정적 버퍼와 Color32만
    /// 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VFX_DroneLaser : MonoBehaviour
    {
        [Header("렌더링")]
        [SerializeField] private Material laserMaterial;
        [SerializeField] private string sortingLayerName = "FlyingItem";
        [SerializeField] private int sortingOrder = 10;

        [Header("타임라인 (초)")]
        [SerializeField] private float burstDuration = 0.05f; // ① 파지직 - 최대 굵기 유지
        [SerializeField] private float thinDuration = 0.10f; // ② 실선화 - 계단식으로 1픽셀까지
        [SerializeField] private float dissolveDuration = 0.15f; // ③ 점선 소멸
        [SerializeField] private float jitterInterval = 0.035f; // 지그재그 경로를 새로 뽑는 주기(60fps 기준 약 2프레임)

        [Header("굵기 (픽셀)")]
        [SerializeField] private int burstWidthPx = 8; // 처음 터질 때 몸통 굵기
        [SerializeField] private int glowPaddingPx = 2; // 바깥 글로우가 몸통보다 넓은 폭(양쪽 합계)

        [Header("지그재그 (픽셀)")]
        [SerializeField] private float burstAmplitudePx = 10f; // 파지직 구간 최대 꺾임 폭
        [SerializeField] private float tailAmplitudePx = 1.5f; // 실선화가 끝난 뒤 꺾임 폭
        [SerializeField] private int pathStepPx = 9; // 꺾임점 간격

        [Header("보조 가닥 / 가지")]
        [SerializeField] private int strandCount = 1; // 메인 빔 주위를 다른 궤적으로 도는 얇은 흰 가닥 수
        [SerializeField] private float strandAmplitudeScale = 1.4f;
        [SerializeField] private int burstBranchCount = 4; // 파지직 구간에서 홉마다 튀어나오는 스파크 가지 수(실선화에서 0으로 줄어든다)
        [SerializeField] private int branchLengthMinPx = 6;
        [SerializeField] private int branchLengthMaxPx = 16;

        [Header("양 끝 플래시")]
        [SerializeField] private int flashRadiusPx = 5;

        [Header("점선 소멸")]
        [SerializeField, Range(0f, 0.5f)] private float dimBand = 0.18f; // 탈락 직전 이 구간의 도트는 어두운 색으로 꺼진다

        [Header("색상 (Drone이 SetColor로 덮어쓴다)")]
        [SerializeField] private Color defaultColor = Color.yellow;
        [SerializeField] private float defaultIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float coreWhiteMix = 0.85f; // 코어를 흰색으로 섞는 비율
        [SerializeField, Range(0f, 1f)] private float outerBrightness = 0.5f; // 바깥 글로우 밝기(몸통 색 대비)

        private const float PixelsPerUnit = 32f;
        private const float PixelUnit = 1f / PixelsPerUnit;
        private const int MaxVertices = 60000;

        private const int KindMain = 0;
        private const int KindStrand = 1;
        private const int KindBranch = 2;

        private struct PolyRange
        {
            public int start; // polyPts 내 시작 인덱스
            public int count;
            public int kind;
            public int hop; // 몇 번째 홉(총구->타겟 또는 타겟->타겟)의 경로인지
        }

        // 메쉬 버퍼는 재생 직전 항상 Clear 후 채우므로 인스턴스끼리 공유해도 안전하다(메인 스레드 단일 실행)
        private static readonly List<Vector3> meshVertices = new List<Vector3>(2048);
        private static readonly List<Color32> meshColors = new List<Color32>(2048);
        private static readonly List<int> meshTriangles = new List<int>(3072);
        private static readonly int PropBoost = Shader.PropertyToID("_Boost");

        //내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private MaterialPropertyBlock propBlock;

        //상태 변수
        private readonly List<Vector3> hopPoints = new List<Vector3>(16); // 월드 좌표 경로점(총구, 타겟1, 타겟2...)
        private readonly List<Vector2Int> polyPts = new List<Vector2Int>(256); // 픽셀 좌표 폴리라인 점 전체
        private readonly List<PolyRange> polys = new List<PolyRange>(64);
        private int hopCount;
        private float elapsed;
        private float tickTimer;
        private int hashSeed;
        private bool bInitialized;
        private bool bColorAssigned;
        private Matrix4x4 worldToLocal;
        private float meshZ;

        private float boost = 2f;
        private Color32 outerColor;
        private Color32 bodyColor;
        private Color32 coreColor;

        /// <summary>
        /// 색상과 HDR Intensity(노출값)를 지정한다. VFX_LightningZap.SetColor(Color, float)와 같은 규약으로
        /// RGB 발광 배율은 2^_intensity다(색 자체는 정점 색으로, 발광 배율은 머티리얼 _Boost로 나눠 전달).
        /// </summary>
        public void SetColor(Color _baseColor, float _intensity)
        {
            boost = Mathf.Pow(2f, _intensity);
            ComputeColors(_baseColor);
            bColorAssigned = true;
        }

        /// <summary>
        /// N개의 점을 이어주는 체인 번개 재생(총구 -> 타겟1 -> 타겟2...). 재생 중에 다시 부르면 처음부터 다시 터진다.
        /// </summary>
        public void PlayZap(IReadOnlyList<Vector3> _points, int _count)
        {
            if (null == _points) return;

            int count = Mathf.Min(_count, _points.Count);
            if (2 > count) return;

            hopPoints.Clear();
            for (int i = 0; i < count; i++)
            {
                hopPoints.Add(_points[i]);
            }

            BeginPlay();
        }

        /// <summary>
        /// 시작점(A)과 끝점(B) 단일 번개 재생
        /// </summary>
        public void PlayZap(Vector3 _startPos, Vector3 _endPos)
        {
            hopPoints.Clear();
            hopPoints.Add(_startPos);
            hopPoints.Add(_endPos);

            BeginPlay();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 플레이 모드에서 컴포넌트 우클릭 메뉴로 실행하는 테스트 발사(현재 위치에서 오른쪽 위로 5유닛)
        /// </summary>
        [ContextMenu("Test Fire (PlayZap)")]
        public void TestFire()
        {
            if (false == Application.isPlaying)
            {
                Debug.LogWarning("Test Fire는 플레이 모드에서만 작동합니다.");
                return;
            }

            PlayZap(transform.position, transform.position + new Vector3(5f, 1.5f, 0f));
        }
#endif

        private float TotalDuration => burstDuration + thinDuration + dissolveDuration;

        private bool IsDissolving => burstDuration + thinDuration <= elapsed;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            mesh = new Mesh { name = "VFX_DroneLaser" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            propBlock = new MaterialPropertyBlock();

            if (null != laserMaterial)
            {
                meshRenderer.sharedMaterial = laserMaterial;
            }
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;

            if (false == bColorAssigned)
            {
                SetColor(defaultColor, defaultIntensity);
            }
        }

        private void ComputeColors(Color _baseColor)
        {
            bodyColor = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 1f);
            outerColor = new Color(_baseColor.r * outerBrightness, _baseColor.g * outerBrightness, _baseColor.b * outerBrightness, 1f);
            Color core = Color.Lerp(_baseColor, Color.white, coreWhiteMix);
            coreColor = new Color(core.r, core.g, core.b, 1f);
        }

        private void BeginPlay()
        {
            EnsureInitialized();

            if (false == gameObject.activeSelf) gameObject.SetActive(true);

            hopCount = hopPoints.Count - 1;
            elapsed = 0f;
            tickTimer = 0f;
            hashSeed = Random.Range(1, int.MaxValue);

            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(PropBoost, boost);
            meshRenderer.SetPropertyBlock(propBlock);

            meshRenderer.enabled = true;
            enabled = true;

            BuildPaths();
            RebuildMesh();
        }

        private void Finish()
        {
            mesh.Clear();
            meshRenderer.enabled = false;
            enabled = false;
        }

        // ---------- 타임라인 ----------

        // ② 구간 진행도(0~1)에 빠르게 시작해 천천히 끝나는 이즈 아웃을 건 값
        private float GetThinEase()
        {
            float u = Mathf.Clamp01((elapsed - burstDuration) / Mathf.Max(thinDuration, 0.0001f));
            return 1f - (1f - u) * (1f - u);
        }

        private int GetCurrentWidth()
        {
            if (elapsed < burstDuration) return burstWidthPx;
            if (true == IsDissolving) return 1;

            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(burstWidthPx, 1f, GetThinEase())));
        }

        private float GetCurrentAmplitude()
        {
            if (elapsed < burstDuration) return burstAmplitudePx;
            if (true == IsDissolving) return tailAmplitudePx;

            return Mathf.Lerp(burstAmplitudePx, tailAmplitudePx, GetThinEase());
        }

        private int GetCurrentBranchCount()
        {
            if (elapsed < burstDuration) return burstBranchCount;
            if (true == IsDissolving) return 0;

            return Mathf.RoundToInt(Mathf.Lerp(burstBranchCount, 0f, GetThinEase()));
        }

        private float GetDissolveProgress()
        {
            return Mathf.Clamp01((elapsed - burstDuration - thinDuration) / Mathf.Max(dissolveDuration, 0.0001f));
        }

        // ---------- 경로 생성 ----------

        private static Vector2Int ToPixel(Vector3 _world)
        {
            return new Vector2Int(Mathf.RoundToInt(_world.x * PixelsPerUnit), Mathf.RoundToInt(_world.y * PixelsPerUnit));
        }

        private void BuildPaths()
        {
            polyPts.Clear();
            polys.Clear();

            bool bDissolving = IsDissolving;
            float amplitude = GetCurrentAmplitude();
            int branchTarget = GetCurrentBranchCount();

            for (int h = 0; h < hopCount; h++)
            {
                Vector2Int a = ToPixel(hopPoints[h]);
                Vector2Int b = ToPixel(hopPoints[h + 1]);

                int mainIndex = polys.Count;
                AddJaggedPoly(a, b, amplitude, KindMain, h);

                if (true == bDissolving) continue;

                for (int s = 0; s < strandCount; s++)
                {
                    AddJaggedPoly(a, b, amplitude * strandAmplitudeScale, KindStrand, h);
                }

                for (int br = 0; br < branchTarget; br++)
                {
                    AddBranch(polys[mainIndex], amplitude, h);
                }
            }
        }

        // a -> b 사이를 pathStepPx 간격으로 나눠 수직으로 꺾는다. 꺾는 방향은 대부분 번갈아 뒤집어(각진 번개 형태)
        // 양 끝은 sin 엔벨로프로 0에 수렴시켜 총구와 타겟에 정확히 붙인다.
        private void AddJaggedPoly(Vector2Int _a, Vector2Int _b, float _amplitude, int _kind, int _hop)
        {
            int start = polyPts.Count;
            polyPts.Add(_a);

            Vector2 delta = new Vector2(_b.x - _a.x, _b.y - _a.y);
            float length = delta.magnitude;

            if (1f <= length)
            {
                Vector2 dir = delta / length;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                int steps = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(1, pathStepPx)));
                float sign = 0.5f > Random.value ? -1f : 1f;

                for (int i = 1; i < steps; i++)
                {
                    float envelope = Mathf.Sin(Mathf.PI * i / steps);
                    if (0.75f > Random.value) sign = -sign;

                    float magnitude = Random.Range(0.35f, 1f) * _amplitude * envelope;
                    Vector2 p = new Vector2(_a.x, _a.y) + dir * (length * i / steps) + perp * (sign * magnitude);
                    polyPts.Add(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
                }
            }

            polyPts.Add(_b);

            polys.Add(new PolyRange { start = start, count = polyPts.Count - start, kind = _kind, hop = _hop });
        }

        // 메인 빔 중간의 한 점에서 25~55도 꺾어 짧게 튀어나가는 스파크 가지
        private void AddBranch(PolyRange _main, float _amplitude, int _hop)
        {
            if (3 > _main.count) return;

            int i = Random.Range(1, _main.count - 1);
            Vector2Int origin = polyPts[_main.start + i];
            Vector2Int prev = polyPts[_main.start + i - 1];
            Vector2Int next = polyPts[_main.start + i + 1];

            Vector2 tangent = new Vector2(next.x - prev.x, next.y - prev.y);
            if (0.0001f > tangent.sqrMagnitude) return;
            tangent.Normalize();

            float angle = Random.Range(25f, 55f) * Mathf.Deg2Rad * (0.5f > Random.value ? -1f : 1f);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            Vector2 dir = new Vector2(tangent.x * cos - tangent.y * sin, tangent.x * sin + tangent.y * cos);

            int length = Random.Range(branchLengthMinPx, branchLengthMaxPx + 1);
            Vector2Int end = new Vector2Int(origin.x + Mathf.RoundToInt(dir.x * length), origin.y + Mathf.RoundToInt(dir.y * length));

            AddJaggedPoly(origin, end, _amplitude * 0.4f, KindBranch, _hop);
        }

        // ---------- 메쉬 생성 ----------

        private void RebuildMesh()
        {
            meshVertices.Clear();
            meshColors.Clear();
            meshTriangles.Clear();

            worldToLocal = transform.worldToLocalMatrix;
            meshZ = transform.position.z;

            if (false == IsDissolving)
            {
                int width = GetCurrentWidth();

                DrawPolys(KindMain, width + glowPaddingPx, outerColor);
                DrawPolys(KindMain, width, bodyColor);
                if (2 <= width)
                {
                    DrawPolys(KindMain, Mathf.Max(1, width - 2), coreColor);
                }
                DrawPolys(KindStrand, 1, coreColor);
                DrawPolys(KindBranch, 1, coreColor);
                DrawFlashes();
            }
            else
            {
                DrawDots();
            }

            mesh.Clear();
            mesh.SetVertices(meshVertices);
            mesh.SetColors(meshColors);
            mesh.SetTriangles(meshTriangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 200f, 10f)); // 절차적 메쉬라 컬링 오차를 막기 위해 넉넉하게 잡는다
        }

        private void DrawPolys(int _kind, int _thickness, Color32 _color)
        {
            for (int p = 0; p < polys.Count; p++)
            {
                PolyRange poly = polys[p];
                if (_kind != poly.kind) continue;

                for (int s = 0; s < poly.count - 1; s++)
                {
                    Vector2Int a = polyPts[poly.start + s];
                    Vector2Int b = polyPts[poly.start + s + 1];
                    DrawSegmentRuns(a.x, a.y, b.x, b.y, _thickness, _color);
                }
            }
        }

        // 선분을 장축 기준으로 한 칸씩 진행하며, 단축 좌표가 같은 구간을 하나의 직사각형으로 합쳐 쿼드 수를 줄인다.
        private void DrawSegmentRuns(int _x0, int _y0, int _x1, int _y1, int _thickness, Color32 _color)
        {
            int dx = _x1 - _x0;
            int dy = _y1 - _y0;
            int adx = Mathf.Abs(dx);
            int ady = Mathf.Abs(dy);

            if (0 == adx && 0 == ady)
            {
                FlushRun(_x0, _x0, _y0, true, _thickness, _color);
                return;
            }

            bool bXMajor = adx >= ady;
            int n = bXMajor ? adx : ady;
            int major0 = bXMajor ? _x0 : _y0;
            int minor0 = bXMajor ? _y0 : _x0;
            int majorSign = bXMajor ? (0 <= dx ? 1 : -1) : (0 <= dy ? 1 : -1);
            int minorDelta = bXMajor ? dy : dx;

            int runStart = major0;
            int runMinor = minor0;

            for (int i = 1; i <= n; i++)
            {
                int major = major0 + majorSign * i;
                int minor = minor0 + Mathf.RoundToInt((float)minorDelta * i / n);

                if (minor != runMinor)
                {
                    FlushRun(runStart, major - majorSign, runMinor, bXMajor, _thickness, _color);
                    runStart = major;
                    runMinor = minor;
                }
            }

            FlushRun(runStart, major0 + majorSign * n, runMinor, bXMajor, _thickness, _color);
        }

        private void FlushRun(int _majorA, int _majorB, int _minor, bool _bXMajor, int _thickness, Color32 _color)
        {
            int majorMin = Mathf.Min(_majorA, _majorB);
            int majorMax = Mathf.Max(_majorA, _majorB) + 1;
            int minorMin = _minor - _thickness / 2;
            int minorMax = minorMin + _thickness;

            if (true == _bXMajor)
            {
                AddPixelRect(majorMin, minorMin, majorMax, minorMax, _color);
            }
            else
            {
                AddPixelRect(minorMin, majorMin, minorMax, majorMax, _color);
            }
        }

        private void DrawFlashes()
        {
            float total = burstDuration + thinDuration;
            int radius = Mathf.RoundToInt(flashRadiusPx * (1f - Mathf.Clamp01(elapsed / Mathf.Max(total, 0.0001f))));
            if (1 > radius) return;

            DrawCross(ToPixel(hopPoints[0]), radius);
            for (int h = 1; h < hopPoints.Count; h++)
            {
                DrawCross(ToPixel(hopPoints[h]), radius);
            }
        }

        private void DrawCross(Vector2Int _center, int _radius)
        {
            AddPixelRect(_center.x - _radius, _center.y, _center.x + _radius + 1, _center.y + 1, coreColor);
            AddPixelRect(_center.x, _center.y - _radius, _center.x + 1, _center.y + _radius + 1, coreColor);
            if (2 <= _radius)
            {
                AddPixelRect(_center.x - 1, _center.y - 1, _center.x + 2, _center.y + 2, coreColor);
            }
        }

        // 1픽셀 실선을 도트로 쪼갠다. 간격은 1 -> 2 -> 4(서로 부분집합)로만 벌어져 도트가 엉뚱한 곳에 새로 생기지 않고,
        // 각 도트는 고정된 키(해시 70% + 총구부터의 거리 30%)가 진행도보다 작아지는 순간 탈락한다(총구 쪽이 먼저).
        private void DrawDots()
        {
            float progress = GetDissolveProgress();
            int spacing = 0.3f > progress ? 1 : (0.6f > progress ? 2 : 4);
            int dotSize = 0.5f > progress ? 2 : 1;

            for (int p = 0; p < polys.Count; p++)
            {
                PolyRange poly = polys[p];
                if (KindMain != poly.kind) continue;

                int totalPixels = 1;
                for (int s = 0; s < poly.count - 1; s++)
                {
                    Vector2Int a = polyPts[poly.start + s];
                    Vector2Int b = polyPts[poly.start + s + 1];
                    totalPixels += Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                }

                int index = 0;
                for (int s = 0; s < poly.count - 1; s++)
                {
                    Vector2Int a = polyPts[poly.start + s];
                    Vector2Int b = polyPts[poly.start + s + 1];
                    int dx = b.x - a.x;
                    int dy = b.y - a.y;
                    int n = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

                    for (int i = (0 == s ? 0 : 1); i <= n; i++)
                    {
                        if (0 == index % spacing)
                        {
                            float key = Hash01(poly.hop, index) * 0.7f + (index / (float)totalPixels) * 0.3f;
                            if (key >= progress)
                            {
                                int x = 0 < n ? a.x + Mathf.RoundToInt((float)dx * i / n) : a.x;
                                int y = 0 < n ? a.y + Mathf.RoundToInt((float)dy * i / n) : a.y;
                                int lo = dotSize / 2;
                                Color32 color = key < progress + dimBand ? outerColor : bodyColor;
                                AddPixelRect(x - lo, y - lo, x - lo + dotSize, y - lo + dotSize, color);
                            }
                        }
                        index++;
                    }
                }
            }
        }

        private float Hash01(int _a, int _b)
        {
            unchecked
            {
                uint h = (uint)(_a * 73856093) ^ (uint)(_b * 19349663) ^ (uint)hashSeed;
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 쿼드로 추가한다. 월드 -> 로컬은 행렬 하나로 변환해 드론이 움직여도 월드 위치에 고정된다.
        private void AddPixelRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (MaxVertices <= meshVertices.Count + 4) return;

            int v = meshVertices.Count;
            meshVertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x0 * PixelUnit, _y0 * PixelUnit, meshZ)));
            meshVertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x0 * PixelUnit, _y1 * PixelUnit, meshZ)));
            meshVertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x1 * PixelUnit, _y1 * PixelUnit, meshZ)));
            meshVertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x1 * PixelUnit, _y0 * PixelUnit, meshZ)));

            meshColors.Add(_color);
            meshColors.Add(_color);
            meshColors.Add(_color);
            meshColors.Add(_color);

            meshTriangles.Add(v);
            meshTriangles.Add(v + 1);
            meshTriangles.Add(v + 2);
            meshTriangles.Add(v);
            meshTriangles.Add(v + 2);
            meshTriangles.Add(v + 3);
        }

        private void Awake()
        {
            EnsureInitialized();
            meshRenderer.enabled = false;
            enabled = false; // 재생(PlayZap) 전까지 Update를 돌리지 않는다
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (TotalDuration <= elapsed)
            {
                Finish();
                return;
            }

            tickTimer += Time.deltaTime;
            if (jitterInterval <= tickTimer)
            {
                tickTimer = 0f;
                BuildPaths();
            }

            RebuildMesh();
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
