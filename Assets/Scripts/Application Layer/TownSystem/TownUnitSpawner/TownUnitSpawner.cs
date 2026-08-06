using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마을(Town)에서 OffroadContainer -> LogContainer로 로그를 옮겨주는 운반 NPC(OffroadPorterNPC)를
/// 관리한다. InDungeonUnitSpawner(럼버잭 NPC)와 달리 던전 재입장마다 반복 스폰/해제되는 것이 아니라,
/// 마을 최초 진입 시 한 번만 집 주변에 배치된다.
/// </summary>
public class TownUnitSpawner : MonoBehaviour, ITownUnitSpawnerCH
{
    [Header("Spawn Settings")]
    [SerializeField] private OffroadPorterNPC npcPrefab;
    // 포터는 스킬로만 얻는다. 스킬 레벨이 오를 때마다 SetOffroadPorterNPCCount()로 1명씩 늘어나며
    // (최대 인원은 스킬 노드의 amountCurve가 결정한다), 아무것도 찍지 않은 초기 상태는 0명이다.
    [SerializeField] private int npcCount = 0;
    [Tooltip("캐릭터 스폰 위치 기준, 한 명씩 이 방향(월드 좌표 벡터)으로 한 칸씩 더 떨어진 위치에 배치된다.")]
    [SerializeField] private Vector2 diagonalWorldStep = new Vector2(0.6f, -0.35f);

    // 모든 오프로드 포터 NPC가 공용으로 참조하는 스탯. 여기 값을 바꾸면 스폰된 NPC 전체에 동일하게 적용된다.
    [SerializeField] private OffroadPorterStatComponent statComponent;

    private IEnvironmentProvider environmentProvider;
    private TownTilemapDataProvider tilemapDataProvider;

    // TownSystem.StartTownSystem()이 이 인스턴스를 character.SetTilemapDataProvider()로 직접 넘겨줘서
    // 캐릭터도 던전 전용 TileMapGenerator 대신 Town의 실제 타일맵을 조회하도록 한다.
    public ITilemapDataProvider TilemapDataProvider => tilemapDataProvider;

    private OffroadContainer offroadContainer;
    private LogContainer logContainer;

    private readonly List<OffroadPorterNPC> spawnedNPCs = new List<OffroadPorterNPC>(8);
    private readonly List<Vector3> spawnPositions = new List<Vector3>(8);
    public IReadOnlyList<OffroadPorterNPC> NPCs => spawnedNPCs;

    private bool bSpawned = false;

    // 지금 마을에서 포터들이 활동해야 하는 상태인지(던전에 들어가 있는 동안엔 false). 스킬로 포터가
    // 중간에 늘어났을 때 새로 만든 NPC의 활성 상태를 현재 맥락에 맞추기 위해 추적한다.
    private bool bNPCsActive = true;

    // 포터를 추가 배치할 때 기준이 되는 위치(캐릭터 스폰 지점). 스킬로 인원이 늘어나는 시점엔
    // 앵커를 넘겨받을 수 없으므로 SpawnNPCsIfNeeded에서 캐싱해 둔다.
    private Vector3 spawnBasePos;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    /// <summary>
    /// 마을 그리드/오프로드 차량/컨테이너가 전부 준비된 뒤(TownSystem.StartTownSystem) 호출한다.
    /// 이미 스폰된 NPC는 다시 만들지 않고, 길찾기 그리드와 차량 발밑 콜라이더 등록을 최신화한 뒤
    /// 목표 인원(npcCount)에 모자란 만큼만 추가로 배치한다.
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

        if (npcPrefab == null)
        {
            Debug.LogError("[TownUnitSpawner] npcPrefab이 지정되지 않았습니다.");
            return;
        }

        spawnBasePos = _startPointAnchor != null ? _startPointAnchor.position
            : (_houseAnchor != null ? _houseAnchor.position : transform.position);

        bSpawned = true;

