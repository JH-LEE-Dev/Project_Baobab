using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2스테이지 포자막 상태 연출. 렌더링만 바꾸고 게임 동작(포자막 수치·회복·판정)에는 영향이 없다.
///  - 막을 때리면: 막 결정이 하얗게 번쩍이고 맞은 수관에서 하늘색 결정 조각이 튄다.
///  - 막이 깎인 만큼: 결정이 어둡고 탁해진다(남은 막이 눈에 보인다).
///  - 막이 다시 차오르는 동안: 둘레에서 포자 알갱이가 수관으로 빨려 들어가고 결정 위로 빛 띠가 아래에서 위로 훑어 올라간다.
///  - 막이 가득 차는 순간: 결정이 한 번 밝게 맥동하고 작은 빛 알갱이가 퍼진다.
/// 결정 표현은 나무 셰이더(TreeSporeShield.hlsl)가 포자막 그림에서만 그리고, 값은 TreeVisualComponent.SetSporeShieldFx로 넣는다.
/// 알갱이는 씬에 파티클 시스템 둘(빨려 드는 알갱이, 튀는 조각)만 두고 Emit으로 뿌린다.
/// 맞았거나 회복 중인 나무만 목록에 올려 LateUpdate에서 틱하고, 다 가라앉으면 0으로 되돌린 뒤 뺀다.
/// 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고, 다시 켤 때는 이전 것을 지우고 새로 만든다.
/// 꺼진 곳·로그라이크에서는 아무것도 만들지 않는다(Notify는 아무 일도 하지 않는다).
/// </summary>
public class StageSporeShieldFx : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 회복 알림(EHealthComponent, 0.15초 간격) 사이에도 회복 중으로 보도록 마지막 알림 뒤 이만큼 더 유지한다(초).
    private const float RegenHoldTime = 0.25f;
    // 값을 이 단위로 끊어 넣어 같은 값이면 프로퍼티 블록을 다시 쓰지 않게 한다.
    private const float FxQuantize = 64f;
    // 깎인 정도를 보여 주는 값이 실제 값을 따라가는 빠르기(1/초)
    private const float DepletionFollowRate = 18f;
    // 화면 밖 판정 여유(월드)
    private const float ScreenMargin = 1.5f;

    private static StageSporeShieldFx current;

    private class Entry
    {
        public TreeVisualComponent visual;
        public IHealthComponent health;
        public float displayDepletion;
        public float flash;
        public float regen;
        public float regenUntil;
        public float pulse;
        public float moteAccum;
    }

    // //내부 의존성
    private SporeShieldFxSettings settings;
    private ParticleSystem moteSystem;
    private ParticleSystem shardSystem;
    private readonly List<Entry> entries = new List<Entry>(32);
    private readonly Dictionary<TreeVisualComponent, Entry> entryByVisual = new Dictionary<TreeVisualComponent, Entry>(32);
    private readonly Stack<Entry> entryPool = new Stack<Entry>(32);
    private int lastBurstFrame = -1;
    private int burstsThisFrame;
    private Rect screenRect;
    private int screenRectFrame = -1;

    // // 퍼블릭 제어 메서드
    public static void Apply(SporeShieldFxSettings _settings, string _sceneName)
    {
        Remove();

        // 타격·회복 알림은 플레이 중에만 오므로 에디터 맵 미리보기(편집 모드)에서는 만들지 않는다(편집 씬에 남지 않게).
        if (null == _settings || false == _settings.enabled || null == _settings.material || GameModeContext.IsRoguelike
            || false == Application.isPlaying)
        {
            return;
        }

        GameObject root = new GameObject("[StageSporeShieldFx]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageSporeShieldFx>();
        current.Setup(_settings);
    }

    public static void Remove()
    {
        if (null != current)
        {
            current.ClearAllEntries();
            if (Application.isPlaying)
            {
                Destroy(current.gameObject);
            }
            else
            {
                DestroyImmediate(current.gameObject);
            }
            current = null;
        }
    }

    /// <summary>나무가 맞았을 때(포자막이 남아 있을 때만 연출한다). 꺼진 곳에서는 아무 일도 하지 않는다.</summary>
    public static void NotifyHit(TreeVisualComponent _visual, IHealthComponent _health)
    {
        if (null != current)
        {
            current.OnHit(_visual, _health);
        }
    }

    /// <summary>
    /// 나무의 포자막이 깨졌을 때. 깨는 타격은 이미 막이 0이라 NotifyHit가 아무것도 하지 않으므로 여기서 결정 조각을 튀긴다.
    /// 막 그림은 깨지면 꺼지므로 번쩍임은 넣지 않는다. 꺼진 곳에서는 아무 일도 하지 않는다.
    /// </summary>
    public static void NotifyShieldBroken(TreeVisualComponent _visual)
    {
        if (null != current)
        {
            current.OnShieldBroken(_visual);
        }
    }

    /// <summary>나무의 포자막이 차오를 때(EHealthComponent 회복 알림). 꺼진 곳에서는 아무 일도 하지 않는다.</summary>
    public static void NotifyRecovering(TreeVisualComponent _visual, IHealthComponent _health)
    {
        if (null != current)
        {
            current.OnRecovering(_visual, _health);
        }
    }

    private void Setup(SporeShieldFxSettings _settings)
    {
        settings = _settings;

        // 빨려 드는 알갱이: 중력 없이 정해진 속도로 날아가 도착하는 순간 수명이 끝난다. 나타날 때 서서히 밝아지고 도착하며 사그라든다.
        Gradient moteFade = new Gradient();
        moteFade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        moteSystem = CreateSystem("Motes", 0f, 0f, moteFade);

        // 튀는 조각: 튄 뒤 빠르게 느려지며 떨어진다. 바로 나타났다가 끝에서 옅어진다.
        Gradient shardFade = new Gradient();
        shardFade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        shardSystem = CreateSystem("Shards", _settings.shardGravity, _settings.shardDampen, shardFade);
    }

    private ParticleSystem CreateSystem(string _name, float _gravity, float _dampen, Gradient _fade)
    {
        GameObject go = new GameObject(_name);
        go.transform.SetParent(transform, false);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, settings.maxParticles);
        main.gravityModifier = Mathf.Max(0f, _gravity) / Mathf.Max(0.0001f, Physics.gravity.magnitude);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        if (_dampen > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0f;
            limit.dampen = _dampen;
        }

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(_fade);

        ParticleSystemRenderer particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = settings.material;
        particleRenderer.sortingLayerName = settings.sortingLayerName;
        particleRenderer.sortingOrder = settings.sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 1f;

        system.Play();
        return system;
    }

    private void OnHit(TreeVisualComponent _visual, IHealthComponent _health)
    {
        if (false == HasShield(_visual, _health)) return;

        Entry entry = GetOrAddEntry(_visual, _health);
        entry.flash = 1f;

        Vector2 crown = GetCrownCenter(entry.visual);
        if (IsOnScreen(crown) && TryTakeBurst())
        {
            EmitShards(crown);
        }
    }

    private void OnShieldBroken(TreeVisualComponent _visual)
    {
        if (null == _visual || false == _visual.gameObject.activeInHierarchy) return;

        Vector2 crown = GetCrownCenter(_visual);
        if (IsOnScreen(crown) && TryTakeBurst())
        {
            EmitShards(crown);
        }
    }

    private void OnRecovering(TreeVisualComponent _visual, IHealthComponent _health)
    {
        if (false == HasShield(_visual, _health)) return;

        Entry entry = GetOrAddEntry(_visual, _health);

        // 가득 찬 순간의 알림(회복 완료 시점은 항상 알린다)이면 차오름을 끝내고 맥동을 한 번 낸다.
        if (_health.GetCurrentSP() >= _health.GetMaxSP())
        {
            entry.regenUntil = 0f;
            entry.pulse = 1f;

            Vector2 crown = GetCrownCenter(entry.visual);
            if (IsOnScreen(crown))
            {
                EmitPulseSparkles(crown);
            }
            return;
        }

        entry.regenUntil = Time.time + RegenHoldTime;
    }

    private static bool HasShield(TreeVisualComponent _visual, IHealthComponent _health)
    {
        if (null == _visual || null == _health || false == _visual.gameObject.activeInHierarchy) return false;

        return _health.GetMaxSP() > 0f && _health.GetCurrentSP() > 0f;
    }

    private Entry GetOrAddEntry(TreeVisualComponent _visual, IHealthComponent _health)
    {
        if (entryByVisual.TryGetValue(_visual, out Entry entry))
        {
            entry.health = _health;
            return entry;
        }

        entry = entryPool.Count > 0 ? entryPool.Pop() : new Entry();
        entry.visual = _visual;
        entry.health = _health;
        entry.displayDepletion = 1f - Mathf.Clamp01(_health.GetCurrentSP() / Mathf.Max(0.0001f, _health.GetMaxSP()));
        entry.flash = 0f;
        entry.regen = 0f;
        entry.regenUntil = 0f;
        entry.pulse = 0f;
        entry.moteAccum = 0f;

        entries.Add(entry);
        entryByVisual.Add(_visual, entry);
        return entry;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        float now = Time.time;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];

            // 풀로 돌아갔거나(꺼진다, ResetVisualState가 이미 0으로 되돌렸다) 막이 깨졌으면(포자막 그림이 꺼진다) 뺀다.
            if (null == entry.visual || false == entry.visual.gameObject.activeInHierarchy)
            {
                RemoveAt(i, false);
                continue;
            }

            IHealthComponent health = entry.health;
            float maxSP = null != health ? health.GetMaxSP() : 0f;
            float sp = null != health ? health.GetCurrentSP() : 0f;
            if (maxSP <= 0f || sp <= 0f)
            {
                RemoveAt(i, true);
                continue;
            }

            float targetDepletion = 1f - Mathf.Clamp01(sp / maxSP);
            entry.displayDepletion = Mathf.Lerp(entry.displayDepletion, targetDepletion, 1f - Mathf.Exp(-DepletionFollowRate * dt));
            if (Mathf.Abs(entry.displayDepletion - targetDepletion) < 0.002f) entry.displayDepletion = targetDepletion;

            entry.flash = Mathf.Max(0f, entry.flash - dt / Mathf.Max(0.01f, settings.hitFlashDuration));
            entry.pulse = Mathf.Max(0f, entry.pulse - dt / Mathf.Max(0.01f, settings.fullPulseDuration));

            bool bRegenerating = now < entry.regenUntil;
            float regenRate = bRegenerating ? 1f / Mathf.Max(0.01f, settings.regenFadeIn) : 1f / Mathf.Max(0.01f, settings.regenFadeOut);
            entry.regen = Mathf.MoveTowards(entry.regen, bRegenerating ? 1f : 0f, regenRate * dt);

            if (bRegenerating && settings.motesPerSecond > 0f)
            {
                entry.moteAccum += settings.motesPerSecond * dt;
                if (entry.moteAccum >= 1f)
                {
                    int count = Mathf.Min((int)entry.moteAccum, 2);
                    entry.moteAccum -= (int)entry.moteAccum;

                    Vector2 crown = GetCrownCenter(entry.visual);
                    if (IsOnScreen(crown))
                    {
                        EmitMotes(crown, count);
                    }
                }
            }

            // 번쩍임·맥동은 처음에 세고 빨리 잦아들게 제곱한다.
            Vector4 fx = new Vector4(
                Quantize(entry.displayDepletion),
                Quantize(entry.flash * entry.flash),
                Quantize(entry.regen),
                Quantize(entry.pulse * entry.pulse));
            entry.visual.SetSporeShieldFx(fx);

            if (fx == Vector4.zero && false == bRegenerating)
            {
                RemoveAt(i, false);
            }
        }
    }

    private static float Quantize(float _value)
    {
        return Mathf.Round(Mathf.Clamp01(_value) * FxQuantize) / FxQuantize;
    }

    private void RemoveAt(int _index, bool _bClearVisual)
    {
        Entry entry = entries[_index];
        if (_bClearVisual && null != entry.visual)
        {
            entry.visual.SetSporeShieldFx(Vector4.zero);
        }

        // 파괴된 나무도 같은 객체 참조라 그대로 지워진다(키 비교는 인스턴스 ID).
        entryByVisual.Remove(entry.visual);

        int last = entries.Count - 1;
        entries[_index] = entries[last];
        entries.RemoveAt(last);

        entry.visual = null;
        entry.health = null;
        entryPool.Push(entry);
    }

    private void ClearAllEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (null != entry.visual)
            {
                entry.visual.SetSporeShieldFx(Vector4.zero);
            }
        }
        entries.Clear();
        entryByVisual.Clear();
    }

    // 결정이 박힌 수관 가운데(나무 윗부분 기준점에서 오프셋)
    private Vector2 GetCrownCenter(TreeVisualComponent _visual)
    {
        return (Vector2)_visual.GetTopRootPosition() + settings.crownOffset;
    }

    private Vector2 RandomInCrown(Vector2 _crown)
    {
        Vector2 p = Random.insideUnitCircle;
        return _crown + new Vector2(p.x * settings.crownRadius.x, p.y * settings.crownRadius.y);
    }

    private void EmitMotes(Vector2 _crown, int _count)
    {
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        for (int k = 0; k < _count; k++)
        {
            Vector2 target = RandomInCrown(_crown);
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            float radius = Random.Range(settings.moteSpawnRadiusRange.x, settings.moteSpawnRadiusRange.y);
            Vector2 start = _crown + new Vector2(dir.x * radius, dir.y * radius * settings.moteSpawnFlatten);
            float lifetime = Random.Range(settings.moteLifetimeRange.x, settings.moteLifetimeRange.y);
            Vector2 velocity = (target - start) / Mathf.Max(0.05f, lifetime);

            emitParams.position = new Vector3(start.x, start.y, 0f);
            emitParams.velocity = new Vector3(velocity.x, velocity.y, 0f);
            emitParams.startLifetime = lifetime;
            emitParams.startSize = RandomSizePixels(settings.moteSizePixelsRange) / PixelsPerUnit;
            emitParams.startColor = Color.Lerp(settings.moteColorA, settings.moteColorB, Random.value);
            moteSystem.Emit(emitParams, 1);
        }
    }

    private void EmitShards(Vector2 _crown)
    {
        int count = Random.Range(settings.shardCountRange.x, Mathf.Max(settings.shardCountRange.x, settings.shardCountRange.y) + 1);
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        for (int k = 0; k < count; k++)
        {
            Vector2 origin = RandomInCrown(_crown);
            Vector2 direction = ((origin - _crown).normalized + Random.insideUnitCircle * 0.6f + Vector2.up * settings.shardUpwardBias).normalized;
            float speed = Random.Range(settings.shardSpeedRange.x, settings.shardSpeedRange.y);

            emitParams.position = new Vector3(origin.x, origin.y, 0f);
            emitParams.velocity = new Vector3(direction.x * speed, direction.y * speed, 0f);
            emitParams.startLifetime = Random.Range(settings.shardLifetimeRange.x, settings.shardLifetimeRange.y);
            emitParams.startSize = RandomSizePixels(settings.shardSizePixelsRange) / PixelsPerUnit;
            emitParams.startColor = Color.Lerp(settings.shardColorA, settings.shardColorB, Random.value);
            shardSystem.Emit(emitParams, 1);
        }
    }

    // 가득 찬 순간: 결정 자리에서 빛 알갱이가 사방으로 천천히 퍼진다(조각 시스템을 쓰되 느리게 띄운다).
    private void EmitPulseSparkles(Vector2 _crown)
    {
        int count = Mathf.Max(0, settings.pulseSparkleCount);
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        float angleOffset = Random.value * Mathf.PI * 2f;
        for (int k = 0; k < count; k++)
        {
            float angle = angleOffset + (k + Random.Range(-0.3f, 0.3f)) * (Mathf.PI * 2f / Mathf.Max(1, count));
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * settings.moteSpawnFlatten + 0.35f).normalized;
            Vector2 origin = _crown + new Vector2(direction.x * settings.crownRadius.x, direction.y * settings.crownRadius.y) * 0.6f;
            float speed = Random.Range(settings.pulseSparkleSpeedRange.x, settings.pulseSparkleSpeedRange.y);

            emitParams.position = new Vector3(origin.x, origin.y, 0f);
            emitParams.velocity = new Vector3(direction.x * speed, direction.y * speed, 0f);
            emitParams.startLifetime = Random.Range(settings.pulseSparkleLifetimeRange.x, settings.pulseSparkleLifetimeRange.y);
            emitParams.startSize = 1f / PixelsPerUnit;
            emitParams.startColor = Color.Lerp(settings.moteColorA, Color.white, 0.5f);
            shardSystem.Emit(emitParams, 1);
        }
    }

    private static int RandomSizePixels(Vector2Int _range)
    {
        return Mathf.Max(1, Random.Range(_range.x, Mathf.Max(_range.x, _range.y) + 1));
    }

    // 광역 타격으로 한 프레임에 많은 나무가 맞아도 정해진 수만큼만 조각을 튀게 한다.
    private bool TryTakeBurst()
    {
        int frame = Time.frameCount;
        if (frame != lastBurstFrame)
        {
            lastBurstFrame = frame;
            burstsThisFrame = 0;
        }

        if (burstsThisFrame >= Mathf.Max(1, settings.maxHitBurstsPerFrame)) return false;
        burstsThisFrame++;
        return true;
    }

    private bool IsOnScreen(Vector2 _position)
    {
        int frame = Time.frameCount;
        if (frame != screenRectFrame)
        {
            screenRectFrame = frame;
            Camera cam = Camera.main;
            if (null != cam && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize + ScreenMargin;
                float halfWidth = cam.orthographicSize * cam.aspect + ScreenMargin;
                Vector3 c = cam.transform.position;
                screenRect = new Rect(c.x - halfWidth, c.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
            }
            else
            {
                screenRect = new Rect(float.MinValue * 0.5f, float.MinValue * 0.5f, float.MaxValue, float.MaxValue);
            }
        }

        return screenRect.Contains(_position);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
