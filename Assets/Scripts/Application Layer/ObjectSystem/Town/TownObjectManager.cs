using System;
using System.Collections.Generic;
using UnityEngine;

public class TownObjectManager : MonoBehaviour, ITownObjSystemCH
{
    //이벤트
    public event Action PortalActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성
    [Header("Portal")]
    [SerializeField] private OffroadVehicleObj portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 25; // 거리 컬링 기준
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private Camera mainCam; // 최적화: 카메라 캐싱

    //내부 상태
    private OffroadVehicleObj portal;
    private TreeObj[] trees;
    public IReadOnlyList<TreeObj> Trees => trees;

    // 최적화: HashSet을 사용하여 Contains 중복 체크 속도 향상 (O(1))
    private List<TreeObj> activeTreesForUpdate = new List<TreeObj>(200);
    public IReadOnlyList<TreeObj> ActiveTrees => activeTreesForUpdate;
    private HashSet<TreeObj> activeTreesForUpdateSet = new HashSet<TreeObj>(200);

    private bool bCanTravel = false;

    [SerializeField] private TreeVisualDataBase treeVisualDataBase;

    public float treeGrowTime = 10f;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        mainCam = Camera.main;

        // CullingGroup 및 거리 배열 미리 생성하여 재사용
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            onCullingStateChangedDelegate = OnCullingStateChanged;
            cullingGroup.onStateChanged = onCullingStateChangedDelegate;
        }

        cullingDistances = new float[] { cullingDistance };
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void ReadyObj()
    {
        if (portal == null)
        {
            portal = Instantiate(portalPrefab);
            portal.transform.position = portalSpawnPoint.position;
            portal.Initialize(PortalType.ToDungeonPortal, environmentProvider);
            portal.SetCanTravel(bCanTravel);
        }
        else
            portal.SetCanTravel(bCanTravel);

        // 씬 내의 나무가 이미 관리 중이라면 다시 찾지 않음 (할당 방지)
        if (trees == null)
        {
            trees = FindObjectsByType<TreeObj>(FindObjectsInactive.Include);
        }

        if (trees != null && trees.Length > 0)
        {
            // BoundingSphere 배열 크기 최적화 및 캐싱
            if (spheres == null || spheres.Length < trees.Length)
            {
                spheres = new BoundingSphere[trees.Length];
            }

            SetupCullingGroup();

            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] != null)
                {
                    TreeType randomType = (TreeType)UnityEngine.Random.Range(1, (int)TreeType.Max);
                    trees[i].Initialize(environmentProvider);
                    trees[i].ApplyData(new TreeData(randomType, TreeGrade.Normal, treeVisualDataBase.Get(randomType)));
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
        if (mainCam == null) mainCam = Camera.main;

        // CullingGroup을 새로 생성하지 않고 기존 객체 설정만 갱신
        cullingGroup.targetCamera = mainCam;

        for (int i = 0; i < trees.Length; i++)
        {
            spheres[i].position = trees[i].transform.position;
            spheres[i].radius = 3f;
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(trees.Length);

        // 캐싱된 배열 사용 (할당 방지)
        cullingDistances[0] = cullingDistance;
        cullingGroup.SetBoundingDistances(cullingDistances);
        cullingGroup.SetDistanceReferencePoint(mainCam.transform);

        // 초기 상태 갱신
        activeTreesForUpdate.Clear();
        activeTreesForUpdateSet.Clear();
        for (int i = 0; i < trees.Length; i++)
        {
            bool isVisible = cullingGroup.IsVisible(i);
            bool isNear = cullingGroup.GetDistance(i) == 0;
            bool shouldBeActive = isVisible && isNear;

            if (trees[i].gameObject.activeSelf != shouldBeActive)
                trees[i].gameObject.SetActive(shouldBeActive);

            if (shouldBeActive)
            {
                activeTreesForUpdate.Add(trees[i]);
                activeTreesForUpdateSet.Add(trees[i]);
            }
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent ev)
    {
        if (trees == null)
            return;

        if (ev.index >= trees.Length) return;

        bool shouldBeActive = ev.isVisible && (ev.currentDistance == 0);
        TreeObj tree = trees[ev.index];

        if (tree.gameObject.activeSelf != shouldBeActive)
        {
            tree.gameObject.SetActive(shouldBeActive);
        }

        if (shouldBeActive)
        {
            // 최적화: HashSet을 사용하여 O(1) 검색 및 추가
            if (activeTreesForUpdateSet.Add(tree))
            {
                activeTreesForUpdate.Add(tree);
            }
        }
        else
        {
            // 최적화: HashSet을 사용하여 O(1) 검색 및 삭제
            if (activeTreesForUpdateSet.Remove(tree))
            {
                activeTreesForUpdate.Remove(tree);
            }
        }
    }

    private void Update()
    {
        if (trees == null || trees.Length == 0)
            return;

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

    private void PortalActivated()
    {
        PortalActivatedEvent?.Invoke();
    }

    public void ClearObjManager()
    {
        // 참조를 해제하지 않고 개수만 0으로 설정하여 재할당 방지
        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(0);
        }
        activeTreesForUpdate.Clear();
        activeTreesForUpdateSet.Clear();
        trees = null;
    }

    public void CanTravel()
    {
        bCanTravel = true;

        if (portal != null)
            portal.SetCanTravel(bCanTravel);
    }

    public TownSaveData GetSaveData()
    {
        return new TownSaveData { bCanTravel = bCanTravel };
    }

    public void LoadSaveData(TownSaveData _data)
    {
        bCanTravel = _data.bCanTravel;
        if (portal != null)
        {
            portal.SetCanTravel(bCanTravel);
        }
    }
}