        // 마을에 들어올 때마다 호출되지만 SyncNPCCount()가 "모자란 만큼만" 추가하므로 중복 생성되지 않는다.
        SyncNPCCount();
    }

    /// <summary>
    /// 현재 배치된 포터 수를 목표 인원(npcCount)에 맞춘다. 모자라면 기존 배치 규칙(캐릭터 스폰 지점에서
    /// diagonalWorldStep 방향으로 한 칸씩 더 떨어진 자리)을 이어서 새로 만들고, 목표를 넘는 인원은 끈다.
    ///
    /// 스킬로 인원이 바뀌는 시점(SetOffroadPorterNPCCount)에도 호출되므로 마을에 들어와 있는 도중에
    /// 늘어나도 즉시 반영된다. 최초 스폰 전(bSpawned == false)에는 값만 저장해 두고, 마을 진입 시
    /// SpawnNPCsIfNeeded가 반영한다.
    /// </summary>
    private void SyncNPCCount()
    {
        if (!bSpawned || npcPrefab == null) return;

        int target = Mathf.Max(0, npcCount);

        // 1. 모자란 인원을 새로 배치한다. Initialize()가 스폰 딜레이와 Idle 상태까지 세팅하므로
        //    새로 만든 NPC는 이것만으로 바로 정상 동작한다.
        while (spawnedNPCs.Count < target)
        {
            int step = spawnedNPCs.Count + 1;
            SpawnNPCAt(spawnBasePos + (Vector3)(diagonalWorldStep * step));
        }

        // 2. 활성 상태를 목표 인원에 맞춘다. 인원이 줄어들 때 파괴하지 않고 비활성화만 하는 이유는,
        //    (a) 인출 코루틴은 OffroadContainer 쪽에서 돌기 때문에 여기서 NPC를 파괴하면 그 코루틴이
        //        파괴된 인벤토리를 참조해 MissingReferenceException이 나고,
        //    (b) 포터가 들고 있던 원목이 세이브 정산(AppendTransitToSaveData)에서 통째로 사라지기 때문이다.
        //    꺼둔 채로 남겨두면 나중에 인원이 다시 늘어날 때 그대로 재사용된다.
        for (int i = 0; i < spawnedNPCs.Count; i++)
        {
            OffroadPorterNPC npc = spawnedNPCs[i];
            if (npc == null) continue;

            bool bShouldBeActive = i < target && bNPCsActive;
            if (bShouldBeActive == npc.gameObject.activeSelf) continue;

            if (bShouldBeActive)
            {
                npc.ResetToSpawnPosition(spawnPositions[i]);
            }
            else
            {
                // 진행 중이던 인출/납품을 먼저 정리해야 컨테이너 쪽 코루틴이 꺼진 NPC에게 계속
                // 아이템을 보내지 않는다.
                npc.CancelCurrentTaskForTeleport();
                npc.Deactivate();
            }
        }
    }

    private void SpawnNPCAt(Vector3 _pos)
    {
        OffroadPorterNPC npc = Instantiate(npcPrefab, _pos, Quaternion.identity, transform);
        npc.Initialize(tilemapDataProvider, environmentProvider, offroadContainer, logContainer, statComponent);
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
    /// 던전으로 카메라가 완전히 올라간 뒤(CameraUpIsEnd) 호출한다. 이 NPC들은 DontDestroyOnLoad
    /// 계층에 있어 던전 씬으로 넘어가도 파괴되지 않으므로, GameObject 자체를 꺼서 던전을 도는 동안
    /// 마을에 멈춰있던 위치에 그대로 남아 렌더링/갱신되는 일이 없도록 한다. 다시 켜는 건
    /// ResetAllNPCsToSpawn()(ResetToSpawnPosition)에서 처리한다.
    /// </summary>
    public void DeactivateAllNPCs()
    {
        bNPCsActive = false;

        for (int i = 0; i < spawnedNPCs.Count; i++)
        {
            if (spawnedNPCs[i] != null) spawnedNPCs[i].Deactivate();
        }
    }

    /// <summary>
    /// 던전에서 돌아와 카메라가 마을로 복귀하는 연출(CameraDownIsEnd)이 끝나면, NPC들을 원래
    /// 생성 위치로 되돌리고 스폰 직후와 동일하게 initialMoveDelay만큼 대기한 뒤 다시 움직이게 한다.
    /// </summary>
    public void ResetAllNPCsToSpawn()
    {
        bNPCsActive = true;

        // 던전을 도는 사이에 스킬로 인원이 늘었을 수 있으므로 여기서 한 번 맞춘다.
        SyncNPCCount();

        int activeCount = Mathf.Min(spawnedNPCs.Count, Mathf.Max(0, npcCount));
        for (int i = 0; i < activeCount; i++)
        {
            if (spawnedNPCs[i] != null) spawnedNPCs[i].ResetToSpawnPosition(spawnPositions[i]);
        }
    }

    /// <summary>
    /// 텔레포트 UI가 닫히는 시점에 호출된다(Pause 로직과는 별개). 지금 하던 일을 각자 알맞게
    /// 중단시킨다(OffroadPorterNPC.CancelCurrentTaskForTeleport 참고).
    /// </summary>
    public void CancelActiveTasksForTeleport()
    {
        for (int i = 0; i < spawnedNPCs.Count; i++)
        {
            spawnedNPCs[i]?.CancelCurrentTaskForTeleport();
        }
    }

    public void SetOffroadPorterNPCCount(float _amount)
    {
        npcCount = Mathf.Max(0, (int)_amount);

        // 마을에 들어와 있는 도중에 스킬로 인원이 바뀌어도 즉시 반영한다. (예전에는 값만 저장해서,
        // 최초 마을 진입 이후에 습득한 포터 수 증가 스킬이 아무 효과도 내지 못했다.)
        SyncNPCCount();
    }

    public void IncreaseOffroadPorterNPCSpeed(float _amount)
    {
        statComponent.IncreaseSpeed(_amount);
    }

    public void IncreaseOffroadPorterNPCSlotCapacity(float _amount)
    {
        statComponent.IncreaseSlotCapacity((int)_amount);
    }

    public void IncreaseOffroadPorterNPCJackpotChance(float _amount)
    {
        statComponent.IncreaseJackpotChance(_amount);
    }
}
