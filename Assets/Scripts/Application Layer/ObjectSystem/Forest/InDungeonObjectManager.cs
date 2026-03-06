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

    //내부 의존성
    [Header("Tree")]
    [SerializeField] private TreeObj treePrefab;

    [Header("Portal")]
    [SerializeField] private PortalObj portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;
    private PortalObj portal;

    //내부 의존성 (풀링)
    private IObjectPool<TreeObj> treePool;
    private List<TreeObj> activeTrees = new List<TreeObj>(10);

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        // 오브젝트 풀 초기화
        treePool = new ObjectPool<TreeObj>(
            createFunc: OnCreateTree,
            actionOnGet: OnGetTree,
            actionOnRelease: OnReleaseTree,
            actionOnDestroy: OnDestroyTree,
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: 20
        );
    }

    public void SpawnTree()
    {

    }

    public void ReadyObj()
    {
        SpawnTree();

        portal = Instantiate(portalPrefab);
        portal.transform.position = portalSpawnPoint.position;

        portal.Initialize(PortalType.ToTownPortal);

        BindEvents();
    }

    private TreeObj OnCreateTree()
    {
        TreeObj tree = Instantiate(treePrefab, transform);
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
        if (activeTrees != null)
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
