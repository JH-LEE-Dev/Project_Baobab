using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.ObjectSystem
{
    /// <summary>
    /// 풀링되는 객체가 구현해야 하는 인터페이스입니다.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>
    /// 모든 GameObject에 대응하는 범용 오브젝트 풀링 시스템입니다.
    /// [GEMINI.md] 컨벤션 및 요다 표기법을 준수합니다.
    /// </summary>
    public class ObjectPools : MonoBehaviour
    {
        // //외부 의존성
        // (필요 시 프리팹 리스트 등을 인스펙터에서 관리할 수 있음)

        // //내부 의존성
        private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>(32);
        private Dictionary<GameObject, GameObject> prefabLookup = new Dictionary<GameObject, GameObject>(64);
        
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 풀 관리자를 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            if (true == isInitialized)
                return;
            
            poolDictionary.Clear();
            prefabLookup.Clear();
            isInitialized = true;
        }

        /// <summary>
        /// 특정 프리팹을 미리 풀링합니다. (성능 최적화용)
        /// </summary>
        public void Prewarm(GameObject _prefab, int _count, Transform _parent = null)
        {
            if (null == _prefab)
                return;

            if (false == poolDictionary.ContainsKey(_prefab))
                poolDictionary.Add(_prefab, new Queue<GameObject>(_count));

            Queue<GameObject> _poolQueue = poolDictionary[_prefab];
            
            for (int _i = 0; _i < _count; _i++)
            {
                GameObject _obj = Instantiate(_prefab, _parent);
                _obj.SetActive(false);
                _poolQueue.Enqueue(_obj);
                
                // 생성된 객체가 어떤 프리팹 출신인지 기록 (Despawn 시 필요)
                prefabLookup.Add(_obj, _prefab);
            }
        }

        /// <summary>
        /// 풀에서 객체를 꺼내 활성화합니다.
        /// </summary>
        public T Spawn<T>(GameObject _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null, bool spawnActivate = true) where T : Component
        {
            GameObject _obj = GetObjectFromPool(_prefab, _position, _rotation, _parent, spawnActivate);
            return _obj.GetComponent<T>();
        }

        /// <summary>
        /// 풀에서 GameObject를 꺼내 활성화합니다.
        /// </summary>
        public GameObject Spawn(GameObject _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null, bool spawnActivate = true)
        {
            return GetObjectFromPool(_prefab, _position, _rotation, _parent, spawnActivate);
        }

        /// <summary>
        /// 객체를 풀로 다시 반환합니다.
        /// </summary>
        public void Despawn(GameObject _obj)
        {
            if (null == _obj)
                return;

            if (true == prefabLookup.TryGetValue(_obj, out GameObject _prefab))
            {
                if (true == poolDictionary.TryGetValue(_prefab, out Queue<GameObject> _poolQueue))
                {
                    _obj.SetActive(false);
                    
                    // IPoolable 구현 여부 확인 및 호출
                    IPoolable _poolable = _obj.GetComponent<IPoolable>();
                    if (null != _poolable)
                        _poolable.OnDespawn();

                    _poolQueue.Enqueue(_obj);
                }
                else
                    Destroy(_obj);
            }
            else
                Destroy(_obj);
        }

        private GameObject GetObjectFromPool(GameObject _prefab, Vector3 _position, Quaternion _rotation, Transform _parent, bool spawnActivate)
        {
            if (null == _prefab)
                return null;

            if (false == poolDictionary.ContainsKey(_prefab))
            {
                poolDictionary.Add(_prefab, new Queue<GameObject>(16));
            }

            Queue<GameObject> _poolQueue = poolDictionary[_prefab];
            GameObject _obj;

            if (0 < _poolQueue.Count)
                _obj = _poolQueue.Dequeue();
            else
            {
                _obj = Instantiate(_prefab, _parent);
                prefabLookup.Add(_obj, _prefab);
            }

            _obj.transform.SetPositionAndRotation(_position, _rotation);
            if (null != _parent)
                _obj.transform.SetParent(_parent);
            
            _obj.SetActive(spawnActivate);

            // IPoolable 구현 여부 확인 및 호출
            IPoolable _poolable = _obj.GetComponent<IPoolable>();
            if (null != _poolable)
                _poolable.OnSpawn();

            return _obj;
        }

        // //유니티 이벤트 함수

        private void Awake()
        {
            // 수동 초기화가 되지 않았을 경우 대비
            if (false == isInitialized)
                Initialize();
        }

        private void OnDestroy()
        {
            // 메모리 해제 및 풀 비우기
            foreach (var _pool in poolDictionary.Values)
                _pool.Clear();

            poolDictionary.Clear();
            prefabLookup.Clear();
        }
    }
}
