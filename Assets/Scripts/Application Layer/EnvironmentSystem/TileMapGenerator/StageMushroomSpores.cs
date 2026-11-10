using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버섯 포자 뿜기. 그 씬에 파티클 시스템 하나만 두고, 화면에 보이는(컬링으로 켜져 있는) 큰 버섯 장식이
/// 저마다의 박자로 몇 초에 한 번 갓 위로 작은 포자 알갱이를 몇 개 뿜게 한다.
/// 박자는 버섯 자리로 정해지므로(자리 해시) 버섯마다 따로 기억할 것이 없고, 매 프레임 보이는 버섯만 훑는다.
/// 뿜기 직전에는 갓이 서서히 밝아졌다가 뿜는 순간 가장 밝고, 뿜은 뒤 원래 밝기로 돌아온다(숨쉬기, 장식의 밝기 값만 바꾼다).
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳·로그라이크에서는 아무것도 만들지 않는다.
/// </summary>
public class StageMushroomSpores : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;

    private static StageMushroomSpores current;

    // //내부 의존성
    private MushroomSporeSettings settings;
    private AnimatedObjGenerator generator;
    private ParticleSystem sporeSystem;
    private float previousTime;
    // 갓이 원래보다 밝게 그려지고 있는 버섯(돌아올 때 원래 밝기로 되돌린다)
    private readonly HashSet<StaticObj> glowingMushrooms = new HashSet<StaticObj>();

    // // 퍼블릭 제어 메서드
    public static void Apply(MushroomSporeSettings _settings, AnimatedObjGenerator _generator, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled || null == _settings.material || null == _generator
            || null == _settings.mushroomPrefabs || 0 == _settings.mushroomPrefabs.Count || GameModeContext.IsRoguelike)
        {
            return;
        }

        GameObject root = new GameObject("[StageMushroomSpores]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageMushroomSpores>();
        current.Setup(_settings, _generator);
    }

    public static void Remove()
    {
        if (null != current)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    private void Setup(MushroomSporeSettings _settings, AnimatedObjGenerator _generator)
    {
        settings = _settings;
        generator = _generator;
        previousTime = Time.time;

        sporeSystem = gameObject.AddComponent<ParticleSystem>();
        sporeSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = sporeSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, _settings.maxParticles);
        main.gravityModifier = -Mathf.Max(0f, _settings.buoyancy) / Mathf.Max(0.0001f, Physics.gravity.magnitude);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = sporeSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = sporeSystem.shape;
        shape.enabled = false;

        // 솟은 뒤 서서히 느려져 떠다닌다.
        ParticleSystem.LimitVelocityOverLifetimeModule limit = sporeSystem.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0f;
        limit.dampen = _settings.dampen;

        // 떠다니며 천천히 흔들린다.
        if (_settings.wanderStrength > 0f)
        {
            ParticleSystem.NoiseModule noise = sporeSystem.noise;
            noise.enabled = true;
            noise.strength = _settings.wanderStrength;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.damping = false;
        }

        // 뿜을 때 빠르게 나타났다가 끝에서 옅어진다.
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sporeSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
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

        sporeSystem.Play();
    }

    private void Update()
    {
        float time = Time.time;
        float previous = previousTime;
        previousTime = time;
        if (time <= previous || null == generator)
        {
            return;
        }

        List<StaticObj> prefabs = settings.mushroomPrefabs;
        for (int p = 0; p < prefabs.Count; p++)
        {
            List<StaticObj> mushrooms = generator.GetActiveStaticObjects(prefabs[p]);
            if (null == mushrooms)
            {
                continue;
            }

            for (int i = 0; i < mushrooms.Count; i++)
            {
                StaticObj mushroom = mushrooms[i];
                // 화면 밖 장식은 컬링으로 꺼져 있다.
                if (null == mushroom || false == mushroom.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 position = mushroom.transform.position;
                float interval = Mathf.Max(0.5f, Mathf.Lerp(settings.intervalRange.x, settings.intervalRange.y, Hash(position.x, position.y, 0.17f)));
                float offset = Hash(position.x, position.y, 0.53f) * interval;

                // 이번 프레임에 이 버섯의 박자 하나를 지났을 때만 뿜는다.
                if (Mathf.Floor((time + offset) / interval) != Mathf.Floor((previous + offset) / interval))
                {
                    EmitPuff(position);
                }

                UpdateCapGlow(mushroom, time, offset, interval);
            }
        }
    }

    // 갓 숨쉬기. 다음 뿜기까지 남은 시간과 지난 뿜기 뒤 흐른 시간으로 밝기를 정하므로 버섯마다 기억할 것이 없다.
    private void UpdateCapGlow(StaticObj _mushroom, float _time, float _offset, float _interval)
    {
        float glow = 0f;
        if (settings.capGlowAmount > 0f)
        {
            float sincePuff = Mathf.Repeat(_time + _offset, _interval);
            float untilPuff = _interval - sincePuff;
            float rise = Mathf.Max(0.05f, settings.capGlowRiseTime);
            float fall = Mathf.Max(0.05f, settings.capGlowFallTime);
            if (untilPuff < rise)
            {
                float k = 1f - untilPuff / rise;
                glow = k * k;
            }
            else if (sincePuff < fall)
            {
                float k = 1f - sincePuff / fall;
                glow = k * k;
            }
        }

        if (glow > 0f)
        {
            _mushroom.SetHDRMultiplier(1f + settings.capGlowAmount * glow);
            glowingMushrooms.Add(_mushroom);
        }
        else if (glowingMushrooms.Remove(_mushroom))
        {
            _mushroom.SetHDRMultiplier(1f);
        }
    }

    private void EmitPuff(Vector3 _position)
    {
        int count = Random.Range(settings.burstCountRange.x, Mathf.Max(settings.burstCountRange.x, settings.burstCountRange.y) + 1);
        Vector2 origin = (Vector2)_position + settings.emitOffset;
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();

        for (int k = 0; k < count; k++)
        {
            Vector2 jitter = new Vector2(Random.Range(-1f, 1f) * settings.emitSpread.x, Random.Range(-1f, 1f) * settings.emitSpread.y);
            float rise = Random.Range(settings.riseSpeedRange.x, settings.riseSpeedRange.y);
            float side = Random.Range(-1f, 1f) * settings.sideSpeed;
            int sizePixels = Random.Range(settings.sizePixelsRange.x, Mathf.Max(settings.sizePixelsRange.x, settings.sizePixelsRange.y) + 1);

            emitParams.position = new Vector3(origin.x + jitter.x, origin.y + jitter.y, 0f);
            emitParams.velocity = new Vector3(side, rise, 0f);
            emitParams.startLifetime = Random.Range(settings.lifetimeRange.x, settings.lifetimeRange.y);
            emitParams.startSize = Mathf.Max(1, sizePixels) / PixelsPerUnit;
            emitParams.startColor = Color.Lerp(settings.colorA, settings.colorB, Random.value);
            sporeSystem.Emit(emitParams, 1);
        }
    }

    // 버섯 자리로 정하는 0~1 값(같은 자리는 늘 같은 박자).
    private static float Hash(float _x, float _y, float _salt)
    {
        float h = Mathf.Sin(_x * 127.1f + _y * 311.7f + _salt * 74.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    private void OnDestroy()
    {
        // 밝게 그리던 갓을 원래 밝기로 되돌린다(장식은 풀에서 다시 쓰인다).
        foreach (StaticObj mushroom in glowingMushrooms)
        {
            if (null != mushroom)
            {
                mushroom.SetHDRMultiplier(1f);
            }
        }
        glowingMushrooms.Clear();

        if (current == this)
        {
            current = null;
        }
    }
}
