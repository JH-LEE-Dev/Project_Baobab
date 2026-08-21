using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 현재 언어에 맞는 TMP 폰트로 화면의 모든 텍스트를 교체합니다.
///
/// 교체할 때 텍스트 컴포넌트의 값(폰트 크기·정렬·색 등)은 건드리지 않고, 머티리얼도
/// 원본을 복제해 아틀라스 텍스처만 바꿔 끼우기 때문에 아웃라인 설정이 그대로 유지됩니다.
/// (TMP_Text.font에 대입하면 머티리얼이 새 폰트의 기본 머티리얼로 리셋되므로,
///  대입 직후 반드시 파생 머티리얼을 다시 지정해야 합니다)
///
/// 교체 시점은 두 가지입니다.
///  1) 언어 변경·씬 로드: 로드된 모든 텍스트를 전수 교체한다.
///  2) 프리팹에 미리 붙여둔 <see cref="LocalizedFontTracker"/>의 OnEnable:
///     런타임에 Instantiate된 UI가 처음 그려지기 전에 교체된다.
/// 둘 다 놓친 텍스트(코드로 직접 만든 TMP 등)는 TMP의 텍스트 갱신 이벤트로 뒤늦게 잡지만,
/// 그 경우에만 첫 한 프레임이 원본 폰트로 그려집니다.
///
/// 주의: 대상 폰트 에셋은 원본과 같은 렌더 모드여야 합니다. 이 프로젝트는 픽셀 아트라
/// 비트맵(RASTER) 아틀라스를 쓰는데, SDF 머티리얼에 비트맵 아틀라스를 물리면 글자가 뭉개집니다.
/// 그래서 렌더 모드가 다르면 교체를 포기하고 원본을 유지한 뒤 경고를 남깁니다.
/// </summary>
public static class FontLocalizer
{
    // //내부 의존성
    // TMP의 ShaderUtilities는 초기화 시점에 의존하므로, 프로퍼티 ID는 직접 캐싱한다.
    private static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ID_GradientScale = Shader.PropertyToID("_GradientScale");
    private static readonly int ID_TextureWidth = Shader.PropertyToID("_TextureWidth");
    private static readonly int ID_TextureHeight = Shader.PropertyToID("_TextureHeight");
    private static readonly int ID_WeightNormal = Shader.PropertyToID("_WeightNormal");
    private static readonly int ID_WeightBold = Shader.PropertyToID("_WeightBold");

    private const int MATERIAL_CACHE_CAPACITY = 32;
    private const int TEXT_BUFFER_CAPACITY = 128;

    // (원본 머티리얼, 대상 폰트) 조합마다 파생 머티리얼을 하나만 만들어 재사용한다.
    // 매번 새로 만들면 드로우콜이 분리되고 메모리도 계속 늘어난다.
    private static readonly Dictionary<(Material, TMP_FontAsset), Material> materialCache = new Dictionary<(Material, TMP_FontAsset), Material>(MATERIAL_CACHE_CAPACITY);

    // 계층 순회 결과를 담는 재사용 버퍼. (GetComponentsInChildren 오버로드가 List를 채워준다)
    private static readonly List<TMP_Text> textBuffer = new List<TMP_Text>(TEXT_BUFFER_CAPACITY);

    private static LocalizationFontTable fontTable;
    private static Language currentLanguage = Language.KR;
    private static TMP_FontAsset currentFont;   // null이면 "원본 폰트 유지"
    private static bool isInitialized = false;
    private static FontLocalizerRunner runner;
    // 렌더 모드가 안 맞아 교체를 포기한 (원본, 대상) 조합. 같은 경고를 반복해서 남기지 않는다.
    private static readonly HashSet<(TMP_FontAsset, TMP_FontAsset)> warnedFontPairs = new HashSet<(TMP_FontAsset, TMP_FontAsset)>();
    private static bool isTraversingHierarchy = false;

    public static Language CurrentLanguage => currentLanguage;

    /// <summary>현재 언어에서 사용할 폰트입니다. null이면 원본 폰트를 그대로 씁니다.</summary>
    public static TMP_FontAsset CurrentFont => currentFont;

    // //퍼블릭 초기화 및 제어 메서드
    /// <summary>
    /// 폰트 테이블을 주입하고 교체 시스템을 켭니다. 테이블이 없으면 아무것도 하지 않습니다.
    /// </summary>
    public static void Initialize(LocalizationFontTable _fontTable)
    {
        fontTable = _fontTable;
        isInitialized = (null != fontTable);

        if (false == isInitialized)
        {
            Debug.LogWarning("[FontLocalizer] LocalizationFontTable이 지정되지 않아 언어별 폰트 교체를 건너뜁니다.");
            return;
        }

        EnsureRunner();
        RefreshCurrentFont();
        ApplyToAll();
    }

