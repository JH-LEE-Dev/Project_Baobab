using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 로그라이크 오프로드 소환 위치 · 돌진 방향 표시(기획서 v2.8). 소환될 위치가 정해지는 순간(발동 주기 시작)에 Begin으로 시작해
    /// 발동까지 계속 보여 주고, 발동 때 Fire를 부르면 차량이 지나가는 시간에 맞춰 표시가 걷히며 끝나면 스스로 풀에 반환한다.
    ///
    /// 표시는 차량이 지나갈 타일 칸의 윗면 자체를 물들이는 방식이다(선 · 링 · 화살표 그림 없음).
    ///   - 등장: 소환 지점에서 진행 방향으로 칸이 차례로 켜지고, 켜지는 순간 밝았다가 가라앉는다.
    ///   - 유지: 옅은 바탕 + 칸으로 된 꺾쇠 빛이 진행 방향으로 흐른다(바깥 칸은 1칸 늦어 꺾쇠 모양). 마지막 0.8초(SetRemaining을 알린 경우)는 빨라진다.
    ///   - 발동: 소환 지점 칸이 번쩍이고, 차량 앞 5칸이 가장 밝으며 지나간 칸은 4.5칸에 걸쳐 꺼진다. 차량 위치는 Fire의 돌진 시간 비율로 추정한다.
    ///   - 테두리: 켜진 칸 전체(모든 활성 차선의 합집합)의 바깥 가장자리에만 1px 선. 타일 가장자리(2:1)라 계단이 항상 가지런하다.
    ///   - 빛 알갱이: 차선 위에서 `+` 에서 `.` 로 줄며 12px 떠오른다(`+` `.`만 쓰고 매 프레임 경과 시간으로 그린다).
    ///
    /// 구조: 활성 차선 정적 배열의 맨 앞 차선(호스트) 하나만 모든 활성 차선의 칸 밝기를 max로 합쳐 타일 메쉬를 만들어 그린다(차선이 겹쳐도
    /// 교차 구간이 끊기지 않고 안쪽 테두리가 없다). 나머지 차선의 타일 렌더러는 꺼 두고, 호스트가 끝나면 다음 차선이 호스트가 된다.
    /// 시간은 프레임당 한 번 모든 활성 차선을 한꺼번에 진행시킨다(UpdateAllLanes).
    /// 타일 빛은 Indicators(-5)에 그려 타일 위, 모든 그림자 · 다른 바닥 이펙트 · 나무 · 차량 아래에 놓이고, 빛 알갱이는 ShockWave/0에 그린다.
    ///
    /// 칸 좌표: 칸 (i, j)의 윗면 중심 = gridOrigin + ((i - j) * 0.5, (i + j) * 0.25 + tileTopOffsetY). 실제 GroundTilemap(아이소 칸 (1, 0.5))에
    /// 깔린 타일과 어긋남이 없도록 두 값을 측정해 정한 것이다. 차선에 속한 칸은 칸 윗면 중심에서 차선 선분(_from~_to)까지의 거리가 반폭 이하인 칸으로
    /// 실제 피해 판정(나무 위치와 선분 거리 &lt;= 반지름)과 같은 식이다. Application Layer를 건드리지 않고 Begin으로 받은 월드 좌표와 인스펙터 값만 쓴다.
    ///
    /// 수명: Begin -> (유지) -> Fire -> 걷힘이 끝나면 스스로 루트 ParticleSystem을 멈춰 풀에 반환한다. 웨이브/런 종료는 Cancel로 즉시 회수한다.
    /// Fire를 영영 부르지 않아도 MaxHoldSeconds가 지나면 스스로 끝낸다(루트 ParticleSystem 수명 60초 안쪽).
    /// 정적 버퍼만 써서 재생 중 힙 할당이 없다.
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
        [SerializeField, Tooltip("Fire에 돌진 시간을 넘기지 않을 때 쓰는 기본 걷힘 시간(초). 차량이 경로를 지나는 시간에 맞춘다.")]
        private float clearSeconds = 0.8f;

        [Header("타일 격자 (실제 GroundTilemap 타일과 맞춘 측정값)")]
        [SerializeField, Tooltip("칸 (0, 0)의 GetCellCenterWorld(타일 그림 피벗) 월드 좌표. 칸 (i, j)의 피벗 = gridOrigin + ((i - j) * 0.5, (i + j) * 0.25)")]
        private Vector2 gridOrigin = new Vector2(0.0f, 0.25f);
        [SerializeField, Tooltip("타일 그림 피벗에서 윗면 마름모 중심까지의 높이(월드 유닛). 32x32 그림의 위 16줄이 윗면이라 0.25")]
        private float tileTopOffsetY = 0.25f;

        [Header("타일 빛 메쉬 (타일 위 · 그림자 아래)")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("타일(Tiles) 위, 그림자(TreeShadow) · 바닥 이펙트(ShockWave) · 나무 아래여야 한다.")]
        private string tileSortingLayerName = "Indicators";
        [SerializeField] private int tileSortingOrder = -5;

        [Header("빛 알갱이 메쉬 (루트 스크립트의 메쉬)")]
        [SerializeField] private bool bMotes = true;
        [SerializeField, Tooltip("그림자(TreeShadow) 위, 나무/캐릭터가 있는 Objects 아래여야 한다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        public const float GroundSquashY = 0.5f;

        private const float PixelsPerUnit = 32.0f;
        // Fire를 부르지 않은 채 이 시간이 지나면 스스로 끝낸다(루트 ParticleSystem 수명 60초보다 짧아야 한다)
        private const float MaxHoldSeconds = 55.0f;

        // 타일(아이소 마름모 32x16px)과 칸 버퍼
        private const int TileHalfW = 16;
        private const int TileHalfH = 8;
        private const int WindowSize = 64;
        private const int TileQuadCapacity = 1024;
        private const int MaxLaneCells = 640;
        private const int MaxActiveLanes = 16;
        private const float OnThreshold = 0.03f;
        private const float GlowRange = 2.0f;

        // 시안 수치: 등장
        private const float AppearStagger = 0.02f;
        private const float PopDecay = 9.0f;
        // 시안 수치: 유지 꺾쇠
        private const float IdleSpeed = 7.0f;
        private const float ImminentSpeed = 16.0f;
        private const float ImminentSeconds = 0.8f;
        private const float ChevronPeriod = 6.0f;
        private const float ChevronLength = 2.0f;
        private const float InnerBase = 0.1f;
        private const float OuterBase = 0.05f;
        private const float PulseCover = 0.42f;
        private const float PulseGlow = 0.12f;
        private const float PopCover = 0.4f;
        private const float PopGlow = 0.3f;
        private const float ImminentCover = 0.08f;
        private const float ImminentGlow = 0.04f;
        private const float FarFade = 0.5f;
        private const float InnerWidthRatio = 0.5f;
        private const float EndCellMargin = 0.25f;
        // 시안 수치: 발동과 돌진
        private const float WarmSeconds = 0.45f;
        private const float CarStartIndex = 1.0f;
        private const float CarEndInset = 1.4f;
        private const float AheadReach = 5.0f;
        private const float BehindReach = 4.5f;
        private const float SpawnFlashCells = 2.5f;
        private const float FlashDecay = 7.0f;
        private const float TrailMargin = 0.5f;
        // 시안 수치: 빛 알갱이
        private const int MoteCount = 7;
        private const float MoteDelay = 0.4f;
        private const float MoteStep = 0.23f;
        private const float MotePeriod = 1.6f;
        private const float MoteLife = 1.1f;
        private const float MoteAlongRange = 18.0f;
        private const float MoteLateral = 3.2f;
        private const float MoteRise = 12.0f;
        private const int MoteLift = 2;
        private const int MoteColorSteps = 32;
        private const int MoteQuadBudget = 256;
        private const int MoteMeshQuads = 64;

        // 색(시안 앰버). 바탕색/밝은색은 머티리얼 M_OffroadLaneTile의 _BaseColor/_HighColor가 갖고, 빛 알갱이용 밝은색만 여기 둔다.
        private static readonly Color MoteHighColor = new Color(255.0f / 255.0f, 236.0f / 255.0f, 190.0f / 255.0f, 1.0f);
        private const float MoteGlowMin = 1.2f;
        private const float MoteGlowMax = 6.0f;

        [StructLayout(LayoutKind.Sequential)]
        private struct TileVertex
        {
            public Vector3 position;
            public Color32 color;
            public Vector2 uv;
        }

        private static readonly VertexAttributeDescriptor[] TileVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        private const MeshUpdateFlags TileUploadFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;
        private const ulong HashOffset = 14695981039346656037UL;
        private const ulong HashPrime = 1099511628211UL;

        // 활성 차선 목록(고정 배열). 맨 앞이 타일 메쉬를 그리는 호스트다.
        private static readonly VFX_OffroadLane[] activeLanes = new VFX_OffroadLane[MaxActiveLanes];
        private static int activeLaneCount;
        private static int updatedFrame = -1;

        // 칸 밝기 합집합 버퍼(고정 64x64 창)와 타일 메쉬 정점
        private static readonly float[] cellCover = new float[WindowSize * WindowSize];
        private static readonly float[] cellGlow = new float[WindowSize * WindowSize];
        private static readonly TileVertex[] tileVertices = new TileVertex[TileQuadCapacity * 4];
        private static ushort[] tileIndexPattern;
        private static readonly Color32[] moteColors = new Color32[MoteColorSteps];
        private static bool bMoteColorsReady;

        private static readonly PixelQuadBuffer skyBuffer = new PixelQuadBuffer(MoteQuadBudget);

        // 내부 의존성
        private readonly PixelPainter skyPainter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh groundMesh;
        private ParticleSystem rootParticleSystem;

        // 차선이 덮는 칸(Begin에서 한 번 계산)
        private readonly int[] cellI = new int[MaxLaneCells];
        private readonly int[] cellJ = new int[MaxLaneCells];
        private readonly int[] cellA = new int[MaxLaneCells];
        private readonly bool[] cellInner = new bool[MaxLaneCells];
        private int cellCount;
        private int cellMinI;
        private int cellMaxI;
        private int cellMinJ;
        private int cellMaxJ;
        private int maxCellA;

        // 상태 변수
        private float elapsed;
        private bool bFired;
        private float fireElapsed;
        private float clearDuration;
        private float fireDueTime = -1.0f;
        private bool bActive;
        private bool bInitialized;

        private int originPxX;
        private int originPxY;
        private float laneStartI;
        private float laneStartJ;
        private float laneStepI;
        private float laneStepJ;
        private float lanePerpI;
        private float lanePerpJ;
        private float laneLength;
        private int moteSeed;
        private float warmSeconds;
        private float carSpeed;

        // 월드 픽셀 경계(칸 마름모 전체 + 알갱이가 떠오르는 높이)
        private int boundsMinX;
        private int boundsMinY;
        private int boundsMaxX;
        private int boundsMaxY;

        private int tileSortingLayerId;
        private int groundSortingLayerId;
        private ulong skyHash;
        private int skyVertexCount = -1;
        private ulong tileHash;
        private int tileVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 소환 위치와 경로 표시를 시작한다. 소환될 위치가 정해지는 시점(발동 주기 시작)에 부르고, 발동 때 Fire를 부를 때까지 유지된다.
        /// _from/_to는 차량의 출발점과 도착점(월드), _halfWidthUnits는 돌진 폭의 반폭(0 이하이면 인스펙터의 laneHalfWidth).
        /// 풀에서 꺼내 활성화한 직후에 부른다.
        /// </summary>
        public void Begin(Vector3 _from, Vector3 _to, float _halfWidthUnits = 0.0f)
        {
            EnsureInitialized();
            if (MaxActiveLanes <= activeLaneCount && false == IsRegistered()) return;

            float halfWidth = Mathf.Max(0.05f, 0.0f < _halfWidthUnits ? _halfWidthUnits : laneHalfWidth);
            float fromX = _from.x;
            float fromY = _from.y - groundOffset;
            float toX = _to.x;
            float toY = _to.y - groundOffset;

            BuildLaneCells(fromX, fromY, toX, toY, halfWidth);

            moteSeed = Mathf.RoundToInt(fromX * PixelsPerUnit) * 73856093 ^ Mathf.RoundToInt(fromY * PixelsPerUnit) * 19349663;

            elapsed = 0.0f;
            bFired = false;
            fireElapsed = 0.0f;
            clearDuration = clearSeconds;
            fireDueTime = -1.0f;
            bActive = true;
            skyVertexCount = -1;
            tileVertexCount = -1;

            RegisterLane();
        }

        /// <summary>
        /// 발동한다(차량 소환). 소환 지점 칸이 번쩍이고 차선이 밝아지며 차량이 지나가는 시간에 맞춰 소환 지점부터 걷힌다.
        /// _dashSeconds는 차량이 경로를 지나는 시간(소환 예열 포함)이며 0 이하이면 인스펙터의 clearSeconds. 걷힘이 끝나면 스스로 풀에 반환한다.
        /// 이미 발동했거나 시작 전이면 아무 일도 하지 않는다.
        /// </summary>
        public void Fire(float _dashSeconds = 0.0f)
        {
            if (false == bActive || true == bFired) return;

            bFired = true;
            fireElapsed = 0.0f;
            clearDuration = Mathf.Max(0.1f, 0.0f < _dashSeconds ? _dashSeconds : clearSeconds);
            warmSeconds = Mathf.Min(WarmSeconds, clearDuration * 0.5f);

            float carEnd = Mathf.Max(CarStartIndex + 1.0f, laneLength - CarEndInset);
            carSpeed = (carEnd - CarStartIndex) / Mathf.Max(0.05f, clearDuration - warmSeconds);
        }

        /// <summary>
        /// 발동까지 남은 시간(초)을 알린다(선택). 알리면 마지막 0.8초에 꺾쇠 빛이 빨라지고 바탕이 조금 진해진다. 주기가 바뀌면(레벨, 공격속도)
        /// 다시 불러 갱신한다. 한 번도 부르지 않으면 임박 연출 없이 유지된다.
        /// </summary>
        public void SetRemaining(float _seconds)
        {
            fireDueTime = elapsed + Mathf.Max(0.0f, _seconds);
        }

        /// <summary>웨이브/런 종료 등으로 발동 없이 표시를 지운다. 연출 없이 즉시 풀에 반환한다.</summary>
        public void Cancel()
        {
            if (false == bActive) return;

            Finish();
        }

        // 렌더러를 끄고 루트 ParticleSystem을 멈춘다(VFXComponent가 풀에 반환한다)
        private void Finish()
        {
            bActive = false;
            UnregisterLane();
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
            if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;

            if (null != rootParticleSystem && true == rootParticleSystem.isPlaying) rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private bool IsRegistered()
        {
            for (int i = 0; i < activeLaneCount; i++)
            {
                if (this == activeLanes[i]) return true;
            }

            return false;
        }

        private void RegisterLane()
        {
            if (true == IsRegistered()) return;
            if (MaxActiveLanes <= activeLaneCount) return;

            activeLanes[activeLaneCount++] = this;
        }

        private void UnregisterLane()
        {
            for (int i = 0; i < activeLaneCount; i++)
            {
                if (this != activeLanes[i]) continue;

                activeLanes[i] = activeLanes[--activeLaneCount];
                activeLanes[activeLaneCount] = null;
                return;
            }
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;

            mesh = new Mesh { name = "VFX_OffroadLane" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, MoteMeshQuads);

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
            groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = new Mesh { name = "VFX_OffroadLane_Tiles" };
                groundMesh.MarkDynamic();
                SetupTileMesh(groundMesh);
                groundMeshFilter.sharedMesh = groundMesh;

                groundMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                groundMeshRenderer.receiveShadows = false;
                groundMeshRenderer.enabled = false;

                tileSortingLayerId = SortingLayer.NameToID(tileSortingLayerName);
            }

            if (false == bMoteColorsReady)
            {
                bMoteColorsReady = true;
                for (int i = 0; i < MoteColorSteps; i++) moteColors[i] = MakeMoteColor((i + 0.5f) / MoteColorSteps);
            }
        }

        // 정점 버퍼(위치 + 색 + uv 한 스트림)와 쿼드 인덱스 패턴을 용량만큼 잡는다(재생 중 다시 잡지 않는다)
        private static void SetupTileMesh(Mesh _mesh)
        {
            if (null == tileIndexPattern)
            {
                tileIndexPattern = new ushort[TileQuadCapacity * 6];
                for (int q = 0; q < TileQuadCapacity; q++)
                {
                    int v = q * 4;
                    int n = q * 6;
                    tileIndexPattern[n] = (ushort)v;
                    tileIndexPattern[n + 1] = (ushort)(v + 1);
                    tileIndexPattern[n + 2] = (ushort)(v + 2);
                    tileIndexPattern[n + 3] = (ushort)v;
                    tileIndexPattern[n + 4] = (ushort)(v + 2);
                    tileIndexPattern[n + 5] = (ushort)(v + 3);
                }
            }

            _mesh.SetVertexBufferParams(TileQuadCapacity * 4, TileVertexLayout);
            _mesh.SetIndexBufferParams(TileQuadCapacity * 6, IndexFormat.UInt16);
            _mesh.SetIndexBufferData(tileIndexPattern, 0, 0, TileQuadCapacity * 6, TileUploadFlags);
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0, MeshTopology.Triangles), TileUploadFlags);
        }

        // 빛 알갱이 색: 시안의 gamma 공간 밝기 k(1.25 -> 0.85)를 선형 HDR 목표값으로 바꿔 BrandWrapGlow(GlowMin~GlowMax)의 정점 색 + 알파로 푼다.
        private static Color32 MakeMoteColor(float _age)
        {
            float k = 1.25f - _age * 0.4f;
            float r = Mathf.GammaToLinearSpace(MoteHighColor.r * k);
            float g = Mathf.GammaToLinearSpace(MoteHighColor.g * k);
            float b = Mathf.GammaToLinearSpace(MoteHighColor.b * k);
            float glow = Mathf.Clamp(Mathf.Max(MoteGlowMin, Mathf.Max(r, Mathf.Max(g, b))), MoteGlowMin, MoteGlowMax);
            float alpha = (glow - MoteGlowMin) / (MoteGlowMax - MoteGlowMin);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(r / glow) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(g / glow) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(b / glow) * 255.0f),
                (byte)Mathf.RoundToInt(alpha * 255.0f));
        }

        // ---------- 차선 칸 계산 ----------

        // 월드 좌표를 칸 격자의 소수 좌표(i, j)로 바꾼다. 칸 (i, j) 윗면 중심 픽셀 = (originPxX + 16 * (i - j), originPxY + 8 * (i + j)).
        private void WorldToGrid(float _x, float _y, out float _i, out float _j)
        {
            float px = _x * PixelsPerUnit - originPxX;
            float py = _y * PixelsPerUnit - originPxY;
            _i = px / (TileHalfW * 2.0f) + py / (TileHalfH * 2.0f);
            _j = py / (TileHalfH * 2.0f) - px / (TileHalfW * 2.0f);
        }

        private static float DistanceToSegment(float _px, float _py, float _ax, float _ay, float _bx, float _by)
        {
            float abx = _bx - _ax;
            float aby = _by - _ay;
            float lengthSq = abx * abx + aby * aby;
            float t = 0.0f < lengthSq ? Mathf.Clamp01(((_px - _ax) * abx + (_py - _ay) * aby) / lengthSq) : 0.0f;
            float dx = _px - (_ax + abx * t);
            float dy = _py - (_ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // 차선에 속한 칸(윗면 중심에서 선분까지의 거리 <= 반폭)을 모으고, 칸마다 진행 방향 번호(a)와 안쪽 칸 여부를 정한다.
        private void BuildLaneCells(float _fromX, float _fromY, float _toX, float _toY, float _halfWidth)
        {
            originPxX = Mathf.RoundToInt(gridOrigin.x * PixelsPerUnit);
            originPxY = Mathf.RoundToInt((gridOrigin.y + tileTopOffsetY) * PixelsPerUnit);

            // 차선(_from~_to)과 나무 위치는 칸 중심(타일 그림 피벗) 기준이고 빛은 윗면에 그리므로, 선분을 윗면 높이만큼 올려 윗면 중심과 비교한다
            _fromY += tileTopOffsetY;
            _toY += tileTopOffsetY;

            float fromI;
            float fromJ;
            float toI;
            float toJ;
            WorldToGrid(_fromX, _fromY, out fromI, out fromJ);
            WorldToGrid(_toX, _toY, out toI, out toJ);

            float deltaI = toI - fromI;
            float deltaJ = toJ - fromJ;
            laneLength = Mathf.Max(1.0f, Mathf.Max(Mathf.Abs(deltaI), Mathf.Abs(deltaJ)));
            laneStartI = fromI;
            laneStartJ = fromJ;
            laneStepI = deltaI / laneLength;
            laneStepJ = deltaJ / laneLength;
            lanePerpI = -laneStepJ;
            lanePerpJ = laneStepI;

            // 선분 + 반폭을 감싸는 월드 직사각형의 네 모서리를 격자 좌표로 바꿔 후보 칸 범위를 잡는다
            float minX = Mathf.Min(_fromX, _toX) - _halfWidth;
            float maxX = Mathf.Max(_fromX, _toX) + _halfWidth;
            float minY = Mathf.Min(_fromY, _toY) - _halfWidth;
            float maxY = Mathf.Max(_fromY, _toY) + _halfWidth;

            float scanMinI = float.MaxValue;
            float scanMaxI = float.MinValue;
            float scanMinJ = float.MaxValue;
            float scanMaxJ = float.MinValue;

            for (int corner = 0; corner < 4; corner++)
            {
                float ci;
                float cj;
                WorldToGrid(0 == (corner & 1) ? minX : maxX, 0 == (corner & 2) ? minY : maxY, out ci, out cj);
                if (ci < scanMinI) scanMinI = ci;
                if (scanMaxI < ci) scanMaxI = ci;
                if (cj < scanMinJ) scanMinJ = cj;
                if (scanMaxJ < cj) scanMaxJ = cj;
            }

            int startI = Mathf.FloorToInt(scanMinI) - 1;
            int endI = Mathf.CeilToInt(scanMaxI) + 1;
            int startJ = Mathf.FloorToInt(scanMinJ) - 1;
            int endJ = Mathf.CeilToInt(scanMaxJ) + 1;

            cellCount = 0;
            cellMinI = int.MaxValue;
            cellMaxI = int.MinValue;
            cellMinJ = int.MaxValue;
            cellMaxJ = int.MinValue;
            maxCellA = 0;

            int pixelMinX = int.MaxValue;
            int pixelMinY = int.MaxValue;
            int pixelMaxX = int.MinValue;
            int pixelMaxY = int.MinValue;
            float innerWidth = _halfWidth * InnerWidthRatio;

            for (int i = startI; i <= endI; i++)
            {
                for (int j = startJ; j <= endJ; j++)
                {
                    if (MaxLaneCells <= cellCount) break;

                    int cx = originPxX + TileHalfW * (i - j);
                    int cy = originPxY + TileHalfH * (i + j);
                    float distance = DistanceToSegment(cx / PixelsPerUnit, cy / PixelsPerUnit, _fromX, _fromY, _toX, _toY);
                    if (_halfWidth < distance) continue;

                    // 선분 양 끝을 반폭만큼 넘어간 칸(도착 쪽은 섬 밖 물 위)은 켜지 않는다
                    float along = (i - fromI) * laneStepI + (j - fromJ) * laneStepJ;
                    if (along < -EndCellMargin || laneLength + EndCellMargin < along) continue;

                    int a = Mathf.Max(0, Mathf.RoundToInt(along));
                    cellI[cellCount] = i;
                    cellJ[cellCount] = j;
                    cellA[cellCount] = a;
                    cellInner[cellCount] = distance <= innerWidth;
                    cellCount++;

                    if (i < cellMinI) cellMinI = i;
                    if (cellMaxI < i) cellMaxI = i;
                    if (j < cellMinJ) cellMinJ = j;
                    if (cellMaxJ < j) cellMaxJ = j;
                    if (maxCellA < a) maxCellA = a;
                    if (cx - TileHalfW < pixelMinX) pixelMinX = cx - TileHalfW;
                    if (cy - TileHalfH < pixelMinY) pixelMinY = cy - TileHalfH;
                    if (pixelMaxX < cx + TileHalfW) pixelMaxX = cx + TileHalfW;
                    if (pixelMaxY < cy + TileHalfH) pixelMaxY = cy + TileHalfH;
                }
            }

            if (0 == cellCount)
            {
                cellMinI = cellMaxI = cellMinJ = cellMaxJ = 0;
                pixelMinX = Mathf.FloorToInt(Mathf.Min(_fromX, _toX) * PixelsPerUnit);
                pixelMaxX = Mathf.CeilToInt(Mathf.Max(_fromX, _toX) * PixelsPerUnit);
                pixelMinY = Mathf.FloorToInt(Mathf.Min(_fromY, _toY) * PixelsPerUnit);
                pixelMaxY = Mathf.CeilToInt(Mathf.Max(_fromY, _toY) * PixelsPerUnit);
            }

            boundsMinX = pixelMinX;
            boundsMinY = pixelMinY;
            boundsMaxX = pixelMaxX;
            boundsMaxY = pixelMaxY + Mathf.RoundToInt(MoteRise) + MoteLift + 2;
        }

        // ---------- 프레임 갱신 ----------

        // 프레임당 한 번 모든 활성 차선의 시간을 진행시키고, 호스트가 타일 메쉬를, 각 차선이 빛 알갱이 메쉬를 다시 만든다.
        private static void UpdateAllLanes(float _dt)
        {
            // 끝난 차선은 목록에서 빠진다. 뒤에서부터 돌면 바꿔 들어온 마지막 차선은 이미 처리한 것이다.
            for (int i = activeLaneCount - 1; i >= 0; i--)
            {
                VFX_OffroadLane lane = activeLanes[i];
                if (null == lane) continue;

                lane.Tick(_dt);
            }

            if (0 == activeLaneCount) return;

            for (int i = 1; i < activeLaneCount; i++)
            {
                VFX_OffroadLane other = activeLanes[i];
                if (null != other && null != other.groundMeshRenderer && true == other.groundMeshRenderer.enabled) other.groundMeshRenderer.enabled = false;
            }

            ComposeTiles(activeLanes[0]);

            for (int i = 0; i < activeLaneCount; i++)
            {
                VFX_OffroadLane lane = activeLanes[i];
                if (null != lane) lane.RebuildMotes();
            }
        }

        private void Tick(float _dt)
        {
            elapsed += _dt;
            if (true == bFired) fireElapsed += _dt;

            if (true == bFired ? CarAlong() >= maxCellA + BehindReach + TrailMargin : MaxHoldSeconds <= elapsed)
            {
                Finish();
                return;
            }

            ApplySorting();
        }

        // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 두 렌더러를 각자 레이어로 다시 맞춘다
        private void ApplySorting()
        {
            if (null != groundMeshRenderer)
            {
                if (groundMeshRenderer.sortingLayerID != tileSortingLayerId) groundMeshRenderer.sortingLayerID = tileSortingLayerId;
                if (groundMeshRenderer.sortingOrder != tileSortingOrder) groundMeshRenderer.sortingOrder = tileSortingOrder;
            }

            if (meshRenderer.sortingLayerID != groundSortingLayerId) meshRenderer.sortingLayerID = groundSortingLayerId;
            if (meshRenderer.sortingOrder != groundSortingOrder) meshRenderer.sortingOrder = groundSortingOrder;
        }

        // 차량이 있다고 보는 진행 번호. Fire의 돌진 시간 비율로 추정한다(소환 예열 동안은 소환 지점).
        private float CarAlong()
        {
            return CarStartIndex + carSpeed * Mathf.Max(0.0f, fireElapsed - warmSeconds);
        }

        // 이 차선의 칸 밝기를 합집합 버퍼에 max로 쌓는다(시안 tileLevel + fillTileLevels)
        private void AccumulateCells(int _windowI0, int _windowJ0, int _spanI, int _spanJ)
        {
            bool bImminent = 0.0f <= fireDueTime && fireDueTime - elapsed < ImminentSeconds;
            float phase = elapsed * (true == bImminent ? ImminentSpeed : IdleSpeed);
            float aheadCar = CarAlong();
            float travelled = fireElapsed - warmSeconds;
            float flash = Mathf.Exp(-fireElapsed * FlashDecay);

            for (int n = 0; n < cellCount; n++)
            {
                int wi = cellI[n] - _windowI0;
                int wj = cellJ[n] - _windowJ0;
                if (wi < 0 || _spanI <= wi || wj < 0 || _spanJ <= wj) continue;

                int a = cellA[n];
                bool bInner = cellInner[n];
                float cover;
                float glow;

                if (false == bFired)
                {
                    float on = elapsed - a * AppearStagger;
                    if (on <= 0.0f) continue;

                    float pop = on * PopDecay < 9.0f ? Mathf.Exp(-on * PopDecay) : 0.0f;
                    float d = Mathf.Repeat(phase - (a + (true == bInner ? 0 : 1)), ChevronPeriod);
                    float pulse = d < ChevronLength ? 1.0f - d / ChevronLength : 0.0f;
                    pulse *= 1.0f - FarFade * a / laneLength;

                    cover = (true == bInner ? InnerBase : OuterBase) + PulseCover * pulse + PopCover * pop;
                    glow = PulseGlow * pulse + PopGlow * pop;
                    if (true == bImminent)
                    {
                        cover += ImminentCover;
                        glow += ImminentGlow;
                    }
                }
                else if (a > aheadCar + 0.5f)
                {
                    float near = Mathf.Max(0.0f, 1.0f - (a - aheadCar) / AheadReach);
                    cover = 0.2f + (true == bInner ? 0.1f : 0.0f) + 0.35f * near;
                    glow = 0.04f + 0.3f * near * (0.0f < travelled ? 1.0f : 0.4f);
                    if (a < SpawnFlashCells)
                    {
                        cover += 0.4f * flash;
                        glow += 0.5f * flash;
                    }
                }
                else if (travelled <= 0.0f)
                {
                    cover = 0.35f + 0.4f * flash;
                    glow = 0.1f + 0.5f * flash;
                }
                else
                {
                    float behind = (aheadCar - a) / BehindReach;
                    if (1.0f <= behind) continue;

                    cover = 0.4f * (1.0f - behind) * (1.0f - behind);
                    glow = 0.12f * (1.0f - behind);
                }

                int index = wi * WindowSize + wj;
                if (1.0f < cover) cover = 1.0f;
                if (cover > cellCover[index]) cellCover[index] = cover;
                if (glow > cellGlow[index]) cellGlow[index] = glow;
            }
        }

        private static bool IsCellOn(int _wi, int _wj, int _spanI, int _spanJ)
        {
            if (_wi < 0 || _spanI <= _wi || _wj < 0 || _spanJ <= _wj) return false;

            return OnThreshold <= cellCover[_wi * WindowSize + _wj];
        }

        // 모든 활성 차선의 칸 밝기를 합쳐 호스트의 타일 메쉬를 다시 만든다. 켜진 칸 하나당 쿼드 하나, 이웃 칸이 꺼진 변이 테두리다.
        private static void ComposeTiles(VFX_OffroadLane _host)
        {
            if (null == _host.groundMesh) return;

            int minI = int.MaxValue;
            int maxI = int.MinValue;
            int minJ = int.MaxValue;
            int maxJ = int.MinValue;
            int pixelMinX = int.MaxValue;
            int pixelMinY = int.MaxValue;
            int pixelMaxX = int.MinValue;
            int pixelMaxY = int.MinValue;

            for (int i = 0; i < activeLaneCount; i++)
            {
                VFX_OffroadLane lane = activeLanes[i];
                if (null == lane) continue;

                if (lane.cellMinI < minI) minI = lane.cellMinI;
                if (maxI < lane.cellMaxI) maxI = lane.cellMaxI;
                if (lane.cellMinJ < minJ) minJ = lane.cellMinJ;
                if (maxJ < lane.cellMaxJ) maxJ = lane.cellMaxJ;
                if (lane.boundsMinX < pixelMinX) pixelMinX = lane.boundsMinX;
                if (lane.boundsMinY < pixelMinY) pixelMinY = lane.boundsMinY;
                if (pixelMaxX < lane.boundsMaxX) pixelMaxX = lane.boundsMaxX;
                if (pixelMaxY < lane.boundsMaxY) pixelMaxY = lane.boundsMaxY;
            }

            Vector3 cullingCenter = new Vector3((pixelMinX + pixelMaxX) * 0.5f / PixelsPerUnit, (pixelMinY + pixelMaxY) * 0.5f / PixelsPerUnit, 0.0f);
            Vector3 cullingSize = new Vector3((pixelMaxX - pixelMinX) / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, (pixelMaxY - pixelMinY) / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(cullingCenter, cullingSize))
            {
                if (true == _host.groundMeshRenderer.enabled) _host.groundMeshRenderer.enabled = false;
                _host.tileVertexCount = -1;
                return;
            }

            int windowI0 = minI - 1;
            int windowJ0 = minJ - 1;
            int spanI = Mathf.Min(WindowSize, maxI - windowI0 + 2);
            int spanJ = Mathf.Min(WindowSize, maxJ - windowJ0 + 2);

            System.Array.Clear(cellCover, 0, spanI * WindowSize);
            System.Array.Clear(cellGlow, 0, spanI * WindowSize);

            for (int i = 0; i < activeLaneCount; i++)
            {
                VFX_OffroadLane lane = activeLanes[i];
                if (null != lane) lane.AccumulateCells(windowI0, windowJ0, spanI, spanJ);
            }

            Matrix4x4 worldToLocal = _host.groundMeshRenderer.transform.worldToLocalMatrix;
            float translationX = worldToLocal.m03;
            float translationY = worldToLocal.m13;
            ulong hash = HashOffset;
            hash = MixHash(hash, unchecked((uint)windowI0));
            hash = MixHash(hash, unchecked((uint)windowJ0));
            hash = MixHash(hash, unchecked((uint)Mathf.RoundToInt(translationX * PixelsPerUnit * 16.0f)));
            hash = MixHash(hash, unchecked((uint)Mathf.RoundToInt(translationY * PixelsPerUnit * 16.0f)));

            int quadCount = 0;
            int builtMinX = int.MaxValue;
            int builtMinY = int.MaxValue;
            int builtMaxX = int.MinValue;
            int builtMaxY = int.MinValue;

            for (int wi = 0; wi < spanI; wi++)
            {
                for (int wj = 0; wj < spanJ; wj++)
                {
                    int index = wi * WindowSize + wj;
                    float cover = cellCover[index];
                    if (cover < OnThreshold) continue;
                    if (TileQuadCapacity <= quadCount) break;

                    int flags = 0;
                    if (false == IsCellOn(wi + 1, wj, spanI, spanJ)) flags |= 1;
                    if (false == IsCellOn(wi, wj + 1, spanI, spanJ)) flags |= 2;
                    if (false == IsCellOn(wi - 1, wj, spanI, spanJ)) flags |= 4;
                    if (false == IsCellOn(wi, wj - 1, spanI, spanJ)) flags |= 8;
                    if (false == IsCellOn(wi + 1, wj + 1, spanI, spanJ)) flags |= 16;
                    if (false == IsCellOn(wi - 1, wj - 1, spanI, spanJ)) flags |= 32;

                    byte coverByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(cover) * 255.0f);
                    byte glowByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(cellGlow[index] / GlowRange) * 255.0f);
                    Color32 color = new Color32(coverByte, glowByte, (byte)flags, 255);
                    hash = MixHash(hash, (uint)index | ((uint)coverByte << 12) | ((uint)glowByte << 20));
                    hash = MixHash(hash, (uint)flags);

                    int i = windowI0 + wi;
                    int j = windowJ0 + wj;
                    int cx = _host.originPxX + TileHalfW * (i - j);
                    int cy = _host.originPxY + TileHalfH * (i + j);
                    float x0 = (cx - TileHalfW) / PixelsPerUnit + translationX;
                    float x1 = (cx + TileHalfW) / PixelsPerUnit + translationX;
                    float y0 = (cy - TileHalfH) / PixelsPerUnit + translationY;
                    float y1 = (cy + TileHalfH) / PixelsPerUnit + translationY;

                    int v = quadCount * 4;
                    tileVertices[v].position = new Vector3(x0, y0, 0.0f);
                    tileVertices[v].uv = new Vector2(0.0f, TileHalfH * 2.0f);
                    tileVertices[v + 1].position = new Vector3(x0, y1, 0.0f);
                    tileVertices[v + 1].uv = new Vector2(0.0f, 0.0f);
                    tileVertices[v + 2].position = new Vector3(x1, y1, 0.0f);
                    tileVertices[v + 2].uv = new Vector2(TileHalfW * 2.0f, 0.0f);
                    tileVertices[v + 3].position = new Vector3(x1, y0, 0.0f);
                    tileVertices[v + 3].uv = new Vector2(TileHalfW * 2.0f, TileHalfH * 2.0f);
                    tileVertices[v].color = color;
                    tileVertices[v + 1].color = color;
                    tileVertices[v + 2].color = color;
                    tileVertices[v + 3].color = color;
                    quadCount++;

                    if (cx - TileHalfW < builtMinX) builtMinX = cx - TileHalfW;
                    if (cy - TileHalfH < builtMinY) builtMinY = cy - TileHalfH;
                    if (builtMaxX < cx + TileHalfW) builtMaxX = cx + TileHalfW;
                    if (builtMaxY < cy + TileHalfH) builtMaxY = cy + TileHalfH;
                }
            }

            bool bHasContent = 0 < quadCount;
            if (_host.groundMeshRenderer.enabled != bHasContent) _host.groundMeshRenderer.enabled = bHasContent;

            int vertexCount = quadCount * 4;
            if (false == bHasContent)
            {
                _host.tileVertexCount = -1;
                return;
            }

            if (vertexCount == _host.tileVertexCount && hash == _host.tileHash) return;

            _host.groundMesh.SetVertexBufferData(tileVertices, 0, 0, vertexCount, 0, TileUploadFlags);
            SubMeshDescriptor subMesh = new SubMeshDescriptor(0, quadCount * 6, MeshTopology.Triangles);
            subMesh.firstVertex = 0;
            subMesh.vertexCount = vertexCount;
            _host.groundMesh.SetSubMesh(0, subMesh, TileUploadFlags);

            float boundsX0 = builtMinX / PixelsPerUnit + translationX;
            float boundsY0 = builtMinY / PixelsPerUnit + translationY;
            float boundsX1 = builtMaxX / PixelsPerUnit + translationX;
            float boundsY1 = builtMaxY / PixelsPerUnit + translationY;
            _host.groundMesh.bounds = new Bounds(new Vector3((boundsX0 + boundsX1) * 0.5f, (boundsY0 + boundsY1) * 0.5f, 0.0f), new Vector3(boundsX1 - boundsX0, boundsY1 - boundsY0, 1.0f));

            _host.tileVertexCount = vertexCount;
            _host.tileHash = hash;
        }

        private static ulong MixHash(ulong _hash, uint _value)
        {
            return unchecked((_hash ^ _value) * HashPrime);
        }

        // ---------- 빛 알갱이 ----------

        private static float NoiseHash(int _a, int _b, int _c)
        {
            unchecked
            {
                int h = _a * 374761393 + _b * 668265263 + _c * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return ((uint)(h ^ (int)((uint)h >> 16))) / 4294967296.0f;
            }
        }

        private void RebuildMotes()
        {
            bool bDraw = true == bMotes && false == bFired;
            if (true == bDraw)
            {
                Vector3 center = new Vector3((boundsMinX + boundsMaxX) * 0.5f / PixelsPerUnit, (boundsMinY + boundsMaxY) * 0.5f / PixelsPerUnit, 0.0f);
                Vector3 size = new Vector3((boundsMaxX - boundsMinX) / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, (boundsMaxY - boundsMinY) / PixelsPerUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
                bDraw = PixelVfxCulling.IsVisible(center, size);
            }

            skyPainter.Begin(skyBuffer, transform.worldToLocalMatrix, transform.position.z);
            if (true == bDraw) DrawMotes(skyPainter);
            skyPainter.Commit(mesh, meshRenderer, ref skyHash, ref skyVertexCount);
        }

        // 차선 위에서 떠오르는 빛 알갱이. 7개가 0.23초 간격으로 1.6초 주기로 돌고, 한 번에 1.1초 동안 `+`에서 `.`로 줄며 12px 떠오른다.
        private void DrawMotes(PixelPainter _p)
        {
            float alongRange = Mathf.Min(MoteAlongRange, Mathf.Max(1.0f, laneLength - 1.0f));

            for (int n = 0; n < MoteCount; n++)
            {
                float t = elapsed - MoteDelay - n * MoteStep;
                if (t < 0.0f) continue;

                int cycle = Mathf.FloorToInt(t / MotePeriod);
                float age = (t - cycle * MotePeriod) / MoteLife;
                if (1.0f <= age) continue;

                float along = NoiseHash(moteSeed, n, cycle) * alongRange;
                float lateral = NoiseHash(moteSeed + 5, n, cycle) * MoteLateral - MoteLateral * 0.5f;
                float gi = laneStartI + laneStepI * along + lanePerpI * lateral;
                float gj = laneStartJ + laneStepJ * along + lanePerpJ * lateral;

                int x = originPxX + Mathf.RoundToInt(TileHalfW * (gi - gj));
                int y = originPxY + Mathf.RoundToInt(TileHalfH * (gi + gj)) + MoteLift + Mathf.RoundToInt(age * MoteRise);
                Color32 color = moteColors[Mathf.Min(MoteColorSteps - 1, (int)(age * MoteColorSteps))];
                _p.Shape(age < 0.55f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot, x, y, color);
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
            bFired = false;
            elapsed = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (false == bActive) return;

            int frame = Time.frameCount;
            if (frame == updatedFrame) return;

            updatedFrame = frame;
            UpdateAllLanes(Time.deltaTime);
        }

        private void OnDisable()
        {
            bActive = false;
            UnregisterLane();

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeLaneCount = 0;
            updatedFrame = -1;
            bMoteColorsReady = false;
            for (int i = 0; i < MaxActiveLanes; i++) activeLanes[i] = null;
        }

        private void OnValidate()
        {
            laneHalfWidth = Mathf.Max(0.1f, laneHalfWidth);
            clearSeconds = Mathf.Max(0.1f, clearSeconds);
        }
    }
}
