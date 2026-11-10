using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 용암 불씨. 그 씬에 파티클 시스템 하나만 두고, 화면 안의 용암 칸에서 작은 불씨 알갱이가 비스듬히 튀어 올라
/// 포물선을 그리며 떨어지다가 튀어 오른 높이쯤에서 사라지게 한다(수명을 그 순간에 맞춰 정한다).
/// 비용을 줄이려고 용암 칸 목록을 만들거나 훑지 않는다. 매 프레임 화면 안 무작위 자리 몇 곳만 골라
/// 그 자리가 물(용암) 타일맵 칸일 때만 튀게 하므로, 보이는 용암이 넓을수록 자주 튀고 안 보이면 칸 조회 몇 번으로 끝난다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳·로그라이크에서는 아무것도 만들지 않는다.
/// </summary>
public class StageLavaEmbers : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 한 번 멈췄다 돌아왔을 때 밀린 횟수를 한꺼번에 쏟지 않도록 모아 둘 수 있는 최대치(프레임 수 기준)
    private const float MaxPendingFrames = 2f;

    private static StageLavaEmbers current;

    // //내부 의존성
    private LavaEmberSettings settings;
    private Tilemap lavaTilemap;
    private ParticleSystem emberSystem;
    private float pendingSamples;

    // // 퍼블릭 제어 메서드
    public static void Apply(LavaEmberSettings _settings, Tilemap _lavaTilemap, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material || null == _lavaTilemap
            || _settings.samplesPerSecond <= 0f || GameModeContext.IsRoguelike)
        {
            return;
        }

        GameObject root = new GameObject("[StageLavaEmbers]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageLavaEmbers>();
        current.Setup(_settings, _lavaTilemap);
    }

    public static void Remove()
    {
        if (null != current)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    private void Setup(LavaEmberSettings _settings, Tilemap _lavaTilemap)
    {
        settings = _settings;
        lavaTilemap = _lavaTilemap;

        emberSystem = gameObject.AddComponent<ParticleSystem>();
        emberSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = emberSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(8, _settings.maxParticles);
        main.gravityModifier = Mathf.Max(0.01f, _settings.gravity) / Mathf.Max(0.0001f, Physics.gravity.magnitude);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = emberSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = emberSystem.shape;
        shape.enabled = false;

        // 포물선 내내 밝다가, 떨어지는 끝에서 빠르게 옅어진다(꺼져 가며 조금 붉어진다).
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = emberSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.6f), new GradientColorKey(new Color(1f, 0.6f, 0.45f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

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

        emberSystem.Play();
    }

    private void Update()
    {
        if (null == lavaTilemap)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        int maxPerFrame = Mathf.Max(1, settings.maxSamplesPerFrame);
        pendingSamples = Mathf.Min(pendingSamples + settings.samplesPerSecond * deltaTime, maxPerFrame * MaxPendingFrames);
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
            if (lavaTilemap.HasTile(lavaTilemap.WorldToCell(point)))
            {
                EmitBurst(point);
            }
        }
    }

    private void EmitBurst(Vector3 _origin)
    {
        int count = Random.Range(settings.burstCountRange.x, Mathf.Max(settings.burstCountRange.x, settings.burstCountRange.y) + 1);
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();

        float gravity = Mathf.Max(0.01f, settings.gravity);

        for (int k = 0; k < count; k++)
        {
            int sizePixels = Random.Range(settings.sizePixelsRange.x, Mathf.Max(settings.sizePixelsRange.x, settings.sizePixelsRange.y) + 1);
            float upSpeed = Random.Range(settings.launchUpSpeedRange.x, settings.launchUpSpeedRange.y);
            float sideSpeed = Random.Range(settings.launchSideSpeedRange.x, settings.launchSideSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f);
            float fallBelow = Mathf.Max(0f, Random.Range(settings.fallBelowRange.x, settings.fallBelowRange.y));

            emitParams.position = _origin;
            emitParams.velocity = new Vector3(sideSpeed, upSpeed, 0f);
            // 튀어 오른 높이보다 fallBelow만큼 아래로 내려오는 순간까지: up*t - g*t²/2 = -fallBelow
            emitParams.startLifetime = Mathf.Max(0.05f, (upSpeed + Mathf.Sqrt(upSpeed * upSpeed + 2f * gravity * fallBelow)) / gravity);
            emitParams.startSize = Mathf.Max(1, sizePixels) / PixelsPerUnit;
            emitParams.startColor = Color.Lerp(settings.colorA, settings.colorB, Random.value);
            emberSystem.Emit(emitParams, 1);
        }
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
