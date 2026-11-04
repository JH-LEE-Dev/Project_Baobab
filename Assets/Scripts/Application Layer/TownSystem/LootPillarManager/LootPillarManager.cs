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

        spawnedPillars.Clear();
        pendingFirstReveals.Clear();

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

            pillar.InteractStateChangedEvent -= OnPillarInteractStateChanged;
            pillar.InteractStateChangedEvent += OnPillarInteractStateChanged;

            pillar.LootPillarInteractEvent -= OnPillarInteract;
            pillar.LootPillarInteractEvent += OnPillarInteract;

            spawnedPillars.Add(pillar);

            // 처음 얻고 아직 등장 연출을 못 본 전리품이면 기둥을 숨긴 채 세워 두고, 조작이 풀릴 때 하늘에서 떨어뜨린다.
            if (true == _inDungeonObjectManager.IsPillarRevealPending(DisplayOrder[i]))
            {
                pillar.PrepareFirstReveal();
                pendingFirstReveals.Add(pillar);
            }

            pointIndex++;
        }

        // 조작이 기둥을 세우기 전에 이미 풀렸다면(bArrivalUnlocked 주석 참고) 기다릴 신호가 다시 오지 않으므로 지금 떨어뜨린다.
        if (true == bArrivalUnlocked) PlayPendingFirstReveals();
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

    private void OnPillarInteractStateChanged(bool _state, LootType _lootType)
    {
        LootPillarInteractStateChangedEvent?.Invoke(_state, _lootType);
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
