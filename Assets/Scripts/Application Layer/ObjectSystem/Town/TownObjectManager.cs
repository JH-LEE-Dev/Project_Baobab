using System;
using System.Collections.Generic;
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

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 25; // 거리 컬링 기준
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;

    //내부 의존성
    private PortalObj portal;
    private TreeObj[] trees;
    private List<TreeObj> activeTreesForUpdate = new List<TreeObj>();

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    public void ReadyObj()
    {
        portal = Instantiate(portalPrefab);
        portal.transform.position = portalSpawnPoint.position;
        portal.Initialize(PortalType.ToDungeonPortal);

        // 씬 내의 모든 나무(비활성 포함)를 찾아 관리 대상으로 등록
        trees = FindObjectsByType<TreeObj>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (trees != null && trees.Length > 0)
        {
            SetupCullingGroup();

            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] != null)
                {
                    trees[i].Initialize(environmentProvider, new TreeData(TreeType.BirchTree, TreeGrade.Normal, TreeState.Idle));
                }
            }
        }

        BindEvents();
    }

    public Transform GetPortalTransform()
    {
        return portal.transform;
    }

    private void SetupCullingGroup()
    {
        if (cullingGroup != null) cullingGroup.Dispose();

        cullingGroup = new CullingGroup();
        cullingGroup.targetCamera = Camera.main;

        spheres = new BoundingSphere[trees.Length];
        for (int i = 0; i < trees.Length; i++)
        {
            spheres[i] = new BoundingSphere(trees[i].transform.position, 3f);
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(trees.Length);

        // 거리 단계 설정 및 기준점(카메라) 지정
        cullingGroup.SetBoundingDistances(new float[] { cullingDistance });
        cullingGroup.SetDistanceReferencePoint(Camera.main.transform);

        cullingGroup.onStateChanged = OnCullingStateChanged;

        // 초기 상태 갱신
        activeTreesForUpdate.Clear();
        for (int i = 0; i < trees.Length; i++)
        {
            bool shouldBeActive = cullingGroup.IsVisible(i) && cullingGroup.GetDistance(i) == 0;

            if (trees[i].gameObject.activeSelf != shouldBeActive)
                trees[i].gameObject.SetActive(shouldBeActive);

            if (shouldBeActive) activeTreesForUpdate.Add(trees[i]);
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent ev)
    {
        if (trees == null)
            return;

        if (ev.index >= trees.Length) return;

        // InDungeonObjectManager와 동일하게 시야(Visible)와 거리(Distance == 0)를 모두 체크
        bool shouldBeActive = ev.isVisible && (ev.currentDistance == 0);
        TreeObj tree = trees[ev.index];

        if (tree.gameObject.activeSelf != shouldBeActive)
        {
            tree.gameObject.SetActive(shouldBeActive);
        }

        if (shouldBeActive)
        {
            if (!activeTreesForUpdate.Contains(tree))
                activeTreesForUpdate.Add(tree);
        }
        else
        {
            activeTreesForUpdate.Remove(tree);
        }
    }

    private void Update()
    {
        if (trees == null || trees.Length == 0)
            return;

        // 시야 내 나무들만 ManualUpdate 호출
        for (int i = 0; i < activeTreesForUpdate.Count; i++)
        {
            activeTreesForUpdate[i].ManualUpdate();
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
        ReleaseEvents();
    }

    private void BindEvents()
    {
        if (portal == null) return;
        portal.PortalActivated -= PortalActivated;
        portal.PortalActivated += PortalActivated;
    }

    private void ReleaseEvents()
    {
        if (portal != null) portal.PortalActivated -= PortalActivated;
    }

    private void PortalActivated(PortalType _type)
    {
        PortalActivatedEvent?.Invoke(_type);
        trees = null;
        activeTreesForUpdate.Clear();
    }
}
