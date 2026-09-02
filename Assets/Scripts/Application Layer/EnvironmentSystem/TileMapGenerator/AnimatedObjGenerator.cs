using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public class AnimatedObjGenerator : MonoBehaviour
{
    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 35f;

    // // 내부 의존성 및 캐싱 필드
    private List<AnimatedObj> currentLandPrefabs;
    private List<DecoSpritePatternAnimator> currentWaterPrefabs;
    private List<StaticObj> currentGrassStaticPrefabs;
    private List<StaticObj> currentSandStaticPrefabs;
    private List<StaticObj> currentShorelineStaticPrefabs;
    private List<AnimatedObj> currentShorelineAnimatedPrefabs;
    private List<AnimatedObj> currentWaterOtherTypeAnimatedPrefabs;
    
    private Dictionary<AnimatedObj, IObjectPool<AnimatedObj>> poolDict;
    private Dictionary<AnimatedObj, List<AnimatedObj>> activeObjectsDict;

    private Dictionary<DecoSpritePatternAnimator, IObjectPool<DecoSpritePatternAnimator>> waterPoolDict;
    private Dictionary<DecoSpritePatternAnimator, List<DecoSpritePatternAnimator>> activeWaterObjectsDict;

    private Dictionary<StaticObj, IObjectPool<StaticObj>> staticPoolDict;
    private Dictionary<StaticObj, List<StaticObj>> activeStaticObjectsDict;

    // // 컬링 최적화
    private List<Component> cullableObjects;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private Camera mainCam;

    // // 퍼블릭 초기화 및 제어 메서드

    public void SetPrefabs(List<AnimatedObj> _landPrefabs, List<DecoSpritePatternAnimator> _waterPrefabs, List<StaticObj> _grassStaticPrefabs, List<StaticObj> _sandStaticPrefabs, List<StaticObj> _shorelineStaticPrefabs, List<AnimatedObj> _shorelineAnimatedPrefabs, List<AnimatedObj> _waterOtherTypeAnimatedPrefabs)
    {
        ReleaseAllActive();
        currentLandPrefabs = _landPrefabs;
        currentWaterPrefabs = _waterPrefabs;
        currentGrassStaticPrefabs = _grassStaticPrefabs;
        currentSandStaticPrefabs = _sandStaticPrefabs;
        currentShorelineStaticPrefabs = _shorelineStaticPrefabs;
        currentShorelineAnimatedPrefabs = _shorelineAnimatedPrefabs;
        currentWaterOtherTypeAnimatedPrefabs = _waterOtherTypeAnimatedPrefabs;

        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (cullingGroup != null && mainCam != null)
            {
                cullingGroup.targetCamera = mainCam;
                cullingGroup.SetDistanceReferencePoint(mainCam.transform);
            }
        }
    }

    /// <summary>
    /// 던전에 들어올 때마다 호출된다(TileMapGenerator.InitializeMapData).
    ///
    /// 그래서 컬렉션을 매번 새로 만들면 안 된다. 여기서 만드는 오브젝트는
    /// Instantiate(_prefab, transform)으로 이 컴포넌트(= GameInstaller 하위, DontDestroyOnLoad)에
    /// 매달리기 때문에, 딕셔너리를 교체하는 순간 추적에서 떨어져 나가 파괴도 회수도 되지 않는다.
    ///   - 타운을 거친 진입: 직전 TownStarted가 전부 비활성화해 풀에 넣어둔 상태라, 그게 통째로
    ///     고아가 되고 새 맵은 처음부터 다시 Instantiate했다. 원정 한 번마다 수백 개씩 쌓였다.
    ///   - 재도전(타운 미경유): ClearObjManager()가 ReleaseAllAnimatedObj()를 부르지 않으므로
    ///     이전 맵 장식이 활성 상태 그대로 고아가 되고, 뒤이은 ReleaseAllActive()는 이미 비워진
    ///     딕셔너리를 훑어 아무것도 끄지 못했다. 이전 던전의 풀·물결이 새 맵 위에 겹쳐 보였다.
    ///
    /// 따라서 컬렉션은 최초 1회만 만들고, 이후 호출은 "이전 맵 정리 + 카메라 재연결"만 한다.
    /// (씬이 바뀌면 Camera.main도 새 인스턴스이므로 SetupCullingGroup은 매번 다시 불러야 한다)
    /// </summary>
    public void Initialize()
    {
        if (poolDict != null)
        {
            ReleaseAllActive();
            SetupCullingGroup();
            return;
        }

        poolDict = new Dictionary<AnimatedObj, IObjectPool<AnimatedObj>>(8);
        activeObjectsDict = new Dictionary<AnimatedObj, List<AnimatedObj>>(8);

        waterPoolDict = new Dictionary<DecoSpritePatternAnimator, IObjectPool<DecoSpritePatternAnimator>>(8);
        activeWaterObjectsDict = new Dictionary<DecoSpritePatternAnimator, List<DecoSpritePatternAnimator>>(8);

        staticPoolDict = new Dictionary<StaticObj, IObjectPool<StaticObj>>(8);
        activeStaticObjectsDict = new Dictionary<StaticObj, List<StaticObj>>(8);

        cullableObjects = new List<Component>(2000);
        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[2000];
        onCullingStateChangedDelegate = OnCullingStateChanged;

        SetupCullingGroup();
    }

    private void SetupCullingGroup()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = onCullingStateChangedDelegate;
        }

        mainCam = Camera.main;
        if (mainCam != null)
        {
            cullingGroup.targetCamera = mainCam;
            cullingGroup.SetBoundingDistances(cullingDistances);
            cullingGroup.SetDistanceReferencePoint(mainCam.transform);
            cullingGroup.SetBoundingSpheres(spheres);
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

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= cullableObjects.Count) return;

        Component obj = cullableObjects[_ev.index];
        if (obj == null) return;

        bool shouldBeActive = _ev.isVisible && (_ev.currentDistance == 0);
        
        if (obj.gameObject.activeSelf != shouldBeActive)
        {
            obj.gameObject.SetActive(shouldBeActive);
            
            // 객체가 다시 화면에 들어올 때 애니메이션 초기화 처리
            if (shouldBeActive && obj is AnimatedObj animObj)
            {
                animObj.ResetAnimationToRandomFrame();
            }
        }
    }

    private IObjectPool<AnimatedObj> GetPool(AnimatedObj _prefab)
    {
        if (!poolDict.TryGetValue(_prefab, out var _pool))
        {
            _pool = new ObjectPool<AnimatedObj>(
                createFunc: () => CreateAnimatedObj(_prefab),
                actionOnGet: OnGetAnimatedObj,
                actionOnRelease: OnReleaseAnimatedObj,
                actionOnDestroy: OnDestroyAnimatedObj,
                collectionCheck: true,
                defaultCapacity: 32,
                maxSize: 500
            );
            poolDict.Add(_prefab, _pool);
            activeObjectsDict.Add(_prefab, new List<AnimatedObj>(100));
        }
        return _pool;
    }

    private IObjectPool<DecoSpritePatternAnimator> GetWaterPool(DecoSpritePatternAnimator _prefab)
    {
        if (!waterPoolDict.TryGetValue(_prefab, out var _pool))
        {
            _pool = new ObjectPool<DecoSpritePatternAnimator>(
                createFunc: () => CreateWaterAnimatedObj(_prefab),
                actionOnGet: OnGetWaterAnimatedObj,
                actionOnRelease: OnReleaseWaterAnimatedObj,
                actionOnDestroy: OnDestroyWaterAnimatedObj,
                collectionCheck: true,
                defaultCapacity: 32,
                maxSize: 500
            );
            waterPoolDict.Add(_prefab, _pool);
            activeWaterObjectsDict.Add(_prefab, new List<DecoSpritePatternAnimator>(100));
        }
        return _pool;
    }

    private IObjectPool<StaticObj> GetStaticPool(StaticObj _prefab)
    {
        if (!staticPoolDict.TryGetValue(_prefab, out var _pool))
        {
            _pool = new ObjectPool<StaticObj>(
                createFunc: () => CreateStaticObj(_prefab),
                actionOnGet: OnGetStaticObj,
                actionOnRelease: OnReleaseStaticObj,
                actionOnDestroy: OnDestroyStaticObj,
                collectionCheck: true,
                defaultCapacity: 32,
                maxSize: 500
            );
            staticPoolDict.Add(_prefab, _pool);
            activeStaticObjectsDict.Add(_prefab, new List<StaticObj>(100));
        }
        return _pool;
    }

    public AnimatedObj SpawnAnimatedObj(Vector3 _position)
    {
        return SpawnAnimatedObjFromList(_position, currentLandPrefabs);
    }

    public AnimatedObj SpawnShorelineAnimatedObj(Vector3 _position)
    {
        return SpawnAnimatedObjFromList(_position, currentShorelineAnimatedPrefabs);
    }

    // WaterAnimatedObjPrefabs(DecoSpritePatternAnimator)와 별개로, AnimatedObj 타입 프리팹을 물 위에 스폰한다.
    public AnimatedObj SpawnWaterAnimatedOtherTypeObj(Vector3 _position)
    {
        return SpawnAnimatedObjFromList(_position, currentWaterOtherTypeAnimatedPrefabs);
    }

    private AnimatedObj SpawnAnimatedObjFromList(Vector3 _position, List<AnimatedObj> _prefabs)
    {
        if (_prefabs == null || _prefabs.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, _prefabs.Count);
        AnimatedObj _prefab = _prefabs[randomIndex];
        if (_prefab == null) return null;

        IObjectPool<AnimatedObj> _pool = GetPool(_prefab);
        AnimatedObj _targetObj = _pool.Get();

        if (_targetObj != null)
        {
            _targetObj.transform.position = _position;
            _targetObj.SetSortingOrder();
            activeObjectsDict[_prefab].Add(_targetObj);
            AddCullingObject(_targetObj, _position);
        }
        return _targetObj;
    }

    public DecoSpritePatternAnimator SpawnWaterAnimatedObj(Vector3 _position)
    {
        if (currentWaterPrefabs == null || currentWaterPrefabs.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, currentWaterPrefabs.Count);
        DecoSpritePatternAnimator _prefab = currentWaterPrefabs[randomIndex];
        if (_prefab == null) return null;

        IObjectPool<DecoSpritePatternAnimator> _pool = GetWaterPool(_prefab);
        DecoSpritePatternAnimator _targetObj = _pool.Get();

        if (_targetObj != null)
        {
            _targetObj.transform.position = _position;
            _targetObj.SetSortingOrder();
            activeWaterObjectsDict[_prefab].Add(_targetObj);
            AddCullingObject(_targetObj, _position);
        }
        return _targetObj;
    }

    public StaticObj SpawnGrassStaticObj(Vector3 _position)
    {
        return SpawnStaticObjFromList(_position, currentGrassStaticPrefabs);
    }

    public StaticObj SpawnSandStaticObj(Vector3 _position)
    {
        return SpawnStaticObjFromList(_position, currentSandStaticPrefabs);
    }

    public StaticObj SpawnShorelineStaticObj(Vector3 _position)
    {
        return SpawnStaticObjFromList(_position, currentShorelineStaticPrefabs);
    }

    private StaticObj SpawnStaticObjFromList(Vector3 _position, List<StaticObj> _prefabs)
    {
        if (_prefabs == null || _prefabs.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, _prefabs.Count);
        StaticObj _prefab = _prefabs[randomIndex];
        if (_prefab == null) return null;

        IObjectPool<StaticObj> _pool = GetStaticPool(_prefab);
        StaticObj _targetObj = _pool.Get();

        if (_targetObj != null)
        {
            _targetObj.transform.position = _position;
            _targetObj.SetSortingOrder();
            activeStaticObjectsDict[_prefab].Add(_targetObj);
            AddCullingObject(_targetObj, _position);
        }
        return _targetObj;
    }

    private void AddCullingObject(Component _targetObj, Vector3 _position)
    {
        int index = cullableObjects.Count;
        cullableObjects.Add(_targetObj);

        if (spheres.Length <= index)
        {
            System.Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, index + 1));
            cullingGroup.SetBoundingSpheres(spheres);
        }
        spheres[index].position = _position;
        spheres[index].radius = 1.5f;

        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(cullableObjects.Count);
            
            bool isVisible = cullingGroup.IsVisible(index) && (cullingGroup.GetDistance(index) == 0);
            if (_targetObj.gameObject.activeSelf != isVisible)
            {
                _targetObj.gameObject.SetActive(isVisible);
            }
        }
    }

    public void ReleaseAllActive()
    {
        if (activeObjectsDict != null && poolDict != null)
        {
            foreach (var kvp in activeObjectsDict)
            {
                AnimatedObj _prefab = kvp.Key;
                List<AnimatedObj> _activeList = kvp.Value;
                if (poolDict.TryGetValue(_prefab, out var _pool))
                {
                    for (int i = 0; i < _activeList.Count; i++)
                    {
                        AnimatedObj _obj = _activeList[i];
                        if (_obj != null) _pool.Release(_obj);
                    }
                }
                _activeList.Clear();
            }
        }

        if (activeWaterObjectsDict != null && waterPoolDict != null)
        {
            foreach (var kvp in activeWaterObjectsDict)
            {
                DecoSpritePatternAnimator _prefab = kvp.Key;
                List<DecoSpritePatternAnimator> _activeList = kvp.Value;
                if (waterPoolDict.TryGetValue(_prefab, out var _pool))
                {
                    for (int i = 0; i < _activeList.Count; i++)
                    {
                        DecoSpritePatternAnimator _obj = _activeList[i];
                        if (_obj != null) _pool.Release(_obj);
                    }
                }
                _activeList.Clear();
            }
        }

        if (activeStaticObjectsDict != null && staticPoolDict != null)
        {
            foreach (var kvp in activeStaticObjectsDict)
            {
                StaticObj _prefab = kvp.Key;
                List<StaticObj> _activeList = kvp.Value;
                if (staticPoolDict.TryGetValue(_prefab, out var _pool))
                {
                    for (int i = 0; i < _activeList.Count; i++)
                    {
                        StaticObj _obj = _activeList[i];
                        if (_obj != null) _pool.Release(_obj);
                    }
                }
                _activeList.Clear();
            }
        }

        if (cullableObjects != null)
        {
            cullableObjects.Clear();
        }
        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(0);
        }
    }

    // // 내부 풀 관리 메서드

    private AnimatedObj CreateAnimatedObj(AnimatedObj _prefab)
    {
        if (_prefab == null) return null;
        AnimatedObj _newItem = Instantiate(_prefab, transform);
        if (_newItem != null) _newItem.Initialize();
        return _newItem;
    }

    private void OnGetAnimatedObj(AnimatedObj _obj)
    {
        if (_obj != null)
        {
            _obj.gameObject.SetActive(true);
            _obj.ResetAnimationToRandomFrame();
        }
    }

    private void OnReleaseAnimatedObj(AnimatedObj _obj)
    {
        if (_obj != null) _obj.gameObject.SetActive(false);
    }

    private void OnDestroyAnimatedObj(AnimatedObj _obj)
    {
        if (_obj != null) Destroy(_obj.gameObject);
    }

    // // Water Pool
    private DecoSpritePatternAnimator CreateWaterAnimatedObj(DecoSpritePatternAnimator _prefab)
    {
        if (_prefab == null) return null;
        return Instantiate(_prefab, transform);
    }

    private void OnGetWaterAnimatedObj(DecoSpritePatternAnimator _obj)
    {
        if (_obj != null) _obj.gameObject.SetActive(true);
    }

    private void OnReleaseWaterAnimatedObj(DecoSpritePatternAnimator _obj)
    {
        if (_obj != null) _obj.gameObject.SetActive(false);
    }

    private void OnDestroyWaterAnimatedObj(DecoSpritePatternAnimator _obj)
    {
        if (_obj != null) Destroy(_obj.gameObject);
    }

    // // Static Pool
    private StaticObj CreateStaticObj(StaticObj _prefab)
    {
        if (_prefab == null) return null;
        StaticObj _newItem = Instantiate(_prefab, transform);
        if (_newItem != null) _newItem.Initialize();
        return _newItem;
    }

    private void OnGetStaticObj(StaticObj _obj)
    {
        if (_obj != null) _obj.gameObject.SetActive(true);
    }

    private void OnReleaseStaticObj(StaticObj _obj)
    {
        if (_obj != null) _obj.gameObject.SetActive(false);
    }

    private void OnDestroyStaticObj(StaticObj _obj)
    {
        if (_obj != null) Destroy(_obj.gameObject);
    }
}