    /// <summary>언어를 바꾸고 로드된 모든 텍스트에 즉시 반영합니다.</summary>
    public static void SetLanguage(Language _language)
    {
        // 초기화 전에 들어온 값도 기억해둔다. 나중에 Initialize가 불릴 때 이 언어로 적용된다.
        currentLanguage = _language;

        if (false == isInitialized) return;

        RefreshCurrentFont();
        ApplyToAll();
    }

    /// <summary>로드된 모든 씬(비활성 오브젝트 포함)의 텍스트에 현재 언어 폰트를 적용합니다.</summary>
    public static void ApplyToAll()
    {
        if (false == isInitialized) return;

        // 언어 변경·씬 로드처럼 드문 시점에만 호출되므로 배열 할당을 감수한다.
        TMP_Text[] _texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        for (int i = 0; i < _texts.Length; i++)
        {
            Apply(_texts[i]);
        }
    }

    /// <summary>
    /// 특정 계층 아래의 텍스트에만 적용합니다.
    /// 런타임에 Instantiate한 UI를 첫 프레임부터 올바른 폰트로 띄우고 싶을 때 직접 호출하세요.
    /// (호출하지 않아도 자동 추적이 켜져 있으면 한 프레임 뒤에 따라잡습니다)
    /// </summary>
    public static void Apply(GameObject _root)
    {
        if (false == isInitialized || null == _root) return;

        // 공용 버퍼는 재진입에 안전하지 않다. 교체 도중 호출된 코드가 이 메서드를 다시 부르면
        // 안쪽 호출이 버퍼를 비워버려 바깥 루프가 남은 텍스트를 조용히 건너뛴다.
        // 사용 중이면 임시 리스트로 처리한다. (평소 경로에서는 항상 공용 버퍼가 쓰인다)
        bool _isOuterCall = (false == isTraversingHierarchy);
        List<TMP_Text> _texts = _isOuterCall ? textBuffer : new List<TMP_Text>(16);

        if (true == _isOuterCall) isTraversingHierarchy = true;

        try
        {
            _root.GetComponentsInChildren(true, _texts);
            for (int i = 0; i < _texts.Count; i++)
            {
                Apply(_texts[i]);
            }
        }
        finally
        {
            _texts.Clear();
            if (true == _isOuterCall) isTraversingHierarchy = false;
        }
    }

    /// <summary>텍스트 하나에 현재 언어 폰트를 적용합니다. 이미 맞춰져 있으면 아무 일도 하지 않습니다.</summary>
    public static void Apply(TMP_Text _text)
    {
        if (false == isInitialized || null == _text) return;

        LocalizedFontTracker _tracker = GetOrCreateTracker(_text);
        if (null == _tracker || false == _tracker.HasCaptured) return;

        TMP_FontAsset _origin = _tracker.OriginalFont;

        // 원본 폰트를 알 수 없으면(참조 유실 등) 건드리지 않는다. 되돌릴 방법이 없기 때문이다.
        if (null == _origin) return;

        TMP_FontAsset _target = currentFont;
        if (null == _target || true == fontTable.IsExcluded(_origin))
        {
            _target = _origin;
        }

        if (_target == _origin)
        {
            RestoreOriginal(_text, _tracker);
            return;
        }

        // 렌더 모드가 다르면(SDF ↔ 비트맵) 교체하지 않고 원본을 유지한다.
        // 억지로 바꾸면 SDF 셰이더가 비트맵 아틀라스를 샘플링하게 되어 글자가 뭉개진다.
        // 글자가 안 보이는 편이 뭉개진 채로 출시되는 것보다 낫고, 경고로 원인도 남는다.
        if (false == IsRenderModeCompatible(_tracker.OriginalMaterial, _target))
        {
            WarnRenderModeMismatch(_text, _origin, _target);
            RestoreOriginal(_text, _tracker);
            return;
        }

        if (_text.font == _target) return;

        // font에 대입하면 머티리얼이 대상 폰트의 기본 머티리얼로 리셋되므로, 반드시 그 뒤에 지정한다.
        _text.font = _target;
        _text.fontSharedMaterial = ResolveMaterial(_tracker.OriginalMaterial, _target);
    }

