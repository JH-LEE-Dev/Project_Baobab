using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 웨이브용 나무 생성 전략. 기획서 3-1 "필드와 웨이브".
///
///   - 필드: 플레이어 시작 지점을 중심으로 한 타원 섬(TileMapGenerator 타원 섬) 안의 잔디 칸에만 심는다.
///   - 등장: 묘목 단계를 건너뛰고 다 자란 나무로 심은 뒤, 본편의 "다 자랄 때" 연출(성장 스케일 + 플래시)을 바로 재생한다.
///   - 스폰: 웨이브가 진행 중일 때만, spawnInterval마다 1그루. 스폰 상한(T + 추가 개수)에 닿으면 멈춘다.
///   - 상한 도달 후: 한 그루가 죽으면 다음 프레임에 바로 1그루를 다시 심는다(주기를 기다리지 않는다).
///   - 웨이브 시작 시 필드는 비어 있다(초기 스폰 없음).
///
/// 처치 수 집계와 골드 드랍은 TreeKilledEvent를 받은 RoguelikeSystem이 한다. 이 전략은 "어디에 언제 심는가"만 맡는다.
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

    private Vector3 fieldCenter;
    private float fieldRadiusX = 8f;
    private float fieldRadiusY = 4.5f;

    private bool bSpawning;
    private float spawnInterval = 0.5f;
    private int spawnCap = 60;
    private float spawnAccumulator;
    private int pendingImmediateSpawns;

    public int FieldPositionCount => fieldPositions.Count;

    /// <summary>다음 ReadyTrees에서 쓸 필드 범위(타원 반지름, 월드). 맵을 새로 만들 때마다 먼저 부른다.</summary>
    public void ConfigureField(Vector3 _center, float _radiusX, float _radiusY)
    {
        fieldCenter = _center;
        fieldRadiusX = Mathf.Max(1f, _radiusX);
        fieldRadiusY = Mathf.Max(1f, _radiusY);
    }

    /// <summary>웨이브 시작. 스폰 위치를 필드 전체로 되돌리고 스폰을 켠다.</summary>
    public void BeginWave(InDungeonObjectManager _manager, float _spawnInterval, int _spawnCap)
    {
        spawnInterval = Mathf.Max(0.01f, _spawnInterval);
        spawnCap = Mathf.Max(1, _spawnCap);
        spawnAccumulator = 0f;
        pendingImmediateSpawns = 0;

        ResetSpawnPositions(_manager);
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

    public override IEnumerator SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions)
    {
        fieldPositions.Clear();

        // 잔디 칸은 이미 섬 안에만 있다. 섬과 같은 타원으로 한 번 더 걸러 둔다(섬 설정이 꺼져 있을 때의 안전장치).
        float invRx2 = 1f / (fieldRadiusX * fieldRadiusX);
        float invRy2 = 1f / (fieldRadiusY * fieldRadiusY);
        AddFieldPositions(_grassTilePositions, invRx2, invRy2);

        // 시작점·포탈 옆 칸은 본편에서 "입장 5초 뒤 개방"으로 따로 빠져 있다(TileMapGenerator delayedGrassPositions).
        // 로그라이크 웨이브는 빈 필드에서 시작하므로 처음부터 후보에 넣는다. 매니저의 지연 개방 예약은
        // RoguelikeSystem이 준비 직후 취소한다 - 남겨 두면 웨이브마다 그 칸이 열리는 시점이 들쭉날쭉해진다.
        AddFieldPositions(_manager.EnvironmentProvider?.tilemapDataProvider?.GetDelayedGrassTileWorldPositions(), invRx2, invRy2);

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

            int alive = _manager.ActiveTrees.Count;

            // 상한에서 한 그루가 죽은 자리는 주기를 기다리지 않고 채운다.
            while (pendingImmediateSpawns > 0 && alive < spawnCap)
            {
                pendingImmediateSpawns--;
                if (false == SpawnOne(_manager)) break;
                alive++;
            }
            pendingImmediateSpawns = 0;

            spawnAccumulator += Time.deltaTime;
            while (spawnAccumulator >= spawnInterval && alive < spawnCap)
            {
                spawnAccumulator -= spawnInterval;
                if (false == SpawnOne(_manager))
                {
                    spawnAccumulator = 0f;
                    break;
                }

                alive++;
            }

            // 상한에 막혀 있는 동안 주기를 쌓아 두면, 자리가 나는 순간 한꺼번에 몰려 심긴다. 한 주기분만 남긴다.
            if (alive >= spawnCap && spawnAccumulator > spawnInterval) spawnAccumulator = spawnInterval;

            yield return null;
        }
    }

    public override void OnTreeDead(InDungeonObjectManager _manager, TreeObj _treeObj, Vector3 _deadPos)
    {
        // 후보 순서는 상관없다. SpawnOneTreeFromAvailable이 매번 무작위 시작점에서 고른다.
        _manager.AddAvailablePosition(_deadPos);

        // 이 콜백은 죽은 나무가 아직 활성 목록에 있을 때 불린다. "죽기 전 수 >= 상한"이면 상한에 막혀 있던 상태다.
        if (bSpawning && _manager.ActiveTrees.Count >= spawnCap)
        {
            pendingImmediateSpawns++;
        }

        TreeKilledEvent?.Invoke(_treeObj, _deadPos);
    }

    /// <summary>
    /// 다 자란 나무로 한 그루를 심고 성장 연출을 바로 재생한다. 묘목 상태를 거치지 않으므로 심는 즉시 벨 수 있다.
    /// SpawnTreeAt은 새 나무를 활성 목록 맨 뒤에 넣으므로 그 나무가 방금 심은 나무다.
    /// </summary>
    private bool SpawnOne(InDungeonObjectManager _manager)
    {
        if (false == _manager.SpawnOneTreeFromAvailable(false)) return false;

        if (bPlayGrowUpOnSpawn)
        {
            IReadOnlyList<TreeObj> trees = _manager.ActiveTrees;
            if (trees.Count > 0) trees[trees.Count - 1].PlayGrowUpForRun();
        }

        return true;
    }

    private void ResetSpawnPositions(InDungeonObjectManager _manager)
    {
        _manager.ClearAvailablePositions();
        for (int i = 0; i < fieldPositions.Count; i++)
        {
            _manager.AddAvailablePosition(fieldPositions[i]);
        }
    }

    private void AddFieldPositions(List<Vector3> _positions, float _invRx2, float _invRy2)
    {
        if (null == _positions) return;

        for (int i = 0; i < _positions.Count; i++)
        {
            Vector3 pos = _positions[i];
            float dx = pos.x - fieldCenter.x;
            float dy = pos.y - fieldCenter.y;
            if (dx * dx * _invRx2 + dy * dy * _invRy2 <= 1f) fieldPositions.Add(pos);
        }
    }
}
