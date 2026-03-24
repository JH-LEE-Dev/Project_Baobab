using DG.Tweening;
using UnityEngine;

public class TreeVisualComponent : MonoBehaviour
{
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
    [SerializeField] private Sprite[] topSprites = new Sprite[3];
    [SerializeField] private Sprite[] bottomSprites = new Sprite[3];

    [Header("Default Tint")]
    [SerializeField] private Color32 topBrightColor = new(53, 204, 92, 255);
    [SerializeField] private Color32 bottomBrightColor = new(132, 102, 36, 255);
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

    private Vector3 topRendererBaseLocalPosition;
    private Quaternion topRendererBaseLocalRotation;
    private float swayPhase;

    public void Initialize()
    {
        CacheSwayBasePose();
        ResetVisualState();
    }

    public void ApplyVisual(TreeData _treeData)
    {
        ApplyRandomVisual();
    }

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

    [ContextMenu("Refresh Visual Preview")]
    public void RefreshVisualPreview()
    {
        ApplyRandomVisual();
        ResetVisualState();
    }

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

    // 나무의 색상을 바꿔준다.
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

    private void ApplyRandomVisual()
    {
        SetRandomSprite(bottomRenderer, bottomSprites);
        SetRandomSprite(topRenderer, topSprites);
        ApplyColors();
        SyncShadowSprite();
        CacheSwayBasePose();
        ResetTopSway();
    }

    private static void SetRandomSprite(SpriteRenderer _renderer, Sprite[] _sprites)
    {
        if (_renderer == null || _sprites == null || _sprites.Length == 0)
        {
            return;
        }

        _renderer.sprite = _sprites[Random.Range(0, _sprites.Length)];
    }

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

    private void OnValidate()
    {
        if (Application.isPlaying || !previewInEditor)
        {
            return;
        }

        RefreshVisualPreview();
    }

    private void Awake()
    {
        CacheSwayBasePose();
    }

    private void Update()
    {
        ApplyWindSway();
    }

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

    private void ResetTopSway()
    {
        if (topRenderer == null)
        {
            return;
        }

        topRenderer.transform.localPosition = topRendererBaseLocalPosition;
        topRenderer.transform.localRotation = topRendererBaseLocalRotation;
    }
}
