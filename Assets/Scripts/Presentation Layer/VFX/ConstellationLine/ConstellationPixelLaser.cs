using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace PresentationLayer.VFX
{

    /// <summary>
    /// 별자리 픽셀 레이저 빔(Constellation Pixel Laser) 컨트롤러입니다.
    /// 성좌 맵 분위기에 맞추어, 링 나무가 파괴되어 소환된 큰 별들이 터질 때
    /// 첫 번째 별부터 마지막 별까지 도미노처럼 순차적으로 레이저가 찍- 뻗어나가며 별들이 연쇄 폭발하는 연출을 재생합니다.
    /// 100% 절차적 픽셀 쿼드 메쉬 + MaterialPropertyBlock 기반으로 런타임 Zero GC Alloc을 보장합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConstellationPixelLaser : MonoBehaviour
    {
        // 외부 풀링 관리자 호환 이벤트 (Boomerang / LightningZapCreator 호환)
        public event Action<ConstellationPixelLaser> ReturnToPoolEvent;

        /// <summary>
        /// 레이저가 순차적으로 다음 별에 도달할 때마다 발생하는 이벤트 (별 인덱스, 별 월드 위치).
        /// 해당 별의 폭발/소멸 애니메이션(TreeStarMarkGroundAnimator.PlayManifestEffect)을 정확히 트리거할 때 사용합니다.
        /// </summary>
        public event Action<int, Vector3> OnStarReachedEvent;

        [Header("렌더링 및 머티리얼 세팅")]
        [SerializeField] private Material laserMaterial;
        [SerializeField] private string sortingLayerName = "FlyingItem";
        [SerializeField] private int sortingOrder = 10;
        [SerializeField] private float beamHalfWidth = 0.035f;

        [Header("색상 및 발광 세팅 (별 표식 매칭 및 커스터마이징)")]
        [SerializeField, ColorUsage(true, true), Tooltip("중심 코어 발광 색상 (은은한 화이트-아이스 블루)")]
        private Color laserCoreColor = new Color(1.2f, 1.35f, 1.5f, 1.0f);
        [SerializeField, ColorUsage(true, true), Tooltip("선단 헤드 별빛 플래시 색상 (눈부심 없는 파스텔 별빛)")]
        private Color laserHeadColor = new Color(1.3f, 1.5f, 1.8f, 1.0f);
        [SerializeField, ColorUsage(true, true), Tooltip("도트 선 시작점 틴트 색상 (정갈한 파스텔 시안)")]
        private Color laserStartColor = new Color(0.5f, 0.85f, 1.2f, 1.0f);
        [SerializeField, ColorUsage(true, true), Tooltip("도트 선 도착점 틴트 색상 (부드러운 아이스 블루)")]
        private Color laserEndColor = new Color(0.4f, 0.75f, 1.1f, 1.0f);
        [SerializeField, ColorUsage(true, true), Tooltip("꼬리 소멸 성운 잔상 색상 (차분한 딥 스카이 블루)")]
        private Color laserTailColor = new Color(0.15f, 0.35f, 0.7f, 1.0f);
        [SerializeField, Range(0.1f, 3.0f), Tooltip("전체 발광/블룸 강도 배율 (기본 1.0, 0.5~0.8로 낮추면 블룸이 더욱 은은해집니다)")]
        private float emissionBoost = 1.0f;

        [Header("레이저 비행 및 연출 세팅 (상호 양방향 동시 발사)")]
        [SerializeField, Range(0.05f, 1.5f), Tooltip("유성 꼬리(Trail) 길이 비율 (0.05~1.5)")]
        private float tailLength = 0.85f;
        [SerializeField, Tooltip("한 세그먼트(별과 별 사이)를 레이저가 뻗어나가는 시간(초)")]
        private float segmentShootDuration = 0.14f;
        [SerializeField, Tooltip("전면 동시 발사 후 전체 별자리 영역 형태가 환하게 유지되는 시간(초)")]
        private float fullAreaSustainDuration = 0.15f;
        [SerializeField, Range(0.5f, 1.0f), Tooltip("빔 선단이 이 진행도에 이르면 별에 도달한 것으로 보고 OnStarReachedEvent를 발생시킵니다. " +
            "선단은 감속 곡선(ease-out)으로 날아가 끝부분 몇 픽셀을 오래 채우므로, 1.0이면 눈에 보이는 도착보다 이벤트가 늦습니다. (1.0 = 비행 완료 시점)")]
        private float starReachProgressThreshold = 1.0f;

        [Header("지그재그 (Zigzag) 형태 세팅")]
        [SerializeField, Tooltip("지그재그 꺾임 폭 (월드 단위, 32 PPU 기준 0.09375 = 3픽셀)")]
        private float zigzagAmplitude = 0.09375f;
        [SerializeField, Tooltip("지그재그 한 번 꺾이는 주기 간격 (월드 단위)")]
        private float zigzagStepLength = 0.2f;

        [Header("곡선화 (Curved Spline) 세팅")]
        [SerializeField, Range(0.0f, 1.0f), Tooltip("성좌 외곽선 둥글기 강도 (0 = 각진 다각형, 0.5~0.7 = 유려한 곡선, 1 = 원형에 가까운 둥근 루프)")]
        private float curveRoundness = 0.65f;

        [Header("선 꼬임 방지 (Auto Untangle) 세팅")]
        [SerializeField, Tooltip("발사 시 선 꼬임(X자 교차)을 자동으로 감지하여 외곽 순서로 정돈 후 발사")]
        private bool autoUntangle = true;

        [Header("최적화 세팅")]
        [SerializeField] private int maxPrewarmedSegments = 12;
        [SerializeField] private bool disableObjectOnComplete = true;

        [Header("에디터 테스트 좌표 및 폐곡선 옵션")]
        [SerializeField] private List<Vector3> testNodes = new List<Vector3>();
        [SerializeField] private bool testIsClosedLoop = true;

        public float TailLength
        {
            get => tailLength;
            set => tailLength = value;
        }

        public float SegmentShootDuration
        {
            get => segmentShootDuration;
            set => segmentShootDuration = value;
        }

        public float ZigzagAmplitude
        {
            get => zigzagAmplitude;
            set => zigzagAmplitude = value;
        }

        public float ZigzagStepLength
        {
            get => zigzagStepLength;
            set => zigzagStepLength = value;
        }

        public float CurveRoundness
        {
            get => curveRoundness;
            set => curveRoundness = value;
        }

        public bool AutoUntangle
        {
            get => autoUntangle;
            set => autoUntangle = value;
        }

        public float FullAreaSustainDuration
        {
            get => fullAreaSustainDuration;
            set => fullAreaSustainDuration = value;
        }

        public float StarReachProgressThreshold
        {
            get => starReachProgressThreshold;
            set => starReachProgressThreshold = Mathf.Clamp(value, 0.5f, 1.0f);
        }

        public Color LaserCoreColor
        {
            get => laserCoreColor;
            set => laserCoreColor = value;
        }

        public Color LaserHeadColor
        {
            get => laserHeadColor;
            set => laserHeadColor = value;
        }

        public Color LaserStartColor
        {
            get => laserStartColor;
            set => laserStartColor = value;
        }

        public Color LaserEndColor
        {
            get => laserEndColor;
            set => laserEndColor = value;
        }

        public Color LaserTailColor
        {
            get => laserTailColor;
            set => laserTailColor = value;
        }

        public float EmissionBoost
        {
            get => emissionBoost;
            set => emissionBoost = value;
        }

        /// <summary>
        /// 레이저 발광 색상을 일괄 변경합니다 (Zero GC).
        /// </summary>
        public void SetLaserColors(Color _core, Color _head, Color _bodyStart, Color _bodyEnd, Color _tail)
        {
            laserCoreColor = _core;
            laserHeadColor = _head;
            laserStartColor = _bodyStart;
            laserEndColor = _bodyEnd;
            laserTailColor = _tail;
        }

        // 32 PPU 픽셀 격자 정수 스냅 상수 (Zero GC)
        private const float BasePPU = 32.0f;
        private const float PixelUnit = 1.0f / BasePPU; // 0.03125f (1픽셀 기본 유닛)
        private const float HalfPixel = PixelUnit * 0.5f;

        // 세그먼트 풀
        private readonly List<PixelLaserSegment> segmentPool = new List<PixelLaserSegment>();
        private readonly List<Vector3> activePointsBuffer = new List<Vector3>(16);

        // 런타임 상태
        private Coroutine activePlayRoutine;
        private IObjectPool<ConstellationPixelLaser> managedPool;
        private bool isPlaying = false;

        // 셰이더 프로퍼티 ID 캐싱 (Zero GC)
        private static readonly int PropProgress = Shader.PropertyToID("_Progress");
        private static readonly int PropTailLength = Shader.PropertyToID("_TailLength");
        private static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        private static readonly int PropHeadColor = Shader.PropertyToID("_HeadColor");
        private static readonly int PropTailColor = Shader.PropertyToID("_TailColor");
        private static readonly int PropEmissionBoost = Shader.PropertyToID("_EmissionBoost");

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureSegmentsPrewarmed(maxPrewarmedSegments);
        }

        private void Reset()
        {
            // 인스펙터 기본 테스트 5각 별자리 좌표 자동 세팅
            testNodes.Clear();
            testNodes.Add(new Vector3(-3.0f, 0.0f, 0.0f));
            testNodes.Add(new Vector3(-0.5f, 2.0f, 0.0f));
            testNodes.Add(new Vector3(2.5f, 1.2f, 0.0f));
            testNodes.Add(new Vector3(1.8f, -1.8f, 0.0f));
            testNodes.Add(new Vector3(-1.5f, -1.5f, 0.0f));
            testIsClosedLoop = true;
        }

        private void OnDisable()
        {
            StopCurrentRoutine();
        }

        private void OnDestroy()
        {
            StopCurrentRoutine();
            for (int i = 0; i < segmentPool.Count; i++)
            {
                if (null != segmentPool[i])
                {
                    segmentPool[i].Dispose();
                }
            }
            segmentPool.Clear();
        }

        #endregion

        #region Public APIs (실전 인게임 및 호환용)

        /// <summary>
        /// 외부 IObjectPool 주입
        /// </summary>
        public void SetPool(IObjectPool<ConstellationPixelLaser> _pool)
        {
            managedPool = _pool;
        }

        /// <summary>
        /// 오브젝트 풀에 안전하게 반환합니다.
        /// </summary>
        public void ReturnToPool()
        {
            StopCurrentRoutine();
            HideAllSegments();
            isPlaying = false;

            if (null != ReturnToPoolEvent)
            {
                ReturnToPoolEvent.Invoke(this);
            }
            else if (null != managedPool)
            {
                managedPool.Release(this);
            }
            else if (true == disableObjectOnComplete)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 소팅 레이어 및 오더를 설정합니다.
        /// </summary>
        public void SetSortingOrder(string _layerName, int _order)
        {
            sortingLayerName = _layerName;
            sortingOrder = _order;
            for (int i = 0; i < segmentPool.Count; i++)
            {
                segmentPool[i].UpdateSorting(sortingLayerName, sortingOrder);
            }
        }

        /// <summary>
        /// 별자리 픽셀 레이저를 발사합니다 (상호 양방향 전면 동시 발사, Zero GC).
        /// 각 변(에지)마다 정방향과 역방향 2개의 빔이 상호 교차하며 동시에 발사되어 눈부신 성좌 마법진을 형성합니다.
        /// </summary>
        /// <param name="_points">별들의 월드 좌표 리스트</param>
        /// <param name="_isClosedLoop">마지막 별에서 첫 번째 별로 닫는 루프 여부</param>
        public void PlayLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = true)
        {
            if (null == _points || 2 > _points.Count) return;

            activePointsBuffer.Clear();
            for (int i = 0; i < _points.Count; i++)
            {
                activePointsBuffer.Add(_points[i]);
            }
            if (true == autoUntangle && 3 <= activePointsBuffer.Count)
            {
                UntanglePoints(activePointsBuffer, 0);
            }

            StopCurrentRoutine();
            gameObject.SetActive(true);
            activePlayRoutine = StartCoroutine(MutualSimultaneousRoutine(activePointsBuffer, _isClosedLoop));
        }

        #region Compatibility Forwarders (기존 외부 연동 호환성 100% 보장)

        /// <summary> (호환용) 상호 양방향 전면 동시 발사 </summary>
        public void PlayMutualSimultaneousLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = true)
        {
            PlayLaser(_points, _isClosedLoop);
        }

        /// <summary> (호환용) 도미노 레이저 발사 API </summary>
        public void PlayDominoLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = false)
        {
            PlayLaser(_points, _isClosedLoop);
        }

        /// <summary> (호환용) 전면 동시 발사 </summary>
        public void PlaySimultaneousLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = true)
        {
            PlayLaser(_points, _isClosedLoop);
        }

        /// <summary> (호환용) 순차 발사 </summary>
        public void PlaySequentialDominoLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = false)
        {
            PlayLaser(_points, _isClosedLoop);
        }

        /// <summary> (호환용) 양방향 발사 </summary>
        public void PlayBidirectionalDominoLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = true)
        {
            PlayLaser(_points, _isClosedLoop);
        }

        /// <summary> (호환용) 궤도 선행 빔 발사 </summary>
        public void PlayOrbitGuideMeteorLaser(IReadOnlyList<Vector3> _points, bool _isClosedLoop = true)
        {
            PlayLaser(_points, _isClosedLoop);
        }

        /// <summary>
        /// 시작점(A)과 끝점(B) 단일 선분 레이저를 양방향 상호 교차 발사합니다.
        /// </summary>
        public void PlayLaser(Vector3 _startPos, Vector3 _endPos)
        {
            activePointsBuffer.Clear();
            activePointsBuffer.Add(_startPos);
            activePointsBuffer.Add(_endPos);

            StopCurrentRoutine();
            gameObject.SetActive(true);
            activePlayRoutine = StartCoroutine(MutualSimultaneousRoutine(activePointsBuffer, false));
        }

        /// <summary> (기존 VFX_LightningZap 100% 호환 오버로드) </summary>
        public void PlayZap(IReadOnlyList<Vector3> _points, int _count)
        {
            if (null == _points || 2 > _count) return;

            activePointsBuffer.Clear();
            int validCount = Mathf.Min(_points.Count, _count);
            for (int i = 0; i < validCount; i++)
            {
                activePointsBuffer.Add(_points[i]);
            }

            StopCurrentRoutine();
            gameObject.SetActive(true);
            activePlayRoutine = StartCoroutine(MutualSimultaneousRoutine(activePointsBuffer, false));
        }

        /// <summary> (기존 VFX_LightningZap 호환 단일 발사) </summary>
        public void PlayZap(Vector3 _startPos, Vector3 _endPos)
        {
            PlayLaser(_startPos, _endPos);
        }

        // 인스펙터 테스트 버튼 - 에디터에서 사람이 눌러야만 실행되므로 빌드에는 넣지 않는다.
