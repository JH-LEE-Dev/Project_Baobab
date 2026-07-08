using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InDungeonUnitSpawner : MonoBehaviour, IInDungeonUnitSpawnerCH
{
    // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    // 내부 의존성
    [Header("Spawn Settings")]
    [SerializeField] private LumberjackNPC npcPrefab;
    [SerializeField] private int maxNPCs = 3;

    // 모든 럼버잭 NPC가 공용으로 참조하는 스탯. 여기 값을 바꾸면 스폰된 NPC 전체에 동일하게 적용된다.
    [SerializeField] private LumberjackStatComponent statComponent;

    // 셰이크웨이브 생성을 담당하는 공용 인스턴스. 인스펙터 연결을 위해 구체 타입 필드로 두지만,
    // 실제로 NPC에 넘길 때나 내부적으로 다룰 때는 IShockWaveCreator 인터페이스로만 참조한다.
    [SerializeField] private AxeExtraAttackCreator sharedShockWaveCreator;
    private IShockWaveCreator shockWaveCreator => sharedShockWaveCreator;

    // 캐릭터의 StatComponent를 셰이크웨이브 스탯 전용 인터페이스로 좁혀서 들고 있는다.
    // InDungeonUnitSpawner.Initialize() 시점엔 아직 캐릭터가 스폰되기 전이라, 캐릭터 스폰 이후
    // SetPlayerStatForShockWave()로 뒤늦게 주입받는다.
    private ICharacterStatForNPC playerStatForShockWave;

    // 부메랑 생성을 담당하는 공용 인스턴스. BoomerangCreator는 데미지/범위/공격속도/치명타를 전부
    // 자신에게 주입된 StatComponent에서 직접 읽으므로(Character의 BoomerangCreator와 동일한 구조),
    // 여기에도 캐릭터의 StatComponent를 그대로 주입해두면 럼버잭이 던지는 부메랑도 캐릭터와 완전히
    // 동일한 스탯을 갖는다.
    [SerializeField] private BoomerangCreator sharedBoomerangCreator;
    private IBoomerangCreator boomerangCreator => sharedBoomerangCreator;

    // 부메랑 발사 주기/사거리/개수 판단(boomerangCount, boomerangCooldown, boomerangMajorAxisRatio)에는
    // BoomerangCreator를 거치지 않고 럼버잭이 직접 읽어야 하는 값도 있어서, ShockWave와 달리 인터페이스로
    // 좁히지 않고 캐릭터의 StatComponent 실체를 그대로 들고 있는다(BoomerangCreator 자체도 동일한 방식).
    private StatComponent playerStatForBoomerang;

    private IObjectPool<LumberjackNPC> npcPool;

    private List<LumberjackNPC> allSpawnedNPCs = new List<LumberjackNPC>(16); // 마스터 리스트 (컬링 그룹용)
    public IReadOnlyList<LumberjackNPC> NPCs => allSpawnedNPCs;

    private List<LumberjackNPC> activeNPCs = new List<LumberjackNPC>(16); // 업데이트 및 가시성 리스트
    public IReadOnlyList<LumberjackNPC> ActiveNPCs => activeNPCs;

    [Header("Optimization")]
    [SerializeField] private bool useCulling = false; // 컬링 사용 여부
    [SerializeField] private float cullingDistance = 25f;
    [SerializeField] private float cullingUpdateInterval = 0.1f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;

    // 풀 설정 변수
    [SerializeField] private bool collectionCheck = false; // 에디터 성능을 위해 false로 설정
    [SerializeField] private int defaultCapacity = 10;
    [SerializeField] private int maxSize = 50;

    private IPathfindTreeProvider pathfindTreeProvider;
    private OffroadContainer offroadContainer;

    [Header("Debug")]
    [Tooltip("럼버잭 NPC 멈춤 버그 추적용 [LJDebug] 로그를 켜고 끕니다. 재현할 때만 켜두세요.")]
    [SerializeField] private bool enableLJDebugLog = false;

    private void OnValidate()
    {
        LJDebugLog.Enabled = enableLJDebugLog;
    }

    // 퍼블릭 메서드
    public void Initialize(IEnvironmentProvider _environmentProvider, IPathfindTreeProvider _pathfindTreeProvider, OffroadContainer _offroadContainer = null)
    {
        LJDebugLog.Enabled = enableLJDebugLog; // OnValidate는 에디터 전용이라, 빌드에서도 적용되도록 여기서 동기화

        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;
        pathfindTreeProvider = _pathfindTreeProvider;
        offroadContainer = _offroadContainer;

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[maxSize];
        onCullingStateChangedDelegate = OnCullingStateChanged;

        if (npcPrefab != null)
        {
            npcPool = new ObjectPool<LumberjackNPC>(
                () => Instantiate(npcPrefab, transform),
                OnGetNPC,
                OnReleaseNPC,
                OnDestroyNPC,
                collectionCheck,
                defaultCapacity,
                maxSize
            );
        }
    }

    public void SpawnNPC() // 호환성을 위해 이름 유지 (필요 시 상위 구조에서 SpawnNPCs로 호출 변경)
    {
        SpawnNPCs();
    }

    public void SpawnNPCs()
    {
        if (tilemapDataProvider == null || npcPrefab == null)
        {
            return;
        }

        if (useCulling)
        {
            SetupCullingGroup();
        }

        Vector3 playerPos = tilemapDataProvider.GetPlayerSpawnPosition();
        Vector3Int playerCellPos = tilemapDataProvider.WorldToCell(playerPos);

        // 캐릭터 두 칸 아래 셀의 월드 좌표를 기준점으로 사용
        Vector3Int centerCell = new Vector3Int(playerCellPos.x, playerCellPos.y - 2, 0);
        Vector3 centerWorldPos = tilemapDataProvider.CellToWorld(centerCell);

        // 타일맵이 아이소메트릭 등으로 기울어져 있어도 화면상 완전한 수평 배치가 되도록,
        // 인접 셀 간 월드 X 간격만 구해서 순수 X축으로만 좌우 대칭 배치한다.
        float spacingX = tilemapDataProvider.CellToWorld(centerCell + new Vector3Int(1, 0, 0)).x - centerWorldPos.x;
        float startOffsetX = -(maxNPCs - 1) * 0.5f * spacingX;

        for (int i = 0; i < maxNPCs; i++)
        {
            Vector3 spawnWorldPos = centerWorldPos + new Vector3(startOffsetX + i * spacingX, 0f, 0f);

            SpawnNPCAt(spawnWorldPos);
        }

        if (useCulling)
        {
            RefreshCullingGroup();
        }
        else
        {
            // 컬링을 안 쓸 경우 전부 활성화
            foreach (var npc in allSpawnedNPCs)
            {
                UpdateNPCVisibility(npc, true);
            }
        }
    }

    private void SetupCullingGroup()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = onCullingStateChangedDelegate;
        }

        if (Camera.main != null)
        {
            cullingGroup.targetCamera = Camera.main;
            cullingGroup.SetDistanceReferencePoint(Camera.main.transform);
        }
        cullingGroup.SetBoundingDistances(cullingDistances);
        cullingGroup.SetBoundingSpheres(spheres);
    }

    public void RefreshCullingGroup()
    {
        if (cullingGroup == null || !useCulling) return;

        int count = allSpawnedNPCs.Count;
        if (spheres == null || spheres.Length < count)
        {
            spheres = new BoundingSphere[Mathf.Max(count + 10, maxSize)];
        }

        for (int i = 0; i < count; i++)
        {
            spheres[i].position = allSpawnedNPCs[i].transform.position;
            spheres[i].radius = 3f;
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(count);

        activeNPCs.Clear();
        for (int i = 0; i < count; i++)
        {
            bool isVisible = cullingGroup.IsVisible(i);
            bool isNear = cullingGroup.GetDistance(i) == 0;
            bool shouldBeActive = isVisible && isNear;

            UpdateNPCVisibility(allSpawnedNPCs[i], shouldBeActive);
        }
    }

    private void UpdateCullingSpheres()
    {
        if (!useCulling) return;
        int count = allSpawnedNPCs.Count;
        for (int i = 0; i < count; i++)
        {
            spheres[i].position = allSpawnedNPCs[i].transform.position;
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (!useCulling || _ev.index >= allSpawnedNPCs.Count) return;

        bool shouldBeActive = _ev.isVisible && (_ev.currentDistance == 0);
        UpdateNPCVisibility(allSpawnedNPCs[_ev.index], shouldBeActive);
    }

    private void UpdateNPCVisibility(LumberjackNPC _npc, bool _shouldBeActive)
    {
        if (_npc == null) return;

        if (_npc.gameObject.activeSelf != _shouldBeActive)
        {
            _npc.gameObject.SetActive(_shouldBeActive);
        }

        bool isActiveInList = activeNPCs.Contains(_npc);

        if (_shouldBeActive && !isActiveInList)
        {
            activeNPCs.Add(_npc);
        }
        else if (!_shouldBeActive && isActiveInList)
        {
            activeNPCs.Remove(_npc);
        }
    }

    private void Update()
    {
        if (useCulling && cullingGroup != null && allSpawnedNPCs.Count > 0)
        {
            cullingUpdateTimer += Time.deltaTime;
            if (cullingUpdateTimer >= cullingUpdateInterval)
            {
                UpdateCullingSpheres();
                cullingUpdateTimer = 0f;
            }
        }
    }

    private void SpawnNPCAt(Vector3 _pos)
    {
        if (npcPool == null) return;

        LumberjackNPC npc = npcPool.Get();
        npc.transform.position = _pos;
        npc.gameObject.SetActive(true);

        // NPC 초기화 (환경 데이터, 길찾기 그리드, 로그 납품용 오프로드 컨테이너, 공용 스탯 제공)
        npc.Initialize(
            environmentProvider,
            pathfindTreeProvider,
            offroadContainer,
            statComponent
        );
        npc.SetShockWaveDependencies(shockWaveCreator, playerStatForShockWave);
        npc.SetBoomerangDependencies(boomerangCreator, playerStatForBoomerang);

        allSpawnedNPCs.Add(npc);
        int index = allSpawnedNPCs.Count - 1;

        if (useCulling)
        {
            if (spheres.Length <= index)
            {
                Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, index + 1));
                if (cullingGroup != null) cullingGroup.SetBoundingSpheres(spheres);
            }
            spheres[index] = new BoundingSphere(_pos, 3f);

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(allSpawnedNPCs.Count);
                bool shouldBeActive = cullingGroup.IsVisible(index) && (cullingGroup.GetDistance(index) == 0);
                UpdateNPCVisibility(npc, shouldBeActive);
            }
        }
        else
        {
            UpdateNPCVisibility(npc, true);
        }
    }

    public void ReleaseNPC(LumberjackNPC _npc)
    {
        if (npcPool != null)
        {
            npcPool.Release(_npc);
        }
        else
        {
            UpdateNPCVisibility(_npc, false);
            allSpawnedNPCs.Remove(_npc);
            Destroy(_npc.gameObject);
        }
    }

    public void ReleaseAllNPC() // 기존 이름 호환성 유지
    {
        ReleaseAllNPCs();
    }

    public void ReleaseAllNPCs()
    {
        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }

        if (allSpawnedNPCs == null) return;

        this.gameObject.SetActive(false);

        for (int i = allSpawnedNPCs.Count - 1; i >= 0; i--)
        {
            LumberjackNPC npc = allSpawnedNPCs[i];
            if (npc != null)
            {
                ReleaseNPC(npc);
            }
        }

        allSpawnedNPCs.Clear();
        activeNPCs.Clear();

        this.gameObject.SetActive(true);
    }

    private void OnGetNPC(LumberjackNPC _npc)
    {
        // Get 시 필요한 리셋 로직
    }

    private void OnReleaseNPC(LumberjackNPC _npc)
    {
        // NPC는 던전에서만 유효하므로, 마을로 돌아가는 시점에 인벤토리/타겟나무/경로/방향을 즉시 전부 초기화한다.
        // (다음 던전에서 Initialize() 시 다시 한 번 초기화되지만, 여기서도 즉시 정리해 풀에 머무는 동안
        // 이전 생애의 상태가 하나도 남아있지 않도록 보장한다)
        _npc.ResetToCleanState();
        UpdateNPCVisibility(_npc, false); // 여기서 SetActive(false)까지 처리됨
        allSpawnedNPCs.Remove(_npc);
    }

    private void OnDestroyNPC(LumberjackNPC _npc)
    {
        if (_npc != null && _npc.gameObject != null)
        {
            Destroy(_npc.gameObject);
        }
    }

    public void PauseAllNPC()
    {
        for (int i = 0; i < allSpawnedNPCs.Count; i++)
        {
            if (allSpawnedNPCs[i] != null)
                allSpawnedNPCs[i].PauseNPC();
        }
    }

    public void ResumeAllNPC()
    {
        for (int i = 0; i < allSpawnedNPCs.Count; i++)
        {
            if (allSpawnedNPCs[i] != null)
                allSpawnedNPCs[i].ResumeNPC();
        }
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

    public void SetLumberjackNPCCount(float _amount)
    {
        maxNPCs = (int)_amount;
    }

    /// <summary>
    /// 캐릭터가 스폰된 뒤(InDungeonUnitSpawner.Initialize() 시점엔 아직 캐릭터가 없으므로) 호출해서
    /// 럼버잭 NPC들이 셰이크웨이브에 사용할 캐릭터 스탯을 넘겨준다. 이미 스폰된 NPC들에게도 즉시 반영된다.
    /// </summary>
    public void SetPlayerStatForShockWave(ICharacterStatForNPC _playerStat)
    {
        playerStatForShockWave = _playerStat;

        if (sharedShockWaveCreator != null)
        {
            sharedShockWaveCreator.Initialize(_playerStat);
        }

        for (int i = 0; i < allSpawnedNPCs.Count; i++)
        {
            allSpawnedNPCs[i]?.SetShockWaveDependencies(shockWaveCreator, playerStatForShockWave);
        }
    }

    public void IncreaseAttackSpeed(float _amount)
    {
        statComponent.IncreaseAttackSpeed(_amount);
    }

    public void IncreaseDamage(float _amount)
    {
        statComponent.IncreaseDamage(_amount);
    }

    public void IncreaseSpeed(float _amount)
    {
        statComponent.IncreaseSpeed(_amount);
    }

    public void SetShockWaveEnable(bool _boolean)
    {
        statComponent.SetShockWaveEnabled(_boolean);
    }

    public void SetBoomerangEnable(bool _boolean)
    {
        statComponent.SetBoomerangEnabled(_boolean);
    }

    /// <summary>
    /// 캐릭터가 스폰된 뒤(InDungeonUnitSpawner.Initialize() 시점엔 아직 캐릭터가 없으므로) 호출해서
    /// 럼버잭 NPC들이 부메랑에 사용할 캐릭터 StatComponent를 넘겨준다. SetPlayerStatForShockWave와
    /// 동일한 시점/방식으로 호출된다. 이미 스폰된 NPC들에게도 즉시 반영된다.
    /// </summary>
    public void SetPlayerStatForBoomerang(StatComponent _playerStat)
    {
        playerStatForBoomerang = _playerStat;

        if (sharedBoomerangCreator != null)
        {
            sharedBoomerangCreator.Initialize(_playerStat);
        }

        for (int i = 0; i < allSpawnedNPCs.Count; i++)
        {
            allSpawnedNPCs[i]?.SetBoomerangDependencies(boomerangCreator, playerStatForBoomerang);
        }
    }

    public void SetOffroadPorterNPCCount(float _amount)
    {
        throw new NotImplementedException();
    }
}
