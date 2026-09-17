using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 보석 나무가 최종적으로 쓰러질 때 떨어지는 원석의 풀과 드랍을 담당한다.
///
/// 드랍 위치 계산(퍼지는 방향/거리, 물 타일 회피, 포물선 높이/회전)은 LogItemController.SpawnLogItem과
/// 같은 값을 쓴다. 원목과 원석이 나란히 떨어지는 장면이 없더라도, 같은 나무에서 나오는 것들이
/// 서로 다른 느낌으로 흩어지면 어색하기 때문이다.
/// </summary>
public class GemOreItemController : MonoBehaviour
{
    public event Action<GemOreItem> GemOreItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private GemOreItem gemOreItemPrefab;

    [Header("원석 종류별 외형/재화량")]
    [SerializeField]
    private List<GemOreTypeData> gemOreTypeDatas = new List<GemOreTypeData>
    {
        new GemOreTypeData { gemOreType = GemOreType.Gold,    color = Color.white, currencyAmount = 1 },
        new GemOreTypeData { gemOreType = GemOreType.Diamond, color = Color.white, currencyAmount = 1 },
        new GemOreTypeData { gemOreType = GemOreType.Prism,   color = Color.white, currencyAmount = 1 },
    };

    [Header("보석 단계별 드랍 개수")]
    [SerializeField]
    private List<GemOreDropCntData> gemOreDropCntDatas = new List<GemOreDropCntData>
    {
        new GemOreDropCntData { gemOreType = GemOreType.Gold,    minCnt = 4, maxCnt = 7 },
        new GemOreDropCntData { gemOreType = GemOreType.Diamond, minCnt = 4, maxCnt = 7 },
        new GemOreDropCntData { gemOreType = GemOreType.Prism,   minCnt = 4, maxCnt = 7 },
    };

    // 내부 의존성
    private IObjectPool<GemOreItem> gemOrePool;
    private readonly List<GemOreItem> activeItemsList = new List<GemOreItem>(64);
    private readonly List<GemOreItem> cleanupList = new List<GemOreItem>(64);

    private ICharacter character;
    private ITilemapDataProvider tilemapDataProvider;

    // 물 타일 폴백 시 "몇 월드 유닛 = 1타일"인지 알아야 해서 실제 그리드 셀 크기를 한 번만 계산해 캐싱한다.
    // Initialize() 시점엔 아직 던전 타일맵이 생성되기 전이라 첫 드랍 때 지연 계산한다.
    private float tileWorldSize = 1f;
    private bool tileWorldSizeMeasured = false;

    public void Initialize(ICharacter _character, ITilemapDataProvider _tilemapDataProvider)
    {
        character = _character;
        tilemapDataProvider = _tilemapDataProvider;
        tileWorldSizeMeasured = false;

        gemOrePool = new ObjectPool<GemOreItem>(
            createFunc: CreateGemOreItem,
            actionOnGet: OnGetGemOreItem,
            actionOnRelease: OnReleaseGemOreItem,
            actionOnDestroy: OnDestroyGemOreItem,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: 32,
            maxSize: 200
        );
    }

