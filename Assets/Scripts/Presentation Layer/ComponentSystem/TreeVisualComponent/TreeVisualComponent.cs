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

    [Tooltip("물 위 반사용. 물결 일렁임을 유지하면서 같은 보석 효과를 얹은 머티리얼(Custom/OnWaterObject-Gem).")]
    public Material onWaterMaterial;
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
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private Color criticalHitFlashColor = Color.red;

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
    // FadeAlpha의 DOTween.To에 메서드 그룹을 넘기면 호출마다 getter/setter 델리게이트가 할당되므로 한 번만 만든다.
    private DG.Tweening.Core.DOGetter<float> alphaGetter;
    private DG.Tweening.Core.DOSetter<float> alphaSetter;

    // 이 컴포넌트가 마지막으로 건 트윈들. 각 타깃(visualRoot / this)에 트윈을 거는 곳은 각각 PlayHitFeedback /
    // FadeAlpha 한 곳뿐이고 새로 걸기 전에 항상 이전 것을 먼저 죽이므로, 그 타깃에 살아 있을 수 있는 트윈은 이
    // 참조가 가리키는 마지막 트윈 하나뿐이다. 그래서 타깃 기준 DOKill(DOTween 활성 트윈 전체를 선형 탐색) 대신
    // 이 트윈 하나만 직접 죽인다(KillHitPunchTween / KillAlphaTween).
    // 재활용 트윈(피격 펀치)은 죽은 뒤 다른 나무의 트윈으로 다시 쓰일 수 있으므로, 참조의 target이 여전히 내
    // 타깃인지 확인한다. 아니라면 내 트윈은 이미 죽은 것이고, 타깃 기준 DOKill도 아무것도 죽이지 않았을 것이다.
    private Tween hitPunchTween;
    // TreeFlashSystem 목록에서 이 나무 항목의 위치(없으면 -1). TreeFlashSystem만 읽고 쓴다.
    internal int flashEntryIndex = -1;
    private Tween alphaTween;
    // 이 나무의 transform에 스케일 트윈을 거는 묘목 연출. 연결되지 않았으면 예전처럼 항상 DOKill한다.
    private SaplingVEComponent saplingVEComponent;
    private bool isShieldActive = false;
    private bool isOnWaterActive = false;

    // 보석 머티리얼인지 판별하는 데 쓰는 프로퍼티
    private static readonly int GemColorID = Shader.PropertyToID("_GemColor");

    // 바람 흔들림. 보석 나무는 흔들리지 않아야 하므로 개체별로 꺼야 한다.
    private static readonly int EnableWindSwayID = Shader.PropertyToID("_EnableWindSway");
    // 현재 이 나무에 sway가 켜져 있는지. 갓 생성된 렌더러는 오버라이드가 없어 머티리얼 기본값(켜짐)을 따르므로 true로 시작한다.
    private bool bWindSwayEnabled = true;

    // 보석 상태에서는 본체(top/bottom)만 보석 재질로 바뀌고 실드/하이라이트 스프라이트는 그리지 않는다.
    // 실드의 게임플레이 상태(isShieldActive)와는 무관한 순수 표시 여부 플래그다.
    private bool bGemActive = false;

    // Shield HDR
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private MaterialPropertyBlock _mpb;
    private MaterialPropertyBlock Mpb => _mpb ??= new MaterialPropertyBlock();

    // Hit Flash
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");
    // 치명타(빨간색) 점멸이 끝나는 시각. 이 시각 전까지 들어온 일반 타격은 점멸을 덮어쓰지 않는다.
    // bool 플래그 대신 시각으로 들고 있는 이유: 점멸 도중 오브젝트가 꺼지면 코루틴이 끝을 못 봐서
    // 플래그가 켜진 채 남을 수 있는데, 시각은 시간이 지나면 저절로 풀린다.
    private float criticalFlashEndTime = 0f;
    private MaterialPropertyBlock _flashMPB;
    // 플래시 진행은 TreeFlashSystem이 한 곳에서 틱한다(나무마다 코루틴을 만들지 않는다)

    // Gem Visual - 보석 머티리얼로 갈아끼우기 전의 원본 머티리얼.
    // 에디터에서 인스펙터로 토글하면 스크립트 재컴파일(도메인 리로드)로 런타임 필드가 날아가는데,
    // 그때 이미 보석 머티리얼이 적용된 상태를 "원본"으로 다시 캐싱해버리면 되돌릴 수가 없다.
    // 그래서 직렬화해서 리로드를 넘겨 살린다.
    [SerializeField, HideInInspector] private Material defaultTopMaterial;
    [SerializeField, HideInInspector] private Material defaultBottomMaterial;
    // 물 위 반사도 본체와 같이 보석 재질로 갈아끼우므로 원본을 함께 보관한다.
    [SerializeField, HideInInspector] private Material defaultOnWaterMaterial;

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
            // ResetTree가 스폰/반환마다 false로 호출한다. 이미 같은 상태면 HDR 재적용(렌더러 7개 MPB 갱신)까지 전부 생략한다.
            // HDR 입력값(스프라이트/실드/물 위 상태)이 바뀌는 경로는 각자 UpdateHDRStates를 호출하므로 결과는 동일하다.
            if (constellationRenderer.gameObject.activeSelf == _active) return;

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

        // 보석 상태에서는 실드/하이라이트를 그리지 않으므로 물 위 반사도 함께 숨긴다.
        if (topShieldOnWaterSR != null)
        {
            topShieldOnWaterSR.gameObject.SetActive(!bGemActive && isShieldActive && topShieldRenderer != null && topShieldRenderer.sprite != null);
        }

        if (topHighlightOnWaterSR != null)
        {
            topHighlightOnWaterSR.gameObject.SetActive(!bGemActive && topHighlightRenderer != null && topHighlightRenderer.sprite != null);
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

        // 보석 상태에서는 실드/하이라이트를 그리지 않는다 (본체만 보석으로 바뀐다).
        bool bShowShield = isShieldActive && !bGemActive;
        bool bShowHighlight = !bGemActive;

        if (topShieldRenderer != null)
        {
            topShieldRenderer.gameObject.SetActive(bShowShield && topShieldRenderer.sprite != null);
        }

        if (bottomShieldRenderer != null)
        {
            bottomShieldRenderer.gameObject.SetActive(bShowShield && bottomShieldRenderer.sprite != null);
        }

        // 하이라이트는 원래 ApplyVisual에서만 켜졌는데, 보석 전환 때 다시 판단해야 하므로 여기로 모은다.
        if (topHighlightRenderer != null)
        {
            topHighlightRenderer.gameObject.SetActive(bShowHighlight && topHighlightRenderer.sprite != null);
        }

        if (bottomHighlightRenderer != null)
        {
            bottomHighlightRenderer.gameObject.SetActive(bShowHighlight && bottomHighlightRenderer.sprite != null);
        }

        if (topShieldOnWaterSR != null)
        {
            topShieldOnWaterSR.gameObject.SetActive(isOnWaterActive && bShowShield && topShieldRenderer != null && topShieldRenderer.sprite != null);
        }

        if (topHighlightOnWaterSR != null)
        {
            topHighlightOnWaterSR.gameObject.SetActive(isOnWaterActive && bShowHighlight && topHighlightRenderer != null && topHighlightRenderer.sprite != null);
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

        KillHitPunchTween();
        visualRoot.localPosition = Vector3.zero;
        // 피격마다 새 Tweener를 만들지 않도록 재활용한다. 죽일 때는 target이 visualRoot인지 확인하므로
        // 다른 나무에 재활용된 트윈을 잘못 죽일 일이 없다(KillHitPunchTween 참조).
        hitPunchTween = visualRoot.DOPunchPosition(new Vector3(hitPunchX, 0f, 0f), hitDuration, hitVibrato, hitElasticity).SetRecyclable(true);
    }

    /// <summary>
    /// 이 타격으로 나무가 죽을 때, 사망 이벤트보다 먼저 해 둬야 하는 PlayHitFeedback의 부분(진행 중인 흔들림을
    /// 멈추고 visualRoot를 제자리로)만 한다. 사망 이펙트와 피격 이펙트가 topRoot/bottomRoot(visualRoot의 자식)
    /// 위치를 읽기 때문이다. 새 흔들림·번쩍임은 TreeObj가 사망 이벤트 뒤에도 나무가 풀로 가지 않았을 때만 건다.
    /// visualRoot가 없으면 false를 돌려준다. 그때는 ResetVisualState가 첫머리에서 바로 끝나 번쩍임을 지우지 않으므로
    /// (미뤘다가 생략하면 결과가 달라진다) 호출부가 예전처럼 타격 시점에 그대로 걸어야 한다.
    /// </summary>
    public bool TrySettleVisualRootForDeath()
    {
        if (visualRoot == null)
        {
            return false;
        }

        KillHitPunchTween();
        visualRoot.localPosition = Vector3.zero;
        return true;
    }

    private void KillHitPunchTween()
    {
        KillLastTween(hitPunchTween, visualRoot, false); // 예전: visualRoot.DOKill()
    }

    private void KillAlphaTween()
    {
        // 예전 호출 this.DOKill(this)는 this가 bool로 암묵 변환되어 DOKill(complete: true)였다.
        // 완료(끝값 적용) 후 죽이는 동작을 그대로 유지한다.
        KillLastTween(alphaTween, this, true);
    }

    /// <summary>
    /// _target에 살아 있을 수 있는 유일한 트윈(_lastTween)만 죽여, DOTween.Kill(_target, _complete)와 같은 결과를 낸다.
    /// 마지막 트윈이 이미 죽었거나 다른 대상에 재활용됐으면 _target에 살아 있는 트윈이 없으므로 아무것도 하지 않는다.
    /// Tween.Kill이 무시되는 상태(DOTween이 아직 초기화되지 않은 경우 - 이때도 DOTween.Kill(target)은 동작한다)에서는
    /// 트윈이 그대로 살아 있으므로, 그때는 예전처럼 타깃 기준으로 죽인다.
    /// </summary>
    internal static void KillLastTween(Tween _lastTween, object _target, bool _complete)
    {
        if (false == _lastTween.IsActive() || false == ReferenceEquals(_lastTween.target, _target))
            return;

        _lastTween.Kill(_complete);

        if (_lastTween.IsActive())
            DOTween.Kill(_target, _complete);
    }

    // 피격 시 나무 스프라이트가 짧게 흰색으로 번쩍였다가 원래 색으로 돌아오도록 한다.
    // 치명타로 맞았다면 흰색 대신 빨간색으로 번쩍인다.
    public void PlayHitFlash(bool _bCritical = false)
    {
        // 빨간 점멸이 진행 중일 때 들어온 일반 타격은 무시하고 빨간 점멸을 끝까지 보여준다.
        // 그러지 않으면 치명타 직후 충격파·드론 타격이 곧바로 흰색으로 덮어써 빨간색이 거의 안 보인다.
        // (새로 시작하지 않고 남은 시간만 이어가므로, 연타가 계속 들어와도 빨간 상태가 늘어나지 않는다)
        if (!_bCritical && Time.time < criticalFlashEndTime)
        {
            return;
        }

        PlayFlash(hitFlashDuration, hitFlashCurve, _bCritical ? criticalHitFlashColor : hitFlashColor, _bCritical);
    }

    /// <summary>
    /// PlayHitFlash와 같되, 오브젝트 활성 여부 검사를 하지 않는다. TreeObj가 사망 이벤트 뒤로 미뤄 둔 번쩍임을
    /// 걸 때 쓴다 - 예전에는 타격 시점(활성임이 보장된 시점)에 걸었으므로 그때의 판정 결과(통과)를 그대로 따른다.
    /// </summary>
    public void PlayHitFlashIgnoringActiveState(bool _bCritical)
    {
        if (!_bCritical && Time.time < criticalFlashEndTime)
        {
            return;
        }

        StartFlash(hitFlashDuration, hitFlashCurve, _bCritical ? criticalHitFlashColor : hitFlashColor, _bCritical);
    }

    // 묘목이 다 자라 스케일이 최대가 되는 순간, 피격 플래시와 같은 셰이더로 한 번 하얗게 반짝인다.
    public void PlayGrowUpFlash()
    {
        PlayFlash(growUpFlashDuration, growUpFlashCurve, Color.white, false);
    }

    private void PlayFlash(float _duration, AnimationCurve _curve, Color _color, bool _bCritical)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        StartFlash(_duration, _curve, _color, _bCritical);
    }

    private void StartFlash(float _duration, AnimationCurve _curve, Color _color, bool _bCritical)
    {
        TreeFlashSystem.Cancel(this);

        // 색은 점멸 동안 바뀌지 않으므로 시작할 때 한 번만 넣고, 진행 중에는 세기만 갱신한다.
        ApplyFlashColorToRenderers(_color);
        criticalFlashEndTime = _bCritical ? Time.time + _duration : 0f;
        TreeFlashSystem.Begin(this, _curve, _duration);
    }

    // TreeFlashSystem이 프레임마다 호출한다(예전 FlashRoutine의 ApplyFlashAmountToRenderers 호출과 동일).
    internal void ApplyFlashAmountFromSystem(float _flash)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        ApplyFlashAmountToRenderers(_flash);
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

    private void ApplyFlashColorToRenderers(Color _color)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        SetFlashColorToRenderer(topRenderer, _color);
        SetFlashColorToRenderer(bottomRenderer, _color);
        SetFlashColorToRenderer(topShieldRenderer, _color);
        SetFlashColorToRenderer(bottomShieldRenderer, _color);
        SetFlashColorToRenderer(topHighlightRenderer, _color);
        SetFlashColorToRenderer(bottomHighlightRenderer, _color);
    }

    private void SetFlashColorToRenderer(SpriteRenderer sr, Color _color)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetColor(FlashColorID, _color);
        sr.SetPropertyBlock(_flashMPB);
    }

    // VFX 재생에 필요한 위치/회전/색상 데이터를 외부(InDungeonVFXManager)에 제공한다.
    public Vector3 GetTopRootPosition() => topRoot != null ? topRoot.position : transform.position;
    public Vector3 GetBottomRootPosition() => bottomRoot != null ? bottomRoot.position : transform.position;
    public Quaternion GetTopRootRotation() => topRoot != null ? topRoot.rotation : Quaternion.identity;
    public Quaternion GetBottomRootRotation() => bottomRoot != null ? bottomRoot.rotation : Quaternion.identity;
    public ParticleColorSet GetTopVfxColor() => currentTopVfxColor;
    public ParticleColorSet GetBottomVfxColor() => currentBottomVfxColor;

    // 이 나무의 transform에 스케일 트윈을 거는 묘목 연출을 알려준다(ResetVisualState의 DOKill 생략 판단용).
    public void SetSaplingVEComponent(SaplingVEComponent _saplingVEComponent)
    {
        saplingVEComponent = _saplingVEComponent;
    }

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
        KillHitPunchTween();
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localScale = Vector3.one;

        // 2. 묘목(Sapling) 애니메이션이 사용했던 Transform 크기 초기화
        if (saplingVEComponent == null)
            transform.DOKill();
        else
            saplingVEComponent.KillScaleTweenOn(transform);
        transform.localScale = Vector3.one;

        // 3. 투명도(Alpha)를 완전한 불투명(1.0f) 상태로 복구
        SetAlpha(1.0f);

        // 4. 피격 Flash 연출 초기화 (풀 반환 시 흰색 상태로 남는 것을 방지)
        TreeFlashSystem.Cancel(this);
        criticalFlashEndTime = 0f;

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
        // 투명도 트윈은 이 컴포넌트(this)를 타깃으로만 건다. 렌더러 개별 DOKill은 걸린 트윈이 없는데도
        // DOTween의 활성 트윈 전체를 선형 탐색하므로(ResetTree마다 8회) 제거했다.
        KillAlphaTween(); // 현재 스크립트 기반 float 트윈 정지

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
        KillAlphaTween(); // 기존 트윈 취소 (렌더러 개별 DOKill은 SetAlpha와 같은 이유로 제거)

        currentAlpha = topRenderer != null ? topRenderer.color.a : 1f;

        if (alphaGetter == null)
        {
            alphaGetter = GetCurrentAlpha;
            alphaSetter = SetCurrentAlpha;
        }

        alphaTween = DOTween.To(alphaGetter, alphaSetter, _targetAlpha, _duration).SetTarget(this);
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

        if (defaultOnWaterMaterial == null && topOnWaterSR != null && !IsGemMaterial(topOnWaterSR.sharedMaterial))
        {
            defaultOnWaterMaterial = topOnWaterSR.sharedMaterial;
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
            if (topOnWaterSR != null && defaultOnWaterMaterial != null) topOnWaterSR.sharedMaterial = defaultOnWaterMaterial;
            // 원본 머티리얼로 되돌린 뒤에 읽어야 그 머티리얼에 저장된 원래 sway 설정을 복원할 수 있다.
            SetWindSwayEnabled(true);

            // 숨겨 뒀던 실드/하이라이트를 원래 조건대로 되살린다.
            SetGemRenderState(false);
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

        // 물 위 반사도 같은 보석 효과로 그린다. 전용 머티리얼이 없으면 기존 물 재질을 그대로 둔다.
        if (topOnWaterSR != null && materialSet.onWaterMaterial != null)
        {
            topOnWaterSR.sharedMaterial = materialSet.onWaterMaterial;
        }

        SetWindSwayEnabled(false);

        // 본체만 보석으로 바꾸고 실드/하이라이트 스프라이트는 끈다.
        SetGemRenderState(true);
    }

    /// <summary>
    /// 보석 표시 상태를 바꾸고 실드/하이라이트 렌더러를 다시 판단한다.
    ///
    /// 상태가 실제로 바뀔 때만 갱신한다. ApplyGemVisual(false)는 나무가 스폰/반환될 때마다
    /// ResetTree에서 호출되는데, 매번 전 렌더러를 훑으면 던전 로드(나무 최대 2500그루)에서
    /// ApplyVisual이 이미 하는 일을 통째로 한 번 더 하는 셈이 된다.
    /// 또 이 경로는 에디터 OnValidate에서도 불리므로, 불필요한 SetActive 호출을 줄여야
    /// "SendMessage cannot be called during OnValidate" 경고가 새지 않는다.
    /// </summary>
    private void SetGemRenderState(bool _gemActive)
    {
        if (bGemActive == _gemActive) return;

        bGemActive = _gemActive;
        UpdateRendererSprites();
        UpdateHDRStates();
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

