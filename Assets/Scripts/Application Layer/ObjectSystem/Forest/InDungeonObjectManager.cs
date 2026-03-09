using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System;

public class InDungeonObjectManager : MonoBehaviour
{
    //이벤트
    public event Action<PortalType> PortalActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;
    private Character character;

    //내부 의존성
    [Header("Tree Settings")]
    [SerializeField] private TreeObj treePrefab;
    [SerializeField, Range(0.01f, 0.5f)] private float treeDensity = 0.1f; // 나무 스폰 밀도 (0.1 = 약 10% 확률)

    [Header("Portal")]
    [SerializeField] private PortalObj portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;
    private PortalObj portal;
    private List<Vector3> grassTileWorldPositions;

    //내부 의존성 (풀링)
    private IObjectPool<TreeObj> treePool;
    private List<TreeObj> activeTrees = new List<TreeObj>(100);

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        // 오브젝트 풀 초기화 (최대 사이즈 확대)
        treePool = new ObjectPool<TreeObj>(
            createFunc: OnCreateTree,
            actionOnGet: OnGetTree,
            actionOnRelease: OnReleaseTree,
            actionOnDestroy: OnDestroyTree,
            collectionCheck: true,
            defaultCapacity: 50,
            maxSize: 500
        );
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
    }

    public void SpawnTree()
    {
        if (grassTileWorldPositions == null || grassTileWorldPositions.Count == 0) return;

        // 기존 나무 정리
        ClearTrees();

        // 시드 기반 랜덤 사용 (필요 시 seed 값 사용 가능)
        for (int i = 0; i < grassTileWorldPositions.Count; i++)
        {
            // 설정된 밀도 확률에 따라 스폰 결정
            if (UnityEngine.Random.value <= treeDensity)
            {
                TreeObj tree = treePool.Get();

                // 이미 TileMapGenerator에서 grid.GetCellCenterWorld()로 처리된 좌표임
                tree.transform.position = grassTileWorldPositions[i];
                activeTrees.Add(tree);
            }
        }
    }

    public void ReadyTrees(List<Vector3> _grassTileWorldPositions)
    {
        grassTileWorldPositions = _grassTileWorldPositions;

        SpawnTree();
    }

    public void ReadyPortalAndCharacter()
    {
        // 포탈이 없으면 생성, 있으면 위치만 업데이트
        if (portal == null)
        {
            portal = Instantiate(portalPrefab);
            portal.Initialize(PortalType.ToTownPortal);
        }

        // 맵이 재생성될 때마다 새로운 스폰 위치로 강제 이동
        portal.transform.position = environmentProvider.tilemapDataProvider.GetPortalSpawnPosition();

        if (character != null)
        {
            // 캐릭터도 새로운 시작 지점으로 이동
            character.transform.position = environmentProvider.tilemapDataProvider.GetPlayerSpawnPosition();
        }

        BindEvents();
    }

    private void ClearTrees()
    {
        for (int i = 0; i < activeTrees.Count; i++)
        {
            if (activeTrees[i] != null)
            {
                treePool.Release(activeTrees[i]);
            }
        }
        activeTrees.Clear();
    }

    private TreeObj OnCreateTree()
    {
        TreeObj tree = Instantiate(treePrefab);
        tree.Initialize(environmentProvider);

        return tree;
    }

    private void OnGetTree(TreeObj _tree)
    {
        _tree.gameObject.SetActive(true);
    }

    private void OnReleaseTree(TreeObj _tree)
    {
        _tree.gameObject.SetActive(false);
    }

    private void OnDestroyTree(TreeObj _tree)
    {
        Destroy(_tree.gameObject);
    }

    private void OnDestroy()
    {
        // 씬 파괴 시 풀링된 객체들 정리
        ClearTrees();

        ReleaseEvents();
    }

    private void BindEvents()
    {
        if (portal == null)
            return;

        portal.PortalActivated -= PortalActivated;
        portal.PortalActivated += PortalActivated;
    }

    private void ReleaseEvents()
    {
        if (portal != null)
            portal.PortalActivated -= PortalActivated;
    }

    private void PortalActivated(PortalType _type)
    {
        PortalActivatedEvent?.Invoke(_type);
    }
}
