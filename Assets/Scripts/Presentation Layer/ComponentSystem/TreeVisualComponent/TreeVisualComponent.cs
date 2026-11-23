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

    [Header("Critical Hit Flash")]
    // 치명타 번쩍임은 2단계다: 처음엔 흰색으로 터지고(WhiteHold까지), 주황(MidColor, MidPoint까지)을 거쳐 빨강(criticalHitFlashColor)으로 가라앉는다.
    // WhiteHold·MidPoint는 번쩍임 길이에 대한 비율(0~1)이다.
    [SerializeField] private float criticalHitFlashDuration = 0.26f;
    [SerializeField] private AnimationCurve criticalHitFlashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f));
    [SerializeField] private Color criticalHitFlashMidColor = new Color(1f, 0.6f, 0.15f, 1f);
    [SerializeField, Range(0f, 1f)] private float criticalHitFlashWhiteHold = 0.18f;
    [SerializeField, Range(0f, 1f)] private float criticalHitFlashMidPoint = 0.45f;
    // 치명타 펀치(옆 흔들림)는 hitPunchX에 이 배율을 곱한다.
    [SerializeField] private float criticalPunchMultiplier = 1.8f;

    [Header("Critical Outline Flash")]
    // 치명타 번쩍임 동안 외곽선을 흰색 → 금색으로 번쩍인다. 원래 외곽선이 켜져 있던 나무(도끼 대상)는 원래 색으로 돌아가고,
    // 꺼져 있던 나무는 금색이 흐려지며 사라진다. 시각은 번쩍임 길이에 대한 비율(0~1)이다.
    [SerializeField] private Color criticalOutlineFlashColor = Color.white;
    [SerializeField] private Color criticalOutlineMidColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField, Range(0f, 1f)] private float criticalOutlineWhiteHold = 0.3f;
    [SerializeField, Range(0f, 1f)] private float criticalOutlineMidPoint = 0.6f;

    [Header("Critical Hit Scale")]
    // 치명타일 때만 나무 그림(BaseVisual과 물 반사, 밑동 기준)이 빠르게 옆으로 퍼지며 눌렸다가(가로 커짐·세로 눌림)
    // 반대로 한 번 출렁인 뒤 원래 크기로 돌아온다.
    // visualRoot는 키우지 않는다: 그 자식인 topRoot/bottomRoot를 부메랑 판정·별자리 폭발 범위 등 게임 판정이 읽기 때문이다.
    [SerializeField] private float criticalScalePeak = 1.08f;
    [SerializeField] private float criticalScaleUpDuration = 0.05f;
    [SerializeField] private float criticalScaleDownDuration = 0.12f;
    // 가로가 커지는 만큼 세로를 이 비율로 눌러 준다(0이면 가로·세로가 같이 커진다).
    [SerializeField, Range(0f, 1f)] private float criticalSquashRatio = 0.6f;

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

    // 별 표식 링의 HDR 덮어쓰기. 0 이하이면 highlightHDRIntensity를 쓴다(본편 별자리 나무의 기존 동작).
    // 로그라이크 표식이 SetConstellationMarkActive로 값을 넘기고, 표식이 꺼지면 0으로 돌아간다.
    private float constellationHDROverride;

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
    // 치명타 스케일 트윈. visualRoot에 이미 펀치 트윈이 걸리므로 타깃을 visualRoot로 두면 재활용된 펀치 트윈을
    // 이 참조로 잘못 죽일 수 있다. 그래서 이 트윈만 쓰는 전용 타깃 객체를 둔다(KillLastTween의 타깃 비교가 정확해진다).
    private Tween critScaleTween;
    private readonly object critScaleTweenTarget = new object();
    private DG.Tweening.Core.DOGetter<float> critScaleGetter;
    private DG.Tweening.Core.DOSetter<float> critScaleSetter;
    private float critScaleProgress;
    private float critScaleStartOffset;
    private float critScaleCurrentOffset;
    // 확대 대상(그림 루트)과 원래 크기. 물 반사 루트는 좌우 반전(-1) 크기를 갖고 있어 원래 크기에 곱해 키운다.
    // 첫 치명타 때 한 번만 찾는다(그 전에는 아무도 이 두 루트의 크기를 바꾸지 않는다).
    private bool bCritScaleTargetsResolved;
    private Transform critScaleBaseVisual;
    private Transform critScaleWaterVisual;
    private Vector3 critScaleBaseVisualScale;
    private Vector3 critScaleWaterVisualScale;
    private float critScaleUpTime;
    private float critScaleTotalTime;
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

    // 실드(Top/Bottom Shield)·하이라이트·별자리·물 위 실드/하이라이트 그림은 HDR 세기만 블록으로 올려서(기본 1.05), 블록 때문에 SRP Batcher를
    // 못 타고 하나씩 그려졌다. HDR 세기만 다른 재질 사본을 (원본 재질, 세기)마다 하나씩 만들어 끼우고, 블록 값이 모두 끼운 재질 값과 같으면
    // 블록을 비운다(CommitBodyBlock). 사본은 원본의 셰이더·프로퍼티·키워드를 그대로 베끼고 HDR만 바꾸므로 셰이더가 받는 값은 예전과 같다.
    private static readonly Dictionary<Material, Dictionary<float, Material>> hdrMaterialCache = new Dictionary<Material, Dictionary<float, Material>>();
    // 각 렌더러에 처음 끼워져 있던 원본 재질(사본을 만들 기준).
    private Material topShieldBaseMaterial;
    private Material bottomShieldBaseMaterial;
    private Material topHighlightBaseMaterial;
    private Material bottomHighlightBaseMaterial;
    private Material constellationBaseMaterial;
    private Material topShieldOnWaterBaseMaterial;
    private Material topHighlightOnWaterBaseMaterial;

    // Hit Flash
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");
    private static readonly int HeatChargeID = Shader.PropertyToID("_HeatCharge");
    // 깎인 모습(양수 = 금, 음수 = 숯불, 0 = 없음). 지금 렌더러에 들어가 있는 값을 기억해 같은 값은 다시 쓰지 않는다.
    private static readonly int DamageLookID = Shader.PropertyToID("_DamageLook");
    // 깎인 모습 무늬 위치를 정하는 나무 루트 월드 위치. 렌더러 위치는 피격 흔들림으로 움직여 무늬가 바뀌므로 루트 위치를 따로 넣는다.
    private static readonly int DamageLookPivotID = Shader.PropertyToID("_DamageLookPivot");
    private float appliedTopDamageLook = 0f;
    private float appliedBottomDamageLook = 0f;
    // 깎인 모습을 넣은 이 나무 전용 재질 사본(SetDamageLookToRenderer). 깎이지 않았으면 null이다.
    private Material topDamageMaterial;
    private Material bottomDamageMaterial;
    // 원본 재질마다 쓰고 돌려받은 깎인 모습 재질 사본.
    private static readonly Dictionary<Material, Stack<Material>> damageMaterialPool = new Dictionary<Material, Stack<Material>>();
    // 배리어가 깨진 직후의 푸른 테두리 잔광 세기. 지금 렌더러에 들어가 있는 값을 기억해 같은 값은 다시 쓰지 않는다.
    private static readonly int BarrierAfterglowID = Shader.PropertyToID("_BarrierAfterglow");
    private float appliedBarrierAfterglow = 0f;
    // 2스테이지 포자막 상태(SetSporeShieldFx). 지금 포자막 렌더러에 들어가 있는 값을 기억해 같은 값은 다시 쓰지 않는다.
    private static readonly int SporeShieldFxID = Shader.PropertyToID("_SporeShieldFx");
    private Vector4 appliedSporeShieldFx = Vector4.zero;
    // 치명타(빨간색) 점멸이 끝나는 시각. 이 시각 전까지 들어온 일반 타격은 점멸을 덮어쓰지 않는다.
    // bool 플래그 대신 시각으로 들고 있는 이유: 점멸 도중 오브젝트가 꺼지면 코루틴이 끝을 못 봐서
    // 플래그가 켜진 채 남을 수 있는데, 시각은 시간이 지나면 저절로 풀린다.
    private float criticalFlashEndTime = 0f;
    // 지금 진행 중인 번쩍임이 치명타(2단계 색·외곽선 번쩍임)인지. 다른 번쩍임이 시작되거나 상태를 되돌릴 때 끈다.
    private bool bCritFlashActive = false;
    // 치명타 외곽선 번쩍임 때문에 외곽선 오브젝트를 켜 두었는지(원래 꺼져 있던 나무라도 번쩍임 동안은 켠다).
    private bool bCritOutlineShown = false;
    private bool bCritOutlineColorApplied = false;
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private MaterialPropertyBlock _flashMPB;
    // 플래시 진행은 TreeFlashSystem이 한 곳에서 틱한다(나무마다 코루틴을 만들지 않는다)

    // 수관·밑동(topRenderer/bottomRenderer) 블록에 들어가는 float 값들. 이 밖에는 _FlashColor(색)만 들어간다
    // (HDR은 하이라이트·실드·물 위·별자리, 포자막은 실드, 외곽선 색은 외곽선 렌더러에만 넣는다. 깎인 모습은 블록이 아니라 재질 사본에 넣는다).
    private static readonly int[] BodyBlockFloatIDs = { FlashAmountID, HeatChargeID, BarrierAfterglowID, EnableWindSwayID };
    // BodyBlockFloatIDs에서 HeatChargeID의 자리.
    private const int HeatChargeBodyBlockIndex = 1;
    // 블록 값이 모두 이 값과 같으면 블록을 비운다(CommitBodyBlock). 기본 재질 값이며, 보석 재질도 같은 값을 가질 때만 쓴다.
    private float[] topBodyBlockDefaults;
    private float[] bottomBodyBlockDefaults;
    private Color topFlashColorDefault;
    private Color bottomFlashColorDefault;
    private bool bBodyBlockDefaultsResolved = false;

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

    // 나무 그림 전체(별자리 표식 포함)보다 앞에 그려야 하는 나무 부착 연출(3스테이지 배리어)의 정렬 순서. UpdateSortingOrder 뒤에 읽는다.
    public int GetFrontEffectSortingOrder() => GetTopHighlightSortingOrder() + 2;

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
    public void SetConstellationMarkActive(bool _active, float _hdrOverride = 0f)
    {
        if (constellationRenderer != null)
        {
            // 꺼질 때는 덮어쓰기를 항상 지운다(풀에서 재사용되는 나무에 남지 않게).
            float hdrOverride = _active ? Mathf.Max(0f, _hdrOverride) : 0f;

            // ResetTree가 스폰/반환마다 false로 호출한다. 이미 같은 상태면 HDR 재적용(렌더러 7개 MPB 갱신)까지 전부 생략한다.
            // HDR 입력값(스프라이트/실드/물 위 상태)이 바뀌는 경로는 각자 UpdateHDRStates를 호출하므로 결과는 동일하다.
            // 켜진 채로 덮어쓰기 값만 바뀌는 경우(본편 표식 위에 로그라이크 표식)는 HDR만 다시 적용한다.
            if (constellationRenderer.gameObject.activeSelf == _active && constellationHDROverride == hdrOverride) return;

            constellationHDROverride = hdrOverride;
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

        ResetCritScale();
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

    /// <summary>[로그라이크 전용] ApplySaplingVisual 직후 묘목 위쪽 그림을 바꿔 끼운다(공용 데이터의 잘못된 묘목 그림 보정). 본편은 부르지 않는다.</summary>
    public void SetSaplingTopSpriteForRun(Sprite _sprite)
    {
        if (null == topRenderer || null == _sprite) return;

        topRenderer.sprite = _sprite;
        defaultTopSprite = _sprite;
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
    // 치명타면 스케일 펀치(PlayCriticalHitScale)도 함께 건다.
    public void PlayHitFeedback(bool _bCritical = false)
    {
        if (visualRoot == null)
        {
            return;
        }

        KillHitPunchTween();
        visualRoot.localPosition = Vector3.zero;
        // 피격마다 새 Tweener를 만들지 않도록 재활용한다. 죽일 때는 target이 visualRoot인지 확인하므로
        // 다른 나무에 재활용된 트윈을 잘못 죽일 일이 없다(KillHitPunchTween 참조).
        float punchX = _bCritical ? hitPunchX * criticalPunchMultiplier : hitPunchX;
        if (hitDuration <= 0f)
        {
            // DOPunchPosition은 길이가 0 이하면 경고만 남기고 null을 돌려준다. 그 경로는 그대로 둔다.
            hitPunchTween = visualRoot.DOPunchPosition(new Vector3(punchX, 0f, 0f), hitDuration, hitVibrato, hitElasticity).SetRecyclable(true);
        }
        else
        {
            // DOPunchPosition 본문(DOTween.Punch(getter, setter, ...).SetTarget(target).SetOptions(false))과 같다.
            // 피격마다 새로 만들던 getter/setter 대리자만 visualRoot별로 한 번 만들어 둔다.
            EnsureHitPunchAccessors();
            hitPunchTween = DOTween.Punch(hitPunchGetter, hitPunchSetter, new Vector3(punchX, 0f, 0f), hitDuration, hitVibrato, hitElasticity)
                .SetTarget(visualRoot).SetOptions(false).SetRecyclable(true);
        }

        if (_bCritical)
        {
            PlayCriticalHitScale();
        }
    }

    // 피격 펀치용 getter/setter. DOPunchPosition이 매번 만들던 것과 같은 대상(그 시점의 visualRoot)을 붙잡는다.
    private Transform hitPunchAccessorTarget;
    private DG.Tweening.Core.DOGetter<Vector3> hitPunchGetter;
    private DG.Tweening.Core.DOSetter<Vector3> hitPunchSetter;

    private void EnsureHitPunchAccessors()
    {
        if (null != hitPunchGetter && ReferenceEquals(hitPunchAccessorTarget, visualRoot)) return;

        CreateHitPunchAccessors(visualRoot);
    }

    // 람다가 붙잡는 변수를 이 함수의 인자로 둔다. 같은 함수 안에 두면 컴파일러가 붙잡을 상자를 함수 첫머리에서 만들어
    // 위의 이미 만들어 둔 경우(바로 돌아가는 경로)에도 피격마다 할당이 생긴다.
    private void CreateHitPunchAccessors(Transform _target)
    {
        hitPunchAccessorTarget = _target;
        hitPunchGetter = () => _target.localPosition;
        hitPunchSetter = x => _target.localPosition = x;
    }

    // 치명타: 빠르게(OutQuad) 커졌다가, 줄어들며 반대로 한 번 출렁이고(감쇠 진동) 원래 크기로 돌아온다.
    // 진행 중에 다시 치명타가 들어오면 지금 크기에서 이어서 다시 커지므로 1프레임 튀는 일이 없다.
    private void PlayCriticalHitScale()
    {
        float upTime = Mathf.Max(0f, criticalScaleUpDuration);
        float totalTime = upTime + Mathf.Max(0f, criticalScaleDownDuration);
        if (totalTime <= 0f)
        {
            return;
        }

        ResolveCritScaleTargets();
        if (critScaleBaseVisual == null && critScaleWaterVisual == null)
        {
            return;
        }

        // 진행 중인 치명타 스케일이 있을 때만 현재 크기를 이어받는다. 없으면 기본 크기(1)에서 시작한다.
        // 반대로 출렁이는 중(음수)이어도 그 크기에서 이어서 커져야 튀지 않는다.
        critScaleStartOffset = critScaleTween.IsActive() && ReferenceEquals(critScaleTween.target, critScaleTweenTarget)
            ? critScaleCurrentOffset
            : 0f;
        KillCritScaleTween();

        critScaleUpTime = upTime;
        critScaleTotalTime = totalTime;
        critScaleProgress = 0f;

        if (critScaleGetter == null)
        {
            critScaleGetter = GetCritScaleProgress;
            critScaleSetter = SetCritScaleProgress;
        }

        critScaleTween = DOTween.To(critScaleGetter, critScaleSetter, 1f, totalTime)
            .SetEase(Ease.Linear)
            .SetTarget(critScaleTweenTarget)
            .SetRecyclable(true);
    }

    private float GetCritScaleProgress()
    {
        return critScaleProgress;
    }

    private void SetCritScaleProgress(float _progress)
    {
        critScaleProgress = _progress;

        float peakOffset = criticalScalePeak - 1f;
        float time = _progress * critScaleTotalTime;
        float offset;

        if (time < critScaleUpTime)
        {
            float t = time / critScaleUpTime;
            float easeOut = 1f - (1f - t) * (1f - t);
            offset = Mathf.Lerp(critScaleStartOffset, peakOffset, easeOut);
        }
        else
        {
            float downTime = critScaleTotalTime - critScaleUpTime;
            float t = downTime > 0f ? Mathf.Clamp01((time - critScaleUpTime) / downTime) : 1f;
            // 감쇠 진동: 최고점에서 줄어들어 t=1/3에 원래 크기를 지나 반대로 살짝 눌렸다가(가로 줄고 세로 늘어남) t=1에 0이 된다.
            offset = peakOffset * (1f - t) * Mathf.Cos(1.5f * Mathf.PI * t);
        }

        ApplyCritScaleOffset(offset);
    }

    private void ResolveCritScaleTargets()
    {
        if (bCritScaleTargetsResolved)
        {
            return;
        }

        bCritScaleTargetsResolved = true;

        // topRoot/bottomRoot가 딸린 visualRoot나 나무 루트 자체는 대상으로 쓰지 않는다(판정 위치가 움직인다).
        critScaleBaseVisual = baseVisualObj != null ? baseVisualObj.transform : null;
        if (critScaleBaseVisual == visualRoot || critScaleBaseVisual == transform)
        {
            critScaleBaseVisual = null;
        }

        critScaleWaterVisual = topOnWaterSR != null ? topOnWaterSR.transform.parent : null;
        if (critScaleWaterVisual == visualRoot || critScaleWaterVisual == transform || critScaleWaterVisual == critScaleBaseVisual)
        {
            critScaleWaterVisual = null;
        }

        if (critScaleBaseVisual != null) critScaleBaseVisualScale = critScaleBaseVisual.localScale;
        if (critScaleWaterVisual != null) critScaleWaterVisualScale = critScaleWaterVisual.localScale;
    }

    private void ApplyCritScaleOffset(float _offset)
    {
        critScaleCurrentOffset = _offset;
        float scaleX = 1f + _offset;
        float scaleY = 1f - _offset * criticalSquashRatio;

        // 트윈 도중 나무가 파괴되면(씬 전환 등) 렌더러 루트도 함께 사라지므로 Unity null 비교로 거른다.
        if (critScaleBaseVisual != null)
        {
            critScaleBaseVisual.localScale = new Vector3(critScaleBaseVisualScale.x * scaleX, critScaleBaseVisualScale.y * scaleY, critScaleBaseVisualScale.z);
        }

        if (critScaleWaterVisual != null)
        {
            critScaleWaterVisual.localScale = new Vector3(critScaleWaterVisualScale.x * scaleX, critScaleWaterVisualScale.y * scaleY, critScaleWaterVisualScale.z);
        }
    }

    private void KillCritScaleTween()
    {
        KillLastTween(critScaleTween, critScaleTweenTarget, false);
    }

    // 진행 중인 치명타 스케일을 멈추고 그림 루트를 원래 크기로 되돌린다. 한 번도 치명타를 맞지 않았으면 할 일이 없다.
    private void ResetCritScale()
    {
        KillCritScaleTween();

        if (bCritScaleTargetsResolved && critScaleCurrentOffset != 0f)
        {
            ApplyCritScaleOffset(0f);
        }
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

        if (_bCritical)
            PlayFlash(criticalHitFlashDuration, criticalHitFlashCurve, criticalHitFlashColor, true);
        else
            PlayFlash(hitFlashDuration, hitFlashCurve, hitFlashColor, false);
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

        if (_bCritical)
            StartFlash(criticalHitFlashDuration, criticalHitFlashCurve, criticalHitFlashColor, true);
        else
            StartFlash(hitFlashDuration, hitFlashCurve, hitFlashColor, false);
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

        // 일반 점멸은 색이 바뀌지 않으므로 시작할 때 한 번만 넣고, 진행 중에는 세기만 갱신한다.
        // 치명타 점멸은 진행도에 따라 색(흰색 → 주황 → 빨강)과 외곽선을 매 틱 갱신한다(ApplyFlashAmountFromSystem).
        if (_bCritical)
        {
            BeginCriticalFlash();
        }
        else
        {
            EndCriticalFlash();
            ApplyFlashColorToRenderers(_color);
        }

        criticalFlashEndTime = _bCritical ? Time.time + _duration : 0f;
        TreeFlashSystem.Begin(this, _curve, _duration);
    }

    /// <summary>
    /// 열기 차오름 진행도(0~1)를 나무 그림에 넣는다(TreeHeatIndicator가 카운트다운 동안 매 프레임 부른다, 끝나면 0).
    /// 셰이더가 스테이지 설정(꺼진 스테이지·로그라이크는 끔)에 따라 뜨거운 줄기를 밝힌다. 렌더링 전용이다.
    /// </summary>
    public void SetHeatCharge(float _charge)
    {
        // 켜져 있는 동안은 값만 기억해 두고 그리기 직전에 카메라에 걸릴 때만 써넣는다(TreeDeferredMaterialWrites).
        if (bDeferralEnabled)
        {
            deferredHeatCharge = _charge;
            bHeatChargeDeferred = true;
            if (deferredWriteIndex < 0) TreeDeferredMaterialWrites.Register(this);
            return;
        }

        bHeatChargeDeferred = false;
        WriteHeatCharge(_charge);
    }

    /// <summary>
    /// 열기 값을 미루지 않고 바로 넣는다. 카운트다운이 끝나 더 갱신되지 않을 값(TreeHeatIndicator.Stop의 0)에 쓴다 -
    /// 미뤄 두면 화면 밖 나무가 검사 목록에 계속 남는다. 그보다 먼저 미뤄 둔 열기 값은 이 값에 덮이므로 버린다.
    /// </summary>
    public void SetHeatChargeImmediate(float _charge)
    {
        bHeatChargeDeferred = false;
        WriteHeatCharge(_charge);
    }

    private void WriteHeatCharge(float _charge)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        // 빛나는 줄기·불씨는 하이라이트 그림에 들어 있어 하이라이트에도 넣는다.
        SetHeatChargeToRenderer(topRenderer, _charge);
        SetHeatChargeToRenderer(bottomRenderer, _charge);
        SetHeatChargeToRenderer(topHighlightRenderer, _charge);
        SetHeatChargeToRenderer(bottomHighlightRenderer, _charge);
    }

    private void SetHeatChargeToRenderer(SpriteRenderer sr, float _charge)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetFloat(HeatChargeID, _charge);

        // 방금 넣은 열기 값이 비워도 되는 기준 값과 다르면 CommitBodyBlock은 다른 값을 볼 것 없이 블록을 그대로 넣는다.
        // 그 판정을 열기 값 하나로 먼저 해 나머지 값 읽기를 건너뛴다(같으면 예전처럼 전부 본다).
        if (IsHeatChargeOffDefault(sr, _charge))
            sr.SetPropertyBlock(_flashMPB);
        else
            CommitBodyBlock(sr, _flashMPB);
    }

    // IsBodyBlockAtDefaults가 열기 값 때문에 false를 낼 것이 확실한지. 확실하지 않으면 false.
    private bool IsHeatChargeOffDefault(SpriteRenderer _sr, float _charge)
    {
        if (IsHDRMaterialRenderer(_sr))
        {
            Material material = _sr.sharedMaterial;
            return null == material || false == material.HasFloat(HeatChargeID) || material.GetFloat(HeatChargeID) != _charge;
        }

        bool bTop = _sr == topRenderer;
        if (false == bTop && _sr != bottomRenderer) return false;

        ResolveBodyBlockDefaults();
        float[] defaults = bTop ? topBodyBlockDefaults : bottomBodyBlockDefaults;
        return null == defaults || defaults[HeatChargeBodyBlockIndex] != _charge;
    }

    /// <summary>
    /// 깎인 정도(0~1)를 나무 그림에 넣는다. 나무 셰이더가 4스테이지에서만 금(돌처럼 생긴 나무) 또는
    /// 숯불(_bEmber, 잿불 나무)을 그린다(다른 스테이지·로그라이크는 값이 들어와도 그대로다). 렌더링 전용이다.
    /// 숯불은 수관 그림의 명암에 맞춘 표현이라 수관에만 넣고 밑동에는 넣지 않는다.
    /// 같은 값이면 아무것도 하지 않으므로, 호출부가 값을 단계로 끊어 넘기면 재질 값 쓰기가 그만큼 줄어든다.
    /// </summary>
    public void SetDamageLook(float _amount, bool _bEmber)
    {
        float topLook = _bEmber ? -_amount : _amount;
        if (appliedTopDamageLook == topLook) return;

        EnsureDefaultMaterialsCached();

        appliedTopDamageLook = topLook;
        SetDamageLookToRenderer(topRenderer, defaultTopMaterial, ref topDamageMaterial, topLook);

        float bottomLook = _bEmber ? 0f : _amount;
        if (appliedBottomDamageLook != bottomLook)
        {
            appliedBottomDamageLook = bottomLook;
            SetDamageLookToRenderer(bottomRenderer, defaultBottomMaterial, ref bottomDamageMaterial, bottomLook);
        }
    }

    // 깎인 모습(_DamageLook)과 무늬 위치 기준(_DamageLookPivot, 나무 루트 위치)은 프로퍼티 블록이 아니라 이 나무 전용 재질 사본에 넣는다.
    // 블록이 남은 렌더러는 SRP Batcher를 못 타고 하나씩 그려지는데, 깎인 나무는 쓰러질 때까지 값을 들고 있어 넓은 공격 뒤에는
    // 그런 나무가 많이 남았다. 사본은 원본 재질을 그대로 베끼고 이 두 값만 바꾸므로 셰이더가 받는 값은 블록으로 넣던 때와 같다.
    // 무늬 위치는 나무마다 달라 사본을 나무끼리 나눠 쓸 수 없다. 깎인 모습이 0으로 돌아가면(쓰러져 풀로 돌아갈 때 등) 원본 재질로
    // 되돌리고 사본은 원본 재질별 풀에 돌려 다른 나무가 다시 쓴다(살아 있는 깎인 나무 수만큼만 사본이 있다).
    // 보석 재질이 끼워져 있는 동안은 사본을 끼우지 않는다(보석 셰이더는 이 값을 읽지 않아 블록으로 넣던 때도 아무것도 바뀌지 않았다).
    // 보석이 풀리면 ApplyGemVisual이 사본을 다시 끼운다(블록은 재질을 바꿔도 남아 있었으므로 같은 결과).
    private void SetDamageLookToRenderer(SpriteRenderer _sr, Material _baseMaterial, ref Material _damageMaterial, float _look)
    {
        if (_sr == null || _baseMaterial == null) return;

        if (0f == _look)
        {
            if (null == _damageMaterial) return;
            if (_sr.sharedMaterial == _damageMaterial) _sr.sharedMaterial = _baseMaterial;
            ReleaseDamageMaterial(_baseMaterial, _damageMaterial);
            _damageMaterial = null;
            return;
        }

        if (null == _damageMaterial) _damageMaterial = AcquireDamageMaterial(_baseMaterial);
        _damageMaterial.SetFloat(DamageLookID, _look);
        // 무늬 위치 기준(나무 루트 위치, 흔들리지 않는다). 풀에서 다시 꺼낸 나무도 여기서 새 위치로 들어간다.
        Vector3 pivot = transform.position;
        _damageMaterial.SetVector(DamageLookPivotID, new Vector4(pivot.x, pivot.y, 0f, 0f));
        if (_sr.sharedMaterial == _baseMaterial) _sr.sharedMaterial = _damageMaterial;
    }

    private static Material AcquireDamageMaterial(Material _baseMaterial)
    {
        if (damageMaterialPool.TryGetValue(_baseMaterial, out Stack<Material> pool))
        {
            while (pool.Count > 0)
            {
                Material pooled = pool.Pop();
                if (null != pooled) return pooled;
            }
        }

        return new Material(_baseMaterial) { name = _baseMaterial.name + " (Damage)", hideFlags = HideFlags.DontSave };
    }

    private static void ReleaseDamageMaterial(Material _baseMaterial, Material _damageMaterial)
    {
        if (false == damageMaterialPool.TryGetValue(_baseMaterial, out Stack<Material> pool))
        {
            pool = new Stack<Material>();
            damageMaterialPool.Add(_baseMaterial, pool);
        }

        pool.Push(_damageMaterial);
    }

    /// <summary>
    /// 이 나무의 렌더러(_source)를 베낀 대역(_copy, 지진에 쓰러진 나무가 솟구치는 대역 등)이 깎인 모습 재질 사본을 그대로 끼우고 있으면
    /// 원본 재질로 바꾸고 깎인 모습 값을 대역의 블록에 넣는다. 사본을 그대로 두면 나무가 풀로 돌아간 뒤 사본이 다른 나무에 넘어가
    /// 대역의 금·숯불이 바뀐다. 블록으로 넣던 때 대역이 블록째 베끼던 값과 같다.
    /// </summary>
    public void DetachDamageLookCopy(SpriteRenderer _source, SpriteRenderer _copy)
    {
        if (_source == null || _copy == null) return;

        Material damageMaterial = _source == topRenderer ? topDamageMaterial : (_source == bottomRenderer ? bottomDamageMaterial : null);
        if (null == damageMaterial || _copy.sharedMaterial != damageMaterial) return;

        _copy.sharedMaterial = _source == topRenderer ? defaultTopMaterial : defaultBottomMaterial;
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        _copy.GetPropertyBlock(_flashMPB);
        _flashMPB.SetFloat(DamageLookID, damageMaterial.GetFloat(DamageLookID));
        _flashMPB.SetVector(DamageLookPivotID, damageMaterial.GetVector(DamageLookPivotID));
        _copy.SetPropertyBlock(_flashMPB);
    }

    // ===== 화면 밖 재질 쓰기 미루기 =====
    // 열기 차오름(SetHeatCharge)과 점멸(TreeFlashSystem이 매 틱 넣는 세기, 치명타는 색·외곽선 색까지)은 진행 중인 나무마다 매 프레임
    // 렌더러 4~8개의 블록을 읽고 다시 넣는데, 대부분 화면 밖 나무다. 값은 그리는 데만 쓰이므로 가장 나중 값만 기억해 두고, 카메라가
    // 그리기 직전(TreeDeferredMaterialWrites)에 그 카메라에 걸리는 나무만 실제로 써넣는다.
    // - 다른 값들은 "블록을 읽어 한 값만 바꿔 다시 넣는" 방식이라 이 값들이 늦게 들어가도 나머지 값은 그대로다.
    // - 블록을 비우는 판정(CommitBodyBlock)은 비워도 셰이더가 받는 값이 같을 때만 비우므로, 써넣은 뒤 셰이더가 받는 값은 바로 쓰던 때와 같다.
    // - 같은 값을 바로 쓰는 경로(점멸 시작·끝, 초기화)는 미뤄 둔 쓰기를 먼저 쓰거나(순서 유지) 덮이는 것만 버린다.
    // - 나무가 꺼질 때와, 블록을 밖에서 읽어 가는 곳(TreeFadeMask·지진 대역)은 읽기 전에 미뤄 둔 값을 먼저 써넣는다(FlushDeferredMaterial).
    private const byte DeferredFlashNone = 0;
    private const byte DeferredFlashNormal = 1;
    private const byte DeferredFlashCrit = 2;
    private bool bDeferralEnabled;
    private bool bHeatChargeDeferred;
    private float deferredHeatCharge;
    private byte deferredFlashKind;
    // 미뤄 둔 점멸의 세기는 이 곡선의 deferredFlashProgress 지점 값이다(써넣을 때 구한다).
    private AnimationCurve deferredFlashCurve;
    private float deferredFlashProgress;
    private bool bDeferredCritOutline;
    private Color deferredCritOutlineTop;
    private Color deferredCritOutlineBottom;
    // TreeDeferredMaterialWrites 목록에서의 자리(-1이면 목록에 없다).
    internal int deferredWriteIndex = -1;
    // 미룬 값을 받는 렌더러(수관·밑동·실드·하이라이트·외곽선)가 나무 뿌리 위치에서 뻗을 수 있는 범위(월드 유닛)를 넉넉히 잡은 것.
    // 렌더러마다 경계를 구하면 그 비용이 아끼는 쓰기 비용과 맞먹어서, 뿌리 위치 하나와 고정 범위로 판정한다.
    // 실측(네 숲 3단계, 모든 특성 상태): 왼쪽 0.88, 오른쪽 0.78, 아래 0.39, 위 2.58이 최대(나무 배율 최대 1.48) - 그 1.5배 이상으로 둔다.
    // 판정 때 각 변에 PixelVfxCulling.MarginUnits가 더 붙는다. 새 나무 그림이 이 범위를 넘으면 에디터 점검(EditorCheckDeferredReach)이 오류를 낸다.
    private const float DeferredReachSide = 1.5f;
    private const float DeferredReachDown = 1.0f;
    private const float DeferredReachUp = 4.0f;
    // 미룬 값을 받는 렌더러들이 놓인 레이어의 비트 합(0이면 아직 안 구했다). 이 레이어를 그리지 않는 카메라(UI 카메라 등)는 판정에서 건너뛴다.
    // 나무 렌더러의 레이어를 실행 중에 바꾸는 코드는 없고, 나무가 켜질 때마다 처음 쓸 때 다시 구한다.
    private int deferredLayerMask;
    // 이번 그리기 묶음에서 읽어 둔 뿌리 위치(카메라가 여럿이어도 한 번만 읽는다).
    private int deferredRootSerial = -1;
    private Vector3 deferredRootPosition;

    internal bool HasDeferredMaterial => bHeatChargeDeferred || DeferredFlashNone != deferredFlashKind;

    internal int DeferredLayerMask
    {
        get
        {
            if (0 == deferredLayerMask)
            {
                int mask = 0;
                AddDeferredLayer(topRenderer, ref mask);
                AddDeferredLayer(bottomRenderer, ref mask);
                AddDeferredLayer(topShieldRenderer, ref mask);
                AddDeferredLayer(bottomShieldRenderer, ref mask);
                AddDeferredLayer(topHighlightRenderer, ref mask);
                AddDeferredLayer(bottomHighlightRenderer, ref mask);
                AddDeferredLayer(topOutlineSR, ref mask);
                AddDeferredLayer(bottomOutlineSR, ref mask);
                // 렌더러가 하나도 없으면 쓸 곳도 없으니 어느 카메라에서든 바로 걸리는 것으로 본다.
                deferredLayerMask = 0 == mask ? -1 : mask;
            }

            return deferredLayerMask;
        }
    }

    private static void AddDeferredLayer(SpriteRenderer _sr, ref int _mask)
    {
        if (_sr != null) _mask |= 1 << _sr.gameObject.layer;
    }

    /// <summary>미뤄 둔 열기·점멸을 지금 써넣는다. 미룬 것이 없으면 아무것도 하지 않는다.</summary>
    internal void FlushDeferredMaterial()
    {
        if (deferredWriteIndex >= 0) TreeDeferredMaterialWrites.Unregister(this);

        if (bHeatChargeDeferred)
        {
            bHeatChargeDeferred = false;
            WriteHeatCharge(deferredHeatCharge);
        }

        if (DeferredFlashNone != deferredFlashKind) FlushDeferredFlash();
    }

    /// <summary>
    /// 미룬 값을 받는 렌더러가 이 절두체에 걸릴 수 있는지. 나무 뿌리 위치에서 고정 범위(DeferredReach*)만큼 뻗은 상자를 각 변에 _margin만큼
    /// 더 넓혀 본다. 실제 렌더러 경계는 이 상자 안에 있으므로, 상자가 절두체 밖이면 유니티 컬링도 이 나무를 그리지 않는다.
    /// _planeData는 평면 6개의 (nx, ny, nz, |nx|, |ny|, |nz|, d), _serial은 그리기 묶음 번호(같은 묶음 안에서는 위치를 다시 읽지 않는다).
    /// </summary>
    internal bool IsDeferredTargetInFrustum(float[] _planeData, float _margin, int _serial)
    {
        if (deferredRootSerial != _serial)
        {
            deferredRootSerial = _serial;
            deferredRootPosition = cachedTransform.position;
        }

        float cx = deferredRootPosition.x;
        float cy = deferredRootPosition.y + (DeferredReachUp - DeferredReachDown) * 0.5f;
        float cz = deferredRootPosition.z;
        float ex = DeferredReachSide + _margin;
        float ey = (DeferredReachUp + DeferredReachDown) * 0.5f + _margin;
        float ez = PresentationLayer.VFX.PixelVfxCulling.BoundsDepth * 0.5f + _margin;
        for (int o = 0; o < 6 * 7; o += 7)
        {
            float reach = _planeData[o] * cx + _planeData[o + 1] * cy + _planeData[o + 2] * cz + _planeData[o + 6]
                + _planeData[o + 3] * ex + _planeData[o + 4] * ey + _planeData[o + 5] * ez;
            if (0f > reach) return false;
        }

        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터 전용 점검. 미룬 값을 받는 렌더러들의 실제 경계가 판정에 쓰는 고정 범위 안에 있는지 본다.
    /// 넘으면 false와 함께 얼마나 넘었는지 돌려준다(그 나무는 화면에 걸려도 값이 늦게 들어갈 수 있으니 DeferredReach*를 키워야 한다).
    /// </summary>
    internal bool EditorCheckDeferredReach(out string _detail)
    {
        _detail = null;
        Vector3 root = cachedTransform.position;
        float left = 0f, right = 0f, down = 0f, up = 0f;
        EditorMeasureReach(topRenderer, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(bottomRenderer, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(topShieldRenderer, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(bottomShieldRenderer, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(topHighlightRenderer, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(bottomHighlightRenderer, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(topOutlineSR, root, ref left, ref right, ref down, ref up);
        EditorMeasureReach(bottomOutlineSR, root, ref left, ref right, ref down, ref up);
        if (left <= DeferredReachSide && right <= DeferredReachSide && down <= DeferredReachDown && up <= DeferredReachUp) return true;

        _detail = name + " 왼쪽 " + left.ToString("F2") + " 오른쪽 " + right.ToString("F2") + " 아래 " + down.ToString("F2") + " 위 " + up.ToString("F2")
            + " (허용 좌우 " + DeferredReachSide + " 아래 " + DeferredReachDown + " 위 " + DeferredReachUp + ")";
        return false;
    }

    private static void EditorMeasureReach(SpriteRenderer _sr, Vector3 _root, ref float _left, ref float _right, ref float _down, ref float _up)
    {
        if (_sr == null || false == _sr.enabled || false == _sr.gameObject.activeInHierarchy || _sr.sprite == null) return;

        Bounds bounds = _sr.bounds;
        _left = Mathf.Max(_left, _root.x - bounds.min.x);
        _right = Mathf.Max(_right, bounds.max.x - _root.x);
        _down = Mathf.Max(_down, _root.y - bounds.min.y);
        _up = Mathf.Max(_up, bounds.max.y - _root.y);
    }
#endif


    private void OnEnable()
    {
        bDeferralEnabled = Application.isPlaying;
        deferredLayerMask = 0;
    }

    // 꺼지는 나무는 그리기 직전 검사를 받지 못하므로 미뤄 둔 값을 여기서 써넣고, 꺼져 있는 동안 들어오는 값은 바로 쓴다.
    private void OnDisable()
    {
        FlushDeferredMaterial();
        bDeferralEnabled = false;
    }

    // 깎인 채로 나무가 없어지면(씬 정리 등) 끼워 두었던 사본을 풀로 돌린다.
    private void OnDestroy()
    {
        if (null != topDamageMaterial && null != defaultTopMaterial) ReleaseDamageMaterial(defaultTopMaterial, topDamageMaterial);
        if (null != bottomDamageMaterial && null != defaultBottomMaterial) ReleaseDamageMaterial(defaultBottomMaterial, bottomDamageMaterial);
        topDamageMaterial = null;
        bottomDamageMaterial = null;
    }

    /// <summary>
    /// 배리어가 깨진 직후의 잔광 세기(0~1)를 나무 그림에 넣는다. 나무 셰이더가 그림 테두리를 배리어와 같은 푸른빛으로
    /// 밝힌다(0이면 그대로). VFX_TreeBarrier가 깨짐 연출 동안 부르고 끝나면 0으로 돌린다. 렌더링 전용이다.
    /// 같은 값이면 아무것도 하지 않으므로, 호출부가 값을 단계로 끊어 넘기면 프로퍼티 블록 쓰기가 그만큼 줄어든다.
    /// </summary>
    public void SetBarrierAfterglow(float _amount)
    {
        if (appliedBarrierAfterglow == _amount) return;

        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        appliedBarrierAfterglow = _amount;
        // 수관과 밑동 모두에 넣는다(밑동은 가늘어서 대부분 테두리 빛으로 밝아진다).
        SetBarrierAfterglowToRenderer(topRenderer, _amount);
        SetBarrierAfterglowToRenderer(bottomRenderer, _amount);
    }

    private void SetBarrierAfterglowToRenderer(SpriteRenderer sr, float _amount)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetFloat(BarrierAfterglowID, _amount);
        CommitBodyBlock(sr, _flashMPB);
    }

    /// <summary>
    /// 2스테이지 포자막 상태(x 깎인 정도, y 번쩍임, z 차오르는 중, w 가득 참 맥동, 각 0~1)를 포자막 그림(Top/Bottom Shield)에만 넣는다.
    /// 나무 셰이더(TreeSporeShield.hlsl)가 포자막 그림의 하늘색 결정 픽셀만 바꾼다(0이면 그대로). StageSporeShieldFx가 부른다. 렌더링 전용이다.
    /// 같은 값이면 아무것도 하지 않는다.
    /// </summary>
    public void SetSporeShieldFx(Vector4 _fx)
    {
        if (appliedSporeShieldFx == _fx) return;

        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        appliedSporeShieldFx = _fx;
        SetSporeShieldFxToRenderer(topShieldRenderer, _fx);
        SetSporeShieldFxToRenderer(bottomShieldRenderer, _fx);
    }

    private void SetSporeShieldFxToRenderer(SpriteRenderer sr, Vector4 _fx)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetVector(SporeShieldFxID, _fx);
        CommitBodyBlock(sr, _flashMPB);
    }

    // TreeFlashSystem이 프레임마다 호출한다(예전 FlashRoutine의 ApplyFlashAmountToRenderers 호출과 동일).
    // _progress는 점멸 진행도(0~1, 끝나면 1)로 치명타 점멸의 색·외곽선 단계에만 쓴다.
    internal void ApplyFlashAmountFromSystem(float _flash, float _progress)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        if (bCritFlashActive)
        {
            // 아래에서 색·점멸량(과 외곽선)을 바로 쓰므로, 그보다 먼저 미뤄 둔 점멸 쓰기는 모두 덮인 값이다.
            deferredFlashKind = DeferredFlashNone;

            // 색과 점멸량을 렌더러마다 한 번의 Get/Set으로 함께 쓴다. 외곽선 단계는 별도 외곽선 렌더러만 건드리므로
            // 쓰는 순서가 바뀌어도 각 렌더러에 남는 값은 예전(색 전부 → 외곽선 → 점멸량 전부)과 같다.
            Color critColor = EvaluateCriticalFlashColor(_progress);
            SetFlashColorAndAmountToRenderer(topRenderer, critColor, _flash);
            SetFlashColorAndAmountToRenderer(bottomRenderer, critColor, _flash);
            SetFlashColorAndAmountToRenderer(topShieldRenderer, critColor, _flash);
            SetFlashColorAndAmountToRenderer(bottomShieldRenderer, critColor, _flash);
            SetFlashColorAndAmountToRenderer(topHighlightRenderer, critColor, _flash);
            SetFlashColorAndAmountToRenderer(bottomHighlightRenderer, critColor, _flash);

            if (_progress >= 1f)
                EndCriticalFlash();
            else
                ApplyCriticalOutlineFlash(_progress);
            return;
        }

        ApplyFlashAmountToRenderers(_flash);
    }

    /// <summary>
    /// TreeFlashSystem이 진행 중인 점멸마다 부른다. ApplyFlashAmountFromSystem(_curve.Evaluate(_progress), _progress)와 같은 결과를 내되,
    /// 켜져 있는 동안은 쓰기를 그리기 직전으로 미루고 곡선 값도 실제로 써넣을 때 구한다(같은 곡선·같은 진행도라 값이 같다).
    /// 치명타 점멸이 끝나는 틱(진행도 1 이상)은 상태를 바꾸므로(EndCriticalFlash) 미루지 않는다. 일반 점멸이 끝나는 틱(TreeFlashSystem이
    /// ApplyFlashAmountFromSystem(0, 1)로 부른다)도 바로 쓴다 - 더 갱신되지 않을 값을 미뤄 두면 화면 밖 나무가 검사 목록에 계속 남는다.
    /// </summary>
    internal void ApplyFlashFromSystem(AnimationCurve _curve, float _progress)
    {
        if (false == bDeferralEnabled || (bCritFlashActive && _progress >= 1f))
        {
            ApplyFlashAmountFromSystem(_curve.Evaluate(_progress), _progress);
            return;
        }

        deferredFlashCurve = _curve;
        deferredFlashProgress = _progress;
        if (bCritFlashActive)
        {
            deferredFlashKind = DeferredFlashCrit;
            // 외곽선 색은 이 시점의 외곽선 상태(켜짐 여부·재질 색)로 정해지므로 지금 구해 두고 쓰기만 미룬다(ApplyCriticalOutlineFlash와 같다).
            bDeferredCritOutline = bCritOutlineShown;
            if (bCritOutlineShown)
            {
                bCritOutlineColorApplied = true;
                deferredCritOutlineTop = EvaluateCriticalOutlineColor(_progress, topOutlineSR);
                deferredCritOutlineBottom = EvaluateCriticalOutlineColor(_progress, bottomOutlineSR);
            }
        }
        else
        {
            deferredFlashKind = DeferredFlashNormal;
        }

        if (deferredWriteIndex < 0) TreeDeferredMaterialWrites.Register(this);
    }

    // 미뤄 둔 점멸 쓰기를 지금 한다. 치명타는 ApplyFlashAmountFromSystem의 진행 중 틱과 같은 값을 같은 렌더러에 쓴다.
    private void FlushDeferredFlash()
    {
        byte kind = deferredFlashKind;
        deferredFlashKind = DeferredFlashNone;
        if (DeferredFlashNone == kind) return;

        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        float flash = deferredFlashCurve.Evaluate(deferredFlashProgress);

        if (DeferredFlashNormal == kind)
        {
            ApplyFlashAmountToRenderers(flash);
            return;
        }

        Color critColor = EvaluateCriticalFlashColor(deferredFlashProgress);
        SetFlashColorAndAmountToRenderer(topRenderer, critColor, flash);
        SetFlashColorAndAmountToRenderer(bottomRenderer, critColor, flash);
        SetFlashColorAndAmountToRenderer(topShieldRenderer, critColor, flash);
        SetFlashColorAndAmountToRenderer(bottomShieldRenderer, critColor, flash);
        SetFlashColorAndAmountToRenderer(topHighlightRenderer, critColor, flash);
        SetFlashColorAndAmountToRenderer(bottomHighlightRenderer, critColor, flash);

        if (bDeferredCritOutline)
        {
            SetOutlineColorToRenderer(topOutlineSR, deferredCritOutlineTop);
            SetOutlineColorToRenderer(bottomOutlineSR, deferredCritOutlineBottom);
        }
    }

    private void SetFlashColorAndAmountToRenderer(SpriteRenderer sr, Color _color, float flash)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetColor(FlashColorID, _color);
        _flashMPB.SetFloat(FlashAmountID, flash);
        CommitBodyBlock(sr, _flashMPB);
    }

    private Color EvaluateCriticalFlashColor(float _progress)
    {
        if (_progress <= criticalHitFlashWhiteHold)
            return Color.white;

        if (_progress <= criticalHitFlashMidPoint)
        {
            float span = criticalHitFlashMidPoint - criticalHitFlashWhiteHold;
            float t = span > 0f ? (_progress - criticalHitFlashWhiteHold) / span : 1f;
            return Color.Lerp(Color.white, criticalHitFlashMidColor, t);
        }

        float restSpan = 1f - criticalHitFlashMidPoint;
        float rest = restSpan > 0f ? Mathf.Clamp01((_progress - criticalHitFlashMidPoint) / restSpan) : 1f;
        return Color.Lerp(criticalHitFlashMidColor, criticalHitFlashColor, rest);
    }

    private void BeginCriticalFlash()
    {
        // 앞선 치명타 점멸의 미뤄 둔 쓰기 가운데 색은 아래 흰색에, 외곽선은 아래 ApplyCriticalOutlineFlash(0)에 덮인다
        // (외곽선을 안 쓰는 나무는 미뤄 둔 외곽선 쓰기가 애초에 없다). 점멸량만 다음 틱까지 남으므로 점멸량 쓰기로 바꿔 둔다.
        if (DeferredFlashCrit == deferredFlashKind) deferredFlashKind = DeferredFlashNormal;

        bCritFlashActive = true;
        ApplyFlashColorToRenderers(Color.white);

        // 외곽선을 쓰지 않는 나무(마을 등 DisableOutline)는 외곽선 번쩍임을 하지 않는다.
        if (bDisableOutline || outlineVisualObj == null)
            return;

        bCritOutlineShown = true;
        if (false == outlineVisualObj.activeSelf)
            outlineVisualObj.SetActive(true);

        ApplyCriticalOutlineFlash(0f);
    }

    // 치명타 점멸 상태를 끝내고 외곽선을 원래(대상이면 켜진 원래 색, 아니면 꺼짐)로 되돌린다. 치명타 점멸 중이 아니면 할 일이 없다.
    private void EndCriticalFlash()
    {
        // 미뤄 둔 치명타 점멸 쓰기가 있으면 순서대로 먼저 쓴 뒤 끝낸다(끝나는 틱은 이미 버리고 들어온다).
        if (DeferredFlashCrit == deferredFlashKind) FlushDeferredFlash();

        bCritFlashActive = false;

        if (bCritOutlineColorApplied)
        {
            bCritOutlineColorApplied = false;
            SetOutlineColorToRenderer(topOutlineSR, GetBaseOutlineColor(topOutlineSR));
            SetOutlineColorToRenderer(bottomOutlineSR, GetBaseOutlineColor(bottomOutlineSR));
        }

        if (bCritOutlineShown)
        {
            bCritOutlineShown = false;
            if (outlineVisualObj != null && outlineVisualObj.activeSelf != isOutlineActive)
                outlineVisualObj.SetActive(isOutlineActive);
        }
    }

    private void ApplyCriticalOutlineFlash(float _progress)
    {
        if (false == bCritOutlineShown)
            return;

        bCritOutlineColorApplied = true;
        SetOutlineColorToRenderer(topOutlineSR, EvaluateCriticalOutlineColor(_progress, topOutlineSR));
        SetOutlineColorToRenderer(bottomOutlineSR, EvaluateCriticalOutlineColor(_progress, bottomOutlineSR));
    }

    private Color EvaluateCriticalOutlineColor(float _progress, SpriteRenderer _sr)
    {
        if (_progress <= criticalOutlineWhiteHold)
            return criticalOutlineFlashColor;

        if (_progress <= criticalOutlineMidPoint)
        {
            float span = criticalOutlineMidPoint - criticalOutlineWhiteHold;
            float t = span > 0f ? (_progress - criticalOutlineWhiteHold) / span : 1f;
            return Color.Lerp(criticalOutlineFlashColor, criticalOutlineMidColor, t);
        }

        float restSpan = 1f - criticalOutlineMidPoint;
        float rest = restSpan > 0f ? Mathf.Clamp01((_progress - criticalOutlineMidPoint) / restSpan) : 1f;

        // 원래 외곽선이 켜져 있던 나무는 원래 색으로, 꺼져 있던 나무는 금색이 흐려지며 사라진다.
        Color end = criticalOutlineMidColor;
        if (isOutlineActive)
            end = GetBaseOutlineColor(_sr);
        else
            end.a = 0f;

        return Color.Lerp(criticalOutlineMidColor, end, rest);
    }

    private static Color GetBaseOutlineColor(SpriteRenderer _sr)
    {
        if (_sr == null) return Color.clear;
        Material mat = _sr.sharedMaterial;
        return mat != null && mat.HasProperty(OutlineColorID) ? mat.GetColor(OutlineColorID) : Color.clear;
    }

    private void SetOutlineColorToRenderer(SpriteRenderer sr, Color _color)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(_flashMPB);
        _flashMPB.SetColor(OutlineColorID, _color);
        CommitBodyBlock(sr, _flashMPB);
    }

    private void ApplyFlashAmountToRenderers(float flash)
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        
        // 미뤄 둔 치명타 점멸 쓰기가 있으면(색·외곽선 포함) 순서대로 먼저 쓰고, 미뤄 둔 일반 점멸량은 지금 쓰는 값에 덮이므로 버린다.
        if (DeferredFlashCrit == deferredFlashKind) FlushDeferredFlash();
        deferredFlashKind = DeferredFlashNone;

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
        CommitBodyBlock(sr, _flashMPB);
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
        CommitBodyBlock(sr, _flashMPB);
    }

    /// <summary>
    /// 렌더러에 프로퍼티 블록을 넣는다. 수관·밑동은 블록 값(BodyBlockFloatIDs·_FlashColor)이 모두 기본 값과 같으면 블록을 비운다.
    /// 셰이더가 받는 값은 같아 그림은 그대로이고, 블록이 없는 렌더러는 SRP Batcher로 그려져 나무마다 드는 그리기 비용이 준다.
    /// 다음에 값을 쓸 때 GetPropertyBlock이 빈 블록을 주므로 비운 값들은 블록에 다시 들어가지 않지만, 모두 재질 값과 같아 결과가 같다.
    /// 실드·하이라이트·별자리·물 위 실드/하이라이트는 블록 값이 모두 지금 끼워진 재질(HDR 사본) 값과 같으면 비운다. 그 밖의 렌더러는 예전처럼 블록을 그대로 넣는다.
    /// </summary>
    private void CommitBodyBlock(SpriteRenderer _sr, MaterialPropertyBlock _block)
    {
        if (IsBodyBlockAtDefaults(_sr, _block))
            _sr.SetPropertyBlock(null);
        else
            _sr.SetPropertyBlock(_block);
    }

    private bool IsBodyBlockAtDefaults(SpriteRenderer _sr, MaterialPropertyBlock _block)
    {
        if (IsHDRMaterialRenderer(_sr)) return IsBlockAtMaterial(_sr, _block);

        bool bTop = _sr == topRenderer;
        if (false == bTop && _sr != bottomRenderer) return false;

        ResolveBodyBlockDefaults();
        float[] defaults = bTop ? topBodyBlockDefaults : bottomBodyBlockDefaults;
        if (null == defaults) return false;

        for (int i = 0; i < BodyBlockFloatIDs.Length; i++)
        {
            int id = BodyBlockFloatIDs[i];
            if (_block.HasFloat(id) && _block.GetFloat(id) != defaults[i]) return false;
        }

        if (_block.HasColor(FlashColorID) && false == _block.GetColor(FlashColorID).Equals(bTop ? topFlashColorDefault : bottomFlashColorDefault)) return false;

        return true;
    }

    // HDR 사본을 끼우는 렌더러(실드·하이라이트·별자리·물 위 실드/하이라이트)인지.
    private bool IsHDRMaterialRenderer(SpriteRenderer _sr)
    {
        return _sr == topShieldRenderer || _sr == bottomShieldRenderer || _sr == topHighlightRenderer || _sr == bottomHighlightRenderer
            || _sr == constellationRenderer || _sr == topShieldOnWaterSR || _sr == topHighlightOnWaterSR;
    }

    // 블록에 들어가는 값들(HDR·번쩍임 양·흔들림·열기, 번쩍임 색, 포자막)이 모두 지금 끼워진 재질 값과 정확히 같은지 본다.
    // 이 렌더러들의 블록에는 이 여섯 값만 들어간다(ApplyHDRToRenderer·SetFlash*ToRenderer·ApplyWindSwayToRenderer·SetHeatChargeToRenderer(하이라이트)·
    // SetSporeShieldFxToRenderer(실드)).
    private static readonly int[] HDRMaterialBlockFloatIDs = { HDRIntensityID, FlashAmountID, EnableWindSwayID, HeatChargeID };

    private static bool IsBlockAtMaterial(SpriteRenderer _sr, MaterialPropertyBlock _block)
    {
        Material material = _sr.sharedMaterial;
        if (null == material) return false;

        for (int i = 0; i < HDRMaterialBlockFloatIDs.Length; i++)
        {
            int id = HDRMaterialBlockFloatIDs[i];
            if (_block.HasFloat(id) && (false == material.HasFloat(id) || material.GetFloat(id) != _block.GetFloat(id))) return false;
        }

        if (_block.HasColor(FlashColorID) && (false == material.HasColor(FlashColorID) || false == material.GetColor(FlashColorID).Equals(_block.GetColor(FlashColorID)))) return false;
        if (_block.HasVector(SporeShieldFxID) && (false == material.HasVector(SporeShieldFxID) || false == material.GetVector(SporeShieldFxID).Equals(_block.GetVector(SporeShieldFxID)))) return false;

        return true;
    }

    // 수관·밑동 블록을 비워도 되는 기준 값을 한 번 정한다. 기본 재질이 값을 모두 가져야 하고, 이 나무가 바꿔 끼우는 보석 재질이
    // 같은 프로퍼티를 가지면 그 값도 같아야 한다(보석 재질로 바뀐 뒤에도 비운 블록이 예전 블록과 같은 값을 넘기게). 하나라도 어긋나면 비우지 않는다.
    private void ResolveBodyBlockDefaults()
    {
        if (bBodyBlockDefaultsResolved) return;
        bBodyBlockDefaultsResolved = true;

        EnsureDefaultMaterialsCached();
        topBodyBlockDefaults = ReadBodyBlockDefaults(defaultTopMaterial, true, out topFlashColorDefault);
        bottomBodyBlockDefaults = ReadBodyBlockDefaults(defaultBottomMaterial, false, out bottomFlashColorDefault);
    }

    private float[] ReadBodyBlockDefaults(Material _material, bool _bTop, out Color _flashColor)
    {
        _flashColor = default;
        if (null == _material || false == _material.HasColor(FlashColorID)) return null;

        float[] values = new float[BodyBlockFloatIDs.Length];
        for (int i = 0; i < BodyBlockFloatIDs.Length; i++)
        {
            int id = BodyBlockFloatIDs[i];
            if (false == _material.HasFloat(id)) return null;
            values[i] = _material.GetFloat(id);
        }
        _flashColor = _material.GetColor(FlashColorID);

        if (null != gemMaterialSets)
        {
            for (int s = 0; s < gemMaterialSets.Length; s++)
            {
                Material gem = _bTop ? gemMaterialSets[s].topMaterial : gemMaterialSets[s].bottomMaterial;
                if (null == gem) continue;

                for (int i = 0; i < BodyBlockFloatIDs.Length; i++)
                {
                    int id = BodyBlockFloatIDs[i];
                    if (gem.HasFloat(id) && gem.GetFloat(id) != values[i]) return null;
                }

                if (gem.HasColor(FlashColorID) && false == gem.GetColor(FlashColorID).Equals(_flashColor)) return null;
            }
        }

        return values;
    }

    // VFX 재생에 필요한 위치/회전/색상 데이터를 외부(InDungeonVFXManager)에 제공한다.
    public Vector3 GetTopRootPosition() => topRoot != null ? topRoot.position : transform.position;
    public Vector3 GetBottomRootPosition() => bottomRoot != null ? bottomRoot.position : transform.position;
    public Quaternion GetTopRootRotation() => topRoot != null ? topRoot.rotation : Quaternion.identity;
    public Quaternion GetBottomRootRotation() => bottomRoot != null ? bottomRoot.rotation : Quaternion.identity;
    public ParticleColorSet GetTopVfxColor() => currentTopVfxColor;
    public ParticleColorSet GetBottomVfxColor() => currentBottomVfxColor;

    /// <summary>
    /// 지진에 쓰러진 나무를 공중으로 튀어 오르게 보이는 대역(QuakeTreeLaunch)이 베낄 그림을 모은다. 읽기만 한다.
    /// _body: 지금 보이는 수관·밑동과 그 위에 겹친 포자막·하이라이트(함께 튀어 오른다). _shadows: 바닥 그림자(바닥에 남는다).
    /// 물 위 반사·외곽선·별자리 표시는 담지 않는다.
    /// </summary>
    public void CollectLaunchRenderers(List<SpriteRenderer> _body, List<SpriteRenderer> _shadows)
    {
        AddIfVisible(_body, bottomRenderer);
        AddIfVisible(_body, bottomShieldRenderer);
        AddIfVisible(_body, bottomHighlightRenderer);
        AddIfVisible(_body, topRenderer);
        AddIfVisible(_body, topShieldRenderer);
        AddIfVisible(_body, topHighlightRenderer);
        AddIfVisible(_shadows, bottomShadowRenderer);
        AddIfVisible(_shadows, topShadowRenderer);
    }

    private static void AddIfVisible(List<SpriteRenderer> _into, SpriteRenderer _renderer)
    {
        if (null == _renderer || false == _renderer.enabled || false == _renderer.gameObject.activeInHierarchy) return;
        if (_renderer.forceRenderingOff || null == _renderer.sprite || _renderer.color.a <= 0f) return;

        _into.Add(_renderer);
    }

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

        // 치명타 점멸 도중 풀로 돌아간 나무(TreeFlashSystem이 조용히 뺀다)의 외곽선 번쩍임을 되돌린다.
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();
        EndCriticalFlash();

        if (visualRoot == null)
        {
            return;
        }

        // 1. 기존 비주얼 루트(전체 쉐이킹·치명타 스케일 등) 초기화
        KillHitPunchTween();
        ResetCritScale();
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

        // 5. 깎인 금·숯불 초기화 (풀에서 다시 꺼낸 새 나무에 남지 않게)
        SetDamageLook(0f, false);

        // 6. 배리어 잔광 초기화
        SetBarrierAfterglow(0f);

        // 7. 포자막 상태 표현 초기화
        SetSporeShieldFx(Vector4.zero);
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

        // 반투명한 동안 수관이 덮는 칸에는 밑동을 그리지 않는다(밑동이 수관을 뚫고 비쳐 보이지 않게).
        UpdateFadeMask(_alpha < FadeMaskAlphaThreshold);
    }

    // ── 반투명 밑동 가림 마스크(TreeFadeMask) ──
    // 이 값보다 투명하면 마스크를 켠다. 불투명(1)일 때는 끄므로 평소 그림·그리기 수는 예전과 같다.
    private const float FadeMaskAlphaThreshold = 0.999f;
    private TreeFadeMask fadeMask;
    // 원본 수관 재질마다 하나씩 만든 마스크 재질(같은 셰이더, 스텐실 64를 찍고 색은 안 씀).
    private static readonly Dictionary<Material, Material> fadeMaskMaterials = new Dictionary<Material, Material>();
    private static readonly int TreeStencilRefID = Shader.PropertyToID("_TreeStencilRef");
    private static readonly int TreeStencilCompID = Shader.PropertyToID("_TreeStencilComp");
    private static readonly int TreeColorMaskID = Shader.PropertyToID("_TreeColorMask");

    private void UpdateFadeMask(bool _bActive)
    {
        if (false == _bActive)
        {
            if (fadeMask != null && fadeMask.gameObject.activeSelf) fadeMask.gameObject.SetActive(false);
            return;
        }

        if (false == Application.isPlaying || topRenderer == null || bottomRenderer == null) return;

        if (fadeMask == null)
        {
            Material maskMaterial = GetFadeMaskMaterial();
            if (maskMaterial == null) return;

            GameObject maskObj = new GameObject("Fade Mask");
            maskObj.layer = topRenderer.gameObject.layer;
            maskObj.transform.SetParent(topRenderer.transform, false);
            SpriteRenderer maskRenderer = maskObj.AddComponent<SpriteRenderer>();
            maskRenderer.sharedMaterial = maskMaterial;
            maskRenderer.renderingLayerMask = topRenderer.renderingLayerMask;
            fadeMask = maskObj.AddComponent<TreeFadeMask>();
            fadeMask.Bind(maskRenderer, topRenderer, bottomRenderer, this);
        }

        if (false == fadeMask.gameObject.activeSelf) fadeMask.gameObject.SetActive(true);
        fadeMask.Sync();
    }

    private Material GetFadeMaskMaterial()
    {
        EnsureDefaultMaterialsCached();
        Material baseMaterial = defaultTopMaterial;
        if (baseMaterial == null || false == baseMaterial.HasProperty(TreeStencilRefID)) return null;

        if (false == fadeMaskMaterials.TryGetValue(baseMaterial, out Material maskMaterial) || maskMaterial == null)
        {
            maskMaterial = new Material(baseMaterial)
            {
                name = baseMaterial.name + " (Fade Mask)",
                hideFlags = HideFlags.DontSave,
            };
            maskMaterial.SetFloat(TreeStencilRefID, 64f);
            maskMaterial.SetFloat(TreeStencilCompID, (float)CompareFunction.Always);
            maskMaterial.SetFloat(TreeColorMaskID, 0f);
            fadeMaskMaterials[baseMaterial] = maskMaterial;
        }
        return maskMaterial;
    }

    // 스프라이트 메시(정점·삼각형) 사본. Sprite.vertices/triangles는 부를 때마다 새 배열을 만들어서 스프라이트마다 한 번만 읽는다.
    private static readonly Dictionary<Sprite, KeyValuePair<Vector2[], ushort[]>> spriteMeshCache = new Dictionary<Sprite, KeyValuePair<Vector2[], ushort[]>>();

    /// <summary>
    /// 수관·밑동 그림이 _occludee(그 렌더러)보다 앞에 그려지면서 월드 좌표 _origin + _offsets[i] 중 하나라도 덮는지.
    /// 덮음 판정은 스프라이트의 실제 메시(그림 둘레를 따라 잘린 메시) 안인지로 하므로 그림 사각형의 투명 여백은 거의 걸리지 않는다.
    /// 렌더러 상태(켜짐·정렬 순서·뒤집기·스프라이트 경계)는 점마다 바뀌지 않으므로 렌더러마다 한 번만 읽는다.
    /// </summary>
    public bool CoversAnyWorldPoint(Vector2 _origin, Vector2[] _offsets, Renderer _occludee)
    {
        int occludeeLayer = 0;
        int occludeeOrder = 0;
        if (_occludee != null)
        {
            occludeeLayer = SortingLayer.GetLayerValueFromID(_occludee.sortingLayerID);
            occludeeOrder = _occludee.sortingOrder;
        }

        CoverProbe top = CoverProbe.Prepare(topRenderer, _occludee != null, occludeeLayer, occludeeOrder);
        CoverProbe bottom = CoverProbe.Prepare(bottomRenderer, _occludee != null, occludeeLayer, occludeeOrder);
        if (false == top.bValid && false == bottom.bValid) return false;

        for (int i = 0; i < _offsets.Length; i++)
        {
            Vector2 worldPoint = _origin + _offsets[i];
            if (top.bValid && top.Covers(worldPoint)) return true;
            if (bottom.bValid && bottom.Covers(worldPoint)) return true;
        }
        return false;
    }

    // 렌더러 하나의 점과 무관한 부분(유효성·정렬·변환·경계)을 미리 읽어 두고 점마다 덮음을 판정한다.
    private struct CoverProbe
    {
        public bool bValid;
        private Transform transform;
        private float z;
        private bool bFlipX;
        private bool bFlipY;
        private float minX, maxX, minY, maxY;
        private Sprite sprite;

        public static CoverProbe Prepare(SpriteRenderer _renderer, bool _bHasOccludee, int _occludeeLayer, int _occludeeOrder)
        {
            CoverProbe probe = default;
            if (_renderer == null || false == _renderer.enabled || false == _renderer.gameObject.activeInHierarchy) return probe;
            Sprite sprite = _renderer.sprite;
            if (sprite == null) return probe;
            if (_bHasOccludee)
            {
                int frontLayer = SortingLayer.GetLayerValueFromID(_renderer.sortingLayerID);
                bool bDrawnAfter = frontLayer != _occludeeLayer ? frontLayer > _occludeeLayer : _renderer.sortingOrder > _occludeeOrder;
                if (false == bDrawnAfter) return probe;
            }

            probe.bValid = true;
            probe.sprite = sprite;
            probe.transform = _renderer.transform;
            probe.z = probe.transform.position.z;
            probe.bFlipX = _renderer.flipX;
            probe.bFlipY = _renderer.flipY;
            Bounds bounds = sprite.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            probe.minX = min.x; probe.maxX = max.x;
            probe.minY = min.y; probe.maxY = max.y;
            return probe;
        }

        public bool Covers(Vector2 _worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(new Vector3(_worldPoint.x, _worldPoint.y, z));
            if (bFlipX) local.x = -local.x;
            if (bFlipY) local.y = -local.y;

            if (local.x < minX || local.x > maxX || local.y < minY || local.y > maxY) return false;

            if (false == spriteMeshCache.TryGetValue(sprite, out KeyValuePair<Vector2[], ushort[]> mesh))
            {
                mesh = new KeyValuePair<Vector2[], ushort[]>(sprite.vertices, sprite.triangles);
                spriteMeshCache[sprite] = mesh;
            }

            Vector2[] vertices = mesh.Key;
            ushort[] triangles = mesh.Value;
            Vector2 p = local;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                if (IsPointInTriangle(p, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]])) return true;
            }
            return false;
        }
    }

    private static bool IsPointInTriangle(Vector2 _p, Vector2 _a, Vector2 _b, Vector2 _c)
    {
        float d1 = (_p.x - _b.x) * (_a.y - _b.y) - (_a.x - _b.x) * (_p.y - _b.y);
        float d2 = (_p.x - _c.x) * (_b.y - _c.y) - (_b.x - _c.x) * (_p.y - _c.y);
        float d3 = (_p.x - _a.x) * (_c.y - _a.y) - (_c.x - _a.x) * (_p.y - _a.y);
        bool bNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool bPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return false == (bNeg && bPos);
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

        // 치명타 외곽선 번쩍임 중에는 번쩍임이 끝날 때까지 켜 두고, 끝날 때(EndCriticalFlash) 이 값으로 맞춘다.
        if (outlineVisualObj != null)
        {
            outlineVisualObj.SetActive(_boolean || bCritOutlineShown);
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
            // 깎인 모습 사본이 있으면 원본 대신 사본을 끼운다(SetDamageLookToRenderer 참고).
            if (topRenderer != null && topDamageMaterial != null && topRenderer.sharedMaterial == defaultTopMaterial) topRenderer.sharedMaterial = topDamageMaterial;
            if (bottomRenderer != null && bottomDamageMaterial != null && bottomRenderer.sharedMaterial == defaultBottomMaterial) bottomRenderer.sharedMaterial = bottomDamageMaterial;
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
        CommitBodyBlock(_renderer, Mpb);
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
        float hdr = _active ? _intensity : 1f;

        // 블록에 넣는 HDR과 같은 값을 가진 재질로 함께 바꿔 끼운다(블록을 비워도 HDR이 그대로 남게).
        if (_renderer == topShieldRenderer) ApplyHDRMaterial(_renderer, ref topShieldBaseMaterial, hdr);
        else if (_renderer == bottomShieldRenderer) ApplyHDRMaterial(_renderer, ref bottomShieldBaseMaterial, hdr);
        else if (_renderer == topHighlightRenderer) ApplyHDRMaterial(_renderer, ref topHighlightBaseMaterial, hdr);
        else if (_renderer == bottomHighlightRenderer) ApplyHDRMaterial(_renderer, ref bottomHighlightBaseMaterial, hdr);
        else if (_renderer == constellationRenderer) ApplyHDRMaterial(_renderer, ref constellationBaseMaterial, hdr);
        else if (_renderer == topShieldOnWaterSR) ApplyHDRMaterial(_renderer, ref topShieldOnWaterBaseMaterial, hdr);
        else if (_renderer == topHighlightOnWaterSR) ApplyHDRMaterial(_renderer, ref topHighlightOnWaterBaseMaterial, hdr);

        _renderer.GetPropertyBlock(Mpb);
        Mpb.SetFloat(HDRIntensityID, hdr);
        CommitBodyBlock(_renderer, Mpb);
    }

    private static void ApplyHDRMaterial(SpriteRenderer _renderer, ref Material _baseMaterial, float _hdr)
    {
        if (null == _baseMaterial) _baseMaterial = _renderer.sharedMaterial;
        if (null == _baseMaterial || false == _baseMaterial.HasFloat(HDRIntensityID)) return;

        Material target = _baseMaterial.GetFloat(HDRIntensityID) == _hdr ? _baseMaterial : GetHDRMaterial(_baseMaterial, _hdr);
        if (_renderer.sharedMaterial != target) _renderer.sharedMaterial = target;
    }

    private static Material GetHDRMaterial(Material _baseMaterial, float _hdr)
    {
        if (false == hdrMaterialCache.TryGetValue(_baseMaterial, out Dictionary<float, Material> byHdr))
        {
            byHdr = new Dictionary<float, Material>();
            hdrMaterialCache.Add(_baseMaterial, byHdr);
        }

        if (false == byHdr.TryGetValue(_hdr, out Material material) || null == material)
        {
            material = new Material(_baseMaterial) { name = _baseMaterial.name + " (HDR)", hideFlags = HideFlags.DontSave };
            material.SetFloat(HDRIntensityID, _hdr);
            byHdr[_hdr] = material;
        }

        return material;
    }

    private void UpdateHDRStates()
    {
        if (topHighlightRenderer != null) ApplyHDRToRenderer(topHighlightRenderer, topHighlightRenderer.sprite != null, highlightHDRIntensity);
        if (bottomHighlightRenderer != null) ApplyHDRToRenderer(bottomHighlightRenderer, bottomHighlightRenderer.sprite != null, highlightHDRIntensity);

        if (topShieldRenderer != null) ApplyHDRToRenderer(topShieldRenderer, isShieldActive && topShieldRenderer.sprite != null, shieldHDRIntensity);
        if (bottomShieldRenderer != null) ApplyHDRToRenderer(bottomShieldRenderer, isShieldActive && bottomShieldRenderer.sprite != null, shieldHDRIntensity);

        if (topShieldOnWaterSR != null) ApplyHDRToRenderer(topShieldOnWaterSR, isOnWaterActive && isShieldActive && topShieldOnWaterSR.sprite != null, shieldHDRIntensity + 0.25f);
        if (topHighlightOnWaterSR != null) ApplyHDRToRenderer(topHighlightOnWaterSR, isOnWaterActive && topHighlightOnWaterSR.sprite != null, highlightHDRIntensity + 0.25f);

        if (constellationRenderer != null) ApplyHDRToRenderer(constellationRenderer, constellationRenderer.gameObject.activeSelf && constellationRenderer.sprite != null, 0f < constellationHDROverride ? constellationHDROverride : highlightHDRIntensity);
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

