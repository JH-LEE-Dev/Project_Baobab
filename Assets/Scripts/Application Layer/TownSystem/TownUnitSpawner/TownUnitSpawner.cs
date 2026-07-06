using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마을(Town)에서 OffroadContainer -> LogContainer로 로그를 옮겨주는 운반 NPC(OffroadPorterNPC)를
/// 관리한다. InDungeonUnitSpawner(럼버잭 NPC)와 달리 던전 재입장마다 반복 스폰/해제되는 것이 아니라,
/// 마을 최초 진입 시 한 번만 집 주변에 배치된다.
/// </summary>
public class TownUnitSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private OffroadPorterNPC npcPrefab;
    [SerializeField] private int npcCount = 3;
    [Tooltip("캐릭터 스폰 위치 기준, 한 명씩 이 방향(월드 좌표 벡터)으로 한 칸씩 더 떨어진 위치에 배치된다.")]
    [SerializeField] private Vector2 diagonalWorldStep = new Vector2(0.6f, -0.35f);

    private IEnvironmentProvider environmentProvider;
    private TownTilemapDataProvider tilemapDataProvider;

    private OffroadContainer offroadContainer;
    private LogContainer logContainer;

    private readonly List<OffroadPorterNPC> spawnedNPCs = new List<OffroadPorterNPC>(8);
    private readonly List<Vector3> spawnPositions = new List<Vector3>(8);
    public IReadOnlyList<OffroadPorterNPC> NPCs => spawnedNPCs;

    private bool bSpawned = false;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    /// <summary>
    /// 마을 그리드/오프로드 차량/컨테이너가 전부 준비된 뒤(TownSystem.StartTownSystem) 호출한다.
    /// 이미 스폰된 적이 있다면 NPC는 다시 만들지 않고, 길찾기 그리드와 차량 발밑 콜라이더 등록만 최신화한다.
    /// </summary>
    public void SpawnNPCsIfNeeded(TownTileManager _townTileManager, OffroadVehicleObj _offroadVehicle,
        OffroadContainer _offroadContainer, LogContainer _logContainer, Transform _houseAnchor, Transform _startPointAnchor)
    {
        offroadContainer = _offroadContainer;
        logContainer = _logContainer;

        if (tilemapDataProvider == null)
        {
            tilemapDataProvider = new TownTilemapDataProvider(_townTileManager);
        }
        else
        {
            tilemapDataProvider.RefreshBounds();
        }

        // 오프로드 차량 발밑 ColliderTilemap도 길찾기 이동 불가 타일로 등록
        if (_offroadVehicle != null)
        {
            tilemapDataProvider.RegisterExtraColliderTilemap(_offroadVehicle.FootprintColliderTilemap);
        }

        if (bSpawned) return;

        if (npcPrefab == null)
        {
            Debug.LogError("[TownUnitSpawner] npcPrefab이 지정되지 않았습니다.");
            return;
        }

        bSpawned = true;
        SpawnNPCsAlongDiagonal(_houseAnchor, _startPointAnchor);
    }

    /// <summary>
    /// 캐릭터가 처음 스폰되는 위치를 기준으로, diagonalWorldStep 방향으로 한 칸씩 더 떨어진 위치에
    /// NPC를 한 명씩 배치한다. (1번째 NPC는 1칸, 2번째는 2칸, ...)
    /// </summary>
    private void SpawnNPCsAlongDiagonal(Transform _houseAnchor, Transform _startPointAnchor)
    {
        Vector3 characterSpawnPos = _startPointAnchor != null ? _startPointAnchor.position
            : (_houseAnchor != null ? _houseAnchor.position : transform.position);

        for (int i = 0; i < npcCount; i++)
        {
            int step = i + 1;
            Vector3 spawnPos = characterSpawnPos + (Vector3)(diagonalWorldStep * step);
            SpawnNPCAt(spawnPos);
        }
    }

    private void SpawnNPCAt(Vector3 _pos)
    {
        OffroadPorterNPC npc = Instantiate(npcPrefab, _pos, Quaternion.identity, transform);
        npc.Initialize(tilemapDataProvider, environmentProvider, offroadContainer, logContainer);
        spawnedNPCs.Add(npc);
        spawnPositions.Add(_pos);
    }

    /// <summary>
    /// 카메라가 하늘로 줌인되어 던전으로 넘어가는 연출(CameraUpIsEnd) 중에는 운반 NPC들이
    /// 계속 돌아다니지 않도록 멈춘다.
    /// </summary>
    public void PauseAllNPCs()
    {
        for (int i = 0; i < spawnedNPCs.Count; i++)
        {
            if (spawnedNPCs[i] != null) spawnedNPCs[i].PauseNPC();
        }
    }

    /// <summary>
    /// 던전에서 돌아와 카메라가 마을로 복귀하는 연출(CameraDownIsEnd)이 끝나면, NPC들을 원래
    /// 생성 위치로 되돌리고 스폰 직후와 동일하게 initialMoveDelay만큼 대기한 뒤 다시 움직이게 한다.
    /// </summary>
    public void ResetAllNPCsToSpawn()
    {
        for (int i = 0; i < spawnedNPCs.Count; i++)
        {
            if (spawnedNPCs[i] != null) spawnedNPCs[i].ResetToSpawnPosition(spawnPositions[i]);
        }
    }
}
