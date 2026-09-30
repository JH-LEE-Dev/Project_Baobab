using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 도트 렌더링 형태 열거형
/// </summary>
public enum EDotShape
{
    SinglePixel,     // 1x1 픽셀 기본 ( . )
    DoublePixel,     // 2x2 픽셀 정방형 ( ■ )
    CrossStar,       // 3x3 픽셀 정방향 십자별 ( + )
    Random2x2,       // (레거시) 2x2 바운드 내 점마다 무작위 픽셀 클러스터
    AestheticRandom, // 픽셀 3대 형태 ( . , + , x ) 유기적 혼합 (기본값)
    XStar            // 3x3 픽셀 회전 십자별 ( x )
}

/// <summary>
/// 별자리를 구성하는 개별 노드 정보.
/// 나무(황금빛) 또는 나무 파괴 후 소환된 푸른색 큰 별(푸른빛)을 표현합니다.
/// </summary>
[System.Serializable]
public struct ConstellationNode
{
    [Tooltip("노드의 월드 위치 (target이 지정되어 있으면 target.position이 우선 반영됩니다)")]
    public Vector3 position;

    [Tooltip("추적할 Transform 대상 (나무 또는 큰 별 프리팹. null이면 position 사용)")]
    public Transform target;

    [Tooltip("큰 별 여부 (false: 나무[황금빛], true: 큰 별[푸른빛])")]
    public bool isBigStar;

    public ConstellationNode(Vector3 _position, bool _isBigStar = false, Transform _target = null)
    {
        position = _position;
        isBigStar = _isBigStar;
        target = _target;
    }
}

