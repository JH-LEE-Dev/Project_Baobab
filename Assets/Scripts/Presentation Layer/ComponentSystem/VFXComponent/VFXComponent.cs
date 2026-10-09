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

[System.Serializable]
public struct VFXPlaySettings
{
    // 외부 의존성
    [SerializeField] public string vfxTag;
    [SerializeField] public Vector3 position;
    [SerializeField] public Quaternion rotation;
    [SerializeField] public Transform parent;
    [SerializeField] public bool withChildren;
    [SerializeField] public bool overrideColor;
    [SerializeField] public bool overrideChildrenColor;
    [SerializeField] public ParticleSystem.MinMaxGradient startColor;
    [SerializeField] public bool overrideSorting;
    [SerializeField] public string sortingLayerName;
    [SerializeField] public int sortingOrder;

    // 퍼블릭 초기화 및 제어 메서드
    public string VfxTag { get => vfxTag; set => vfxTag = value; }
    public Vector3 Position { get => position; set => position = value; }
    public Quaternion Rotation { get => rotation; set => rotation = value; }
    public Transform Parent { get => parent; set => parent = value; }
    public bool WithChildren { get => withChildren; set => withChildren = value; }
    public bool OverrideColor { get => overrideColor; set => overrideColor = value; }
    public bool OverrideChildrenColor { get => overrideChildrenColor; set => overrideChildrenColor = value; }
    public ParticleSystem.MinMaxGradient StartColor { get => startColor; set => startColor = value; }
    public bool OverrideSorting { get => overrideSorting; set => overrideSorting = value; }
    public string SortingLayerName { get => sortingLayerName; set => sortingLayerName = value; }
    public int SortingOrder { get => sortingOrder; set => sortingOrder = value; }

    public VFXPlaySettings(string _tag, Vector3 _pos, Quaternion _rot, Transform _parent = null)
    {
        vfxTag = _tag;
        position = _pos;
        rotation = _rot;
        parent = _parent;
        withChildren = true;
        overrideColor = false;
        overrideChildrenColor = false;
        startColor = new ParticleSystem.MinMaxGradient(Color.white);
        overrideSorting = false;
        sortingLayerName = string.Empty;
        sortingOrder = 0;
    }

    public VFXPlaySettings(string _tag, Vector3 _pos, Quaternion _rot, ParticleSystem.MinMaxGradient _color, Transform _parent = null)
    {
        vfxTag = _tag;
        position = _pos;
        rotation = _rot;
        parent = _parent;
        withChildren = true;
        overrideColor = true;
        overrideChildrenColor = false;
        startColor = _color;
        overrideSorting = false;
        sortingLayerName = string.Empty;
        sortingOrder = 0;
    }

    public VFXPlaySettings(string _tag, Vector3 _pos, Quaternion _rot, ParticleSystem.MinMaxGradient _color, bool _overrideChildrenColor, Transform _parent = null)
    {
        vfxTag = _tag;
        position = _pos;
        rotation = _rot;
        parent = _parent;
        withChildren = true;
        overrideColor = true;
        overrideChildrenColor = _overrideChildrenColor;
        startColor = _color;
        overrideSorting = false;
        sortingLayerName = string.Empty;
        sortingOrder = 0;
    }

    public VFXPlaySettings(string _tag, Vector3 _pos, Quaternion _rot, int _sortingOrder, Transform _parent = null)
    {
        vfxTag = _tag;
        position = _pos;
        rotation = _rot;
        parent = _parent;
        withChildren = true;
        overrideColor = false;
        overrideChildrenColor = false;
        startColor = new ParticleSystem.MinMaxGradient(Color.white);
        overrideSorting = true;
        sortingLayerName = string.Empty;
        sortingOrder = _sortingOrder;
    }

    public VFXPlaySettings(string _tag, Vector3 _pos, Quaternion _rot, string _sortingLayerName, int _sortingOrder, Transform _parent = null)
    {
        vfxTag = _tag;
        position = _pos;
        rotation = _rot;
        parent = _parent;
        withChildren = true;
        overrideColor = false;
        overrideChildrenColor = false;
        startColor = new ParticleSystem.MinMaxGradient(Color.white);
        overrideSorting = true;
        sortingLayerName = _sortingLayerName;
        sortingOrder = _sortingOrder;
    }

    public VFXPlaySettings(string _tag, Vector3 _pos, Quaternion _rot, ParticleSystem.MinMaxGradient _color, bool _overrideChildrenColor, string _sortingLayerName, int _sortingOrder, Transform _parent = null)
    {
        vfxTag = _tag;
        position = _pos;
        rotation = _rot;
        parent = _parent;
        withChildren = true;
        overrideColor = true;
        overrideChildrenColor = _overrideChildrenColor;
        startColor = _color;
        overrideSorting = true;
        sortingLayerName = _sortingLayerName;
        sortingOrder = _sortingOrder;
    }
}

/// <summary>
/// 태그별 풀 목록에서 "이 위치 앞의 인스턴스는 전부 꺼낼 수 없다(사용 중이거나 파괴됨)"를 뜻하는 위치.
/// VFXComponent.Get이 이 위치부터 찾아도 맨 앞부터 훑었을 때와 같은 인스턴스(가장 앞의 비활성 인스턴스)를 고른다.
/// 인스턴스가 꺼질 때마다(VFXPoolInstanceHelper.NotifyPoolSlotFree) 그 위치까지 내려온다.
/// </summary>
public sealed class VFXPoolFreeHint
{
    public int firstMaybeFree;
}

