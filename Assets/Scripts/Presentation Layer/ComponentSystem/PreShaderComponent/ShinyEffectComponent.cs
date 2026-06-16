using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Reflection;
using System.Collections.Generic;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Component))]
public class ShinyEffectComponent : MonoBehaviour
{
    [Header("Master Switch")]
    [Tooltip("스크립트 기능(이펙트 재생) 사용 여부를 결정합니다. 끄더라도 세팅된 오버레이는 유지되나, 완전히 투명해져 부하가 사라집니다.")]
    [SerializeField] private bool _useShinyEffect = true;

    // 외부 의존성
    [Header("Material")]
    [SerializeField] private Material _shinyOriginalMaterial;
    
    [Header("Settings")]
    [SerializeField] private Color _shinyColor = new Color(1, 1, 1, 0.5f);
    [SerializeField] [Range(0.01f, 1f)] private float _shinyWidth = 0.2f;
    [SerializeField] [Range(0.01f, 1f)] private float _shinySoftness = 0.1f;
    [SerializeField] [Range(0f, 360f)] private float _shinyAngle = 45f;

    [Header("Animation")]
    [SerializeField] private bool _playOnEnable = true;
    [SerializeField] private float _duration = 1.0f;
    [SerializeField] private float _delay = 0.5f;
    [Tooltip("-1 for infinite loop, 1+ for exact count")]
    [SerializeField] private int _loopCount = -1;

    [Header("VFX Effect")]
    [SerializeField] private bool _useVfxEffect = false;
    
    [ShowIf("_useVfxEffect")]
    [SerializeField] private ParticleSystem _vfxPrefab;

    [ShowIf("_useVfxEffect")]
    [SerializeField] private string _vfxTag = "ShinyVFX";
    
    [ShowIf("_useVfxEffect")]
    [SerializeField] private Color _vfxColor = Color.white;
    
    [ShowIf("_useVfxEffect")]
    [SerializeField] private bool _isUIVfx = true;
    
    [ShowIf("_useVfxEffect")]
    [SerializeField] private float _vfxUiScale = 1.0f;

    [Header("VFX Pool Settings")]
    [ShowIf("_useVfxEffect")]
    [SerializeField] private int _initialPoolSize = 1;

    [ShowIf("_useVfxEffect")]
    [SerializeField] private bool _allowDynamicExpansion = true;

    private bool ShowMaxPoolSize() => true == _useVfxEffect && true == _allowDynamicExpansion;

    [ShowIf("ShowMaxPoolSize")]
    [SerializeField] private int _maxPoolSize = 10;

    [Header("VFX Sorting")]
    [ShowIf("_useVfxEffect")]
    [Tooltip("체크 시 현재 렌더러나 캔버스의 소팅 속성을 찾아 +1 된 값으로 자동 렌더링합니다.")]
    [SerializeField] private bool _autoSorting = true;
    
    private bool ShowCustomSorting() => true == _useVfxEffect && false == _autoSorting;

    [ShowIf("ShowCustomSorting")]
    [SerializeField] private string _vfxSortingLayer = "Default";

    [ShowIf("ShowCustomSorting")]
    [SerializeField] private int _vfxSortingOrder = 0;

    // 내부 의존성
    private Material _instanceMaterial;
    private Material[] _cachedMaterials; 
    private Graphic _graphic;
    private Renderer _renderer;
    private Tween _shinyTween;

    private VFXComponent _vfxComponent;
    private ParticleSystem _activeVfxParticle;
    private List<VFXPoolData> _cachedPoolList; 
    private VFXPoolData _cachedPoolData;
    
    private GameObject _uiOverlayObj;
    private Graphic _uiOverlayGraphic;
    
    private bool _isOverlaySetup = false;

    // 리플렉션 정보 캐싱
    private static readonly FieldInfo _isUiField = typeof(VFXComponent).GetField("isUIComponent", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _listField = typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.NonPublic | BindingFlags.Instance);
    
