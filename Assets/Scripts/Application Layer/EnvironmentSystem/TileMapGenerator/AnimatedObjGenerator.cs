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
    
    private Dictionary<AnimatedObj, IObjectPool<AnimatedObj>> poolDict;
    private Dictionary<AnimatedObj, List<AnimatedObj>> activeObjectsDict;

    private Dictionary<DecoSpritePatternAnimator, IObjectPool<DecoSpritePatternAnimator>> waterPoolDict;
    private Dictionary<DecoSpritePatternAnimator, List<DecoSpritePatternAnimator>> activeWaterObjectsDict;

    // // 컬링 최적화
    private List<Component> cullableObjects;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private Camera mainCam;

    // // 퍼블릭 초기화 및 제어 메서드

    public void SetPrefabs(List<AnimatedObj> _landPrefabs, List<DecoSpritePatternAnimator> _waterPrefabs)
    {
        ReleaseAllActive();
        currentLandPrefabs = _landPrefabs;
        currentWaterPrefabs = _waterPrefabs;

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

    public void Initialize()
    {
        poolDict = new Dictionary<AnimatedObj, IObjectPool<AnimatedObj>>(8);
        activeObjectsDict = new Dictionary<AnimatedObj, List<AnimatedObj>>(8);

        waterPoolDict = new Dictionary<DecoSpritePatternAnimator, IObjectPool<DecoSpritePatternAnimator>>(8);
        activeWaterObjectsDict = new Dictionary<DecoSpritePatternAnimator, List<DecoSpritePatternAnimator>>(8);

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

    public AnimatedObj SpawnAnimatedObj(Vector3 _position)
    {
        if (currentLandPrefabs == null || currentLandPrefabs.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, currentLandPrefabs.Count);
        AnimatedObj _prefab = currentLandPrefabs[randomIndex];
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
}
