using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 보석 종류 하나에 대응하는 머티리얼 한 쌍.
/// </summary>
[System.Serializable]
public struct TreeGemMaterialSet
{
    public TreeGemType gemType;
    public Material topMaterial;
    public Material bottomMaterial;
}

public class TreeVisualComponent : MonoBehaviour
{
    #region Serialized Fields

    [Header("Editor Preview")]
    [SerializeField] private bool previewInEditor = true;

    [Header("Roots")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform topRoot;
    [SerializeField] private Transform bottomRoot;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer topRenderer;
    [SerializeField] private SpriteRenderer bottomRenderer;
    [SerializeField] private SpriteRenderer topShieldRenderer;
    [SerializeField] private SpriteRenderer bottomShieldRenderer;
    [SerializeField] private SpriteRenderer topHighlightRenderer;
    [SerializeField] private SpriteRenderer bottomHighlightRenderer;
    [SerializeField] private SpriteRenderer topShadowRenderer;
    [SerializeField] private SpriteRenderer bottomShadowRenderer;
    [SerializeField] private SpriteRenderer topOnWaterSR;
    [SerializeField] private SpriteRenderer topShieldOnWaterSR;
    [SerializeField] private SpriteRenderer topHighlightOnWaterSR;
    [SerializeField] private SpriteRenderer bottomOnWaterSR;
    [SerializeField] private SpriteRenderer topOutlineSR;
    [SerializeField] private SpriteRenderer bottomOutlineSR;
    [SerializeField] private SpriteRenderer topStencilOutlineSR;
    [SerializeField] private SpriteRenderer bottomStencilOutlineSR;
    [SerializeField] private SpriteRenderer constellationRenderer;

    [Header("Sprite Variations")]
    [SerializeField] private Sprite[] topSprites;
    [SerializeField] private Sprite[] bottomSprites;

    [Header("Hit Feedback")]
    [SerializeField] private float hitPunchX = 0.1f;
    [SerializeField] private float hitDuration = 0.2f;
    [SerializeField] private int hitVibrato = 15;
    [SerializeField] private float hitElasticity = 1f;
    [SerializeField] private float hitFlashDuration = 0.15f;
    [SerializeField] private AnimationCurve hitFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Grow Up Flash")]
    [SerializeField] private float growUpFlashDuration = 0.35f;
    [SerializeField] private AnimationCurve growUpFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);



    [Header("Outline")]
    [SerializeField] private GameObject outlineVisualObj;

    [Header("Gem Visual")]
    // 보석 종류별 머티리얼 세트. 색·투명도·면 크기 등을 종류마다 머티리얼에서 독립적으로 설정한다.
    // 아웃라인/그림자/물그림자 렌더러는 건드리지 않으므로 기존 나무 시스템과 그대로 호환된다.
    [SerializeField] private TreeGemMaterialSet[] gemMaterialSets;

    [Tooltip("등급 매핑이 없을 때 쓸 기본 보석 종류.")]
    [SerializeField] private TreeGemType defaultGemType = TreeGemType.Diamond;

    [Tooltip("나무 등급 -> 보석 종류 매핑. 비워두면 항상 기본 종류를 쓴다.")]
    [SerializeField] private TreeGemColorDataBase treeGemColorDataBase;


    [Header("Other Settings")]
    public GameObject baseVisualObj;

    [Header("HDR")]
    [SerializeField] private float shieldHDRIntensity = 1.05f;
    [SerializeField] private float highlightHDRIntensity = 1.05f;


    [Header("Editor Custom Settings")]
    public TreeType customTreeType = TreeType.OakTree;
    public TreeVisualDataBase treeVisualDataBase;

    #endregion

    #region Private Fields

    // 외부 의존성
    private CustomSortable customSortable;

    // 내부 의존성
    private Transform cachedTransform;

    // 스프라이트 리소스 캐시 및 상태
    private Sprite defaultTopSprite;
    private Sprite defaultBottomSprite;

    // 상태 변수
    private bool isOutlineActive = false;
    private bool bDisableOutline = false;
    private float currentAlpha;
    private bool isShieldActive = false;
    private bool isOnWaterActive = false;

    // 보석 머티리얼인지 판별하는 데 쓰는 프로퍼티
    private static readonly int GemColorID = Shader.PropertyToID("_GemColor");

    // 바람 흔들림. 보석 나무는 흔들리지 않아야 하므로 개체별로 꺼야 한다.
    private static readonly int EnableWindSwayID = Shader.PropertyToID("_EnableWindSway");
    // 현재 이 나무에 sway가 켜져 있는지. 갓 생성된 렌더러는 오버라이드가 없어 머티리얼 기본값(켜짐)을 따르므로 true로 시작한다.
    private bool bWindSwayEnabled = true;

    // Shield HDR
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private MaterialPropertyBlock _mpb;
    private MaterialPropertyBlock Mpb => _mpb ??= new MaterialPropertyBlock();

    // Hit Flash
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock _flashMPB;
    private Coroutine hitFlashCoroutine;

    // Gem Visual - 보석 머티리얼로 갈아끼우기 전의 원본 머티리얼.
    // 에디터에서 인스펙터로 토글하면 스크립트 재컴파일(도메인 리로드)로 런타임 필드가 날아가는데,
    // 그때 이미 보석 머티리얼이 적용된 상태를 "원본"으로 다시 캐싱해버리면 되돌릴 수가 없다.
    // 그래서 직렬화해서 리로드를 넘겨 살린다.
    [SerializeField, HideInInspector] private Material defaultTopMaterial;
    [SerializeField, HideInInspector] private Material defaultBottomMaterial;