    private static readonly FieldInfo _tagField = typeof(VFXPoolData).GetField("vfxTag", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _prefabField = typeof(VFXPoolData).GetField("effectPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _initSizeField = typeof(VFXPoolData).GetField("initialPoolSize", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _allowExpField = typeof(VFXPoolData).GetField("allowDynamicExpansion", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _maxSizeField = typeof(VFXPoolData).GetField("maxPoolSize", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _uiScaleField = typeof(VFXPoolData).GetField("uiParticleScale", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly int _shinyColorId = Shader.PropertyToID("_ShinyColor");
    private static readonly int _shinyWidthId = Shader.PropertyToID("_ShinyWidth");
    private static readonly int _shinySoftnessId = Shader.PropertyToID("_ShinySoftness");
    private static readonly int _shinyAngleId = Shader.PropertyToID("_ShinyAngle");
    private static readonly int _shinyLocationId = Shader.PropertyToID("_ShinyLocation");

    private const string _overlayName = "__ShinyOverlay__";

    // 퍼블릭 초기화 및 제어 메서드
    public bool UseShinyEffect
    {
        get => _useShinyEffect;
        set
        {
            if (_useShinyEffect == value)
                return;

            _useShinyEffect = value;
            if (false == _useShinyEffect)
                StopEffect();
            else if (true == Application.isPlaying && true == _playOnEnable)
                PlayEffect();
        }
    }

    public void PlayEffect()
    {
        if (false == _useShinyEffect)
            return;

        UpdateOverlaySprite();

        if (null != _instanceMaterial)
        {
            StopEffect();

            _instanceMaterial.SetFloat(_shinyLocationId, -1f);
            
            _shinyTween = _instanceMaterial.DOFloat(2f, "_ShinyLocation", _duration)
                .SetDelay(_delay)
                .SetEase(Ease.Linear)
                .OnKill(() => 
                {
                    if (true == Application.isPlaying && null != _vfxComponent && null != _activeVfxParticle)
                    {
                        _vfxComponent.Stop(_activeVfxParticle, true);
                        _activeVfxParticle = null;
                    }
                });

            if (-1 == _loopCount)
                _shinyTween.SetLoops(-1, LoopType.Restart);
            else if (0 < _loopCount)
                _shinyTween.SetLoops(_loopCount, LoopType.Restart);
        }

        if (true == Application.isPlaying && true == _useVfxEffect && null != _vfxComponent)
        {
            _activeVfxParticle = _vfxComponent.Play(_vfxTag, transform.position, transform.rotation, transform);
            
            if (null != _activeVfxParticle)
            {
                _vfxComponent.SetStartColor(_activeVfxParticle, _vfxColor);

                if (true == _autoSorting)
                    ApplyAutoSorting(_activeVfxParticle);
                else
                    _vfxComponent.SetSortingSettings(_activeVfxParticle, _vfxSortingLayer, _vfxSortingOrder);
            }
        }
    }

    public void StopEffect(bool _immediate = false)
    {
        if (null != _shinyTween)
        {
            _shinyTween.Kill();
            _shinyTween = null;
            
            if (null != _instanceMaterial)
                _instanceMaterial.SetFloat(_shinyLocationId, -1f);
        }

        if (true == Application.isPlaying && null != _vfxComponent && null != _activeVfxParticle)
        {
            _vfxComponent.Stop(_activeVfxParticle, _immediate);
            _activeVfxParticle = null;
        }
    }

    public void UpdateMaterialProperties()
    {
        if (null == _instanceMaterial)
            return;

        _instanceMaterial.SetColor(_shinyColorId, _shinyColor);
        _instanceMaterial.SetFloat(_shinyWidthId, _shinyWidth);
        _instanceMaterial.SetFloat(_shinySoftnessId, _shinySoftness);
        _instanceMaterial.SetFloat(_shinyAngleId, _shinyAngle);

        if (null != _uiOverlayGraphic && true == _uiOverlayGraphic.gameObject.activeInHierarchy)
            _uiOverlayGraphic.SetMaterialDirty();
    }

    public void UpdateOverlaySprite()
    {
        if (null == _graphic || null == _uiOverlayGraphic)
            return;

        if (_graphic is Image _origImg && _uiOverlayGraphic is Image _newImg)
        {
            _newImg.sprite = _origImg.sprite;
            _newImg.type = _origImg.type;
            _newImg.preserveAspect = _origImg.preserveAspect;
            _newImg.fillMethod = _origImg.fillMethod;
            _newImg.fillAmount = _origImg.fillAmount;
        }
        else if (_graphic is RawImage _origRawI && _uiOverlayGraphic is RawImage _newRawI)
        {
            _newRawI.texture = _origRawI.texture;
            _newRawI.uvRect = _origRawI.uvRect;
        }
    }

    private void SetupOverlay()
    {
        if (true == _isOverlaySetup)
            return;

        SetupVFXComponent();

        if (null == _shinyOriginalMaterial)
            return;

        if (null == _instanceMaterial)
        {
            _instanceMaterial = new Material(_shinyOriginalMaterial);
            _instanceMaterial.hideFlags = HideFlags.DontSave;
            
            _instanceMaterial.SetFloat("_OverlayMode", 1f);
            _instanceMaterial.EnableKeyword("UI_OVERLAY");
        }

        UpdateMaterialProperties();

        if (null != _graphic)
            CreateUIOverlay();
        else if (null != _renderer)
        {
            Material[] _prevMaterials = _renderer.sharedMaterials;
            if (null != _prevMaterials)
            {
                int _len = _prevMaterials.Length;
                if (null == _cachedMaterials || _cachedMaterials.Length != _len + 1)
                    _cachedMaterials = new Material[_len + 1];

                for (int i = 0; i < _len; i++)
                    _cachedMaterials[i] = _prevMaterials[i];
                    
                _cachedMaterials[_len] = _instanceMaterial;
                _renderer.sharedMaterials = _cachedMaterials;
            }
            else
            {
                if (null == _cachedMaterials || _cachedMaterials.Length != 1)
                    _cachedMaterials = new Material[] { _instanceMaterial };
                else
                    _cachedMaterials[0] = _instanceMaterial;
                    
                _renderer.sharedMaterials = _cachedMaterials;
            }
        }

        _isOverlaySetup = true;
    }

    private void SetupVFXComponent()
    {
        if (false == _useVfxEffect || null == _vfxPrefab)
            return;
            
        if (false == Application.isPlaying)
            return;

        if (null == _vfxComponent)
            _vfxComponent = GetComponent<VFXComponent>();

        if (null == _vfxComponent)
            _vfxComponent = gameObject.AddComponent<VFXComponent>();

        if (null != _isUiField)
            _isUiField.SetValue(_vfxComponent, _isUIVfx);

        if (null == _cachedPoolData)
            _cachedPoolData = new VFXPoolData();

        _tagField?.SetValue(_cachedPoolData, _vfxTag);
        _prefabField?.SetValue(_cachedPoolData, _vfxPrefab);
        _initSizeField?.SetValue(_cachedPoolData, _initialPoolSize);
        _allowExpField?.SetValue(_cachedPoolData, _allowDynamicExpansion);
        _maxSizeField?.SetValue(_cachedPoolData, _maxPoolSize);
        _uiScaleField?.SetValue(_cachedPoolData, _vfxUiScale);

        if (null != _listField)
        {
            if (null == _cachedPoolList)
                _cachedPoolList = new List<VFXPoolData>(1) { _cachedPoolData };
            else
                _cachedPoolList[0] = _cachedPoolData;

            _listField.SetValue(_vfxComponent, _cachedPoolList);
        }

        _vfxComponent.Initialize();
        _vfxComponent.SetStartColorOfTag(_vfxTag, _vfxColor);
    }

    private void ApplyAutoSorting(ParticleSystem _vfxParticle)
    {
        string _targetLayer = "Default";
        int _targetOrder = 1;

        if (null != _renderer)
        {
            _targetLayer = _renderer.sortingLayerName;
            _targetOrder = _renderer.sortingOrder + 1;
        }
        else if (null != _graphic && null != _graphic.canvas)
        {
            _targetLayer = _graphic.canvas.sortingLayerName;
            _targetOrder = _graphic.canvas.sortingOrder + 1;
        }

        if (null != _vfxComponent)
            _vfxComponent.SetSortingSettings(_vfxParticle, _targetLayer, _targetOrder);
    }

    private void CreateUIOverlay()
    {
        if (null == _graphic)
            return;

        if (null == _uiOverlayObj)
        {
            Transform _existing = _graphic.transform.Find(_overlayName);
            if (null != _existing)
            {
                _uiOverlayObj = _existing.gameObject;
                _uiOverlayGraphic = _uiOverlayObj.GetComponent<Graphic>();
            }
            else
            {
                _uiOverlayObj = new GameObject(_overlayName);
                _uiOverlayObj.hideFlags = HideFlags.HideAndDontSave; 

                RectTransform _rt = _uiOverlayObj.AddComponent<RectTransform>();
                _rt.SetParent(_graphic.transform, false);
                _rt.anchorMin = Vector2.zero;
                _rt.anchorMax = Vector2.one;
                _rt.offsetMin = Vector2.zero;
                _rt.offsetMax = Vector2.zero;
                _rt.localScale = Vector3.one;
            }
        }

        if (null == _uiOverlayGraphic && null != _uiOverlayObj)
        {
            if (_graphic is Image _origImage)
            {
                Image _newImage = _uiOverlayObj.AddComponent<Image>();
                _newImage.raycastTarget = false;
                _uiOverlayGraphic = _newImage;
            }
            else if (_graphic is RawImage _origRaw)
            {
                RawImage _newRaw = _uiOverlayObj.AddComponent<RawImage>();
                _newRaw.raycastTarget = false;
                _uiOverlayGraphic = _newRaw;
            }
        }

        if (null != _uiOverlayGraphic)
        {
            if (_graphic is Image _origImg && _uiOverlayGraphic is Image _newImg)
            {
                _newImg.sprite = _origImg.sprite;
                _newImg.type = _origImg.type;
                _newImg.preserveAspect = _origImg.preserveAspect;
                _newImg.fillMethod = _origImg.fillMethod;
                _newImg.fillAmount = _origImg.fillAmount;
            }
            else if (_graphic is RawImage _origRawI && _uiOverlayGraphic is RawImage _newRawI)
            {
                _newRawI.texture = _origRawI.texture;
                _newRawI.uvRect = _origRawI.uvRect;
            }

            if (null != _instanceMaterial)
                _uiOverlayGraphic.material = _instanceMaterial;
        }

        if (null != _uiOverlayObj && false == _uiOverlayObj.activeSelf)
            _uiOverlayObj.SetActive(true);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (null == _shinyOriginalMaterial)
            _shinyOriginalMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShinyCard.mat");
    }
#endif

    // 유니티 이벤트 함수
    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _renderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        SetupOverlay();

        if (true == Application.isPlaying && true == _useShinyEffect && true == _playOnEnable)
            PlayEffect();
    }

    private void OnDisable()
    {
        StopEffect(true);
    }

    private void OnDestroy()
    {
        if (null != _instanceMaterial)
        {
            if (true == Application.isPlaying)
                Destroy(_instanceMaterial);
            else
                DestroyImmediate(_instanceMaterial);
                
            _instanceMaterial = null;
        }

        if (null != _uiOverlayObj)
        {
            if (true == Application.isPlaying)
                Destroy(_uiOverlayObj);
            else
                DestroyImmediate(_uiOverlayObj);
                
            _uiOverlayObj = null;
        }
    }

    private void OnValidate()
    {
        if (false == isActiveAndEnabled)
            return;

        if (false == _isOverlaySetup)
            SetupOverlay();

        if (null != _instanceMaterial)
        {
            UpdateMaterialProperties();
            
            if (false == Application.isPlaying)
                _instanceMaterial.SetFloat(_shinyLocationId, true == _useShinyEffect ? 0.5f : -1f);
        }
    }
}
