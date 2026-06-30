using UnityEngine;

public class ShockWaveVisualComponent : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float visualRangeMultiplier = 3f;
    [SerializeField] private float visualExtraDuration = 0.1f;
    [SerializeField] private float visualFadeOutDuration = 0.15f;
    [SerializeField] private float visualStartThickness = 0.025f;
    [SerializeField] private float visualAngleMultiplier = 0.75f;
    [SerializeField] private float shockWaveScaleFactor = 2f;

    private ShockWave shockWave;
    private SpriteRenderer sourceRenderer;
    private Quaternion initialRotation;

    public void Initialize(ShockWave _shockWave)
    {
        shockWave = _shockWave;
        sourceRenderer = GetComponent<SpriteRenderer>();
        initialRotation = transform.rotation;

        if (sourceRenderer != null)
        {
            sourceRenderer.enabled = false;
        }

    }

    public void Play(Transform _origin, float _moveSpeed, float _duration)
    {
        if (shockWave == null)
        {
            shockWave = GetComponent<ShockWave>();
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (sourceRenderer != null)
        {
            sourceRenderer.enabled = false;
        }

        ShockWaveVisualRunner visualRunner = CreateVisualRunner();

        if (visualRunner == null || shockWave == null) return;

        visualRunner.Play(new ShockWaveVisualRunner.PlayData
        {
            Owner = transform,
            Origin = _origin,
            InitialRotation = initialRotation,
            StartPosition = transform.position,
            Sprite = sourceRenderer != null ? sourceRenderer.sprite : null,
            Material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null,
            Color = sourceRenderer != null ? sourceRenderer.color : Color.white,
            SortingLayerID = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0,
            SortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder : 0,
            MoveSpeed = _moveSpeed,
            Duration = _duration,
            ScaleFactor = shockWaveScaleFactor,
            MaxDist = shockWave.maxDist,
            FindRange = shockWave.findRange,
            Angle = shockWave.angle,
            VisualRangeMultiplier = visualRangeMultiplier,
            VisualExtraDuration = visualExtraDuration,
            VisualFadeOutDuration = visualFadeOutDuration,
            VisualStartThickness = visualStartThickness,
            VisualAngleMultiplier = visualAngleMultiplier
        });
    }

    private ShockWaveVisualRunner CreateVisualRunner()
    {
        GameObject visualObject = new GameObject("ShockWaveArcVisual");
        DontDestroyOnLoad(visualObject);
        return visualObject.AddComponent<ShockWaveVisualRunner>();
    }
}

public class ShockWaveVisualRunner : MonoBehaviour
{
    public struct PlayData
    {
        public Transform Owner;
        public Transform Origin;
        public Quaternion InitialRotation;
        public Vector3 StartPosition;
        public Sprite Sprite;
        public Material Material;
        public Color Color;
        public int SortingLayerID;
        public int SortingOrder;
        public float MoveSpeed;
        public float Duration;
        public float ScaleFactor;
        public float MaxDist;
        public float FindRange;
        public float Angle;
        public float VisualRangeMultiplier;
        public float VisualExtraDuration;
        public float VisualFadeOutDuration;
        public float VisualStartThickness;
        public float VisualAngleMultiplier;
    }

    private const float SpriteRadius = 4f;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private PlayData data;
    private float timer;
    private float visualFullRadius;
    private Vector2 cachedDirection = Vector2.right;
    private bool isPlaying;

    private static readonly int MinRadiusID = Shader.PropertyToID("_MinRadius");
    private static readonly int MaxRadiusID = Shader.PropertyToID("_MaxRadius");
    private static readonly int AngleID = Shader.PropertyToID("_Angle");
    private static readonly int AttackDirID = Shader.PropertyToID("_AttackDir");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    private static readonly int TrailTimeID = Shader.PropertyToID("_TrailTime");

