using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using PresentationLayer.UISystem.UIView.HUD.Equipment;

/// <summary>
/// 4스테이지(MagmaForest) 불씨의 생성·소멸을 맡는다. 주운 뒤 과열 수치를 올리는 것은 InDungeonObjectManager가 한다.
///
/// 불씨가 생기는 경로는 셋이다(수치는 전부 인스펙터에서 조정).
/// 1) 입장 배치: 나무가 다 심어진 직후, 나무 1그루당 평균 initialEmbersPerTree개를 그 나무 옆 빈 칸에 놓는다.
/// 2) 자연 생성: naturalSpawnInterval초마다 캐릭터 주변(거의 한 화면, naturalSpawnHalfExtents)의 빈 칸 하나에 놓는다.
/// 3) 벌목 드랍: 나무가 쓰러질 때와 희귀나무 등급이 바뀔 때마다 dropCountMin~Max개를 튀어나오게 한다("불씨 드랍량 증가" 특성 배율을 곱한다).
///    이렇게 떨어진 불씨만 droppedLifetime초 뒤 사라진다(1, 2는 남아 있다).
/// 과열이 최대여도 2, 3은 계속 생긴다. 과열이 최대일 때 주운 불씨는 과열을 더 올리지 않는다(지진 스택은 오른다).
/// 1~3 모두 차량 휴식 구역 안에는 놓지 않는다(BeginRun이 받는 판정, 휴식 특성으로 구역이 열렸을 때만).
///
/// 용암숲 기믹 안내(Quest UI)가 떠 있는 동안(SetGuideActive)에는:
/// - 바닥에 놓인 모든 불씨 위에 화살표(QuestIndicator)를 띄운다(입장 배치·자연 생성·벌목 드랍 전부).
/// - 안내 중 처음 불씨를 떨어뜨리는 나무는 무조건 guideFirstTreeDropCount개를 떨어뜨리고, 그 불씨는 사라지지 않는다.
///   그 불씨를 모두 주우면 GuideEmbersCollectedEvent로 알린다(안내 클리어). 안내가 끝나면 화살표를 모두 내린다.
/// - 주운 불씨는 캐릭터 대신 화면의 과열 게이지(HUD_HeatGauge 바) 자리로 빨려 들어간다(같은 흡입 공식, 도착하면 획득).
///   게이지가 보이지 않으면 평소처럼 캐릭터로 간다. 안내 중에 빨려가기 시작한 불씨는 안내가 끝나도 게이지까지 간다.
///
/// 이펙트 담당 연결 지점: 이 오브젝트의 VFXComponent에 이펙트를 등록하고 아래 태그를 채우면 된다(비어 있으면 재생하지 않는다).
/// </summary>
public class EmberItemController : MonoBehaviour
{
    public event Action<EmberItem> EmberAcquiredEvent;
    // 용암숲 기믹 안내 중 첫 나무에서 나온 불씨를 모두 주운 순간(안내 클리어 판정용).
    public event Action GuideEmbersCollectedEvent;

    [SerializeField] private EmberItem emberItemPrefab;

    [Header("적용 맵")]
    [SerializeField] private List<MapType> enabledMapTypes = new List<MapType> { MapType.MagmaForest };

    [Header("획득")]
    [Tooltip("불씨 1개를 주울 때 오르는 과열 수치")]
    [SerializeField] private float overheatPerEmber = 1f;
    // 과열이 오를수록 불씨 1개가 올리는 양이 줄어든다(GetGainEfficiency).
    //   과열 ≤ gainFalloffStart: 효율 1
    //   그 위: 효율 = gainMinEfficiency ^ ((과열 - gainFalloffStart) / (gainFloorStart - gainFalloffStart))  (지수 감소)
    //   과열 ≥ gainFloorStart: gainMinEfficiency부터 시작하며, 90 이상은 별도 구간 효율 적용
    // 기본값(20 / 75 / 0.005)이면 50 부근에서 약 0.056.
    [Tooltip("과열 수치가 이 값까지는 불씨 효율 100%")]
    [SerializeField, Min(0f)] private float gainFalloffStart = 20f;
    [Tooltip("과열 수치가 이 값 이상이면 기준 효율 유지(90 이상은 별도 구간 효율, 그 사이는 지수적으로 감소)")]
    [SerializeField, Min(0f)] private float gainFloorStart = 75f;
    [Tooltip("지수 감소 끝의 기준 효율(기본 75~90 미만). 90 이상은 별도 구간 효율을 쓴다")]
    [SerializeField, Range(0.0001f, 1f)] private float gainMinEfficiency = 0.005f;

    [Header("입장 배치")]
    [Tooltip("나무 1그루당 평균 불씨 수. 1보다 작으면 그 확률로 1개, 크면 정수부만큼 + 소수부 확률로 1개 더")]
    [SerializeField, Min(0f)] private float initialEmbersPerTree = 0.25f;

    [Header("자연 생성")]
    [SerializeField, Min(0.1f)] private float naturalSpawnInterval = 5f;
    [Tooltip("캐릭터를 중심으로 자연 생성 칸을 고르는 범위(월드 단위 반폭·반높이). 기준 화면은 10 x 5.6")]
    [SerializeField] private Vector2 naturalSpawnHalfExtents = new Vector2(9f, 5f);
    [Tooltip("빈 칸을 찾을 때 시도하는 횟수. 다 실패하면 이번 주기는 건너뛴다")]
    [SerializeField, Min(1)] private int naturalSpawnAttempts = 20;

