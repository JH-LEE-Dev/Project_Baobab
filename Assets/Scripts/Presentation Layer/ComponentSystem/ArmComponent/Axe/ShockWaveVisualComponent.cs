using System;
using System.Collections.Generic;
using UnityEngine;

public class ShockWaveVisualComponent : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float visualRangeMultiplier = 3f;
    [SerializeField] private float visualExtraDuration = 0.1f;
    [SerializeField] private float visualFadeOutDuration = 0.15f;
    [SerializeField] private float visualStartThickness = 0.025f;
    [SerializeField] private float visualAngleMultiplier = 1f;
    // 효과 레이어 재질(일반 충격파 ShockWaveFx: 발밑 충격·흙먼지·소멸 흩날림 / 과열 충격파 OverheatShockWaveFx: 점화·마루 불꽃 혀·잔불·불씨). 비어 있으면 그리지 않는다.
    [SerializeField] private Material fxMaterial;

    private ShockWave shockWave;
    private SpriteRenderer sourceRenderer;
    private Quaternion initialRotation;
    // 정적 메서드 그룹도 델리게이트 변환마다 할당되므로(C# 9) 한 번만 만들어 둔다.
    private static readonly Action<ShockWaveVisualRunner> cachedReturnVisualRunner = ReturnVisualRunner;

    public virtual void Initialize(ShockWave _shockWave)
    {
        shockWave = _shockWave;
        sourceRenderer = GetComponent<SpriteRenderer>();
        initialRotation = transform.rotation;

        if (sourceRenderer != null)
        {
            sourceRenderer.enabled = false;
        }

    }

    public virtual void Play(float _duration)
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

        ShockWaveVisualRunner visualRunner = GetVisualRunner();

        if (visualRunner == null || shockWave == null) return;

        Color color = sourceRenderer != null ? sourceRenderer.color : Color.white;
        // [로그라이크] 포자 충격파는 초록 충격파다. 본편은 bShockWaveTinted가 false라 원래 색 그대로다.
        if (RunCombatOverrides.bShockWaveTinted) color *= RunCombatOverrides.ShockWaveTint;

        visualRunner.SetOnStopped(cachedReturnVisualRunner);
        visualRunner.Play(new ShockWaveVisualRunner.PlayData
        {
            Owner = transform,
            Origin = shockWave.VisualOrigin,
            InitialRotation = initialRotation,
            StartPosition = transform.position,
            Sprite = sourceRenderer != null ? sourceRenderer.sprite : null,
            Material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null,
            Color = color,
            SortingLayerID = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0,
            SortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder : 0,
            ExpandSpeed = shockWave.EffectiveExpandSpeed,
            Duration = _duration,
            InitialMinDist = shockWave.minDist,
            MaxDist = shockWave.maxDist,
            FindRange = shockWave.findRange,
            Angle = shockWave.angle,
            VisualRangeMultiplier = visualRangeMultiplier,
            VisualExtraDuration = visualExtraDuration,
            VisualFadeOutDuration = visualFadeOutDuration,
            VisualStartThickness = visualStartThickness,
            VisualAngleMultiplier = visualAngleMultiplier,
            TrailSeed = UnityEngine.Random.Range(0f, 1000f),
            FxMaterial = fxMaterial
        });
    }

    // 도끼를 휘두를 때마다 GameObject를 새로 만들고 파괴하지 않도록, 재생이 끝난 러너를
    // 공용 풀에 담아두고 다시 꺼내 쓴다. 비주얼 러너는 ShockWave 본체(오브젝트 풀)와 별개로,
    // 본체가 풀로 돌아간 뒤에도 페이드아웃이 끝날 때까지 독립적으로 살아있어야 하므로
    // 본체 풀에 묶지 않고 이 전용 풀에서 따로 관리한다.
    private static readonly Stack<ShockWaveVisualRunner> runnerPool = new Stack<ShockWaveVisualRunner>();

    private ShockWaveVisualRunner GetVisualRunner()
    {
        while (runnerPool.Count > 0)
        {
            ShockWaveVisualRunner pooled = runnerPool.Pop();
            if (pooled != null) return pooled; // 씬 전환 등으로 파괴된 참조는 건너뛴다
        }

        GameObject visualObject = new GameObject("ShockWaveArcVisual");
        DontDestroyOnLoad(visualObject);
        return visualObject.AddComponent<ShockWaveVisualRunner>();
    }

    private static void ReturnVisualRunner(ShockWaveVisualRunner _runner)
    {
        if (_runner == null) return;
        runnerPool.Push(_runner);
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
        public float ExpandSpeed;
        public float Duration;
        public float InitialMinDist;
        public float MaxDist;
        public float FindRange;
        public float Angle;
        public float VisualRangeMultiplier;
        public float VisualExtraDuration;
        public float VisualFadeOutDuration;
        public float VisualStartThickness;
        public float VisualAngleMultiplier;
        public float TrailSeed;
        public Material FxMaterial;
    }

    private const float SpriteRadius = 4f;
    private const float PixelsPerUnit = 32f;
    // 잔상 파동: 본체 앞 가장자리를 이만큼 늦게, 조금 안쪽에서 따라온다.
    private const float EchoLag = 0.04f;
    // 효과 레이어(소멸 흩날림)는 판정이 끝난 뒤 이만큼 더 그린다(가장 늦게 생긴 티끌이 사라지는 때: 0.08 + 0.18 + 0.08초).
    private const float FxTailSeconds = 0.34f;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private MeshRenderer fxRenderer;
    private Transform fxTransform;
    private MaterialPropertyBlock fxPropertyBlock;
    private float trailOffset;
    private static Mesh fxQuadMesh;
    private PlayData data;
    private float timer;
    private float visualFullRadius;
    private Vector2 cachedDirection = Vector2.right;
    private bool isPlaying;
    private Action<ShockWaveVisualRunner> onStopped; // 재생 종료 시 공용 풀로 자신을 되돌리는 콜백

    private static readonly int MinRadiusID = Shader.PropertyToID("_MinRadius");
    private static readonly int MaxRadiusID = Shader.PropertyToID("_MaxRadius");
    private static readonly int AngleID = Shader.PropertyToID("_Angle");
    private static readonly int AttackDirID = Shader.PropertyToID("_AttackDir");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    private static readonly int TrailTimeID = Shader.PropertyToID("_TrailTime");
    private static readonly int TrailSeedID = Shader.PropertyToID("_TrailSeed");
    private static readonly int TrailOffsetID = Shader.PropertyToID("_TrailOffset");
    private static readonly int CenterID = Shader.PropertyToID("_Center");
    private static readonly int EchoID = Shader.PropertyToID("_Echo");
    private static readonly int FxGeoID = Shader.PropertyToID("_Geo");
    private static readonly int FxDirID = Shader.PropertyToID("_Dir");
    private static readonly int FxTimeID = Shader.PropertyToID("_T");
    private static readonly int FxSeedID = Shader.PropertyToID("_Seed");
    private static readonly int FxTintID = Shader.PropertyToID("_Tint");

    public void SetOnStopped(Action<ShockWaveVisualRunner> _onStopped)
    {
        onStopped = _onStopped;
    }

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
        trailOffset = data.Material != null && data.Material.HasProperty(TrailOffsetID) ? data.Material.GetFloat(TrailOffsetID) : 0f;

        // 효과 레이어는 재질이 있을 때만 그린다. 풀을 일반·과열 충격파가 같이 써서 매번 켜고 끈다.
        bool bFx = spriteRenderer.enabled && data.FxMaterial != null;
        if (bFx) EnsureFxRenderer();
        if (fxRenderer != null)
        {
            fxRenderer.enabled = bFx;
            if (bFx)
            {
                fxRenderer.sharedMaterial = data.FxMaterial;
                fxRenderer.sortingLayerID = data.SortingLayerID;
                fxRenderer.sortingOrder = data.SortingOrder + 1;
            }
        }

        CacheVisualRange();
        UpdateCachedDirection();
        UpdateTransform();
        UpdateShaderProperties();
        UpdateFxProperties();
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

    private void EnsureFxRenderer()
    {
        if (fxRenderer != null) return;

        if (fxQuadMesh == null)
        {
            fxQuadMesh = new Mesh { name = "ShockWaveFxQuad" };
            fxQuadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            fxQuadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            fxQuadMesh.RecalculateBounds();
        }

        GameObject fxObject = new GameObject("ShockWaveFx");
        fxTransform = fxObject.transform;
        fxTransform.SetParent(transform, false);
        fxObject.AddComponent<MeshFilter>().sharedMesh = fxQuadMesh;
        fxRenderer = fxObject.AddComponent<MeshRenderer>();
        fxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fxRenderer.receiveShadows = false;
        fxRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        fxRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        fxRenderer.enabled = false;
        fxPropertyBlock = new MaterialPropertyBlock();
    }

    private bool IsFxActive => fxRenderer != null && fxRenderer.enabled;

    private void CacheVisualRange()
    {
        float distanceAtEnd = data.ExpandSpeed * Mathf.Max(data.Duration, 0f);
        float visualMultiplier = Mathf.Max(data.VisualRangeMultiplier, 0.0001f);
        float finalRadius = Mathf.Max(data.FindRange, data.MaxDist) + distanceAtEnd;
        visualFullRadius = Mathf.Max(finalRadius * visualMultiplier, 0.0001f);
    }

    private void Update()
    {
        if (isPlaying == false) return;

        timer += Time.deltaTime;

        // 방향은 Play에서 한 번만 정한다. 충격파 본체(Owner)는 판정이 끝나면 풀로 돌아가 다음 발사에 다시 쓰이므로,
        // 여기서 매 프레임 다시 읽으면 아직 사라지는 중인 이 연출이 새 충격파 방향으로 돌아간다.
        // (본체 방향은 발사 전 SetDirection으로만 정해지고 날아가는 동안에는 바뀌지 않는다.)
        UpdateTransform();

        // 본체는 예전과 같은 시간에 사라지고, 효과 레이어(흙먼지·소멸 흩날림)는 조금 더 남는다.
        if (timer >= GetVisualDuration() && spriteRenderer != null) spriteRenderer.enabled = false;
        UpdateShaderProperties();
        UpdateFxProperties();

        if (timer >= GetTotalDuration())
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
        bool bFollowOrigin = data.Origin != null && data.Origin != data.Owner;
        transform.position = bFollowOrigin ? data.Origin.position : data.StartPosition;
        transform.rotation = Quaternion.identity;

        float scale = visualFullRadius / SpriteRadius;
        transform.localScale = new Vector3(scale, scale, 1f);

        if (IsFxActive)
        {
            // 원점에 깔리는 쿼드. 파동 끝(그림 반지름) 밖으로 튀는 흙먼지와 흩날리는 티끌까지 덮는다(부모 크기를 되돌려 월드 크기로 맞춘다).
            fxTransform.localPosition = Vector3.zero;
            fxTransform.localRotation = Quaternion.identity;
            fxTransform.localScale = new Vector3((visualFullRadius * 2.8f + 3f) / scale, (visualFullRadius * 1.6f + 3f) / scale, 1f);
        }
    }

    // 시각 _time의 본체 안쪽·바깥 반경(그림 반지름으로 정규화). 판정 시간 동안 퍼지고, 그 뒤에는 안쪽이 바깥을 따라잡아 얇아진다.
    private Vector2 GetRadii(float _time)
    {
        float rangeTimer = Mathf.Min(_time, data.Duration);
        float expandDistance = data.ExpandSpeed * rangeTimer;
        float minRadius = (data.InitialMinDist + expandDistance) / visualFullRadius;
        float maxRadius = (data.MaxDist + expandDistance) / visualFullRadius;

        if (_time > data.Duration)
        {
            float fadeProgress = Mathf.InverseLerp(data.Duration, GetVisualDuration(), _time);
            minRadius = Mathf.Lerp(minRadius, maxRadius, Mathf.SmoothStep(0f, 1f, fadeProgress));
        }

        maxRadius = Mathf.Max(Mathf.Clamp01(data.VisualStartThickness), Mathf.Clamp01(maxRadius));
        minRadius = Mathf.Clamp01(Mathf.Min(minRadius, maxRadius));
        return new Vector2(minRadius, maxRadius);
    }

    private void UpdateShaderProperties()
    {
        if (spriteRenderer == null || spriteRenderer.enabled == false) return;

        Vector2 radii = GetRadii(timer);
        float minRadius = radii.x;
        float maxRadius = radii.y;
        float alpha = GetAlpha();

        // 잔상 파동: EchoLag 전 본체 앞 가장자리를 조금 안쪽에서, 본체 두께의 절반쯤으로 따라온다.
        float echoTime = timer - EchoLag;
        Vector4 echo = Vector4.zero;
        if (echoTime > 0f)
        {
            float mainThickness = maxRadius - minRadius + trailOffset;
            float echoOuter = GetRadii(echoTime).y * 0.985f - 0.05f;
            float echoInner = echoOuter - (mainThickness * 0.45f - 0.05f) * 1.3f;
            if (echoOuter > 0f)
            {
                echo = new Vector4(echoInner, echoOuter, 1f, Mathf.Clamp01((1f - alpha) * 1.3f + 0.15f));
            }
        }

        Vector3 center = transform.position;

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(MinRadiusID, minRadius);
        propertyBlock.SetFloat(MaxRadiusID, maxRadius);
        propertyBlock.SetFloat(AngleID, data.Angle * 2f * Mathf.Max(data.VisualAngleMultiplier, 0f));
        propertyBlock.SetVector(AttackDirID, cachedDirection);
        propertyBlock.SetFloat(AlphaID, alpha);
        propertyBlock.SetFloat(TrailTimeID, timer);
        propertyBlock.SetFloat(TrailSeedID, data.TrailSeed);
        propertyBlock.SetVector(CenterID, new Vector4(center.x, center.y, visualFullRadius, PixelsPerUnit));
        propertyBlock.SetVector(EchoID, echo);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateFxProperties()
    {
        if (IsFxActive == false) return;

        Vector3 origin = transform.position;
        float halfAngle = data.Angle * Mathf.Max(data.VisualAngleMultiplier, 0f) * Mathf.Deg2Rad;
        fxPropertyBlock.SetVector(CenterID, new Vector4(origin.x, origin.y, visualFullRadius, PixelsPerUnit));
        fxPropertyBlock.SetVector(FxGeoID, new Vector4(data.InitialMinDist, data.MaxDist, data.ExpandSpeed, data.Duration));
        fxPropertyBlock.SetVector(FxDirID, new Vector4(cachedDirection.x, cachedDirection.y, halfAngle, 0f));
        fxPropertyBlock.SetFloat(FxTimeID, timer);
        // 셰이더 해시(sin)가 큰 인자에서 정밀도를 잃지 않도록 작은 범위로 접는다.
        fxPropertyBlock.SetFloat(FxSeedID, 1f + Mathf.Repeat(data.TrailSeed, 10f));
        fxPropertyBlock.SetVector(FxTintID, new Vector4(data.Color.r, data.Color.g, data.Color.b, 1f));
        fxRenderer.SetPropertyBlock(fxPropertyBlock);
    }

    private float GetVisualDuration()
    {
        return data.Duration + Mathf.Max(data.VisualExtraDuration, 0f);
    }

    // 러너가 살아 있는 시간. 효과 레이어가 있으면 가장 늦게 튄 흙먼지(최대 수명 0.38초)와 소멸 흩날림이 끝날 때까지.
    private float GetTotalDuration()
    {
        float visualDuration = GetVisualDuration();
        if (IsFxActive == false) return visualDuration;
        return Mathf.Max(visualDuration, data.Duration + FxTailSeconds, data.Duration * 0.95f + 0.38f);
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
        if (fxRenderer != null)
        {
            fxRenderer.enabled = false;
        }

        // 파괴하지 않고 비활성화한 뒤 공용 풀로 되돌려 재사용한다.
        gameObject.SetActive(false);

        Action<ShockWaveVisualRunner> callback = onStopped;
        onStopped = null;
        callback?.Invoke(this);
    }
}
