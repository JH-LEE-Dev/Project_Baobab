using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 웨이브용 나무 생성 전략. 기획서 3-1 "필드와 웨이브"(v2.3 초기 나무 · 생성 위치 포함).
///
///   - 필드: 플레이어 시작 지점을 중심으로 한 타원 섬(TileMapGenerator 타원 섬) 안의 잔디 칸에만 심는다.
///   - 등장: 묘목 단계를 건너뛰고 다 자란 나무로 심은 뒤, 본편의 "다 자랄 때" 연출(성장 스케일 + 플래시)을 바로 재생한다.
///   - 초기 나무: 웨이브 전투가 시작될 때 초기 N그루가 솟아난 채로 시작한다(목표 처치 수에 포함). 플레이어 시작 위치에서 가까운 군집에 둔다.
///   - 스폰: 웨이브가 진행 중일 때만, spawnInterval마다 1그루. 스폰 상한(T + 추가 개수)에 닿으면 멈춘다.
///     웨이브 동안 심는 총량은 목표 처치 수(초기 N 포함)까지다 - 이후 스폰은 "목표 − N"그루.
///   - 상한 도달 후: 한 그루가 죽으면 다음 프레임에 바로 1그루를 다시 심는다(주기를 기다리지 않는다).
///   - 생성 위치: 군집 스폰(ClusterSpawnRules). 군집 중심은 웨이브마다(그리고 clusterMoveInterval마다) 옮기고, 플레이어 바로 옆에는 심지 않으며,
///     가장 가까운 나무가 R보다 멀면 그 나무 쪽 군집에 우선 심는다.
///
/// 빈칸 목록은 이 전략이 직접 들고 있고, 본편 매니저의 후보 목록(availablePositions)은 비워 둔다 - 군집 위치에 직접 심어야 해서
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
    private readonly List<Vector3> treePositionScratch = new List<Vector3>(64);
    // 이번 스폰에서 심지 못한 칸(잠깐 점유된 칸). 스폰이 끝나면 빈칸으로 되돌린다. 바위 장식 칸은 필드에서 미리 뺀다.
    private readonly List<Vector3> blockedScratch = new List<Vector3>(16);
    private readonly List<int> pickScratch = new List<int>(256);
    private readonly System.Random rng = new System.Random();

    private Vector3 fieldCenter;
    private float fieldRadiusX = 8f;
    private float fieldRadiusY = 4.5f;

    private bool bSpawning;
    private float spawnInterval = 0.5f;
    private int spawnCap = 60;
    // 이 웨이브에 심을 수 있는 총량(초기 나무 포함). 0 이하면 제한 없음.
    private int spawnBudget;
    // 생성 위치 수치(초기 나무 · 군집 · 허탕 방지 · 플레이어 금지 반경). RoguelikeSystem의 밸런스 에셋을 그대로 읽는다.
    private RoguelikeBalanceSO balance;
    private int spawnedThisWave;
    private float spawnAccumulator;
    private int pendingImmediateSpawns;
    private Vector3 clusterCenter;
    private float nextClusterMoveTime;
    private Transform playerTransform;

    public int FieldPositionCount => fieldPositions.Count;

    /// <summary>다음 ReadyTrees에서 쓸 필드 범위(타원 반지름, 월드). 맵을 새로 만들 때마다 먼저 부른다.</summary>
    public void ConfigureField(Vector3 _center, float _radiusX, float _radiusY)
    {
        fieldCenter = _center;
        fieldRadiusX = Mathf.Max(1f, _radiusX);
        fieldRadiusY = Mathf.Max(1f, _radiusY);
    }

    /// <summary>
    /// 웨이브 시작. 빈칸을 필드 전체로 되돌리고, 플레이어 근처에 군집 중심을 잡아 초기 나무 _initialTrees그루를 한꺼번에 심은 뒤 스폰을 켠다.
    /// _spawnBudget은 이 웨이브에 심는 총량(초기 나무 포함, 0 이하면 제한 없음). 생성 위치 수치는 _balance에서 읽는다.
    /// _player는 플레이어 바로 옆 금지·허탕 방지 판정에 쓴다(없으면 필드 중심 기준).
    /// </summary>
    public void BeginWave(InDungeonObjectManager _manager, float _spawnInterval, int _spawnCap, int _initialTrees, int _spawnBudget,
        RoguelikeBalanceSO _balance, Transform _player)
    {
        spawnInterval = Mathf.Max(0.01f, _spawnInterval);
        spawnCap = Mathf.Max(1, _spawnCap);
        spawnBudget = _spawnBudget;
        balance = _balance;
        playerTransform = _player;
        spawnedThisWave = 0;
        spawnAccumulator = 0f;
        pendingImmediateSpawns = 0;

        ResetSpawnPositions(_manager);

        // 초기 나무는 플레이어 시작 위치에서 이동 거리가 짧은 곳의 군집에 둔다(기획서 v2.3 3-1).
        clusterCenter = PickClusterCenter(PlayerPosition, InitialClusterMaxDistance);
        nextClusterMoveTime = ClusterMoveInterval > 0f ? Time.time + ClusterMoveInterval : float.MaxValue;

        int initial = Mathf.Min(Mathf.Max(0, _initialTrees), spawnCap);
        for (int i = 0; i < initial; i++)
        {
            if (false == SpawnOne(_manager)) break;
        }

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
        spawnAccumulator = 0f;
        pendingImmediateSpawns = 0;
    }

    /// <summary>_position에서 가장 가까운 나무까지의 거리(월드, 등각 보정). 나무가 없으면 float.MaxValue. 허탕 이동 측정용.</summary>
    public float NearestTreeDistance(InDungeonObjectManager _manager, Vector3 _position)
    {
        CollectTreePositions(_manager);
        if (ClusterSpawnRules.NearestIndex(treePositionScratch, _position, out float distanceSq) < 0) return float.MaxValue;
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

            // 군집 중심을 일정 시간마다 필드 안 다른 곳으로 옮긴다(기획서 v2.3 "웨이브마다(또는 일정 시간마다)").
            if (Time.time >= nextClusterMoveTime)
            {
                clusterCenter = PickClusterCenter(fieldCenter, float.MaxValue);
                nextClusterMoveTime = Time.time + ClusterMoveInterval;
            }

            int alive = _manager.ActiveTrees.Count;
            int cap = spawnCap;

            // 상한에서 한 그루가 죽은 자리는 주기를 기다리지 않고 채운다.
            while (pendingImmediateSpawns > 0 && alive < cap && HasBudget)
            {
                pendingImmediateSpawns--;
                if (false == SpawnOne(_manager)) break;
                alive++;
            }
            pendingImmediateSpawns = 0;

            spawnAccumulator += Time.deltaTime;
            while (spawnAccumulator >= spawnInterval && alive < cap && HasBudget)
            {
                spawnAccumulator -= spawnInterval;
                if (false == SpawnOne(_manager))
                {
                    spawnAccumulator = 0f;
                    break;
                }

                alive++;
            }

            // 상한·총량에 막혀 있는 동안 주기를 쌓아 두면, 자리가 나는 순간 한꺼번에 몰려 심긴다. 한 주기분만 남긴다.
            if ((alive >= cap || false == HasBudget) && spawnAccumulator > spawnInterval) spawnAccumulator = spawnInterval;

            yield return null;
        }
    }

    public override void OnTreeDead(InDungeonObjectManager _manager, TreeObj _treeObj, Vector3 _deadPos)
    {
        // 빈칸으로 돌려놓는다. 다음 스폰이 군집 규칙으로 다시 고른다.
        freePositions.Add(_deadPos);

        // 이 콜백은 죽은 나무가 아직 활성 목록에 있을 때 불린다. "죽기 전 수 >= 상한"이면 상한에 막혀 있던 상태다.
        if (bSpawning && _manager.ActiveTrees.Count >= spawnCap)
        {
            pendingImmediateSpawns++;
        }

        TreeKilledEvent?.Invoke(_treeObj, _deadPos);
    }

    // // 내부

    private bool HasBudget => spawnBudget <= 0 || spawnedThisWave < spawnBudget;

    private float ClusterRadius => null != balance ? balance.clusterRadius : 2.5f;
    private float ClusterMoveInterval => null != balance ? balance.clusterMoveInterval : 0f;
    private float GapDistance => null != balance ? balance.gapDistance : 0f;
    private float PlayerExclusionRadius => null != balance ? balance.playerExclusionRadius : 0f;
    private float InitialClusterMaxDistance => null != balance ? balance.initialClusterMaxDistance : 4f;

    private Vector3 PlayerPosition => null != playerTransform ? playerTransform.position : fieldCenter;

    /// <summary>
    /// 군집 규칙으로 고른 빈칸에 다 자란 나무 한 그루를 심고 성장 연출을 바로 재생한다. 묘목 상태를 거치지 않으므로 심는 즉시 벨 수 있다.
    /// 지금 심을 수 없는 칸(바위 장식·잠깐 점유된 칸)은 이번 스폰에서만 건너뛰고, 스폰이 끝나면 빈칸으로 되돌린다.
    /// </summary>
    private bool SpawnOne(InDungeonObjectManager _manager)
    {
        Vector3 player = PlayerPosition;
        Vector3 anchor = clusterCenter;

        // 허탕 방지: 가장 가까운 나무가 R보다 멀면 그 나무 쪽 군집에 우선 심는다.
        CollectTreePositions(_manager);
        if (ClusterSpawnRules.TryGetGapAnchor(treePositionScratch, player, GapDistance, out Vector3 gapAnchor)) anchor = gapAnchor;

        bool bSpawned = false;
        blockedScratch.Clear();

        while (freePositions.Count > 0)
        {
            int index = ClusterSpawnRules.PickIndex(freePositions, anchor, ClusterRadius, player, PlayerExclusionRadius, rng, pickScratch);
            if (index < 0) break;

            Vector3 pos = freePositions[index];
            RemoveFreeAt(index);

            TreeObj tree = _manager.SpawnTreeAt(pos, false);
            if (null == tree)
            {
                blockedScratch.Add(pos);
                continue;
            }

            spawnedThisWave++;
            if (bPlayGrowUpOnSpawn) tree.PlayGrowUpForRun();
            bSpawned = true;
            break;
        }

        freePositions.AddRange(blockedScratch);
        blockedScratch.Clear();
        return bSpawned;
    }

    /// <summary>군집 중심을 고른다. _origin에서 _maxDistance 안의 빈칸 중 하나(플레이어 바로 옆 제외). 없으면 필드 아무 빈칸.</summary>
    private Vector3 PickClusterCenter(Vector3 _origin, float _maxDistance)
    {
        if (0 == freePositions.Count) return fieldCenter;

        float radius = float.MaxValue == _maxDistance ? Mathf.Max(fieldRadiusX, fieldRadiusY) * 2f : Mathf.Max(0.5f, _maxDistance);
        int index = ClusterSpawnRules.PickIndex(freePositions, _origin, radius, PlayerPosition, PlayerExclusionRadius + ClusterRadius * 0.5f, rng, pickScratch);
        return index >= 0 ? freePositions[index] : fieldCenter;
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
}
