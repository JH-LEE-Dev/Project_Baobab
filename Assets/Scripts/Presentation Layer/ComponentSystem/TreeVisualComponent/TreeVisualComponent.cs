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

    public void Initialize()
    {
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
}
