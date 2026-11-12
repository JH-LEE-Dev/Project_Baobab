using UnityEngine;

/// <summary>
/// 숲의 숨결. 몇십 초에 한 번, 화면 밖 먼 곳에 물결 중심을 두고 느린 빛 물결 하나를 땅을 따라 퍼뜨려 화면을 쓸고 지나가게 한다.
/// 중심이 화면에서 멀어서 화면 안에서는 둥근 고리가 아니라 완만하게 굽은 앞머리가 한쪽에서 반대쪽으로 지나가는 것처럼 보인다
/// (별자리 노바처럼 한 점에서 터지는 고리와 헷갈리지 않게).
/// 물결 자리는 매 프레임 전역값(_ForestBreathA~F)으로 넘기고, 꽃 장식(BloomDecoTwinkle)·물(WaterShimmer)·나무(TreeForestBreath)·
/// 땅(GroundForestBreathLit) 셰이더가 읽는다.
/// 숨쉬는 장식(StageDecoBreath)은 Sample로 같은 식을 CPU에서 계산해 함께 밝아진다. 식은 ForestBreath.hlsl과 같아야 한다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 해당 씬에 붙여 두므로 씬이 내려가면 같이 사라지고,
/// 다시 켤 때는 이전 것을 지우고 새로 만든다. 꺼진 곳에서는 아무것도 만들지 않는다(Sample은 0).
/// 로그라이크 지형 전환 때는 셰이더가 물결 세기에 연출 세기(StageAmbienceFade.hlsl)를 곱한다.
/// </summary>
public class StageForestBreath : MonoBehaviour
{
    // 물결 중심을 화면 가장자리에서 이만큼 더 바깥(땅 거리)에 둔다. 클수록 앞머리가 곧게 보인다.
    private const float OriginMargin = 8f;
    // 나무 별점이 늦게 반짝이는 몫(차오름 폭의 배수). TreeForestBreath.hlsl의 starSpread와 같아야 한다.
    private const float TreeStarSpreadRatio = 1.5f;
    // 밑동은 화면 밖이어도 수관이 화면에 걸친 나무까지 물결이 다 지나가게 화면을 넓혀 잰다(월드 단위).
    private const float ScreenMarginSide = 1f;
    private const float ScreenMarginBottom = 4f;
    private const float ScreenMarginTop = 0.5f;
    // 플레이어가 물결을 따라가 화면을 좀처럼 벗어나지 않을 때, 카메라가 멈춰 있었다면 걸렸을 시간보다 이만큼 더 지나면 서서히 끈다.
    private const float ExtraWaveTime = 5f;
    // 서서히 사라지는 시간(초)
    private const float FadeOutDuration = 1.5f;

    private static readonly int ForestBreathAID = Shader.PropertyToID("_ForestBreathA");
    private static readonly int ForestBreathBID = Shader.PropertyToID("_ForestBreathB");
    private static readonly int ForestBreathCID = Shader.PropertyToID("_ForestBreathC");
    private static readonly int ForestBreathDID = Shader.PropertyToID("_ForestBreathD");
    private static readonly int ForestBreathEID = Shader.PropertyToID("_ForestBreathE");
    private static readonly int ForestBreathFID = Shader.PropertyToID("_ForestBreathF");
    private static readonly int ForestBreathColorID = Shader.PropertyToID("_ForestBreathColor");

    private static StageForestBreath current;

    // //내부 의존성
    private ForestBreathSettings settings;
    private float nextWaveTime;
    private bool bWaveActive;
    private Vector2 waveOrigin;
    // 반지름이 0이었던 때(시간). 지금 반지름 = (지금 - 이 값) * 빠르기
    private float waveStartTime;
    // 이때부터 물결 전체를 서서히 줄여 끈다(물결이 화면을 다 지나가면 그 전에 끝난다).
    private float waveFadeStartTime;
    // 물결 전체 세기(0~1). 평소 1, 시간 상한에 걸리면 서서히 0으로.
    private float waveFade = 1f;
    private Vector4 breathD;

