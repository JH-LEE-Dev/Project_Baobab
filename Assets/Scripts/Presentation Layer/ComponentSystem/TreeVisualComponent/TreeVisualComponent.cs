using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

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

    [Header("Sprite Variations")]
    [SerializeField] private Sprite[] topSprites;
    [SerializeField] private Sprite[] bottomSprites;

    [Header("Hit Feedback")]
    [SerializeField] private float hitPunchX = 0.1f;
    [SerializeField] private float hitDuration = 0.2f;
    [SerializeField] private int hitVibrato = 15;
    [SerializeField] private float hitElasticity = 1f;



    [Header("Outline")]
    [SerializeField] private GameObject outlineVisualObj;

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

    // Shield HDR
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private MaterialPropertyBlock _mpb;
    private MaterialPropertyBlock Mpb => _mpb ??= new MaterialPropertyBlock();

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
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.DOKill();
        visualRoot.localPosition = Vector3.zero;
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

    private void ApplyHDRToRenderer(SpriteRenderer _renderer, bool _active, float _intensity)
    {
        if (_renderer == null) return;
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

    private void OnValidate()
    {
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

