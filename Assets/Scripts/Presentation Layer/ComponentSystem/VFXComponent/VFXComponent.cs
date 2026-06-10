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

    // 퍼블릭 초기화 및 제어 메서드
    public string VfxTag => vfxTag;
    public ParticleSystem EffectPrefab => effectPrefab;
    public int InitialPoolSize => initialPoolSize;
    public bool AllowDynamicExpansion => allowDynamicExpansion;
    public int MaxPoolSize => maxPoolSize;
}

/// <summary>
/// 여러 종류의 이펙트 프리팹을 태그별로 바인딩하여 각각 로컬 오브젝트 풀링을 수행하는 VFX 컴포넌트입니다.
/// </summary>
public class VFXComponent : MonoBehaviour
{
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
                ParticleSystem _newInstance = CreateNewInstance(_data.EffectPrefab);
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
                return _effect;
        }

        if (false == _config.AllowDynamicExpansion)
            return null;

        if (_poolList.Count >= _config.MaxPoolSize)
            return null;

        ParticleSystem _dynamicInstance = CreateNewInstance(_config.EffectPrefab);
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

        _effect.transform.SetParent(_parent);
        _effect.transform.position = _position;
        _effect.transform.rotation = _rotation;

        _effect.gameObject.SetActive(true);
        _effect.Play(true);

        return _effect;
    }

    /// <summary>
    /// 재생 중인 특정 이펙트의 재생을 멈추고 풀에 반환(비활성화)합니다.
    /// </summary>
    public void Stop(ParticleSystem _effect)
    {
        if (null == _effect)
            return;

        if (null == masterList)
            return;

        if (true == masterList.Contains(_effect))
        {
            _effect.Stop(true);
            _effect.Clear(true);
            _effect.gameObject.SetActive(false);
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
                _effect.Stop(true);
                _effect.Clear(true);
                _effect.gameObject.SetActive(false);
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
                Destroy(_effect.gameObject);
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


    // 내부 로직

    /// <summary>
    /// 프리팹을 기반으로 새로운 이펙트 인스턴스를 생성하고 초기 설정을 수행합니다.
    /// </summary>
    private ParticleSystem CreateNewInstance(ParticleSystem _prefab)
    {
        if (null == _prefab)
            return null;

        ParticleSystem _newInstance = Instantiate(_prefab, transform);
        if (null == _newInstance)
            return null;

        _newInstance.gameObject.SetActive(false);

        var _main = _newInstance.main;
        _main.stopAction = ParticleSystemStopAction.Disable;

        if (null != masterList)
            masterList.Add(_newInstance);

        return _newInstance;
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