    public void Play(PlayData _data)
    {
        EnsureRenderer();

        data = _data;
        timer = 0f;
        isPlaying = true;
        gameObject.SetActive(true);

        spriteRenderer.sprite = data.Sprite;
        spriteRenderer.sharedMaterial = data.Material;
        spriteRenderer.color = data.Color;
        spriteRenderer.sortingLayerID = data.SortingLayerID;
        spriteRenderer.sortingOrder = data.SortingOrder;
        spriteRenderer.enabled = data.Sprite != null && data.Material != null;

        CacheVisualRange();
        UpdateCachedDirection();
        UpdateTransform();
        UpdateShaderProperties();
    }

    private void Awake()
    {
        EnsureRenderer();
        gameObject.SetActive(false);
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void CacheVisualRange()
    {
        float distanceAtEnd = data.MoveSpeed * Mathf.Max(data.Duration, 0f);
        float finalScaleMultiplier = 1f + distanceAtEnd * data.ScaleFactor;
        float visualMultiplier = Mathf.Max(data.VisualRangeMultiplier, 0.0001f);
        float finalRadius = Mathf.Max(data.FindRange, data.MaxDist) * finalScaleMultiplier;
        visualFullRadius = Mathf.Max(finalRadius * visualMultiplier, 0.0001f);
    }

    private void Update()
    {
        if (isPlaying == false) return;

        timer += Time.deltaTime;

        UpdateCachedDirection();
        UpdateTransform();
        UpdateShaderProperties();

        if (timer >= GetVisualDuration())
        {
            Stop();
        }
    }

    private void UpdateCachedDirection()
    {
        if (data.Owner == null) return;

        Quaternion directionRotation = data.Owner.rotation * Quaternion.Inverse(data.InitialRotation);
        Vector3 direction = directionRotation * Vector3.right;
        Vector2 visualDirection = new Vector2(direction.x, direction.y * 2f).normalized;
        if (visualDirection.sqrMagnitude >= 0.0001f)
        {
            cachedDirection = visualDirection;
        }
    }

    private void UpdateTransform()
    {
        transform.position = data.Origin != null ? data.Origin.position : data.StartPosition;
        transform.rotation = Quaternion.identity;

        float scale = visualFullRadius / SpriteRadius;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void UpdateShaderProperties()
    {
        if (spriteRenderer == null || spriteRenderer.enabled == false) return;

        float maxRadius = Mathf.Max(
            Mathf.Clamp01(data.VisualStartThickness),
            Mathf.Clamp01(timer / Mathf.Max(data.Duration, 0.0001f)));
        float minProgress = Mathf.Clamp01(timer / Mathf.Max(GetVisualDuration(), 0.0001f));
        float minRadius = Mathf.SmoothStep(0f, 1f, minProgress);
        minRadius = Mathf.Min(minRadius, maxRadius);

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(MinRadiusID, minRadius);
        propertyBlock.SetFloat(MaxRadiusID, maxRadius);
        propertyBlock.SetFloat(AngleID, data.Angle * Mathf.Max(data.VisualAngleMultiplier, 0f));
        propertyBlock.SetVector(AttackDirID, cachedDirection);
        propertyBlock.SetFloat(AlphaID, GetAlpha());
        propertyBlock.SetFloat(TrailTimeID, timer);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    private float GetVisualDuration()
    {
        return data.Duration + Mathf.Max(data.VisualExtraDuration, 0f);
    }

    private float GetAlpha()
    {
        float fadeDuration = Mathf.Max(data.VisualFadeOutDuration, 0.0001f);
        float visualDuration = GetVisualDuration();
        float fadeStartTime = Mathf.Max(0f, visualDuration - fadeDuration);
        if (timer <= fadeStartTime) return 1f;

        float fadeProgress = Mathf.InverseLerp(fadeStartTime, visualDuration, timer);
        return 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
    }

    private void Stop()
    {
        isPlaying = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        Destroy(gameObject);
    }
}
