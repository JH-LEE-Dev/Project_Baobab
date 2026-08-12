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

    // 마을에 전시할 고정 순서 - LootPoint_01부터 차례로 채운다.
    private static readonly LootType[] displayOrder =
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
        for (int i = 0; i < displayOrder.Length && pointIndex < lootPoints.Length; i++)
        {
            if (!IsAcquired(_inDungeonObjectManager, displayOrder[i])) continue;

            Transform point = lootPoints[pointIndex];
            Transform pivot = (lootPointPivots != null && pointIndex < lootPointPivots.Length) ? lootPointPivots[pointIndex] : null;

            LootDisplayObject pillar = Instantiate(lootPillarPrefab, point.position, Quaternion.identity);
            pillar.ApplySortingBasis(pivot != null ? pivot.position.y : point.position.y);
            pillar.SetLootDisplay(displayOrder[i]);
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

    private bool IsAcquired(InDungeonObjectManager _inDungeonObjectManager, LootType _type)
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
