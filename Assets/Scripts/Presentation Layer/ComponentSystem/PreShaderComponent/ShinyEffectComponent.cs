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

    private bool ShowMaxPoolSize() => _useVfxEffect && _allowDynamicExpansion;

    [ShowIf("ShowMaxPoolSize")]
    [SerializeField] private int _maxPoolSize = 10;

    [Header("VFX Sorting")]
    [ShowIf("_useVfxEffect")]
    [Tooltip("체크 시 현재 렌더러나 캔버스의 소팅 속성을 찾아 +1 된 값으로 자동 렌더링합니다.")]
    [SerializeField] private bool _autoSorting = true;
    
    private bool ShowCustomSorting() => _useVfxEffect && !_autoSorting;

    [ShowIf("ShowCustomSorting")]
    [SerializeField] private string _vfxSortingLayer = "Default";

    [ShowIf("ShowCustomSorting")]
    [SerializeField] private int _vfxSortingOrder = 0;

    // 내부 의존성
    private Material _instanceMaterial;
    private Material[] _prevMaterials;
    private Graphic _graphic;
    private Renderer _renderer;
    private Tween _shinyTween;

    private VFXComponent _vfxComponent;
    private ParticleSystem _activeVfxParticle;
    
    private GameObject _uiOverlayObj;
    private Graphic _uiOverlayGraphic;

    private static readonly int _shinyColorId = Shader.PropertyToID("_ShinyColor");
    private static readonly int _shinyWidthId = Shader.PropertyToID("_ShinyWidth");
    private static readonly int _shinySoftnessId = Shader.PropertyToID("_ShinySoftness");
    private static readonly int _shinyAngleId = Shader.PropertyToID("_ShinyAngle");
    private static readonly int _shinyLocationId = Shader.PropertyToID("_ShinyLocation");

    private const string _overlayName = "__ShinyOverlay__";

    /// <summary>
    /// 수동으로 이펙트를 재생합니다.
    /// </summary>
    public void PlayEffect()
    {
        if (_instanceMaterial != null)
        {
            StopEffect();

            _instanceMaterial.SetFloat(_shinyLocationId, -1f);
            
            _shinyTween = _instanceMaterial.DOFloat(2f, "_ShinyLocation", _duration)
                .SetDelay(_delay)
                .SetEase(Ease.Linear);

            if (_loopCount == -1)
            {
                _shinyTween.SetLoops(-1, LoopType.Restart);
            }
            else if (_loopCount > 0)
            {
                _shinyTween.SetLoops(_loopCount, LoopType.Restart);
            }
        }

        // 재생 시 VFX 파티클 함께 출력
        if (Application.isPlaying && _useVfxEffect && _vfxComponent != null)
        {
            if (_activeVfxParticle != null)
            {
                bool _immediate = !_activeVfxParticle.gameObject.activeInHierarchy;
                _vfxComponent.Stop(_activeVfxParticle, _immediate);
            }

            _activeVfxParticle = _vfxComponent.Play(_vfxTag, transform.position, transform.rotation, transform);
            
            if (_activeVfxParticle != null)
            {
                _vfxComponent.SetStartColor(_activeVfxParticle, _vfxColor);

                if (_autoSorting)
                    ApplyAutoSorting(_activeVfxParticle);
                else
                    _vfxComponent.SetSortingSettings(_activeVfxParticle, _vfxSortingLayer, _vfxSortingOrder);
            }
        }
    }

    /// <summary>
    /// 진행 중인 이펙트를 중지합니다.
    /// </summary>
    public void StopEffect()
    {
        if (_shinyTween != null)
        {
            _shinyTween.Kill();
            _shinyTween = null;
        }

        if (Application.isPlaying && _activeVfxParticle != null && _vfxComponent != null)
        {
            bool _immediate = !_activeVfxParticle.gameObject.activeInHierarchy;
            _vfxComponent.Stop(_activeVfxParticle, _immediate);
            _activeVfxParticle = null;
        }
    }

    /// <summary>
    /// 인스펙터의 변경사항을 머테리얼에 수동으로 즉시 적용합니다.
    /// </summary>
    public void UpdateMaterialProperties()
    {
        if (_instanceMaterial == null) return;

        _instanceMaterial.SetColor(_shinyColorId, _shinyColor);
        _instanceMaterial.SetFloat(_shinyWidthId, _shinyWidth);
        _instanceMaterial.SetFloat(_shinySoftnessId, _shinySoftness);
        _instanceMaterial.SetFloat(_shinyAngleId, _shinyAngle);

        if (_uiOverlayGraphic != null)
        {
            _uiOverlayGraphic.SetMaterialDirty();
        }
    }

    private void SetupVFXComponent()
    {
        if (!_useVfxEffect || _vfxPrefab == null) return;
        if (!Application.isPlaying) return;

        _vfxComponent = GetComponent<VFXComponent>();
        if (_vfxComponent == null)
            _vfxComponent = gameObject.AddComponent<VFXComponent>();

        FieldInfo isUiField = typeof(VFXComponent).GetField("isUIComponent", BindingFlags.NonPublic | BindingFlags.Instance);
        if (isUiField != null)
            isUiField.SetValue(_vfxComponent, _isUIVfx);

        VFXPoolData poolData = new VFXPoolData();
        var type = typeof(VFXPoolData);
        type.GetField("vfxTag", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(poolData, _vfxTag);
        type.GetField("effectPrefab", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(poolData, _vfxPrefab);
        type.GetField("initialPoolSize", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(poolData, _initialPoolSize);
        type.GetField("allowDynamicExpansion", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(poolData, _allowDynamicExpansion);
        type.GetField("maxPoolSize", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(poolData, _maxPoolSize);
        type.GetField("uiParticleScale", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(poolData, _vfxUiScale);

        FieldInfo listField = typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (listField != null)
            listField.SetValue(_vfxComponent, new List<VFXPoolData> { poolData });

        _vfxComponent.Initialize();
        _vfxComponent.SetStartColorOfTag(_vfxTag, _vfxColor);
    }

    private void ApplyAutoSorting(ParticleSystem vfxParticle)
    {
        string _targetLayer = "Default";
        int _targetOrder = 1;

        if (_renderer != null)
        {
            _targetLayer = _renderer.sortingLayerName;
            _targetOrder = _renderer.sortingOrder + 1;
        }
        else if (_graphic != null && _graphic.canvas != null)
        {
            _targetLayer = _graphic.canvas.sortingLayerName;
            _targetOrder = _graphic.canvas.sortingOrder + 1;
        }

        if (_vfxComponent != null)
            _vfxComponent.SetSortingSettings(vfxParticle, _targetLayer, _targetOrder);
    }

    private void CreateUIOverlay()
    {
        if (_graphic == null) return;
        DestroyUIOverlay(); // 기존 찌꺼기 방지

        _uiOverlayObj = new GameObject(_overlayName);
        _uiOverlayObj.hideFlags = HideFlags.HideAndDontSave; // 에디터 씬 더티/누수 방지

        RectTransform rt = _uiOverlayObj.AddComponent<RectTransform>();
        rt.SetParent(_graphic.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        if (_graphic is Image origImage)
        {
            Image newImage = _uiOverlayObj.AddComponent<Image>();
            newImage.sprite = origImage.sprite;
            newImage.type = origImage.type;
            newImage.preserveAspect = origImage.preserveAspect;
            newImage.fillMethod = origImage.fillMethod;
            newImage.fillAmount = origImage.fillAmount;
            newImage.raycastTarget = false;
            _uiOverlayGraphic = newImage;
        }
        else if (_graphic is RawImage origRaw)
        {
            RawImage newRaw = _uiOverlayObj.AddComponent<RawImage>();
            newRaw.texture = origRaw.texture;
            newRaw.uvRect = origRaw.uvRect;
            newRaw.raycastTarget = false;
            _uiOverlayGraphic = newRaw;
        }

        if (_uiOverlayGraphic != null && _instanceMaterial != null)
        {
            _uiOverlayGraphic.material = _instanceMaterial;
        }
    }

    private void DestroyUIOverlay()
    {
        if (_uiOverlayObj != null)
        {
            if (Application.isPlaying) Destroy(_uiOverlayObj);
            else DestroyImmediate(_uiOverlayObj);
            _uiOverlayObj = null;
            _uiOverlayGraphic = null;
        }

        // 찌꺼기 검사
        if (_graphic != null)
        {
            Transform existing = _graphic.transform.Find(_overlayName);
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (_shinyOriginalMaterial == null)
            _shinyOriginalMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ShinyCard.mat");
    }
#endif

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _renderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        SetupVFXComponent();

        if (_shinyOriginalMaterial == null) return;

        if (_instanceMaterial == null)
        {
            _instanceMaterial = new Material(_shinyOriginalMaterial);
            _instanceMaterial.hideFlags = HideFlags.DontSave;
            
            // 오버레이 모드 켜기 (기존 이미지를 지우고 빛만 렌더링하여 덧씌우기)
            _instanceMaterial.SetFloat("_OverlayMode", 1f);
            _instanceMaterial.EnableKeyword("UI_OVERLAY");
        }

        UpdateMaterialProperties();

        if (_graphic != null)
        {
            // UI 자식 생성 방식 (기존 머테리얼 보존)
            CreateUIOverlay();
        }
        else if (_renderer != null)
        {
            // 월드 렌더러 배열 추가 방식 (기존 머테리얼 보존)
            _prevMaterials = _renderer.sharedMaterials;
            
            if (_prevMaterials != null)
            {
                Material[] newMaterials = new Material[_prevMaterials.Length + 1];
                for (int i = 0; i < _prevMaterials.Length; i++)
                {
                    newMaterials[i] = _prevMaterials[i];
                }
                newMaterials[newMaterials.Length - 1] = _instanceMaterial;
                _renderer.sharedMaterials = newMaterials;
            }
            else
            {
                _renderer.sharedMaterials = new Material[] { _instanceMaterial };
            }
        }

        if (Application.isPlaying && _playOnEnable)
        {
            PlayEffect();
        }
    }

    private void OnDisable()
    {
        StopEffect();

        if (_graphic != null)
        {
            DestroyUIOverlay();
        }
        else if (_renderer != null)
        {
            if (_prevMaterials != null)
            {
                _renderer.sharedMaterials = _prevMaterials;
            }
        }

        if (_instanceMaterial != null)
        {
            if (Application.isPlaying) Destroy(_instanceMaterial);
            else DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
        }
    }

    private void OnValidate()
    {
        if (_instanceMaterial != null)
        {
            UpdateMaterialProperties();
            
            if (!Application.isPlaying)
            {
                _instanceMaterial.SetFloat(_shinyLocationId, 0.5f);
            }
        }
    }
}
