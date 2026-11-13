using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.Environment;

public class LootPillarManager : MonoBehaviour
{
    [SerializeField] private LootDisplayObject lootPillarPrefab;
    [SerializeField] private Transform[] lootPoints;
    // lootPoints와 1:1 대응 - 정렬 기준으로 쓸 실제 지면 접점(Pivot). 인덱스가 어긋나지 않도록 항상 같은 순서로 채운다.
    [SerializeField] private Transform[] lootPointPivots;

    // 마을에 전시할 고정 순서 - 아래 SpawnAcquiredPillars()가 획득한 것만 LootPoint_01부터
    // "빈칸 없이 앞에서부터" 채운다(미획득은 건너뛰되 포인트 인덱스는 올리지 않는다).
    //
    // TownTileManager.ApplyLootPillarColliderState()도 같은 순서 + 같은 압축 규칙으로 콜라이더 타일을
    // 채운다. 이 배열을 공유하는 것만으로는 부족하다 - 한쪽만 압축하면 획득 집합이 이 배열의
    // 접두사가 아닐 때 그림과 콜라이더가 어긋난다(실제로 그런 버그가 있었다). 한쪽 채우는 방식을
    // 바꾸면 반드시 다른 쪽도 같이 바꿀 것.
    public static readonly LootType[] DisplayOrder =
    {
        LootType.LostAndFoundBox,
        LootType.SporePotion,
        LootType.StarCompass,
        LootType.ObsidianCharm,
    };

    public event Action<bool, LootType> LootPillarInteractStateChangedEvent;
    public event Action<bool, LootType> LootPillarInteractEvent;

    private readonly List<LootDisplayObject> spawnedPillars = new List<LootDisplayObject>();
    // spawnedPillars와 1:1 - 각 기둥의 지면 접점(Pivot) 위치. 왼쪽 위 칸에 다른 기둥이 있는지 확인하는 데 쓴다.
    private readonly List<Vector2> spawnedPivots = new List<Vector2>();

    [Header("Side Interaction (왼쪽 위 면 상호작용)")]
    [SerializeField, Tooltip("기둥 지면 접점에서 왼쪽 위(북서) 이웃 칸까지의 오프셋(아이소 격자 1칸). 이 칸에 다른 기둥이 없을 때만 그 면까지 상호작용 범위를 늘린다.")]
    private Vector2 sideNeighborOffset = new Vector2(-0.5f, 0.25f);
    private const float SideNeighborTolerance = 0.1f;

    /// <summary>이번 마을 방문에 세워진 기둥들. 마을 강아지가 기둥의 상호작용 영역을 피하는 데 쓴다.</summary>
    public IReadOnlyList<LootDisplayObject> SpawnedPillars => spawnedPillars;

    private InputManager inputManager;

    // 전리품 성장 단계 변경(프레스티지 레벨업) 구독. 특성 창(TentUI)이 열려 있는 동안 단계가 바뀌면 기둥은 건드리지 않고
    // 전리품마다 최신 단계만 보류했다가, 창이 닫힐 때(PlayPendingEvolutions) 진화 연출로 반영한다.
    private InDungeonObjectManager subscribedObjectManager;
    private readonly Dictionary<LootType, int> pendingEvolutions = new Dictionary<LootType, int>(4);

    [Header("First Reveal (처음 얻은 전리품 기둥 등장)")]
    [SerializeField, Tooltip("처음 얻은 전리품이 한 번에 여럿일 때 기둥이 차례로 떨어지는 간격(초).")]
    private float firstRevealStagger = 0.35f;

    // 이번 방문에 처음 등장할 기둥들. 숨긴 채로 세워 두었다가 캐릭터가 움직일 수 있게 되면(PlayPendingFirstReveals) 떨어뜨린다.
    private readonly List<LootDisplayObject> pendingFirstReveals = new List<LootDisplayObject>(4);

