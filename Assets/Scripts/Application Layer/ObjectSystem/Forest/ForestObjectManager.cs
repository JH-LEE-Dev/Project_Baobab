using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public class ForestObjectManager : MonoBehaviour
{
    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성
    [Header("Tree")]
    [SerializeField] private Tree treePrefab;
    [SerializeField] private Transform treeSpawnCenterPoint;

    //내부 의존성 (풀링)
    private IObjectPool<Tree> treePool;
    private List<Tree> activeTrees = new List<Tree>(10);

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        // 오브젝트 풀 초기화
        treePool = new ObjectPool<Tree>(
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
        float spawnRange = 5f;

        for (int i = 0; i < 10; i++)
        {
            Tree tree = treePool.Get();
            
            // 랜덤 위치 설정 (treeSpawnCenterPoint 주변)
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRange, spawnRange),
                Random.Range(-spawnRange, spawnRange),
                0f
            );
            tree.transform.position = treeSpawnCenterPoint.position + randomOffset;
            
            activeTrees.Add(tree);
        }
    }

    public void ReadyObj()
    {
        SpawnTree();
    }

    private Tree OnCreateTree()
    {
        Tree tree = Instantiate(treePrefab, transform);
        tree.Initialize(environmentProvider);
        return tree;
    }

    private void OnGetTree(Tree _tree)
    {
        _tree.gameObject.SetActive(true);
    }

    private void OnReleaseTree(Tree _tree)
    {
        _tree.gameObject.SetActive(false);
    }

    private void OnDestroyTree(Tree _tree)
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
    }
}
