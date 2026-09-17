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
public class GemOreItemController : MonoBehaviour, IGemOreAuraProvider
{
    public event Action<GemOreItem> GemOreItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private GemOreItem gemOreItemPrefab;

    [Header("원석 종류별 외형 (크기별 그림)")]
    [SerializeField] private List<GemOreTypeData> gemOreTypeDatas = new List<GemOreTypeData>();

    [Header("나무 종류 x 보석 단계별 드랍 테이블")]
    [Tooltip("총 재화량이 기준이고, 알갱이 개수는 그 총량을 어떻게 나눠 보여줄지를 정한다.")]
    [SerializeField] private List<GemOreDropData> gemOreDropDatas = new List<GemOreDropData>();

    [Header("Gem Aura")]
    [Tooltip("원석에 붙는 아우라. 원석 종류별로 프리셋 프리팹을 연결한다(원목의 보석 등급 아우라와 동일).")]
    [SerializeField] private List<GemOreAuraData> gemOreAuraDatas = new List<GemOreAuraData>();
    [SerializeField] private int auraPoolDefaultCapacity = 8;
    [SerializeField] private int auraPoolMaxSize = 64;

    [Header("크기별 재화 분배 가중치")]
    [Tooltip("총 재화량을 알갱이들에 나눌 때 쓰는 비중. S:M:L = 1:3:9면 M 하나가 S 셋, L 하나가 S 아홉 몫을 가져간다.")]
    [SerializeField] private float smallWeight = 1f;
    [SerializeField] private float mediumWeight = 3f;
    [SerializeField] private float largeWeight = 9f;

    [Header("Optimization")]
    [SerializeField] private bool enableCulling = true;
    [SerializeField] private float cullingUpdateInterval = 0.05f;

    // 내부 의존성
    private IObjectPool<GemOreItem> gemOrePool;
    private readonly List<GemOreItem> activeItemsList = new List<GemOreItem>(256);      // 마스터 리스트 (컬링 그룹용)
    private readonly List<GemOreItem> activeItemsForUpdate = new List<GemOreItem>(256); // 업데이트 리스트 (가시성 기준)

    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;

    // 드랍 1회 계산용 재사용 버퍼 (매 그루 할당을 피한다)
    private readonly List<GemOreSize> sizeBuffer = new List<GemOreSize>(32);
    private readonly List<long> amountBuffer = new List<long>(32);
    private readonly List<float> weightBuffer = new List<float>(32);
    private readonly List<float> fractionBuffer = new List<float>(32);

    // 값이 비어 있는 조합을 만났을 때 한 번만 알리기 위한 기록
    private readonly HashSet<int> warnedCombinations = new HashSet<int>();

    private ICharacter character;
    private ITilemapDataProvider tilemapDataProvider;

    private VFXComponent vfxComponent;
    private Dictionary<GemOreType, IObjectPool<ItemAuraEffectController>> auraPools;

    // 물 타일 폴백 시 "몇 월드 유닛 = 1타일"인지 알아야 해서 실제 그리드 셀 크기를 한 번만 계산해 캐싱한다.
    // Initialize() 시점엔 아직 던전 타일맵이 생성되기 전이라 첫 드랍 때 지연 계산한다.
    private float tileWorldSize = 1f;
    private bool tileWorldSizeMeasured = false;

