using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 물가·풀숲 반딧불. 그 씬에 파티클 시스템 하나만 두고, 화면 안 무작위 자리 몇 곳을 골라
/// 물가(물과 땅의 경계에서 shoreReachCells 칸 안, StageShoreMap)면 늘, 풀 장식 칸이면 grassChance 확률로 작은 빛 알갱이를 띄운다.
/// 알갱이는 천천히 떠다니며(노이즈) 수명 동안 몇 번 깜빡이다 사라진다.
/// 칸 목록을 만들거나 훑지 않고 매 프레임 칸 조회 몇 번으로 끝난다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳에서는 아무것도 만들지 않는다.
/// 로그라이크 지형 전환 때는 새로 띄우는 양이 연출 세기(StageAmbienceFade.Visibility)를 따른다.
/// </summary>
public class StageFireflies : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 한 번 멈췄다 돌아왔을 때 밀린 횟수를 한꺼번에 쏟지 않도록 모아 둘 수 있는 최대치(프레임 수 기준)
    private const float MaxPendingFrames = 2f;

    private static StageFireflies current;

    // //내부 의존성
    private FireflySettings settings;
    private Tilemap grassDecoTilemap;
    private ParticleSystem fireflySystem;
    private float pendingSamples;

    // // 퍼블릭 제어 메서드
    /// <summary>_grassDecoTilemap은 풀 장식이 깔린 타일맵(없으면 물가에만 띄운다).</summary>
    public static void Apply(FireflySettings _settings, Tilemap _grassDecoTilemap, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material
            || _settings.samplesPerSecond <= 0f)
        {
            return;
        }

        GameObject root = new GameObject("[StageFireflies]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageFireflies>();
        current.Setup(_settings, _grassDecoTilemap);
    }

    public static void Remove()
    {
        if (null != current)
        {
            // 로그라이크 지형 전환으로 바뀔 때는 이미 떠 있는 알갱이가 수명대로 사라지게 둔다(StageAmbienceFade).
            if (false == StageAmbienceFade.TryRetire(current))
            {
                Destroy(current.gameObject);
            }
            current = null;
        }
    }

    private void Setup(FireflySettings _settings, Tilemap _grassDecoTilemap)
    {
        settings = _settings;
        grassDecoTilemap = _grassDecoTilemap;

        fireflySystem = gameObject.AddComponent<ParticleSystem>();
        fireflySystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = fireflySystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(8, _settings.maxParticles);
        main.gravityModifier = 0f;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = fireflySystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = fireflySystem.shape;
        shape.enabled = false;

        // 천천히 이리저리 떠다닌다.
        ParticleSystem.NoiseModule noise = fireflySystem.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.strength = Mathf.Max(0f, _settings.wanderSpeed);
        noise.frequency = Mathf.Max(0.01f, _settings.wanderFrequency);
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.octaveCount = 1;

        // 수명 동안 세 번 밝아졌다 어두워지며(두 번은 완전히 꺼지지 않는다) 끝에서 사라진다.
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = fireflySystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient blink = new Gradient();
        blink.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.2f, 0.3f), new GradientAlphaKey(1f, 0.45f),
                new GradientAlphaKey(0.2f, 0.62f), new GradientAlphaKey(1f, 0.78f), new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(blink);

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = _settings.material;
        particleRenderer.sortingLayerName = _settings.sortingLayerName;
        particleRenderer.sortingOrder = _settings.sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 1f;

        fireflySystem.Play();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        int maxPerFrame = Mathf.Max(1, settings.maxSamplesPerFrame);
        pendingSamples = Mathf.Min(pendingSamples + settings.samplesPerSecond * StageAmbienceFade.Visibility * deltaTime, maxPerFrame * MaxPendingFrames);
        int samples = Mathf.Min((int)pendingSamples, maxPerFrame);
        if (samples <= 0)
        {
            return;
        }
        pendingSamples -= samples;

        Camera mainCamera = Camera.main;
        if (null == mainCamera || false == mainCamera.orthographic)
        {
            return;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 center = mainCamera.transform.position;

        for (int k = 0; k < samples; k++)
        {
            Vector3 point = new Vector3(center.x + Random.Range(-halfWidth, halfWidth), center.y + Random.Range(-halfHeight, halfHeight), 0f);
            if (IsFireflySpot(point))
            {
                Emit(point);
            }
        }
    }

    private bool IsFireflySpot(Vector3 _point)
    {
        if (StageShoreMap.TryGetShoreDistance(_point, out int shoreCells) && shoreCells <= settings.shoreReachCells)
        {
            return true;
        }

        return null != grassDecoTilemap && settings.grassChance > 0f
            && grassDecoTilemap.HasTile(grassDecoTilemap.WorldToCell(_point))
            && Random.value < settings.grassChance;
    }

    private void Emit(Vector3 _ground)
    {
        // 로그라이크 지형 전환에서 새 연출이 파도를 따라 번져 켜지는 동안은 켜진 자리에만 띄운다(StageAmbienceFade, 본편은 늘 true).
        if (false == StageAmbienceFade.ShowsAt(_ground)) return;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();

        int sizePixels = Random.Range(settings.sizePixelsRange.x, Mathf.Max(settings.sizePixelsRange.x, settings.sizePixelsRange.y) + 1);
        float height = Random.Range(settings.heightRange.x, settings.heightRange.y);
        Vector2 drift = Random.insideUnitCircle * settings.wanderSpeed * 0.5f;

        emitParams.position = _ground + new Vector3(0f, height, 0f);
        emitParams.velocity = new Vector3(drift.x, drift.y, 0f);
        emitParams.startLifetime = Mathf.Max(0.1f, Random.Range(settings.lifetimeRange.x, settings.lifetimeRange.y));
        emitParams.startSize = Mathf.Max(1, sizePixels) / PixelsPerUnit;
        emitParams.startColor = Color.Lerp(settings.colorA, settings.colorB, Random.value);
        fireflySystem.Emit(emitParams, 1);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