    [Header("벌목 드랍")]
    [SerializeField, Min(0)] private int dropCountMin = 2;
    [SerializeField, Min(0)] private int dropCountMax = 3;
    [Tooltip("벌목으로 떨어진 불씨가 사라지기까지의 시간(초)")]
    [SerializeField] private float droppedLifetime = 20f;

    [Header("이펙트 연결 (이펙트 담당)")]
    [Tooltip("바닥에 놓여 있는 동안 붙는 루프 이펙트")]
    [SerializeField] private string groundLoopVfxTag = "";
    [Tooltip("주운 순간 캐릭터 자리에서 한 번")]
    [SerializeField] private string acquireVfxTag = "";
    [Tooltip("수명이 다해 사라지는 순간 그 자리에서 한 번")]
    [SerializeField] private string expireVfxTag = "";

    [Header("기믹 안내 (용암숲 Quest UI)")]
    [Tooltip("안내 중 바닥의 불씨 위에 띄우는 화살표 프리팹(튜토리얼 퀘스트 화살표와 같은 것)")]
    [SerializeField] private QuestIndicator guideIndicatorPrefab;
    [Tooltip("불씨 원점 기준 화살표 위치(월드 단위)")]
    [SerializeField] private Vector3 guideIndicatorOffset = new Vector3(0f, 0.75f, 0f);
    [Tooltip("안내 중 처음 불씨를 떨어뜨리는 나무가 무조건 떨어뜨리는 불씨 수(특성 배율 무시, 사라지지 않음)")]
    [SerializeField, Min(1)] private int guideFirstTreeDropCount = 2;

    private IObjectPool<EmberItem> emberPool;
    // 꺼내 쓰는 중인 불씨 전부(ClearAll용)와, 그중 매 프레임 갱신이 필요한 것(움직이는 중·착지 연출 중·수명이 도는 중).
    // 바닥에 가만히 놓인 수명 없는 불씨(입장 배치·자연 생성)는 흡입이 걸릴 때까지 갱신 목록에서 빠진다.
    private readonly List<EmberItem> activeEmbers = new List<EmberItem>(128);
    private readonly List<EmberItem> updatingEmbers = new List<EmberItem>(64);
    private readonly List<EmberItem> cleanupList = new List<EmberItem>(128);
    // 칸마다 바닥에 놓인 불씨 수. 입장 배치·자연 생성이 불씨가 있는 칸에 겹쳐 놓지 않게 한다.
    // 벌목 드랍은 칸을 가리지 않고 흩뿌려 한 칸에 여럿이 놓일 수 있으므로 있고 없음이 아니라 개수를 센다.
    private readonly Dictionary<Vector3Int, int> groundEmberCounts = new Dictionary<Vector3Int, int>();

    private ICharacter character;
    private IEnvironmentProvider environmentProvider;
    private VFXComponent vfxComponent;

    // 이번 원정에서 불씨가 나오는지(맵 + "산불 조심" 특성 + 로그라이크 기믹 끔 여부). BeginRun에서 정한다.
    private bool bEnabledThisRun;
    // 이번 원정의 벌목 드랍 개수 배율("불씨 드랍량 증가" 특성). BeginRun에서 정한다.
    private float dropMultiplierThisRun = 1f;
    // 자연 생성·드랍을 지금 해도 되는지(원정 종료 중·사망·탑승 중이면 false). InDungeonObjectManager가 넘겨준다.
    private Func<bool> canSpawnMore;
    // 불씨를 놓으면 안 되는 자리(차량 휴식 구역 안)인지. InDungeonObjectManager가 넘겨준다.
    private Func<Vector3, bool> isBlockedPosition;
    private float naturalSpawnTimer;

    // 기믹 안내 상태. bGuideActive 동안 바닥 불씨에 화살표를 띄우고, 첫 벌목 드랍을 추적한다.
    private bool bGuideActive;
    private bool bGuideFirstDropDone;
    private int guideEmbersRemaining;
    // 쓰고 남은 화살표(퇴장 연출 중이어도 다시 Show하면 그대로 재사용된다).
    private readonly Stack<QuestIndicator> freeGuideIndicators = new Stack<QuestIndicator>(16);

    // 기믹 안내 중 주운 불씨가 빨려 들어갈 과열 게이지 자리. 화면 위치를 매 프레임 월드 위치로 바꿔 이 Transform에 둔다.
    private Transform gaugeAbsorbTarget;
    private HUD_HeatGauge heatGauge;
    private RectTransform heatGaugeBar;
    private Canvas heatGaugeCanvas;
    // 게이지로 날아가는 불씨가 있을 수 있는 동안 true(안내가 끝난 뒤에도 남은 비행이 끝날 때까지 자리를 갱신한다).
    private bool bGaugeAbsorbInFlight;

    private static readonly Vector3Int[] NeighborOffsets =
    {
        new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
    };
    private readonly List<Vector3Int> neighborBuffer = new List<Vector3Int>(8);

    // 나무가 한 프레임에 여러 그루 쓰러지면 불씨를 풀에서 꺼내 튀기는 일도 그 프레임에 몰린다. 원목(LogItemController.SpawnLogItem)과
    // 같은 방식으로, 한 프레임에 MaxDropEmbersPerFrame개까지만 그 자리에서 튀기고 넘는 분량은 튀길 값을 적어 줄을 세웠다가 다음
    // 프레임들에 순서대로 튀긴다. 개수·방향·착지 칸·높이는 쓰러지는 순간에 예전과 같은 순서로 정하므로 난수 흐름은 그대로이고,
    // 한도 안인 프레임은 예전과 같다. 기믹 안내의 첫 드랍은 항상 그 자리에서 튀긴다.
    private struct PendingEmberDrop
    {
        public Vector3 treePosition;
        public Vector3 endPos;
        public float height;
        public float lifetime;
    }

