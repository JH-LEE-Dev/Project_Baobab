using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 용암 기둥. 그 씬에 파티클 시스템 하나만 두고, 몇 초에 한 번 화면 안 용암 칸(안쪽 칸 우선) 한 곳에서
/// 짧은 시간 동안 위 속도가 조금씩 다른 작은 알갱이를 이어 쏘아, 곧게 솟았다가 제자리로 떨어지는 작은 기둥처럼 보이게 한다.
/// 알갱이는 쏜 높이로 돌아오는 순간 사라진다(수명을 그 순간에 맞춰 정한다).
/// 자리는 화면 안 무작위 위치 몇 곳만 찍어 고르므로 용암 칸 목록을 만들거나 훑지 않는다. 화면에 용암이 안 보이면 그 차례는 건너뛴다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳에서는 아무것도 만들지 않는다.
/// 로그라이크 지형 전환 때는 뿜는 양이 연출 세기(StageAmbienceFade.Visibility)를 따른다.
/// </summary>
public class StageLavaSpouts : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 한 프레임이 길어졌을 때 한꺼번에 쏟는 알갱이 수 상한
    private const int MaxEmitPerFrame = 8;

    private struct Spout
    {
        public Vector3 origin;
        public float remaining;
        public float pending;
    }

    private static StageLavaSpouts current;

    // //내부 의존성
    private LavaSpoutSettings settings;
    private Tilemap lavaTilemap;
    private ParticleSystem spoutSystem;
    private readonly List<Spout> spouts = new List<Spout>(4);
    private float nextSpoutTimer;

    // // 퍼블릭 제어 메서드
    public static void Apply(LavaSpoutSettings _settings, Tilemap _lavaTilemap, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material || null == _lavaTilemap)
        {
            return;
        }

        GameObject root = new GameObject("[StageLavaSpouts]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageLavaSpouts>();
        current.Setup(_settings, _lavaTilemap);
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

    private void Setup(LavaSpoutSettings _settings, Tilemap _lavaTilemap)
    {
        settings = _settings;
        lavaTilemap = _lavaTilemap;
        nextSpoutTimer = RandomInterval();

        spoutSystem = gameObject.AddComponent<ParticleSystem>();
        spoutSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = spoutSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(8, _settings.maxParticles);
        main.gravityModifier = Mathf.Max(0.01f, _settings.gravity) / Mathf.Max(0.0001f, Physics.gravity.magnitude);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = spoutSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = spoutSystem.shape;
        shape.enabled = false;

        // 솟는 동안 밝다가, 떨어져 내려오는 끝에서 식으며(붉어지며) 옅어진다.
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = spoutSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.55f), new GradientColorKey(new Color(1f, 0.55f, 0.4f, 1f), 1f) },
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

        spoutSystem.Play();
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

        nextSpoutTimer -= deltaTime;
        if (nextSpoutTimer <= 0f)
        {
            nextSpoutTimer = RandomInterval();
            if (TryPickOrigin(out Vector3 origin))
            {
                spouts.Add(new Spout
                {
                    origin = origin,
                    remaining = Random.Range(settings.durationRange.x, Mathf.Max(settings.durationRange.x, settings.durationRange.y)),
                    pending = 1f,
                });
            }
        }

        for (int i = spouts.Count - 1; i >= 0; i--)
        {
            Spout spout = spouts[i];
            spout.pending += settings.particlesPerSecond * StageAmbienceFade.Visibility * deltaTime;
            int count = Mathf.Min((int)spout.pending, MaxEmitPerFrame);
            spout.pending -= count;
            for (int k = 0; k < count; k++)
            {
                EmitParticle(spout.origin);
            }

            spout.remaining -= deltaTime;
            if (spout.remaining <= 0f)
            {
                spouts.RemoveAt(i);
            }
            else
            {
                spouts[i] = spout;
            }
        }
    }

    private float RandomInterval()
    {
        return Mathf.Max(0.1f, Random.Range(settings.intervalRange.x, Mathf.Max(settings.intervalRange.x, settings.intervalRange.y)));
    }

    // 화면 안 무작위 자리를 몇 번 찍어 용암 칸을 고른다. 상하좌우 이웃까지 모두 용암인 칸(가장자리가 아닌 안쪽)이 걸리면
    // 그 자리를 쓰고, 안쪽 칸이 하나도 안 걸렸으면 처음 걸린 용암 칸을 쓴다(용암 띠가 좁아 안쪽 칸이 드물다).
    private bool TryPickOrigin(out Vector3 _origin)
    {
        _origin = Vector3.zero;
        bool bFoundLava = false;

        Camera mainCamera = Camera.main;
        if (null == mainCamera || false == mainCamera.orthographic)
        {
            return false;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 center = mainCamera.transform.position;
        int attempts = Mathf.Max(1, settings.pickAttempts);

        for (int k = 0; k < attempts; k++)
        {
            Vector3 point = new Vector3(center.x + Random.Range(-halfWidth, halfWidth), center.y + Random.Range(-halfHeight, halfHeight), 0f);
            Vector3Int cell = lavaTilemap.WorldToCell(point);
            if (false == lavaTilemap.HasTile(cell))
            {
                continue;
            }

            if (lavaTilemap.HasTile(cell + Vector3Int.right) && lavaTilemap.HasTile(cell + Vector3Int.left)
                && lavaTilemap.HasTile(cell + Vector3Int.up) && lavaTilemap.HasTile(cell + Vector3Int.down))
            {
                _origin = point;
                return true;
            }

            if (false == bFoundLava)
            {
                bFoundLava = true;
                _origin = point;
            }
        }

        return bFoundLava;
    }

    private void EmitParticle(Vector3 _origin)
    {
        // 로그라이크 지형 전환에서 새 연출이 파도를 따라 번져 켜지는 동안은 켜진 자리에만 띄운다(StageAmbienceFade, 본편은 늘 true).
        if (false == StageAmbienceFade.ShowsAt(_origin)) return;

        float gravity = Mathf.Max(0.01f, settings.gravity);
        float upSpeed = Random.Range(settings.upSpeedRange.x, Mathf.Max(settings.upSpeedRange.x, settings.upSpeedRange.y));
        float sideSpeed = Random.Range(-settings.sideSpeed, settings.sideSpeed);
        int sizePixels = Random.Range(settings.sizePixelsRange.x, Mathf.Max(settings.sizePixelsRange.x, settings.sizePixelsRange.y) + 1);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = _origin + new Vector3(Random.Range(-settings.baseWidth, settings.baseWidth), 0f, 0f);
        emitParams.velocity = new Vector3(sideSpeed, upSpeed, 0f);
        // 쏜 높이로 다시 내려오는 순간까지: up*t - g*t²/2 = 0
        emitParams.startLifetime = Mathf.Max(0.05f, 2f * upSpeed / gravity);
        emitParams.startSize = Mathf.Max(1, sizePixels) / PixelsPerUnit;
        emitParams.startColor = Color.Lerp(settings.colorA, settings.colorB, Random.value);
        spoutSystem.Emit(emitParams, 1);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