    // 별도 플래그를 들고 있으면 리로드 후 실제 머티리얼과 어긋날 수 있어, 현재 머티리얼에서 직접 판단한다.
    // 색을 바꾼 인스턴스 머티리얼도 보석으로 쳐야 하므로 참조 비교가 아니라 프로퍼티 유무로 본다.
    public bool IsGemActive => topRenderer != null && IsGemMaterial(topRenderer.sharedMaterial);

    // VFX Color Settings
    private ParticleColorSet currentTopVfxColor = new ParticleColorSet { startColor = new ParticleSystem.MinMaxGradient(Color.white), overrideChildrenColor = true };
    private ParticleColorSet currentBottomVfxColor = new ParticleColorSet { startColor = new ParticleSystem.MinMaxGradient(Color.white), overrideChildrenColor = true };

    #endregion

    #region Unity Events

    #endregion

    #region Initialize

    public void Initialize(Transform _topShadowTransform, CustomSortable _customSortable)
    {
        if (cachedTransform == null) cachedTransform = transform;

        ResetVisualState();

        customSortable = _customSortable;

        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(topRenderer);
            customSortable.AddSpriteRenderer(bottomRenderer);
            customSortable.AddSpriteRenderer(topShadowRenderer);
            customSortable.AddSpriteRenderer(bottomShadowRenderer);
            customSortable.AddSpriteRenderer(topOutlineSR);
            customSortable.AddSpriteRenderer(bottomOutlineSR);
            customSortable.AddSpriteRenderer(topStencilOutlineSR);
            customSortable.AddSpriteRenderer(bottomStencilOutlineSR);

            //customSortable.SetSortingGroup(baseVisualObj.GetComponent<SortingGroup>());
        }
    }

    public int GetTopSortingOrder() => topRenderer != null ? topRenderer.sortingOrder : 0;

    public int GetTopShieldSortingOrder() => topShieldRenderer != null ? topShieldRenderer.sortingOrder : GetTopSortingOrder();

    public int GetTopHighlightSortingOrder() => topHighlightRenderer != null ? topHighlightRenderer.sortingOrder : GetTopShieldSortingOrder();

    public void UpdateOnWaterSortingOrder()
    {
        if (cachedTransform == null) cachedTransform = transform;
        int order = (int)(cachedTransform.position.y * 100);
        if (topOnWaterSR != null) topOnWaterSR.sortingOrder = order;
        if (bottomOnWaterSR != null) bottomOnWaterSR.sortingOrder = order;
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.sortingOrder = order - 1;
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.sortingOrder = order - 1;
    }

    public void UpdateSortingOrder()
    {
        // [1단계] 스텐실 쓰기: 무조건 본체(bottomRenderer)보다 먼저(음수) 그려서 도화지를 깐다.
        bottomStencilOutlineSR.sortingOrder = bottomRenderer.sortingOrder - 2;
        topStencilOutlineSR.sortingOrder = bottomRenderer.sortingOrder - 1;
        // [2단계] 본체 그리기: 스텐실 영역에 0을 써서 구멍을 뚫는다.
        topRenderer.sortingOrder = bottomRenderer.sortingOrder + 1;
        // [3단계] 아웃라인 그리기: 구멍이 뚫리고 남은 스텐실에만 선을 그린다.
        bottomOutlineSR.sortingOrder = topRenderer.sortingOrder + 1;
        topOutlineSR.sortingOrder = topRenderer.sortingOrder + 2;
        // [4단계] 기타 이펙트 (아웃라인 위를 덮음)
        bottomShieldRenderer.sortingOrder = topOutlineSR.sortingOrder + 1;
        topShieldRenderer.sortingOrder = topOutlineSR.sortingOrder + 2;
        bottomHighlightRenderer.sortingOrder = topShieldRenderer.sortingOrder + 1;
        topHighlightRenderer.sortingOrder = topShieldRenderer.sortingOrder + 2;
        // [5단계] 별자리 표식(StarrootForest): topHighlight보다 한 단계 앞에 그려진다.
        if (constellationRenderer != null) constellationRenderer.sortingOrder = topHighlightRenderer.sortingOrder + 1;
    }

    /// <summary>
    /// StarrootForest 별 표식 마커(Constellation)의 표시 여부를 전환한다.
    /// </summary>
    public void SetConstellationMarkActive(bool _active)
    {
        if (constellationRenderer != null)
        {
            constellationRenderer.gameObject.SetActive(_active);
            UpdateHDRStates();
        }
    }

    // 루트 트랜스폼이 틀어졌을 때 위치, 회전, 스케일을 모두 기본값으로 맞춘다.
    public void NormalizeVisualRootTransform()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    // 상단/하단 스프라이트를 랜덤으로 고르고 그림자 비주얼까지 함께 갱신한다. (에디터 미리보기용)
    private void ApplyRandomVisual()
    {
        if (treeVisualDataBase != null)
        {
            TreeVisualData customVisualData = treeVisualDataBase.Get(customTreeType);
            if (customVisualData.treeType != TreeType.None)
            {
                // 에디터 설정 시에는 바리에이션 요동을 막기 위해 첫 번째 대표 스프라이트로 고정
                int bottomIndex = SetFirstSprite(bottomRenderer, customVisualData.bottomSprites);
                defaultBottomSprite = bottomRenderer.sprite;

                int topIndex = SetFirstSprite(topRenderer, customVisualData.topSprites);
                defaultTopSprite = topRenderer.sprite;

                if (topShieldRenderer != null)
                {
                    topShieldRenderer.sprite = (topIndex >= 0 && customVisualData.shieldTopSprites != null && topIndex < customVisualData.shieldTopSprites.Count) ? customVisualData.shieldTopSprites[topIndex] : null;
                    topShieldRenderer.gameObject.SetActive(topShieldRenderer.sprite != null);
                }

                if (bottomShieldRenderer != null)
                {
                    bottomShieldRenderer.sprite = (bottomIndex >= 0 && customVisualData.shieldBottomSprites != null && bottomIndex < customVisualData.shieldBottomSprites.Count) ? customVisualData.shieldBottomSprites[bottomIndex] : null;
                    bottomShieldRenderer.gameObject.SetActive(bottomShieldRenderer.sprite != null);
                }

                if (topHighlightRenderer != null)
                {
                    topHighlightRenderer.sprite = (topIndex >= 0 && customVisualData.highlightTopSprites != null && topIndex < customVisualData.highlightTopSprites.Count) ? customVisualData.highlightTopSprites[topIndex] : null;
                    topHighlightRenderer.gameObject.SetActive(topHighlightRenderer.sprite != null);
                }

                if (bottomHighlightRenderer != null)
                {
                    bottomHighlightRenderer.sprite = (bottomIndex >= 0 && customVisualData.highlightBottomSprites != null && bottomIndex < customVisualData.highlightBottomSprites.Count) ? customVisualData.highlightBottomSprites[bottomIndex] : null;
                    bottomHighlightRenderer.gameObject.SetActive(bottomHighlightRenderer.sprite != null);
                }

                isShieldActive = ((topShieldRenderer != null && topShieldRenderer.sprite != null) || (bottomShieldRenderer != null && bottomShieldRenderer.sprite != null));

                UpdateRendererSprites();
                ApplyDefaultScale();
                shieldHDRIntensity = customVisualData.shieldHDRIntensity;
                highlightHDRIntensity = customVisualData.highlightHDRIntensity;
                currentTopVfxColor = customVisualData.topVfxColor;
                currentBottomVfxColor = customVisualData.bottomVfxColor;
                UpdateHDRStates();
                return;
            }
        }

        // 데이터베이스가 없는 경우 기본 인스펙터 배열에서 랜덤 선택
        SetRandomSprite(bottomRenderer, bottomSprites);
        defaultBottomSprite = bottomRenderer.sprite;
        if (bottomShieldRenderer != null) bottomShieldRenderer.sprite = null;
        if (bottomHighlightRenderer != null) bottomHighlightRenderer.sprite = null;

        SetRandomSprite(topRenderer, topSprites);
        defaultTopSprite = topRenderer.sprite;
        if (topShieldRenderer != null) topShieldRenderer.sprite = null;
        if (topHighlightRenderer != null) topHighlightRenderer.sprite = null;

        isShieldActive = false;

        UpdateRendererSprites();
        ApplyDefaultScale();
        UpdateHDRStates();
    }

    public void RefreshVisualPreview()
    {
        if (topRenderer != null) topRenderer.color = Color.white;
        if (bottomRenderer != null) bottomRenderer.color = Color.white;
        
        ApplyRandomVisual();
        SyncShadowSprite();
    }
    #endregion

    #region Apply Data

    // 트리 데이터가 적용될 때 데이터에 정의된 스프라이트를 적용한다.
    public void ApplyVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;
        int topIndex = -1;
        int bottomIndex = -1;

        if (topRenderer != null)
        {
            topIndex = SetRandomSprite(topRenderer, visualData.topSprites);
            defaultTopSprite = topRenderer.sprite;
        }

        if (bottomRenderer != null)
        {
            bottomIndex = SetRandomSprite(bottomRenderer, visualData.bottomSprites);
            defaultBottomSprite = bottomRenderer.sprite;
        }

        if (topShieldRenderer != null)
        {
            topShieldRenderer.sprite = (topIndex >= 0 && visualData.shieldTopSprites != null && topIndex < visualData.shieldTopSprites.Count) ? visualData.shieldTopSprites[topIndex] : null;
            topShieldRenderer.gameObject.SetActive(topShieldRenderer.sprite != null);
        }

        if (bottomShieldRenderer != null)
        {
            bottomShieldRenderer.sprite = (bottomIndex >= 0 && visualData.shieldBottomSprites != null && bottomIndex < visualData.shieldBottomSprites.Count) ? visualData.shieldBottomSprites[bottomIndex] : null;
            bottomShieldRenderer.gameObject.SetActive(bottomShieldRenderer.sprite != null);
        }

        if (topHighlightRenderer != null)
        {
            topHighlightRenderer.sprite = (topIndex >= 0 && visualData.highlightTopSprites != null && topIndex < visualData.highlightTopSprites.Count) ? visualData.highlightTopSprites[topIndex] : null;
            topHighlightRenderer.gameObject.SetActive(topHighlightRenderer.sprite != null);
        }

        if (bottomHighlightRenderer != null)
        {
            bottomHighlightRenderer.sprite = (bottomIndex >= 0 && visualData.highlightBottomSprites != null && bottomIndex < visualData.highlightBottomSprites.Count) ? visualData.highlightBottomSprites[bottomIndex] : null;
            bottomHighlightRenderer.gameObject.SetActive(bottomHighlightRenderer.sprite != null);
        }

        // 초기 쉴드 활성화 여부 판단 (쉴드 스프라이트가 존재하면 활성화)
        isShieldActive = ((topShieldRenderer != null && topShieldRenderer.sprite != null) || (bottomShieldRenderer != null && bottomShieldRenderer.sprite != null));

        UpdateRendererSprites();
        ApplyDefaultScale();

        shieldHDRIntensity = _treeData.treeVisualData.shieldHDRIntensity;
        highlightHDRIntensity = _treeData.treeVisualData.highlightHDRIntensity;
        currentTopVfxColor = _treeData.treeVisualData.topVfxColor;
        currentBottomVfxColor = _treeData.treeVisualData.bottomVfxColor;
        UpdateHDRStates();
    }

    // 묘목(Sapling) 비주얼을 적용한다.
    public void ApplySaplingVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (topRenderer != null)
        {
            SetRandomSprite(topRenderer, visualData.saplingTopSprites);
            defaultTopSprite = topRenderer.sprite;
        }

        if (bottomRenderer != null)
        {
            SetRandomSprite(bottomRenderer, visualData.saplingBottomSprites);
            defaultBottomSprite = bottomRenderer.sprite;
        }

        if (topShieldRenderer != null) topShieldRenderer.sprite = null;
        if (bottomShieldRenderer != null) bottomShieldRenderer.sprite = null;
        if (topHighlightRenderer != null)
        {
            topHighlightRenderer.sprite = null;
            topHighlightRenderer.gameObject.SetActive(false);
        }
        if (bottomHighlightRenderer != null)
        {
            bottomHighlightRenderer.sprite = null;
            bottomHighlightRenderer.gameObject.SetActive(false);
        }

        isShieldActive = false;

        UpdateRendererSprites();
        ApplyDefaultScale();
        currentTopVfxColor = visualData.topVfxColor;
        currentBottomVfxColor = visualData.bottomVfxColor;
        UpdateHDRStates();
    }

    public void DeActivateOnWaterObject()
    {
        isOnWaterActive = false;
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(false);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(false);
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(false);
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.gameObject.SetActive(false);
        UpdateHDRStates();
    }

    public void ActivateOnWaterObject()
    {
        isOnWaterActive = true;
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(true);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(true);

        if (topShieldOnWaterSR != null)
        {
            topShieldOnWaterSR.gameObject.SetActive(isShieldActive && topShieldRenderer != null && topShieldRenderer.sprite != null);
        }

        if (topHighlightOnWaterSR != null)
        {
            topHighlightOnWaterSR.gameObject.SetActive(topHighlightRenderer != null && topHighlightRenderer.sprite != null);
        }

        UpdateOnWaterSortingOrder();
        UpdateHDRStates();
    }

    // 나무의 전체적인 크기를 기본값(1.0)으로 설정한다.
    private void ApplyDefaultScale()
    {
        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one;
        }
    }

    private void UpdateRendererSprites()
    {
        if (topRenderer != null)
        {
            topRenderer.sprite = defaultTopSprite;
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.sprite = defaultBottomSprite;
        }

        if (topShieldRenderer != null)
        {
            topShieldRenderer.gameObject.SetActive(isShieldActive && topShieldRenderer.sprite != null);
        }

        if (bottomShieldRenderer != null)
        {
            bottomShieldRenderer.gameObject.SetActive(isShieldActive && bottomShieldRenderer.sprite != null);
        }

        if (topShieldOnWaterSR != null)
        {
            topShieldOnWaterSR.gameObject.SetActive(isOnWaterActive && isShieldActive && topShieldRenderer != null && topShieldRenderer.sprite != null);
        }

        if (topHighlightOnWaterSR != null)
        {
            topHighlightOnWaterSR.gameObject.SetActive(isOnWaterActive && topHighlightRenderer != null && topHighlightRenderer.sprite != null);
        }

        SyncShadowSprite();
    }

    // 그림자 및 물 위 렌더러가 본체와 같은 스프라이트와 색상을 따라가도록 동기화한다.
    private void SyncShadowSprite()
    {
        if (topRenderer != null)
        {
            if (topShadowRenderer != null)
            {
                topShadowRenderer.sprite = topRenderer.sprite;
                topShadowRenderer.color = topRenderer.color;
            }

            if (topOnWaterSR != null)
            {
                topOnWaterSR.sprite = topRenderer.sprite;
                topOnWaterSR.color = topRenderer.color;
            }

            if (topShieldOnWaterSR != null)
            {
                topShieldOnWaterSR.sprite = topShieldRenderer != null ? topShieldRenderer.sprite : null;
                topShieldOnWaterSR.color = topShieldRenderer != null ? topShieldRenderer.color : Color.white;
            }

            if (topHighlightOnWaterSR != null)
            {
                topHighlightOnWaterSR.sprite = topHighlightRenderer != null ? topHighlightRenderer.sprite : null;
                topHighlightOnWaterSR.color = topHighlightRenderer != null ? topHighlightRenderer.color : Color.white;
            }

            if (topOutlineSR != null)
            {
                topOutlineSR.sprite = topRenderer.sprite;
                Color outlineColor = topOutlineSR.color;
                outlineColor.a = topRenderer.color.a;
                topOutlineSR.color = outlineColor;
            }

            if (topStencilOutlineSR != null)
            {
                topStencilOutlineSR.sprite = topRenderer.sprite;
            }
        }

        if (bottomRenderer != null)
        {
            if (bottomShadowRenderer != null)
            {
                bottomShadowRenderer.sprite = bottomRenderer.sprite;
                bottomShadowRenderer.color = bottomRenderer.color;
            }

            if (bottomOnWaterSR != null)
            {
                bottomOnWaterSR.sprite = bottomRenderer.sprite;
                bottomOnWaterSR.color = bottomRenderer.color;
            }

            if (bottomOutlineSR != null)
            {
                bottomOutlineSR.sprite = bottomRenderer.sprite;
                Color outlineColor = bottomOutlineSR.color;
                outlineColor.a = bottomRenderer.color.a;
                bottomOutlineSR.color = outlineColor;
            }

            if (bottomStencilOutlineSR != null)
            {
                bottomStencilOutlineSR.sprite = bottomRenderer.sprite;
            }
        }
    }

    // 전달받은 렌더러에 스프라이트 리스트 중 하나를 무작위로 적용하고 선택된 인덱스를 반환한다.
    private static int SetRandomSprite(SpriteRenderer _renderer, System.Collections.Generic.List<Sprite> _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Count == 0)
        {
            return -1;
        }

        int index = Random.Range(0, _sprites.Count);
        _renderer.sprite = _sprites[index];
        return index;
    }

    // 전달받은 렌더러에 스프라이트 배열 중 하나를 무작위로 적용하고 선택된 인덱스를 반환한다.
    private static int SetRandomSprite(SpriteRenderer _renderer, Sprite[] _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Length == 0)
        {
            return -1;
        }

        int index = Random.Range(0, _sprites.Length);
        _renderer.sprite = _sprites[index];
        return index;
    }

    // 전달받은 렌더러에 스프라이트 리스트 중 첫 번째(기본) 스프라이트를 고정 적용하고 0을 반환한다.
    private static int SetFirstSprite(SpriteRenderer _renderer, System.Collections.Generic.List<Sprite> _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Count == 0)
        {
            return -1;
        }

        _renderer.sprite = _sprites[0];
        return 0;
    }

    // 전달받은 렌더러에 스프라이트 배열 중 첫 번째(기본) 스프라이트를 고정 적용하고 0을 반환한다.
    private static int SetFirstSprite(SpriteRenderer _renderer, Sprite[] _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Length == 0)
        {
            return -1;
        }

        _renderer.sprite = _sprites[0];
        return 0;
    }

    #endregion

    #region Motion

    // 피격 시 나무 전체가 짧게 옆으로 흔들리도록 루트에 펀치 이동을 준다.
    public void PlayHitFeedback()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.DOKill();
        visualRoot.localPosition = Vector3.zero;
        visualRoot.DOPunchPosition(new Vector3(hitPunchX, 0f, 0f), hitDuration, hitVibrato, hitElasticity);
    }

    // 피격 시 나무 스프라이트가 짧게 흰색으로 번쩍였다가 원래 색으로 돌아오도록 한다.
    public void PlayHitFlash()
    {
        PlayFlash(hitFlashDuration, hitFlashCurve);
    }

    // 묘목이 다 자라 스케일이 최대가 되는 순간, 피격 플래시와 같은 셰이더로 한 번 하얗게 반짝인다.
    public void PlayGrowUpFlash()
    {
        PlayFlash(growUpFlashDuration, growUpFlashCurve);
    }

    private void PlayFlash(float _duration, AnimationCurve _curve)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(FlashRoutine(_duration, _curve));
    }

    private IEnumerator FlashRoutine(float _duration, AnimationCurve _curve)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        float elapsed = 0f;
        while (elapsed < _duration)
        {
            float t = elapsed / _duration;
            float flash = _curve.Evaluate(t);

            ApplyFlashAmountToRenderers(flash);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyFlashAmountToRenderers(0f);

        hitFlashCoroutine = null;
    }

    private void ApplyFlashAmountToRenderers(float flash)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        
        SetFlashAmountToRenderer(topRenderer, flash);
        SetFlashAmountToRenderer(bottomRenderer, flash);
        SetFlashAmountToRenderer(topShieldRenderer, flash);
        SetFlashAmountToRenderer(bottomShieldRenderer, flash);
        SetFlashAmountToRenderer(topHighlightRenderer, flash);
        SetFlashAmountToRenderer(bottomHighlightRenderer, flash);
    }

    private void SetFlashAmountToRenderer(SpriteRenderer sr, float flash)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetFloat(FlashAmountID, flash);
        sr.SetPropertyBlock(_flashMPB);
    }

    // VFX 재생에 필요한 위치/회전/색상 데이터를 외부(InDungeonVFXManager)에 제공한다.
    public Vector3 GetTopRootPosition() => topRoot != null ? topRoot.position : transform.position;
    public Vector3 GetBottomRootPosition() => bottomRoot != null ? bottomRoot.position : transform.position;
    public Quaternion GetTopRootRotation() => topRoot != null ? topRoot.rotation : Quaternion.identity;
    public Quaternion GetBottomRootRotation() => bottomRoot != null ? bottomRoot.rotation : Quaternion.identity;
    public ParticleColorSet GetTopVfxColor() => currentTopVfxColor;
    public ParticleColorSet GetBottomVfxColor() => currentBottomVfxColor;

    // 누적된 연출 값을 지우고 비주얼을 기본 위치와 포즈로 되돌린다.
    public void ResetVisualState()
    {
        // 보석 비주얼은 여기서 건드리지 않는다. 켜고 끄는 판단은 TreeObj가 단독으로 갖고 있고
        // (TreeObj.bGemVisual), ResetTree가 이 함수 호출 직후에 그 값을 다시 적용한다.

        if (visualRoot == null)
        {
            return;
        }

        // 1. 기존 비주얼 루트(전체 쉐이킹 등) 초기화
        visualRoot.DOKill();
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localScale = Vector3.one;

        // 2. 묘목(Sapling) 애니메이션이 사용했던 Transform 크기 초기화
        transform.DOKill();
        transform.localScale = Vector3.one;

        // 3. 투명도(Alpha)를 완전한 불투명(1.0f) 상태로 복구
        SetAlpha(1.0f);

        // 4. 피격 Flash 연출 초기화 (풀 반환 시 흰색 상태로 남는 것을 방지)
        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        ApplyFlashAmountToRenderers(0f);
    }



    private void ApplyAlpha(float _alpha)
    {
        Color topColor = topRenderer.color;
        topColor.a = _alpha;
        topRenderer.color = topColor;

        Color bottomColor = bottomRenderer.color;
        bottomColor.a = _alpha;
        bottomRenderer.color = bottomColor;

        if (topShadowRenderer != null)
        {
            Color tsColor = topShadowRenderer.color;
            tsColor.a = _alpha;
            topShadowRenderer.color = tsColor;
        }

        if (bottomShadowRenderer != null)
        {
            Color bsColor = bottomShadowRenderer.color;
            bsColor.a = _alpha;
            bottomShadowRenderer.color = bsColor;
        }

        if (topShieldRenderer != null)
        {
            Color color = topShieldRenderer.color;
            color.a = _alpha;
            topShieldRenderer.color = color;
        }

        if (bottomShieldRenderer != null)
        {
            Color color = bottomShieldRenderer.color;
            color.a = _alpha;
            bottomShieldRenderer.color = color;
        }

        if (topHighlightRenderer != null)
        {
            Color color = topHighlightRenderer.color;
            color.a = _alpha;
            topHighlightRenderer.color = color;
        }

        if (bottomHighlightRenderer != null)
        {
            Color color = bottomHighlightRenderer.color;
            color.a = _alpha;
            bottomHighlightRenderer.color = color;
        }
    }

    public void SetAlpha(float _alpha)
    {
        this.DOKill(this); // 현재 스크립트 기반 float 트윈 정지

        topRenderer.DOKill();
        bottomRenderer.DOKill();
        if (topShadowRenderer != null) topShadowRenderer.DOKill();
        if (bottomShadowRenderer != null) bottomShadowRenderer.DOKill();
        if (topShieldRenderer != null) topShieldRenderer.DOKill();
        if (bottomShieldRenderer != null) bottomShieldRenderer.DOKill();
        if (topHighlightRenderer != null) topHighlightRenderer.DOKill();
        if (bottomHighlightRenderer != null) bottomHighlightRenderer.DOKill();

        ApplyAlpha(_alpha);
    }

    private float GetCurrentAlpha()
    {
        return currentAlpha;
    }

    private void SetCurrentAlpha(float _alpha)
    {
        currentAlpha = _alpha;
        ApplyAlpha(_alpha);
    }

    public void FadeAlpha(float _targetAlpha, float _duration)
    {
        this.DOKill(this); // 기존 트윈 취소

        topRenderer.DOKill();
        bottomRenderer.DOKill();
        if (topShadowRenderer != null) topShadowRenderer.DOKill();
        if (bottomShadowRenderer != null) bottomShadowRenderer.DOKill();
        if (topShieldRenderer != null) topShieldRenderer.DOKill();
        if (bottomShieldRenderer != null) bottomShieldRenderer.DOKill();
        if (topHighlightRenderer != null) topHighlightRenderer.DOKill();
        if (bottomHighlightRenderer != null) bottomHighlightRenderer.DOKill();

        currentAlpha = topRenderer != null ? topRenderer.color.a : 1f;

        DOTween.To(GetCurrentAlpha, SetCurrentAlpha, _targetAlpha, _duration).SetTarget(this);
    }

    public void SetOutline(bool _boolean)
    {
        if (bDisableOutline == true)
            return;

        isOutlineActive = _boolean;

        if (outlineVisualObj != null)
        {
            outlineVisualObj.SetActive(_boolean);
        }
    }

    public void DisableOutline()
    {
        bDisableOutline = true;
    }

    public void EnableOutline()
    {
        bDisableOutline = false;
    }

    public void ShieldBroken()
    {
        isShieldActive = false;
        UpdateRendererSprites();
        UpdateHDRStates();
    }

    public void ShieldRegened()
    {
        isShieldActive = true;
        UpdateRendererSprites();
        UpdateHDRStates();
    }

    // 보석 머티리얼(또는 그 복제본)인지 판별한다. 복제본도 gem 프로퍼티를 갖고 있으므로
    // 원본 머티리얼을 캐싱할 때 이걸로 걸러야 한다.
    private static bool IsGemMaterial(Material _material)
    {
        return _material != null && _material.HasProperty(GemColorID);
    }

    // 원본 머티리얼을 아직 모르는 경우에만 현재 값을 기록한다.
    // 이미 보석 머티리얼이 적용된 상태를 원본으로 잘못 굳히지 않도록 걸러낸다.
    private void EnsureDefaultMaterialsCached()
    {
        if (defaultTopMaterial == null && topRenderer != null && !IsGemMaterial(topRenderer.sharedMaterial))
        {
            defaultTopMaterial = topRenderer.sharedMaterial;
        }

        if (defaultBottomMaterial == null && bottomRenderer != null && !IsGemMaterial(bottomRenderer.sharedMaterial))
        {
            defaultBottomMaterial = bottomRenderer.sharedMaterial;
        }
    }

    /// <summary>
    /// 본체 렌더러의 머티리얼을 보석 머티리얼로 교체하거나 원본으로 되돌린다.
    /// 보석 종류별 색은 베이스 머티리얼을 복제한 인스턴스로 처리한다.
    /// </summary>
    public void ApplyGemVisual(bool _active, TreeGrade _grade = TreeGrade.None)
    {
        EnsureDefaultMaterialsCached();

        if (!_active)
        {
            if (topRenderer != null && defaultTopMaterial != null) topRenderer.sharedMaterial = defaultTopMaterial;
            if (bottomRenderer != null && defaultBottomMaterial != null) bottomRenderer.sharedMaterial = defaultBottomMaterial;
            // 원본 머티리얼로 되돌린 뒤에 읽어야 그 머티리얼에 저장된 원래 sway 설정을 복원할 수 있다.
            SetWindSwayEnabled(true);
            return;
        }

        if (!TryGetGemMaterialSet(_grade, out TreeGemMaterialSet materialSet)) return;

        if (topRenderer != null && materialSet.topMaterial != null)
        {
            topRenderer.sharedMaterial = materialSet.topMaterial;
        }

        if (bottomRenderer != null && materialSet.bottomMaterial != null)
        {
            bottomRenderer.sharedMaterial = materialSet.bottomMaterial;
        }

        SetWindSwayEnabled(false);
    }

    /// <summary>
    /// 이 나무의 모든 렌더러에 바람 흔들림을 켜거나 끈다.
    ///
    /// 셰이더의 ApplyWindSway가 _EnableWindSway &lt; 0.5일 때 정점을 변형하지 않고 원본 위치를 그대로
    /// 반환하므로, 끄는 순간의 흔들린 자세가 남지 않고 sway 연산이 아예 없었던 정지 포즈로 그려진다.
    ///
    /// 본체/그림자/아웃라인/스텐실이 같은 sway 함수를 공유하므로 한꺼번에 꺼야 실루엣이 어긋나지 않는다.
    /// </summary>
    private void SetWindSwayEnabled(bool _enabled)
    {
        // 나무 스폰/반환마다 ResetTree가 복원을 호출하므로, 실제로 상태가 바뀔 때만 렌더러를 건드린다.
        // (던전 하나에 나무가 수천 그루라 매번 전 렌더러를 순회하면 로드 시 부하가 커진다)
        if (bWindSwayEnabled == _enabled) return;
        bWindSwayEnabled = _enabled;

        ApplyWindSwayToRenderer(topRenderer, _enabled);
        ApplyWindSwayToRenderer(bottomRenderer, _enabled);
        ApplyWindSwayToRenderer(topShieldRenderer, _enabled);
        ApplyWindSwayToRenderer(bottomShieldRenderer, _enabled);
        ApplyWindSwayToRenderer(topHighlightRenderer, _enabled);
        ApplyWindSwayToRenderer(bottomHighlightRenderer, _enabled);
        ApplyWindSwayToRenderer(topShadowRenderer, _enabled);
        ApplyWindSwayToRenderer(bottomShadowRenderer, _enabled);
        ApplyWindSwayToRenderer(topOutlineSR, _enabled);
        ApplyWindSwayToRenderer(bottomOutlineSR, _enabled);
        ApplyWindSwayToRenderer(topStencilOutlineSR, _enabled);
        ApplyWindSwayToRenderer(bottomStencilOutlineSR, _enabled);
        ApplyWindSwayToRenderer(constellationRenderer, _enabled);
    }

    private void ApplyWindSwayToRenderer(SpriteRenderer _renderer, bool _enabled)
    {
        if (_renderer == null) return;

        // sway를 쓰지 않는 셰이더(물 위 반사 등)는 건너뛴다.
        Material material = _renderer.sharedMaterial;
        if (material == null || !material.HasProperty(EnableWindSwayID)) return;

        // 되돌릴 때는 1로 고정하지 않고 머티리얼에 저장된 원래 값을 쓴다.
        // 나무 종류나 렌더러에 따라 애초에 sway가 꺼져 있을 수 있기 때문이다.
        float value = _enabled ? material.GetFloat(EnableWindSwayID) : 0f;

        // GetPropertyBlock으로 기존 오버라이드(HDR 등)를 보존한 뒤 sway 값만 덮어쓴다.
        _renderer.GetPropertyBlock(Mpb);
        Mpb.SetFloat(EnableWindSwayID, value);
        _renderer.SetPropertyBlock(Mpb);
    }

    /// <summary>
    /// 등급에 맞는 머티리얼 세트를 찾는다.
    /// 등급 매핑이 없으면 defaultGemType 세트를 쓴다.
    /// </summary>
    private bool TryGetGemMaterialSet(TreeGrade _grade, out TreeGemMaterialSet _materialSet)
    {
        _materialSet = default;
        if (gemMaterialSets == null || gemMaterialSets.Length == 0) return false;

        TreeGemType gemType = defaultGemType;
        if (treeGemColorDataBase != null && treeGemColorDataBase.TryResolveGemType(_grade, out TreeGemType resolved))
        {
            gemType = resolved;
        }

        for (int i = 0; i < gemMaterialSets.Length; i++)
        {
            if (gemMaterialSets[i].gemType == gemType)
            {
                _materialSet = gemMaterialSets[i];
                return true;
            }
        }

        // 지정한 종류의 세트가 없으면 첫 번째 세트로라도 보석 비주얼은 유지한다.
        _materialSet = gemMaterialSets[0];
        return true;
    }

    private void ApplyHDRToRenderer(SpriteRenderer _renderer, bool _active, float _intensity)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(Mpb);
        Mpb.SetFloat(HDRIntensityID, _active ? _intensity : 1f);
        _renderer.SetPropertyBlock(Mpb);
    }

    private void UpdateHDRStates()
    {
        if (topHighlightRenderer != null) ApplyHDRToRenderer(topHighlightRenderer, topHighlightRenderer.sprite != null, highlightHDRIntensity);
        if (bottomHighlightRenderer != null) ApplyHDRToRenderer(bottomHighlightRenderer, bottomHighlightRenderer.sprite != null, highlightHDRIntensity);

        if (topShieldRenderer != null) ApplyHDRToRenderer(topShieldRenderer, isShieldActive && topShieldRenderer.sprite != null, shieldHDRIntensity);
        if (bottomShieldRenderer != null) ApplyHDRToRenderer(bottomShieldRenderer, isShieldActive && bottomShieldRenderer.sprite != null, shieldHDRIntensity);

        if (topShieldOnWaterSR != null) ApplyHDRToRenderer(topShieldOnWaterSR, isOnWaterActive && isShieldActive && topShieldOnWaterSR.sprite != null, shieldHDRIntensity + 0.25f);
        if (topHighlightOnWaterSR != null) ApplyHDRToRenderer(topHighlightOnWaterSR, isOnWaterActive && topHighlightOnWaterSR.sprite != null, highlightHDRIntensity + 0.25f);

        if (constellationRenderer != null) ApplyHDRToRenderer(constellationRenderer, constellationRenderer.gameObject.activeSelf && constellationRenderer.sprite != null, highlightHDRIntensity);
    }

    #endregion

    #region Unity Events

    private void Awake()
    {
        cachedTransform = transform;

        if (topRenderer != null) topRenderer.color = Color.white;
        if (bottomRenderer != null) bottomRenderer.color = Color.white;
        if (topShadowRenderer != null) topShadowRenderer.color = Color.white;
        if (bottomShadowRenderer != null) bottomShadowRenderer.color = Color.white;
        if (topOnWaterSR != null) topOnWaterSR.color = Color.white;
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.color = Color.white;
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.color = Color.white;
        if (bottomOnWaterSR != null) bottomOnWaterSR.color = Color.white;
        if (topOutlineSR != null) topOutlineSR.color = Color.white;
        if (bottomOutlineSR != null) bottomOutlineSR.color = Color.white;
        if (topStencilOutlineSR != null) topStencilOutlineSR.color = Color.white;
        if (bottomStencilOutlineSR != null) bottomStencilOutlineSR.color = Color.white;

        UpdateOnWaterSortingOrder();
    }

    // 나무가 죽었음을 알린다. VFX 재생은 InDungeonVFXManager에서 담당한다.
    public void TreeIsDead()
    {
    }