    private const int MaxDropEmbersPerFrame = 12;
    private readonly Queue<PendingEmberDrop> pendingEmberDrops = new Queue<PendingEmberDrop>(64);
    private int emberDropFrame = -1;
    private int emberDropsThisFrame = 0;

    public bool bEnabled => bEnabledThisRun;

    public void Initialize(ICharacter _character, IEnvironmentProvider _environmentProvider)
    {
        character = _character;
        environmentProvider = _environmentProvider;

        vfxComponent = GetComponent<VFXComponent>();
        if (vfxComponent != null) vfxComponent.Initialize();

        emberPool = new ObjectPool<EmberItem>(
            createFunc: TakeOrCreateEmberItem,
            actionOnGet: OnGetEmberItem,
            actionOnRelease: OnReleaseEmberItem,
            actionOnDestroy: OnDestroyEmberItem,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: 32,
            maxSize: 512
        );
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        for (int i = 0; i < activeEmbers.Count; i++)
        {
            activeEmbers[i].SetCharacter(_character);
        }
    }

    /// <summary>
    /// 새 원정이 시작될 때(나무 초기 스폰 직전) 부른다. 이전 원정의 불씨를 치우고 이번 맵에서 불씨가 나오는지 정한다.
    /// _bEverywhere("산불 조심" 특성)면 적용 맵이 아니어도 불씨가 나온다. _dropMultiplier는 벌목 드랍 개수 배율이다.
    /// _isBlockedPosition이 true를 돌려주는 자리(차량 휴식 구역 안)에는 어떤 경로로도 불씨를 놓지 않는다.
    /// </summary>
    public void BeginRun(MapType _mapType, bool _bStageGimmicksDisabled, Func<bool> _canSpawnMore, bool _bEverywhere, float _dropMultiplier,
        Func<Vector3, bool> _isBlockedPosition)
    {
        ClearAll();

        bEnabledThisRun = emberItemPrefab != null && false == _bStageGimmicksDisabled && (_bEverywhere || enabledMapTypes.Contains(_mapType));
        // 이번 원정에 불씨가 나오면 로딩 중인 지금 여유분을 만들어 둔다(전투 중 대량 벌목 프레임의 생성을 줄인다).
        if (true == bEnabledThisRun) PrewarmReserve();
        dropMultiplierThisRun = Mathf.Max(0f, _dropMultiplier);
        canSpawnMore = _canSpawnMore;
        isBlockedPosition = _isBlockedPosition;
        naturalSpawnTimer = 0f;

        // 기믹 안내는 캐릭터가 조작 가능해질 때(이 뒤) 다시 켜진다.
        SetGuideActive(false);
    }

    /// <summary>나무 초기 스폰이 끝난 직후 부른다. 나무 옆 빈 칸에 불씨를 드문드문 놓는다.</summary>
    public void SpawnInitialEmbers(IReadOnlyList<TreeObj> _trees)
    {
        if (!bEnabledThisRun || _trees == null || initialEmbersPerTree <= 0f) return;

        for (int i = 0; i < _trees.Count; i++)
        {
            TreeObj tree = _trees[i];
            if (tree == null || tree.bIsSapling) continue;

            int count = RollCount(initialEmbersPerTree);
            for (int c = 0; c < count; c++)
            {
                if (!TryPickNeighborCell(tree.CellPos, false, out Vector3Int cell)) break;
                PlaceEmber(cell, 0f);
            }
        }
    }

    /// <summary>나무가 쓰러지거나 희귀나무 등급이 바뀐 자리에서 불씨를 튀어나오게 한다.</summary>
    public void SpawnDropEmbers(Vector3 _treePosition)
    {
        if (!CanSpawnNow()) return;

        // 기믹 안내 중 첫 드랍: 개수를 고정하고(특성 배율 무시) 사라지지 않게 해서, 화살표를 보고 주울 때까지 남겨 둔다.
        bool bGuideDrop = bGuideActive && false == bGuideFirstDropDone;

        int count;
        if (bGuideDrop)
        {
            count = guideFirstTreeDropCount;
        }
        else
        {
            count = UnityEngine.Random.Range(dropCountMin, Mathf.Max(dropCountMin, dropCountMax) + 1);
            // 특성 배율의 소수 부분은 그 확률로 1개 더(배율이 1이면 굴린 개수 그대로).
            if (dropMultiplierThisRun != 1f) count = RollCount(count * dropMultiplierThisRun);
        }
        ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
        Vector3Int treeCell = tilemap != null ? tilemap.WorldToCell(_treePosition) : default;

        for (int i = 0; i < count; i++)
        {
            // 원목과 같은 흩뿌림(LogItemController.SpawnLogItem과 같은 거리·높이). 방향·착지 칸·높이는 불씨를 지금 튀기든
            // 줄을 세우든 여기서 예전과 같은 순서로 정한다(풀에서 꺼내기·초기화는 난수를 쓰지 않는다).
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(1.25f, 1.75f);
            Vector3 endPos = _treePosition + new Vector3(randomDir.x, randomDir.y * 0.5f, 0f) * randomDist;

            // 떨어질 칸이 물·용암·나무처럼 서 있을 수 없는 칸이면 나무 바로 옆의 설 수 있는 칸으로 바꾼다.
            // 나무 자리로 당기면 등급만 바뀌고 살아 있는 희귀나무는 밑동에 가려지고 걸어서 닿을 수 없다.
            // 옆 칸이 전부 막혔을 때만 나무 자리에 둔다(쓰러진 나무는 그 칸이 비어 있다).
            // 흩뿌린 자리가 휴식 구역 안이어도 같은 방법으로 구역 밖 옆 칸에 내린다. 나무 자리까지 구역 안이면 이 불씨는 내지 않는다.
            if (tilemap != null && (!IsCellLandable(tilemap.WorldToCell(endPos)) || IsBlockedPosition(endPos)))
            {
                endPos = TryPickNeighborCell(treeCell, true, out Vector3Int landCell) ? tilemap.CellToWorld(landCell) : _treePosition;

                // 예전에는 꺼내 둔 불씨를 그대로 풀에 되돌렸다. 이제는 꺼내기 전이라 건너뛰기만 한다.
                if (IsBlockedPosition(endPos)) continue;
            }

            float launchHeight = UnityEngine.Random.Range(0.75f, 1.25f);

            if (false == bGuideDrop && false == TryTakeEmberDropSlot())
            {
                pendingEmberDrops.Enqueue(new PendingEmberDrop
                {
                    treePosition = _treePosition,
                    endPos = endPos,
                    height = launchHeight,
                    lifetime = droppedLifetime,
                });
                continue;
            }

            if (bGuideDrop) CountEmberDrop();

            LaunchDropEmber(_treePosition, endPos, launchHeight, bGuideDrop ? 0f : droppedLifetime, bGuideDrop);
        }

        // 하나라도 실제로 떨어졌을 때만 첫 나무로 친다(전부 휴식 구역에 막히면 다음 나무가 다시 첫 나무가 된다).
        if (bGuideDrop && 0 < guideEmbersRemaining) bGuideFirstDropDone = true;
    }

