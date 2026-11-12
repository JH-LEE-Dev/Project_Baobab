using UnityEngine;

/// <summary>
/// 지진 흙먼지·파편·잔돌 낙하(본편 던전 지진). QuakeTileRipple 앞머리를 따라 바닥에서 흙먼지가 피어오르고 돌 부스러기가 튀어 오르며,
/// 터진 뒤 잠깐 화면 위쪽에서 잔돌·먼지가 떨어진다. 모두 도트 사각형 입자(텍스처 없는 재질 - 흰 사각형에 입자 색)다.
///
/// [보이기만 한다]
///   입자는 연출 전용 파티클 시스템 셋에 직접 낸다(EmitParams). 게임 판정·물리와 상관이 없고, 위치·개수를 정하는 난수도
///   따로 둔 System.Random이라 게임 난수(UnityEngine.Random)의 흐름을 바꾸지 않는다.
///
/// [화면 안의 물결 칸에서만]
///   앞머리는 진원에서 바닥 평면 거리 R인 타원(아이소라 세로가 절반)이다. 타원 위 아무 곳이나 골라, 화면 안이고 물결이 닿는 바닥 칸(물·영역 밖이 아닌)일 때만 낸다.
///   앞머리가 커질수록 타원 둘레가 길어지므로 내는 빈도를 반지름에 비례해 늘려 화면에 보이는 띠의 밀도를 같게 한다. 한 프레임에 시도하는 수는 MaxAttemptsPerFrame까지다.
/// </summary>
public sealed class QuakeDebrisEffect
{
    private const float PixelsPerUnit = 32f;
    // 앞머리 반지름이 이 값일 때 dustPerSecond만큼 낸다(그보다 크면 비례해 늘어난다).
    private const float DustReferenceRadius = 4f;
    private const int MaxAttemptsPerFrame = 48;
    private const int MaxFallPerFrame = 8;
    // 입자 중력 배율(물리 중력 9.81 기준). 부스러기·잔돌은 묵직하게 떨어진다.
    private const float ChipGravity = 1.5f;
    private const float FallGravity = 0.9f;

    private QuakeTileRippleSettings settings;
    private GameObject root;
    private ParticleSystem dust;
    private ParticleSystem chips;
    private ParticleSystem fall;
    private readonly System.Random random = new System.Random(0x51A4E);

    private QuakeTileRipple ripple;
    private bool bFrontActive;
    private float dustBudget;
    private float fallElapsed = -1f;
    private float fallBudget;

    public void Initialize(QuakeTileRippleSettings _settings, Transform _parent)
    {
        settings = _settings;
        if (null == settings || null == settings.debrisMaterial || null != root) return;

        root = new GameObject("QuakeDebris");
        root.transform.SetParent(_parent, false);
        root.transform.position = Vector3.zero;

        dust = CreateSystem("Dust", "ShockWave", 10, 0f, 0.6f);
        chips = CreateSystem("Chips", "ShockWave", 11, ChipGravity, 0f);
        fall = CreateSystem("Fall", "FlyingItem", 10, FallGravity, 0f);
        // 지진이 없을 때는 꺼 둔다(빈 시스템도 매 프레임 갱신된다). 터질 때 켜고, 다 내고 입자가 모두 사라지면 다시 끈다.
        root.SetActive(false);
    }

    public void Release()
    {
        Stop();
        if (null != root) DestroyObject(root);
        root = null;
        dust = null;
        chips = null;
        fall = null;
    }

    /// <summary>물결이 퍼지기 시작한다(터지는 순간). 앞머리 흙먼지를 내기 시작하고 잔돌 낙하를 시작한다.</summary>
    public void Play(QuakeTileRipple _ripple)
    {
        if (null == root) return;

        ripple = _ripple;
        bFrontActive = null != ripple;
        dustBudget = 0f;
        fallElapsed = settings.fallSeconds > 0f && settings.fallPerSecond > 0f ? 0f : -1f;
        fallBudget = 0f;
        SetRunning(bFrontActive || fallElapsed >= 0f);
    }

    /// <summary>바로 끝낸다(떠 있는 입자도 지운다). 맵을 새로 깔거나 던전을 떠날 때 부른다.</summary>
    public void Stop()
    {
        bFrontActive = false;
        ripple = null;
        fallElapsed = -1f;
        if (null != dust) dust.Clear();
        if (null != chips) chips.Clear();
        if (null != fall) fall.Clear();
        SetRunning(false);
    }

