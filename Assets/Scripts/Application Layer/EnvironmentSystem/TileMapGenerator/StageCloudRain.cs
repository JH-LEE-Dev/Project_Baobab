using UnityEngine;

/// <summary>
/// 큰 구름에서만 내리는 비. 구름 그림자(StageCloudShadow 셰이더)와 같은 식을 CPU에서 계산해,
/// 둘레 insetRadius 안이 모두 구름 그림자인 자리(큰 구름의 속)를 찾고, 그 자리에서 insetRadius 안의 구름 그림자 땅에 빗줄기를 흩뿌린다.
/// 그래서 큰 구름은 가장자리 가까이까지 비가 내리고, 그런 속이 없는 작은 구름에서는 내리지 않는다.
/// 빗줄기는 화면 위 밖(하늘)에서 그 땅을 향해 화면 아래로 떨어져, 구름과 같이 흘러가며 땅에 닿을 때 사라진다.
/// 땅에 닿는 순간 그 자리에 작은 물방울이 튄다(빗줄기 파티클이 죽을 때 엔진이 띄우는 서브 이미터).
/// 빗줄기 파티클 시스템 하나에 Emit으로 뿌린다. 빗줄기·물방울 그림(작은 텍스처)은 코드로 만든다.
/// StageCloudShadows가 자기 아래에 만들고 지우므로 따로 켜고 끌 일이 없다. 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// </summary>
public class StageCloudRain : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 한 프레임에 고를 수 있는 자리 수 상한(프레임이 크게 밀렸을 때 한꺼번에 몰리지 않게)
    private const int MaxAttemptsPerFrame = 400;
    // 큰 구름 판정에 쓰는 둘레 점 수
    private const int RingSamples = 8;
    // 화면 아래 밖에서 땅 자리를 더 고르는 거리(월드)
    private const float GroundMarginBelowScreen = 2f;
    // 땅에 닿을 때 튀는 물방울 그림(픽셀)
    // 로그라이크 타일 파도의 물가 물보라(RoguelikeTileWaveFeedback)도 같은 그림을 쓴다.
    internal const int SplashFrameWidth = 7;
    internal const int SplashArtRows = 4;
    internal const int SplashFrameCount = 3;

    // //내부 의존성
    private CloudRainSettings settings;
    private ParticleSystem rainSystem;
    private Material materialInstance;
    private Texture2D streakTexture;
    private Material splashMaterial;
    private Texture2D splashTexture;

    // 구름 값(StageCloudShadows가 머티리얼에 넣는 것과 같은 정리)
    private Vector2 cloudDir;
    private float cloudSpeed;
    private float cloudScale;
    private float cloudThreshold;

    private float attemptAccum;
    private Vector2[] ring;

    // 한 시각의 구름 이동량(같은 프레임의 모든 자리에서 같아 시각이 바뀔 때만 다시 계산한다)
    private float cachedTime = float.NaN;
    private float driftX;
    private float driftY;
    private float slideX;
    private float slideY;

    // // 퍼블릭 제어 메서드
    public void Setup(CloudRainSettings _rain, CloudShadowSettings _cloud)
    {
        settings = _rain;

        cloudDir = _cloud.direction.sqrMagnitude > 0.0001f ? _cloud.direction.normalized : Vector2.right;
        cloudSpeed = _cloud.speed;
        cloudScale = Mathf.Max(0.5f, _cloud.scale);
        cloudThreshold = Mathf.Lerp(0.8f, 0.3f, Mathf.Clamp01(_cloud.coverage));

        // 아이소메트릭 바닥에 맞게 세로는 절반인 둘레
        float radius = Mathf.Max(0f, _rain.insetRadius);
        ring = new Vector2[RingSamples];
        for (int k = 0; k < RingSamples; k++)
        {
            float angle = k * Mathf.PI * 2f / RingSamples;
            ring[k] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.5f);
        }

        int streak = Mathf.Clamp(_rain.streakPixels, 2, 12);
        streakTexture = CreateStreakTexture(streak);
        materialInstance = new Material(_rain.material);
        materialInstance.mainTexture = streakTexture;

        rainSystem = gameObject.AddComponent<ParticleSystem>();
        rainSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = rainSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, _rain.maxParticles);
        main.gravityModifier = 0f;
        main.startSize3D = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = rainSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = rainSystem.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = materialInstance;
        particleRenderer.sortingLayerName = _rain.sortingLayerName;
        particleRenderer.sortingOrder = _rain.sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 1f;

        SetupSplash(streak);

        rainSystem.Play();
    }

    // 땅에 닿을 때 튀는 물방울. 빗줄기는 땅에 닿는 순간 수명이 끝나므로, 파티클 시스템의 죽을 때 서브 이미터로
    // 그 자리에 물방울 그림을 하나 띄운다. 생성·위치 계산을 엔진이 하므로 스크립트에서 매 프레임 할 일이 없다.
    private void SetupSplash(int _streak)
    {
        // 빗줄기 판과 가운데가 같은 판에 그림을 아래쪽에 그려, 빗줄기가 죽은 자리(빗줄기 가운데)에서 그림 바닥이 땅(빗줄기 아래 끝)에 오게 한다.
        int frameHeight = Mathf.Max(_streak, SplashArtRows);
        splashTexture = CreateSplashTexture(frameHeight);
        splashMaterial = new Material(settings.material);
        splashMaterial.mainTexture = splashTexture;

        GameObject splashObject = new GameObject("[Splash]");
        splashObject.transform.SetParent(transform, false);
        ParticleSystem splashSystem = splashObject.AddComponent<ParticleSystem>();
        splashSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = splashSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, settings.maxParticles / 4);
        main.gravityModifier = 0f;
        main.startSpeed = 0f;
        main.startLifetime = Mathf.Max(0.05f, settings.splashDuration);
        main.startSize3D = true;
        main.startSizeX = SplashFrameWidth / PixelsPerUnit;
        main.startSizeY = frameHeight / PixelsPerUnit;
        main.startSizeZ = 1f;
        main.startColor = settings.splashColor;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        // 서브 이미터로 불릴 때마다 하나씩
        ParticleSystem.EmissionModule emission = splashSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = splashSystem.shape;
        shape.enabled = false;

        ParticleSystem.TextureSheetAnimationModule sheet = splashSystem.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = SplashFrameCount;
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.cycleCount = 1;

        ParticleSystemRenderer splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
        splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        splashRenderer.alignment = ParticleSystemRenderSpace.View;
        splashRenderer.sharedMaterial = splashMaterial;
        splashRenderer.sortingLayerName = settings.sortingLayerName;
        splashRenderer.sortingOrder = settings.sortingOrder;
        splashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        splashRenderer.receiveShadows = false;
        splashRenderer.minParticleSize = 0f;
        splashRenderer.maxParticleSize = 1f;

        ParticleSystem.SubEmittersModule subEmitters = rainSystem.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(splashSystem, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
    }

    // 7픽셀 폭 물방울 그림 3장(아래 4줄에 그린다): 땅에 부딪힘 → 양옆 위로 튐 → 바깥으로 떨어지며 옅어짐.
    internal static Texture2D CreateSplashTexture(int _frameHeight)
    {
        int width = SplashFrameWidth * SplashFrameCount;
        Texture2D texture = new Texture2D(width, _frameHeight, TextureFormat.RGBA32, false, false);
        texture.name = "CloudRainSplash";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        Color32[] pixels = new Color32[width * _frameHeight];
        // 프레임별 칠할 픽셀(프레임 안 x, y. y는 아래가 0)과 진하기
        int[][] frames =
        {
            new[] { 3, 0, 2, 1, 4, 1 },
            new[] { 1, 2, 5, 2, 3, 3, 2, 0, 4, 0 },
            new[] { 0, 1, 6, 1, 1, 0, 5, 0 },
        };
        byte[] alpha = { 255, 230, 140 };
        for (int f = 0; f < SplashFrameCount; f++)
        {
            int[] p = frames[f];
            for (int k = 0; k + 1 < p.Length; k += 2)
            {
                pixels[(f * SplashFrameWidth + p[k]) + p[k + 1] * width] = new Color32(255, 255, 255, alpha[f]);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // 1픽셀 폭 빗줄기: 아래(앞머리)가 진하고 위(꼬리)로 갈수록 옅다.
    private static Texture2D CreateStreakTexture(int _length)
    {
        Texture2D texture = new Texture2D(1, _length, TextureFormat.RGBA32, false, false);
        texture.name = "CloudRainStreak";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        Color32[] pixels = new Color32[_length];
        for (int y = 0; y < _length; y++)
        {
            float t = _length > 1 ? (float)y / (_length - 1) : 0f;
            pixels[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 90f, t)));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void LateUpdate()
    {
        if (null == rainSystem)
        {
            return;
        }

        EmitUnderBigClouds();
    }

    private void EmitUnderBigClouds()
    {
        Camera cam = Camera.main;
        if (null == cam || false == cam.orthographic)
        {
            return;
        }

        // 빗줄기는 화면 위 밖(하늘)에서 시작해 화면을 가로질러 땅에 닿는다.
        // 화면 바로 아래 밖 그림자로 떨어지는 빗줄기도 화면을 지나가므로 땅 자리는 화면보다 조금 아래까지 고른다.
        float halfWidth = cam.orthographicSize * cam.aspect + 0.5f;
        float bottom = -cam.orthographicSize - GroundMarginBelowScreen;
        float top = cam.orthographicSize;
        float area = halfWidth * 2f * (top - bottom);

        // 로그라이크 지형 전환 때는 비의 양이 연출 세기를 따른다(StageAmbienceFade, 본편은 늘 1).
        attemptAccum += Mathf.Max(0f, settings.searchesPerSecondPerArea) * area * StageAmbienceFade.Visibility * Time.deltaTime;
        int attempts = Mathf.Min((int)attemptAccum, MaxAttemptsPerFrame);
        attemptAccum -= (int)attemptAccum;
        if (attempts <= 0)
        {
            return;
        }

        Vector2 center = cam.transform.position;
        // 셰이더의 _Time.y와 같은 시간. URP는 플레이 중 _Time.y에 Time.time을 넣는다
        // (Time.timeSinceLevelLoad는 씬을 불러올 때마다 0으로 돌아가 구름 그림자와 어긋난다).
        float time = Time.time;
        float speed = Mathf.Max(0.1f, settings.fallSpeed);
        Vector2 drift = cloudDir * cloudSpeed;
        Vector3 size = new Vector3(1f / PixelsPerUnit, Mathf.Clamp(settings.streakPixels, 2, 12) / PixelsPerUnit, 1f);
        float radius = Mathf.Max(0f, settings.insetRadius);
        int dropsPerHit = Mathf.Max(1, settings.dropsPerHit);
        float skyTop = center.y + cam.orthographicSize;
        float startMin = Mathf.Max(0f, settings.startAboveScreen.x);
        float startMax = Mathf.Max(startMin, settings.startAboveScreen.y);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.startSize3D = size;
        emitParams.startColor = settings.color;
        for (int k = 0; k < attempts; k++)
        {
            Vector2 core = center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(bottom, top));
            if (false == IsInsideBigCloud(core, time))
            {
                continue;
            }

            // 큰 구름 속 자리에서 둘레 안(세로는 절반) 구름 그림자 땅에 흩뿌린다.
            for (int d = 0; d < dropsPerHit; d++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector2 ground = core + new Vector2(offset.x, offset.y * 0.5f);
                if (false == IsCloudShadow(ground, time))
                {
                    continue;
                }
                // 로그라이크 지형 전환에서 새 연출이 파도를 따라 번져 켜지는 동안은 켜진 자리에만 띄운다(StageAmbienceFade, 본편은 늘 true).
                if (false == StageAmbienceFade.ShowsAt(ground))
                {
                    continue;
                }

                // 화면 위 밖에서 시작해 땅에 닿는 순간 사라지도록 수명을 떨어지는 시간에 맞춘다. 그동안 구름과 같이 흘러간다.
                float height = skyTop + Random.Range(startMin, startMax) - ground.y;
                emitParams.position = new Vector3(ground.x, ground.y + height + size.y * 0.5f, 0f);
                emitParams.velocity = new Vector3(drift.x, drift.y - speed, 0f);
                emitParams.startLifetime = height / speed;
                rainSystem.Emit(emitParams, 1);
            }
        }
    }

    // 그 자리와 둘레가 모두 구름 그림자 안이면 큰 구름의 안쪽이다.
    private bool IsInsideBigCloud(Vector2 _position, float _time)
    {
        if (false == IsCloudShadow(_position, _time))
        {
            return false;
        }

        for (int k = 0; k < RingSamples; k++)
        {
            if (false == IsCloudShadow(_position + ring[k], _time))
            {
                return false;
            }
        }
        return true;
    }

    // // StageCloudShadow.shader의 구름 노이즈를 그대로 옮긴 것(0~1, 덮는 비율 문턱 이상이면 그림자).
    private static float Frac(float _x)
    {
        return _x - Mathf.Floor(_x);
    }

    private static float CloudHash21(float _x, float _y)
    {
        float px = Frac(_x * 0.1031f);
        float py = Frac(_y * 0.1031f);
        float pz = px;
        float d = px * (py + 33.33f) + py * (pz + 33.33f) + pz * (px + 33.33f);
        px += d;
        py += d;
        pz += d;
        return Frac((px + py) * pz);
    }

    private static float CloudNoise(float _x, float _y)
    {
        float ix = Mathf.Floor(_x);
        float iy = Mathf.Floor(_y);
        float fx = _x - ix;
        float fy = _y - iy;
        float ux = fx * fx * (3f - 2f * fx);
        float uy = fy * fy * (3f - 2f * fy);
        float a = CloudHash21(ix, iy);
        float b = CloudHash21(ix + 1f, iy);
        float c = CloudHash21(ix, iy + 1f);
        float d = CloudHash21(ix + 1f, iy + 1f);
        return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
    }

    // 그 자리가 구름 그림자 안인지(셰이더의 세 겹 노이즈 합이 덮는 비율 문턱 이상인지).
    // 겹마다 노이즈는 0~1이라 남은 겹을 모두 1로 쳐도 문턱에 못 미치면 그 자리에서 끝낸다(결과는 세 겹을 다 더한 것과 같다).
    private bool IsCloudShadow(Vector2 _position, float _time)
    {
        if (_time != cachedTime)
        {
            cachedTime = _time;
            driftX = cloudDir.x * (_time * cloudSpeed);
            driftY = cloudDir.y * (_time * cloudSpeed);
            float slideScale = _time * cloudSpeed / cloudScale;
            slideX = cloudDir.x * slideScale;
            slideY = cloudDir.y * slideScale;
        }

        // 아이소메트릭 바닥: 세로를 두 배로 늘린 뒤 덩어리 크기로 나누고 첫 겹 방향(turnA)으로 튼다.
        float px = (_position.x - driftX) / cloudScale;
        float py = (_position.y - driftY) * 2f / cloudScale;
        float qx = 0.8f * px + -0.6f * py;
        float qy = 0.6f * px + 0.8f * py;

        // 남은 겹 최댓값(0.45, 0.15)에 반올림 여유를 조금 더해 문턱 바로 옆 자리 판정이 바뀌지 않게 한다.
        float n = CloudNoise(qx, qy) * 0.55f;
        if (n + 0.4501f < cloudThreshold)
        {
            return false;
        }

        float bx = 0.28f * qx + -0.96f * qy;
        float by = 0.96f * qx + 0.28f * qy;
        n += CloudNoise(bx * 2.03f + slideX * 0.4f + 17.3f, by * 2.03f + slideY * 0.4f + 17.3f) * 0.3f;
        if (n + 0.1501f < cloudThreshold)
        {
            return false;
        }

        float ax = 0.8f * qx + -0.6f * qy;
        float ay = 0.6f * qx + 0.8f * qy;
        n += CloudNoise(ax * 4.07f - slideX * 0.6f + 41.7f, ay * 4.07f - slideY * 0.6f + 41.7f) * 0.15f;
        return n >= cloudThreshold;
    }

    private void OnDestroy()
    {
        if (null != materialInstance)
        {
            Destroy(materialInstance);
        }
        if (null != streakTexture)
        {
            Destroy(streakTexture);
        }
        if (null != splashMaterial)
        {
            Destroy(splashMaterial);
        }
        if (null != splashTexture)
        {
            Destroy(splashTexture);
        }
    }
}
