using UnityEngine;

/// <summary>
/// 돌풍에 실려 날아가는 꽃잎·잎. 돌풍(WindGust.hlsl)이 지금 지나가는 자리에서만 작은 꽃잎과 잎이 생겨,
/// 돌풍과 같은 방향·비슷한 속도로 날아가며 뒤집히듯 모양이 바뀌다(4x4 픽셀 그림 4장) 사라진다.
/// 돌풍 자리는 셰이더(SampleWindGust)와 같은 식을 CPU에서 그대로 계산해, 풀이 눕고 나무가 흔들리는 바로 그 띠에서만 꽃잎이 난다.
/// 씬에 파티클 시스템 하나만 두고 Emit으로 뿌린다. 꽃잎 그림(작은 텍스처)은 코드로 만든다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳·돌풍이 없는 곳·로그라이크에서는 아무것도 만들지 않는다.
/// </summary>
public class StageWindPetals : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 꽃잎 그림 한 장 크기(픽셀)와 장 수
    private const int FramePixels = 4;
    private const int FrameCount = 4;
    // 한 프레임에 고를 수 있는 자리 수 상한(프레임이 크게 밀렸을 때 한꺼번에 몰리지 않게)
    private const int MaxAttemptsPerFrame = 24;

    private static StageWindPetals current;

    // //내부 의존성
    private WindPetalSettings settings;
    private ParticleSystem petalSystem;
    private Material materialInstance;
    private Texture2D petalTexture;

    // 돌풍 값(StageAmbienceShaderGlobals.ApplyWindGust와 같은 정리)
    private Vector2 gustDir;
    private float gustSpeed;
    private float gustSpacing;
    private float gustHalfWidth;
    private float gustPatchSize;
    private float gustCoverage;
    private float gustGroupLength;
    private float gustSmallScale;

    private float attemptAccum;

    // // 퍼블릭 제어 메서드
    public static void Apply(WindPetalSettings _settings, WindGustSettings _gust, string _sceneName)
    {
        Remove();

        // 바람이 지나갈 때만 생기는 연출이라 플레이 중에만 만든다(에디터 맵 미리보기에서는 만들지 않는다).
        if (null == _settings || false == _settings.enabled || null == _settings.material
            || null == _gust || false == _gust.enabled || GameModeContext.IsRoguelike || false == Application.isPlaying)
        {
            return;
        }

        GameObject root = new GameObject("[StageWindPetals]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageWindPetals>();
        current.Setup(_settings, _gust);
    }

    public static void Remove()
    {
        if (null != current)
        {
            current.enabled = false;
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

    private void Setup(WindPetalSettings _settings, WindGustSettings _gust)
    {
        settings = _settings;

        gustDir = _gust.direction.sqrMagnitude > 0.0001f ? _gust.direction.normalized : Vector2.right;
        gustSpeed = _gust.speed;
        gustSpacing = Mathf.Max(1f, _gust.spacing);
        gustHalfWidth = Mathf.Max(0.01f, _gust.bandHalfWidth);
        gustPatchSize = Mathf.Max(0.5f, _gust.patchSize);
        gustCoverage = _gust.patchCoverage;
        gustGroupLength = Mathf.Max(0f, _gust.gustGroupLength);
        gustSmallScale = Mathf.Clamp(_gust.smallPatchScale, 1f, 2f);

        petalTexture = CreatePetalTexture();
        materialInstance = new Material(_settings.material);
        materialInstance.mainTexture = petalTexture;

        petalSystem = gameObject.AddComponent<ParticleSystem>();
        petalSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = petalSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(16, _settings.maxParticles);
        main.gravityModifier = Mathf.Max(0f, _settings.gravity) / Mathf.Max(0.0001f, Physics.gravity.magnitude);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = petalSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = petalSystem.shape;
        shape.enabled = false;

        // 바람결에 나풀거린다.
        ParticleSystem.NoiseModule noise = petalSystem.noise;
        noise.enabled = _settings.flutterStrength > 0f;
        noise.strength = _settings.flutterStrength;
        noise.frequency = Mathf.Max(0.01f, _settings.flutterFrequency);
        noise.scrollSpeed = 0.6f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        // 뒤집히듯 그림 4장을 돌린다.
        ParticleSystem.TextureSheetAnimationModule sheet = petalSystem.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = FrameCount;
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.cycleCount = Mathf.Max(1, _settings.tumbleCycles);

        // 생길 때 서서히 나타나고 끝에서 옅어진다.
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = petalSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = materialInstance;
        particleRenderer.sortingLayerName = _settings.sortingLayerName;
        particleRenderer.sortingOrder = _settings.sortingOrder;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 1f;

        petalSystem.Play();
    }

    // 4x4 픽셀 그림 4장: 비스듬히 누운 꽃잎(4픽셀) → 옆으로 선 꽃잎(가는 3픽셀) → 반대로 누운 꽃잎(4픽셀) → 기운 꽃잎(2픽셀).
    // 바닥의 꽃 장식(3픽셀 남짓)보다 조금 커야 날아가는 꽃잎으로 읽힌다.
    private static Texture2D CreatePetalTexture()
    {
        int width = FramePixels * FrameCount;
        Texture2D texture = new Texture2D(width, FramePixels, TextureFormat.RGBA32, false, false);
        texture.name = "WindPetals";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        Color32[] pixels = new Color32[width * FramePixels];
        // 프레임별 칠할 픽셀(프레임 안 x, y. y는 아래가 0)
        int[][] frames =
        {
            new[] { 1, 2, 2, 2, 0, 1, 1, 1 },
            new[] { 0, 2, 1, 2, 2, 1 },
            new[] { 1, 2, 1, 1, 2, 1, 2, 0 },
            new[] { 2, 2, 1, 1 },
        };
        for (int f = 0; f < FrameCount; f++)
        {
            int[] p = frames[f];
            for (int k = 0; k + 1 < p.Length; k += 2)
            {
                pixels[(f * FramePixels + p[k]) + p[k + 1] * width] = new Color32(255, 255, 255, 255);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void LateUpdate()
    {
        if (null == petalSystem)
        {
            return;
        }

        Camera cam = Camera.main;
        if (null == cam || false == cam.orthographic)
        {
            return;
        }

        attemptAccum += Mathf.Max(0f, settings.spawnAttemptsPerSecond) * Time.deltaTime;
        int attempts = Mathf.Min((int)attemptAccum, MaxAttemptsPerFrame);
        attemptAccum -= (int)attemptAccum;
        if (attempts <= 0)
        {
            return;
        }

        float halfHeight = cam.orthographicSize + settings.viewMargin;
        float halfWidth = cam.orthographicSize * cam.aspect + settings.viewMargin;
        Vector2 center = cam.transform.position;
        // 셰이더의 _Time.y와 같은 시간. URP는 플레이 중 _Time.y에 Time.time을 넣는다
        // (Time.timeSinceLevelLoad는 씬을 불러올 때마다 0으로 돌아가 돌풍 자리와 어긋난다).
        float time = Time.time;
        float size = FramePixels / PixelsPerUnit;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        for (int k = 0; k < attempts; k++)
        {
            Vector2 position = center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight));
            if (SampleWindGust(position, time) < settings.gustThreshold)
            {
                continue;
            }

            float speed = gustSpeed * Random.Range(settings.speedScaleRange.x, Mathf.Max(settings.speedScaleRange.x, settings.speedScaleRange.y));
            Vector2 velocity = gustDir * speed + Vector2.up * Random.Range(settings.liftRange.x, Mathf.Max(settings.liftRange.x, settings.liftRange.y));

            emitParams.position = new Vector3(position.x, position.y, 0f);
            emitParams.velocity = velocity;
            emitParams.startLifetime = Random.Range(settings.lifetimeRange.x, Mathf.Max(settings.lifetimeRange.x, settings.lifetimeRange.y));
            emitParams.startSize = size;
            emitParams.startColor = PickColor();
            petalSystem.Emit(emitParams, 1);
        }
    }

    private Color PickColor()
    {
        Color[] colors = settings.colors;
        if (null == colors || 0 == colors.Length)
        {
            return Color.white;
        }

        return colors[Random.Range(0, colors.Length)];
    }

    // // WindGust.hlsl SampleWindGust를 그대로 옮긴 것(돌풍 세기 0~1). 꽃잎이 날 자리를 고를 때만 쓴다.
    private static float Hash11(float _x)
    {
        // WindGust.hlsl WindGustHash11과 같은 곱셈·frac 해시(GPU와 결과가 같다)
        float p = _x * 0.1031f;
        p -= Mathf.Floor(p);
        p *= p + 33.33f;
        p *= p + p;
        return p - Mathf.Floor(p);
    }

    private static float ValueNoise(float _x)
    {
        float i = Mathf.Floor(_x);
        float f = _x - i;
        float u = f * f * (3f - 2f * f);
        return Mathf.Lerp(Hash11(i), Hash11(i + 1f), u);
    }

    private static float SmoothStep01(float _edge0, float _edge1, float _x)
    {
        float t = Mathf.Clamp01((_x - _edge0) / (_edge1 - _edge0));
        return t * t * (3f - 2f * t);
    }

    private float SampleWindGust(Vector2 _position, float _time)
    {
        Vector2 perp = new Vector2(-gustDir.y, gustDir.x);
        float along = Vector2.Dot(_position, gustDir);
        float across = Vector2.Dot(_position, perp);
        along += Mathf.Sin(across * 0.45f) * 0.9f + Mathf.Sin(across * 1.1f + 1.7f) * 0.35f + Mathf.Sin(across * 2.3f + 0.6f) * 0.45f;

        float s = along - _time * gustSpeed;
        float idx = Mathf.Floor(s / gustSpacing);
        float local = s - idx * gustSpacing - gustSpacing * 0.5f;

        float t = local >= 0f ? local / gustHalfWidth : -local / (gustHalfWidth * 2.5f);
        float band = Mathf.Clamp01(1f - t);
        band = band * band * (3f - 2f * band);
        if (band <= 0f)
        {
            return 0f;
        }

        float group = 1f;
        if (gustGroupLength > 0f)
        {
            float groupNoise = ValueNoise((idx + 0.5f) * gustSpacing / gustGroupLength + 3.7f);
            if (groupNoise <= 0.3f)
            {
                return 0f;
            }
            group = Mathf.Lerp(0.8f, 1f, SmoothStep01(0.3f, 0.7f, groupNoise));
        }

        float cut = 1f - gustCoverage;
        float patchX = across / gustPatchSize + idx * 7.31f;
        float patchCell = Mathf.Floor(patchX);
        float patchF = patchX - patchCell;
        float patchA = Hash11(patchCell) > cut ? 1f : 0f;
        float patchB = Hash11(patchCell + 1f) > cut ? 1f : 0f;
        float patch = Mathf.Lerp(patchA, patchB, patchF * patchF * (3f - 2f * patchF));
        patch = SmoothStep01(0.3f, 0.7f, patch);

        if (gustSmallScale > 1f)
        {
            for (int k = -1; k <= 2; k++)
            {
                float cellK = patchCell + k;
                bool isolated = Hash11(cellK) > cut && Hash11(cellK - 1f) <= cut && Hash11(cellK + 1f) <= cut;
                float dist = Mathf.Abs(patchX - cellK) / gustSmallScale;
                if (isolated && dist < 1f)
                {
                    float fall = dist * dist * (3f - 2f * dist);
                    patch = Mathf.Max(patch, SmoothStep01(0.3f, 0.7f, 1f - fall));
                }
            }
        }

        return band * patch * group;
    }

    private void OnDestroy()
    {
        if (null != materialInstance)
        {
            Destroy(materialInstance);
        }
        if (null != petalTexture)
        {
            Destroy(petalTexture);
        }
        if (current == this)
        {
            current = null;
        }
    }
}
