using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InDungeonUnitSpawner : MonoBehaviour
{
    // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    // 내부 의존성
    [Header("Spawn Settings")]
    [SerializeField] private LumberjackNPC npcPrefab;
    [SerializeField] private int maxNPCs = 3;

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

    // 퍼블릭 메서드
    public void Initialize(IEnvironmentProvider _environmentProvider, IPathfindTreeProvider _pathfindTreeProvider)
    {
        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;
        pathfindTreeProvider = _pathfindTreeProvider;

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
        
        // NPC 초기화 (환경 데이터 및 길찾기 그리드 제공)
        npc.Initialize(
            environmentProvider, 
            pathfindTreeProvider
        );

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
        _npc.ReleaseTargetTree(); // 타겟 나무 예약을 풀어 다른 NPC가 다시 타겟팅할 수 있도록 함
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

    private void OnDestroy()
    {
        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }
}
