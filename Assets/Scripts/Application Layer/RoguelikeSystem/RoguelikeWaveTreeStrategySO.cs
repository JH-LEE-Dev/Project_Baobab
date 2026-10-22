using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 웨이브용 나무 생성 전략. 기획서 3-1 "필드와 웨이브"(v2.5 초기 나무 · 묶음 스폰 · 생성 위치 · 필드 가장자리 포함).
///
///   - 필드: 플레이어 시작 지점을 중심으로 한 타원 섬(TileMapGenerator 타원 섬) 안의 잔디 칸에만 심는다.
///     섬 가장자리(물 · 용암 칸에서 edgeInsetCells칸 안)는 빼 둔다 - 맵 끝 나무가 HUD 밖으로 나가 보이지 않게.
///   - 등장: 묘목 단계를 건너뛰고 다 자란 나무로 심은 뒤, 본편의 "다 자랄 때" 연출(성장 스케일 + 플래시)을 바로 재생한다.
///   - 초기 나무: 웨이브 전투가 시작될 때 현재 임계치의 절반이 필드 곳곳에 솟아난 채로 시작한다(목표 처치 수에 포함).
///   - 스폰: 웨이브가 진행 중일 때만, 묶음 주기마다 초당 스폰 × 주기만큼 한꺼번에(소수는 다음 묶음으로 이월).
///     스폰 총량에는 제한이 없다 - 목표 처치 수와 상관없이 웨이브가 끝날 때까지 계속 심는다(처치가 늦으면 임계치를 넘는다).
///   - 상한 도달 후: 묶음이 스폰 상한(T + 추가 개수)을 넘으면 상한까지만 심고 넘치는 몫은 버린다. 죽은 자리는 다음 묶음 때 채운다.
///   - 생성 위치: 필드 곳곳에 분산(TreeSpawnRules). 다른 나무와 최소 간격을 두고, 플레이어 바로 옆에는 심지 않는다.
///
/// 빈칸 목록은 이 전략이 직접 들고 있고, 본편 매니저의 후보 목록(availablePositions)은 비워 둔다 - 간격 규칙으로 고른 칸에 직접 심어야 해서
/// 매니저의 무작위 후보 뽑기(SpawnOneTreeFromAvailable)는 쓰지 않는다.
///
/// 처치 수 집계는 TreeKilledEvent를 받은 RoguelikeSystem이 한다. 이 전략은 "어디에 언제 심는가"만 맡는다.
/// InDungeonObjectManager.SetExternalTreeGenerationStrategy로 주입되며, 런타임 사본(Instantiate)을 쓰므로
/// 에셋 원본의 값은 바뀌지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "RoguelikeWaveTreeStrategy", menuName = "Baobab/Roguelike/Wave Tree Generation Strategy")]
public class RoguelikeWaveTreeStrategySO : TreeGenerationStrategySO
{
    /// <summary>(죽은 나무, 위치) - 웨이브 진행 여부와 무관하게 모든 사망을 알린다.</summary>
    public event Action<TreeObj, Vector3> TreeKilledEvent;

    [Tooltip("true면 심는 순간 성장 연출(묘목 → 나무로 커지는 스케일 + 플래시)을 재생한다. 묘목 단계는 항상 건너뛴다.")]
    [SerializeField] private bool bPlayGrowUpOnSpawn = true;

    private readonly List<Vector3> fieldPositions = new List<Vector3>(1024);
    // 필드 칸을 모을 때 같은 칸(잔디 목록과 지연 개방 목록에 함께 든 칸)이 두 번 들어가지 않게 거른다.
    private readonly HashSet<Vector3Int> fieldCells = new HashSet<Vector3Int>();
    // 지금 나무가 없는 필드 칸. 심으면 빠지고 나무가 죽으면 돌아온다.
    private readonly List<Vector3> freePositions = new List<Vector3>(1024);
    // 지금 서 있는 나무 + 이번 묶음에 심은 나무 위치. 최소 간격 판정에 쓴다.
    private readonly List<Vector3> treePositionScratch = new List<Vector3>(64);
    // 이번 스폰에서 심지 못한 칸(잠깐 점유된 칸). 스폰이 끝나면 빈칸으로 되돌린다. 바위 장식 칸은 필드에서 미리 뺀다.
    private readonly List<Vector3> blockedScratch = new List<Vector3>(16);
    private readonly List<int> pickScratch = new List<int>(256);
    private readonly System.Random rng = new System.Random();

    private Vector3 fieldCenter;
    private float fieldRadiusX = 8f;
    private float fieldRadiusY = 4.5f;
    private int edgeInsetCells;