    public void Initialize(ICharacter _character, ITilemapDataProvider _tilemapDataProvider)
    {
        character = _character;
        tilemapDataProvider = _tilemapDataProvider;
        tileWorldSizeMeasured = false;

        // 반짝임 파티클("Shiny")은 이 컴포넌트가 풀로 들고 있다가 원석마다 빌려준다.
        vfxComponent = GetComponent<VFXComponent>();
        if (vfxComponent != null) vfxComponent.Initialize();

        BuildAuraPools();

        gemOrePool = new ObjectPool<GemOreItem>(
            createFunc: CreateGemOreItem,
            actionOnGet: OnGetGemOreItem,
            actionOnRelease: OnReleaseGemOreItem,
            actionOnDestroy: OnDestroyGemOreItem,
            collectionCheck: PoolSettings.CollectionCheck,
            // 게임 중후반에는 보석 나무가 맵마다 여러 그루 뜨므로 원목과 같은 규모로 잡는다.
            defaultCapacity: 200,
            maxSize: 1000
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
        float deltaTime = Time.deltaTime;

        // 가시 영역 안에서 움직이는 원석만 갱신한다
        if (activeItemsForUpdate.Count > 0)
        {
            // ManualUpdate 중 아이템이 해제(Release)되어 리스트가 변형될 수 있으므로 역순 순회
            for (int i = activeItemsForUpdate.Count - 1; i >= 0; i--)
            {
                activeItemsForUpdate[i].ManualUpdate(deltaTime);
            }
        }

        // 컬링 구체 위치 업데이트 (스로틀링) - 마스터 리스트 기반
        if (enableCulling && cullingGroup != null && activeItemsList.Count > 0)
        {
            cullingUpdateTimer += deltaTime;
            if (cullingUpdateTimer >= cullingUpdateInterval)
            {
                UpdateCullingSpheres();
                cullingUpdateTimer = 0f;
            }
        }
    }

    // ── 컬링 (LogItemController와 동일한 구조) ──

    public void SetupCullingGroup()
    {
        if (!enableCulling) return;

        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = OnCullingStateChanged;
        }

        cullingGroup.targetCamera = Camera.main;
        spheres = new BoundingSphere[1000];
        cullingGroup.SetBoundingSpheres(spheres);
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (!enableCulling) return;
        if (_ev.index >= activeItemsList.Count) return;

        UpdateItemVisibility(activeItemsList[_ev.index], _ev.isVisible);
    }

    private void UpdateItemVisibility(GemOreItem _item, bool _isVisible)
    {
        if (_item.gameObject.activeSelf != _isVisible)
        {
            _item.gameObject.SetActive(_isVisible);
        }

        if (_isVisible)
        {
            if (_item.UpdateIndex == -1 && _item.IsMoving)
            {
                _item.UpdateIndex = activeItemsForUpdate.Count;
                activeItemsForUpdate.Add(_item);
                _item.bCanGetSortingOrder = true;
            }
        }
        else
        {
            int idx = _item.UpdateIndex;
            if (idx != -1)
            {
                int lastIdx = activeItemsForUpdate.Count - 1;
                if (idx != lastIdx)
                {
                    GemOreItem lastItem = activeItemsForUpdate[lastIdx];
                    activeItemsForUpdate[idx] = lastItem;
                    lastItem.UpdateIndex = idx;
                }
                activeItemsForUpdate.RemoveAt(lastIdx);
                _item.UpdateIndex = -1;
                _item.bCanGetSortingOrder = false;
            }
        }
    }

    private void UpdateCullingSpheres()
    {
        int count = activeItemsForUpdate.Count;
        for (int i = 0; i < count; i++)
        {
            GemOreItem item = activeItemsForUpdate[i];
            if (item.IsMoving && item.PoolIndex != -1)
            {
                spheres[item.PoolIndex].position = item.transform.position;
            }
        }
    }

    // 원석이 움직이기 시작하면 업데이트 목록에 넣고, 완전히 안착하면 뺀다.
    private void GemOreItemActivated(GemOreItem _item)
    {
        if (_item.UpdateIndex == -1)
        {
            _item.UpdateIndex = activeItemsForUpdate.Count;
            activeItemsForUpdate.Add(_item);
            _item.bCanGetSortingOrder = true;
        }
    }