    /// <summary>
    /// 캐싱해둔 파생 머티리얼을 모두 버립니다. (폰트 테이블을 런타임에 갈아끼울 때만 필요)
    /// </summary>
    public static void ClearMaterialCache()
    {
        foreach (KeyValuePair<(Material, TMP_FontAsset), Material> _pair in materialCache)
        {
            if (null != _pair.Value) Object.Destroy(_pair.Value);
        }
        materialCache.Clear();
    }

    // //내부 로직
    private static void RefreshCurrentFont()
    {
        currentFont = fontTable.GetFont(currentLanguage);
    }

    private static void RestoreOriginal(TMP_Text _text, LocalizedFontTracker _tracker)
    {
        if (_text.font == _tracker.OriginalFont && _text.fontSharedMaterial == _tracker.OriginalMaterial) return;

        _text.font = _tracker.OriginalFont;

        if (null != _tracker.OriginalMaterial)
        {
            _text.fontSharedMaterial = _tracker.OriginalMaterial;
        }
    }

    private static LocalizedFontTracker GetOrCreateTracker(TMP_Text _text)
    {
        if (true == _text.TryGetComponent(out LocalizedFontTracker _tracker)) return _tracker;

        // AddComponent 시점에 오브젝트가 활성이면 Awake가 즉시 돌아 원본이 기록되고,
        // 이어지는 OnEnable이 Apply를 한 번 더 호출한다. (이미 맞춰진 상태라 두 번째는 즉시 반환)
        _tracker = _text.gameObject.AddComponent<LocalizedFontTracker>();
        _tracker.EnsureCaptured();
        return _tracker;
    }

    /// <summary>
    /// 원본 머티리얼의 설정(아웃라인 색·두께, 페이스 컬러 등)은 그대로 두고
    /// 폰트 아틀라스만 대상 폰트의 것으로 바꾼 머티리얼을 돌려줍니다.
    /// </summary>
    private static Material ResolveMaterial(Material _sourceMaterial, TMP_FontAsset _targetFont)
    {
        Material _targetDefault = _targetFont.material;

        if (null == _sourceMaterial) return _targetDefault;

        Texture _atlas = _targetFont.atlasTexture;
        if (null == _atlas) return _targetDefault;

        (Material, TMP_FontAsset) _key = (_sourceMaterial, _targetFont);
        if (true == materialCache.TryGetValue(_key, out Material _cached) && null != _cached) return _cached;

        Material _derived = new Material(_sourceMaterial);
        _derived.hideFlags = HideFlags.HideAndDontSave;
        _derived.SetTexture(ID_MainTex, _atlas);

        // SDF 계열 셰이더는 아틀라스에 종속된 스칼라를 따로 들고 있어 대상 폰트 값으로 맞춰야 한다.
        // 비트맵 셰이더에는 없는 프로퍼티이므로 존재 여부를 확인하고 옮긴다.
        // (아웃라인·언더레이 등 원본 고유의 값은 복제된 그대로 남는다)
        if (true == _sourceMaterial.HasProperty(ID_GradientScale) && null != _targetDefault && true == _targetDefault.HasProperty(ID_GradientScale))
        {
            _derived.SetFloat(ID_GradientScale, _targetDefault.GetFloat(ID_GradientScale));
            _derived.SetFloat(ID_TextureWidth, _targetDefault.GetFloat(ID_TextureWidth));
            _derived.SetFloat(ID_TextureHeight, _targetDefault.GetFloat(ID_TextureHeight));
            _derived.SetFloat(ID_WeightNormal, _targetDefault.GetFloat(ID_WeightNormal));
            _derived.SetFloat(ID_WeightBold, _targetDefault.GetFloat(ID_WeightBold));
        }

#if UNITY_EDITOR
        // 이름 조합은 문자열 할당이 있으므로 에디터에서만 한다. (프로파일러에서 구분하기 위한 용도)
        _derived.name = _sourceMaterial.name + " + " + _targetFont.name;
#endif

        materialCache[_key] = _derived;
        return _derived;
    }

    /// <summary>
    /// 원본 머티리얼과 대상 폰트가 같은 렌더 방식(SDF/비트맵)인지 확인합니다.
    /// SDF 머티리얼에는 _GradientScale이 있고 비트맵 머티리얼에는 없다는 점으로 구분합니다.
    /// </summary>
    private static bool IsRenderModeCompatible(Material _sourceMaterial, TMP_FontAsset _targetFont)
    {
        if (null == _sourceMaterial) return true;   // 원본 머티리얼이 없으면 대상 기본 머티리얼을 그대로 쓴다.

        Material _targetDefault = _targetFont.material;
        if (null == _targetDefault) return true;

        return _sourceMaterial.HasProperty(ID_GradientScale) == _targetDefault.HasProperty(ID_GradientScale);
    }