/// <summary>
/// 여러 종류의 이펙트 프리팹을 태그별로 바인딩하여 각각 로컬 오브젝트 풀링을 수행하는 VFX 컴포넌트입니다.
/// </summary>
public class VFXComponent : MonoBehaviour
{
    [Header("UI Canvas Settings")]
    [SerializeField] private bool isUIComponent = false;
    [SerializeField] private bool initializeOnAwake = true;

    // 외부 의존성
    [Header("VFX Pool List")]
    [SerializeField] private List<VFXPoolData> vfxPoolDataList;

    // 내부 의존성
    private Dictionary<string, List<ParticleSystem>> poolDictionary;
    private Dictionary<string, VFXPoolData> configDictionary;
    private List<ParticleSystem> masterList;
    // 풀 인스턴스 → 헬퍼 조회표. Get/Play/Stop마다 GetComponent를 다시 하지 않도록 생성 시 한 번만 채운다.
    private Dictionary<ParticleSystem, VFXPoolInstanceHelper> helperLookup;
    // 태그별 "앞쪽은 전부 사용 중" 위치(VFXPoolFreeHint 참조). 바닥에 놓인 보석 원목마다 루프 Shiny가 켜져 있어,
    // 매번 맨 앞부터 훑으면 원목이 하나 착지할 때마다 켜진 Shiny 수만큼 activeSelf를 읽었다.
    private Dictionary<string, VFXPoolFreeHint> poolFreeHints;
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
        helperLookup = new Dictionary<ParticleSystem, VFXPoolInstanceHelper>();
        poolFreeHints = new Dictionary<string, VFXPoolFreeHint>(_dataCount);

        for (int i = 0; i < _dataCount; i++)
        {
            VFXPoolData _data = vfxPoolDataList[i];
            if (null == _data || string.IsNullOrEmpty(_data.VfxTag) || null == _data.EffectPrefab)
                continue;

            if (true == configDictionary.ContainsKey(_data.VfxTag))
                continue;

            configDictionary.Add(_data.VfxTag, _data);

            List<ParticleSystem> _list = new List<ParticleSystem>(_data.InitialPoolSize);
            VFXPoolFreeHint _hint = new VFXPoolFreeHint();
            for (int j = 0; j < _data.InitialPoolSize; j++)
            {
                ParticleSystem _newInstance = CreateNewInstance(_data);
                if (null != _newInstance)
                {
                    BindPoolSlot(_newInstance, _hint, _list.Count);
                    _list.Add(_newInstance);
                }
            }

            poolDictionary.Add(_data.VfxTag, _list);
            poolFreeHints.Add(_data.VfxTag, _hint);
        }

