using System;
using UnityEngine;

/// <summary>바닥 타일 파도의 소리·진동·물가 물보라 수치.</summary>
[Serializable]
public sealed class RoguelikeTileWaveFeedbackSettings
{
    [Header("앞머리 소리")]
    [Tooltip("앞머리가 퍼지는 동안 일정 거리마다 내는 짧은 소리. None이면 끈다.")]
    public SoundID frontTickSound = SoundID.ZoomTick;
    [Tooltip("앞머리가 이 파도 거리(바닥 평면 거리)만큼 퍼질 때마다 한 번 낸다. 섬 가장자리에 닿으면 그친다.")]
    public float frontTickSpacing = 1.5f;
    [Range(0f, 1f)] public float frontTickVolume = 0.35f;
    [Tooltip("앞머리 소리 높낮이: 섬 중심(x)에서 섬 가장자리(y)로 갈수록 올라간다.")]
    public Vector2 frontTickPitch = new Vector2(0.8f, 1.35f);

    [Header("바다에 닿음")]
    [Tooltip("파도가 섬 가장자리(바다)에 처음 닿을 때 한 번 내는 소리. None이면 끈다.")]
    public SoundID shoreSound = SoundID.ShockWaveAppear;
    [Range(0f, 1f)] public float shoreVolume = 0.4f;
    public float shorePitch = 0.75f;

    [Header("물가 물보라")]
    [Tooltip("섬 가장자리 칸이 새 지형으로 바뀔 때 그 물가에 튀는 물보라 재질(정점색 x 그림, 픽셀 격자 맞춤 - 1스테이지 꽃잎·비와 같은 WindPetal). 비우면 끈다.")]
    public Material splashMaterial;
    [Tooltip("물가 자리 하나에 튀는 물보라 수.")]
    [Min(0)] public int splashesPerPoint = 1;
    [Tooltip("물보라가 물가 자리에서 흩어지는 거리(월드, 세로는 절반).")]
    public float splashScatter = 0.12f;
    [Tooltip("물보라가 보이는 시간(초). 그림 세 장(부딪힘 → 양옆 위로 튐 → 떨어지며 옅어짐)이 이 시간에 지나간다.")]
    public float splashSeconds = 0.3f;
    [Tooltip("물 티어의 물보라 색.")]
    public Color splashColor = new Color(0.85f, 0.93f, 1f, 0.8f);
    [Tooltip("용암 티어의 물보라(불티) 색.")]
    public Color lavaSplashColor = new Color(1f, 0.72f, 0.3f, 0.9f);
    public string sortingLayerName = "Objects";
    public int sortingOrder = 32767;
    [Min(16)] public int maxSplashes = 256;
}

/// <summary>
/// 바닥 타일 파도(RoguelikeTileWave)에 맞춘 소리·진동·물가 물보라.
/// - 앞머리가 떠나는 순간 바닥이 일렁이는 진동, 퍼지는 동안 거리마다 짧은 소리(섬 가장자리로 갈수록 높아진다).
/// - 파도가 바다에 처음 닿는 순간 소리 한 번과 짧은 진동.
/// - 섬 가장자리 칸이 새 지형으로 바뀔 때마다 그 물가에 물보라(용암 티어는 불티 색). 그림은 1스테이지 비의 착지 물방울과 같다.
/// 게임 동작에는 영향이 없다(렌더링·소리·진동만).
/// </summary>
public sealed class RoguelikeTileWaveFeedback
{
    private const float PixelsPerUnit = 32f;

    private RoguelikeTileWave wave;
    private RoguelikeTileWaveFeedbackSettings settings;
    private Transform root;

    private GameObject splashObject;
    private ParticleSystem splashSystem;
    private Material splashMaterial;
    private Texture2D splashTexture;

    // 이번 파도에서 다음 앞머리 소리를 낼 파도 거리. 앞머리가 떠나기 전이면 NaN.
    private float nextTickDistance = float.NaN;
    // 이번 파도에서 바다에 닿는 소리·진동을 냈는지.
    private bool bShorePlayed;

    public void Initialize(RoguelikeTileWave _wave, RoguelikeTileWaveFeedbackSettings _settings, Transform _root)
    {
        wave = _wave;
        settings = _settings ?? new RoguelikeTileWaveFeedbackSettings();
        root = _root;

        wave.FrontLaunchedEvent += OnFrontLaunched;
        wave.ShoreRevealedEvent += OnShoreRevealed;
    }

