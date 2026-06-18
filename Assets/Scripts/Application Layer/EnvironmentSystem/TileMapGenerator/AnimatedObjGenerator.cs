using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public class AnimatedObjGenerator : MonoBehaviour
{
    // // 내부 의존성 및 캐싱 필드
    private List<AnimatedObj> currentPrefabs;
    private Dictionary<AnimatedObj, IObjectPool<AnimatedObj>> poolDict;
    private Dictionary<AnimatedObj, List<AnimatedObj>> activeObjectsDict;

    // // 퍼블릭 초기화 및 제어 메서드

    public void SetPrefabs(List<AnimatedObj> _prefabs)
    {
        ReleaseAllActive();
        currentPrefabs = _prefabs;
    }

    public void Initialize()
    {
        poolDict = new Dictionary<AnimatedObj, IObjectPool<AnimatedObj>>(8);
        activeObjectsDict = new Dictionary<AnimatedObj, List<AnimatedObj>>(8);
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
                maxSize: 100
            );
            poolDict.Add(_prefab, _pool);
            activeObjectsDict.Add(_prefab, new List<AnimatedObj>(32));
        }
        return _pool;
    }

    public AnimatedObj SpawnAnimatedObj(Vector3 _position)
    {
        if (currentPrefabs == null || currentPrefabs.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, currentPrefabs.Count);
        AnimatedObj _prefab = currentPrefabs[randomIndex];
        if (_prefab == null) return null;

        IObjectPool<AnimatedObj> _pool = GetPool(_prefab);
        AnimatedObj _targetObj = _pool.Get();

        if (_targetObj != null)
        {
            _targetObj.transform.position = _position;
            activeObjectsDict[_prefab].Add(_targetObj);
        }
        return _targetObj;
    }

    public void ReleaseAllActive()
    {
        if (activeObjectsDict == null || poolDict == null) return;

        foreach (var kvp in activeObjectsDict)
        {
            AnimatedObj _prefab = kvp.Key;
            List<AnimatedObj> _activeList = kvp.Value;
            if (poolDict.TryGetValue(_prefab, out var _pool))
            {
                for (int i = 0; i < _activeList.Count; i++)
                {
                    AnimatedObj _obj = _activeList[i];
                    if (_obj != null)
                    {
                        _pool.Release(_obj);
                    }
                }
            }
            _activeList.Clear();
        }
    }

    // // 내부 풀 관리 메서드

    private AnimatedObj CreateAnimatedObj(AnimatedObj _prefab)
    {
        if (_prefab == null) return null;

        AnimatedObj _newItem = Instantiate(_prefab, transform);
        if (_newItem != null)
        {
            _newItem.Initialize();
        }
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
        if (_obj != null)
        {
            _obj.gameObject.SetActive(false);
        }
    }

    private void OnDestroyAnimatedObj(AnimatedObj _obj)
    {
        if (_obj != null)
        {
            Destroy(_obj.gameObject);
        }
    }
}