    /// <summary>
    /// 렌더 모드가 달라 교체를 포기했음을 알립니다. 폰트 조합당 한 번만 남깁니다.
    /// (매 텍스트마다 남기면 콘솔이 같은 경고로 가득 차 원인을 찾기 어려워집니다)
    /// </summary>
    private static void WarnRenderModeMismatch(TMP_Text _text, TMP_FontAsset _origin, TMP_FontAsset _target)
    {
        if (false == warnedFontPairs.Add((_origin, _target))) return;

        Debug.LogWarning("[FontLocalizer] '" + _origin.name + "' → '" + _target.name + "' 교체를 건너뜁니다. " +
            "두 폰트의 렌더 모드가 다릅니다(SDF ↔ 비트맵). 해당 텍스트는 원본 폰트로 남으므로 " +
            "이 언어에서 글자가 보이지 않을 수 있습니다. 텍스트의 폰트를 다른 UI와 같은 것으로 " +
            "맞추거나, LocalizationFontTable의 excludedFonts에 등록하세요.", _text);
    }

    private static void EnsureRunner()
    {
        if (null != runner) return;
        if (false == Application.isPlaying) return;

        GameObject _go = new GameObject("[FontLocalizer]");
        Object.DontDestroyOnLoad(_go);
        runner = _go.AddComponent<FontLocalizerRunner>();
    }

    /// <summary>
    /// 도메인 리로드를 끈 환경에서도 플레이 시작 시 상태가 초기화되도록 보장합니다.
    /// (정적 필드가 이전 플레이 세션의 값을 그대로 들고 있으면 파괴된 오브젝트를 참조하게 됩니다)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        materialCache.Clear();
        textBuffer.Clear();
        fontTable = null;
        currentFont = null;
        currentLanguage = Language.KR;
        isInitialized = false;
        runner = null;
        warnedFontPairs.Clear();
        isTraversingHierarchy = false;
    }

    /// <summary>
    /// 씬 로드와 "런타임에 새로 생긴 텍스트"를 감시하는 구동체입니다.
    ///
    /// TMP는 텍스트 메시를 다시 만들 때마다 TEXT_CHANGED_EVENT를 쏘므로, 이걸 받으면
    /// 새로 생성된 텍스트를 전수 검사 없이 알아낼 수 있습니다. 다만 이 이벤트는 메시 생성
    /// 도중에 호출되므로, 그 자리에서 폰트를 바꾸면 캔버스 리빌드 루프 안에서 다시 리빌드를
    /// 요청하게 됩니다. 그래서 목록에만 담아두고 LateUpdate에서 처리합니다.
    /// </summary>
    private sealed class FontLocalizerRunner : MonoBehaviour
    {
        // //내부 의존성
        private readonly List<TMP_Text> pendingTexts = new List<TMP_Text>(TEXT_BUFFER_CAPACITY);

        // 델리게이트는 등록/해제 때마다 새로 만들면 힙 할당이 생기므로 한 번만 만들어 재사용한다.
        private System.Action<UnityEngine.Object> textChangedHandler;
        private bool isTrackingRuntimeText = false;

        // //유니티 이벤트 함수
        private void Awake()
        {
            textChangedHandler = OnTextChanged;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            isTrackingRuntimeText = (null != fontTable && true == fontTable.TrackRuntimeCreatedText);
            if (true == isTrackingRuntimeText)
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(textChangedHandler);
            }
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            if (true == isTrackingRuntimeText)
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(textChangedHandler);
            }
        }

        private void LateUpdate()
        {
            if (0 == pendingTexts.Count) return;

            for (int i = 0; i < pendingTexts.Count; i++)
            {
                Apply(pendingTexts[i]);
            }
            pendingTexts.Clear();
        }

        // //내부 로직
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene _scene, UnityEngine.SceneManagement.LoadSceneMode _mode)
        {
            ApplyToAll();
        }

        private void OnTextChanged(UnityEngine.Object _object)
        {
            TMP_Text _text = _object as TMP_Text;
            if (null == _text) return;

            // 이미 추적 중이면(=원본을 기록해둔 상태면) 손댈 필요가 없다.
            // 여기가 매 텍스트 갱신마다 도는 유일한 지점이므로 컴포넌트 조회 한 번으로 끝낸다.
            if (true == _text.TryGetComponent(out LocalizedFontTracker _)) return;

            pendingTexts.Add(_text);
        }
    }
}
