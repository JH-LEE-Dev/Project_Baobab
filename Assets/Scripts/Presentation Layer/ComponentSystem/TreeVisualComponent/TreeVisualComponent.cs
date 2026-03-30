using DG.Tweening;
using UnityEngine;

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

    [Header("Sprite Variations")]
    [SerializeField] private Sprite[] topSprites;
    [SerializeField] private Sprite[] bottomSprites;

    [Header("Default Tint")]
    [SerializeField] private Color32 topBrightColor = new Color32(53, 204, 92, 255);
    [SerializeField] private Color32 bottomBrightColor = new Color32(132, 102, 36, 255);
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.8f;

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

    private Vector3 topRendererBaseLocalPosition;
    private Quaternion topRendererBaseLocalRotation;
    private float swayPhase;

    #endregion

    #region Unity Events

    // 플레이 시작 시 바람 흔들림의 기준이 되는 상단 스프라이트 기본 포즈를 저장한다.
    private void Awake()
    {
        CacheSwayBasePose();
    }

    // 매 프레임 상단 수관에 아주 약한 바람 흔들림을 적용한다.
    private void Update()
    {
        ApplyWindSway();
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

    public void Initialize()
    {
        CacheSwayBasePose();
        ResetVisualState();
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

    // 트리 데이터가 적용될 때 스프라이트 조합과 색상 바리에이션을 새로 구성한다.
    public void ApplyVisual(TreeData _treeData)
    {
        ApplyRandomVisual();
    }

    // 상단/하단 스프라이트를 랜덤으로 고르고 색상과 그림자 비주얼까지 함께 갱신한다.
    private void ApplyRandomVisual()
    {
        SetRandomSprite(bottomRenderer, bottomSprites);
        SetRandomSprite(topRenderer, topSprites);
        ApplyColors();
        SyncShadowSprite();
        CacheSwayBasePose();
        ResetTopSway();
    }

    // 상단/하단 스프라이트에 밝기 편차를 줘서 개체마다 미묘한 색 차이를 만든다.
    private void ApplyColors()
    {
        if (topRenderer != null)
        {
            topRenderer.color = GetRandomTint(topBrightColor);
        }

        if (bottomRenderer != null)
        {
            bottomRenderer.color = GetRandomTint(bottomBrightColor);
        }
    }

    // 그림자 렌더러가 본체와 같은 스프라이트와 색상을 따라가도록 동기화한다.
    private void SyncShadowSprite()
    {
        if (topShadowRenderer != null && topRenderer != null)
        {
            topShadowRenderer.sprite = topRenderer.sprite;
            topShadowRenderer.color = topRenderer.color;
        }

        if (bottomShadowRenderer != null && bottomRenderer != null)
        {
            bottomShadowRenderer.sprite = bottomRenderer.sprite;
            bottomShadowRenderer.color = bottomRenderer.color;
        }
    }

    // 전달받은 렌더러에 스프라이트 배열 중 하나를 무작위로 적용한다.
    private static void SetRandomSprite(SpriteRenderer _renderer, Sprite[] _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Length == 0)
        {
            return;
        }

        _renderer.sprite = _sprites[Random.Range(0, _sprites.Length)];
    }

    // 기준 색상에서 밝기만 살짝 달라진 틴트를 만들어 자연스러운 개체 차이를 만든다.
    private Color GetRandomTint(Color32 _brightColor)
    {
        float brightness = Random.Range(minBrightness, 1f);
        return new Color(
            (_brightColor.r / 255f) * brightness,
            (_brightColor.g / 255f) * brightness,
            (_brightColor.b / 255f) * brightness,
            _brightColor.a / 255f
        );
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
    private void CacheSwayBasePose()
    {
        if (topRenderer == null)
        {
            return;
        }

        topRendererBaseLocalPosition = topRenderer.transform.localPosition;
        topRendererBaseLocalRotation = topRenderer.transform.localRotation;
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // 느린 큰 파형과 빠른 작은 파형을 섞어 나무 윗부분만 자연스럽게 흔들리게 만든다.
    private void ApplyWindSway()
    {
        if (!Application.isPlaying || !enableWindSway || topRenderer == null)
        {
            return;
        }

        float time = Time.time;
        float mainWave = Mathf.Sin((time * swayMainSpeed) + swayPhase);
        float detailWave = Mathf.Sin((time * swayDetailSpeed) + (swayPhase * 1.73f)) * swayDetailWeight;
        float sway = mainWave + detailWave;

        Transform topTransform = topRenderer.transform;
        topTransform.localPosition = topRendererBaseLocalPosition + new Vector3(sway * swayPositionAmplitude, 0f, 0f);
        topTransform.localRotation = topRendererBaseLocalRotation * Quaternion.Euler(0f, 0f, -sway * swayRotationAmplitude);
    }

    // 바람 흔들림을 제거하고 상단 스프라이트를 저장된 기본 포즈로 되돌린다.
    private void ResetTopSway()
    {
        if (topRenderer == null)
        {
            return;
        }

        topRenderer.transform.localPosition = topRendererBaseLocalPosition;
        topRenderer.transform.localRotation = topRendererBaseLocalRotation;
    }

    #endregion
}