/// <summary>
/// 여러 개의 나무(또는 큰 별) 사이를 1픽셀 크기의 도트(' . ', ' + ', ' x ')로 연결하고,
/// URP 셰이더와 연동하여 별처럼 빛나며 지점 간 알파 감쇄 및 황금빛 ↔ 푸른빛 동적 그라데이션을 표현하는
/// 고성능 절차적 별자리 네트워크 컴포넌트입니다.
/// 단일 메쉬 병합(1 Draw Call), 무할당(Zero GC Alloc), 프로젝트 표준 IObjectPool 호환 풀링을 지원합니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConstellationDottedLine : MonoBehaviour
{
    private const float BasePPU = 32.0f;
    private const float DefaultPixelUnit = 1.0f / BasePPU; // 0.03125f (1픽셀 유닛)

    // 노드 타입별 기준 색상 상수 (HDR Bloom 연동)
    // 푸른빛은 R/G를 억제하고 B 채널을 2.4(HDR)로 부스트하여 블룸 포화 시에도 맑고 강렬한 네온 블루를 유지합니다.
    // 황금빛은 B를 억제하고 R/G 순도를 높여 푸른빛과의 보색 콘트라스트를 명확히 분리합니다.
    public static readonly Color TreeGoldenColor = new Color(1.8f, 0.85f, 0.05f, 1.0f);
    public static readonly Color BigStarBlueColor = new Color(0.0f, 0.65f, 2.4f, 1.0f);

    // 벌목 전환 앞선이 지나가는 도트에 섞는 밝은 색(살짝 따뜻한 흰빛)
    private static readonly Color TransitionGlowColor = new Color(1.6f, 1.5f, 1.3f, 1.0f);

    // 전환 앞선의 경계 폭(선분 길이 대비). 클수록 황금빛과 푸른빛이 넓고 부드럽게 섞인다.
    private const float SweepSoftness = 0.6f;

    [Header("Network & Topology Settings")]
    [Tooltip("마지막 노드와 첫 번째 노드를 연결하여 닫힌 다각형(폐곡선)을 형성할지 여부")]
    [SerializeField] private bool isClosedLoop = true;

    [Header("Dot Settings")]
    [Tooltip("도트 렌더링 형태 (AestheticRandom: 픽셀 3대 형태 . / + / x 혼합, SinglePixel: 1x1 ., DoublePixel: 2x2 ■, CrossStar: 십자 +, XStar: 회전 십자 x)")]
    [SerializeField] private EDotShape dotShape = EDotShape.AestheticRandom;

    [Tooltip("도트 1개의 기준 픽셀 크기 (기본값: 1픽셀)")]
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
    [SerializeField] private int sortingOrderOffset = 10;

    [Header("Felled Node Transition (나무 벌목 시 황금빛 → 푸른빛 전환)")]
    [Tooltip("벌목된 노드에서 푸른빛이 선을 따라 번지는 데 걸리는 시간(초). 0이면 즉시 전환")]
    [SerializeField] private float felledTransitionSeconds = 1.4f;

    [Tooltip("벌목 후 전환이 시작되기 전 대기 시간(초)")]
    [SerializeField] private float felledTransitionDelay = 0.15f;

    [Tooltip("전환 앞선이 지나가는 도트의 밝기 강조 정도(0~1). 0이면 강조 없음")]
    [SerializeField] [Range(0.0f, 1.0f)] private float felledTransitionGlow = 0.2f;

    [Header("Editor Preview & Multi-Node Testing")]
    [Tooltip("에디터 인스펙터 및 씬 뷰에서 실시간 프리뷰 활성화")]
    [SerializeField] private bool previewInEditor = true;

    [Tooltip("에디터 테스트용 노드 리스트 (인스펙터 및 씬 뷰에서 직접 조작 가능)")]
    [SerializeField] private List<ConstellationNode> editorNodes = new List<ConstellationNode>();

    // 외부 컴포넌트
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh proceduralMesh;

    // 풀링 인터페이스
    private IObjectPool<ConstellationDottedLine> pool;

    // Zero GC 무할당 사전 캐시 버퍼
    private readonly List<Vector3> cachedVertices = new List<Vector3>(512);
    private readonly List<Vector3> cachedNormals = new List<Vector3>(512);
    private readonly List<int> cachedTriangles = new List<int>(768);
    private readonly List<Color> cachedColors = new List<Color>(512);
    private readonly List<Vector2> cachedUV0s = new List<Vector2>(512);
    private readonly List<Vector2> cachedUV1s = new List<Vector2>(512);
    private readonly List<Vector3> cachedWaypoints = new List<Vector3>(64);
    private readonly List<ConstellationNode> activeNodes = new List<ConstellationNode>(16);

    // activeNodes와 같은 인덱스의 푸른빛 전환 진행도(0: 황금빛, 1: 푸른빛)와 시작 전 남은 대기 시간
    private readonly List<float> nodeBlendRaw = new List<float>(16);
    private readonly List<float> nodeBlendDelay = new List<float>(16);

    // 런타임 추적 상태
    private bool bHasDynamicTargets = false;

    public bool IsClosedLoop
    {
        get => isClosedLoop;
        set
        {
            isClosedLoop = value;
            RebuildActiveMesh();
        }
    }

    public List<ConstellationNode> EditorNodes => editorNodes;

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

        // 런타임 시작 시 에디터 노드가 존재하면 초기 메쉬 빌드
        if (0 < editorNodes.Count)
        {
            SetNodes(editorNodes, isClosedLoop);
        }
    }

    private void Reset()
    {
        // 프리팹을 처음 배치할 때 첨부 사진과 유사한 5각 별자리 기본 노드 세트 자동 구성
        editorNodes.Clear();
        editorNodes.Add(new ConstellationNode(new Vector3(-3.2f, 0.2f, 0.0f), false));  // 노드 0: 황금빛 나무
        editorNodes.Add(new ConstellationNode(new Vector3(0.5f, 1.3f, 0.0f), false));   // 노드 1: 상단 푸른 나무
        editorNodes.Add(new ConstellationNode(new Vector3(0.8f, -0.4f, 0.0f), false));  // 노드 2: 중단 푸른 나무
        editorNodes.Add(new ConstellationNode(new Vector3(2.8f, -0.2f, 0.0f), false));  // 노드 3: 우측 푸른 나무
        editorNodes.Add(new ConstellationNode(new Vector3(0.9f, -1.8f, 0.0f), false));  // 노드 4: 하단 푸른 나무
        isClosedLoop = true;
    }

    private void LateUpdate()
    {
        // 벌목된 노드의 푸른빛 전환이 진행 중이면 그 프레임만 무할당 재빌드(전환이 끝나면 다시 정적 상태)
        bool bBlendChanged = AdvanceNodeBlends(Time.deltaTime);

        // 타겟 Transform을 추적 중이고 위치가 변경되었을 때만 무할당 재빌드
        if (true == bHasDynamicTargets && 0 < activeNodes.Count)
        {
            bool bMoved = false;
            for (int i = 0; i < activeNodes.Count; i++)
            {
                ConstellationNode node = activeNodes[i];
                if (null != node.target)
                {
                    Vector3 targetPos = node.target.position;
                    if (targetPos != node.position)
                    {
                        node.position = targetPos;
                        activeNodes[i] = node;
                        bMoved = true;
                    }
                }
            }

            if (true == bMoved)
            {
                BuildConstellationMesh(activeNodes, isClosedLoop);
                return;
            }
        }

        if (true == bBlendChanged && 0 < activeNodes.Count)
        {
            BuildConstellationMesh(activeNodes, isClosedLoop);
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

    #region Public APIs (향후 런타임 간편 연동용)

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
        bHasDynamicTargets = false;
        activeNodes.Clear();
        nodeBlendRaw.Clear();
        nodeBlendDelay.Clear();

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
    /// 다중 노드(ConstellationNode) 리스트를 전달받아 별자리 네트워크 메쉬를 생성합니다.
    /// </summary>
    public void SetNodes(IReadOnlyList<ConstellationNode> _nodes, bool _isClosed = true)
    {
        activeNodes.Clear();
        bHasDynamicTargets = false;

        if (null != _nodes)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                ConstellationNode node = _nodes[i];
                if (null != node.target)
                {
                    node.position = node.target.position;
                    bHasDynamicTargets = true;
                }
                activeNodes.Add(node);
            }
        }

        isClosedLoop = _isClosed;
        ResetNodeBlends();
        EnsureMeshInitialized();
        BuildConstellationMesh(activeNodes, isClosedLoop);
    }

    /// <summary>
    /// 단순 월드 좌표 리스트를 전달받아 기본 황금빛 나무 별자리로 즉시 생성합니다.
    /// </summary>
    public void SetPoints(IReadOnlyList<Vector3> _points, bool _isClosed = true)
    {
        activeNodes.Clear();
        bHasDynamicTargets = false;

        if (null != _points)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                activeNodes.Add(new ConstellationNode(_points[i], false));
            }
        }

        isClosedLoop = _isClosed;
        ResetNodeBlends();
        EnsureMeshInitialized();
        BuildConstellationMesh(activeNodes, isClosedLoop);
    }

    /// <summary>
    /// 다중 Transform 타겟 리스트를 전달받아 실시간 추적 별자리로 생성합니다.
    /// </summary>
    public void SetTargets(IReadOnlyList<Transform> _targets, bool _isClosed = true)
    {
        activeNodes.Clear();
        bHasDynamicTargets = false;

        if (null != _targets)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                Transform tr = _targets[i];
                Vector3 pos = null != tr ? tr.position : Vector3.zero;
                if (null != tr) bHasDynamicTargets = true;
                activeNodes.Add(new ConstellationNode(pos, false, tr));
            }
        }

        isClosedLoop = _isClosed;
        ResetNodeBlends();
        EnsureMeshInitialized();
        BuildConstellationMesh(activeNodes, isClosedLoop);
    }

    /// <summary>
    /// 특정 인덱스의 노드를 '푸른색 큰 별'로 전환하거나 타겟을 갱신합니다.
    /// 나무가 파괴되어 큰 별이 소환되었을 때 호출하면, 해당 노드 연결선이 황금빛 ↔ 푸른빛 그라데이션으로 전환됩니다.
    /// 재생 중에는 색이 한 번에 바뀌지 않고, 벌목된 노드에서 푸른빛이 선을 따라 부드럽게 번집니다(felledTransitionSeconds).
    /// </summary>
    public void SetNodeAsBigStar(int _nodeIndex, bool _isBigStar, Transform _newTarget = null)
    {
        if (0 <= _nodeIndex && activeNodes.Count > _nodeIndex)
        {
            ConstellationNode node = activeNodes[_nodeIndex];
            bool bWasBigStar = node.isBigStar;
            node.isBigStar = _isBigStar;
            if (null != _newTarget)
            {
                node.target = _newTarget;
                node.position = _newTarget.position;
                bHasDynamicTargets = true;
            }
            activeNodes[_nodeIndex] = node;

            SyncNodeBlendCount();
            if (false == _isBigStar)
            {
                nodeBlendRaw[_nodeIndex] = 0.0f;
                nodeBlendDelay[_nodeIndex] = 0.0f;
            }
            else if (false == bWasBigStar)
            {
                // 보이는 상태에서 새로 벌목된 노드만 애니메이션한다. 그 외(에디터 프리뷰 등)는 즉시 푸른빛이다.
                bool bAnimate = true == Application.isPlaying && 0.0f < felledTransitionSeconds && true == gameObject.activeInHierarchy;
                nodeBlendRaw[_nodeIndex] = true == bAnimate ? 0.0f : 1.0f;
                nodeBlendDelay[_nodeIndex] = true == bAnimate ? Mathf.Max(0.0f, felledTransitionDelay) : 0.0f;
            }

            BuildConstellationMesh(activeNodes, isClosedLoop);
        }
    }

    /// <summary>
    /// (하위 호환) 시작점과 끝점 2개 좌표를 전달받아 단일 선분 메쉬를 생성합니다.
    /// </summary>
    public void SetPoints(Vector3 _startPos, Vector3 _endPos)
    {
        activeNodes.Clear();
        bHasDynamicTargets = false;
        activeNodes.Add(new ConstellationNode(_startPos, false));
        activeNodes.Add(new ConstellationNode(_endPos, false));
        isClosedLoop = false;

        ResetNodeBlends();
        EnsureMeshInitialized();
        BuildConstellationMesh(activeNodes, isClosedLoop);
    }

    /// <summary>
    /// (하위 호환) 두 Transform 타겟을 지속적으로 추적하도록 설정합니다.
    /// </summary>
    public void SetTargets(Transform _startTr, Transform _endTr)
    {
        activeNodes.Clear();
        bHasDynamicTargets = null != _startTr || null != _endTr;
        activeNodes.Add(new ConstellationNode(null != _startTr ? _startTr.position : Vector3.zero, false, _startTr));
        activeNodes.Add(new ConstellationNode(null != _endTr ? _endTr.position : Vector3.zero, false, _endTr));
        isClosedLoop = false;

        ResetNodeBlends();
        EnsureMeshInitialized();
        BuildConstellationMesh(activeNodes, isClosedLoop);
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

    /// <summary>
    /// 노드들의 중심점(Centroid) 기준 각도 순으로 정렬하여, 선분이 서로 교차하지 않는 완벽한 단순 다각형(Simple Polygon) 성도를 형성합니다 (Zero GC).
    /// </summary>
    public void AutoUntangleNodes()
    {
        if (null != editorNodes && 3 <= editorNodes.Count)
        {
            UntangleList(editorNodes);
        }
        if (null != activeNodes && 3 <= activeNodes.Count)
        {
            UntangleList(activeNodes);
        }

        RebuildActiveMesh();
    }

    private static Vector3 untangleCentroid;

    private static void UntangleList(List<ConstellationNode> _list)
    {
        if (null == _list || 3 > _list.Count) return;

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < _list.Count; i++)
        {
            centroid += _list[i].position;
        }
        centroid /= _list.Count;

        untangleCentroid = centroid;
        _list.Sort(CompareNodesByCentroidAngle);
    }

    private static int CompareNodesByCentroidAngle(ConstellationNode _a, ConstellationNode _b)
    {
        float angleA = Mathf.Atan2((_a.position.y - untangleCentroid.y) * 2.0f, _a.position.x - untangleCentroid.x);
        float angleB = Mathf.Atan2((_b.position.y - untangleCentroid.y) * 2.0f, _b.position.x - untangleCentroid.x);
        return angleA.CompareTo(angleB);
    }

    #endregion

    // nodeBlendRaw/Delay 길이를 activeNodes에 맞춘다(모자란 칸은 큰 별 여부에 따라 채운다).
    private void SyncNodeBlendCount()
    {
        while (nodeBlendRaw.Count < activeNodes.Count)
        {
            int index = nodeBlendRaw.Count;
            nodeBlendRaw.Add(true == activeNodes[index].isBigStar ? 1.0f : 0.0f);
            nodeBlendDelay.Add(0.0f);
        }

        while (nodeBlendRaw.Count > activeNodes.Count)
        {
            nodeBlendRaw.RemoveAt(nodeBlendRaw.Count - 1);
            nodeBlendDelay.RemoveAt(nodeBlendDelay.Count - 1);
        }
    }

    // 노드 목록을 새로 채운 직후: 이미 큰 별인 노드는 애니메이션 없이 곧바로 푸른빛으로 시작한다.
    private void ResetNodeBlends()
    {
        nodeBlendRaw.Clear();
        nodeBlendDelay.Clear();
        SyncNodeBlendCount();
    }

    // 큰 별 노드의 전환 진행도를 시간에 따라 올린다. 값이 바뀌었으면(메쉬 재빌드 필요) true.
    private bool AdvanceNodeBlends(float _deltaTime)
    {
        bool bChanged = false;
        if (nodeBlendRaw.Count != activeNodes.Count) return false;

        float duration = Mathf.Max(0.0001f, felledTransitionSeconds);
        for (int i = 0; i < activeNodes.Count; i++)
        {
            if (false == activeNodes[i].isBigStar || 1.0f <= nodeBlendRaw[i]) continue;

            if (0.0f < nodeBlendDelay[i])
            {
                nodeBlendDelay[i] -= _deltaTime;
                continue;
            }

            nodeBlendRaw[i] = Mathf.Min(1.0f, nodeBlendRaw[i] + _deltaTime / duration);
            bChanged = true;
        }

        return bChanged;
    }

    // 노드의 푸른빛 정도(0~1, 처음과 끝이 느린 완만한 곡선). 프리뷰용 editorNodes는 애니메이션 없이 즉시 값이다.
    private float GetNodeBlend(int _nodeIndex, IReadOnlyList<ConstellationNode> _nodes)
    {
        if (true == ReferenceEquals(_nodes, activeNodes) && _nodeIndex < nodeBlendRaw.Count)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, nodeBlendRaw[_nodeIndex]);
        }

        return true == _nodes[_nodeIndex].isBigStar ? 1.0f : 0.0f;
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

    private void RebuildActiveMesh()
    {
        EnsureMeshInitialized();
        if (0 < activeNodes.Count)
        {
            BuildConstellationMesh(activeNodes, isClosedLoop);
        }
        else if (0 < editorNodes.Count)
        {
            BuildConstellationMesh(editorNodes, isClosedLoop);
        }
    }

    /// <summary>
    /// 다중 노드 별자리 네트워크 단일 메쉬 일괄 빌드 (Zero GC Alloc)
    /// </summary>
    private void BuildConstellationMesh(IReadOnlyList<ConstellationNode> _nodes, bool _isClosed)
    {
        cachedVertices.Clear();
        cachedNormals.Clear();
        cachedTriangles.Clear();
        cachedColors.Clear();
        cachedUV0s.Clear();
        cachedUV1s.Clear();

        if (null == _nodes || 2 > _nodes.Count)
        {
            proceduralMesh.Clear();
            return;
        }

        int nodeCount = _nodes.Count;
        bool bClose = true == _isClosed && 2 < nodeCount;
        int totalSegments = true == bClose ? nodeCount : nodeCount - 1;

        // 1. 전체 별자리 경로 총 길이 계산 (전체 진행도 uv0.x 계산용)
        float totalNetworkDist = 0.0f;
        for (int s = 0; s < totalSegments; s++)
        {
            Vector3 pA = _nodes[s].position;
            Vector3 pB = _nodes[(s + 1) % nodeCount].position;
            totalNetworkDist += (pB - pA).magnitude;
        }
        if (0.0001f >= totalNetworkDist) totalNetworkDist = 1.0f;

        float accumulatedNetworkDist = 0.0f;
        float minSpacingUnit = Mathf.Max(1.0f, minDotSpacingPixels) * DefaultPixelUnit;
        float maxSpacingUnit = Mathf.Max(minSpacingUnit, maxDotSpacingPixels * DefaultPixelUnit);
        float basePixelSize = Mathf.Max(0.5f, dotPixelSize) * DefaultPixelUnit;
        float maxAmplitude = zigzagAmplitudePixels * DefaultPixelUnit;

        int dotGlobalIndex = 0;

        // 2. 각 세그먼트(지점과 지점 사이) 순회 빌드
        for (int s = 0; s < totalSegments; s++)
        {
            ConstellationNode nodeA = _nodes[s];
            ConstellationNode nodeB = _nodes[(s + 1) % nodeCount];

            Vector3 startPos = nodeA.position;
            Vector3 endPos = nodeB.position;
            Vector3 diff = endPos - startPos;
            float segDist = diff.magnitude;

            if (0.0001f >= segDist) continue;

            Vector3 segDir = diff / segDist;
            Vector3 rawNormal = new Vector3(-segDir.y, segDir.x, 0.0f);
            Vector3 lineNormal = 0.0001f < rawNormal.sqrMagnitude ? rawNormal.normalized : Vector3.up;

            // 시작/끝 노드의 푸른빛 정도 (나무 = 0: 황금빛, 큰 별 = 1: 푸른빛). 벌목 직후에는 0에서 1로 서서히 올라간다.
            float blendA = GetNodeBlend(s, _nodes);
            float blendB = GetNodeBlend((s + 1) % nodeCount, _nodes);

            // 꺾임점(Waypoints) 생성 (노드 결착부는 편차 0으로 완벽 앵커링)
            cachedWaypoints.Clear();
            int subSegmentCount = Mathf.Max(1, Mathf.RoundToInt(segDist / Mathf.Max(0.1f, segmentLength)));
            cachedWaypoints.Add(startPos);

            int segBaseSeed = randomSeed ^ Mathf.RoundToInt(startPos.x * 71f) ^ Mathf.RoundToInt(startPos.y * 37f) ^ (s * 101);

            for (int w = 1; w < subSegmentCount; w++)
            {
                float t = (float)w / subSegmentCount;
                Vector3 basePoint = startPos + segDir * (t * segDist);

                // 노드 양끝 결착점 20% 구간에서는 꺾임 진폭이 0으로 부드럽게 수렴하여 나무 링/큰별에 정확히 안착
                float anchor = Mathf.SmoothStep(0.0f, 0.2f, t) * Mathf.SmoothStep(1.0f, 0.8f, t);
                float pseudoRandom = GetPseudoRandom(segBaseSeed + w * 47);
                float offset = pseudoRandom * maxAmplitude * anchor;

                Vector3 waypoint = basePoint + lineNormal * offset;
                cachedWaypoints.Add(waypoint);
            }
            cachedWaypoints.Add(endPos);

            // 세그먼트 내 도트 배치
            float currentSegDist = 0.0f;
            float nextSpacing = GetPseudoRandomRange(segBaseSeed + dotGlobalIndex * 43, minSpacingUnit, maxSpacingUnit);

            for (int wp = 0; wp < cachedWaypoints.Count - 1; wp++)
            {
                Vector3 p0 = cachedWaypoints[wp];
                Vector3 p1 = cachedWaypoints[wp + 1];
                Vector3 subDiff = p1 - p0;
                float subLen = subDiff.magnitude;

                if (0.0001f >= subLen) continue;

                Vector3 subDir = subDiff / subLen;
                float currentSubDist = 0.0f;

                while (subLen >= currentSubDist)
                {
                    Vector3 dotCenter = p0 + subDir * currentSubDist;
                    float progressAlongSeg = Mathf.Clamp01(currentSegDist / segDist);
                    float globalProgress = Mathf.Clamp01((accumulatedNetworkDist + currentSegDist) / totalNetworkDist);

                    // 노드 간 색상 보간 (황금빛 나무 ↔ 푸른색 큰 별 그라데이션)
                    // 푸른색 영역이 노란색에 묻히지 않도록, 큰 별과 연결된 구간에서는 푸른색이 선분의 65% 이상을 시원하게 장악하는 비대칭 곡선을 쓴다.
                    // 벌목 전환 중에는 각 노드의 푸른빛이 그 노드에서 선분 안쪽으로 번지는 앞선(sweep)을 따라 드러난다.
                    // 앞선이 완전히 지나간 뒤(blend = 1)의 색은 기존 최종 색과 정확히 같다.
                    float sweepA = Mathf.Clamp01((blendA * (1.0f + SweepSoftness) - progressAlongSeg) / SweepSoftness);
                    float sweepB = Mathf.Clamp01((blendB * (1.0f + SweepSoftness) - (1.0f - progressAlongSeg)) / SweepSoftness);
                    float reachA = Mathf.SmoothStep(0.0f, 1.0f, sweepA);
                    float reachB = Mathf.SmoothStep(0.0f, 1.0f, sweepB);

                    // 나무(A) <- 큰별(B): 푸른빛이 B 쪽에서 빠르게 치고 올라옴 / 큰별(A) -> 나무(B): A 쪽에서 오래 유지되다가 나무 쪽에서 전환
                    float weightFromA = Mathf.Pow(1.0f - progressAlongSeg, 0.55f);
                    float weightFromB = Mathf.Pow(progressAlongSeg, 0.55f);
                    float blueAmount = reachA * (1.0f - reachB) * weightFromA + (1.0f - reachA) * reachB * weightFromB + reachA * reachB;

                    Color dotBaseColor = Color.Lerp(TreeGoldenColor, BigStarBlueColor, blueAmount);

                    // 전환 앞선이 지나가는 도트만 살짝 밝게 (앞선의 중앙에서 최대, 앞선 앞뒤에서는 0)
                    if (0.0f < felledTransitionGlow)
                    {
                        float frontBump = Mathf.Max(4.0f * sweepA * (1.0f - sweepA), 4.0f * sweepB * (1.0f - sweepB));
                        dotBaseColor = Color.Lerp(dotBaseColor, TransitionGlowColor, felledTransitionGlow * frontBump);
                    }

                    // 트윙클 및 색상 시프트 위상
                    float twinklePhase = Mathf.Repeat(GetPseudoRandom(segBaseSeed + dotGlobalIndex * 17) * 0.5f + 0.5f, 1.0f);
                    float colorPhase   = Mathf.Repeat(GetPseudoRandom(segBaseSeed + dotGlobalIndex * 29) * 0.5f + 0.5f, 1.0f);
                    Vector2 uv1Phases  = new Vector2(twinklePhase, colorPhase);

                    // 도트 형태별 쿼드 생성
                    AddDotByShape(dotCenter, basePixelSize, globalProgress, progressAlongSeg, dotBaseColor, lineNormal, uv1Phases, segBaseSeed + dotGlobalIndex);

                    dotGlobalIndex++;
                    nextSpacing = GetPseudoRandomRange(segBaseSeed + dotGlobalIndex * 43, minSpacingUnit, maxSpacingUnit);
                    currentSubDist += nextSpacing;
                    currentSegDist += nextSpacing;
                }
            }

            accumulatedNetworkDist += segDist;
        }

        // 3. 메쉬 데이터 주입 (무할당 List API)
        proceduralMesh.Clear();
        proceduralMesh.SetVertices(cachedVertices);
        proceduralMesh.SetNormals(cachedNormals);
        proceduralMesh.SetUVs(0, cachedUV0s);
        proceduralMesh.SetUVs(1, cachedUV1s);
        proceduralMesh.SetColors(cachedColors);
        proceduralMesh.SetTriangles(cachedTriangles, 0);
        proceduralMesh.RecalculateBounds();
    }

    /// <summary>
    /// 지정된 EDotShape에 맞춰 적절한 픽셀 쿼드를 생성합니다.
    /// </summary>
    private void AddDotByShape(Vector3 _center, float _basePixelSize, float _globalProgress, float _segProgress, Color _color, Vector3 _normal, Vector2 _uv1, int _seed)
    {
        if (EDotShape.AestheticRandom == dotShape)
        {
            // 픽셀 3대 형태 ( . , + , x ) 유기적 혼합
            int randVal = Math.Abs(_seed ^ (_seed << 5)) % 100;

            if (30 > randVal)
            {
                // 30%: 정방향 십자별 (+)
                AddCrossStar(_center, _basePixelSize, _globalProgress, _segProgress, _color, _normal, _uv1);
            }
            else if (60 > randVal)
            {
                // 30%: 회전 십자별 (x)
                AddXStar(_center, _basePixelSize, _globalProgress, _segProgress, _color, _normal, _uv1);
            }
            else
            {
                // 40%: 1x1 도트 (.)
                AddDot(_center, _basePixelSize, _globalProgress, _segProgress, _color, _normal, _uv1);
            }
        }
        else if (EDotShape.XStar == dotShape)
        {
            AddXStar(_center, _basePixelSize, _globalProgress, _segProgress, _color, _normal, _uv1);
        }
        else if (EDotShape.CrossStar == dotShape)
        {
            AddCrossStar(_center, _basePixelSize, _globalProgress, _segProgress, _color, _normal, _uv1);
        }
        else if (EDotShape.DoublePixel == dotShape)
        {
            float halfSize = _basePixelSize;
            AddQuad(_center, halfSize, halfSize, _globalProgress, _segProgress, _color, _normal, _uv1);
        }
        else
        {
            AddDot(_center, _basePixelSize, _globalProgress, _segProgress, _color, _normal, _uv1);
        }
    }

    /// <summary>
    /// 정방형 또는 직사각형 쿼드를 캐시 버퍼에 추가합니다 (Zero GC).
    /// </summary>
    private void AddQuad(Vector3 _center, float _halfWidth, float _halfHeight, float _globalProgress, float _segProgress, Color _color, Vector3 _normal, Vector2 _uv1)
    {
        int vIndex = cachedVertices.Count;

        Vector3 v0 = _center + new Vector3(-_halfWidth, -_halfHeight, 0.0f);
        Vector3 v1 = _center + new Vector3(-_halfWidth,  _halfHeight, 0.0f);
        Vector3 v2 = _center + new Vector3( _halfWidth,  _halfHeight, 0.0f);
        Vector3 v3 = _center + new Vector3( _halfWidth, -_halfHeight, 0.0f);

        cachedVertices.Add(v0);
        cachedVertices.Add(v1);
        cachedVertices.Add(v2);
        cachedVertices.Add(v3);

        cachedNormals.Add(_normal);
        cachedNormals.Add(_normal);
        cachedNormals.Add(_normal);
        cachedNormals.Add(_normal);

        // uv0.x: 전체 별자리 경로 진행도 (0~1, 펄스 파동용)
        // uv0.y: 세그먼트(지점과 지점 사이) 국소 진행도 (0~1, 양끝 알파 감쇄용)
        Vector2 uvCoord = new Vector2(_globalProgress, _segProgress);
        cachedUV0s.Add(uvCoord);
        cachedUV0s.Add(uvCoord);
        cachedUV0s.Add(uvCoord);
        cachedUV0s.Add(uvCoord);

        // uv1.x: Twinkle 위상, uv1.y: Color Shift 위상
        cachedUV1s.Add(_uv1);
        cachedUV1s.Add(_uv1);
        cachedUV1s.Add(_uv1);
        cachedUV1s.Add(_uv1);

        // 버텍스 컬러: 노드간 보간된 황금빛 ↔ 푸른빛 그라데이션
        cachedColors.Add(_color);
        cachedColors.Add(_color);
        cachedColors.Add(_color);
        cachedColors.Add(_color);

        cachedTriangles.Add(vIndex);
        cachedTriangles.Add(vIndex + 1);
        cachedTriangles.Add(vIndex + 2);

        cachedTriangles.Add(vIndex);
        cachedTriangles.Add(vIndex + 2);
        cachedTriangles.Add(vIndex + 3);
    }

    private void AddDot(Vector3 _center, float _basePixelSize, float _globalProgress, float _segProgress, Color _color, Vector3 _normal, Vector2 _uv1)
    {
        float halfPixel = _basePixelSize * 0.5f;
        AddQuad(_center, halfPixel, halfPixel, _globalProgress, _segProgress, _color, _normal, _uv1);
    }

    private void AddCrossStar(Vector3 _center, float _basePixelSize, float _globalProgress, float _segProgress, Color _color, Vector3 _normal, Vector2 _uv1)
    {
        float halfBarLong = _basePixelSize * 1.5f;
        float halfBarShort = _basePixelSize * 0.5f;
        AddQuad(_center, halfBarLong, halfBarShort, _globalProgress, _segProgress, _color, _normal, _uv1);
        AddQuad(_center, halfBarShort, halfBarLong, _globalProgress, _segProgress, _color, _normal, _uv1);
    }

    private void AddXStar(Vector3 _center, float _basePixelSize, float _globalProgress, float _segProgress, Color _color, Vector3 _normal, Vector2 _uv1)
    {
        float halfPixel = _basePixelSize * 0.5f;
        AddQuad(_center, halfPixel, halfPixel, _globalProgress, _segProgress, _color, _normal, _uv1);
        AddQuad(_center + new Vector3(-_basePixelSize,  _basePixelSize, 0.0f), halfPixel, halfPixel, _globalProgress, _segProgress, _color, _normal, _uv1);
        AddQuad(_center + new Vector3( _basePixelSize,  _basePixelSize, 0.0f), halfPixel, halfPixel, _globalProgress, _segProgress, _color, _normal, _uv1);
        AddQuad(_center + new Vector3(-_basePixelSize, -_basePixelSize, 0.0f), halfPixel, halfPixel, _globalProgress, _segProgress, _color, _normal, _uv1);
        AddQuad(_center + new Vector3( _basePixelSize, -_basePixelSize, 0.0f), halfPixel, halfPixel, _globalProgress, _segProgress, _color, _normal, _uv1);
    }

    private float GetPseudoRandomRange(int _seed, float _min, float _max)
    {
        float normalized = (GetPseudoRandom(_seed) * 0.5f) + 0.5f;
        return Mathf.Lerp(_min, _max, normalized);
    }

    private float GetPseudoRandom(int _seed)
    {
        int x = _seed;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        float normalized = (float)(Math.Abs(x) % 10000) / 10000.0f;
        return (normalized * 2.0f) - 1.0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (false == previewInEditor) return;
        EnsureMeshInitialized();
        UpdateSortingOrder();
        RebuildActiveMesh();
    }
#endif
}
