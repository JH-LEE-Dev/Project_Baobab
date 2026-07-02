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
    [SerializeField] private float spawnRadius = 10f; // 캐릭터 스폰 위치 기준 반경

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
        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        
        if (walkablePositions == null || walkablePositions.Count == 0) return;

        // 반경 내의 후보지 찾기
        List<Vector3> safeWalkablePositions = new List<Vector3>();
        float radiusSq = spawnRadius * spawnRadius;

        for (int i = 0; i < walkablePositions.Count; i++)
        {
            Vector3 pos = walkablePositions[i];
            if ((pos - playerPos).sqrMagnitude <= radiusSq)
            {
                safeWalkablePositions.Add(pos);
            }
        }

        int spawnLimit = Mathf.Min(maxNPCs, safeWalkablePositions.Count);

        // 부분 셔플
        for (int i = 0; i < spawnLimit; i++)
        {
            int rnd = UnityEngine.Random.Range(i, safeWalkablePositions.Count);
            Vector3 temp = safeWalkablePositions[rnd];
            safeWalkablePositions[rnd] = safeWalkablePositions[i];
            safeWalkablePositions[i] = temp;

            SpawnNPCAt(safeWalkablePositions[i]);
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
            environmentProvider.tilemapDataProvider, 
            environmentProvider.pathfindGridProvider, 
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
        UpdateNPCVisibility(_npc, false);
        allSpawnedNPCs.Remove(_npc);
        
        if (_npc.gameObject.activeSelf)
        {
            _npc.gameObject.SetActive(false);
        }
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