    public void Release()
    {
        if (null != wave)
        {
            wave.FrontLaunchedEvent -= OnFrontLaunched;
            wave.ShoreRevealedEvent -= OnShoreRevealed;
        }

        if (null != splashObject) UnityEngine.Object.Destroy(splashObject);
        if (null != splashMaterial) UnityEngine.Object.Destroy(splashMaterial);
        if (null != splashTexture) UnityEngine.Object.Destroy(splashTexture);
        splashObject = null;
        splashSystem = null;
        splashMaterial = null;
        splashTexture = null;
    }

    /// <summary>매 프레임(게임 시간). 앞머리가 퍼지는 동안 거리마다 소리를 낸다.</summary>
    public void Tick()
    {
        if (null == wave || false == wave.TryGetFront(out float front, out float landEdge))
        {
            nextTickDistance = float.NaN;
            return;
        }

        if (SoundID.None == settings.frontTickSound || settings.frontTickSpacing <= 0f || float.IsNaN(nextTickDistance)) return;
        if (nextTickDistance > landEdge || front < nextTickDistance) return;

        // 한 프레임에 여러 칸 거리를 지나도 소리는 한 번만 낸다(겹쳐 커지지 않게).
        float t = landEdge > 0.01f ? Mathf.Clamp01(nextTickDistance / landEdge) : 1f;
        float pitch = Mathf.Lerp(settings.frontTickPitch.x, settings.frontTickPitch.y, t);
        Sound.Play(settings.frontTickSound, wave.WaveCenter, settings.frontTickVolume, false, pitch);
        while (nextTickDistance <= front) nextTickDistance += settings.frontTickSpacing;
    }

    private void OnFrontLaunched(bool _bEntry)
    {
        nextTickDistance = Mathf.Max(0.01f, settings.frontTickSpacing);
        bShorePlayed = false;
        Rumble.Play(EHapticEvent.RoguelikeTileWave);
    }

    private void OnShoreRevealed(Vector3 _position, bool _bLava)
    {
        if (false == bShorePlayed)
        {
            bShorePlayed = true;
            if (SoundID.None != settings.shoreSound) Sound.Play(settings.shoreSound, _position, settings.shoreVolume, false, settings.shorePitch);
            Rumble.Play(EHapticEvent.RoguelikeTileWaveShore);
        }

        EmitSplash(_position, _bLava);
    }

    private void EmitSplash(Vector3 _position, bool _bLava)
    {
        if (settings.splashesPerPoint <= 0 || false == EnsureSplashSystem()) return;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        Color color = _bLava ? settings.lavaSplashColor : settings.splashColor;
        float scatter = Mathf.Max(0f, settings.splashScatter);
        for (int k = 0; k < settings.splashesPerPoint; k++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * scatter;
            emitParams.position = _position + new Vector3(offset.x, offset.y * 0.5f, 0f);
            emitParams.velocity = Vector3.zero;
            emitParams.startLifetime = Mathf.Max(0.05f, settings.splashSeconds);
            emitParams.startColor = color;
            splashSystem.Emit(emitParams, 1);
        }
    }

    /// <summary>물보라 파티클 시스템을 처음 쓸 때 만든다(1스테이지 비의 착지 물방울과 같은 그림 세 장).</summary>
    private bool EnsureSplashSystem()
    {
        if (null != splashSystem) return true;
        if (null == settings.splashMaterial) return false;

        int frameHeight = StageCloudRain.SplashArtRows;
        splashTexture = StageCloudRain.CreateSplashTexture(frameHeight);
        splashMaterial = new Material(settings.splashMaterial) { name = "RoguelikeTileWaveSplash" };
        splashMaterial.mainTexture = splashTexture;

        splashObject = new GameObject("[RoguelikeTileWaveSplash]");
        if (null != root) splashObject.transform.SetParent(root, false);
        splashSystem = splashObject.AddComponent<ParticleSystem>();
        splashSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = splashSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, settings.maxSplashes);
        main.gravityModifier = 0f;
        main.startSize3D = true;
        main.startSizeX = StageCloudRain.SplashFrameWidth / PixelsPerUnit;
        main.startSizeY = frameHeight / PixelsPerUnit;
        main.startSizeZ = 1f;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = splashSystem.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = splashSystem.shape;
        shape.enabled = false;

        ParticleSystem.TextureSheetAnimationModule sheet = splashSystem.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = StageCloudRain.SplashFrameCount;
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.cycleCount = 1;

        ParticleSystemRenderer renderer = splashObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = splashMaterial;
        renderer.sortingLayerName = settings.sortingLayerName;
        renderer.sortingOrder = settings.sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 1f;

        splashSystem.Play();
        return true;
    }
}
