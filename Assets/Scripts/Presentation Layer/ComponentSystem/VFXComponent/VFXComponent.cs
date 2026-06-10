using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// 이펙트 프리팹을 바인딩하여 로컬 오브젝트 풀링을 수행하는 VFX 컴포넌트입니다.
/// </summary>
public class VFXComponent : MonoBehaviour
{
    // 외부 의존성
    [Header("Pool Settings")]
    [SerializeField] private ParticleSystem effectPrefab;
    [SerializeField] private int initialPoolSize = 5;

    [Header("Expansion Settings")]
    [SerializeField] private bool allowDynamicExpansion = true;
    [SerializeField] [ShowIf("allowDynamicExpansion")] private int maxPoolSize = 10;

    // 내부 의존성
    private List<ParticleSystem> poolList;
    private bool isInitialized = false;


    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// 풀을 초기화하고 지정된 크기만큼 이펙트를 미리 생성합니다.
    /// </summary>
    public void Initialize()
    {
        if (true == isInitialized)
            return;

        if (null == effectPrefab)
            return;

        poolList = new List<ParticleSystem>(initialPoolSize);

        for (int i = 0; i < initialPoolSize; i++)
        {
            ParticleSystem _newInstance = CreateNewInstance();
            if (null != _newInstance)
                poolList.Add(_newInstance);
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
    /// 풀에서 사용 가능한(비활성화된) 이펙트 컴포넌트를 반환합니다.
    /// 모든 이펙트가 사용 중일 경우, 동적으로 풀을 늘려 새 이펙트를 생성합니다.
    /// </summary>
    public ParticleSystem Get()
    {
        if (false == isInitialized)
            Initialize();

        if (null == poolList)
            return null;

        int _count = poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = poolList[i];
            if (null != _effect && false == _effect.gameObject.activeSelf)
                return _effect;
        }

        if (false == allowDynamicExpansion)
            return null;

        if (poolList.Count >= maxPoolSize)
            return null;

        ParticleSystem _dynamicInstance = CreateNewInstance();
        if (null != _dynamicInstance)
            poolList.Add(_dynamicInstance);

        return _dynamicInstance;
    }

    /// <summary>
    /// 사용하지 않는 이펙트를 바로 꺼내 지정된 위치와 회전값으로 재생합니다.
    /// </summary>
    public ParticleSystem Play(Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {
        ParticleSystem _effect = Get();
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

        if (true == poolList.Contains(_effect))
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
        if (null == poolList)
            return;

        int _count = poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = poolList[i];
            if (null != _effect && true == _effect.gameObject.activeSelf)
            {
                _effect.Stop(true);
                _effect.Clear(true);
                _effect.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 풀링된 모든 이펙트 오브젝트를 파괴하고 풀을 안전하게 정리합니다.
    /// </summary>
    public void Clear()
    {
        if (null == poolList)
            return;

        int _count = poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = poolList[i];
            if (null != _effect)
                Destroy(_effect.gameObject);
        }
        poolList.Clear();
        isInitialized = false;
    }


    // 내부 로직

    /// <summary>
    /// 프리팹을 기반으로 새로운 이펙트 인스턴스를 생성하고 초기 설정을 수행합니다.
    /// </summary>
    private ParticleSystem CreateNewInstance()
    {
        if (null == effectPrefab)
            return null;

        ParticleSystem _newInstance = Instantiate(effectPrefab, transform);
        if (null == _newInstance)
            return null;

        _newInstance.gameObject.SetActive(false);

        var _main = _newInstance.main;
        _main.stopAction = ParticleSystemStopAction.Disable;

        return _newInstance;
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
