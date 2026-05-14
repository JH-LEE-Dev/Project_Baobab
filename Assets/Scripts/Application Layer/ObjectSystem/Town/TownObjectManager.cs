using System;
using System.Collections.Generic;
using UnityEngine;

public class TownObjectManager : MonoBehaviour, ITownObjSystemCH
{
    //이벤트
    public event Action PortalActivatedEvent;
    public event Action PortalDeActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;
    private InputManager inputManager;

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

    // 최적화: 인덱스 기반 관리로 HashSet 제거 및 O(1) 처리
    private List<TreeObj> activeTreesForUpdate = new List<TreeObj>(200);
    public IReadOnlyList<TreeObj> ActiveTrees => activeTreesForUpdate;

    private bool bCanTravel = false;

    [SerializeField] private TreeVisualDataBase treeVisualDataBase;

    public float treeGrowTime = 10f;

    public void Initialize(IEnvironmentProvider _environmentProvider, InputManager _inputManager)
    {
        environmentProvider = _environmentProvider;
        inputManager = _inputManager;
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
            portal.Initialize(PortalType.ToDungeonPortal, environmentProvider, inputManager);
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
                    trees[i].ApplyData(new TreeData(randomType, TreeGrade.Normal, treeVisualDataBase.Get(randomType), default));
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
        for (int i = 0; i < trees.Length; i++)
        {
            bool isVisible = cullingGroup.IsVisible(i);
            bool isNear = cullingGroup.GetDistance(i) == 0;
            bool shouldBeActive = isVisible && isNear;

            if (trees[i].gameObject.activeSelf != shouldBeActive)
                trees[i].gameObject.SetActive(shouldBeActive);

            if (shouldBeActive)
            {
                trees[i].UpdateIndex = activeTreesForUpdate.Count;
                activeTreesForUpdate.Add(trees[i]);
            }
            else
            {
                trees[i].UpdateIndex = -1;
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
            // 최적화: 인덱스 기반 O(1) 추가
            if (tree.UpdateIndex == -1)
            {
                tree.UpdateIndex = activeTreesForUpdate.Count;
                activeTreesForUpdate.Add(tree);
            }
        }
        else
        {
            // 최적화: Swap-with-last 기반 O(1) 삭제
            int idx = tree.UpdateIndex;
            if (idx != -1 && idx < activeTreesForUpdate.Count)
            {
                int lastIdx = activeTreesForUpdate.Count - 1;
                if (idx != lastIdx)
                {
                    TreeObj lastTree = activeTreesForUpdate[lastIdx];
                    activeTreesForUpdate[idx] = lastTree;
                    lastTree.UpdateIndex = idx;
                }
                activeTreesForUpdate.RemoveAt(lastIdx);
                tree.UpdateIndex = -1;
            }
        }
    }

    private void Update()
    {
        if (trees == null || trees.Length == 0)
            return;

        // 버그 수정: ManualUpdate 중 리스트 변형에 대비하여 역순 순회
        for (int i = activeTreesForUpdate.Count - 1; i >= 0; i--)
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

        portal.PortalDeActivatedEvent -= PortalDeActivated;
        portal.PortalDeActivatedEvent += PortalDeActivated;
    }

    private void ReleaseEvents()
    {
        if (portal != null)
        {
            portal.PortalActivated -= PortalActivated;
            portal.PortalDeActivatedEvent -= PortalDeActivated;
        }
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

        for (int i = 0; i < activeTreesForUpdate.Count; i++)
        {
            activeTreesForUpdate[i].UpdateIndex = -1;
        }
        activeTreesForUpdate.Clear();
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

    public void TeleportUIClosed()
    {
        if (portal != null)
        {
            portal.SetUIActivated(false);
        }
    }

    private void PortalDeActivated()
    {
        PortalDeActivatedEvent?.Invoke();
    }
}
