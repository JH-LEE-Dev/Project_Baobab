using System;
using System.Collections.Generic;
using UnityEngine;

public class TownObjectManager : MonoBehaviour, ITownObjSystemCH
{
    //이벤트
    public event Action<bool> OffroadInteractStateChangedEvent;
    public event Action PortalActivatedEvent;
    public event Action PortalDeActivatedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;
    private InputManager inputManager;

    //내부 의존성
    [Header("Portal")]
    [SerializeField] private OffroadVehicleObj offroadVehiclePrefab;
    [SerializeField] private Transform portalSpawnPoint;

    [Header("Optimization")]
    [SerializeField] private bool enableCulling = false;
    [SerializeField] private float cullingDistance = 25; // 거리 컬링 기준
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private Camera mainCam; // 최적화: 카메라 캐싱

    //내부 상태
    public OffroadVehicleObj offroadVehicle { get; private set; }
    private TreeObj[] trees;
    public IReadOnlyList<TreeObj> Trees => trees;

    // Town 소유 나무 루트. 씬 전체를 뒤지면 InDungeonObjectManager가 풀링 중인 나무(비활성/죽은 나무 포함)까지
    // 함께 잡혀서 Town 진입 시 되살아나 보이는 문제가 있어, 이 루트 하위만 탐색하도록 범위를 제한한다.
    private const string TreeRootName = "Trees";
    private Transform treeRoot;

    // 최적화: 인덱스 기반 관리로 HashSet 제거 및 O(1) 처리
    private List<TreeObj> activeTreesForUpdate = new List<TreeObj>(200);
    public IReadOnlyList<TreeObj> ActiveTrees => activeTreesForUpdate;

    private bool bCanTravel = true;

    [SerializeField] private TreeVisualDataBase treeVisualDataBase;

    private IInventory characterInventory;
    private OffroadContainer offroadContainer;

    [SerializeField] private Transform townReturnPoint;

    private Character character;

    public float treeGrowTime = 10f;

    public void Initialize(IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer)
    {
        environmentProvider = _environmentProvider;
        inputManager = _inputManager;
        mainCam = Camera.main;
        characterInventory = _characterInventory;
        offroadContainer = _offroadContainer;

        // CullingGroup 및 거리 배열 미리 생성하여 재사용
        if (enableCulling)
        {
            if (cullingGroup == null)
            {
                cullingGroup = new CullingGroup();
                onCullingStateChangedDelegate = OnCullingStateChanged;
                cullingGroup.onStateChanged = onCullingStateChangedDelegate;
            }

            cullingDistances = new float[] { cullingDistance };
        }
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
    }

    public void ReadyObj()
    {
        if (offroadVehicle == null)
        {
            offroadVehicle = Instantiate(offroadVehiclePrefab);
            offroadVehicle.transform.position = portalSpawnPoint.position;
            offroadVehicle.Initialize(PortalType.ToDungeonPortal, environmentProvider, inputManager, characterInventory, offroadContainer,
            character.centerTransform);
            offroadVehicle.ResetObject();
            offroadVehicle.SetCanTravel(bCanTravel);
        }
        else
        {
            offroadVehicle.ResetObject();
            offroadVehicle.SetCanTravel(bCanTravel);
        }

        offroadVehicle.SetVisualActive(true);
        offroadVehicle.DeActivateRepairBox();
        
        if (treeRoot == null)
        {
            GameObject treeRootObj = GameObject.Find(TreeRootName);
            if (treeRootObj != null)
            {
                treeRoot = treeRootObj.transform;
            }
        }

        trees = treeRoot != null
            ? treeRoot.GetComponentsInChildren<TreeObj>(true)
            : Array.Empty<TreeObj>();

        if (trees != null && trees.Length > 0)
        {
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] != null)
                {
                    trees[i].Initialize(environmentProvider);
                    trees[i].BTreeShadowSet = false;

                    TreeType type = trees[i].GetCustomTreeType();
                    TreeGrade grade = TreeGrade.Normal;

                    trees[i].ApplyData(new TreeData(type, grade, treeVisualDataBase.Get(type), default));
                    trees[i].SetSortOrder();
                    trees[i].DisableOutline();
                }
            }

            if (enableCulling)
            {
                // BoundingSphere 배열 크기 최적화 및 캐싱
                if (spheres == null || spheres.Length < trees.Length)
                {
                    spheres = new BoundingSphere[trees.Length];
                }

                SetupCullingGroup();
            }
            else
            {
                activeTreesForUpdate.Clear();
                for (int i = 0; i < trees.Length; i++)
                {
                    if (trees[i] != null)
                    {
                        trees[i].gameObject.SetActive(true);
                        trees[i].UpdateIndex = activeTreesForUpdate.Count;
                        activeTreesForUpdate.Add(trees[i]);
                    }
                }
            }
        }

        BindEvents();
    }

    public Transform GetPortalTransform()
    {
        return offroadVehicle.transform;
    }

    public Transform GetTownReturnPoint()
    {
        return townReturnPoint;
    }

    private void SetupCullingGroup()
    {
        if (!enableCulling) return;

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
        if (!enableCulling) return;

        if (trees == null)
            return;

        if (ev.index >= trees.Length) return;

        bool shouldBeActive = ev.isVisible && (ev.currentDistance == 0);
        TreeObj tree = trees[ev.index];

        if (tree == null)
            return;

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
        if (offroadVehicle == null) return;

        offroadVehicle.PortalActivated -= PortalActivated;
        offroadVehicle.PortalActivated += PortalActivated;

        offroadVehicle.PortalDeActivatedEvent -= PortalDeActivated;
        offroadVehicle.PortalDeActivatedEvent += PortalDeActivated;

        offroadVehicle.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        offroadVehicle.OffroadInteractStateChangedEvent += OffroadInteractStateChanged;
    }

    private void ReleaseEvents()
    {
        if (offroadVehicle != null)
        {
            offroadVehicle.PortalActivated -= PortalActivated;
            offroadVehicle.PortalDeActivatedEvent -= PortalDeActivated;
            offroadVehicle.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        }
    }

    private void PortalActivated()
    {
        PortalActivatedEvent?.Invoke();
    }

    public void ClearObjManager()
    {
        offroadVehicle.SetVisualActive(false);
        offroadVehicle.gameObject.SetActive(false);
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

        if (offroadVehicle != null)
            offroadVehicle.SetCanTravel(bCanTravel);
    }

    public void TeleportUIClosed()
    {
        if (offroadVehicle != null)
        {
            offroadVehicle.SetUIActivated(false);
        }
    }

    private void PortalDeActivated()
    {
        PortalDeActivatedEvent?.Invoke();
    }

    private void OffroadInteractStateChanged(bool _boolean)
    {
        OffroadInteractStateChangedEvent?.Invoke(_boolean);
    }
}
