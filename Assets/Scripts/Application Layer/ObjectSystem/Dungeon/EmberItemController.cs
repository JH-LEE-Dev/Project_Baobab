using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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

    private static readonly Vector3Int[] NeighborOffsets =
    {
        new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
    };
    private readonly List<Vector3Int> neighborBuffer = new List<Vector3Int>(8);

    public bool bEnabled => bEnabledThisRun;

    public void Initialize(ICharacter _character, IEnvironmentProvider _environmentProvider)
    {
        character = _character;
        environmentProvider = _environmentProvider;

        vfxComponent = GetComponent<VFXComponent>();
        if (vfxComponent != null) vfxComponent.Initialize();

        emberPool = new ObjectPool<EmberItem>(
            createFunc: CreateEmberItem,
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
            EmberItem ember = emberPool.Get();
            if (ember == null) continue;

            ember.Initialize(character, bGuideDrop ? 0f : droppedLifetime);

            // 원목과 같은 흩뿌림(LogItemController.SpawnLogItem과 같은 거리·높이).
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

                if (IsBlockedPosition(endPos))
                {
                    TryReleaseEmber(ember);
                    continue;
                }
            }

            if (bGuideDrop)
            {
                ember.bGuideTracked = true;
                guideEmbersRemaining++;
            }

            ember.Launch(_treePosition, endPos, UnityEngine.Random.Range(0.75f, 1.25f));
        }

        // 하나라도 실제로 떨어졌을 때만 첫 나무로 친다(전부 휴식 구역에 막히면 다음 나무가 다시 첫 나무가 된다).
        if (bGuideDrop && 0 < guideEmbersRemaining) bGuideFirstDropDone = true;
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