    // 불씨 하나를 풀에서 꺼내 튀긴다(예전 SpawnDropEmbers 루프 안에서 하던 일 그대로).
    private void LaunchDropEmber(Vector3 _treePosition, Vector3 _endPos, float _height, float _lifetime, bool _bGuideDrop)
    {
        EmberItem ember = emberPool.Get();
        if (ember == null) return;

        ember.Initialize(character, _lifetime);

        if (_bGuideDrop)
        {
            ember.bGuideTracked = true;
            guideEmbersRemaining++;
        }

        ember.Launch(_treePosition, _endPos, _height);
    }

    private void SyncEmberDropFrame()
    {
        int frame = Time.frameCount;
        if (frame != emberDropFrame)
        {
            emberDropFrame = frame;
            emberDropsThisFrame = 0;
        }
    }

    private void CountEmberDrop()
    {
        SyncEmberDropFrame();
        ++emberDropsThisFrame;
    }

    // 이번 프레임 한도 안이고 앞에 줄 선 불씨가 없으면 자리를 하나 쓰고 true(그 자리에서 튀긴다). 아니면 false(줄을 세운다 - 순서 유지).
    private bool TryTakeEmberDropSlot()
    {
        SyncEmberDropFrame();

        if (0 < pendingEmberDrops.Count || MaxDropEmbersPerFrame <= emberDropsThisFrame) return false;

        ++emberDropsThisFrame;
        return true;
    }

    private void FlushPendingEmberDrops()
    {
        SyncEmberDropFrame();

        // 밀린 양이 많으면 한도를 그 절반까지 늘려 두세 프레임 안에 따라잡는다.
        int limit = Mathf.Max(MaxDropEmbersPerFrame, (pendingEmberDrops.Count + 1) / 2);

        while (0 < pendingEmberDrops.Count && emberDropsThisFrame < limit)
        {
            PendingEmberDrop pending = pendingEmberDrops.Dequeue();
            ++emberDropsThisFrame;
            LaunchDropEmber(pending.treePosition, pending.endPos, pending.height, pending.lifetime, false);
        }
    }

    /// <summary>
    /// 용암숲 기믹 안내(Quest UI)가 시작되거나(true) 끝날 때(false, 클리어·원정 종료) 부른다.
    /// 시작하면 바닥에 놓인 모든 불씨 위에 화살표를 띄우고 첫 벌목 드랍 추적을 새로 시작한다. 끝나면 화살표를 모두 내린다.
    /// </summary>
    public void SetGuideActive(bool _bActive)
    {
        bGuideActive = _bActive;
        bGuideFirstDropDone = false;
        guideEmbersRemaining = 0;

        for (int i = 0; i < activeEmbers.Count; i++)
        {
            EmberItem ember = activeEmbers[i];
            if (_bActive)
            {
                if (ember.bGrounded) ShowGuideIndicator(ember);
            }
            else
            {
                ember.bGuideTracked = false;
                HideGuideIndicator(ember);
            }
        }
    }

    /// <summary>
    /// 기믹 안내 중 화살표가 떠 있는(바닥에 놓인) 불씨를 _results에 담는다. 안내 중이 아니면 비운 채로 둔다.
    /// EnvironmentInteractionManager가 이 불씨를 가리는 나무를 반투명하게 할 때 쓴다.
    /// </summary>
    public void CollectGuideEmbers(List<EmberItem> _results)
    {
        _results.Clear();
        if (false == bGuideActive) return;

        for (int i = 0; i < activeEmbers.Count; i++)
        {
            EmberItem ember = activeEmbers[i];
            if (ember != null && ember.GuideIndicator != null) _results.Add(ember);
        }
    }

    private void ShowGuideIndicator(EmberItem _ember)
    {
        if (_ember.GuideIndicator != null || guideIndicatorPrefab == null) return;

        QuestIndicator indicator;
        if (freeGuideIndicators.Count > 0)
        {
            indicator = freeGuideIndicators.Pop();
        }
        else
        {
            indicator = Instantiate(guideIndicatorPrefab, transform);
            indicator.HideImmediately();
        }

        _ember.GuideIndicator = indicator;
        indicator.Show(_ember.transform, guideIndicatorOffset);
    }

