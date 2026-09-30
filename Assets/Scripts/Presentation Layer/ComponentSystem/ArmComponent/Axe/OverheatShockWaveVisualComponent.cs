using System;
using System.Collections.Generic;
using UnityEngine;

public class OverheatShockWaveVisualComponent : ShockWaveVisualComponent
{
    [Header("Overheat Particles")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Sprite[] dustFrames = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] starFrames = Array.Empty<Sprite>();
    [SerializeField] private int dustPoolSize = 28;
    [SerializeField] private int starPoolSize = 14;
    [SerializeField] private int dustBurstCount = 3;
    [SerializeField] private int starBurstCount = 2;
    [SerializeField] private float dustSpawnInterval = 0.04f;
    [SerializeField] private float starSpawnInterval = 0.06f;
    [SerializeField] private float particleTailDuration = 0.42f;
    [SerializeField] private Vector2 dustLifetime = new Vector2(0.2f, 0.32f);
    [SerializeField] private Vector2 starLifetime = new Vector2(0.18f, 0.3f);
    [SerializeField, ColorUsage(true, true)] private Color cyan = new Color(0.12f, 1.75f, 2.1f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color blue = new Color(0.08f, 0.45f, 1.5f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color violet = new Color(0.85f, 0.12f, 1.25f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color ember = new Color(1.6f, 0.16f, 0.03f, 1f);

    private ShockWave shockWave;
    private SpriteRenderer sourceRenderer;
    private Quaternion initialRotation;

    public override void Initialize(ShockWave _shockWave)
    {
        base.Initialize(_shockWave);
        shockWave = _shockWave;
        sourceRenderer = GetComponent<SpriteRenderer>();
        initialRotation = transform.rotation;
    }

    public override void Play(float _duration)
    {
        base.Play(_duration);

        if (shockWave == null) shockWave = GetComponent<ShockWave>();
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        if (shockWave == null || particleMaterial == null) return;

        OverheatShockWaveParticleRunner runner = GetParticleRunner();
        runner.SetOnStopped(ReturnParticleRunner);
        runner.Play(new OverheatShockWaveParticleRunner.PlayData
        {
            Owner = transform,
            Origin = shockWave.VisualOrigin,
            InitialRotation = initialRotation,
            StartPosition = transform.position,
            Material = particleMaterial,
            DustFrames = dustFrames,
            StarFrames = starFrames,
            SortingLayerID = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0,
            SortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder + 1 : 1,
            ExpandSpeed = shockWave.EffectiveExpandSpeed,
            Duration = _duration,
            InitialMinDist = shockWave.minDist,
            InitialMaxDist = shockWave.maxDist,
            HalfAngle = shockWave.angle,
            DustPoolSize = dustPoolSize,
            StarPoolSize = starPoolSize,
            DustBurstCount = dustBurstCount,
            StarBurstCount = starBurstCount,
            DustSpawnInterval = dustSpawnInterval,
            StarSpawnInterval = starSpawnInterval,
            TailDuration = particleTailDuration,
            DustLifetime = dustLifetime,
            StarLifetime = starLifetime,
            Cyan = cyan,
            Blue = blue,
            Violet = violet,
            Ember = ember
        });
    }

    private static readonly Stack<OverheatShockWaveParticleRunner> runnerPool = new Stack<OverheatShockWaveParticleRunner>();

    private static OverheatShockWaveParticleRunner GetParticleRunner()
    {
        while (runnerPool.Count > 0)
        {
            OverheatShockWaveParticleRunner pooled = runnerPool.Pop();
            if (pooled != null) return pooled;
        }

        GameObject runnerObject = new GameObject("OverheatShockWaveParticles");
        DontDestroyOnLoad(runnerObject);
        return runnerObject.AddComponent<OverheatShockWaveParticleRunner>();
    }

    private static void ReturnParticleRunner(OverheatShockWaveParticleRunner _runner)
    {
        if (_runner != null) runnerPool.Push(_runner);
    }
}

public class OverheatShockWaveParticleRunner : MonoBehaviour
{
    public struct PlayData
    {
        public Transform Owner;
        public Transform Origin;
        public Quaternion InitialRotation;
        public Vector3 StartPosition;
        public Material Material;
        public Sprite[] DustFrames;
        public Sprite[] StarFrames;
        public int SortingLayerID;
        public int SortingOrder;
        public float ExpandSpeed;
        public float Duration;
        public float InitialMinDist;
        public float InitialMaxDist;
        public float HalfAngle;
        public int DustPoolSize;
        public int StarPoolSize;
        public int DustBurstCount;
        public int StarBurstCount;
        public float DustSpawnInterval;
        public float StarSpawnInterval;
        public float TailDuration;
        public Vector2 DustLifetime;
        public Vector2 StarLifetime;
        public Color Cyan;
        public Color Blue;
        public Color Violet;
        public Color Ember;
    }

    private sealed class Particle
    {
        public SpriteRenderer Renderer;
        public Sprite[] Frames;
        public int StartFrame;
        public float Age;
        public float Lifetime;
        public Vector3 Velocity;
        public float Spin;
        public float InitialScale;
        public Color Color;
        public bool Active;
    }

    private const float PixelsPerUnit = 32f;
    private readonly List<Particle> dust = new List<Particle>();
    private readonly List<Particle> stars = new List<Particle>();
    // DontDestroyOnLoad 러너는 도메인 리로드 뒤 관리 객체 필드가 null로 돌아올 수 있으므로 지연 생성한다.
    private MaterialPropertyBlock propertyBlock;
    private static readonly int TintColorID = Shader.PropertyToID("_TintColor");

    private PlayData data;
    private float timer;
    private float dustTimer;
    private float starTimer;
    private float cachedDustInterval;
    private float cachedStarInterval;
    private Vector2 cachedDirection = Vector2.right;
    private Action<OverheatShockWaveParticleRunner> onStopped;
    private bool isPlaying;

    public void SetOnStopped(Action<OverheatShockWaveParticleRunner> _onStopped) => onStopped = _onStopped;

    public void Play(PlayData _data)
    {
        data = _data;
        EnsurePool(dust, Mathf.Max(1, data.DustPoolSize), "Dust");
        EnsurePool(stars, Mathf.Max(1, data.StarPoolSize), "Star");
        StopAllParticles();
        timer = 0f;
        dustTimer = 0f;
        starTimer = 0f;
        cachedDustInterval = Mathf.Max(0.01f, data.DustSpawnInterval);
        cachedStarInterval = Mathf.Max(0.01f, data.StarSpawnInterval);
        isPlaying = true;
        gameObject.SetActive(true);
        UpdateDirection();
        SpawnDustBurst();
        SpawnStarBurst();
    }

    private void Awake() => gameObject.SetActive(false);

    private void Update()
    {
        if (!isPlaying) return;

        timer += Time.deltaTime;

        if (timer <= data.Duration)
        {
            dustTimer += Time.deltaTime;
            starTimer += Time.deltaTime;
            while (dustTimer >= cachedDustInterval)
            {
                dustTimer -= cachedDustInterval;
                SpawnDustBurst();
            }
            while (starTimer >= cachedStarInterval)
            {
                starTimer -= cachedStarInterval;
                SpawnStarBurst();
            }
        }

        UpdateParticles(dust);
        UpdateParticles(stars);

        // 어떤 상황에서도 러너가 현장에 남지 않도록 꼬리 시간이 끝나면 활성 입자까지 정리한다.
        if (timer >= data.Duration + Mathf.Max(0.05f, data.TailDuration)) Stop();
    }

    private void UpdateDirection()
    {
        if (data.Owner == null) return;
        Vector3 direction = (data.Owner.rotation * Quaternion.Inverse(data.InitialRotation)) * Vector3.right;
        Vector2 isoDirection = new Vector2(direction.x, direction.y * 2f).normalized;
        if (isoDirection.sqrMagnitude > 0.0001f) cachedDirection = isoDirection;
    }

    private void SpawnDustBurst()
    {
        if (data.DustFrames == null || data.DustFrames.Length == 0) return;
        int count = Mathf.Max(1, data.DustBurstCount);
        for (int i = 0; i < count; i++)
        {
            // 풀이 거의 찼을 때도 항상 같은 한쪽 각도만 살아남지 않도록 각 입자의 각도를 독립 추첨한다.
            float angle = UnityEngine.Random.Range(-data.HalfAngle, data.HalfAngle);
            float radius = data.InitialMaxDist + data.ExpandSpeed * Mathf.Min(timer, data.Duration);
            Vector2 isoDirection = Rotate(cachedDirection, angle);
            Vector3 position = GetOrigin() + FromIso(isoDirection * (radius + UnityEngine.Random.Range(-0.06f, 0.09f)));
            // 파면보다 조금 느리게 따라가게 하여 끝 검기에 붙어 나가다가 뒤로 흩어지는 불꽃 꼬리를 만든다.
            Vector3 velocity = FromIso(isoDirection * data.ExpandSpeed * UnityEngine.Random.Range(0.48f, 0.82f));
            float colorRoll = UnityEngine.Random.value;
            Color color = colorRoll < 0.1f ? data.Ember : colorRoll < 0.26f ? data.Violet : colorRoll < 0.7f ? data.Cyan : data.Blue;
            Spawn(GetFree(dust), data.DustFrames, UnityEngine.Random.Range(0, Mathf.Min(3, data.DustFrames.Length)), position, velocity,
                UnityEngine.Random.Range(data.DustLifetime.x, data.DustLifetime.y), color, UnityEngine.Random.Range(0.6f, 1.25f), UnityEngine.Random.Range(-120f, 120f));
        }
    }

    private void SpawnStarBurst()
    {
        if (data.StarFrames == null || data.StarFrames.Length == 0) return;
        for (int i = 0; i < Mathf.Max(1, data.StarBurstCount); i++)
        {
            float angle = UnityEngine.Random.Range(-data.HalfAngle * 0.85f, data.HalfAngle * 0.85f);
            float front = data.InitialMaxDist + data.ExpandSpeed * Mathf.Min(timer, data.Duration);
            float trailStart = Mathf.Max(data.InitialMinDist, front - 0.55f);
            float trailEnd = Mathf.Max(trailStart + 0.01f, front * 0.96f);
            float radius = UnityEngine.Random.Range(trailStart, trailEnd);
            Vector2 isoDirection = Rotate(cachedDirection, angle);
            Vector3 position = GetOrigin() + FromIso(isoDirection * radius);
            float colorRoll = UnityEngine.Random.value;
            Color color = colorRoll < 0.07f ? data.Ember : colorRoll < 0.18f ? data.Violet : data.Cyan;
            Spawn(GetFree(stars), data.StarFrames, UnityEngine.Random.Range(0, Mathf.Min(3, data.StarFrames.Length)), position, Vector3.zero,
                UnityEngine.Random.Range(data.StarLifetime.x, data.StarLifetime.y), color, UnityEngine.Random.Range(0.35f, 0.8f), UnityEngine.Random.Range(-160f, 160f));
        }
    }

    private void Spawn(Particle _particle, Sprite[] _frames, int _startFrame, Vector3 _position, Vector3 _velocity,
        float _lifetime, Color _color, float _scale, float _spin)
    {
        if (_particle == null) return;
        _particle.Frames = _frames;
        _particle.StartFrame = Mathf.Clamp(_startFrame, 0, _frames.Length - 1);
        _particle.Age = 0f;
        _particle.Lifetime = Mathf.Max(0.05f, _lifetime);
        _particle.Velocity = _velocity;
        _particle.Spin = _spin;
        _particle.InitialScale = Mathf.Round(_scale * 4f) * 0.25f;
        _particle.Color = _color;
        _particle.Active = true;

        SpriteRenderer renderer = _particle.Renderer;
        renderer.sharedMaterial = data.Material;
        renderer.sprite = _frames[_particle.StartFrame];
        renderer.sortingLayerID = data.SortingLayerID;
        renderer.sortingOrder = data.SortingOrder;
        renderer.enabled = true;
        renderer.transform.position = Snap(_position);
        renderer.transform.localScale = Vector3.one * _particle.InitialScale;
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0, 4) * 90f);
        ApplyColor(_particle, 1f);
    }

    private void UpdateParticles(List<Particle> _particles)
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            Particle particle = _particles[i];
            if (!particle.Active) continue;

            particle.Age += Time.deltaTime;
            float progress = Mathf.Clamp01(particle.Age / particle.Lifetime);
            int remainingFrames = particle.Frames.Length - particle.StartFrame;
            int frameOffset = Mathf.Min(remainingFrames - 1, Mathf.FloorToInt(progress * remainingFrames));
            particle.Renderer.sprite = particle.Frames[particle.StartFrame + frameOffset];
            particle.Renderer.transform.position = Snap(particle.Renderer.transform.position + particle.Velocity * Time.deltaTime);
            particle.Renderer.transform.Rotate(0f, 0f, particle.Spin * Time.deltaTime);
            float fade = 1f - Mathf.SmoothStep(0.12f, 1f, progress);
            particle.Renderer.transform.localScale = Vector3.one * particle.InitialScale * Mathf.Lerp(1f, 0.35f, progress);
            ApplyColor(particle, fade);

            if (progress >= 1f)
            {
                particle.Active = false;
                particle.Renderer.enabled = false;
            }
        }
    }

    private void ApplyColor(Particle _particle, float _alpha)
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Color color = _particle.Color;
        color.a *= _alpha;
        propertyBlock.Clear();
        propertyBlock.SetColor(TintColorID, color);
        _particle.Renderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsurePool(List<Particle> _pool, int _size, string _name)
    {
        while (_pool.Count < _size)
        {
            GameObject child = new GameObject(_name + "_" + _pool.Count);
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            _pool.Add(new Particle { Renderer = renderer });
        }
    }

    private static Particle GetFree(List<Particle> _pool)
    {
        for (int i = 0; i < _pool.Count; i++) if (!_pool[i].Active) return _pool[i];
        return null;
    }

    private Vector3 GetOrigin()
    {
        if (data.Origin != null && data.Origin != data.Owner) return data.Origin.position;
        return data.StartPosition;
    }

    private static Vector2 Rotate(Vector2 _direction, float _angle)
    {
        float radians = _angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(_direction.x * cos - _direction.y * sin, _direction.x * sin + _direction.y * cos);
    }

    private static Vector3 FromIso(Vector2 _position) => new Vector3(_position.x, _position.y * 0.5f, 0f);
    private static Vector3 Snap(Vector3 _position) => new Vector3(Mathf.Round(_position.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(_position.y * PixelsPerUnit) / PixelsPerUnit, _position.z);

    private void StopAllParticles()
    {
        for (int i = 0; i < dust.Count; i++) { dust[i].Active = false; dust[i].Renderer.enabled = false; }
        for (int i = 0; i < stars.Count; i++) { stars[i].Active = false; stars[i].Renderer.enabled = false; }
    }

    private void Stop()
    {
        isPlaying = false;
        StopAllParticles();
        gameObject.SetActive(false);
        Action<OverheatShockWaveParticleRunner> callback = onStopped;
        onStopped = null;
        callback?.Invoke(this);
    }
}
