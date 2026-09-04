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

    private InputManager inputManager;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
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

        int pointIndex = 0;
        for (int i = 0; i < DisplayOrder.Length && pointIndex < lootPoints.Length; i++)
        {
            if (!IsAcquired(_inDungeonObjectManager, DisplayOrder[i])) continue;

            Transform point = lootPoints[pointIndex];
            Transform pivot = (lootPointPivots != null && pointIndex < lootPointPivots.Length) ? lootPointPivots[pointIndex] : null;

            LootDisplayObject pillar = Instantiate(lootPillarPrefab, point.position, Quaternion.identity);
            pillar.ApplySortingBasis(pivot != null ? pivot.position.y : point.position.y);
            pillar.SetLootDisplay(DisplayOrder[i]);
            pillar.Initialize(inputManager);

            pillar.InteractStateChangedEvent -= OnPillarInteractStateChanged;
            pillar.InteractStateChangedEvent += OnPillarInteractStateChanged;

            pillar.LootPillarInteractEvent -= OnPillarInteract;
            pillar.LootPillarInteractEvent += OnPillarInteract;

            spawnedPillars.Add(pillar);

            pointIndex++;
        }
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