#if UNITY_EDITOR
        /// <summary>
        /// 상호 양방향 전면 동시 발사(1<>2<>3 상호 교차 발사)를 즉시 테스트합니다.
        /// </summary>
        [ContextMenu("Test Fire Laser (Mutual Simultaneous)")]
        public void TestFireLaser()
        {
            if (null != testNodes && 1 < testNodes.Count)
            {
                PlayLaser(testNodes, testIsClosedLoop);
            }
        }

        /// <summary> (호환용 테스트 트리거) </summary>
        public void TestFireMutualSimultaneousLaser() => TestFireLaser();
#endif

        #endregion

        // 인스펙터 테스트 버튼 - 에디터에서 사람이 눌러야만 실행되므로 빌드에는 넣지 않는다.
#if UNITY_EDITOR
        /// <summary>
        /// 테스트로 켜진 레이저 표시를 화면에서 즉시 정리합니다.
        /// </summary>
        public void ClearTestDisplay()
        {
            StopCurrentRoutine();
            HideAllSegments();
            isPlaying = false;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
#endif

        /// <summary>
        /// 플레이 모드가 아닌 에디터 편집 모드(Edit Mode)에서도 씬 뷰에 즉시 레이저를 고정 렌더링합니다.
        /// </summary>
        public void PreviewHoldInEditor(IReadOnlyList<Vector3> _points, bool _isClosedLoop)
        {
            if (null == _points || 2 > _points.Count) return;

            StopCurrentRoutine();
            gameObject.SetActive(true);

            activePointsBuffer.Clear();
            for (int i = 0; i < _points.Count; i++)
            {
                activePointsBuffer.Add(_points[i]);
            }
            if (true == autoUntangle && 3 <= activePointsBuffer.Count)
            {
                UntanglePoints(activePointsBuffer, 0);
            }

            bool bLoop = true == _isClosedLoop && 2 < activePointsBuffer.Count;
            int segmentCount = true == bLoop ? activePointsBuffer.Count : activePointsBuffer.Count - 1;
            EnsureSegmentsPrewarmed(segmentCount);

            for (int i = 0; i < segmentCount; i++)
            {
                ComputeBezierControlPoints(activePointsBuffer, i, bLoop, curveRoundness, out Vector3 pStart, out Vector3 pEnd, out Vector3 c0, out Vector3 c1);

                PixelLaserSegment segment = segmentPool[i];
                segment.Setup(
                    this.transform,
                    pStart,
                    pEnd,
                    c0,
                    c1,
                    beamHalfWidth,
                    laserMaterial,
                    sortingLayerName,
                    sortingOrder,
                    zigzagAmplitude,
                    zigzagStepLength,
                    0.0f,
                    laserStartColor,
                    laserEndColor,
                    laserCoreColor,
                    laserHeadColor,
                    laserTailColor,
                    emissionBoost);
                segment.SetProgress(1.0f, 2.0f); // TailLength = 2.0f 전체 표시
                segment.SetActive(true);
            }

            for (int s = segmentCount; s < segmentPool.Count; s++)
            {
                segmentPool[s].SetActive(false);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        #endregion

        #region Mutual Simultaneous Routine (Zero GC)

        private struct SimultaneousStep
        {
            public PixelLaserSegment segment;
            public Vector3 startPt;
            public Vector3 endPt;
            public int targetStarIndex;
            public float duration;
            public float elapsed;
            public bool reached;
            public bool starEventFired;
        }

        private readonly List<SimultaneousStep> simultaneousSteps = new List<SimultaneousStep>(16);

        private void StopCurrentRoutine()
        {
            if (null != activePlayRoutine)
            {
                StopCoroutine(activePlayRoutine);
                activePlayRoutine = null;
            }
        }

        private void HideAllSegments()
        {
            for (int i = 0; i < segmentPool.Count; i++)
            {
                segmentPool[i].SetActive(false);
            }
        }


        /// <summary>
        /// 연결된 모든 지점으로 상호 양방향 동시 발사 코루틴 (Zero GC).
        /// 1 <> 2 <> 3 구조라면 1번은 2번으로, 2번은 1번과 3번 양쪽으로, 3번은 2번으로 동시 발사합니다.
        /// 폐곡선인 경우 모든 변에서 마주보는 두 별이 서로를 향해 2개의 빔을 교차 발사하며,
        /// 역위상 지그재그(De-phasing)를 적용하여 두 빔이 아름답게 엇갈려 비행합니다.
        /// </summary>
        private IEnumerator MutualSimultaneousRoutine(List<Vector3> _points, bool _isClosedLoop)
        {
            isPlaying = true;
            HideAllSegments();

            int ptCount = _points.Count;
            if (2 > ptCount)
            {
                isPlaying = false;
                yield break;
            }

            // 변의 개수: 폐곡선(루프)이면 ptCount개, 개곡선이면 ptCount - 1개
            bool bLoop = true == _isClosedLoop && 2 < ptCount;
            int edgeCount = (true == bLoop) ? ptCount : ptCount - 1;
            int totalSegments = edgeCount * 2; // 각 변마다 정방향(A->B)과 역방향(B->A) 총 2개씩 발사!

            EnsureSegmentsPrewarmed(totalSegments);
            simultaneousSteps.Clear();

            int segIndex = 0;
            for (int i = 0; i < edgeCount; i++)
            {
                int idxA = i;
                int idxB = (i + 1) % ptCount;

                ComputeBezierControlPoints(_points, i, bLoop, curveRoundness, out Vector3 pA, out Vector3 pB, out Vector3 cA, out Vector3 cB);

                // 1) 정방향 빔: A -> B (목표: idxB)
                float durForward = Mathf.Max(0.04f, segmentShootDuration * UnityEngine.Random.Range(0.9f, 1.1f));
                PixelLaserSegment segForward = segmentPool[segIndex++];
                segForward.Setup(
                    this.transform,
                    pA,
                    pB,
                    cA,
                    cB,
                    beamHalfWidth,
                    laserMaterial,
                    sortingLayerName,
                    sortingOrder,
                    zigzagAmplitude,
                    zigzagStepLength,
                    0.0f, // 정위상 지그재그
                    laserStartColor,
                    laserEndColor,
                    laserCoreColor,
                    laserHeadColor,
                    laserTailColor,
                    emissionBoost);
                segForward.SetProgress(0.0f, tailLength);
                segForward.SetActive(true);

                simultaneousSteps.Add(new SimultaneousStep
                {
                    segment = segForward,
                    startPt = pA,
                    endPt = pB,
                    targetStarIndex = idxB,
                    duration = durForward,
                    elapsed = 0.0f,
                    reached = false,
                    starEventFired = false
                });

                // 2) 역방향 빔: B -> A (목표: idxA)
                float durBackward = Mathf.Max(0.04f, segmentShootDuration * UnityEngine.Random.Range(0.9f, 1.1f));
                PixelLaserSegment segBackward = segmentPool[segIndex++];
                segBackward.Setup(
                    this.transform,
                    pB,
                    pA,
                    cB,
                    cA,
                    beamHalfWidth,
                    laserMaterial,
                    sortingLayerName,
                    sortingOrder,
                    zigzagAmplitude,
                    zigzagStepLength,
                    0.5f, // 역위상 지그재그 (0.5주기 시프트로 교차 파형 형성!)
                    laserStartColor,
                    laserEndColor,
                    laserCoreColor,
                    laserHeadColor,
                    laserTailColor,
                    emissionBoost);
                segBackward.SetProgress(0.0f, tailLength);
                segBackward.SetActive(true);

                simultaneousSteps.Add(new SimultaneousStep
                {
                    segment = segBackward,
                    startPt = pB,
                    endPt = pA,
                    targetStarIndex = idxA,
                    duration = durBackward,
                    elapsed = 0.0f,
                    reached = false,
                    starEventFired = false
                });
            }

            // Phase 1: 모든 빔 동시 비행 (0.0 -> 1.0) & 각자 도달 시점 개별 이벤트 트리거
            bool allReached = false;
            while (false == allReached)
            {
                float dt = Time.deltaTime;
                allReached = true;

                for (int i = 0; i < simultaneousSteps.Count; i++)
                {
                    SimultaneousStep step = simultaneousSteps[i];
                    if (false == step.reached)
                    {
                        step.elapsed += dt;
                        float t = Mathf.Clamp01(step.elapsed / step.duration);
                        float progress = 1.0f - Mathf.Pow(1.0f - t, 4.0f);
                        step.segment.SetProgress(progress, tailLength);

                        bool bFlightDone = step.duration <= step.elapsed;
                        if (true == bFlightDone)
                        {
                            step.reached = true;
                            step.segment.SetProgress(1.0f, tailLength);
                        }
                        else
                        {
                            allReached = false;
                        }

                        // 도달 이벤트는 비행 완료와 별개로, 선단이 눈에 보이게 별에 닿는 진행도에서 한 번만 보낸다.
                        // 임계값이 1.0이면 비행 완료 시점에만 보낸다 - float 반올림으로 t ≈ 0.987부터 progress가
                        // 이미 1.0f가 되므로, 진행도 비교에 맡기면 기존보다 한 프레임가량 먼저 나갈 수 있다.
                        bool bReachedByProgress = 1.0f > starReachProgressThreshold && starReachProgressThreshold <= progress;
                        if (false == step.starEventFired && (true == bFlightDone || true == bReachedByProgress))
                        {
                            step.starEventFired = true;
                            OnStarReachedEvent?.Invoke(step.targetStarIndex, step.endPt);
                        }
                        simultaneousSteps[i] = step;
                    }
                }

                yield return null;
            }

            // 모든 선분 도달 완료(1.0f) 보장
            for (int i = 0; i < simultaneousSteps.Count; i++)
            {
                simultaneousSteps[i].segment.SetProgress(1.0f, tailLength);
            }

            // Phase 2: 성좌 전 영역 동시 발광 유지 (Full Area Reveal & Sustain)
            if (0.0f < fullAreaSustainDuration)
            {
                float sustainElapsed = 0.0f;
                while (sustainElapsed < fullAreaSustainDuration)
                {
                    sustainElapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // Phase 3: 모든 선분 일제히 부드러운 꼬리 페이드아웃
            float fadeElapsed = 0.0f;
            float fadeDuration = Mathf.Max(0.05f, segmentShootDuration * tailLength);
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float tFade = Mathf.Clamp01(fadeElapsed / fadeDuration);
                float tailProgress = 1.0f + tFade * tailLength;

                for (int i = 0; i < simultaneousSteps.Count; i++)
                {
                    simultaneousSteps[i].segment.SetProgress(tailProgress, tailLength);
                }
                yield return null;
            }

            HideAllSegments();
            isPlaying = false;
            activePlayRoutine = null;

            ReturnToPool();
        }

        #endregion

        #region Bezier Spline Curvature (Zero GC)

        /// <summary>
        /// P_curr과 P_next 사이를 유려하게 이어주는 3차 베지에 제어점(C0, C1)을 산출합니다 (Zero GC).
        /// roundness가 0이면 직선 분할(1/3, 2/3), 1이면 완벽한 Centripetal Catmull-Rom 스플라인이 됩니다.
        /// </summary>
        public static void ComputeBezierControlPoints(
            IReadOnlyList<Vector3> _points,
            int _index,
            bool _isClosedLoop,
            float _roundness,
            out Vector3 _p0,
            out Vector3 _p1,
            out Vector3 _ctrl0,
            out Vector3 _ctrl1)
        {
            int count = _points.Count;
            _p0 = _points[_index];
            _p1 = _points[(_index + 1) % count];

            Vector3 delta = _p1 - _p0;
            if (0.0001f >= _roundness || 3 > count)
            {
                _ctrl0 = _p0 + delta * (1.0f / 3.0f);
                _ctrl1 = _p1 - delta * (1.0f / 3.0f);
                return;
            }

            Vector3 pPrev;
            Vector3 pNext2;

            if (true == _isClosedLoop)
            {
                pPrev = _points[(_index - 1 + count) % count];
                pNext2 = _points[(_index + 2) % count];
            }
            else
            {
                pPrev = (0 < _index) ? _points[_index - 1] : _p0 - delta;
                pNext2 = (_index + 2 < count) ? _points[_index + 2] : _p1 + delta;
            }

            // Catmull-Rom Tangents
            Vector3 tan0 = (_p1 - pPrev) * 0.5f;
            Vector3 tan1 = (pNext2 - _p0) * 0.5f;

            // 직선 탄젠트와 Catmull-Rom 탄젠트를 roundness 비율로 블렌딩
            Vector3 finalTan0 = Vector3.Lerp(delta, tan0, _roundness);
            Vector3 finalTan1 = Vector3.Lerp(delta, tan1, _roundness);

            _ctrl0 = _p0 + finalTan0 * (1.0f / 3.0f);
            _ctrl1 = _p1 - finalTan1 * (1.0f / 3.0f);
        }

        #endregion

        #region Auto Untangle Algorithms (Zero GC)

        private struct PointAngle
        {
            public Vector3 point;
            public float angle;
        }

        private static readonly List<PointAngle> angleBuffer = new List<PointAngle>(32);
        private static readonly List<Vector3> reorderBuffer = new List<Vector3>(32);
        private static readonly Comparison<PointAngle> AngleComparison = (a, b) => a.angle.CompareTo(b.angle);

        /// <summary>
        /// 인스펙터/에디터의 testNodes를 원주 각도순 및 2-Opt 선분 교차 검증을 거쳐 100% 꼬임 없는 외곽선 순서로 정돈합니다.
        /// </summary>
        public void AutoUntangleNodes()
        {
            if (null == testNodes || 3 > testNodes.Count) return;
            UntanglePoints(testNodes, 0);
        }

        /// <summary>
        /// 임의 순서의 좌표 리스트를 2:1 아이소메트릭 방위각 정렬과 2-Opt 교차 검증으로 무할당(Zero GC) 자동 정돈합니다.
        /// </summary>
        /// <param name="_points">정돈할 좌표 리스트</param>
        /// <param name="_startNodeIndex">출발점으로 보존할 원래 노드 인덱스</param>
        public static void UntanglePoints(List<Vector3> _points, int _startNodeIndex = 0)
        {
            if (null == _points || 3 > _points.Count) return;

            int count = _points.Count;
            int safeStartIdx = Mathf.Clamp(_startNodeIndex, 0, count - 1);
            Vector3 startPt = _points[safeStartIdx];

            // 1. 무게중심(Centroid) 계산
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                centroid.x += _points[i].x;
                centroid.y += _points[i].y;
            }
            centroid /= (float)count;

            // 2. 2:1 아이소메트릭 비율 보정 방위각(Polar Angle) 산출
            angleBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                Vector3 p = _points[i];
                float angle = Mathf.Atan2((p.y - centroid.y) * 2.0f, p.x - centroid.x);
                angleBuffer.Add(new PointAngle { point = p, angle = angle });
            }

            // 3. 각도 순 정렬
            angleBuffer.Sort(AngleComparison);

            // 4. 원래 시작 노드(startPt)의 정렬 후 위치 탐색
            reorderBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                reorderBuffer.Add(angleBuffer[i].point);
            }

            int foundStartPos = 0;
            float minDistSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float dSq = (reorderBuffer[i] - startPt).sqrMagnitude;
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    foundStartPos = i;
                }
            }

            // 5. 시작 노드를 0번으로 보존하는 원형 회전(Circular Shift)
            _points.Clear();
            for (int i = 0; i < count; i++)
            {
                _points.Add(reorderBuffer[(foundStartPos + i) % count]);
            }

            // 6. 2-Opt 선분 교차 검증 및 해소 (오목 다각형/비정형 배치 완벽 방어)
            bool improved = true;
            int maxIter = 10;
            while (true == improved && 0 < maxIter--)
            {
                improved = false;
                for (int i = 0; i < count; i++)
                {
                    int iNext = (i + 1) % count;
                    Vector2 a = _points[i];
                    Vector2 b = _points[iNext];

                    for (int j = i + 2; j < count; j++)
                    {
                        int jNext = (j + 1) % count;
                        if (0 == i && 0 == jNext) continue; // 폐곡선 인접 변

                        Vector2 c = _points[j];
                        Vector2 d = _points[jNext];

                        if (true == DoLinesIntersect(a, b, c, d))
                        {
                            ReverseSubSegment(_points, i + 1, j);
                            improved = true;
                            break;
                        }
                    }
                    if (true == improved) break;
                }
            }

            // 7. 2-Opt 스왑으로 인해 시작점이 뒤바뀌었을 경우 재회전
            int finalStartPos = 0;
            minDistSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float dSq = (_points[i] - startPt).sqrMagnitude;
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    finalStartPos = i;
                }
            }

            if (0 != finalStartPos)
            {
                reorderBuffer.Clear();
                for (int i = 0; i < count; i++)
                {
                    reorderBuffer.Add(_points[i]);
                }
                _points.Clear();
                for (int i = 0; i < count; i++)
                {
                    _points.Add(reorderBuffer[(finalStartPos + i) % count]);
                }
            }
        }

        private static bool DoLinesIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d1 = Direction(p3, p4, p1);
            float d2 = Direction(p3, p4, p2);
            float d3 = Direction(p1, p2, p3);
            float d4 = Direction(p1, p2, p4);

            if (((d1 > 0.0001f && d2 < -0.0001f) || (d1 < -0.0001f && d2 > 0.0001f)) &&
                ((d3 > 0.0001f && d4 < -0.0001f) || (d3 < -0.0001f && d4 > 0.0001f)))
            {
                return true;
            }
            return false;
        }

        private static float Direction(Vector2 pi, Vector2 pj, Vector2 pk)
        {
            return (pk.x - pi.x) * (pj.y - pi.y) - (pj.x - pi.x) * (pk.y - pi.y);
        }

        private static void ReverseSubSegment(List<Vector3> _list, int _from, int _to)
        {
            int left = _from;
            int right = _to;
            while (left < right)
            {
                Vector3 temp = _list[left];
                _list[left] = _list[right];
                _list[right] = temp;
                left++;
                right--;
            }
        }

        #endregion

        #region Internal Segment Pool & Builder

        private void EnsureSegmentsPrewarmed(int _requiredCount)
        {
            // 1. 이미 하이라키(자식)에 존재하는 LaserSegment 오브젝트가 있다면 먼저 풀에 수집 및 바인딩
            if (0 == segmentPool.Count)
            {
                int childCount = transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.name.StartsWith("LaserSegment"))
                    {
                        PixelLaserSegment existing = new PixelLaserSegment(child.gameObject, laserMaterial);
                        existing.UpdateSorting(sortingLayerName, sortingOrder);
                        existing.SetActive(false);
                        segmentPool.Add(existing);
                    }
                }
            }

            // 2. 부족한 만큼 신규 생성
            while (segmentPool.Count < _requiredCount)
            {
                int index = segmentPool.Count;
                GameObject segGo = new GameObject($"LaserSegment_{index}");
                segGo.transform.SetParent(this.transform, false);

                PixelLaserSegment segment = new PixelLaserSegment(segGo, laserMaterial);
                segment.UpdateSorting(sortingLayerName, sortingOrder);
                segment.SetActive(false);
                segmentPool.Add(segment);
            }
        }

        /// <summary>
        /// 단일 레이저 선분을 32 PPU 픽셀 도트 및 십자별로 렌더링하는 절차적 메쉬 세그먼트 (Zero GC)
        /// </summary>
        private class PixelLaserSegment
        {
            public readonly GameObject gameObject;
            private readonly MeshFilter meshFilter;
            private readonly MeshRenderer meshRenderer;
            private Mesh proceduralMesh;
            private readonly MaterialPropertyBlock propBlock;

            // Zero GC 무할당 정적 메쉬 캐시 버퍼
            private static readonly List<Vector3> segVertices = new List<Vector3>(256);
            private static readonly List<Vector2> segUVs = new List<Vector2>(256);
            private static readonly List<Color> segColors = new List<Color>(256);
            private static readonly List<int> segTriangles = new List<int>(384);

            public PixelLaserSegment(GameObject _go, Material _material)
            {
                gameObject = _go;
                meshFilter = _go.GetComponent<MeshFilter>();
                if (null == meshFilter) meshFilter = _go.AddComponent<MeshFilter>();

                meshRenderer = _go.GetComponent<MeshRenderer>();
                if (null == meshRenderer) meshRenderer = _go.AddComponent<MeshRenderer>();

                if (null != _material)
                {
                    meshRenderer.sharedMaterial = _material;
                }
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                propBlock = new MaterialPropertyBlock();
                EnsureMesh();
            }

            private void EnsureMesh()
            {
                if (null == proceduralMesh)
                {
                    if (null != meshFilter && null != meshFilter.sharedMesh)
                    {
                        proceduralMesh = meshFilter.sharedMesh;
                    }
                    else
                    {
                        proceduralMesh = new Mesh
                        {
                            name = "PixelLaser_SegmentMesh"
                        };
                        proceduralMesh.MarkDynamic();
                        if (null != meshFilter)
                        {
                            meshFilter.sharedMesh = proceduralMesh;
                        }
                    }
                }
                else if (null != meshFilter && meshFilter.sharedMesh != proceduralMesh)
                {
                    meshFilter.sharedMesh = proceduralMesh;
                }
            }

            private static void AddQuad(Vector3 _center, float _halfW, float _halfH, float _progress, Color _color)
            {
                int vIndex = segVertices.Count;

                segVertices.Add(new Vector3(_center.x - _halfW, _center.y - _halfH, 0.0f));
                segVertices.Add(new Vector3(_center.x - _halfW, _center.y + _halfH, 0.0f));
                segVertices.Add(new Vector3(_center.x + _halfW, _center.y + _halfH, 0.0f));
                segVertices.Add(new Vector3(_center.x + _halfW, _center.y - _halfH, 0.0f));

                Vector2 uv = new Vector2(_progress, 0.0f);
                segUVs.Add(uv);
                segUVs.Add(uv);
                segUVs.Add(uv);
                segUVs.Add(uv);

                segColors.Add(_color);
                segColors.Add(_color);
                segColors.Add(_color);
                segColors.Add(_color);

                segTriangles.Add(vIndex);
                segTriangles.Add(vIndex + 1);
                segTriangles.Add(vIndex + 2);

                segTriangles.Add(vIndex);
                segTriangles.Add(vIndex + 2);
                segTriangles.Add(vIndex + 3);
            }

            private static void AddDot(Vector3 _center, float _progress, Color _color)
            {
                AddQuad(_center, HalfPixel, HalfPixel, _progress, _color);
            }

            private static void AddCrossStar(Vector3 _center, float _progress, Color _color)
            {
                // 3x3 픽셀 십자별 (가로바 + 세로바)
                AddQuad(_center, HalfPixel * 2.5f, HalfPixel, _progress, _color);
                AddQuad(_center, HalfPixel, HalfPixel * 2.5f, _progress, _color);
            }

            public void SetColors(Color _core, Color _head, Color _tail, float _boost = 1.0f)
            {
                meshRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(PropCoreColor, _core);
                propBlock.SetColor(PropHeadColor, _head);
                propBlock.SetColor(PropTailColor, _tail);
                propBlock.SetFloat(PropEmissionBoost, _boost);
                meshRenderer.SetPropertyBlock(propBlock);
            }

            public void Setup(
                Transform _ownerTransform,
                Vector3 _start,
                Vector3 _end,
                Vector3 _ctrl0,
                Vector3 _ctrl1,
                float _halfWidth,
                Material _material,
                string _layerName,
                int _order,
                float _zigzagAmp,
                float _zigzagStep,
                float _zigzagPhase,
                Color _startCol,
                Color _endCol,
                Color _coreCol,
                Color _headCol,
                Color _tailCol,
                float _boost = 1.0f)
            {
                EnsureMesh();
                if (null == proceduralMesh) return;

                if (null != _material && meshRenderer.sharedMaterial != _material)
                {
                    meshRenderer.sharedMaterial = _material;
                }

                SetColors(_coreCol, _headCol, _tailCol, _boost);

                // 월드 좌표를 부모 Transform 기준의 로컬 좌표로 변환
                Vector3 localStart = null != _ownerTransform ? _ownerTransform.InverseTransformPoint(_start) : _start;
                Vector3 localEnd = null != _ownerTransform ? _ownerTransform.InverseTransformPoint(_end) : _end;
                Vector3 localC0 = null != _ownerTransform ? _ownerTransform.InverseTransformPoint(_ctrl0) : _ctrl0;
                Vector3 localC1 = null != _ownerTransform ? _ownerTransform.InverseTransformPoint(_ctrl1) : _ctrl1;
                localStart.z = 0.0f;
                localEnd.z = 0.0f;
                localC0.z = 0.0f;
                localC1.z = 0.0f;

                // 3차 베지에 호의 근사 호장(Arc Length) 계산
                float chordLength = (localEnd - localStart).magnitude;
                float polyLength = (localC0 - localStart).magnitude + (localC1 - localC0).magnitude + (localEnd - localC1).magnitude;
                float length = (chordLength + polyLength) * 0.5f;

                Vector3 defaultDir = 0.0001f < chordLength ? (localEnd - localStart) / chordLength : Vector3.right;

                segVertices.Clear();
                segUVs.Clear();
                segColors.Clear();
                segTriangles.Clear();

                // 2픽셀 간격(PixelUnit * 2.0f)으로 촘촘하고 팽팽한 성좌 도트 실선 배치
                float stepDist = PixelUnit * 2.0f;
                float currentDist = 0.0f;
                int dotIndex = 0;

                float safeStep = Mathf.Max(PixelUnit * 2.0f, _zigzagStep);

                while (currentDist <= length)
                {
                    float normT = 0.0001f < length ? Mathf.Clamp01(currentDist / length) : 0.0f;

                    // 3차 베지에 위치 Q(t) 산출
                    float oneMinusT = 1.0f - normT;
                    float b0 = oneMinusT * oneMinusT * oneMinusT;
                    float b1 = 3.0f * oneMinusT * oneMinusT * normT;
                    float b2 = 3.0f * oneMinusT * normT * normT;
                    float b3 = normT * normT * normT;

                    Vector3 curvePt = b0 * localStart + b1 * localC0 + b2 * localC1 + b3 * localEnd;

                    // 3차 베지에 진행 접선 벡터 Q'(t) 산출
                    Vector3 tangent = 3.0f * oneMinusT * oneMinusT * (localC0 - localStart)
                                    + 6.0f * oneMinusT * normT * (localC1 - localC0)
                                    + 3.0f * normT * normT * (localEnd - localC1);
                    float tanLen = tangent.magnitude;
                    Vector3 curDir = 0.0001f < tanLen ? tangent / tanLen : defaultDir;
                    Vector3 curPerp = new Vector3(-curDir.y, curDir.x, 0.0f); // 곡선 진행 방향에 수직인 법선 벡터

                    // 양 끝점 밀착 앵커 엔벨로프 (시작점과 끝점에서 오프셋 0 수렴)
                    float envelope = Mathf.Sin(normT * Mathf.PI);

                    // 샤프 선형 삼각파 지그재그 + 역위상 지원 (_zigzagPhase)
                    float cycle = (currentDist / safeStep) + _zigzagPhase;
                    float zigzagNorm = (Mathf.PingPong(cycle, 1.0f) - 0.5f) * 2.0f; // -1.0 ~ 1.0
                    float perpOffset = zigzagNorm * _zigzagAmp * envelope;

                    // 32 PPU 픽셀 격자 정수 스냅 강제 (도트 깨짐 원천 차단)
                    perpOffset = Mathf.Round(perpOffset / PixelUnit) * PixelUnit;

                    Vector3 dotPos = curvePt + curPerp * perpOffset;
                    dotPos.x = Mathf.Round(dotPos.x / PixelUnit) * PixelUnit;
                    dotPos.y = Mathf.Round(dotPos.y / PixelUnit) * PixelUnit;
                    dotPos.z = 0.0f;

                    Color col = Color.Lerp(_startCol, _endCol, normT);

                    // 5개 도트마다 은은한 십자별(+) 배치, 나머지는 1x1 도트(.)
                    if (0 == (dotIndex % 5))
                    {
                        AddCrossStar(dotPos, normT, col);
                    }
                    else
                    {
                        AddDot(dotPos, normT, col);
                    }

                    currentDist += stepDist;
                    dotIndex++;
                }

                // 끝점 도트 보장 (오프셋 0, 도착 별 중심에 칼같이 안착)
                if (currentDist - stepDist < length)
                {
                    Vector3 finalPos = localEnd;
                    finalPos.x = Mathf.Round(finalPos.x / PixelUnit) * PixelUnit;
                    finalPos.y = Mathf.Round(finalPos.y / PixelUnit) * PixelUnit;
                    finalPos.z = 0.0f;
                    AddCrossStar(finalPos, 1.0f, _endCol);
                }

                proceduralMesh.Clear();
                proceduralMesh.SetVertices(segVertices);
                proceduralMesh.SetUVs(0, segUVs);
                proceduralMesh.SetColors(segColors);
                proceduralMesh.SetTriangles(segTriangles, 0);
                proceduralMesh.RecalculateBounds();

                // 2D 카메라 프러스텀 컬링 오차 방지를 위해 Z축 및 XY 안전 마진 확장
                Bounds expandedBounds = proceduralMesh.bounds;
                expandedBounds.Expand(new Vector3(4.0f, 4.0f, 20.0f));
                proceduralMesh.bounds = expandedBounds;

                UpdateSorting(_layerName, _order);
            }

            public void Setup(
                Transform _ownerTransform,
                Vector3 _start,
                Vector3 _end,
                float _halfWidth,
                Material _material,
                string _layerName,
                int _order,
                float _zigzagAmp,
                float _zigzagStep,
                float _zigzagPhase,
                Color _startCol,
                Color _endCol,
                Color _coreCol,
                Color _headCol,
                Color _tailCol,
                float _boost = 1.0f)
            {
                Vector3 delta = _end - _start;
                Vector3 c0 = _start + delta * (1.0f / 3.0f);
                Vector3 c1 = _end - delta * (1.0f / 3.0f);
                Setup(
                    _ownerTransform,
                    _start,
                    _end,
                    c0,
                    c1,
                    _halfWidth,
                    _material,
                    _layerName,
                    _order,
                    _zigzagAmp,
                    _zigzagStep,
                    _zigzagPhase,
                    _startCol,
                    _endCol,
                    _coreCol,
                    _headCol,
                    _tailCol,
                    _boost);
            }

            public void Setup(
                Transform _ownerTransform,
                Vector3 _start,
                Vector3 _end,
                float _halfWidth,
                Material _material,
                string _layerName,
                int _order,
                float _zigzagAmp = 0.09375f,
                float _zigzagStep = 0.2f,
                float _zigzagPhase = 0.0f)
            {
                Setup(
                    _ownerTransform,
                    _start,
                    _end,
                    _halfWidth,
                    _material,
                    _layerName,
                    _order,
                    _zigzagAmp,
                    _zigzagStep,
                    _zigzagPhase,
                    new Color(0.7f, 1.8f, 2.8f, 1.0f),
                    new Color(0.5f, 1.5f, 2.6f, 1.0f),
                    new Color(2.0f, 2.4f, 2.8f, 1.0f),
                    new Color(2.6f, 3.6f, 4.8f, 1.0f),
                    new Color(0.2f, 0.6f, 1.8f, 1.0f));
            }

            public void SetProgress(float _progress, float _tailLength = 0.4f)
            {
                meshRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(PropProgress, _progress);
                propBlock.SetFloat(PropTailLength, _tailLength);
                meshRenderer.SetPropertyBlock(propBlock);
            }

            public void UpdateSorting(string _layerName, int _order)
            {
                meshRenderer.sortingLayerName = _layerName;
                meshRenderer.sortingOrder = _order;
            }

            public void SetActive(bool _active)
            {
                gameObject.SetActive(_active);
                if (null != meshRenderer)
                {
                    meshRenderer.enabled = _active;
                }
            }

            public void Dispose()
            {
                if (null != proceduralMesh)
                {
                    Destroy(proceduralMesh);
                    proceduralMesh = null;
                }
                if (null != gameObject)
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region Editor Helpers

        public List<Vector3> TestNodes => testNodes;
        public bool TestIsClosedLoop
        {
            get => testIsClosedLoop;
            set => testIsClosedLoop = value;
        }
        public bool IsPlaying => isPlaying;

        #endregion
    }
}