    private void HideGuideIndicator(EmberItem _ember)
    {
        QuestIndicator indicator = _ember.GuideIndicator;
        if (indicator == null) return;

        _ember.GuideIndicator = null;
        indicator.Hide();
        freeGuideIndicators.Push(indicator);
    }

    // 과열 게이지가 화면에 보이면 빨려 들어갈 자리(gaugeAbsorbTarget)를 지금 위치로 맞추고 true. 게이지가 없거나 숨겨져 있으면 false.
    // _z는 불씨가 움직이는 평면의 z(흡입은 3차원 거리로 움직이므로 목표도 같은 평면에 둔다).
    private bool TryPrepareGaugeAbsorbTarget(float _z)
    {
        if (heatGauge == null)
        {
            heatGauge = FindAnyObjectByType<HUD_HeatGauge>();
            if (heatGauge == null) return false;

            // 게이지의 세로 바(Visual/Bar)를 목표로 한다. 계층이 바뀌어 못 찾으면 게이지 전체 영역 가운데로 간다.
            heatGaugeBar = heatGauge.transform.Find("Visual/Bar") as RectTransform;
            if (heatGaugeBar == null) heatGaugeBar = heatGauge.transform as RectTransform;
            heatGaugeCanvas = heatGauge.GetComponentInParent<Canvas>();
            if (heatGaugeCanvas != null) heatGaugeCanvas = heatGaugeCanvas.rootCanvas;
        }

        if (heatGaugeBar == null || false == heatGauge.isActiveAndEnabled || false == heatGauge.IsVisible) return false;

        if (gaugeAbsorbTarget == null)
        {
            gaugeAbsorbTarget = new GameObject("EmberGaugeAbsorbTarget").transform;
            gaugeAbsorbTarget.SetParent(transform, false);
        }

        Vector3 position = gaugeAbsorbTarget.position;
        position.z = _z;
        gaugeAbsorbTarget.position = position;

        return TrySyncGaugeAbsorbTarget();
    }

    // 게이지 바 가운데의 화면 위치를 게임 카메라의 월드 위치로 바꿔 목표에 둔다(z는 그대로). 카메라가 없으면 false.
    private bool TrySyncGaugeAbsorbTarget()
    {
        Camera worldCamera = CameraFinder.Instance != null ? CameraFinder.Instance.PPMainCamera : null;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null || heatGaugeBar == null || gaugeAbsorbTarget == null) return false;

        Camera uiCamera = null;
        if (heatGaugeCanvas != null && RenderMode.ScreenSpaceOverlay != heatGaugeCanvas.renderMode)
        {
            uiCamera = heatGaugeCanvas.worldCamera != null ? heatGaugeCanvas.worldCamera : Camera.main;
        }

