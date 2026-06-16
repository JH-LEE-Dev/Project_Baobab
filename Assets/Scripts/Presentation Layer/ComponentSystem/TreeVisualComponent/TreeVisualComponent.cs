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


    [Header("Editor Custom Settings")]
    public TreeType customTreeType = TreeType.OakTree;
    public TreeVisualDataBase treeVisualDataBase;

    #endregion

    #region Private Fields

    // 외부 의존성
    private CustomSortable customSortable;

    // 내부 의존성
    private Transform cachedTransform;
    private Transform topTransform;
    private Transform topShadowTransform;

    // 캐시된 기본 포즈
    private Vector3 topRendererBaseLocalPosition;
    private Quaternion topRendererBaseLocalRotation;
    private Vector3 topShadowBaseLocalPosition;
    private Quaternion topShadowBaseLocalRotation;

    private Vector3 outlineTopBaseLocalPosition;
    private Quaternion outlineTopBaseLocalRotation;

    private Vector3 outlineStencilTopBaseLocalPosition;
    private Quaternion outlineStencilTopBaseLocalRotation;

    // 스프라이트 리소스 캐시 및 상태
    private Sprite defaultTopSprite;
    private Sprite defaultBottomSprite;
    private Sprite shieldTopSprite;
    private Sprite shieldBottomSprite;

    // 상태 변수
    private float swayPhase;
    private bool isOutlineActive = false;
    private bool bDisableOutline = false;
    private float currentAlpha;
    private bool isShieldActive = false;

    #endregion

    #region Unity Events

    #endregion

    #region Initialize

    public void Initialize(Transform _topShadowTransform, CustomSortable _customSortable)
    {
        if (cachedTransform == null) cachedTransform = transform;
        if (topRenderer != null && topTransform == null) topTransform = topRenderer.transform;
        if (topShadowRenderer != null && topShadowTransform == null) topShadowTransform = _topShadowTransform;

        CacheSwayBasePose();
        ResetVisualState();

        customSortable = _customSortable;

        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.SetSortingGroup(baseVisualObj.GetComponent<SortingGroup>());
        }
    }

    public void UpdateOnWaterSortingOrder()
    {
        if (cachedTransform == null) cachedTransform = transform;
        int order = (int)(cachedTransform.position.y * 100);
        if (topOnWaterSR != null) topOnWaterSR.sortingOrder = order;
        if (bottomOnWaterSR != null) bottomOnWaterSR.sortingOrder = order;
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

    // 상단/하단 스프라이트를 랜덤으로 고르고 그림자 비주얼까지 함께 갱신한다. (에디터 미리보기용)
    private void ApplyRandomVisual()
    {
        if (treeVisualDataBase != null)
        {
            TreeVisualData customVisualData = treeVisualDataBase.Get(customTreeType);
            if (customVisualData.treeType != TreeType.None)
            {
                // 에디터 설정 시에는 바리에이션 요동을 막기 위해 첫 번째 대표 스프라이트로 고정
                SetFirstSprite(bottomRenderer, customVisualData.bottomSprites);
                defaultBottomSprite = bottomRenderer.sprite;

                int index = SetFirstSprite(topRenderer, customVisualData.topSprites);
                defaultTopSprite = topRenderer.sprite;

                if (index >= 0 && customVisualData.shieldTopSprites != null && index < customVisualData.shieldTopSprites.Count)
                {
                    shieldTopSprite = customVisualData.shieldTopSprites[index];
                }
                else
                {
                    shieldTopSprite = null;
                }

                if (customVisualData.shieldBottomSprites != null && customVisualData.shieldBottomSprites.Count > 0)
                {
                    shieldBottomSprite = customVisualData.shieldBottomSprites[0];
                }
                else
                {
                    shieldBottomSprite = null;
                }

                UpdateRendererSprites();
                ApplyDefaultScale();
                return;
            }
        }

        // 데이터베이스가 없는 경우 기본 인스펙터 배열에서 랜덤 선택
        SetRandomSprite(bottomRenderer, bottomSprites);
        defaultBottomSprite = bottomRenderer.sprite;
        shieldBottomSprite = null;

        SetRandomSprite(topRenderer, topSprites);
        defaultTopSprite = topRenderer.sprite;
        shieldTopSprite = null;

        UpdateRendererSprites();
        ApplyDefaultScale();
    }

    private void RefreshVisualPreview()
    {
        ApplyRandomVisual();
        SyncShadowSprite();
    }
    #endregion

    #region Apply Data

    // 트리 데이터가 적용될 때 데이터에 정의된 스프라이트를 적용한다.
    public void ApplyVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (topRenderer != null)
        {
            int index = SetRandomSprite(topRenderer, visualData.topSprites);
            defaultTopSprite = topRenderer.sprite;
            if (index >= 0 && visualData.shieldTopSprites != null && index < visualData.shieldTopSprites.Count)
            {
                shieldTopSprite = visualData.shieldTopSprites[index];
            }
            else
            {
                shieldTopSprite = null;
            }
        }

        if (bottomRenderer != null)
        {
            int index = SetRandomSprite(bottomRenderer, visualData.bottomSprites);
            defaultBottomSprite = bottomRenderer.sprite;
            if (index >= 0 && visualData.shieldBottomSprites != null && index < visualData.shieldBottomSprites.Count)
            {
                shieldBottomSprite = visualData.shieldBottomSprites[index];
            }
            else
            {
                shieldBottomSprite = null;
            }
        }

        // 초기 쉴드 활성화 여부 판단 (쉴드 스프라이트가 존재하면 활성화)
        isShieldActive = (shieldTopSprite != null || shieldBottomSprite != null);

        UpdateRendererSprites();
        ApplyDefaultScale();
        ResetTopSway();
        CacheSwayBasePose();
    }

    // 묘목(Sapling) 비주얼을 적용한다.
    public void ApplySaplingVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (topRenderer != null)
        {
            SetRandomSprite(topRenderer, visualData.saplingTopSprites);
            defaultTopSprite = topRenderer.sprite;
            shieldTopSprite = null;
        }

        if (bottomRenderer != null)
        {
            SetRandomSprite(bottomRenderer, visualData.saplingBottomSprites);
            defaultBottomSprite = bottomRenderer.sprite;
            shieldBottomSprite = null;
        }

        isShieldActive = false;

        UpdateRendererSprites();
        ApplyDefaultScale();
        ResetTopSway();
        CacheSwayBasePose();
    }

    public void DeActivateOnWaterObject()
    {
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(false);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(false);
    }

    public void ActivateOnWaterObject()
    {
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(true);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(true);

        UpdateOnWaterSortingOrder();
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
            topRenderer.sprite = isShieldActive && shieldTopSprite != null ? shieldTopSprite : defaultTopSprite;
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.sprite = isShieldActive && shieldBottomSprite != null ? shieldBottomSprite : defaultBottomSprite;
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
    }

    public void ShieldRegened()
    {
        isShieldActive = true;
        UpdateRendererSprites();
    }

    #endregion

    #region Unity Events

    private void Awake()
    {
        cachedTransform = transform;
        if (topRenderer != null) topTransform = topRenderer.transform;

        if (topRenderer != null) topRenderer.color = Color.white;
        if (bottomRenderer != null) bottomRenderer.color = Color.white;
        if (topShadowRenderer != null) topShadowRenderer.color = Color.white;
        if (bottomShadowRenderer != null) bottomShadowRenderer.color = Color.white;
        if (topOnWaterSR != null) topOnWaterSR.color = Color.white;
        if (bottomOnWaterSR != null) bottomOnWaterSR.color = Color.white;
        if (topOutlineSR != null) topOutlineSR.color = Color.white;
        if (bottomOutlineSR != null) bottomOutlineSR.color = Color.white;
        if (topStencilOutlineSR != null) topStencilOutlineSR.color = Color.white;
        if (bottomStencilOutlineSR != null) bottomStencilOutlineSR.color = Color.white;

        CacheSwayBasePose();
        UpdateOnWaterSortingOrder();
    }

    private void Update()
    {
        ApplyWindSway();
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