    private bool bSpawning;
    private float spawnInterval = 0.5f;
    private int spawnCap = 60;
    // 묶음 주기 · 최소 간격 · 플레이어 금지 반경. RoguelikeSystem의 밸런스 에셋을 그대로 읽는다.
    private RoguelikeBalanceSO balance;
    // 다음 묶음까지 남은 시간과, 묶음 크기의 소수 이월분.
    private float batchTimer;
    private float batchCarry;
    // 웨이브가 정한 정수 묶음(기획서 v2.6). 0이면 옛 방식(초당 스폰 × 묶음 주기, 소수 이월).
    private int waveBatchSize;
    private float waveBatchSeconds;
    private Transform playerTransform;

    public int FieldPositionCount => fieldPositions.Count;

    /// <summary>
    /// 다음 ReadyTrees에서 쓸 필드 범위(타원 반지름, 월드)와 가장자리에서 뺄 칸 수(_edgeInsetCells, 0이면 빼지 않음).
    /// 맵을 새로 만들 때마다 먼저 부른다.
    /// </summary>
    public void ConfigureField(Vector3 _center, float _radiusX, float _radiusY, int _edgeInsetCells)
    {
        fieldCenter = _center;
        fieldRadiusX = Mathf.Max(1f, _radiusX);
        fieldRadiusY = Mathf.Max(1f, _radiusY);
        edgeInsetCells = Mathf.Max(0, _edgeInsetCells);
    }

    /// <summary>
    /// 웨이브 시작. 빈칸을 필드 전체로 되돌리고, 초기 나무 _initialTrees그루를 필드 곳곳에 한꺼번에 심은 뒤 묶음 스폰을 켠다.
    /// 스폰 총량에는 제한이 없다: 웨이브가 끝날 때까지(StopSpawning) 스폰 상한까지 계속 심는다(기획서 v2.5 3-1). 묶음 주기 · 생성 위치 수치는 _balance에서 읽는다.
    /// _player는 플레이어 바로 옆 금지 판정에 쓴다(없으면 필드 중심 기준).
    /// </summary>
    public void BeginWave(InDungeonObjectManager _manager, float _spawnInterval, int _spawnCap, int _initialTrees,
        RoguelikeBalanceSO _balance, Transform _player, int _batchSize = 0, float _batchSeconds = 0f)
    {
        spawnInterval = Mathf.Max(0.01f, _spawnInterval);
        spawnCap = Mathf.Max(1, _spawnCap);
        balance = _balance;
        playerTransform = _player;
        waveBatchSize = Mathf.Max(0, _batchSize);
        waveBatchSeconds = Mathf.Max(0f, _batchSeconds);
        batchTimer = BatchSeconds;
        batchCarry = 0f;

        ResetSpawnPositions(_manager);

        // 초기 나무도 한 묶음처럼 필드 곳곳에 흩어 심는다(기획서 v2.5 3-1).
        SpawnBatch(_manager, Mathf.Max(0, _initialTrees));

        bSpawning = true;
    }

    /// <summary>스폰 주기만 바꾼다(웨이브 도중 카드 효과가 바뀌는 경우는 없지만 디버그 조정용).</summary>
    public void SetSpawnInterval(float _spawnInterval)
    {
        spawnInterval = Mathf.Max(0.01f, _spawnInterval);
    }

    /// <summary>웨이브 종료. 이 순간부터 한 그루도 심지 않는다.</summary>
    public void StopSpawning()
    {
        bSpawning = false;
        batchTimer = 0f;
        batchCarry = 0f;
    }

    /// <summary>_position에서 가장 가까운 나무까지의 거리(월드, 등각 보정). 나무가 없으면 float.MaxValue. 허탕 이동 측정용.</summary>
    public float NearestTreeDistance(InDungeonObjectManager _manager, Vector3 _position)
    {
        CollectTreePositions(_manager);
        if (TreeSpawnRules.NearestIndex(treePositionScratch, _position, out float distanceSq) < 0) return float.MaxValue;
        return Mathf.Sqrt(distanceSq);
    }

