using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

[System.Serializable]
public class VFXPoolData
{
    // 외부 의존성
    [SerializeField] private string vfxTag;
    [SerializeField] private ParticleSystem effectPrefab;
    [SerializeField] private int initialPoolSize = 5;
    [SerializeField] private bool allowDynamicExpansion = true;
    [SerializeField] [ShowIf("allowDynamicExpansion")] private int maxPoolSize = 10;
    [SerializeField] private float uiParticleScale = 1.0f;

    // 퍼블릭 초기화 및 제어 메서드
    public string VfxTag => vfxTag;
    public ParticleSystem EffectPrefab => effectPrefab;
    public int InitialPoolSize => initialPoolSize;
    public bool AllowDynamicExpansion => allowDynamicExpansion;
    public int MaxPoolSize => maxPoolSize;
    public float UiParticleScale => uiParticleScale;
}

/// <summary>
/// 여러 종류의 이펙트 프리팹을 태그별로 바인딩하여 각각 로컬 오브젝트 풀링을 수행하는 VFX 컴포넌트입니다.
/// </summary>
public class VFXComponent : MonoBehaviour
{
    [Header("UI Canvas Settings")]
    [SerializeField] private bool isUIComponent = false;

    // 외부 의존성
    [Header("VFX Pool List")]
    [SerializeField] private List<VFXPoolData> vfxPoolDataList;

    // 내부 의존성
    private Dictionary<string, List<ParticleSystem>> poolDictionary;
    private Dictionary<string, VFXPoolData> configDictionary;
    private List<ParticleSystem> masterList;
    private bool isInitialized = false;


    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// 풀 리스트의 설정 데이터를 기반으로 각 태그별 로컬 풀을 초기화합니다.
    /// </summary>
    public void Initialize()
    {
        if (true == isInitialized)
            return;

        if (null == vfxPoolDataList)
            return;

        int _dataCount = vfxPoolDataList.Count;
        poolDictionary = new Dictionary<string, List<ParticleSystem>>(_dataCount);
        configDictionary = new Dictionary<string, VFXPoolData>(_dataCount);
        masterList = new List<ParticleSystem>();

        for (int i = 0; i < _dataCount; i++)
        {
            VFXPoolData _data = vfxPoolDataList[i];
            if (null == _data || string.IsNullOrEmpty(_data.VfxTag) || null == _data.EffectPrefab)
                continue;

            if (true == configDictionary.ContainsKey(_data.VfxTag))
                continue;

            configDictionary.Add(_data.VfxTag, _data);

            List<ParticleSystem> _list = new List<ParticleSystem>(_data.InitialPoolSize);
            for (int j = 0; j < _data.InitialPoolSize; j++)
            {
                ParticleSystem _newInstance = CreateNewInstance(_data);
                if (null != _newInstance)
                    _list.Add(_newInstance);
            }

            poolDictionary.Add(_data.VfxTag, _list);
        }

        isInitialized = true;
    }

    /// <summary>
    /// 외부에서 명시적으로 풀을 미리 예열(생성)할 때 사용합니다.
    /// </summary>
    public void Prewarm()
    {
        Initialize();
    }

    /// <summary>
    /// 지정한 태그의 풀에서 사용 가능한(비활성화된) 이펙트 컴포넌트를 반환합니다.
    /// 해당 태그의 모든 이펙트가 사용 중일 경우, 설정을 확인하여 동적으로 풀을 늘립니다.
    /// </summary>
    public ParticleSystem Get(string _tag)
    {
        if (false == isInitialized)
            Initialize();

        if (null == poolDictionary || null == configDictionary)
            return null;

        if (false == poolDictionary.TryGetValue(_tag, out List<ParticleSystem> _poolList))
            return null;

        if (false == configDictionary.TryGetValue(_tag, out VFXPoolData _config))
            return null;

        int _count = _poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = _poolList[i];
            if (null != _effect && false == _effect.gameObject.activeSelf)
            {
                VFXPoolInstanceHelper _helper = _effect.GetComponent<VFXPoolInstanceHelper>();
                if (null != _helper && null != _helper.TargetTransform)
                    _helper.TargetTransform.SetParent(transform);
                else
                    _effect.transform.SetParent(transform);

                return _effect;
            }
        }

        if (false == _config.AllowDynamicExpansion)
            return null;