    // // 퍼블릭 제어 메서드
    public static void Apply(ForestBreathSettings _settings, string _sceneName)
    {
        Remove();

        if (null == _settings || false == _settings.enabled)
        {
            return;
        }

        GameObject root = new GameObject("[StageForestBreath]");
        UnityEngine.SceneManagement.Scene targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_sceneName);
        if (targetScene.IsValid() && targetScene.isLoaded && root.scene != targetScene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, targetScene);
        }

        current = root.AddComponent<StageForestBreath>();
        current.Setup(_settings);
    }

    public static void Remove()
    {
        if (null != current)
        {
            // Destroy는 프레임 끝에 일어나므로, 같은 프레임에 아직 돌지 않은 Update가 물결 전역값을 다시 쓰지 않게 먼저 끈다.
            current.enabled = false;
            Destroy(current.gameObject);
            current = null;
        }

        Shader.SetGlobalVector(ForestBreathAID, Vector4.zero);
    }

    /// <summary>
    /// 이 월드 자리의 숨결 밝기(0~1, 꺼져 있거나 물결이 없으면 0). 숨쉬는 장식이 셰이더와 같은 박자로 밝아질 때 쓴다.
    /// </summary>
    public static float Sample(Vector2 _positionWS)
    {
        if (null == current || false == current.bWaveActive)
        {
            return 0f;
        }

        return Evaluate(current.settings, current.waveOrigin, current.CurrentRadius(), _positionWS) * current.waveFade;
    }

    /// <summary>
    /// 숨쉬는 장식이 가장 밝을 때 더하는 밝기 비율(꺼져 있으면 0).
    /// </summary>
    public static float DecoBrightness => null != current ? Mathf.Max(0f, current.settings.decoBrightness) : 0f;

    /// <summary>
    /// 물결 중심·반지름이 주어졌을 때 한 자리의 숨결 밝기(0~1). ForestBreath.hlsl의 SampleForestBreath와 같은 식이다
    /// (물결 전체 세기 _ForestBreathD.y는 곱하지 않은 값, Sample이 곱한다).
    /// </summary>
    public static float Evaluate(ForestBreathSettings _settings, Vector2 _origin, float _radius, Vector2 _positionWS)
    {
        float wobbleSize = Mathf.Max(_settings.frontWobbleSize, 0.5f);
        float wobbleWidth = Mathf.Max(0f, _settings.frontWobble);
        float rise = Mathf.Max(_settings.riseWidth, 0.05f);
        float fall = Mathf.Max(_settings.fallWidth, 0.05f);
        Vector2 offset = _positionWS - _origin;
        offset.y *= 2f;
        // 흔들림(±wobbleWidth)을 빼고 잰 거리로 물결에서 먼 자리는 노이즈 없이 바로 0(ForestBreathMayReach와 같다).
        float roughPassed = _radius - offset.magnitude;
        float margin = wobbleWidth + 0.01f;
        if (roughPassed <= -margin || roughPassed >= rise + fall + margin)
        {
            return 0f;
        }

        float wobble = (Noise(_positionWS / wobbleSize) - 0.5f) * 2f * wobbleWidth;
        float passed = roughPassed - wobble;
        if (passed <= 0f || passed >= rise + fall)
        {
            return 0f;
        }

        float up = SmoothStep(0f, rise, passed);
        float down = 1f - SmoothStep(rise, rise + fall, passed);
        float strength = Mathf.Lerp(0.65f, 1f, Noise(_positionWS / (wobbleSize * 1.7f) + new Vector2(40f, 40f)));
        return up * down * strength;
    }

    private void Setup(ForestBreathSettings _settings)
    {
        settings = _settings;
        bWaveActive = false;
        nextWaveTime = Time.time + RandomInRange(_settings.firstDelayRange);

        float rise = Mathf.Max(_settings.riseWidth, 0.05f);
        float fall = Mathf.Max(_settings.fallWidth, 0.05f);
        Shader.SetGlobalVector(ForestBreathAID, Vector4.zero);
        Shader.SetGlobalVector(ForestBreathBID, new Vector4(rise, fall, Mathf.Max(0f, _settings.frontWobble), Mathf.Max(0.5f, _settings.frontWobbleSize)));
        Shader.SetGlobalVector(ForestBreathCID, new Vector4(Mathf.Max(0f, _settings.flowerBrightness), Mathf.Clamp01(_settings.waterSparkleCoverage), Mathf.Clamp01(_settings.shoreGlow), Mathf.Clamp01(_settings.surfaceSheen)));
        breathD = new Vector4(Mathf.Max(0.05f, _settings.waterSparkleLength), 1f, 0f, 0f);
        Shader.SetGlobalVector(ForestBreathDID, breathD);
        Shader.SetGlobalVector(ForestBreathEID, new Vector4(Mathf.Max(0f, _settings.treeStarBrightness), Mathf.Max(0f, _settings.treeBrightness), Mathf.Clamp01(_settings.groundSheen), Mathf.Clamp01(_settings.groundGlintCoverage)));
        Shader.SetGlobalVector(ForestBreathFID, new Vector4(Mathf.Max(4f, _settings.groundGlintCellTexels), Mathf.Max(0.05f, _settings.groundGlintLength), Mathf.Max(0f, _settings.groundGlintBrightness), 0f));
        // 전역 색은 자동 변환되지 않으므로 셰이더가 읽는 색 공간(선형)에 맞춘다.
        Color glowColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? _settings.groundGlowColor.linear : _settings.groundGlowColor;
        Shader.SetGlobalVector(ForestBreathColorID, glowColor);
    }

    private void Update()
    {
        // 지금 켜진 숨결이 아니면(지워지는 중) 전역값을 건드리지 않는다.
        if (current != this)
        {
            return;
        }

        if (false == bWaveActive)
        {
            if (Time.time >= nextWaveTime)
            {
                TryStartWave();
            }
            return;
        }

        float radius = CurrentRadius();
        // 지금 화면 기준으로 물결 꼬리가 다 지나갔으면 끝(화면에는 이미 아무 빛도 없다).
        if (HasPassedScreen(radius))
        {
            EndWave();
            return;
        }

        // 플레이어가 물결을 따라가 좀처럼 끝나지 않으면 뚝 끊지 않고 전체를 서서히 줄여 끈다.
        float fade = 1f;
        if (Time.time >= waveFadeStartTime)
        {
            fade = 1f - (Time.time - waveFadeStartTime) / FadeOutDuration;
            if (fade <= 0f)
            {
                EndWave();
                return;
            }
        }
        SetWaveFade(fade);

        Shader.SetGlobalVector(ForestBreathAID, new Vector4(1f, waveOrigin.x, waveOrigin.y, radius));
    }

    private void EndWave()
    {
        bWaveActive = false;
        nextWaveTime = Time.time + RandomInRange(settings.intervalRange);
        Shader.SetGlobalVector(ForestBreathAID, Vector4.zero);
        SetWaveFade(1f);
    }

    private void SetWaveFade(float _fade)
    {
        _fade = Mathf.Clamp01(_fade);
        if (Mathf.Approximately(waveFade, _fade))
        {
            return;
        }

        waveFade = _fade;
        breathD.y = _fade;
        Shader.SetGlobalVector(ForestBreathDID, breathD);
    }

    // 물결 꼬리(가장 늦게 반짝이는 나무 별점까지)가 지금 화면(수관이 걸칠 수 있는 만큼 넓힌 범위)을 다 지나갔는지.
    // 카메라가 없으면 판단하지 않고 시간 상한(서서히 사라짐)에 맡긴다.
    private bool HasPassedScreen(float _radius)
    {
        Camera camera = Camera.main;
        if (null == camera)
        {
            return false;
        }

        Vector2 center = camera.transform.position;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        float left = center.x - halfWidth - ScreenMarginSide;
        float right = center.x + halfWidth + ScreenMarginSide;
        float bottom = center.y - halfHeight - ScreenMarginBottom;
        float top = center.y + halfHeight + ScreenMarginTop;

        // 사각형에서 한 점까지 가장 먼 곳은 꼭짓점이다(땅 거리는 세로를 두 배로 친다).
        float farthest = Mathf.Max(
            Mathf.Max(GroundDistance(new Vector2(left, bottom)), GroundDistance(new Vector2(right, bottom))),
            Mathf.Max(GroundDistance(new Vector2(left, top)), GroundDistance(new Vector2(right, top))));

        float rise = Mathf.Max(settings.riseWidth, 0.05f);
        float fall = Mathf.Max(settings.fallWidth, 0.05f);
        float tail = rise + fall + rise * TreeStarSpreadRatio + Mathf.Max(0f, settings.frontWobble);
        return _radius - tail >= farthest;
    }

    private float GroundDistance(Vector2 _point)
    {
        Vector2 offset = _point - waveOrigin;
        offset.y *= 2f;
        return offset.magnitude;
    }

    // 지금 화면을 기준으로 화면 밖 아무 방향에 물결 중심을 두고, 앞머리가 화면 가장자리 바로 바깥에서 출발하게 한다.
    private void TryStartWave()
    {
        Camera camera = Camera.main;
        if (null == camera)
        {
            nextWaveTime = Time.time + 1f;
            return;
        }

        Vector2 center = camera.transform.position;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        // 땅 거리(세로 두 배)로 잰 화면 중심에서 모서리까지 거리
        float screenReach = Mathf.Sqrt(halfWidth * halfWidth + 4f * halfHeight * halfHeight);
        float wobble = Mathf.Max(0f, settings.frontWobble);
        float originDistance = screenReach + OriginMargin;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 groundDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        waveOrigin = center + new Vector2(groundDirection.x, groundDirection.y * 0.5f) * originDistance;

        float speed = Mathf.Max(0.1f, settings.speed);
        float startRadius = Mathf.Max(0f, OriginMargin - wobble - 0.5f);
        waveStartTime = Time.time - startRadius / speed;
        // 카메라가 멈춰 있었다면 화면을 다 지나갔을 반지름까지 걸리는 시간 + 여유. 그 뒤에도 안 끝났으면 서서히 끈다.
        float rise = Mathf.Max(settings.riseWidth, 0.05f);
        float stillEndRadius = originDistance + screenReach + wobble + rise + Mathf.Max(settings.fallWidth, 0.05f) + rise * TreeStarSpreadRatio;
        waveFadeStartTime = waveStartTime + stillEndRadius / speed + ExtraWaveTime;
        SetWaveFade(1f);
        bWaveActive = true;
        Shader.SetGlobalVector(ForestBreathAID, new Vector4(1f, waveOrigin.x, waveOrigin.y, startRadius));
    }

    private float CurrentRadius()
    {
        return (Time.time - waveStartTime) * Mathf.Max(0.1f, settings.speed);
    }

    private static float RandomInRange(Vector2 _range)
    {
        float min = Mathf.Max(0f, Mathf.Min(_range.x, _range.y));
        float max = Mathf.Max(min, Mathf.Max(_range.x, _range.y));
        return Random.Range(min, max);
    }

    // ForestBreath.hlsl의 ForestBreathHash21과 같은 식(사인 없는 해시라 GPU와 값이 같다).
    private static float Hash(Vector2 _p)
    {
        float x = Frac(_p.x * 0.1031f);
        float y = Frac(_p.y * 0.1031f);
        float z = Frac(_p.x * 0.1031f);
        float d = x * (y + 33.33f) + y * (z + 33.33f) + z * (x + 33.33f);
        x += d;
        y += d;
        z += d;
        return Frac((x + y) * z);
    }

    private static float Noise(Vector2 _p)
    {
        float ix = Mathf.Floor(_p.x);
        float iy = Mathf.Floor(_p.y);
        float fx = _p.x - ix;
        float fy = _p.y - iy;
        float ux = fx * fx * (3f - 2f * fx);
        float uy = fy * fy * (3f - 2f * fy);
        float a = Hash(new Vector2(ix, iy));
        float b = Hash(new Vector2(ix + 1f, iy));
        float c = Hash(new Vector2(ix, iy + 1f));
        float d = Hash(new Vector2(ix + 1f, iy + 1f));
        return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
    }

    private static float Frac(float _value)
    {
        return _value - Mathf.Floor(_value);
    }

    // HLSL smoothstep과 같다.
    private static float SmoothStep(float _edge0, float _edge1, float _x)
    {
        float t = Mathf.Clamp01((_x - _edge0) / (_edge1 - _edge0));
        return t * t * (3f - 2f * t);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
            Shader.SetGlobalVector(ForestBreathAID, Vector4.zero);
        }
    }
}
