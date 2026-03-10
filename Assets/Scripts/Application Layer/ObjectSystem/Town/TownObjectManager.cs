using System;
using UnityEngine;

public class TownObjectManager : MonoBehaviour
{
    //이벤트
    public event Action<PortalType> PortalActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성
    [Header("Portal")]
    [SerializeField] private PortalObj portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;

    //내부 의존성
    private PortalObj portal;
    private TreeObj[] trees;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    public void ReadyObj()
    {
        portal = Instantiate(portalPrefab);
        portal.transform.position = portalSpawnPoint.position;

        portal.Initialize(PortalType.ToDungeonPortal);

        trees = FindObjectsByType<TreeObj>(FindObjectsSortMode.None);

        for (int i = 0; i < trees.Length; i++)
        {
            // 혹시 모를 런타임 파괴를 대비한 null 체크
            if (trees[i] != null)
            {
                trees[i].Initialize(environmentProvider, new TreeData(TreeType.BirchTree, TreeGrade.Normal,TreeState.Idle));
            }
        }

        BindEvents();
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

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void PortalActivated(PortalType _type)
    {
        PortalActivatedEvent?.Invoke(_type);
    }
}