        isInitialized = true;
    }

    /// <summary>
    /// 다른 VFXComponent의 직렬화 설정만 복사해 하나의 공용 런타임 풀을 구성합니다.
    /// 풀 인스턴스가 만들어지기 전에 한 번만 호출해야 합니다.
    /// </summary>
    public bool InitializeFrom(VFXComponent _template)
    {
        if (isInitialized)
            return true;

        if (_template == null)
            return false;

        isUIComponent = _template.isUIComponent;
        vfxPoolDataList = _template.vfxPoolDataList != null
            ? new List<VFXPoolData>(_template.vfxPoolDataList)
            : new List<VFXPoolData>();

        Initialize();
        return isInitialized;
    }

    /// <summary>
    /// 외부에서 명시적으로 풀을 미리 예열(생성)할 때 사용합니다.
    /// </summary>
    public void Prewarm()
    {
        Initialize();
    }

    /// <summary>
    /// 태그에 바인딩된 프리팹과 풀 상한을 돌려줍니다. 공유 이미터(SharedBurstEmitter)가 같은 프리팹을 인스턴스 하나로 재생할 때 씁니다.
    /// </summary>
    public bool TryGetPoolConfig(string _tag, out ParticleSystem _prefab, out int _maxPoolSize)
    {
        _prefab = null;
        _maxPoolSize = 0;

        if (false == isInitialized)
            Initialize();

        if (null == configDictionary || string.IsNullOrEmpty(_tag))
            return false;

        if (false == configDictionary.TryGetValue(_tag, out VFXPoolData _config) || null == _config)
            return false;

        _prefab = _config.EffectPrefab;
        _maxPoolSize = _config.AllowDynamicExpansion ? _config.MaxPoolSize : _config.InitialPoolSize;
        return null != _prefab;
    }

    /// <summary>
    /// 지정한 태그의 풀에서 사용 가능한(비활성화된) 이펙트 컴포넌트를 반환합니다.
    /// 해당 태그의 모든 이펙트가 사용 중일 경우, 설정을 확인하여 동적으로 풀을 늘립니다.
    /// </summary>
    public ParticleSystem Get(string _tag)
    {
        if (true == VFXPoolInstanceHelper.IsQuitting)
            return null;

        if (false == isInitialized)
            Initialize();

        if (null == poolDictionary || null == configDictionary)
            return null;

        if (false == poolDictionary.TryGetValue(_tag, out List<ParticleSystem> _poolList))
            return null;

        if (false == configDictionary.TryGetValue(_tag, out VFXPoolData _config))
            return null;

        // _hint.firstMaybeFree 앞은 전부 꺼낼 수 없는 인스턴스이므로 거기서부터 찾는다(VFXPoolFreeHint 참조).
        // 찾으면 그 위치를 그대로 남긴다(i+1이 아니다) - 꺼낸 쪽이 재생하지 않고 끝나도 다음 Get이 다시 고를 수 있게.
        poolFreeHints.TryGetValue(_tag, out VFXPoolFreeHint _hint);

        int _count = _poolList.Count;
        int _start = (null != _hint) ? Mathf.Clamp(_hint.firstMaybeFree, 0, _count) : 0;
        for (int i = _start; i < _count; i++)
        {
            ParticleSystem _effect = _poolList[i];
            if (null != _effect && false == _effect.gameObject.activeSelf)
            {
                if (null != _hint)
                    _hint.firstMaybeFree = i;

                VFXPoolInstanceHelper _helper = GetHelper(_effect);
                if (null != _helper && null != _helper.TargetTransform)
                {
                    // 직전 반납이 비활성 상태에서 일어났다면 재부모화가 지연 예약되어 있을 수 있다.
                    // 그대로 두면 이 인스턴스가 새 주인에게 붙어 재생을 시작한 뒤 예약분이 터져
                    // 주인에게서 떨어져 나가므로(월드 위치 고정), 꺼내는 시점에 반드시 취소한다.
                    _helper.CancelPendingReparent();
                    _helper.TargetTransform.SetParent(transform);
                }
                else
                    _effect.transform.SetParent(transform);

                return _effect;
            }
        }

        if (null != _hint)
            _hint.firstMaybeFree = _count;

        // 확장을 끈 풀은 설계상 하드 캡이다(OverheatLoop 1개, DroneCharging 3개 등). 예전처럼 null을 돌려준다.
        if (false == _config.AllowDynamicExpansion)
            return null;

        if (_poolList.Count < _config.MaxPoolSize)
        {
            ParticleSystem _dynamicInstance = CreateNewInstance(_config);
            if (null != _dynamicInstance)
            {
                BindPoolSlot(_dynamicInstance, _hint, _poolList.Count);
                _poolList.Add(_dynamicInstance);
            }

            return _dynamicInstance;
        }

        // 루프 이펙트(LogItem의 Shiny, 드론 과열 아우라 등)는 호출부가 인스턴스를 들고 있다가 나중에 Stop한다.
        // 여기서 뺏어오면 새 주인에게 붙은 이펙트를 옛 주인의 Stop이 꺼버리므로, 예전처럼 null을 돌려준다.
        if (true == _config.EffectPrefab.main.loop)
            return null;

        // 단발 이펙트 풀이 전부 재생 중이고 상한까지 찼으면 가장 오래 재생 중인 인스턴스를 회수해 재사용한다.
        // 예전엔 여기서 null을 돌려줬는데, 호출부가 전부 반환값을 버리고 로그도 없어서 광역 공격으로
        // 벌목 이펙트가 한꺼번에 몰리는 순간(과열 쇼크웨이브의 인접 즉사, 부메랑 반복 타격 등)
        // 상한을 넘는 이펙트가 아무 표시 없이 사라졌다. 단발 이펙트라 가장 오래된 것이 조금 일찍
        // 끊기는 쪽이 새 이펙트가 통째로 빠지는 것보다 낫다.
        return RecycleOldestActive(_poolList);
    }

    /// <summary>
    /// 풀에서 가장 오래전에 재생을 시작한 활성 인스턴스를 즉시 풀로 되돌리고 반환합니다.
    /// 회수된 인스턴스는 비활성 + 풀 부모 상태라 바로 Play로 넘길 수 있습니다.
    /// </summary>
    private ParticleSystem RecycleOldestActive(List<ParticleSystem> _poolList)
    {
        ParticleSystem _oldest = null;
        VFXPoolInstanceHelper _oldestHelper = null;
        float _oldestTime = float.MaxValue;

        int _count = _poolList.Count;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _effect = _poolList[i];
            if (null == _effect)
                continue;

            VFXPoolInstanceHelper _helper = GetHelper(_effect);
            if (null == _helper)
                continue;

            if (_helper.LastPlayTime < _oldestTime)
            {
                _oldestTime = _helper.LastPlayTime;
                _oldest = _effect;
                _oldestHelper = _helper;
            }
        }

        if (null == _oldest)
            return null;

        _oldestHelper.ReturnToPool();

        // ReturnToPool이 비활성 상태에서 재부모화를 지연 예약했을 수 있으니 대여 시점에 취소한다(위 루프와 동일).
        _oldestHelper.CancelPendingReparent();
        if (null != _oldestHelper.TargetTransform)
            _oldestHelper.TargetTransform.SetParent(transform);

        return _oldest;
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
    /// 지정된 설정 구조체 데이터를 기반으로 이펙트를 꺼내어 즉시 재생합니다.
    /// </summary>
    public ParticleSystem Play(VFXPlaySettings _settings)
    {
        ParticleSystem _effect = Get(_settings.VfxTag);
        if (null == _effect)
            return null;

        Play(_effect, _settings);

        return _effect;
    }

    /// <summary>
    /// 이미 가져온 특정 이펙트 인스턴스의 부모, 위치, 회전값을 설정하고 즉시 재생합니다.
    /// </summary>
    public void Play(ParticleSystem _effect, Vector3 _position, Quaternion _rotation, Transform _parent = null)
    {
        if (null == _effect || true == VFXPoolInstanceHelper.IsQuitting)
            return;

        VFXPoolInstanceHelper _helper = GetHelper(_effect);
        Transform _target = (null != _helper && null != _helper.TargetTransform) ? _helper.TargetTransform : _effect.transform;

        _target.SetParent(_parent);
        RestoreLocalScaleIfDetached(_helper, _target, _parent);
        _target.position = _position;
        _target.rotation = _rotation;

        _target.gameObject.SetActive(true);
        if (_target != _effect.transform)
            _effect.gameObject.SetActive(true);

        if (null != _helper)
            _helper.MarkPlayed();

        _effect.Play(true);
    }

    /// <summary>
    /// 이미 가져온 특정 이펙트 인스턴스를 지정된 설정 구조체 정보에 맞춰 가공 후 즉시 재생합니다.
    /// </summary>
    public void Play(ParticleSystem _effect, VFXPlaySettings _settings)
    {
        if (null == _effect || true == VFXPoolInstanceHelper.IsQuitting)
            return;

        VFXPoolInstanceHelper _helper = GetHelper(_effect);
        Transform _target = (null != _helper && null != _helper.TargetTransform) ? _helper.TargetTransform : _effect.transform;

        _target.SetParent(_settings.Parent);
        RestoreLocalScaleIfDetached(_helper, _target, _settings.Parent);
        _target.position = _settings.Position;
        _target.rotation = _settings.Rotation;

        _target.gameObject.SetActive(true);
        if (_target != _effect.transform)
            _effect.gameObject.SetActive(true);

        if (null != _helper)
            _helper.MarkPlayed();

        // 소팅 오버라이드 처리 (재생 전에 먼저 적용)
        if (true == _settings.OverrideSorting)
        {
            ApplySortingSettings(_effect, _settings.SortingLayerName, _settings.SortingOrder);
        }

        // 색상 오버라이드 처리
        if (true == _settings.OverrideColor)
        {
            var _main = _effect.main;
            _main.startColor = _settings.StartColor;

            // 자식 파티클 색상 덮어쓰기 여부 판정
            if (true == _settings.OverrideChildrenColor)
            {
                ParticleSystem[] _children = (null != _helper && null != _helper.ChildSystems) ? _helper.ChildSystems : _effect.GetComponentsInChildren<ParticleSystem>(true);
                if (null != _children)
                {
                    int _len = _children.Length;
                    for (int i = 0; i < _len; i++)
                    {
                        ParticleSystem _child = _children[i];
                        if (null != _child)
                        {
                            var _childMain = _child.main;
                            _childMain.startColor = _settings.StartColor;
                        }
                    }
                }
            }
        }

        _effect.Play(_settings.WithChildren);
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

        // 조회표에 있으면 풀 인스턴스가 확실하므로 masterList 선형 탐색을 건너뛴다.
        VFXPoolInstanceHelper _helper = null;
        bool _isPooled = (null != helperLookup && true == helperLookup.TryGetValue(_effect, out _helper))
            || true == masterList.Contains(_effect);

        if (true == _isPooled)
        {
            if (null == _helper)
                _helper = _effect.GetComponent<VFXPoolInstanceHelper>();

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
                    ResetPoolFreeHints();
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
                VFXPoolInstanceHelper _helper = GetHelper(_effect);
                if (null != _helper)
                    _helper.ReturnToPool();
                else
                {
                    _effect.Stop(true);
                    _effect.Clear(true);
                    _effect.transform.SetParent(transform);
                    _effect.gameObject.SetActive(false);
                    ResetPoolFreeHints();
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
                VFXPoolInstanceHelper _helper = GetHelper(_effect);
                if (null != _helper && null != _helper.TargetTransform && _helper.TargetTransform != _effect.transform)
                    Destroy(_helper.TargetTransform.gameObject);
                else
                    Destroy(_effect.gameObject);
            }
        }

        if (null != masterList)
            masterList.Clear();

        if (null != helperLookup)
            helperLookup.Clear();

        if (null != poolDictionary)
            poolDictionary.Clear();

        if (null != poolFreeHints)
            poolFreeHints.Clear();

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
    /// 소팅 레이어를 ID로 지정하는 오버로드.
    /// 매 프레임 동기화하는 호출부(LogItem의 Shiny, 발소리 먼지 등)는 Renderer.sortingLayerName 게터가
    /// 호출마다 새 string을 만들기 때문에 이쪽을 사용한다.
    /// </summary>
    public void SetSortingSettings(ParticleSystem _effect, int _layerID, int _order)
    {
        ApplySortingSettings(_effect, _layerID, _order);
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

            ApplyPoolInstanceSettings(_newInstance, _helper);
            RegisterInstance(_newInstance, _helper);

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

            ApplyPoolInstanceSettings(_newInstance, _helper);
            RegisterInstance(_newInstance, _helper);

            return _newInstance;
        }
    }

    /// <summary>
    /// 풀 인스턴스가 반드시 풀로 돌아오도록 파티클 설정을 보정합니다.
    ///
    /// 반납 경로는 루트의 OnParticleSystemStopped 콜백 하나뿐이다. 그런데 프리팹의 컬링 모드가
    /// Pause면 화면 밖에서 재생 중인 이펙트는 시뮬레이션이 멈춰 끝나지 않고, 콜백도 오지 않아
    /// 활성 상태로 영원히 남는다(카메라가 우연히 그 자리를 다시 비추기 전까지). 그 인스턴스는
    /// 풀 상한을 잠식해 다른 이펙트가 못 나오게 만든다.
    ///
    /// 단발(비루프) 이펙트는 수명이 1초 안팎이라 화면 밖에서도 끝까지 돌리는 비용이 사실상 없다.
    /// 루프 이펙트는 화면 밖 일시정지가 의도된 절약이므로 프리팹 설정을 그대로 둔다.
    /// </summary>
    private static void ApplyPoolInstanceSettings(ParticleSystem _root, VFXPoolInstanceHelper _helper)
    {
        var _rootMain = _root.main;
        _rootMain.stopAction = ParticleSystemStopAction.Callback;

        ParticleSystem[] _systems = (null != _helper && null != _helper.ChildSystems) ? _helper.ChildSystems : _root.GetComponentsInChildren<ParticleSystem>(true);
        if (null == _systems)
            return;

        int _count = _systems.Length;
        for (int i = 0; i < _count; i++)
        {
            ParticleSystem _system = _systems[i];
            if (null == _system)
                continue;

            var _main = _system.main;
            if (false == _main.loop)
                _main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        }
    }

    /// <summary>
    /// 인스턴스에 자기가 속한 태그 풀의 위치를 알려, 꺼질 때 그 풀의 탐색 시작 위치를 내릴 수 있게 합니다.
    /// 풀 목록은 뒤에 추가만 되고 순서가 바뀌거나 빠지지 않으므로 위치는 생성 시 한 번 정하면 된다.
    /// </summary>
    private void BindPoolSlot(ParticleSystem _instance, VFXPoolFreeHint _hint, int _index)
    {
        if (null == _hint)
            return;

        VFXPoolInstanceHelper _helper = GetHelper(_instance);
        if (null != _helper)
            _helper.BindPoolSlot(_hint, _index);
        else
            _hint.firstMaybeFree = 0; // 헬퍼가 없으면 꺼짐을 알 수 없으므로 이 풀은 늘 맨 앞부터 찾게 둔다
    }

    /// <summary>
    /// 헬퍼 없이 인스턴스를 끈 경우(어느 풀의 몇 번째인지 모른다) 모든 풀을 맨 앞부터 찾게 되돌립니다.
    /// 생성된 인스턴스에는 항상 헬퍼가 붙으므로 실제로는 거의 지나지 않는 안전망이다.
    /// </summary>
    private void ResetPoolFreeHints()
    {
        if (null == poolFreeHints)
            return;

        foreach (VFXPoolFreeHint _hint in poolFreeHints.Values)
            _hint.firstMaybeFree = 0;
    }

    /// <summary>
    /// 새 풀 인스턴스를 전체 목록과 헬퍼 조회표에 등록합니다.
    /// </summary>
    private void RegisterInstance(ParticleSystem _instance, VFXPoolInstanceHelper _helper)
    {
        if (null != masterList)
            masterList.Add(_instance);

        if (null != helperLookup && null != _helper)
            helperLookup[_instance] = _helper;
    }

    /// <summary>
    /// 풀 인스턴스의 헬퍼를 조회표에서 찾습니다. 풀 밖에서 주입된 인스턴스(또는 Initialize 전)는
    /// 예전과 같이 컴포넌트 조회로 대응합니다.
    /// </summary>
    private VFXPoolInstanceHelper GetHelper(ParticleSystem _effect)
    {
        if (null != helperLookup && true == helperLookup.TryGetValue(_effect, out VFXPoolInstanceHelper _helper))
            return _helper;

        return _effect.GetComponent<VFXPoolInstanceHelper>();
    }

    /// <summary>
    /// 부모 없이(월드에 그대로) 재생하는 경우에 한해, 풀 인스턴스의 로컬 스케일을 프리팹 원본으로 되돌립니다.
    ///
    /// Transform.SetParent(Transform)은 worldPositionStays:true 오버로드라 월드 스케일을 보존하려고
    /// localScale을 다시 계산해 덮어쓴다. 풀 인스턴스가 대기하는 부모(VFXComponent 자신)가 스케일
    /// 애니메이션이 걸린 노드 밑에 있으면(예: 캐릭터/NPC의 Visuals - 아이템 획득 뽀잉 연출이 이 노드를
    /// 비균등 스케일한다), 재생 시 분리(SetParent(null))와 반납 시 재부착(ReturnToPool)의 기준 스케일이
    /// 서로 달라져 왕복이 상쇄되지 않고 오차가 남는다. 풀 인스턴스는 재사용되므로 이 오차가 계속 누적되어
    /// 먼지(Dust) 같은 이펙트가 점점 찌그러진다.
    ///
    /// 부모가 null이면 "월드 크기 = 프리팹 크기"가 자명하므로 원본으로 되돌리면 그만이다.
    /// 반대로 부모를 명시해서 재생하는 경우(HUD/TentUI 등 캔버스 하위, LogItem의 Shiny 등)는
    /// 월드 크기를 유지하는 현재 동작이 의도된 것이므로 절대 건드리지 않는다.
    /// </summary>
    private void RestoreLocalScaleIfDetached(VFXPoolInstanceHelper _helper, Transform _target, Transform _parent)
    {
        if (null != _parent)
            return;

        if (null == _helper || null == _target)
            return;

        // 캐싱된 원본이 없으면(Initialize를 거치지 않은 외부 주입 인스턴스 등) 손대지 않는다.
        if (false == _helper.TryGetOriginalLocalScale(out Vector3 _originalLocalScale))
            return;

        _target.localScale = _originalLocalScale;
    }

    /// <summary>
    /// 이펙트 및 하위 자식들의 모든 렌더러 소팅 레이어 이름과 순서를 설정합니다.
    /// </summary>
    private void ApplySortingSettings(ParticleSystem _effect, string _layerName, int _order)
    {
        if (null == _effect)
            return;

        bool _setLayer = false == string.IsNullOrEmpty(_layerName);
        int _layerID = _setLayer ? SortingLayer.NameToID(_layerName) : -1;

        // 마지막으로 적용한 값과 같으면(매 프레임 동기화 호출부) 렌더러 순회를 통째로 건너뛴다.
        VFXPoolInstanceHelper _helper = GetHelper(_effect);
        if (null != _helper && true == _helper.IsSortingAlreadyApplied(_setLayer, _layerID, _order))
            return;

        Renderer[] _renderers = ResolveChildRenderers(_effect, _helper);
        if (null == _renderers)
            return;

        int _count = _renderers.Length;
        for (int i = 0; i < _count; i++)
        {
            Renderer _renderer = _renderers[i];
            if (null != _renderer)
            {
                if (true == _setLayer)
                {
                    _renderer.sortingLayerName = _layerName;
                }
                _renderer.sortingOrder = _order;
            }
        }

        if (null != _helper)
            _helper.MarkSortingApplied(_setLayer, _layerID, _order);
    }

    /// <summary>
    /// 이펙트 및 하위 자식들의 모든 렌더러 소팅 레이어 ID와 순서를 설정합니다. (문자열 할당 없음)
    /// </summary>
    private void ApplySortingSettings(ParticleSystem _effect, int _layerID, int _order)
    {
        if (null == _effect)
            return;

        VFXPoolInstanceHelper _helper = GetHelper(_effect);
        if (null != _helper && true == _helper.IsSortingAlreadyApplied(true, _layerID, _order))
            return;

        Renderer[] _renderers = ResolveChildRenderers(_effect, _helper);
        if (null == _renderers)
            return;

        int _count = _renderers.Length;
        for (int i = 0; i < _count; i++)
        {
            Renderer _renderer = _renderers[i];
            if (null != _renderer)
            {
                _renderer.sortingLayerID = _layerID;
                _renderer.sortingOrder = _order;
            }
        }

        if (null != _helper)
            _helper.MarkSortingApplied(true, _layerID, _order);
    }

    /// <summary>
    /// 소팅 적용 대상 렌더러 배열을 돌려줍니다. 헬퍼가 캐시를 갖고 있으면 그것을, 없으면 예전처럼 새로 수집합니다.
    /// </summary>
    private static Renderer[] ResolveChildRenderers(ParticleSystem _effect, VFXPoolInstanceHelper _helper)
    {
        Renderer[] _renderers = (null != _helper) ? _helper.GetChildRenderers() : null;
        if (null == _renderers)
            _renderers = _effect.GetComponentsInChildren<Renderer>(true);

        return _renderers;
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
        if (initializeOnAwake)
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
    private DG.Tweening.TweenCallback cachedDeferredSetParent;
    // 앱 종료/플레이 모드 종료가 시작된 뒤에는 풀 인스턴스의 부모를 바꾸지 않는다(파괴 중인 계층에 SetParent하면 엔진이 Transform 계층 어서션을 낸다).
    private static bool isQuitting = false;
    // 비활성 상태에서 반납될 때 예약해두는 재부모화 트윈. 예약이 살아있는 동안 이 인스턴스가
    // 다시 대여되면 반드시 취소해야 한다(CancelPendingReparent).
    private DG.Tweening.Tween deferredReparentTween;
    private Vector3 originalLocalScale = Vector3.one;
    private bool hasOriginalLocalScale = false;
    // 마지막으로 재생을 시작한 시각. 풀이 가득 찼을 때 가장 오래된 인스턴스를 골라 회수하는 기준이다.
    private float lastPlayTime = float.MinValue;
    // 풀 인스턴스의 계층은 생성 후 바뀌지 않으므로 자식 파티클 시스템은 Initialize에서 한 번만 수집한다.
    private ParticleSystem[] childSystems;
    // 자식 렌더러는 일부 VFX 스크립트(VFX_LaserHit, VFX_ChargeVortex 등)가 Awake에서 메쉬 자식을 만들기 때문에
    // 루트가 한 번이라도 활성화된 뒤 첫 사용 시점에 수집한다.
    private Renderer[] childRenderers;
    // 마지막으로 적용한 소팅 값. 같은 값이 다시 들어오면 렌더러 순회를 건너뛴다. 재생/반납 시 무효화된다.
    private bool bSortingApplied;
    private bool bSortingLayerApplied;
    private int lastSortingLayerID = -1;
    private int lastSortingOrder;
    private WaitForSeconds cachedReturnWait;
    private float cachedReturnWaitSeconds = -1f;
    // 이 인스턴스가 속한 태그 풀과 그 안의 위치. 꺼질 때 풀의 탐색 시작 위치를 여기까지 내린다(VFXPoolFreeHint 참조).
    private VFXPoolFreeHint poolFreeHint;
    private int poolIndex = -1;


    // 퍼블릭 초기화 및 제어 메서드

    public float LastPlayTime => lastPlayTime;

    /// <summary>자기 자신을 포함한 모든 자식 ParticleSystem(비활성 포함). Initialize에서 한 번 수집된다.</summary>
    public ParticleSystem[] ChildSystems => childSystems;

    /// <summary>
    /// 자기 자신을 포함한 모든 자식 Renderer(비활성 포함)를 돌려줍니다.
    /// 아직 한 번도 활성화되지 않았으면(Awake 미실행 → 런타임 생성 자식이 없을 수 있음) null을 돌려주고 캐시하지 않습니다.
    /// </summary>
    public Renderer[] GetChildRenderers()
    {
        if (null == childRenderers && true == gameObject.activeInHierarchy)
            childRenderers = GetComponentsInChildren<Renderer>(true);

        return childRenderers;
    }

    /// <summary>
    /// 직전에 적용한 소팅 값과 동일한지 확인합니다. 레이어를 지정하지 않는 호출(_setLayer=false)은 순서만 비교합니다.
    /// </summary>
    public bool IsSortingAlreadyApplied(bool _setLayer, int _layerID, int _order)
    {
        if (false == bSortingApplied)
            return false;

        if (lastSortingOrder != _order)
            return false;

        if (true == _setLayer && (false == bSortingLayerApplied || lastSortingLayerID != _layerID))
            return false;

        return true;
    }

    public void MarkSortingApplied(bool _setLayer, int _layerID, int _order)
    {
        bSortingApplied = true;
        lastSortingOrder = _order;

        if (true == _setLayer)
        {
            bSortingLayerApplied = true;
            lastSortingLayerID = _layerID;
        }
    }

    public static bool IsQuitting => isQuitting;

    public void BindPoolSlot(VFXPoolFreeHint _hint, int _index)
    {
        poolFreeHint = _hint;
        poolIndex = _index;
    }

    /// <summary>
    /// 이 인스턴스가 다시 꺼낼 수 있는 상태가 됐을(또는 됐을 수 있는) 때 부른다. 풀의 탐색 시작 위치를 이 인스턴스
    /// 위치까지 내린다. 실제로는 꺼지지 않은 경우(부모만 꺼진 경우 등)에 불려도, 탐색을 조금 더 앞에서 시작할 뿐
    /// 고르는 인스턴스는 같다.
    /// </summary>
    private void NotifyPoolSlotFree()
    {
        if (null != poolFreeHint && poolIndex >= 0 && poolIndex < poolFreeHint.firstMaybeFree)
            poolFreeHint.firstMaybeFree = poolIndex;
    }

    private void OnDisable()
    {
        // 반납 경로(ReturnToPool) 밖에서 이 오브젝트가 꺼지는 경우까지 덮는 안전망
        NotifyPoolSlotFree();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterQuitGuard()
    {
        isQuitting = false;
        Application.quitting -= HandleQuitting;
        Application.quitting += HandleQuitting;
    }

    private static void HandleQuitting()
    {
        isQuitting = true;
    }

    /// <summary>
    /// 재생 시작 시각을 기록합니다. VFXComponent.Play가 실제 Play 직전에 호출합니다.
    /// </summary>
    public void MarkPlayed()
    {
        lastPlayTime = Time.time;
        // 새 주인에게 넘어가는 시점이므로 소팅 캐시를 무효화해 첫 적용은 반드시 수행되게 한다.
        bSortingApplied = false;
        bSortingLayerApplied = false;
    }

    public void Initialize(Transform _parent, Transform _target = null)
    {
        originalParent = _parent;
        targetTransform = (null != _target) ? _target : transform;
        particleSys = GetComponent<ParticleSystem>();
        isReturning = false;
        lastPlayTime = float.MinValue;
        childSystems = GetComponentsInChildren<ParticleSystem>(true);
        childRenderers = null;
        bSortingApplied = false;
        bSortingLayerApplied = false;

        // 이 시점의 로컬 스케일은 아직 아무 재부모화도 거치지 않은 프리팹 원본 값이다
        // (CreateNewInstance가 Instantiate(..., worldPositionStays:false) 직후에 이 메서드를 호출한다).
        // 부모 없이 재생할 때 이 값으로 되돌려, 재부모화 과정에서 스케일이 오염되는 것을 막는다.
        originalLocalScale = targetTransform.localScale;
        hasOriginalLocalScale = true;

        if (null == cachedDeferredSetParent)
            cachedDeferredSetParent = ExecuteDeferredSetParent;
    }

    /// <summary>
    /// 캐싱해둔 프리팹 원본 로컬 스케일을 반환합니다.
    /// Initialize 전이라 캐싱된 값이 없으면 false를 반환하며, 이 경우 호출부는 스케일을 건드리면 안 됩니다
    /// (초기값으로 덮어쓰면 이펙트가 사라지거나 크기가 틀어질 수 있음).
    /// </summary>
    public bool TryGetOriginalLocalScale(out Vector3 _localScale)
    {
        _localScale = originalLocalScale;
        return hasOriginalLocalScale;
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

            ParticleSystem[] _children = (null != childSystems) ? childSystems : particleSys.GetComponentsInChildren<ParticleSystem>(true);
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

        // 이 헬퍼의 정지 코루틴은 동시에 하나만 돌므로 대기 객체를 재사용해도 안전하다.
        float _waitSeconds = _maxLifetime + 0.2f;
        if (null == cachedReturnWait || cachedReturnWaitSeconds != _waitSeconds)
        {
            cachedReturnWait = new WaitForSeconds(_waitSeconds);
            cachedReturnWaitSeconds = _waitSeconds;
        }

        yield return cachedReturnWait;
        ReturnToPool();
        stopCoroutine = null;
    }

    public void ReturnToPool()
    {
        if (true == isReturning)
            return;

        // 종료 중에는 파티클 정지 콜백(OnParticleSystemStopped)이 파괴 중인 계층에 SetParent/SetActive를 시도하지 않도록 통째로 건너뛴다.
        if (true == isQuitting)
        {
            UnityEngine.Debug.LogWarning("[VFXPoolInstanceHelper] 종료 중 ReturnToPool 호출을 건너뜀: " + name);
            return;
        }

        isReturning = true;
        bSortingApplied = false;
        bSortingLayerApplied = false;

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
            {
                try
                {
                    // 부모 객체가 비활성화(OnDisable)되는 도중에 SetParent가 호출되면 에러가 발생함.
                    // 따라서 활성화 상태일 때만 즉시 부모를 바꾸고, 비활성화 중일 때는 한 프레임 지연시킵니다.
                    // (GC 최적화를 위해 람다식 대신 캐싱된 델리게이트 사용)
                    if (targetTransform.gameObject.activeInHierarchy)
                    {
                        targetTransform.SetParent(originalParent);
                    }
                    else
                    {
                        CancelPendingReparent();
                        deferredReparentTween = DG.Tweening.DOVirtual.DelayedCall(0.01f, cachedDeferredSetParent, true);
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[VFXPoolInstanceHelper] Failed to set parent: {ex.Message}");
                }
            }
        }

        if (null != targetTransform && true == targetTransform.gameObject.activeSelf)
        {
            targetTransform.gameObject.SetActive(false);
            if (targetTransform != transform)
                gameObject.SetActive(false);
        }

        // 부모가 이미 꺼져 있어 OnDisable이 오지 않는 경우가 있으므로 여기서 직접 알린다
        NotifyPoolSlotFree();

        isReturning = false;
    }

    public Transform TargetTransform => targetTransform;

    /// <summary>
    /// 비활성 반납 시 예약해둔 지연 재부모화를 취소합니다.
    /// 인스턴스를 풀에서 다시 꺼낼 때 호출해야, 새 주인에게 붙은 이펙트가 뒤늦게 떨어져 나가지 않습니다.
    /// </summary>
    public void CancelPendingReparent()
    {
        if (null != deferredReparentTween)
        {
            if (true == DG.Tweening.TweenExtensions.IsActive(deferredReparentTween))
                DG.Tweening.TweenExtensions.Kill(deferredReparentTween, false);

            deferredReparentTween = null;
        }
    }

    private void ExecuteDeferredSetParent()
    {
        deferredReparentTween = null;

        if (true == isQuitting)
            return;

        if (null == targetTransform || null == originalParent)
            return;

        // 예약과 실행 사이에 다시 대여되어 재생 중이면(활성 상태) 손대면 안 된다.
        // 여기서 부모를 바꾸면 이펙트가 주인에게서 분리되어 그 자리에 그대로 남는다.
        if (true == targetTransform.gameObject.activeSelf)
            return;

        if (targetTransform.parent == originalParent)
            return;

        try { targetTransform.SetParent(originalParent); } catch {}
    }

    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnParticleSystemStopped()
    {
        // 풀이 가득 차 강제 회수(ReturnToPool의 Stop+Clear)된 직후 같은 프레임에 다시 대여되어 재생 중이면,
        // 그 Stop에 대한 콜백이 뒤늦게 도착할 수 있다. 지금 재생 중인 새 주인의 이펙트를 꺼버리면 안 된다.
        if (null != particleSys && true == particleSys.isPlaying)
            return;

        ReturnToPool();
    }

    private void OnDestroy()
    {
        CancelPendingReparent();

        if (null != targetTransform && transform != targetTransform)
        {
            Destroy(targetTransform.gameObject);
        }
    }
}