    private void GemOreItemDeActivated(GemOreItem _item)
    {
        int idx = _item.UpdateIndex;
        if (idx == -1) return;

        int lastIdx = activeItemsForUpdate.Count - 1;
        if (idx != lastIdx)
        {
            GemOreItem lastItem = activeItemsForUpdate[lastIdx];
            activeItemsForUpdate[idx] = lastItem;
            lastItem.UpdateIndex = idx;
        }
        activeItemsForUpdate.RemoveAt(lastIdx);
        _item.UpdateIndex = -1;
        _item.bCanGetSortingOrder = false;
    }

    private void OnDestroy()
    {
        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }

    // ── 풀 ──

    private GemOreItem CreateGemOreItem()
    {
        if (gemOreItemPrefab == null) return null;

        GemOreItem newItem = Instantiate(gemOreItemPrefab, transform);
        newItem.GemOreItemAcquired -= OnGemOreItemAcquired;
        newItem.GemOreItemAcquired += OnGemOreItemAcquired;

        newItem.GemOreItemActivatedEvent -= GemOreItemActivated;
        newItem.GemOreItemActivatedEvent += GemOreItemActivated;

        newItem.GemOreItemDeActivatedEvent -= GemOreItemDeActivated;
        newItem.GemOreItemDeActivatedEvent += GemOreItemDeActivated;

        newItem.SetVfxComponent(vfxComponent);
        newItem.SetAuraProvider(this);

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
        // 마스터 리스트 추가 및 인덱스 설정 (O(1))
        _item.PoolIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        // BoundingSphere 즉시 동기화
        if (enableCulling)
        {
            if (spheres == null)
            {
                spheres = new BoundingSphere[1000];
                if (cullingGroup != null) cullingGroup.SetBoundingSpheres(spheres);
            }

            if (spheres.Length <= _item.PoolIndex)
            {
                Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, _item.PoolIndex + 1));
                if (cullingGroup != null) cullingGroup.SetBoundingSpheres(spheres);
            }
            spheres[_item.PoolIndex] = new BoundingSphere(_item.transform.position, 1f);
        }

        if (enableCulling && cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(activeItemsList.Count);
            // 즉시 가시성 체크하여 활성화 및 업데이트 등록 여부 결정
            UpdateItemVisibility(_item, cullingGroup.IsVisible(_item.PoolIndex));
        }
        else
        {
            _item.gameObject.SetActive(true);
            // 컬링이 꺼져 있거나 컬링 그룹이 없으면 무조건 업데이트 리스트에 추가
            _item.UpdateIndex = activeItemsForUpdate.Count;
            activeItemsForUpdate.Add(_item);
            _item.bCanGetSortingOrder = true;
        }

        _item.ResetItem();
    }

    private void OnReleaseGemOreItem(GemOreItem _item)
    {
        _item.IsPooled = true;

        // 빌려간 아우라를 여기서 바로 회수한다. ResetItem은 다음 획득 때 호출되므로,
        // 그때까지 기다리면 풀에서 쉬고 있는 원석들이 아우라를 붙든 채로 남는다.
        _item.ReleaseGemAura();

        // 반짝임 파티클도 같은 이유로 여기서 회수한다. 자식으로 매달린 채 부모가 꺼지면
        // 파티클의 activeSelf는 true로 남아 VFX 풀이 "사용 중"으로 오인하고 영영 누수된다.
        _item.StopGemShiny();

        // 업데이트 리스트에서 제거
        UpdateItemVisibility(_item, false);

        // 마스터 리스트에서 Swap-with-last 방식을 이용한 제거 (O(1))
        int idx = _item.PoolIndex;
        if (idx != -1 && idx < activeItemsList.Count)
        {
            int lastIdx = activeItemsList.Count - 1;
            if (idx != lastIdx)
            {
                GemOreItem lastItem = activeItemsList[lastIdx];
                activeItemsList[idx] = lastItem;
                lastItem.PoolIndex = idx;
                if (enableCulling && spheres != null) spheres[idx] = spheres[lastIdx];
            }
            activeItemsList.RemoveAt(lastIdx);
            _item.PoolIndex = -1;

            if (enableCulling && cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(activeItemsList.Count);
            }
        }

        _item.gameObject.SetActive(false);
    }

    private void OnDestroyGemOreItem(GemOreItem _item)
    {
        if (_item == null) return;

        _item.GemOreItemAcquired -= OnGemOreItemAcquired;
        _item.GemOreItemActivatedEvent -= GemOreItemActivated;
        _item.GemOreItemDeActivatedEvent -= GemOreItemDeActivated;

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
    ///
    /// 기준은 <b>총 재화량</b>이다. 먼저 이 나무 한 그루가 줄 총량을 굴리고, 알갱이 개수를 따로 굴린 뒤,
    /// 총량을 알갱이들에 크기 비중대로 쪼개 담는다. 알갱이마다 고정값을 주는 방식으로는 기획 표의
    /// 범위를 재현할 수 없다 (자작나무 S 4~6 + M 2~3에 총 36~50이라면, 최소 조합의 1.5배가
    /// 최대 조합인데 36의 1.5배는 54라서 50과 맞지 않는다).
    /// </summary>
    /// <param name="_treeObj">쓰러진 나무. 나무 종류와 마지막 보석 단계(gemStage)로 드랍 줄을 고른다.</param>
    /// <param name="_multiplier">등급 드랍 배율(원목과 동일하게 적용)</param>
    /// <param name="_jackPotChance">잭팟 발생 확률(0~1). 원목과 같은 스킬 값을 그대로 받는다.</param>
    /// <param name="_jackPotAmount">잭팟이 터졌을 때 곱해질 배율</param>
    public void SpawnGemOre(TreeObj _treeObj, float _multiplier, float _jackPotChance, float _jackPotAmount)
    {
        if (_treeObj == null || gemOreItemPrefab == null) return;

        // 잭팟 스킬은 원목과 동일하게 동작해야 한다. 원목은 드랍 개수에만 곱하지만 원석은
        // 총 재화량이 실제 보상이므로, 개수와 총량 양쪽에 같은 배율이 걸리도록 _multiplier에 합친다.
        if (UnityEngine.Random.value < _jackPotChance)
        {
            _multiplier *= _jackPotAmount;
        }

        GemOreType oreType = GemStageToOreType(_treeObj.gemStage);
        if (oreType == GemOreType.None) return;

        TreeType treeType = _treeObj.treeData.type;

        if (!TryGetDropData(treeType, oreType, out GemOreDropData dropData))
        {
            // 아직 값을 안 채운 조합. 조용히 아무것도 안 떨어뜨리면 버그로 오해하기 쉬우므로 알린다.
            WarnMissingDropData(treeType, oreType);
            return;
        }

        if (!tileWorldSizeMeasured)
        {
            MeasureTileWorldSize();
        }

        // 1. 알갱이 개수를 굴린다 (배율은 원목과 같은 의미로 개수에 적용)
        int smallCnt = RollCount(dropData.minSmallCnt, dropData.maxSmallCnt, _multiplier);
        int mediumCnt = RollCount(dropData.minMediumCnt, dropData.maxMediumCnt, _multiplier);
        int largeCnt = RollCount(dropData.minLargeCnt, dropData.maxLargeCnt, _multiplier);

        int totalCnt = smallCnt + mediumCnt + largeCnt;
        if (totalCnt <= 0) return;

        // 2. 총 재화량을 굴린다. 개수와 같은 배율을 총량에도 먹여야 "2배 드랍"이 실제로 2배 이득이 된다.
        int totalCurrency = Mathf.RoundToInt(
            UnityEngine.Random.Range(dropData.minTotalCurrency, dropData.maxTotalCurrency + 1) * _multiplier);
        if (totalCurrency <= 0) return;

        // 3. 알갱이가 총 재화량보다 많으면 재화 0짜리 알갱이가 생긴다. 총량이 기준이므로
        //    개수 쪽을 줄이되, 큰 알갱이부터 남겨 눈에 보이는 무게감은 지킨다.
        if (totalCnt > totalCurrency)
        {
            int excess = totalCnt - totalCurrency;

            int cut = Mathf.Min(excess, smallCnt);
            smallCnt -= cut; excess -= cut;

            cut = Mathf.Min(excess, mediumCnt);
            mediumCnt -= cut; excess -= cut;

            cut = Mathf.Min(excess, largeCnt);
            largeCnt -= cut;

            totalCnt = smallCnt + mediumCnt + largeCnt;
            if (totalCnt <= 0) return;
        }

        // 4. 크기 목록을 만들고 총량을 비중대로 나눈다
        sizeBuffer.Clear();
        for (int i = 0; i < largeCnt; i++) sizeBuffer.Add(GemOreSize.Large);
        for (int i = 0; i < mediumCnt; i++) sizeBuffer.Add(GemOreSize.Medium);
        for (int i = 0; i < smallCnt; i++) sizeBuffer.Add(GemOreSize.Small);

        DistributeCurrency(sizeBuffer, totalCurrency, amountBuffer);

        GemOreTypeData typeData = GetTypeData(oreType);
        Vector3 spawnPos = _treeObj.transform.position;

        // 보석 등급 원목과 마찬가지로, 개수만큼 겹쳐 울리면 뭉개지므로 드랍 묶음당 한 번만 울린다.
        Sound.Play(SoundID.NiceItem, spawnPos);
        Rumble.Play(EHapticEvent.RareLogSpawn);

        for (int i = 0; i < sizeBuffer.Count; i++)
        {
            GemOreItem oreItem = gemOrePool.Get();
            if (oreItem == null) continue;

            oreItem.transform.position = spawnPos;
            oreItem.Initialize(typeData, sizeBuffer[i], amountBuffer[i], character);

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

    public static int RollCount(int _min, int _max, float _multiplier)
    {
        if (_max <= 0) return 0;

        int rolled = UnityEngine.Random.Range(Mathf.Max(0, _min), _max + 1);
        return Mathf.Max(0, Mathf.RoundToInt(rolled * _multiplier));
    }

    /// <summary>
    /// 이 드랍의 크기 목록을 가중치로 바꿔 총 재화량을 쪼갠다.
    /// </summary>
    private void DistributeCurrency(List<GemOreSize> _sizes, int _totalCurrency, List<long> _outAmounts)
    {
        weightBuffer.Clear();
        for (int i = 0; i < _sizes.Count; i++)
        {
            weightBuffer.Add(GetSizeWeight(_sizes[i]));
        }

        DistributeByWeight(weightBuffer, _totalCurrency, _outAmounts, fractionBuffer);
    }

    /// <summary>
    /// 총량을 가중치 비율대로 정수로 쪼갠다. 상태에 기대지 않는 순수 계산이다.
    ///
    /// 비중대로 나누면 소수점이 남는데 그냥 버리면 합이 총량에 못 미친다. 그래서 최대잉여법을 쓴다
    /// (몫의 소수부가 큰 것부터 남은 1씩을 가져간다). <b>합계는 항상 정확히 총량과 같아진다.</b>
    /// 모든 몫에 최소 1을 먼저 떼어주므로, 주웠는데 0이 들어오는 알갱이는 생기지 않는다
    /// (총량이 개수보다 적으면 호출 전에 개수를 줄여둔다).
    /// </summary>
    /// <param name="_scratch">소수부 계산용 임시 버퍼. 매 호출 할당을 피하려고 밖에서 받는다.</param>
    public static void DistributeByWeight(List<float> _weights, int _totalCurrency, List<long> _outAmounts, List<float> _scratch)
    {
        _outAmounts.Clear();

        int count = _weights.Count;
        if (count <= 0) return;

        // 모든 몫에 최소 1을 보장하고, 남은 양만 비중대로 나눈다
        int remaining = _totalCurrency - count;

        if (remaining <= 0)
        {
            for (int i = 0; i < count; i++) _outAmounts.Add(1);
            return;
        }

        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
        {
            totalWeight += Mathf.Max(0f, _weights[i]);
        }

        // 가중치를 전부 0으로 꺼두면 균등 분배로 되돌린다
        bool uniform = totalWeight <= 0f;
        if (uniform) totalWeight = count;

        int assigned = 0;
        _scratch.Clear();

        for (int i = 0; i < count; i++)
        {
            float w = uniform ? 1f : Mathf.Max(0f, _weights[i]);
            float exact = remaining * (w / totalWeight);
            int floor = Mathf.FloorToInt(exact);

            _outAmounts.Add(1 + floor);
            _scratch.Add(exact - floor);
            assigned += floor;
        }

        // 최대잉여법: 소수부가 큰 것부터 남은 1씩 배분
        int leftover = remaining - assigned;
        int guard = 0;
        while (leftover > 0 && guard++ < 100000)
        {
            int bestIdx = -1;
            float bestFraction = -1f;

            for (int i = 0; i < count; i++)
            {
                if (_scratch[i] > bestFraction)
                {
                    bestFraction = _scratch[i];
                    bestIdx = i;
                }
            }

            if (bestIdx < 0) break;

            _outAmounts[bestIdx] += 1;
            _scratch[bestIdx] = -1f; // 한 바퀴에 같은 몫이 두 번 받지 않도록
            leftover--;

            // 잉여가 개수보다 많이 남았다면 소수부를 비중으로 되돌리고 한 바퀴 더 돈다
            if (leftover > 0)
            {
                bool allConsumed = true;
                for (int i = 0; i < count; i++)
                {
                    if (_scratch[i] >= 0f) { allConsumed = false; break; }
                }

                if (allConsumed)
                {
                    for (int i = 0; i < count; i++)
                    {
                        _scratch[i] = uniform ? 1f : Mathf.Max(0f, _weights[i]);
                    }
                }
            }
        }
    }

    public float GetSizeWeight(GemOreSize _size)
    {
        switch (_size)
        {
            case GemOreSize.Large: return Mathf.Max(0f, largeWeight);
            case GemOreSize.Medium: return Mathf.Max(0f, mediumWeight);
            default: return Mathf.Max(0f, smallWeight);
        }
    }

    private bool TryGetDropData(TreeType _treeType, GemOreType _gemOreType, out GemOreDropData _data)
    {
        _data = default;
        if (gemOreDropDatas == null) return false;

        for (int i = 0; i < gemOreDropDatas.Count; i++)
        {
            if (gemOreDropDatas[i].treeType == _treeType &&
                gemOreDropDatas[i].gemOreType == _gemOreType &&
                !gemOreDropDatas[i].IsEmpty)
            {
                _data = gemOreDropDatas[i];
                return true;
            }
        }

        return false;
    }

    // 값이 비어 있는 조합을 만났을 때 한 번만 알린다. 매 그루마다 찍으면 콘솔이 잠긴다.
    private void WarnMissingDropData(TreeType _treeType, GemOreType _gemOreType)
    {
        int key = ((int)_treeType << 8) | (int)_gemOreType;
        if (!warnedCombinations.Add(key)) return;

        Debug.LogWarning("[GemOreItemController] " + _treeType + " x " + _gemOreType +
                         " 드랍 값이 비어 있어 원석이 떨어지지 않습니다. " +
                         "GameInstaller > ItemManager > GemOreItemController의 드랍 테이블을 채워주세요.", this);
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

        // 외형을 채워두지 않았어도 재화는 들어와야 하므로 그림 없는 기본값을 돌려준다.
        return new GemOreTypeData { gemOreType = _type, color = Color.white };
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
    /// 타운 귀환(DropAllItem/오프로드 탑승) 확정 시점에 호출한다. 원목(LogItemController.CancelActiveSucking)과
    /// 완전히 같은 처리다: 이미 캐릭터를 향해 흡입 중이던 것은 습득 처리 없이 그대로 풀로 반환해
    /// 자연스럽게 사라지게 하고, 아직 흡입을 시작하지 않은 것은 bCanAcquired를 꺼서 더 이상 습득되지 않게 한다.
    /// </summary>
    public void CancelActiveSucking()
    {
        for (int i = activeItemsList.Count - 1; i >= 0; i--)
        {
            GemOreItem item = activeItemsList[i];

            if (item.MoveState == ItemMoveState.Sucking)
            {
                TryReleaseGemOreItem(item);
            }
            else
            {
                item.SetbCanAcquired(false);
            }
        }
    }

    public void ClearAll()
    {
        int count = activeItemsList.Count;
        if (count == 0) return;

        for (int i = count - 1; i >= 0; i--)
        {
            TryReleaseGemOreItem(activeItemsList[i]);
        }

        activeItemsList.Clear();
        activeItemsForUpdate.Clear();

        if (enableCulling && cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(0);
        }
    }

    public void ReturnToPool(GemOreItem _item)
    {
        TryReleaseGemOreItem(_item);
    }

    // ── 아우라 풀 (LogItemController의 같은 구조) ──

    private void BuildAuraPools()
    {
        if (auraPools != null) return;

        auraPools = new Dictionary<GemOreType, IObjectPool<ItemAuraEffectController>>();
        if (gemOreAuraDatas == null) return;

        for (int i = 0; i < gemOreAuraDatas.Count; i++)
        {
            GemOreAuraData data = gemOreAuraDatas[i];
            if (data.auraPrefab == null || auraPools.ContainsKey(data.gemOreType)) continue;

            // 루프 변수를 그대로 캡처하면 모든 풀이 마지막 프리팹을 쓰게 되므로 지역 변수로 고정한다.
            ItemAuraEffectController prefab = data.auraPrefab;

            auraPools.Add(data.gemOreType, new ObjectPool<ItemAuraEffectController>(
                createFunc: () => Instantiate(prefab, transform),
                actionOnGet: OnGetAura,
                actionOnRelease: OnReleaseAura,
                actionOnDestroy: OnDestroyAura,
                collectionCheck: true,
                defaultCapacity: auraPoolDefaultCapacity,
                maxSize: auraPoolMaxSize
            ));
        }
    }

    public ItemAuraEffectController GetAura(GemOreType _gemOreType)
    {
        if (auraPools != null && auraPools.TryGetValue(_gemOreType, out IObjectPool<ItemAuraEffectController> pool))
        {
            return pool.Get();
        }
        return null;
    }

    public void ReleaseAura(GemOreType _gemOreType, ItemAuraEffectController _aura)
    {
        if (_aura == null) return;

        if (auraPools != null && auraPools.TryGetValue(_gemOreType, out IObjectPool<ItemAuraEffectController> pool))
        {
            pool.Release(_aura);
            return;
        }

        // 풀을 못 찾으면(설정 변경 등) 고아로 남기지 않도록 파기한다.
        Destroy(_aura.gameObject);
    }

    private void OnGetAura(ItemAuraEffectController _aura)
    {
        _aura.gameObject.SetActive(true);
    }

    private void OnReleaseAura(ItemAuraEffectController _aura)
    {
        // 원석에 붙어 있던 것을 떼어내 컨트롤러 아래로 되돌린다. 원석이 비활성화돼도 아우라가 함께 사라지지 않게 한다.
        _aura.transform.SetParent(transform, false);
        _aura.transform.localPosition = Vector3.zero;
        _aura.gameObject.SetActive(false);
    }

    private void OnDestroyAura(ItemAuraEffectController _aura)
    {
        if (_aura != null) Destroy(_aura.gameObject);
    }
}