        if (_poolList.Count >= _config.MaxPoolSize)
            return null;

        ParticleSystem _dynamicInstance = CreateNewInstance(_config);
        if (null != _dynamicInstance)
            _poolList.Add(_dynamicInstance);

        return _dynamicInstance;
    }

    /// <summary>
    /// 지정한 태그의 사용하지 않는 이펙트를 바로 꺼내 지정된 위치와 회전값으로 재생합니다.
    /// </summary>
    public ParticleSystem Play(string _tag, Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {
        ParticleSystem _effect = Get(_tag);
        if (null == _effect)
            return null;

        Play(_effect, _position, _rotation, _parent);

        return _effect;
    }

    /// <summary>
    /// 이미 가져온 특정 이펙트 인스턴스의 부모, 위치, 회전값을 설정하고 즉시 재생합니다.
    /// </summary>
    public void Play(ParticleSystem _effect, Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {
        if (null == _effect)
            return;

        VFXPoolInstanceHelper _helper = _effect.GetComponent<VFXPoolInstanceHelper>();
        Transform _target = (null != _helper && null != _helper.TargetTransform) ? _helper.TargetTransform : _effect.transform;

        _target.SetParent(_parent);
        _target.position = _position;
        _target.rotation = _rotation;

        _target.gameObject.SetActive(true);
        if (_target != _effect.transform)
            _effect.gameObject.SetActive(true);

        _effect.Play(true);
    }

    /// <summary>
    /// 재생 중인 특정 이펙트의 재생을 멈추고 풀에 반환(비활성화)합니다.
    /// _immediate가 true이면 즉시 끄고 반환하며, false이면 방출만 중지한 뒤 파티클이 모두 사라지면 자동으로 반환됩니다.
    /// </summary>
    public void Stop(ParticleSystem _effect, bool _immediate = false)
    {
        if (null == _effect)
            return;

        if (null == masterList)
            return;

        if (true == masterList.Contains(_effect))
        {
            VFXPoolInstanceHelper _helper = _effect.GetComponent<VFXPoolInstanceHelper>();
            if (null != _helper)
            {
                _helper.Stop(_immediate);
            }
            else
            {
                if (true == _immediate)
                {
                    _effect.Stop(true);
                    _effect.Clear(true);
                    _effect.transform.SetParent(transform);
                    _effect.gameObject.SetActive(false);
                }
                else
                {
                    _effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }

    /// <summary>
    /// 현재 활성화되어 재생 중인 모든 이펙트를 즉시 중지하고 풀로 일괄 반환합니다.
    /// </summary>
    public void StopAll()
    {
        if (null == masterList)
            return;

        int _count = masterList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = masterList[i];
            if (null != _effect && true == _effect.gameObject.activeSelf)
            {
                VFXPoolInstanceHelper _helper = _effect.GetComponent<VFXPoolInstanceHelper>();
                if (null != _helper)
                    _helper.ReturnToPool();
                else
                {
                    _effect.Stop(true);
                    _effect.Clear(true);
                    _effect.transform.SetParent(transform);
                    _effect.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 풀링된 모든 이펙트 오브젝트를 파괴하고 풀 데이터를 안전하게 정리합니다.
    /// </summary>
    public void Clear()
    {
        if (null == masterList)
            return;

        int _count = masterList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = masterList[i];
            if (null != _effect)
            {
                VFXPoolInstanceHelper _helper = _effect.GetComponent<VFXPoolInstanceHelper>();
                if (null != _helper && null != _helper.TargetTransform && _helper.TargetTransform != _effect.transform)
                    Destroy(_helper.TargetTransform.gameObject);
                else
                    Destroy(_effect.gameObject);
            }
        }

        if (null != masterList)
            masterList.Clear();

        if (null != poolDictionary)
            poolDictionary.Clear();

        if (null != configDictionary)
            configDictionary.Clear();

        isInitialized = false;
    }

    /// <summary>
    /// 특정 이펙트 인스턴스의 하위 렌더러를 포함한 소팅 레이어 이름과 순서를 설정합니다.
    /// </summary>
    public void SetSortingSettings(ParticleSystem _effect, string _layerName, int _order)
    {
        ApplySortingSettings(_effect, _layerName, _order);
    }

    /// <summary>
    /// 지정한 태그의 풀에 존재하는 모든 이펙트 인스턴스의 소팅 레이어와 순서를 설정합니다.
    /// </summary>
    public void SetSortingSettingsOfTag(string _tag, string _layerName, int _order)
    {
        if (null == poolDictionary)
            return;

        if (false == poolDictionary.TryGetValue(_tag, out List<ParticleSystem> _poolList))
            return;

        int _count = _poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = _poolList[i];
            if (null != _effect)
                ApplySortingSettings(_effect, _layerName, _order);
        }
    }

    /// <summary>
    /// 풀링되어 생성된 모든 이펙트 인스턴스의 소팅 레이어와 순서를 일괄 설정합니다.
    /// </summary>
    public void SetSortingSettingsAll(string _layerName, int _order)
    {
        if (null == masterList)
            return;

        int _count = masterList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = masterList[i];
            if (null != _effect)
                ApplySortingSettings(_effect, _layerName, _order);
        }
    }

    /// <summary>
    /// 특정 이펙트 인스턴스 및 하위 파티클 시스템들의 시작 색상을 설정합니다.
    /// </summary>
    public void SetStartColor(ParticleSystem _effect, Color _color)
    {
        ApplyStartColor(_effect, _color);
    }

    /// <summary>
    /// 지정한 태그의 풀에 존재하는 모든 이펙트 인스턴스들의 시작 색상을 설정합니다.
    /// </summary>
    public void SetStartColorOfTag(string _tag, Color _color)
    {
        if (null == poolDictionary)
            return;

        if (false == poolDictionary.TryGetValue(_tag, out List<ParticleSystem> _poolList))
            return;

        int _count = _poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = _poolList[i];
            if (null != _effect)
                ApplyStartColor(_effect, _color);
        }
    }

    /// <summary>
    /// 풀링되어 생성된 모든 이펙트 인스턴스들의 시작 색상을 일괄 설정합니다.
    /// </summary>
    public void SetStartColorAll(Color _color)
    {
        if (null == masterList)
            return;

        int _count = masterList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = masterList[i];
            if (null != _effect)
                ApplyStartColor(_effect, _color);
        }
    }


    // 내부 로직

    /// <summary>
    /// 풀 설정을 기반으로 새로운 이펙트 인스턴스를 생성하고 초기 설정을 수행합니다.
    /// </summary>
    private ParticleSystem CreateNewInstance(VFXPoolData _config)
    {
        if (null == _config || null == _config.EffectPrefab)
            return null;

        ParticleSystem _prefab = _config.EffectPrefab;

        bool _useUI = isUIComponent;
        if (true == _useUI)
        {
            Canvas _canvas = GetComponentInParent<Canvas>();
            if (null != _canvas && RenderMode.WorldSpace == _canvas.renderMode)
                _useUI = false;
        }

        if (true == _useUI)
        {
            GameObject _uiParentGo = new GameObject(_prefab.name + "_UIParent", typeof(RectTransform), typeof(CanvasRenderer));
            if (null == _uiParentGo)
                return null;

            _uiParentGo.transform.SetParent(transform, false);
            _uiParentGo.SetActive(false);

            Coffee.UIExtensions.UIParticle _uiParticle = _uiParentGo.AddComponent<Coffee.UIExtensions.UIParticle>();
            if (null != _uiParticle)
                _uiParticle.scale = _config.UiParticleScale;

            ParticleSystem _newInstance = Instantiate(_prefab, _uiParentGo.transform, false);
            if (null == _newInstance)
            {
                Destroy(_uiParentGo);
                return null;
            }

            if (null != _uiParticle)
                _uiParticle.RefreshParticles();

            _newInstance.gameObject.SetActive(false);

            VFXPoolInstanceHelper _helper = _newInstance.gameObject.AddComponent<VFXPoolInstanceHelper>();
            if (null != _helper)
                _helper.Initialize(transform, _uiParentGo.transform);

            var _main = _newInstance.main;
            _main.stopAction = ParticleSystemStopAction.Callback;

            if (null != masterList)
                masterList.Add(_newInstance);

            return _newInstance;
        }
        else
        {
            ParticleSystem _newInstance = Instantiate(_prefab, transform, false);
            if (null == _newInstance)
                return null;

            _newInstance.gameObject.SetActive(false);

            VFXPoolInstanceHelper _helper = _newInstance.gameObject.AddComponent<VFXPoolInstanceHelper>();
            if (null != _helper)
                _helper.Initialize(transform);

            var _main = _newInstance.main;
            _main.stopAction = ParticleSystemStopAction.Callback;

            if (null != masterList)
                masterList.Add(_newInstance);

            return _newInstance;
        }
    }

    /// <summary>
    /// 이펙트 및 하위 자식들의 모든 렌더러 소팅 레이어 이름과 순서를 설정합니다.
    /// </summary>
    private void ApplySortingSettings(ParticleSystem _effect, string _layerName, int _order)
    {
        if (null == _effect)
            return;

        Renderer[] _renderers = _effect.GetComponentsInChildren<Renderer>(true);
        if (null == _renderers)
            return;

        int _count = _renderers.Length;
        for (int i = 0; i < _count; i++)
        {
            Renderer _renderer = _renderers[i];
            if (null != _renderer)
            {
                _renderer.sortingLayerName = _layerName;
                _renderer.sortingOrder = _order;
            }
        }
    }

    /// <summary>
    /// 이펙트 및 하위 자식들의 모든 파티클 시스템 시작 색상을 변경합니다.
    /// </summary>
    private void ApplyStartColor(ParticleSystem _effect, Color _color)
    {
        if (null == _effect)
            return;

        ParticleSystem[] _particles = _effect.GetComponentsInChildren<ParticleSystem>(true);
        if (null == _particles)
            return;

        int _count = _particles.Length;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _particle = _particles[i];
            if (null != _particle)
            {
                var _main = _particle.main;
                _main.startColor = _color;
            }
        }
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        Clear();
    }
}

public class VFXPoolInstanceHelper : MonoBehaviour
{
    // 내부 의존성
    private Transform originalParent;
    private Transform targetTransform;
    private ParticleSystem particleSys;
    private bool isReturning;
    private Coroutine stopCoroutine;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(Transform _parent, Transform _target = null)
    {
        originalParent = _parent;
        targetTransform = (null != _target) ? _target : transform;
        particleSys = GetComponent<ParticleSystem>();
        isReturning = false;
    }

    public void Stop(bool _immediate)
    {
        if (null != stopCoroutine)
        {
            StopCoroutine(stopCoroutine);
            stopCoroutine = null;
        }

        if (true == _immediate || false == gameObject.activeInHierarchy)
            ReturnToPool();
        else
        {
            if (null != particleSys)
            {
                particleSys.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                stopCoroutine = StartCoroutine(CoWaitAndReturnToPool());
            }
        }
    }

    private System.Collections.IEnumerator CoWaitAndReturnToPool()
    {
        float _maxLifetime = 0f;
        if (null != particleSys)
        {
            var _main = particleSys.main;
            _maxLifetime = _main.startLifetime.constantMax;

            ParticleSystem[] _children = particleSys.GetComponentsInChildren<ParticleSystem>(true);
            if (null != _children)
            {
                int _len = _children.Length;
                for (int i = 0; i < _len; i++)
                {
                    ParticleSystem _child = _children[i];
                    if (null != _child)
                    {
                        var _childMain = _child.main;
                        float _childLifetime = _childMain.startLifetime.constantMax;
                        if (_childLifetime > _maxLifetime)
                            _maxLifetime = _childLifetime;
                    }
                }
            }
        }

        yield return new WaitForSeconds(_maxLifetime + 0.2f);
        ReturnToPool();
        stopCoroutine = null;
    }

    public void ReturnToPool()
    {
        if (true == isReturning)
            return;

        isReturning = true;

        if (null != stopCoroutine)
        {
            StopCoroutine(stopCoroutine);
            stopCoroutine = null;
        }

        if (null != particleSys)
        {
            if (true == particleSys.isPlaying)
                particleSys.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (null != originalParent && null != targetTransform)
        {
            if (targetTransform.parent != originalParent)
                targetTransform.SetParent(originalParent);
        }

        if (null != targetTransform && true == targetTransform.gameObject.activeSelf)
        {
            targetTransform.gameObject.SetActive(false);
            if (targetTransform != transform)
                gameObject.SetActive(false);
        }

        isReturning = false;
    }

    public Transform TargetTransform => targetTransform;


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnParticleSystemStopped()
    {
        ReturnToPool();
    }

    private void OnDestroy()
    {
        if (null != targetTransform && transform != targetTransform)
        {
            Destroy(targetTransform.gameObject);
        }
    }
}