    /// <summary>
    /// Initialize() 시점엔 캐릭터가 아직 스폰되기 전이라 null이 들어온다.
    /// 캐릭터 스폰 이후 ItemManager.SetCharacter를 통해 뒤늦게 주입받는다.
    /// </summary>
    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        for (int i = 0; i < activeItemsList.Count; i++)
        {
            activeItemsList[i].SetCharacter(_character);
        }
    }

    private void Update()
    {
        if (activeItemsList.Count == 0) return;

        float deltaTime = Time.deltaTime;

        // ManualUpdate 중 아이템이 획득되어 리스트가 변형될 수 있으므로 역순 순회
        for (int i = activeItemsList.Count - 1; i >= 0; i--)
        {
            activeItemsList[i].ManualUpdate(deltaTime);
        }
    }

    // ── 풀 ──

    private GemOreItem CreateGemOreItem()
    {
        if (gemOreItemPrefab == null) return null;

        GemOreItem newItem = Instantiate(gemOreItemPrefab, transform);
        newItem.GemOreItemAcquired -= OnGemOreItemAcquired;
        newItem.GemOreItemAcquired += OnGemOreItemAcquired;

        return newItem;
    }

    /// <summary>
    /// 이미 풀에 들어가 있는 항목을 다시 반환하지 않도록 막고 반환한다.
    /// IsPooled는 풀의 actionOnGet/actionOnRelease에서만 갱신된다.
    /// </summary>
    private bool TryReleaseGemOreItem(GemOreItem _item)
    {
        if (_item == null || _item.IsPooled) return false;

        gemOrePool.Release(_item);
        return true;
    }

    private void OnGetGemOreItem(GemOreItem _item)
    {
        _item.IsPooled = false;
        _item.UpdateIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        _item.gameObject.SetActive(true);
        _item.ResetItem();
    }

    private void OnReleaseGemOreItem(GemOreItem _item)
    {
        _item.IsPooled = true;

        // Swap-with-last 방식을 이용한 리스트 삭제 (O(1))
        int idx = _item.UpdateIndex;
        if (idx != -1 && idx < activeItemsList.Count)
        {
            int lastIdx = activeItemsList.Count - 1;
            if (idx != lastIdx)
            {
                GemOreItem lastItem = activeItemsList[lastIdx];
                activeItemsList[idx] = lastItem;
                lastItem.UpdateIndex = idx;
            }
            activeItemsList.RemoveAt(lastIdx);
            _item.UpdateIndex = -1;
        }

        _item.gameObject.SetActive(false);
    }

    private void OnDestroyGemOreItem(GemOreItem _item)
    {
        if (_item == null) return;

        _item.GemOreItemAcquired -= OnGemOreItemAcquired;
        OnReleaseGemOreItem(_item);
        Destroy(_item.gameObject);
    }

    private void OnGemOreItemAcquired(GemOreItem _item)
    {
        // 원목 획득과 동일한 손맛(짧은 톡 하는 진동)
        Rumble.Play(EHapticEvent.ItemPickup);

        GemOreItemAcquiredEvent?.Invoke(_item);

        TryReleaseGemOreItem(_item);
    }

    // ── 드랍 ──

    /// <summary>
    /// 보석 나무가 쓰러진 자리에 원석을 뿌린다.
    /// </summary>
    /// <param name="_treeObj">쓰러진 나무. 마지막 보석 단계(gemStage)로 원석 종류를 정한다.</param>
    /// <param name="_multiplier">등급 드랍 배율(원목과 동일하게 적용)</param>
    public void SpawnGemOre(TreeObj _treeObj, float _multiplier)
    {
        if (_treeObj == null || gemOreItemPrefab == null) return;

        GemOreType oreType = GemStageToOreType(_treeObj.gemStage);
        if (oreType == GemOreType.None) return;

        if (!tileWorldSizeMeasured)
        {
            MeasureTileWorldSize();
        }

        GemOreTypeData typeData = GetTypeData(oreType);
        GemOreDropCntData dropCntData = GetDropCntData(oreType);

        int spawnCount = Mathf.RoundToInt(
            UnityEngine.Random.Range(dropCntData.minCnt, dropCntData.maxCnt + 1) * _multiplier);
        if (spawnCount <= 0) return;

        Vector3 spawnPos = _treeObj.transform.position;

        // 보석 등급 원목과 마찬가지로, 개수만큼 겹쳐 울리면 뭉개지므로 드랍 묶음당 한 번만 울린다.
        Sound.Play(SoundID.NiceItem, spawnPos);
        Rumble.Play(EHapticEvent.RareLogSpawn);

        for (int i = 0; i < spawnCount; i++)
        {
            GemOreItem oreItem = gemOrePool.Get();
            if (oreItem == null) continue;

            oreItem.transform.position = spawnPos;
            oreItem.Initialize(typeData, character);

            // 포물선 운동 설정 (LogItemController.SpawnLogItem과 동일한 값)
            Vector3 startPos = spawnPos;
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(1.25f, 1.75f);
            Vector3 endPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * randomDist;

            // 물 타일에 착지하면 캐릭터가 접근할 수 없다. 같은 방향으로 1타일 이내까지만 당겨서
            // 한 번 더 확인하고, 그마저도 물이면 그때만 나무 위치로 대체한다.
            if (tilemapDataProvider != null && tilemapDataProvider.IsWaterTile(tilemapDataProvider.WorldToCell(endPos)))
            {
                float pulledDist = Mathf.Min(randomDist, tileWorldSize * 0.9f);
                Vector3 pulledPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * pulledDist;

                endPos = tilemapDataProvider.IsWaterTile(tilemapDataProvider.WorldToCell(pulledPos)) ? spawnPos : pulledPos;
            }

            float height = UnityEngine.Random.Range(0.75f, 1.25f);
            float randomRotation = UnityEngine.Random.Range(1, 3) * 360f * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

            oreItem.Launch(startPos, endPos, height, randomRotation);
        }
    }

    /// <summary>
    /// 나무의 보석 단계를 원석 종류로 바꾼다.
    /// TreeObj.GemStageToVirtualGrade와 같은 대응이다 (1: 황금, 2: 다이아, 3: 프리즘).
    /// </summary>
    public static GemOreType GemStageToOreType(int _gemStage)
    {
        switch (_gemStage)
        {
            case 1: return GemOreType.Gold;
            case 2: return GemOreType.Diamond;
            case 3: return GemOreType.Prism;
            default: return GemOreType.None;
        }
    }

    private GemOreTypeData GetTypeData(GemOreType _type)
    {
        if (gemOreTypeDatas != null)
        {
            for (int i = 0; i < gemOreTypeDatas.Count; i++)
            {
                if (gemOreTypeDatas[i].gemOreType == _type)
                    return gemOreTypeDatas[i];
            }
        }

        // 데이터를 채워두지 않았을 때도 원석 자체는 떨어지도록 기본값을 돌려준다.
        return new GemOreTypeData { gemOreType = _type, color = Color.white, currencyAmount = 1 };
    }

    private GemOreDropCntData GetDropCntData(GemOreType _type)
    {
        if (gemOreDropCntDatas != null)
        {
            for (int i = 0; i < gemOreDropCntDatas.Count; i++)
            {
                if (gemOreDropCntDatas[i].gemOreType == _type)
                    return gemOreDropCntDatas[i];
            }
        }

        return new GemOreDropCntData { gemOreType = _type, minCnt = 4, maxCnt = 7 };
    }

    // Initialize() 시점엔 던전 타일맵이 아직 생성 전이라 측정이 불가능하므로,
    // 나무가 실제로 존재하는(= 맵 생성이 끝난) 첫 드랍 시점에 한 번만 측정해 캐싱한다.
    private void MeasureTileWorldSize()
    {
        tileWorldSizeMeasured = true;

        if (tilemapDataProvider == null) return;

        Vector3Int originCell = tilemapDataProvider.WorldToCell(Vector3.zero);
        float measuredSize = Vector3.Distance(
            tilemapDataProvider.CellToWorld(originCell),
            tilemapDataProvider.CellToWorld(originCell + Vector3Int.right));

        if (measuredSize > 0f) tileWorldSize = measuredSize;
    }

    // ── 정리 ──

    /// <summary>
    /// 흡입이 끝나기 전에 던전이 종료되는 경우, 아직 활성 상태인 원석을 전부 획득 처리한다.
    /// 재화는 놓치면 그대로 손해이므로 확정 지급한다(LootManager.ForceAcquireAllActive와 같은 취지).
    /// </summary>
    public void ForceAcquireAllActive()
    {
        if (activeItemsList.Count == 0) return;

        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            GemOreItemAcquiredEvent?.Invoke(cleanupList[i]);
            TryReleaseGemOreItem(cleanupList[i]);
        }

        cleanupList.Clear();
    }

    public void ClearAll()
    {
        if (activeItemsList.Count == 0) return;

        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            TryReleaseGemOreItem(cleanupList[i]);
        }

        activeItemsList.Clear();
        cleanupList.Clear();
    }

    public void ReturnToPool(GemOreItem _item)
    {
        TryReleaseGemOreItem(_item);
    }
}