#if UNITY_EDITOR
    // 인스펙터에서 미리보기 종류나 베이스 머티리얼을 바꿨을 때 씬 뷰에 즉시 반영한다.
    // 이미 보석이 켜져 있을 때만 다시 적용한다.
    private void RefreshGemVisualInEditor()
    {
        bool isGemActive = (topRenderer != null && IsGemMaterial(topRenderer.sharedMaterial))
                        || (bottomRenderer != null && IsGemMaterial(bottomRenderer.sharedMaterial));

        if (!isGemActive) return;

        // 색 종류는 데이터(TreeGemColorDataBase)가 등급 또는 디버그 강제로 결정한다.
        // 머티리얼 인스턴스는 버리지 않고 색만 다시 계산되므로 DestroyImmediate가 필요 없다.
        ApplyGemVisual(true);
    }
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        // 인스펙터에서 미리보기 종류나 베이스 머티리얼을 바꾼 것을 바로 반영한다.
        RefreshGemVisualInEditor();
#endif

        if (Application.isPlaying || !previewInEditor)
        {
            return;
        }

#if UNITY_EDITOR
        if (treeVisualDataBase == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TreeVisualDataBase");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                treeVisualDataBase = UnityEditor.AssetDatabase.LoadAssetAtPath<TreeVisualDataBase>(path);
            }
        }
#endif
    }

    #endregion
}