    /// <summary>매 프레임(게임 시간). 떠 있는 입자는 파티클 시스템이 스스로 움직인다(게임 시간 - 일시정지·히트스톱에 함께 멈춘다).</summary>
    public void Tick(float _deltaTime)
    {
        if (null == root || false == root.activeSelf || _deltaTime <= 0f) return;

        if (bFrontActive || fallElapsed >= 0f)
        {
            Camera camera = Camera.main;
            if (null == camera || false == camera.orthographic) return;

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 view = camera.transform.position;
            Rect viewRect = new Rect(view.x - halfWidth, view.y - halfHeight, halfWidth * 2f, halfHeight * 2f);

            TickFront(_deltaTime, viewRect);
            TickFall(_deltaTime, viewRect);
            return;
        }

        // 다 냈고 떠 있는 입자도 모두 사라졌으면 끈다.
        if (0 == dust.particleCount && 0 == chips.particleCount && 0 == fall.particleCount) SetRunning(false);
    }

    /// <summary>입자 시스템 묶음을 켜고(시뮬레이션 시작) 끈다. 켤 때 다시 Play한다(꺼진 동안 멈춘 시스템은 켜도 스스로 돌지 않는다).</summary>
    private void SetRunning(bool _bOn)
    {
        if (null == root || root.activeSelf == _bOn) return;

        root.SetActive(_bOn);
        if (false == _bOn) return;

        dust.Play();
        chips.Play();
        fall.Play();
    }

    // // 앞머리 흙먼지·부스러기