    public override IEnumerator SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions)
    {
        fieldPositions.Clear();
        fieldCells.Clear();
        ITilemapDataProvider tiles = _manager.EnvironmentProvider?.tilemapDataProvider;

        // 잔디 칸은 이미 섬 안에만 있다. 섬과 같은 타원으로 한 번 더 걸러 둔다(섬 설정이 꺼져 있을 때의 안전장치).
        float invRx2 = 1f / (fieldRadiusX * fieldRadiusX);
        float invRy2 = 1f / (fieldRadiusY * fieldRadiusY);
        AddFieldPositions(_grassTilePositions, invRx2, invRy2, tiles);

        // 시작점·포탈 옆 칸은 본편에서 "입장 5초 뒤 개방"으로 따로 빠져 있다(TileMapGenerator delayedGrassPositions).
        // 로그라이크 웨이브는 처음부터 후보에 넣는다. 매니저의 지연 개방 예약은
        // RoguelikeSystem이 준비 직후 취소한다 - 남겨 두면 웨이브마다 그 칸이 열리는 시점이 들쭉날쭉해진다.
        AddFieldPositions(tiles?.GetDelayedGrassTileWorldPositions(), invRx2, invRy2, tiles);

        RemoveEdgePositions(tiles);

        if (0 == fieldPositions.Count)
        {
            Debug.LogError($"[RoguelikeWaveTreeStrategySO] 필드({fieldRadiusX} × {fieldRadiusY}) 안에 잔디 칸이 하나도 없습니다. 나무가 생기지 않습니다. " +
                "섬 크기 또는 TileMapGenerator 안전구역 반경을 확인하십시오.");
        }

        ResetSpawnPositions(_manager);
        StopSpawning();

        yield break;
    }

    public override IEnumerator GrowthRoutine(InDungeonObjectManager _manager)
    {
        while (true)
        {
            if (false == bSpawning)
            {
                yield return null;
                continue;
            }

            // 묶음 주기마다 웨이브가 정한 정수 묶음(v2.6)을, 묶음이 없는 옛 데이터는 초당 스폰 × 주기를 한꺼번에 심는다. 상한을 넘는 몫은 SpawnBatch가 버린다.
            batchTimer -= Time.deltaTime;
            if (batchTimer <= 0f)
            {
                batchTimer = Mathf.Max(0f, batchTimer + BatchSeconds);
                SpawnBatch(_manager, 0 < waveBatchSize ? waveBatchSize : TreeSpawnRules.TakeBatch(ref batchCarry, spawnInterval, BatchSeconds));
            }

            yield return null;
        }
    }

    public override void OnTreeDead(InDungeonObjectManager _manager, TreeObj _treeObj, Vector3 _deadPos)
    {
        // 빈칸으로 돌려놓는다. 상한에 막혀 있었어도 바로 채우지 않고 다음 묶음 때 채운다(기획서 v2.5 "상한 도달 후").
        freePositions.Add(_deadPos);

        TreeKilledEvent?.Invoke(_treeObj, _deadPos);
    }

    // // 내부

    private float BatchSeconds => 0 < waveBatchSize && 0f < waveBatchSeconds ? Mathf.Max(0.1f, waveBatchSeconds) : null != balance ? Mathf.Max(0.1f, balance.spawnBatchSeconds) : 3f;
    private float MinSpacing => null != balance ? balance.treeMinSpacing : 0f;
    private float PlayerExclusionRadius => null != balance ? balance.playerExclusionRadius : 0f;

    private Vector3 PlayerPosition => null != playerTransform ? playerTransform.position : fieldCenter;

    /// <summary>
    /// _count그루를 필드 곳곳에 같은 프레임에 심는다(한 묶음이 "팍" 솟아난다). 스폰 상한에 닿으면 거기서 멈추고 남은 몫은 버린다.
    /// </summary>
    private void SpawnBatch(InDungeonObjectManager _manager, int _count)
    {
        int room = Mathf.Min(_count, spawnCap - _manager.ActiveTrees.Count);
        if (room <= 0) return;

        // 간격 판정용 위치 목록. 이번 묶음에 심은 나무는 SpawnOne이 뒤에 더한다.
        CollectTreePositions(_manager);

        for (int i = 0; i < room; i++)
        {
            if (false == SpawnOne(_manager)) break;
        }
    }

    /// <summary>
    /// 분산 규칙으로 고른 빈칸에 다 자란 나무 한 그루를 심고 성장 연출을 바로 재생한다. 묘목 상태를 거치지 않으므로 심는 즉시 벨 수 있다.
    /// 지금 심을 수 없는 칸(바위 장식·잠깐 점유된 칸)은 이번 스폰에서만 건너뛰고, 스폰이 끝나면 빈칸으로 되돌린다.
    /// 심은 위치는 간격 판정 목록에 더해 같은 묶음의 다음 나무도 간격을 지키게 한다.
    /// </summary>
    private bool SpawnOne(InDungeonObjectManager _manager)
    {
        Vector3 player = PlayerPosition;
        bool bSpawned = false;
        blockedScratch.Clear();

        while (freePositions.Count > 0)
        {
            int index = TreeSpawnRules.PickScatteredIndex(freePositions, treePositionScratch, MinSpacing, player, PlayerExclusionRadius, rng, pickScratch);
            if (index < 0) break;

            Vector3 pos = freePositions[index];
            RemoveFreeAt(index);

            TreeObj tree = _manager.SpawnTreeAt(pos, false);
            if (null == tree)
            {
                blockedScratch.Add(pos);
                continue;
            }

            treePositionScratch.Add(tree.transform.position);
            if (bPlayGrowUpOnSpawn) tree.PlayGrowUpForRun();
            bSpawned = true;
            break;
        }

        freePositions.AddRange(blockedScratch);
        blockedScratch.Clear();
        return bSpawned;
    }

    private void CollectTreePositions(InDungeonObjectManager _manager)
    {
        treePositionScratch.Clear();
        IReadOnlyList<TreeObj> trees = _manager.ActiveTrees;
        for (int i = 0; i < trees.Count; i++)
        {
            TreeObj tree = trees[i];
            if (null == tree || tree.bDead || false == tree.bCanApplyDamage) continue;
            treePositionScratch.Add(tree.transform.position);
        }
    }

    private void RemoveFreeAt(int _index)
    {
        int last = freePositions.Count - 1;
        freePositions[_index] = freePositions[last];
        freePositions.RemoveAt(last);
    }

    /// <summary>
    /// 빈칸을 필드 전체로 되돌린다. 웨이브 사이 나무는 이월되지 않고 모두 정리된 상태라 필드 전체가 비어 있다.
    /// 본편 매니저의 후보 목록은 비워 둔다(이 전략이 직접 심는다).
    /// </summary>
    private void ResetSpawnPositions(InDungeonObjectManager _manager)
    {
        _manager.ClearAvailablePositions();
        freePositions.Clear();
        freePositions.AddRange(fieldPositions);
    }

    /// <summary>
    /// 섬 타원 안의 칸을 필드에 더한다. 이미 들어간 칸과 바위 장식 칸(나무를 심을 수 없는 영구 장애물)은 뺀다 -
    /// 남겨 두면 스폰할 때마다 같은 칸을 다시 시도하고, 나무가 죽어 칸이 돌아올 때 중복이 늘어난다.
    /// </summary>
    private void AddFieldPositions(List<Vector3> _positions, float _invRx2, float _invRy2, ITilemapDataProvider _tiles)
    {
        if (null == _positions) return;

        for (int i = 0; i < _positions.Count; i++)
        {
            Vector3 pos = _positions[i];
            float dx = pos.x - fieldCenter.x;
            float dy = pos.y - fieldCenter.y;
            if (dx * dx * _invRx2 + dy * dy * _invRy2 > 1f) continue;

            if (null != _tiles)
            {
                Vector3Int cell = _tiles.WorldToCell(pos);
                if (_tiles.HasRockDeco(cell) || false == fieldCells.Add(cell)) continue;
            }

            fieldPositions.Add(pos);
        }
    }

    /// <summary>
    /// 섬 가장자리 칸을 필드에서 뺀다(기획서 v2.5 3-1 "필드": 맵 끝에 나무가 나오면 HUD 밖으로 빠져나가 보인다).
    /// 8방향으로 edgeInsetCells칸 안에 물(4티어는 용암) 칸이 있으면 가장자리로 본다.
    /// </summary>
    private void RemoveEdgePositions(ITilemapDataProvider _tiles)
    {
        if (null == _tiles || edgeInsetCells <= 0) return;

        int write = 0;
        for (int i = 0; i < fieldPositions.Count; i++)
        {
            Vector3 pos = fieldPositions[i];
            if (IsNearWater(_tiles, _tiles.WorldToCell(pos), edgeInsetCells)) continue;
            fieldPositions[write++] = pos;
        }

        fieldPositions.RemoveRange(write, fieldPositions.Count - write);
    }

    private static bool IsNearWater(ITilemapDataProvider _tiles, Vector3Int _cell, int _range)
    {
        for (int dy = -_range; dy <= _range; dy++)
        {
            for (int dx = -_range; dx <= _range; dx++)
            {
                if (0 == dx && 0 == dy) continue;
                if (_tiles.IsWaterTile(new Vector3Int(_cell.x + dx, _cell.y + dy, _cell.z))) return true;
            }
        }

        return false;
    }
}
