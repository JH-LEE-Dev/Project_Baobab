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

    #endregion

    #region Private Fields

    private Transform cachedTransform;
    private Transform topTransform;
    private Transform topShadowTransform;

    private Vector3 topRendererBaseLocalPosition;
    private Quaternion topRendererBaseLocalRotation;
    private Vector3 topShadowBaseLocalPosition;
    private Quaternion topShadowBaseLocalRotation;
    private Material outLineMaterial;
    private Material originalMaterial;

    private float swayPhase;
    private bool isOnWaterActive = false;

    private MaterialPropertyBlock mpb;
    private static readonly int baseColorID = Shader.PropertyToID("_BaseColor");

    #endregion

    #region Unity Events

    // 플레이 시작 시 바람 흔들림의 기준이 되는 상단 스프라이트 기본 포즈를 저장한다.
    private void Awake()
    {
        cachedTransform = transform;
        if (topRenderer != null) topTransform = topRenderer.transform;
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
        if (!isOnWaterActive) return;

        int order = (int)(cachedTransform.position.y * 100);
        if (topOnWaterSR != null) topOnWaterSR.sortingOrder = order;
        if (bottomOnWaterSR != null) bottomOnWaterSR.sortingOrder = order;
    }

    // 에디터 미리보기 모드에서는 값이 바뀔 때마다 비주얼 조합을 즉시 다시 적용한다.
    private void OnValidate()
    {
        if (Application.isPlaying || !previewInEditor)
        {
            return;
        }

        RefreshVisualPreview();
    }

    #endregion

    #region Initialize

    public void Initialize(Transform _topShadowTransform, Material _outLineMaterial)
    {
        if (cachedTransform == null) cachedTransform = transform;
        if (topRenderer != null && topTransform == null) topTransform = topRenderer.transform;
        if (topShadowRenderer != null && topShadowTransform == null) topShadowTransform = _topShadowTransform;

        CacheSwayBasePose();
        ResetVisualState();

        originalMaterial = topRenderer.material;
        outLineMaterial = _outLineMaterial;
    }

    // 에디터에서 랜덤 스프라이트 조합을 다시 확인할 때 수동으로 호출한다.
    [ContextMenu("Refresh Visual Preview")]
    public void RefreshVisualPreview()
    {
        ApplyRandomVisual();
        ResetVisualState();
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
    #endregion

    #region Apply Data
    // 트리 데이터가 적용될 때 데이터에 정의된 스프라이트와 색상을 적용한다.
    public void ApplyVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (topRenderer != null)
        {
            SetRandomSprite(topRenderer, visualData.topSprites);
        }

        if (bottomRenderer != null)
        {
            SetRandomSprite(bottomRenderer, visualData.bottomSprites);
        }

        ApplyColorSet(visualData);
        ApplyDefaultScale();
        SyncShadowSprite();
        CacheSwayBasePose();
        ResetTopSway();
    }

    // 묘목(Sapling) 비주얼을 적용한다.
    public void ApplySaplingVisual(TreeData _treeData)
    {
        TreeVisualData visualData = _treeData.treeVisualData;

        if (topRenderer != null)
        {
            SetRandomSprite(topRenderer, visualData.saplingTopSprites);
        }

        if (bottomRenderer != null)
        {
            SetRandomSprite(bottomRenderer, visualData.saplingBottomSprites);
        }

        ApplyColorSet(visualData);
        ApplyDefaultScale();
        SyncShadowSprite();
        CacheSwayBasePose();
        ResetTopSway();
    }

    public void DeActivateOnWaterObject()
    {
        isOnWaterActive = false;
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(false);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(false);
    }

    public void ActivateOnWaterObject()
    {
        isOnWaterActive = true;
        if (topOnWaterSR != null) topOnWaterSR.gameObject.SetActive(true);
        if (bottomOnWaterSR != null) bottomOnWaterSR.gameObject.SetActive(true);
    }

    private void ApplyColorSet(TreeVisualData _visualData)
    {
        if (_visualData.treeColorSets == null || _visualData.treeColorSets.Count == 0)
        {
            return;
        }

        TreeColorSet colorSet = _visualData.treeColorSets[Random.Range(0, _visualData.treeColorSets.Count)];

        if (topRenderer != null)
        {
            topRenderer.color = colorSet.topColor;
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.color = colorSet.bottomColor;
        }

        if (topOnWaterSR != null)
        {
            topOnWaterSR.color = colorSet.topColor;
        }

        if (bottomOnWaterSR != null)
        {
            bottomOnWaterSR.color = colorSet.bottomColor;
        }
    }

    // 상단/하단 스프라이트를 랜덤으로 고르고 색상과 그림자 비주얼까지 함께 갱신한다. (에디터 미리보기용)
    private void ApplyRandomVisual()
    {
        SetRandomSprite(bottomRenderer, bottomSprites);
        SetRandomSprite(topRenderer, topSprites);

        if (topRenderer != null)
        {
            topRenderer.color = Color.white;
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.color = Color.white;
        }

        if (topOnWaterSR != null)
        {
            topOnWaterSR.color = Color.white;
        }

        if (bottomOnWaterSR != null)
        {
            bottomOnWaterSR.color = Color.white;
        }

        ApplyDefaultScale();
        SyncShadowSprite();
        CacheSwayBasePose();
        ResetTopSway();
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
        var color = bottomRenderer.color;
        color.a = 1f;

        return color;
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
        }
    }

    // 전달받은 렌더러에 스프라이트 리스트 중 하나를 무작위로 적용한다.
    private static void SetRandomSprite(SpriteRenderer _renderer, System.Collections.Generic.IList<Sprite> _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Count == 0)
        {
            return;
        }

        _renderer.sprite = _sprites[Random.Range(0, _sprites.Count)];
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
    }

    public void SetAlpha(float _alpha)
    {
        topRenderer.DOKill();
        bottomRenderer.DOKill();
        if (topShadowRenderer != null) topShadowRenderer.DOKill();
        if (bottomShadowRenderer != null) bottomShadowRenderer.DOKill();
        if (topOnWaterSR != null) topOnWaterSR.DOKill();
        if (bottomOnWaterSR != null) bottomOnWaterSR.DOKill();

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
    }

    public void FadeAlpha(float _targetAlpha, float _duration)
    {
        topRenderer.DOKill();
        bottomRenderer.DOKill();
        topRenderer.DOFade(_targetAlpha, _duration);
        bottomRenderer.DOFade(_targetAlpha, _duration);

        if (topShadowRenderer != null)
        {
            topShadowRenderer.DOKill();
            topShadowRenderer.DOFade(_targetAlpha, _duration);
        }
        if (bottomShadowRenderer != null)
        {
            bottomShadowRenderer.DOKill();
            bottomShadowRenderer.DOFade(_targetAlpha, _duration);
        }
        if (topOnWaterSR != null)
        {
            topOnWaterSR.DOKill();
            topOnWaterSR.DOFade(_targetAlpha, _duration);
        }
        if (bottomOnWaterSR != null)
        {
            bottomOnWaterSR.DOKill();
            bottomOnWaterSR.DOFade(_targetAlpha, _duration);
        }
    }

    public void SetOutline(bool _boolean)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        topRenderer.material = _boolean ? outLineMaterial : originalMaterial;
        bottomRenderer.material = _boolean ? outLineMaterial : originalMaterial;

        if (_boolean)
        {
            // 상단 렌더러 색상 전달
            topRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(baseColorID, topRenderer.color);
            topRenderer.SetPropertyBlock(mpb);

            // 하단 렌더러 색상 전달
            bottomRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(baseColorID, bottomRenderer.color);
            bottomRenderer.SetPropertyBlock(mpb);
        }
        else
        {
            // 아웃라인 해제 시 PropertyBlock 초기화
            topRenderer.SetPropertyBlock(null);
            bottomRenderer.SetPropertyBlock(null);
        }
    }

    #endregion
}