        Vector3 barCenter = heatGaugeBar.TransformPoint(heatGaugeBar.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, barCenter);
        Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));

        world.z = gaugeAbsorbTarget.position.z;
        gaugeAbsorbTarget.position = world;
        return true;
    }

    // 게이지로 날아가는 불씨가 있는 동안 매 프레임 목표 자리를 갱신한다(카메라가 움직여도 화면의 게이지를 따라간다).
    private void UpdateGaugeAbsorbTarget()
    {
        if (false == bGaugeAbsorbInFlight) return;

        bool bAnyInFlight = false;
        for (int i = 0; i < updatingEmbers.Count; i++)
        {
            EmberItem ember = updatingEmbers[i];
            if (ember.MoveState == ItemMoveState.Sucking && ember.SuckTarget == gaugeAbsorbTarget)
            {
                bAnyInFlight = true;
                break;
            }
        }

        if (false == bAnyInFlight)
        {
            bGaugeAbsorbInFlight = false;
            return;
        }

        TrySyncGaugeAbsorbTarget();
    }

    /// <summary>원정이 끝날 때(마을 도착·재도전 정리) 부른다. 불씨를 치우고 다음 BeginRun까지 아무것도 만들지 않는다.</summary>
    public void EndRun()
    {
        bEnabledThisRun = false;
        canSpawnMore = null;
        isBlockedPosition = null;
        SetGuideActive(false);
        ClearAll();
    }

    /// <summary>
    /// 귀환이 확정된 순간(InDungeonObjectManager.HandleGameEnd) 부른다. 캐릭터에게 날아가던 불씨를 거둬 탑승 연출 중에
    /// 도착해 주워지지 않게 한다(원목·원석과 같은 처리). 새로 빨려가는 것은 캐릭터 쪽 감지가 꺼져서 막힌다.
    /// </summary>
    public void CancelActiveSucking()
    {
        for (int i = updatingEmbers.Count - 1; i >= 0; i--)
        {
            if (i >= updatingEmbers.Count) continue;

            EmberItem ember = updatingEmbers[i];
            if (ember.MoveState == ItemMoveState.Sucking) TryReleaseEmber(ember);
        }
    }

    public void ClearAll()
    {
        // 아직 튀기지 않은 불씨는 바닥에 떨어진 채 치워지는 불씨와 마찬가지로 버린다.
        pendingEmberDrops.Clear();

        cleanupList.Clear();
        cleanupList.AddRange(activeEmbers);
        for (int i = 0; i < cleanupList.Count; i++)
        {
            TryReleaseEmber(cleanupList[i]);
        }
        cleanupList.Clear();

        // 회수하면서 칸 개수가 모두 0으로 돌아왔어야 한다. 다음 원정의 빈 칸 판정에 남지 않도록 비운다.
        groundEmberCounts.Clear();
    }

    private void Update()
    {
        // 게이지로 날아가는 불씨가 이번 프레임 목표를 읽기 전에 게이지 자리를 갱신한다
        UpdateGaugeAbsorbTarget();

        // 앞선 프레임에서 한도를 넘어 줄을 세워 둔 불씨를 튀긴다(아래 갱신 루프가 이번 프레임부터 움직인다).
        if (0 < pendingEmberDrops.Count) FlushPendingEmberDrops();

        if (updatingEmbers.Count > 0)
        {
            float deltaTime = Time.deltaTime;

            // ManualUpdate 중 획득·소멸·정지로 목록에서 빠질 수 있다(마지막 원소와 자리를 바꿔 뺀다).
            // 역순으로 돌면 그 자리에 들어오는 원소는 이미 이번 프레임에 갱신한 것이라 건너뛰거나 두 번 돌지 않는다.
            for (int i = updatingEmbers.Count - 1; i >= 0; i--)
            {
                if (i >= updatingEmbers.Count) continue;
                updatingEmbers[i].ManualUpdate(deltaTime);
            }
        }

        UpdateNaturalSpawn();
    }

    private void UpdateNaturalSpawn()
    {
        if (!CanSpawnNow() || character == null) return;

        naturalSpawnTimer += Time.deltaTime;
        if (naturalSpawnTimer < naturalSpawnInterval) return;
        naturalSpawnTimer = 0f;

        Transform characterTransform = character.GetTransform();
        if (characterTransform == null) return;

        if (TryPickNaturalCell(characterTransform.position, out Vector3Int cell))
        {
            PlaceEmber(cell, 0f);
        }
    }

    private bool CanSpawnNow()
    {
        return bEnabledThisRun && (canSpawnMore == null || canSpawnMore());
    }

    private static int RollCount(float _average)
    {
        int count = Mathf.FloorToInt(_average);
        if (UnityEngine.Random.value < _average - count) count++;
        return count;
    }

    private void PlaceEmber(Vector3Int _cell, float _lifetime)
    {
        ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
        if (tilemap == null) return;

        EmberItem ember = emberPool.Get();
        if (ember == null) return;

        ember.Initialize(character, _lifetime);
        ember.Place(tilemap.CellToWorld(_cell));
    }

    // _bAllowEmberStack: 불씨가 이미 놓인 칸도 후보로 본다(벌목 드랍의 착지 보정 - 흩뿌림은 원래 칸을 가리지 않는다).
    private bool TryPickNeighborCell(Vector3Int _center, bool _bAllowEmberStack, out Vector3Int _cell)
    {
        neighborBuffer.Clear();
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector3Int candidate = _center + NeighborOffsets[i];
            if (_bAllowEmberStack ? IsCellLandable(candidate) : IsCellFree(candidate)) neighborBuffer.Add(candidate);
        }

        if (neighborBuffer.Count == 0)
        {
            _cell = default;
            return false;
        }

        _cell = neighborBuffer[UnityEngine.Random.Range(0, neighborBuffer.Count)];
        return true;
    }

    private bool TryPickNaturalCell(Vector3 _center, out Vector3Int _cell)
    {
        ITilemapDataProvider tilemap = environmentProvider.tilemapDataProvider;

        for (int i = 0; i < naturalSpawnAttempts; i++)
        {
            Vector3 point = _center + new Vector3(
                UnityEngine.Random.Range(-naturalSpawnHalfExtents.x, naturalSpawnHalfExtents.x),
                UnityEngine.Random.Range(-naturalSpawnHalfExtents.y, naturalSpawnHalfExtents.y),
                0f);

            Vector3Int candidate = tilemap.WorldToCell(point);
            if (IsCellFree(candidate))
            {
                _cell = candidate;
                return true;
            }
        }

        _cell = default;
        return false;
    }

    // 불씨가 놓일 수 있는 칸: 걸을 수 있고(나무·장애물 없음) 물·바위 장식·위험 지형이 아니며 휴식 구역 밖인 칸.
    // 휴식 구역 판정은 불씨가 실제로 놓이는 자리(PlaceEmber와 같은 CellToWorld)로 한다.
    private bool IsCellLandable(Vector3Int _cell)
    {
        ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
        if (tilemap == null) return false;

        if (!tilemap.IsWalkable(_cell)) return false;
        if (tilemap.IsWaterTile(_cell) || tilemap.HasRockDeco(_cell)) return false;
        if (tilemap.GetHazardStaminaDrainPerSecond(_cell) > 0f) return false;
        if (IsBlockedPosition(tilemap.CellToWorld(_cell))) return false;

        return true;
    }

    private bool IsBlockedPosition(Vector3 _worldPosition)
    {
        return isBlockedPosition != null && isBlockedPosition(_worldPosition);
    }

    // 비어 있는 칸: 놓일 수 있는 칸 중 유닛이 서 있지 않고 불씨가 이미 놓이지 않은 칸(입장 배치·자연 생성용).
    private bool IsCellFree(Vector3Int _cell)
    {
        if (!IsCellLandable(_cell)) return false;
        if (environmentProvider.pathfindGridProvider != null && environmentProvider.pathfindGridProvider.IsOccupied(_cell)) return false;
        if (groundEmberCounts.ContainsKey(_cell)) return false;

        return true;
    }

    // ── 획득 / 소멸 ──

    private void OnEmberAcquired(EmberItem _ember)
    {
        bool bGuideTracked = _ember.bGuideTracked;

        PlayOneShotVfx(acquireVfxTag, _ember.transform.position);
        EmberAcquiredEvent?.Invoke(_ember);
        TryReleaseEmber(_ember);

        // 안내 중 첫 나무에서 나온 불씨를 모두 주웠다. 회수까지 끝낸 뒤 알린다(받는 쪽이 곧바로 SetGuideActive(false)로 화살표를 내린다).
        if (bGuideTracked && bGuideActive)
        {
            guideEmbersRemaining--;
            if (guideEmbersRemaining <= 0 && bGuideFirstDropDone)
            {
                GuideEmbersCollectedEvent?.Invoke();
            }
        }
    }

    private void OnEmberExpired(EmberItem _ember)
    {
        PlayOneShotVfx(expireVfxTag, _ember.transform.position);
        TryReleaseEmber(_ember);
    }

    public float OverheatPerEmber => overheatPerEmber;

    /// <summary>
    /// 지금 과열 수치(내부 소수 값)에서 불씨 1개의 효율. 불씨를 줍기 직전 값으로 계산한다.
    /// 기본값: 0~20 효율 1, 20~75 지수 감소, 75~90 미만 0.005. 90 이상은 구간별 고정 효율.
    /// </summary>
    public float GetGainEfficiency(float _overheatValue)
    {
        if (_overheatValue >= 140f) return 0.00025f;
        if (_overheatValue >= 130f) return 0.0005f;
        if (_overheatValue >= 120f) return 0.001f;
        if (_overheatValue >= 110f) return 0.0015f;
        if (_overheatValue >= 100f) return 0.002f;
        if (_overheatValue >= 90f) return 0.0025f;
        if (_overheatValue <= gainFalloffStart) return 1f;

        float minEfficiency = Mathf.Clamp(gainMinEfficiency, 0.0001f, 1f);
        float range = gainFloorStart - gainFalloffStart;
        if (range <= 0f || _overheatValue >= gainFloorStart) return minEfficiency;

        float t = (_overheatValue - gainFalloffStart) / range;
        return Mathf.Max(minEfficiency, Mathf.Pow(minEfficiency, t));
    }

    private void OnEmberGroundedChanged(EmberItem _ember, bool _bGrounded)
    {
        if (_bGrounded)
        {
            ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
            if (tilemap != null && false == _ember.bHasGroundCell)
            {
                Vector3Int cell = tilemap.WorldToCell(_ember.transform.position);
                _ember.GroundCell = cell;
                _ember.bHasGroundCell = true;
                groundEmberCounts.TryGetValue(cell, out int count);
                groundEmberCounts[cell] = count + 1;
            }

            if (vfxComponent != null && !string.IsNullOrEmpty(groundLoopVfxTag) && _ember.GroundVfx == null)
            {
                // 불씨 자식으로 붙이지 않는다. 불씨는 회수되면 바로 꺼지므로, 자식이면 꺼지는 연출이 멈춘 채 다음 재사용 때 다시 보인다.
                _ember.GroundVfx = vfxComponent.Play(groundLoopVfxTag, _ember.transform.position, Quaternion.identity);
            }

            // 기믹 안내 중이면 바닥에 놓인 불씨마다 화살표를 띄운다(흡입이 취소돼 바닥으로 돌아온 경우도 다시 띄운다).
            if (bGuideActive) ShowGuideIndicator(_ember);
        }
        else
        {
            // 바닥을 떠나면(흡입 시작·회수) 화살표를 내린다.
            HideGuideIndicator(_ember);

            // 기믹 안내 중 흡입이 시작됐으면 캐릭터 대신 과열 게이지 자리로 빨려 들어가게 한다.
            if (bGuideActive && _ember.MoveState == ItemMoveState.Sucking && TryPrepareGaugeAbsorbTarget(_ember.transform.position.z))
            {
                _ember.RedirectSuck(gaugeAbsorbTarget);
                bGaugeAbsorbInFlight = true;
            }

            if (_ember.bHasGroundCell)
            {
                Vector3Int cell = _ember.GroundCell;
                _ember.bHasGroundCell = false;
                if (groundEmberCounts.TryGetValue(cell, out int count))
                {
                    if (count <= 1) groundEmberCounts.Remove(cell);
                    else groundEmberCounts[cell] = count - 1;
                }
            }

            if (vfxComponent != null && _ember.GroundVfx != null)
            {
                vfxComponent.Stop(_ember.GroundVfx, false);
            }
            _ember.GroundVfx = null;
        }
    }

    private void OnEmberUpdateNeededChanged(EmberItem _ember, bool _bNeedsUpdate)
    {
        if (_bNeedsUpdate)
        {
            if (_ember.UpdateIndex != -1) return;
            _ember.UpdateIndex = updatingEmbers.Count;
            updatingEmbers.Add(_ember);
        }
        else
        {
            RemoveAtSwapBack(updatingEmbers, _ember, _ember.UpdateIndex, (_e, _i) => _e.UpdateIndex = _i);
            _ember.UpdateIndex = -1;
        }
    }

    // 마지막 원소와 자리를 바꿔 O(1)로 뺀다. 자리를 옮긴 원소의 인덱스는 _setIndex로 고친다.
    private static void RemoveAtSwapBack(List<EmberItem> _list, EmberItem _item, int _index, Action<EmberItem, int> _setIndex)
    {
        int lastIdx = _list.Count - 1;
        if (_index < 0 || _index > lastIdx || _list[_index] != _item) return;

        if (_index != lastIdx)
        {
            EmberItem last = _list[lastIdx];
            _list[_index] = last;
            _setIndex(last, _index);
        }
        _list.RemoveAt(lastIdx);
    }

    private void PlayOneShotVfx(string _tag, Vector3 _position)
    {
        if (vfxComponent == null || string.IsNullOrEmpty(_tag)) return;
        vfxComponent.Play(_tag, _position, Quaternion.identity);
    }

    // ── 풀 ──

    // 미리 만들어 둘 불씨 수와 획득·소멸 이펙트 수. 실측(모든 특성·전리품, 3단계 숲, 2026-11-24~25)으로 원정 시작 뒤 몇 초 안에
    // 불씨 190~600개, 획득 이펙트 190~420개, 소멸 이펙트 5~32개가 전투 중에 새로 만들어졌다(불씨 개당 약 0.12ms, 이펙트 개당 약 0.2ms.
    // 초기 배치 한 프레임에 135~225개, 그 뒤 프레임당 수십 개).
    private const int PrewarmEmberCount = 640;
    private const int PrewarmAcquireVfxCount = 448;
    private const int PrewarmExpireVfxCount = 32;

    // 미리 만들어 둔 불씨(아직 한 번도 쓰지 않은 것). 풀이 비어 새 불씨가 필요해지면 여기서 먼저 만든 것부터 꺼낸다(TakeOrCreateEmberItem).
    // 예전에는 그 순간에 새로 만들었으므로, n번째로 만든 불씨가 n번째로 처음 쓰이는 것은 그대로이고 만드는 시점만 로딩으로 옮겨진다.
    private readonly Queue<EmberItem> reserveEmbers = new Queue<EmberItem>(PrewarmEmberCount);

    /// <summary>
    /// 아직 쓰지 않은 불씨가 PrewarmEmberCount개가 되도록 미리 만들고, 획득·소멸 이펙트 풀도 미리 늘려 둔다.
    /// 원정 시작(BeginRun, 로딩 중)에 불씨가 나오는 원정에서만 부른다. 만드는 것은 전투 중에 새로 만들어졌을 것과 같다
    /// (같은 CreateEmberItem - 꺼진 채로 만들어진다 / 이펙트 풀의 같은 생성 함수). 앞선 원정에서 남은 여유분이 있으면 모자란 만큼만 만든다.
    /// </summary>
    private void PrewarmReserve()
    {
        if (emberPool == null || emberItemPrefab == null) return;

        // 풀에 이미 쉬고 있는 불씨(앞선 원정에서 쓰고 돌려받은 것)도 다음 요청에 쓰이므로 그만큼은 덜 만든다.
        int need = PrewarmEmberCount - emberPool.CountInactive - reserveEmbers.Count;
        for (int i = 0; i < need; i++)
        {
            EmberItem item = CreateEmberItem();
            if (item == null) break;
            reserveEmbers.Enqueue(item);
        }

        if (vfxComponent != null)
        {
            vfxComponent.EnsurePoolSize(acquireVfxTag, PrewarmAcquireVfxCount);
            vfxComponent.EnsurePoolSize(expireVfxTag, PrewarmExpireVfxCount);
        }
    }

    // 풀이 비었을 때 불리는 생성 함수. 미리 만들어 둔 것이 있으면 먼저 만든 것부터 내주고, 없으면 예전처럼 그 자리에서 만든다.
    private EmberItem TakeOrCreateEmberItem()
    {
        while (0 < reserveEmbers.Count)
        {
            EmberItem item = reserveEmbers.Dequeue();
            if (item != null) return item;
        }

        return CreateEmberItem();
    }

    private EmberItem CreateEmberItem()
    {
        if (emberItemPrefab == null) return null;

        EmberItem newItem = Instantiate(emberItemPrefab, transform);
        newItem.EmberAcquiredEvent += OnEmberAcquired;
        newItem.EmberExpiredEvent += OnEmberExpired;
        newItem.EmberGroundedChangedEvent += OnEmberGroundedChanged;
        newItem.EmberUpdateNeededChangedEvent += OnEmberUpdateNeededChanged;
        newItem.gameObject.SetActive(false);
        return newItem;
    }

    private bool TryReleaseEmber(EmberItem _ember)
    {
        if (_ember == null || _ember.IsPooled) return false;

        emberPool.Release(_ember);
        return true;
    }

    private void OnGetEmberItem(EmberItem _ember)
    {
        if (_ember == null) return;

        _ember.IsPooled = false;
        _ember.ResetItem();
        _ember.PoolIndex = activeEmbers.Count;
        activeEmbers.Add(_ember);
        _ember.gameObject.SetActive(true);
    }

    private void OnReleaseEmberItem(EmberItem _ember)
    {
        if (_ember == null) return;

        // ResetItem이 바닥 상태와 갱신 필요 여부를 끄면서 칸 개수·바닥 루프 이펙트·갱신 목록을 정리한다
        // (OnEmberGroundedChanged, OnEmberUpdateNeededChanged).
        _ember.ResetItem();

        RemoveAtSwapBack(activeEmbers, _ember, _ember.PoolIndex, (_e, _i) => _e.PoolIndex = _i);
        _ember.PoolIndex = -1;

        _ember.IsPooled = true;
        _ember.gameObject.SetActive(false);
    }

    private void OnDestroyEmberItem(EmberItem _ember)
    {
        if (_ember == null) return;

        _ember.EmberAcquiredEvent -= OnEmberAcquired;
        _ember.EmberExpiredEvent -= OnEmberExpired;
        _ember.EmberGroundedChangedEvent -= OnEmberGroundedChanged;
        _ember.EmberUpdateNeededChangedEvent -= OnEmberUpdateNeededChanged;
        Destroy(_ember.gameObject);
    }
}