    // 이번 마을 방문에서 조작이 이미 풀렸는지. 메인메뉴 → 마을 하강 연출을 건너뛰는 경로(캐릭터/카메라를 못 찾음)는 인트로 종료가
    // StartTownSystem 안에서 곧바로 불려 기둥을 세우기(SpawnAcquiredPillars) 전에 PlayPendingFirstReveals가 먼저 온다.
    // 그때 이 값을 기억해 두었다가 기둥을 세운 직후 바로 떨어뜨린다(안 그러면 그 방문 내내 숨은 채로 남는다).
    private bool bArrivalUnlocked;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
    }

    /// <summary>마을 방문이 새로 시작될 때(TownSystem.StartTownSystem 맨 앞) 지난 방문의 조작 해제 기록을 지운다.</summary>
    public void BeginTownVisit()
    {
        bArrivalUnlocked = false;
    }

    /// <summary>
    /// 영구 획득한 전리품 종류마다 LootPoint를 하나씩 순서대로 사용해 LootPillar를 생성한다.
    /// TownScene은 마을에 들어올 때마다 새로 로드되므로(=이전에 생성된 필러는 이미 사라진 상태),
    /// 매 TownSystem.StartTownSystem() 호출마다 현재 영구 획득 상태를 기준으로 다시 생성한다.
    /// </summary>
    public void SpawnAcquiredPillars(InDungeonObjectManager _inDungeonObjectManager)
    {
        if (lootPillarPrefab == null || lootPoints == null || _inDungeonObjectManager == null) return;

        // 지난 방문의 기둥이 범위 이탈 신호 없이 사라졌다면 켜 둔 상호작용 아이콘을 여기서 끈다(bInteractIconShown 주석 참고).
        if (true == bInteractIconShown)
        {
            bInteractIconShown = false;
            LootPillarInteractStateChangedEvent?.Invoke(false, LootType.None);
        }

        spawnedPillars.Clear();
        spawnedPivots.Clear();
        pendingFirstReveals.Clear();
        focusedPillar = null;
        focusCandidateCount = 0;

        // 새 기둥은 지금 단계로 만들어지므로 이전에 보류한 진화는 버린다.
        pendingEvolutions.Clear();
        if (null != subscribedObjectManager) subscribedObjectManager.LootGrowthTierChangedEvent -= OnLootGrowthTierChanged;
        subscribedObjectManager = _inDungeonObjectManager;
        subscribedObjectManager.LootGrowthTierChangedEvent += OnLootGrowthTierChanged;

        int pointIndex = 0;
        for (int i = 0; i < DisplayOrder.Length && pointIndex < lootPoints.Length; i++)
        {
            if (!IsAcquired(_inDungeonObjectManager, DisplayOrder[i])) continue;

            Transform point = lootPoints[pointIndex];
            Transform pivot = (lootPointPivots != null && pointIndex < lootPointPivots.Length) ? lootPointPivots[pointIndex] : null;

            LootDisplayObject pillar = Instantiate(lootPillarPrefab, point.position, Quaternion.identity);
            pillar.ApplySortingBasis(pivot != null ? pivot.position.y : point.position.y);
            pillar.SetLootDisplay(DisplayOrder[i]);
            pillar.SetGrowthTier(_inDungeonObjectManager.GetLootGrowthTier(DisplayOrder[i]));
            pillar.Initialize(inputManager);

            pillar.FocusCandidateChangedEvent -= RefreshFocus;
            pillar.FocusCandidateChangedEvent += RefreshFocus;

            pillar.LootPillarInteractEvent -= OnPillarInteract;
            pillar.LootPillarInteractEvent += OnPillarInteract;

            spawnedPillars.Add(pillar);

            // 처음 얻고 아직 등장 연출을 못 본 전리품이면 기둥을 숨긴 채 세워 두고, 조작이 풀릴 때 하늘에서 떨어뜨린다.
            if (true == _inDungeonObjectManager.IsPillarRevealPending(DisplayOrder[i]))
            {
                pillar.PrepareFirstReveal();
                pendingFirstReveals.Add(pillar);
            }

            spawnedPivots.Add(pivot != null ? (Vector2)pivot.position : (Vector2)point.position);
            pointIndex++;
        }

        ApplySideInteractExtensions();

        // 조작이 기둥을 세우기 전에 이미 풀렸다면(bArrivalUnlocked 주석 참고) 기다릴 신호가 다시 오지 않으므로 지금 떨어뜨린다.
        if (true == bArrivalUnlocked) PlayPendingFirstReveals();
    }

    // 기둥마다 왼쪽 위(북서) 칸이 비어 있으면 상호작용 범위를 그 면까지 늘린다(LootDisplayObject.SetSideInteractExtended).
    // LootPoint는 아이소 격자에서 한 칸씩 오른쪽 아래로 이어져 있어, 두 번째 기둥부터는 왼쪽 위 칸이 앞 기둥 자리다.
    // 그쪽으로 늘리면 앞 기둥 둘레에서 뒤 기둥이 골라지므로, 실제로 비어 있는지를 세운 기둥들의 지면 접점으로 확인한다.
    private void ApplySideInteractExtensions()
    {
        for (int i = 0; i < spawnedPillars.Count; i++)
        {
            if (null == spawnedPillars[i]) continue;

            Vector2 _neighbor = spawnedPivots[i] + sideNeighborOffset;
            bool _bBlocked = false;
            for (int j = 0; j < spawnedPillars.Count; j++)
            {
                if (j == i || null == spawnedPillars[j]) continue;
                if ((spawnedPivots[j] - _neighbor).sqrMagnitude <= SideNeighborTolerance * SideNeighborTolerance)
                {
                    _bBlocked = true;
                    break;
                }
            }

            spawnedPillars[i].SetSideInteractExtended(false == _bBlocked);
        }
    }

    // 단계 변경 이벤트(레벨이 올라 실제로 단계가 바뀐 전리품만 온다): 기둥은 건드리지 않고 최신 단계만 보류한다.
    private void OnLootGrowthTierChanged(LootType _lootType, int _tier)
    {
        pendingEvolutions[_lootType] = _tier;
    }

    /// <summary>
    /// 보류해 둔 전리품 진화를 기둥에 재생한다. 특성 창(TentUI)이 닫힐 때 TownSystem이 부른다.
    /// 여러 번 레벨업했어도 전리품마다 "지금 표시 단계 -> 최신 단계"로 한 번만 재생한다.
    /// </summary>
    public void PlayPendingEvolutions()
    {
        if (0 == pendingEvolutions.Count) return;

        for (int i = 0; i < spawnedPillars.Count; i++)
        {
            LootDisplayObject pillar = spawnedPillars[i];
            if (null == pillar) continue;

            if (true == pendingEvolutions.TryGetValue(pillar.CurrentLootType, out int tier))
            {
                pillar.PlayGrowthEvolve(tier);
            }
        }

        pendingEvolutions.Clear();
    }

    /// <summary>
    /// 숨겨 둔 첫 등장 기둥들을 하늘에서 떨어뜨린다. 마을에 도착해 캐릭터가 움직일 수 있게 된 순간 TownSystem이 부른다
    /// (던전 → 마을: TownArrivalControlUnlockedSignal, 메인메뉴 → 마을: MainMenuIntroEnd). 여럿이면 DisplayOrder 순서로 차례로 떨어진다.
    /// 연출이 시작되면 보류 목록에서 빼므로 다음 방문부터는 그냥 서 있다.
    /// </summary>
    public void PlayPendingFirstReveals()
    {
        bArrivalUnlocked = true;

        if (0 == pendingFirstReveals.Count) return;

        int _order = 0;
        for (int i = 0; i < pendingFirstReveals.Count; i++)
        {
            LootDisplayObject pillar = pendingFirstReveals[i];
            if (null == pillar) continue;

            pillar.PlayFirstReveal(_order * firstRevealStagger);
            subscribedObjectManager?.ConsumePillarReveal(pillar.CurrentLootType);
            _order++;
        }

        pendingFirstReveals.Clear();
    }

    private void OnDestroy()
    {
        if (null != subscribedObjectManager) subscribedObjectManager.LootGrowthTierChangedEvent -= OnLootGrowthTierChanged;
        subscribedObjectManager = null;
        pendingEvolutions.Clear();
    }

    // ─── 상호작용 대상 선택 ─────────────────────────────────────────────
    // 기둥끼리 상호작용 범위가 겹치면 각 기둥이 따로 아웃라인을 켜고 상호작용 키에 반응해 여러 개가 함께 켜지고
    // 함께 열렸다. 범위 안의 기둥(CanFocus) 중 캐릭터에 가장 가까운 하나만 상호작용 대상으로 고른다.

    [Header("Interaction Focus (겹친 상호작용 범위)")]
    [SerializeField, Tooltip("이미 고른 기둥보다 이만큼(유닛) 더 가까워야 대상을 바꾼다. 두 기둥 사이 경계에서 아웃라인이 깜빡이지 않게 한다.")]
    private float focusSwitchMargin = 0.1f;

    private LootDisplayObject focusedPillar;

    // 상호작용 아이콘(UIView_Unit)에 "켬"을 보내 둔 상태인지. 아이콘은 다른 상호작용과 켬/끔 횟수를 함께 세므로 켬과 끔을
    // 정확히 한 번씩 짝지어야 한다. 고른 기둥이 해제 없이 파괴되면 focusedPillar가 null이 되어 "끔"을 빠뜨리므로 따로 기억한다.
    private bool bInteractIconShown;

    // 범위 안 기둥(CanFocus) 수. 후보가 바뀔 때마다 기둥이 알리므로(FocusCandidateChangedEvent) RefreshFocus에서 센다.
    private int focusCandidateCount;

    // 범위 안 기둥이 둘 이상일 때만 캐릭터가 움직이며 더 가까워진 기둥으로 대상을 옮긴다.
    private void Update()
    {
        if (2 <= focusCandidateCount) RefreshFocus();
    }

    private void RefreshFocus()
    {
        focusCandidateCount = 0;
        for (int i = 0; i < spawnedPillars.Count; i++)
        {
            if (null != spawnedPillars[i] && true == spawnedPillars[i].CanFocus) focusCandidateCount++;
        }

        LootDisplayObject _current = (null != focusedPillar && true == focusedPillar.CanFocus) ? focusedPillar : null;
        LootDisplayObject _next = _current;

        // 창이 열려 있는 기둥은 범위를 벗어나기 전까지 대상을 바꾸지 않는다(바꾸면 창이 닫힌다).
        if (null == _current || false == _current.IsInteracting)
        {
            float _bestDistance = null != _current ? _current.GetFocusDistance() - focusSwitchMargin : float.MaxValue;

            for (int i = 0; i < spawnedPillars.Count; i++)
            {
                LootDisplayObject _pillar = spawnedPillars[i];
                if (null == _pillar || _pillar == _current || false == _pillar.CanFocus) continue;

                float _distance = _pillar.GetFocusDistance();
                if (null == _next || _distance < _bestDistance)
                {
                    _next = _pillar;
                    _bestDistance = _distance;
                }
            }
        }

        // 고른 기둥 말고는 전부 해제한다(이전 대상이 범위를 벗어났으면 여기서 창도 닫힌다).
        for (int i = 0; i < spawnedPillars.Count; i++)
        {
            LootDisplayObject _pillar = spawnedPillars[i];
            if (null != _pillar && _pillar != _next) _pillar.SetFocused(false);
        }

        if (null != _next) _next.SetFocused(true);
        focusedPillar = _next;

        // 상호작용 아이콘(UIView_Unit)은 "대상 없음 <-> 있음"이 바뀔 때만 알린다. 기둥끼리 대상만 옮겨 갈 때는 계속 켜 둔다.
        bool _bHasFocus = null != _next;
        if (bInteractIconShown != _bHasFocus)
        {
            bInteractIconShown = _bHasFocus;
            LootPillarInteractStateChangedEvent?.Invoke(_bHasFocus, _bHasFocus ? _next.CurrentLootType : LootType.None);
        }
    }

    private void OnPillarInteract(bool _bInteract, LootType _lootType)
    {
        LootPillarInteractEvent?.Invoke(_bInteract, _lootType);
    }

    /// <summary>
    /// UIView_ScreenModal이 상호작용 키 토글이 아닌 경로(ESC·패드 Cancel)로 닫혔을 때, 필러들의
    /// 내부 토글 상태를 실제 UI 상태에 맞춰준다. TownSystem이 LootPillarUIClosedSignal을 받아 호출한다.
    /// (TentManager.SyncInteractStateOnExternalClose()와 동일한 역할)
    ///
    /// 어느 필러가 열었는지 추적하지 않고 전부에 알린다 - 이미 닫힘 상태인 필러에는 아무 효과가 없다.
    /// </summary>
    public void SyncInteractStateOnExternalClose()
    {
        for (int i = 0; i < spawnedPillars.Count; i++)
        {
            LootDisplayObject pillar = spawnedPillars[i];
            if (null == pillar) continue;

            pillar.SyncInteractStateOnExternalClose();
        }
    }

    public static bool IsAcquired(InDungeonObjectManager _inDungeonObjectManager, LootType _type)
    {
        switch (_type)
        {
            case LootType.LostAndFoundBox: return _inDungeonObjectManager.bHasAcquiredLostAndFoundBox;
            case LootType.SporePotion: return _inDungeonObjectManager.bHasAcquiredSporePotion;
            case LootType.StarCompass: return _inDungeonObjectManager.bHasAcquiredStarCompass;
            case LootType.ObsidianCharm: return _inDungeonObjectManager.bHasAcquiredObsidianCharm;
            default: return false;
        }
    }
}
