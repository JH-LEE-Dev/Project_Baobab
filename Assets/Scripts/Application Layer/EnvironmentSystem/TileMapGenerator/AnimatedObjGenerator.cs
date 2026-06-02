using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public class AnimatedObjGenerator : MonoBehaviour
{
    // // 외부 의존성
    [SerializeField] private AnimatedObj animatedObjPrefab;

    // // 내부 의존성 및 캐싱 필드
    private IObjectPool<AnimatedObj> animatedObjPool;
    private List<AnimatedObj> activeObjects;

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        activeObjects = new List<AnimatedObj>(32);

        animatedObjPool = new ObjectPool<AnimatedObj>(
            createFunc: CreateAnimatedObj,
            actionOnGet: OnGetAnimatedObj,
            actionOnRelease: OnReleaseAnimatedObj,
            actionOnDestroy: OnDestroyAnimatedObj,
            collectionCheck: true,
            defaultCapacity: 32,
            maxSize: 100
        );
    }

    public AnimatedObj SpawnAnimatedObj(Vector3 _position)
    {
        AnimatedObj _targetObj = animatedObjPool.Get();
        if (_targetObj != null)
        {
            _targetObj.transform.position = _position;
            activeObjects.Add(_targetObj);
        }
        return _targetObj;
    }

    public void ReleaseAllActive()
    {
        if (activeObjects == null || animatedObjPool == null) return;

        for (int i = 0; i < activeObjects.Count; i++)
        {
            AnimatedObj _obj = activeObjects[i];
            if (_obj != null)
            {
                animatedObjPool.Release(_obj);
            }
        }

        activeObjects.Clear();
    }

    // // 내부 풀 관리 메서드

    private AnimatedObj CreateAnimatedObj()
    {
        AnimatedObj _newItem = Instantiate(animatedObjPrefab, transform);
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
