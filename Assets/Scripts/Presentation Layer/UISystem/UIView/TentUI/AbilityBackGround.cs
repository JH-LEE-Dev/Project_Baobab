using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class AbilityBackGround : MonoBehaviour
{
    private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
    private static readonly int RectSizeId = Shader.PropertyToID("_RectSize");
    private static readonly int PivotId = Shader.PropertyToID("_Pivot");
    private static readonly int OffsetId = Shader.PropertyToID("_AbilityTileBGOffset");

    private static readonly Vector2[] DiagonalDirections =
    {
        new Vector2(-1f, 1f),
        new Vector2(-1f, -1f),
        new Vector2(1f, 1f),
        new Vector2(1f, -1f)
    };

    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private Vector2 tileSize = new Vector2(128f, 128f);
    [SerializeField, Min(0f)] private float speedPixelsPerSecond = 12f;

    private Image image;
    private RectTransform rectTransform;
    private Material runtimeMaterial;
    private Vector2 direction;
    private Vector2 offset;
    private float elapsedTime;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        CacheComponents();
        ApplyRuntimeMaterial();
        PickRandomDirection();
        elapsedTime = 0f;
        offset = Vector2.zero;
        ApplyStaticProperties();
        ApplyOffset();
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;

        float pixelDistance = Mathf.Floor(elapsedTime * speedPixelsPerSecond);
        offset.x = RepeatPixel(direction.x * pixelDistance, tileSize.x);
        offset.y = RepeatPixel(direction.y * pixelDistance, tileSize.y);

        ApplyStaticProperties();
        ApplyOffset();
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(OffsetId, Vector4.zero);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }

    private void CacheComponents()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }

    private void ApplyRuntimeMaterial()
    {
        if (backgroundMaterial == null || image == null)
            return;

        if (runtimeMaterial == null)
            runtimeMaterial = new Material(backgroundMaterial);

        image.material = runtimeMaterial;
    }

    private void PickRandomDirection()
    {
        direction = DiagonalDirections[Random.Range(0, DiagonalDirections.Length)];
    }

    private void ApplyStaticProperties()
    {
        if (runtimeMaterial == null || rectTransform == null)
            return;

        Rect rect = rectTransform.rect;
        runtimeMaterial.SetVector(TileSizeId, new Vector4(Mathf.Max(1f, tileSize.x), Mathf.Max(1f, tileSize.y), 0f, 0f));
        runtimeMaterial.SetVector(RectSizeId, new Vector4(rect.width, rect.height, 0f, 0f));
        runtimeMaterial.SetVector(PivotId, new Vector4(rectTransform.pivot.x, rectTransform.pivot.y, 0f, 0f));
    }

    private void ApplyOffset()
    {
        Shader.SetGlobalVector(OffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
    }

    private static float RepeatPixel(float value, float length)
    {
        if (length <= 0f)
            return 0f;

        return Mathf.Repeat(value, length);
    }
}