    private void TickFront(float _deltaTime, Rect _view)
    {
        if (false == bFrontActive) return;
        if (null == ripple || false == ripple.IsPlaying)
        {
            bFrontActive = false;
            return;
        }

        float radius = ripple.FrontRadius;
        if (radius >= ripple.ReachRadius)
        {
            bFrontActive = false;
            return;
        }
        if (radius < 0.3f) return;

        dustBudget += settings.dustPerSecond * Mathf.Max(1f, radius / DustReferenceRadius) * _deltaTime;
        int attempts = Mathf.Min(MaxAttemptsPerFrame, (int)dustBudget);
        dustBudget -= attempts;
        // 못 낸 몫을 쌓아 두지 않는다(멈췄다 풀리는 프레임에 한꺼번에 나오지 않게).
        if (dustBudget > MaxAttemptsPerFrame) dustBudget = 0f;

        Vector3 center = ripple.Center;
        for (int i = 0; i < attempts; i++)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            // 앞머리 줄 폭 안에서 조금씩 뒤로(이미 지나간 쪽) 흩는다.
            float r = radius - Range(0f, 0.35f);
            Vector3 ground = new Vector3(center.x + Mathf.Cos(angle) * r, center.y + Mathf.Sin(angle) * r * 0.5f, 0f);
            if (false == _view.Contains(ground) || false == ripple.TryGetRipplePixels(ground, out int groundPixels)) continue;

            ground.y += groundPixels / PixelsPerUnit;
            Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f);
            // 한 자리에서 두세 알이 함께 피어 작은 먼지 뭉치로 보이게 한다(한 알씩이면 화면에 흩뿌린 점처럼 보인다).
            int puff = random.Next(2, 4);
            for (int k = 0; k < puff; k++) EmitDust(ground, outward);
            if (Range(0f, 1f) < settings.chipChance) EmitChip(ground, outward);
        }
    }

    private void EmitDust(Vector3 _ground, Vector2 _outward)
    {
        // 앞머리 바로 뒤에 짧게 피었다 사라진다(오래 남으면 물결 안쪽이 먼지 가루로 덮인다).
        Color color = settings.dustColor;
        float shade = Range(0.88f, 1.1f);
        color = new Color(color.r * shade, color.g * shade, color.b * shade, color.a * Range(0.7f, 1f));

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = _ground + new Vector3(Range(-0.12f, 0.12f), Range(0f, 0.1f), 0f),
            velocity = new Vector3(_outward.x * Range(0.3f, 0.9f), Range(0.7f, 1.6f) + _outward.y * 0.4f, 0f),
            startLifetime = Range(0.28f, 0.5f),
            startSize = PixelSize(2, 4),
            startColor = color,
        };
        dust.Emit(emit, 1);
    }

    private void EmitChip(Vector3 _ground, Vector2 _outward)
    {
        float up = Range(2.2f, 4.4f);
        float gravity = 9.81f * ChipGravity;
        Color color = settings.chipColor;
        float shade = Range(0.8f, 1.25f);

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = _ground,
            velocity = new Vector3(_outward.x * Range(0.4f, 1.5f), up, 0f),
            // 튀어 오른 높이에서 다시 제 바닥 높이로 돌아오는 시간.
            startLifetime = 2f * up / gravity,
            startSize = PixelSize(1, 2),
            startColor = new Color(color.r * shade, color.g * shade, color.b * shade, color.a),
        };
        chips.Emit(emit, 1);
    }

    // // 잔돌 낙하

    private void TickFall(float _deltaTime, Rect _view)
    {
        if (fallElapsed < 0f) return;

        fallElapsed += _deltaTime;
        float t = fallElapsed / Mathf.Max(0.01f, settings.fallSeconds);
        if (t >= 1f)
        {
            fallElapsed = -1f;
            return;
        }

        // 터진 직후 가장 많이 떨어지고 갈수록 준다.
        fallBudget += settings.fallPerSecond * (1f - t) * (1f - t) * _deltaTime;
        int count = Mathf.Min(MaxFallPerFrame, (int)fallBudget);
        fallBudget -= count;
        if (fallBudget > MaxFallPerFrame) fallBudget = 0f;

        float gravity = 9.81f * FallGravity;
        for (int i = 0; i < count; i++)
        {
            bool bPebble = Range(0f, 1f) < 0.65f;
            Vector3 start = new Vector3(Range(_view.xMin, _view.xMax), _view.yMax + Range(0.05f, 0.4f), 0f);
            // 화면 안 아무 높이에 떨어져 사라진다(바닥에 닿은 것처럼). 화면 위쪽 절반에 더 많이 떨어진다.
            float landY = Mathf.Lerp(_view.yMax, _view.yMin, Mathf.Sqrt(Range(0.05f, 1f)) * 0.9f);
            float drop = start.y - landY;
            float speed = bPebble ? Range(0.5f, 1.8f) : Range(0.2f, 0.6f);
            float g = bPebble ? gravity : gravity * 0.25f;
            float lifetime = (-speed + Mathf.Sqrt(speed * speed + 2f * g * drop)) / g;

            Color color = bPebble ? settings.chipColor : settings.dustColor;
            float shade = Range(0.85f, 1.15f);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = start,
                velocity = new Vector3(Range(-0.15f, 0.15f), -speed, 0f),
                startLifetime = lifetime,
                startSize = bPebble ? PixelSize(2, 3) : PixelSize(1, 2),
                startColor = new Color(color.r * shade, color.g * shade, color.b * shade, bPebble ? 1f : color.a * 0.8f),
            };

            if (bPebble)
            {
                fall.Emit(emit, 1);
            }
            else
            {
                // 먼지는 천천히 내려앉는다: 중력이 약한 흙먼지 층에 낸다(흙먼지 층은 중력이 없어 처음 속도로 내려간다).
                emit.velocity = new Vector3(Range(-0.15f, 0.15f), -Range(0.6f, 1.2f), 0f);
                emit.startLifetime = Range(0.7f, 1.2f);
                dust.Emit(emit, 1);
            }
        }
    }

    // // 공통

    private ParticleSystem CreateSystem(string _name, string _layer, int _order, float _gravity, float _drag)
    {
        GameObject go = new GameObject(_name);
        go.transform.SetParent(root.transform, false);
        go.SetActive(false);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        // 계속 도는 채로 두고(낼 때만 Emit) 스스로 내는 것은 끈다. 멈춘 시스템에 낸 입자는 움직이지 않을 수 있다.
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startSpeed = 0f;
        main.startRotation = 0f;
        main.gravityModifier = _gravity;
        main.maxParticles = 1500;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // 끝으로 갈수록 옅어진다(부스러기·잔돌은 마지막에만 짧게).
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        float fadeStart = _gravity > 0f ? 0.85f : 0.35f;
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        if (_drag > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = _drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = settings.debrisMaterial;
        renderer.sortingLayerID = SortingLayer.NameToID(_layer);
        renderer.sortingOrder = _order;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 1f;

        go.SetActive(true);
        system.Play();
        return system;
    }

    private float Range(float _min, float _max) => _min + (float)random.NextDouble() * (_max - _min);

    // _min~_max 픽셀(정수) 크기의 사각형.
    private float PixelSize(int _min, int _max) => random.Next(_min, _max + 1) / PixelsPerUnit;

    private static void DestroyObject(UnityEngine.Object _object)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(_object);
        else UnityEngine.Object.DestroyImmediate(_object);
    }
}
