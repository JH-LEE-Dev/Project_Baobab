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

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer topRenderer;
    [SerializeField] private SpriteRenderer bottomRenderer;
    [SerializeField] private SpriteRenderer topShadowRenderer;
    [SerializeField] private SpriteRenderer bottomShadowRenderer;
    [SerializeField] private SpriteRenderer topOnWaterSR;
    [SerializeField] private SpriteRenderer bottomOnWaterSR;
    [SerializeField] private SpriteRenderer topOutlineSR;
    [SerializeField] private SpriteRenderer bottomOutlineSR;
    [SerializeField] private SpriteRenderer topShieldOutlineSR;
    [SerializeField] private SpriteRenderer bottomShieldOutlineSR;
    [SerializeField] private SpriteRenderer topStencilOutlineSR;
    [SerializeField] private SpriteRenderer bottomStencilOutlineSR;
    [SerializeField] private SpriteRenderer topShieldStencilOutlineSR;
    [SerializeField] private SpriteRenderer bottomShieldStencilOutlineSR;
    [SerializeField] private SpriteRenderer topHighlightSR;
    [SerializeField] private SpriteRenderer topHighlightOnWaterSR;
    [SerializeField] private SpriteRenderer topShieldSR;
    [SerializeField] private SpriteRenderer bottomShieldSR;
    [SerializeField] private SpriteRenderer topShieldOnWaterSR;
    [SerializeField] private SpriteRenderer bottomShieldOnWaterSR;

    [Header("Sprite Variations")]
    [SerializeField] private Sprite[] topSprites;
    [SerializeField] private Sprite[] bottomSprites;

    [Header("Hit Feedback")]
    [SerializeField] private float hitPunchX = 0.1f;
    [SerializeField] private float hitDuration = 0.2f;
    [SerializeField] private int hitVibrato = 15;
    [SerializeField] private float hitElasticity = 1f;

    [Header("Wind Sway")]
    [SerializeField] private bool enableWindSway = true;
    [SerializeField] private float swayPositionAmplitude = 0.03f;
    [SerializeField] private float swayRotationAmplitude = 1.25f;
    [SerializeField] private float swayMainSpeed = 0.55f;
    [SerializeField] private float swayDetailSpeed = 1.45f;
    [SerializeField] private float swayDetailWeight = 0.35f;

    [Header("Outline")]
    [SerializeField] private GameObject outlineVisualObj;
    [SerializeField] private Transform outlineStencilTopTransform;
    [SerializeField] private Transform outlineTopTransform;

    [Header("Other Settings")]
    public GameObject baseVisualObj;


    [Header("Editor Custom Colors & Type")]
    public bool bUseCustomColor = false;
    public TreeType customTreeType = TreeType.OakTree;
    [SerializeField] private Color customTopColor = Color.white;
    [SerializeField] private Color customBottomColor = Color.white;
    public TreeVisualDataBase treeVisualDataBase;

    #endregion

    #region Private Fields

    private Transform cachedTransform;
    private Transform topTransform;
    private Transform topShadowTransform;

    private Vector3 topRendererBaseLocalPosition;
    private Quaternion topRendererBaseLocalRotation;
    private Vector3 topShadowBaseLocalPosition;
    private Quaternion topShadowBaseLocalRotation;

    private Vector3 outlineTopBaseLocalPosition;
    private Quaternion outlineTopBaseLocalRotation;

    private Vector3 outlineStencilTopBaseLocalPosition;
    private Quaternion outlineStencilTopBaseLocalRotation;

    private float swayPhase;
    private bool isOnWaterActive = false;
    private bool bOnWaterOrderSet = false;

    private Color firstIndexBottomColor;
    private CustomSortable customSortable;

    private Transform topHighlightOnWaterTransform;
    private Vector3 topHighlightOnWaterBaseLocalPosition;
    private Quaternion topHighlightOnWaterBaseLocalRotation;

    private float topHighlightOriginalAlpha = 1f;
    private float topHighlightOnWaterOriginalAlpha = 1f;
    private float topShieldOriginalAlpha = 1f;
    private float bottomShieldOriginalAlpha = 1f;
    private float topShieldOnWaterOriginalAlpha = 1f;
    private float bottomShieldOnWaterOriginalAlpha = 1f;


    private bool isOutlineActive = false;
    private bool bDisableOutline = false;
    private float currentAlpha;

    #endregion

    #region Unity Events

    // 플레이 시작 시 바람 흔들림의 기준이 되는 상단 스프라이트 기본 포즈를 저장한다.
    private void Awake()
    {
        cachedTransform = transform;
        if (topRenderer != null) topTransform = topRenderer.transform;

        if (topHighlightSR != null) topHighlightOriginalAlpha = topHighlightSR.color.a;
        if (topHighlightOnWaterSR != null) topHighlightOnWaterOriginalAlpha = topHighlightOnWaterSR.color.a;
        if (topShieldSR != null) topShieldOriginalAlpha = topShieldSR.color.a;
        if (bottomShieldSR != null) bottomShieldOriginalAlpha = bottomShieldSR.color.a;
        if (topShieldOnWaterSR != null) topShieldOnWaterOriginalAlpha = topShieldOnWaterSR.color.a;
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterOriginalAlpha = bottomShieldOnWaterSR.color.a;

        CacheSwayBasePose();
    }

    // 매 프레임 상단 수관에 아주 약한 바람 흔들림을 적용한다.
    private void Update()
    {
        ApplyWindSway();
    }

    private void LateUpdate()
    {
        // 물 위 효과가 활성화된 경우에만 실행하여 불필요한 계산 방지
        if (!isOnWaterActive || bOnWaterOrderSet == true) return;

        int order = (int)(cachedTransform.position.y * 100);
        if (topOnWaterSR != null) topOnWaterSR.sortingOrder = order;
        if (bottomOnWaterSR != null) bottomOnWaterSR.sortingOrder = order;
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.sortingOrder = order;
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.sortingOrder = order;
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.sortingOrder = order;
        bOnWaterOrderSet = true;
    }

    // 에디터 미리보기 모드에서는 값이 바뀔 때마다 비주얼 조합을 즉시 다시 적용한다.
    private void OnValidate()
    {
        if (Application.isPlaying || !previewInEditor)
        {
            return;
        }

#if UNITY_EDITOR
        // 에디터 상에서 데이터베이스 참조가 누락되어 있다면 자동으로 검색하여 할당
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

        RefreshVisualPreview();
    }

    #endregion

    #region Initialize

    public void Initialize(Transform _topShadowTransform, CustomSortable _customSortable)
    {
        if (cachedTransform == null) cachedTransform = transform;
        if (topRenderer != null && topTransform == null) topTransform = topRenderer.transform;
        if (topShadowRenderer != null && topShadowTransform == null) topShadowTransform = _topShadowTransform;

        if (topHighlightSR != null) topHighlightOriginalAlpha = topHighlightSR.color.a;
        if (topHighlightOnWaterSR != null) topHighlightOnWaterOriginalAlpha = topHighlightOnWaterSR.color.a;
        if (topShieldSR != null) topShieldOriginalAlpha = topShieldSR.color.a;
        if (bottomShieldSR != null) bottomShieldOriginalAlpha = bottomShieldSR.color.a;
        if (topShieldOnWaterSR != null) topShieldOnWaterOriginalAlpha = topShieldOnWaterSR.color.a;
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterOriginalAlpha = bottomShieldOnWaterSR.color.a;

        CacheSwayBasePose();
        ResetVisualState();

        customSortable = _customSortable;

        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.SetSortingGroup(baseVisualObj.GetComponent<SortingGroup>());
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
        ResetTopSway();
    }

    // 상단/하단 스프라이트를 랜덤으로 고르고 색상과 그림자 비주얼까지 함께 갱신한다. (에디터 미리보기용)
    private void ApplyRandomVisual()
    {
        if (bUseCustomColor && treeVisualDataBase != null)
        {
            TreeVisualData customVisualData = treeVisualDataBase.Get(customTreeType);
            if (customVisualData.treeType != TreeType.None)
            {
                // 에디터 설정 시에는 바리에이션 요동을 막기 위해 첫 번째 대표 스프라이트로 고정
                SetFirstSprite(bottomRenderer, customVisualData.bottomSprites);
                int index = SetFirstSprite(topRenderer, customVisualData.topSprites);

                if (topHighlightSR != null)
                {
                    if (index >= 0 && customVisualData.highlightSprites != null && index < customVisualData.highlightSprites.Count)
                    {
                        topHighlightSR.sprite = customVisualData.highlightSprites[index];
                    }
                    else
                    {
                        topHighlightSR.sprite = null;
                    }
                }

                if (topShieldSR != null)
                {
                    if (index >= 0 && customVisualData.shieldTopSprites != null && index < customVisualData.shieldTopSprites.Count)
                    {
                        topShieldSR.sprite = customVisualData.shieldTopSprites[index];
                    }
                    else
                    {
                        topShieldSR.sprite = null;
                    }
                }

                if (bottomShieldSR != null)
                {
                    if (customVisualData.shieldBottomSprites != null && customVisualData.shieldBottomSprites.Count > 0)
                    {
                        bottomShieldSR.sprite = customVisualData.shieldBottomSprites[0];
                    }
                    else
                    {
                        bottomShieldSR.sprite = null;
                    }
                }
            }
        }
        else
        {
            SetRandomSprite(bottomRenderer, bottomSprites);
            SetRandomSprite(topRenderer, topSprites);

            if (topHighlightSR != null)
            {
                topHighlightSR.sprite = null;
            }

            if (topShieldSR != null)
            {
                topShieldSR.sprite = null;
            }

            if (bottomShieldSR != null)
            {
                bottomShieldSR.sprite = null;
            }
        }

        if (bUseCustomColor)
        {
            if (topRenderer != null) topRenderer.color = customTopColor;
            if (bottomRenderer != null) bottomRenderer.color = customBottomColor;
            if (topOnWaterSR != null) topOnWaterSR.color = customTopColor;
            if (bottomOnWaterSR != null) bottomOnWaterSR.color = customBottomColor;
            // if (topHighlightSR != null) topHighlightSR.color = customTopColor;
            // if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.color = customTopColor;
        }
        else
        {
            if (topRenderer != null) topRenderer.color = Color.white;
            if (bottomRenderer != null) bottomRenderer.color = Color.white;
            if (topOnWaterSR != null) topOnWaterSR.color = Color.white;
            if (bottomOnWaterSR != null) bottomOnWaterSR.color = Color.white;
            // if (topHighlightSR != null) topHighlightSR.color = Color.white;
            // if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.color = Color.white;
        }

        ApplyDefaultScale();
    }

    private void RefreshVisualPreview()
    {
        ApplyRandomVisual();
        SyncShadowSprite();
    }
    #endregion

    #region Apply Data
    // 트리 데이터가 적용될 때 데이터에 정의된 스프라이트와 색상을 적용한다.
    public void ApplyVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (bUseCustomColor && treeVisualDataBase != null)
        {
            TreeVisualData customVisualData = treeVisualDataBase.Get(customTreeType);
            if (customVisualData.treeType != TreeType.None)
            {
                visualData = customVisualData;
            }
        }

        if (topRenderer != null)
        {
            int index = SetRandomSprite(topRenderer, visualData.topSprites);
            if (topHighlightSR != null)
            {
                if (index >= 0 && visualData.highlightSprites != null && index < visualData.highlightSprites.Count)
                {
                    topHighlightSR.sprite = visualData.highlightSprites[index];
                }
                else
                {
                    topHighlightSR.sprite = null;
                }
            }

            if (topShieldSR != null)
            {
                if (index >= 0 && visualData.shieldTopSprites != null && index < visualData.shieldTopSprites.Count)
                {
                    topShieldSR.sprite = visualData.shieldTopSprites[index];
                }
                else
                {
                    topShieldSR.sprite = null;
                }
            }
        }

        if (bottomRenderer != null)
        {
            int index = SetRandomSprite(bottomRenderer, visualData.bottomSprites);
            if (bottomShieldSR != null)
            {
                if (index >= 0 && visualData.shieldBottomSprites != null && index < visualData.shieldBottomSprites.Count)
                {
                    bottomShieldSR.sprite = visualData.shieldBottomSprites[index];
                }
                else
                {
                    bottomShieldSR.sprite = null;
                }
            }
        }

        // 쉴드 및 하이라이트 관련 오브젝트 활성화
        if (topHighlightSR != null) topHighlightSR.gameObject.SetActive(true);
        if (topShieldSR != null) topShieldSR.gameObject.SetActive(true);
        if (bottomShieldSR != null) bottomShieldSR.gameObject.SetActive(true);

        if (isOnWaterActive)
        {
            if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.gameObject.SetActive(true);
            if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(true);
            if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.gameObject.SetActive(true);
        }

        if (isOutlineActive)
        {
            if (topShieldOutlineSR != null) topShieldOutlineSR.gameObject.SetActive(true);
            if (bottomShieldOutlineSR != null) bottomShieldOutlineSR.gameObject.SetActive(true);
            if (topShieldStencilOutlineSR != null) topShieldStencilOutlineSR.gameObject.SetActive(true);
            if (bottomShieldStencilOutlineSR != null) bottomShieldStencilOutlineSR.gameObject.SetActive(true);
        }

        ApplyColorSet(visualData);
        ApplyDefaultScale();
        SyncShadowSprite();
        ResetTopSway();
        CacheSwayBasePose();
    }

    // 묘목(Sapling) 비주얼을 적용한다.
    public void ApplySaplingVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (bUseCustomColor && treeVisualDataBase != null)
        {
            TreeVisualData customVisualData = treeVisualDataBase.Get(customTreeType);
            if (customVisualData.treeType != TreeType.None)
            {
                visualData = customVisualData;
            }
        }

        if (topRenderer != null)
        {
            int index = SetRandomSprite(topRenderer, visualData.saplingTopSprites);
            if (topHighlightSR != null)
            {
                if (index >= 0 && visualData.highlightSprites != null && index < visualData.highlightSprites.Count)
                {
                    topHighlightSR.sprite = visualData.highlightSprites[index];
                }
                else
                {
                    topHighlightSR.sprite = null;
                }
            }

            if (topShieldSR != null)
            {
                if (index >= 0 && visualData.shieldTopSprites != null && index < visualData.shieldTopSprites.Count)
                {
                    topShieldSR.sprite = visualData.shieldTopSprites[index];
                }
                else
                {
                    topShieldSR.sprite = null;
                }
            }
        }

        if (bottomRenderer != null)
        {
            int index = SetRandomSprite(bottomRenderer, visualData.saplingBottomSprites);
            if (bottomShieldSR != null)
            {
                if (index >= 0 && visualData.shieldBottomSprites != null && index < visualData.shieldBottomSprites.Count)
                {
                    bottomShieldSR.sprite = visualData.shieldBottomSprites[index];
                }
                else
                {
                    bottomShieldSR.sprite = null;
                }
            }
        }

        // 쉴드 및 하이라이트 관련 오브젝트 비활성화
        if (topHighlightSR != null) topHighlightSR.gameObject.SetActive(false);
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.gameObject.SetActive(false);
        if (topShieldSR != null) topShieldSR.gameObject.SetActive(false);
        if (bottomShieldSR != null) bottomShieldSR.gameObject.SetActive(false);
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(false);
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.gameObject.SetActive(false);
        if (topShieldOutlineSR != null) topShieldOutlineSR.gameObject.SetActive(false);
        if (bottomShieldOutlineSR != null) bottomShieldOutlineSR.gameObject.SetActive(false);
        if (topShieldStencilOutlineSR != null) topShieldStencilOutlineSR.gameObject.SetActive(false);
        if (bottomShieldStencilOutlineSR != null) bottomShieldStencilOutlineSR.gameObject.SetActive(false);

        ApplyColorSet(visualData);
        ApplyDefaultScale();
        SyncShadowSprite();
        ResetTopSway();
        CacheSwayBasePose();
    }

    public void DeActivateOnWaterObject()
    {
        isOnWaterActive = false;
        bOnWaterOrderSet = false;
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(false);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(false);
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.gameObject.SetActive(false);
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(false);
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.gameObject.SetActive(false);
    }

    public void ActivateOnWaterObject()
    {
        isOnWaterActive = true;
        bOnWaterOrderSet = false;
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(true);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(true);
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.gameObject.SetActive(true);
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(true);
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.gameObject.SetActive(true);
    }


    private void ApplyColorSet(TreeVisualData _visualData)
    {
        Color topColor;
        Color bottomColor;

        if (bUseCustomColor)
        {
            topColor = customTopColor;
            bottomColor = customBottomColor;
            firstIndexBottomColor = customBottomColor;
        }
        else
        {
            if (_visualData.treeColorSets == null || _visualData.treeColorSets.Count == 0)
            {
                return;
            }

            TreeColorSet colorSet = _visualData.treeColorSets[Random.Range(0, _visualData.treeColorSets.Count)];
            firstIndexBottomColor = _visualData.treeColorSets[0].bottomColor;

            topColor = colorSet.topColor;
            bottomColor = colorSet.bottomColor;
        }

        if (topRenderer != null)
        {
            topRenderer.color = topColor;
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.color = bottomColor;
        }

        if (topOnWaterSR != null)
        {
            topOnWaterSR.color = topColor;
        }

        if (bottomOnWaterSR != null)
        {
            bottomOnWaterSR.color = bottomColor;
        }

        // if (topHighlightSR != null)
        // {
        //     topHighlightSR.color = topColor;
        // }
        // 
        // if (topHighlightOnWaterSR != null)
        // {
        //     topHighlightOnWaterSR.color = topColor;
        // }
    }
    // 나무의 전체적인 크기를 기본값(1.0)으로 설정한다.
    private void ApplyDefaultScale()
    {
        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one;
        }
    }

    // 상단/하단 스프라이트에 밝기 편차를 줘서 개체마다 미묘한 색 차이를 만든다.
    public Color GetBottomColor()
    {
        return firstIndexBottomColor;
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

            if (topHighlightOnWaterSR != null && topHighlightSR != null)
            {
                topHighlightOnWaterSR.sprite = topHighlightSR.sprite;
            }

            if (topShieldOnWaterSR != null && topShieldSR != null)
            {
                topShieldOnWaterSR.sprite = topShieldSR.sprite;
            }

            if (topShieldOutlineSR != null && topShieldSR != null)
            {
                topShieldOutlineSR.sprite = topShieldSR.sprite;
                Color outlineColor = topShieldOutlineSR.color;
                outlineColor.a = topShieldSR.color.a;
                topShieldOutlineSR.color = outlineColor;
            }

            if (topShieldStencilOutlineSR != null && topShieldSR != null)
            {
                topShieldStencilOutlineSR.sprite = topShieldSR.sprite;
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

            if (bottomShieldOnWaterSR != null && bottomShieldSR != null)
            {
                bottomShieldOnWaterSR.sprite = bottomShieldSR.sprite;
            }

            if (bottomShieldOutlineSR != null && bottomShieldSR != null)
            {
                bottomShieldOutlineSR.sprite = bottomShieldSR.sprite;
                Color outlineColor = bottomShieldOutlineSR.color;
                outlineColor.a = bottomShieldSR.color.a;
                bottomShieldOutlineSR.color = outlineColor;
            }

            if (bottomShieldStencilOutlineSR != null && bottomShieldSR != null)
            {
                bottomShieldStencilOutlineSR.sprite = bottomShieldSR.sprite;
            }
        }
    }

    // 전달받은 렌더러에 스프라이트 리스트 중 하나를 무작위로 적용하고 선택된 인덱스를 반환한다.
    private static int SetRandomSprite(SpriteRenderer _renderer, System.Collections.Generic.IList<Sprite> _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Count == 0)
        {
            return -1;
        }

        int index = Random.Range(0, _sprites.Count);
        _renderer.sprite = _sprites[index];
        return index;
    }

    // 전달받은 렌더러에 스프라이트 리스트 중 첫 번째(기본) 스프라이트를 고정 적용하고 0을 반환한다.
    private static int SetFirstSprite(SpriteRenderer _renderer, System.Collections.Generic.IList<Sprite> _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Count == 0)
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

    // 누적된 연출 값을 지우고 비주얼을 기본 위치와 포즈로 되돌린다.
    public void ResetVisualState()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.DOKill();
        visualRoot.localPosition = Vector3.zero;
        ResetTopSway();
    }

    // 상단 스프라이트의 기본 위치와 회전, 그리고 개체별 랜덤 위상을 저장한다.
    public void CacheSwayBasePose()
    {
        if (topRenderer == null)
        {
            return;
        }

        topTransform = topRenderer.transform;

        topRendererBaseLocalPosition = topTransform.localPosition;
        topRendererBaseLocalRotation = topTransform.localRotation;

        if (topShadowRenderer != null)
        {
            topShadowTransform = topShadowRenderer.transform;
            topShadowBaseLocalPosition = topShadowTransform.localPosition;
            topShadowBaseLocalRotation = topShadowTransform.localRotation;
        }

        if (outlineTopTransform != null)
        {
            outlineTopBaseLocalPosition = outlineTopTransform.localPosition;
            outlineTopBaseLocalRotation = outlineTopTransform.localRotation;
        }

        if (outlineStencilTopTransform != null)
        {
            outlineStencilTopBaseLocalPosition = outlineStencilTopTransform.localPosition;
            outlineStencilTopBaseLocalRotation = outlineStencilTopTransform.localRotation;
        }

        swayPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // 느린 큰 파형과 빠른 작은 파형을 섞어 나무 윗부분만 자연스럽게 흔들리게 만든다.
    private void ApplyWindSway()
    {
        if (!Application.isPlaying || !enableWindSway || topTransform == null)
        {
            return;
        }

        float time = Time.time;
        float mainWave = Mathf.Sin((time * swayMainSpeed) + swayPhase);
        float detailWave = Mathf.Sin((time * swayDetailSpeed) + (swayPhase * 1.73f)) * swayDetailWeight;
        float sway = mainWave + detailWave;

        Vector3 swayOffset = new Vector3(sway * swayPositionAmplitude, 0f, 0f);
        Quaternion swayRotation = Quaternion.Euler(0f, 0f, -sway * swayRotationAmplitude);

        topTransform.localPosition = topRendererBaseLocalPosition + swayOffset;
        topTransform.localRotation = topRendererBaseLocalRotation * swayRotation;

        if (topShadowTransform != null)
        {
            topShadowTransform.localPosition = topShadowBaseLocalPosition + swayOffset;
            topShadowTransform.localRotation = topShadowBaseLocalRotation * swayRotation;
        }

        if (isOutlineActive && outlineTopTransform != null)
        {
            outlineTopTransform.localPosition = outlineTopBaseLocalPosition + swayOffset;
            outlineTopTransform.localRotation = outlineTopBaseLocalRotation * swayRotation;
        }

        if (isOutlineActive && outlineStencilTopTransform != null)
        {
            outlineStencilTopTransform.localPosition = outlineStencilTopBaseLocalPosition + swayOffset;
            outlineStencilTopTransform.localRotation = outlineStencilTopBaseLocalRotation * swayRotation;
        }
    }

    // 바람 흔들림을 제거하고 상단 스프라이트를 저장된 기본 포즈로 되돌린다.
    private void ResetTopSway()
    {
        if (topTransform != null)
        {
            topTransform.localPosition = topRendererBaseLocalPosition;
            topTransform.localRotation = topRendererBaseLocalRotation;
        }

        if (topShadowTransform != null)
        {
            topShadowTransform.localPosition = topShadowBaseLocalPosition;
            topShadowTransform.localRotation = topShadowBaseLocalRotation;
        }

        if (outlineTopTransform != null)
        {
            outlineTopTransform.localPosition = outlineTopBaseLocalPosition;
            outlineTopTransform.localRotation = outlineTopBaseLocalRotation;
        }

        if (outlineStencilTopTransform != null)
        {
            outlineStencilTopTransform.localPosition = outlineStencilTopBaseLocalPosition;
            outlineStencilTopTransform.localRotation = outlineStencilTopBaseLocalRotation;
        }
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

        if (topOnWaterSR != null)
        {
            Color towColor = topOnWaterSR.color;
            towColor.a = _alpha;
            topOnWaterSR.color = towColor;
        }

        if (bottomOnWaterSR != null)
        {
            Color bowColor = bottomOnWaterSR.color;
            bowColor.a = _alpha;
            bottomOnWaterSR.color = bowColor;
        }

        if (topOutlineSR != null)
        {
            Color oTopColor = topOutlineSR.color;
            oTopColor.a = _alpha;
            topOutlineSR.color = oTopColor;
        }

        if (bottomOutlineSR != null)
        {
            Color oBotColor = bottomOutlineSR.color;
            oBotColor.a = _alpha;
            bottomOutlineSR.color = oBotColor;
        }

        if (topShieldOutlineSR != null)
        {
            Color oTopColor = topShieldOutlineSR.color;
            oTopColor.a = _alpha;
            topShieldOutlineSR.color = oTopColor;
        }

        if (bottomShieldOutlineSR != null)
        {
            Color oBotColor = bottomShieldOutlineSR.color;
            oBotColor.a = _alpha;
            bottomShieldOutlineSR.color = oBotColor;
        }

        if (topHighlightSR != null)
        {
            Color thColor = topHighlightSR.color;
            thColor.a = topHighlightOriginalAlpha * _alpha;
            topHighlightSR.color = thColor;
        }

        if (topHighlightOnWaterSR != null)
        {
            Color thowColor = topHighlightOnWaterSR.color;
            thowColor.a = topHighlightOnWaterOriginalAlpha * _alpha;
            topHighlightOnWaterSR.color = thowColor;
        }

        if (topShieldSR != null)
        {
            Color tsColor = topShieldSR.color;
            tsColor.a = topShieldOriginalAlpha * _alpha;
            topShieldSR.color = tsColor;
        }

        if (bottomShieldSR != null)
        {
            Color bsColor = bottomShieldSR.color;
            bsColor.a = bottomShieldOriginalAlpha * _alpha;
            bottomShieldSR.color = bsColor;
        }

        if (topShieldOnWaterSR != null)
        {
            Color tsowColor = topShieldOnWaterSR.color;
            tsowColor.a = topShieldOnWaterOriginalAlpha * _alpha;
            topShieldOnWaterSR.color = tsowColor;
        }

        if (bottomShieldOnWaterSR != null)
        {
            Color bsowColor = bottomShieldOnWaterSR.color;
            bsowColor.a = bottomShieldOnWaterOriginalAlpha * _alpha;
            bottomShieldOnWaterSR.color = bsowColor;
        }
    }

    public void SetAlpha(float _alpha)
    {
        this.DOKill(this); // 현재 스크립트 기반 float 트윈 정지

        topRenderer.DOKill();
        bottomRenderer.DOKill();
        if (topShadowRenderer != null) topShadowRenderer.DOKill();
        if (bottomShadowRenderer != null) bottomShadowRenderer.DOKill();
        if (topOnWaterSR != null) topOnWaterSR.DOKill();
        if (bottomOnWaterSR != null) bottomOnWaterSR.DOKill();
        if (topOutlineSR != null) topOutlineSR.DOKill();
        if (bottomOutlineSR != null) bottomOutlineSR.DOKill();
        if (topShieldOutlineSR != null) topShieldOutlineSR.DOKill();
        if (bottomShieldOutlineSR != null) bottomShieldOutlineSR.DOKill();
        if (topHighlightSR != null) topHighlightSR.DOKill();
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.DOKill();
        if (topShieldSR != null) topShieldSR.DOKill();
        if (bottomShieldSR != null) bottomShieldSR.DOKill();
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.DOKill();
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.DOKill();

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
        if (topOnWaterSR != null) topOnWaterSR.DOKill();
        if (bottomOnWaterSR != null) bottomOnWaterSR.DOKill();
        if (topOutlineSR != null) topOutlineSR.DOKill();
        if (bottomOutlineSR != null) bottomOutlineSR.DOKill();
        if (topShieldOutlineSR != null) topShieldOutlineSR.DOKill();
        if (bottomShieldOutlineSR != null) bottomShieldOutlineSR.DOKill();
        if (topHighlightSR != null) topHighlightSR.DOKill();
        if (topHighlightOnWaterSR != null) topHighlightOnWaterSR.DOKill();
        if (topShieldSR != null) topShieldSR.DOKill();
        if (bottomShieldSR != null) bottomShieldSR.DOKill();
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.DOKill();
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.DOKill();

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

    public void SetCustomSortingLayer()
    {
        topShieldSR.sortingOrder = topRenderer.sortingOrder + 1;
        bottomShieldSR.sortingOrder = bottomRenderer.sortingOrder + 1;
        topShieldOnWaterSR.sortingOrder = topOnWaterSR.sortingOrder + 1;
        bottomShieldOnWaterSR.sortingOrder = bottomOnWaterSR.sortingOrder + 1;
    }

    public void ShieldBroken()
    {
        if (topShieldSR != null) topShieldSR.gameObject.SetActive(false);
        if (bottomShieldSR != null) bottomShieldSR.gameObject.SetActive(false);
        if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(false);
        if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.gameObject.SetActive(false);
        if (topShieldOutlineSR != null) topShieldOutlineSR.gameObject.SetActive(false);
        if (bottomShieldOutlineSR != null) bottomShieldOutlineSR.gameObject.SetActive(false);
        if (topShieldStencilOutlineSR != null) topShieldStencilOutlineSR.gameObject.SetActive(false);
        if (bottomShieldStencilOutlineSR != null) bottomShieldStencilOutlineSR.gameObject.SetActive(false);
    }

    public void ShieldRegened()
    {
        if (topShieldSR != null) topShieldSR.gameObject.SetActive(true);
        if (bottomShieldSR != null) bottomShieldSR.gameObject.SetActive(true);

        if (isOnWaterActive)
        {
            if (topShieldOnWaterSR != null) topShieldOnWaterSR.gameObject.SetActive(true);
            if (bottomShieldOnWaterSR != null) bottomShieldOnWaterSR.gameObject.SetActive(true);
        }

        if (isOutlineActive)
        {
            if (topShieldOutlineSR != null) topShieldOutlineSR.gameObject.SetActive(true);
            if (bottomShieldOutlineSR != null) bottomShieldOutlineSR.gameObject.SetActive(true);
            if (topShieldStencilOutlineSR != null) topShieldStencilOutlineSR.gameObject.SetActive(true);
            if (bottomShieldStencilOutlineSR != null) bottomShieldStencilOutlineSR.gameObject.SetActive(true);
        }
    }

    #endregion
}

