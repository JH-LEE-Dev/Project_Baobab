using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 두 지점(또는 두 나무) 사이를 1픽셀 크기의 도트(' . ')로 불규칙한 지그재그 형태로 연결하고,
/// URP 셰이더와 연동하여 별처럼 빛나며 색상이 불규칙하게 변동하는 절차적 별자리 점선 컴포넌트입니다.
/// 단일 메쉬 병합(1 Draw Call), 무할당(Zero GC Alloc), 프로젝트 표준 IObjectPool 호환 풀링을 지원합니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConstellationDottedLine : MonoBehaviour
{
    private const float BasePPU = 32.0f;
    private const float DefaultPixelUnit = 1.0f / BasePPU; // 0.03125f (1픽셀 유닛)

    [Header("Dot Settings")]
    [Tooltip("도트 1개의 픽셀 크기 (기본값: 1픽셀)")]
    [SerializeField] private float dotPixelSize = 1.0f;

    [Tooltip("도트 간 최소 간격 (픽셀 단위, 뭉침 방지 기본값: 3픽셀)")]
    [SerializeField] private float minDotSpacingPixels = 3.0f;

    [Tooltip("도트 간 최대 간격 (픽셀 단위, 기본값: 7픽셀)")]
    [SerializeField] private float maxDotSpacingPixels = 7.0f;

    [Header("Zigzag Settings")]
    [Tooltip("지그재그 꺾임점 사이의 기준 거리 (월드 유닛)")]
    [SerializeField] private float segmentLength = 0.8f;

    [Tooltip("지그재그 꺾임의 최대 편차 (픽셀 단위, 기본값: 6픽셀)")]
    [SerializeField] [Range(1.0f, 16.0f)] private float zigzagAmplitudePixels = 6.0f;

    [Tooltip("지그재그 난수 시드 (동일 좌표라도 다른 불규칙 형태 생성 가능)")]
    [SerializeField] private int randomSeed = 0;

    [Header("Rendering Settings")]
    [SerializeField] private string sortingLayerName = "Objects";
    [SerializeField] private int sortingOrderOffset = 1;

    // 외부 컴포넌트
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh proceduralMesh;

    // 풀링 인터페이스
    private IObjectPool<ConstellationDottedLine> pool;

    // Zero GC 무할당 사전 캐시 버퍼
    private readonly List<Vector3> cachedVertices = new List<Vector3>(256);
    private readonly List<int> cachedTriangles = new List<int>(384);
    private readonly List<Color> cachedColors = new List<Color>(256);
    private readonly List<Vector2> cachedUVs = new List<Vector2>(256);
    private readonly List<Vector3> cachedWaypoints = new List<Vector3>(32);

    // 내부 상태
    private Vector3 currentStartPos;
    private Vector3 currentEndPos;
    private Transform startTarget;
    private Transform endTarget;
    private bool bHasTargets = false;

#if UNITY_EDITOR
    [Header("Editor Preview (Editor Only)")]
    [SerializeField] private bool previewInEditor = true;
    [SerializeField] private Vector3 editorStartPos = new Vector3(-2.0f, 0.0f, 0.0f);
    [SerializeField] private Vector3 editorEndPos = new Vector3(2.0f, 0.0f, 0.0f);

    public Vector3 EditorStartPos
    {
        get => editorStartPos;
        set => editorStartPos = value;
    }

    public Vector3 EditorEndPos
    {
        get => editorEndPos;
        set => editorEndPos = value;
    }

    public int RandomSeed
    {
        get => randomSeed;
        set => randomSeed = value;
    }
#endif

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (null == proceduralMesh)
        {
            proceduralMesh = new Mesh
            {
                name = "ConstellationDottedLine_Mesh"
            };
            proceduralMesh.MarkDynamic();
            meshFilter.sharedMesh = proceduralMesh;
        }

        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        // 타겟 Transform을 추적 중이고 위치가 변경되었을 때만 재빌드
        if (true == bHasTargets && null != startTarget && null != endTarget)
        {
            Vector3 newStart = startTarget.position;
            Vector3 newEnd = endTarget.position;

            if (newStart != currentStartPos || newEnd != currentEndPos)
            {
                SetPoints(newStart, newEnd);
            }
        }
    }

    private void OnDestroy()
    {
        if (null != proceduralMesh)
        {
            Destroy(proceduralMesh);
            proceduralMesh = null;
        }
    }

    /// <summary>
    /// 프로젝트 표준 IObjectPool 주입
    /// </summary>
    public void SetPool(IObjectPool<ConstellationDottedLine> _pool)
    {
        pool = _pool;
    }

    /// <summary>
    /// 오브젝트 풀에 안전하게 반환합니다.
    /// </summary>
    public void ReturnToPool()
    {
        Clear();
        bHasTargets = false;
        startTarget = null;
        endTarget = null;

        if (null != pool)
        {
            pool.Release(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 외부 매니저에서 씬 전환이나 강제 정리 시 즉시 풀로 회수합니다.
    /// </summary>
    public void ForceReturnToPool()
    {
        ReturnToPool();
    }

    /// <summary>
    /// 시작점과 끝점 좌표를 전달받아 지그재그 1픽셀 도트 점선 메쉬를 생성합니다.
    /// </summary>
    public void SetPoints(Vector3 _startPos, Vector3 _endPos)
    {
        currentStartPos = _startPos;
        currentEndPos = _endPos;

        EnsureMeshInitialized();
        BuildConstellationMesh(_startPos, _endPos);
    }

    /// <summary>
    /// 두 Transform 타겟을 지속적으로 추적하도록 설정합니다.
    /// </summary>
    public void SetTargets(Transform _startTr, Transform _endTr)
    {
        startTarget = _startTr;
        endTarget = _endTr;
        bHasTargets = null != _startTr && null != _endTr;

        if (true == bHasTargets)
        {
            SetPoints(_startTr.position, _endTr.position);
        }
    }

    /// <summary>
    /// 점선 메쉬 데이터를 지웁니다.
    /// </summary>
    public void Clear()
    {
        if (null != proceduralMesh)
        {
            proceduralMesh.Clear();
        }
    }

    /// <summary>
    /// 소팅 레이어 및 오더를 지정합니다.
    /// </summary>
    public void SetSortingOrder(string _layerName, int _order)
    {
        sortingLayerName = _layerName;
        sortingOrderOffset = _order;
        UpdateSortingOrder();
    }

    private void UpdateSortingOrder()
    {
        if (null != meshRenderer)
        {
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrderOffset;
        }
    }

    private void EnsureMeshInitialized()
    {
        if (null == meshFilter) meshFilter = GetComponent<MeshFilter>();
        if (null == meshRenderer) meshRenderer = GetComponent<MeshRenderer>();

        if (null == proceduralMesh)
        {
            proceduralMesh = new Mesh
            {
                name = "ConstellationDottedLine_Mesh"
            };
            proceduralMesh.MarkDynamic();
            meshFilter.sharedMesh = proceduralMesh;
        }
    }

    /// <summary>
    /// 지그재그 경로 계산 및 1픽셀 도트 쿼드 메쉬 빌드 (Zero GC)
    /// </summary>
    private void BuildConstellationMesh(Vector3 _start, Vector3 _end)
    {
        cachedVertices.Clear();
        cachedTriangles.Clear();
        cachedColors.Clear();
        cachedUVs.Clear();
        cachedWaypoints.Clear();

        Vector3 diff = _end - _start;
        float totalDist = diff.magnitude;

        if (totalDist < 0.001f)
        {
            proceduralMesh.Clear();
            return;
        }

        Vector3 dir = diff / totalDist;
        // 2D 평면(X-Y) 상에서 선의 수직 법선 벡터
        Vector3 normal = new Vector3(-dir.y, dir.x, 0.0f);

        // 1. 지그재그 꺾임점(Waypoints) 계산
        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(totalDist / Mathf.Max(0.1f, segmentLength)));
        float stepDist = totalDist / segmentCount;
        float maxAmplitude = zigzagAmplitudePixels * DefaultPixelUnit;

        cachedWaypoints.Add(_start);

        // 일관된 의사 난수를 위한 해시 시드
        int baseSeed = randomSeed ^ Mathf.RoundToInt(_start.x * 100f) ^ Mathf.RoundToInt(_start.y * 100f);

        for (int i = 1; i < segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            Vector3 basePoint = _start + dir * (t * totalDist);

            // 의사 난수 오프셋 (-1.0 ~ +1.0)
            float pseudoRandom = GetPseudoRandom(baseSeed + i * 37);
            float offset = pseudoRandom * maxAmplitude;

            Vector3 waypoint = basePoint + normal * offset;
            cachedWaypoints.Add(waypoint);
        }

        cachedWaypoints.Add(_end);

        // 2. 지그재그 폴리라인을 따라 1픽셀 도트 쿼드 배치 (뭉침 방지 최소 간격 보장 불규칙 스페이싱)
        float minSpacingUnit = Mathf.Max(1.0f, minDotSpacingPixels) * DefaultPixelUnit;
        float maxSpacingUnit = Mathf.Max(minSpacingUnit, maxDotSpacingPixels * DefaultPixelUnit);
        float halfDotSize = (Mathf.Max(0.5f, dotPixelSize) * DefaultPixelUnit) * 0.5f;

        int dotGlobalIndex = 0;
        float accumulatedDist = 0.0f;
        float nextSpacing = GetPseudoRandomRange(baseSeed + dotGlobalIndex * 43, minSpacingUnit, maxSpacingUnit);

        for (int seg = 0; seg < cachedWaypoints.Count - 1; seg++)
        {
            Vector3 pA = cachedWaypoints[seg];
            Vector3 pB = cachedWaypoints[seg + 1];
            Vector3 segDiff = pB - pA;
            float segLen = segDiff.magnitude;

            if (0.0001f >= segLen) continue;

            Vector3 segDir = segDiff / segLen;
            float currentSegDist = 0.0f;

            // 이전 세그먼트에서 남은 거리 보정
            if (0.0f < accumulatedDist)
            {
                currentSegDist = nextSpacing - accumulatedDist;
            }

            while (segLen >= currentSegDist)
            {
                Vector3 dotCenter = pA + segDir * currentSegDist;

                // 1x1 픽셀 크기의 정방형 쿼드 (버텍스 4개)
                int vIndex = cachedVertices.Count;

                Vector3 v0 = dotCenter + new Vector3(-halfDotSize, -halfDotSize, 0.0f);
                Vector3 v1 = dotCenter + new Vector3(-halfDotSize,  halfDotSize, 0.0f);
                Vector3 v2 = dotCenter + new Vector3( halfDotSize,  halfDotSize, 0.0f);
                Vector3 v3 = dotCenter + new Vector3( halfDotSize, -halfDotSize, 0.0f);

                cachedVertices.Add(v0);
                cachedVertices.Add(v1);
                cachedVertices.Add(v2);
                cachedVertices.Add(v3);

                cachedUVs.Add(new Vector2(0.0f, 0.0f));
                cachedUVs.Add(new Vector2(0.0f, 1.0f));
                cachedUVs.Add(new Vector2(1.0f, 1.0f));
                cachedUVs.Add(new Vector2(1.0f, 0.0f));

                // 버텍스 컬러에 개별 도트 난수 위상 주입
                // r: 트윙클 위상 (0.0 ~ 1.0)
                // g: 색상 변동 위상 (0.0 ~ 1.0)
                float twinklePhase = Mathf.Repeat(GetPseudoRandom(baseSeed + dotGlobalIndex * 17) * 0.5f + 0.5f, 1.0f);
                float colorPhase   = Mathf.Repeat(GetPseudoRandom(baseSeed + dotGlobalIndex * 29) * 0.5f + 0.5f, 1.0f);
                Color dotColor = new Color(twinklePhase, colorPhase, 0.0f, 1.0f);

                cachedColors.Add(dotColor);
                cachedColors.Add(dotColor);
                cachedColors.Add(dotColor);
                cachedColors.Add(dotColor);

                // 트라이앵글 2개 (인덱스 6개)
                cachedTriangles.Add(vIndex);
                cachedTriangles.Add(vIndex + 1);
                cachedTriangles.Add(vIndex + 2);

                cachedTriangles.Add(vIndex);
                cachedTriangles.Add(vIndex + 2);
                cachedTriangles.Add(vIndex + 3);

                dotGlobalIndex++;
                nextSpacing = GetPseudoRandomRange(baseSeed + dotGlobalIndex * 43, minSpacingUnit, maxSpacingUnit);
                currentSegDist += nextSpacing;
            }

            accumulatedDist = segLen - (currentSegDist - nextSpacing);
        }

        // 3. 메쉬 데이터 주입 (무할당 List API 사용)
        proceduralMesh.Clear();
        proceduralMesh.SetVertices(cachedVertices);
        proceduralMesh.SetUVs(0, cachedUVs);
        proceduralMesh.SetColors(cachedColors);
        proceduralMesh.SetTriangles(cachedTriangles, 0);
        proceduralMesh.RecalculateBounds();
    }

    /// <summary>
    /// 결정론적 의사 난수 범위 함수 (_min ~ _max)
    /// </summary>
    private float GetPseudoRandomRange(int _seed, float _min, float _max)
    {
        float normalized = (GetPseudoRandom(_seed) * 0.5f) + 0.5f; // 0.0f ~ 1.0f
        return Mathf.Lerp(_min, _max, normalized);
    }

    /// <summary>
    /// 결정론적 의사 난수 함수 (-1.0f ~ 1.0f)
    /// </summary>
    private float GetPseudoRandom(int _seed)
    {
        int x = _seed;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        // 0.0 ~ 1.0 변환 후 -1.0 ~ 1.0
        float normalized = (float)(Math.Abs(x) % 10000) / 10000.0f;
        return (normalized * 2.0f) - 1.0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (false == previewInEditor) return;
        EnsureMeshInitialized();
        UpdateSortingOrder();
        BuildConstellationMesh(editorStartPos, editorEndPos);
    }
#endif
}
