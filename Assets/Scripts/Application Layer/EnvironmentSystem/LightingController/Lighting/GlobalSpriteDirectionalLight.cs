using UnityEngine;

[ExecuteAlways]
public class GlobalSpriteDirectionalLight : MonoBehaviour
{
    [System.Serializable]
    private struct DirectionKeyframe
    {
        [Range(0f, 24f)] public float hour;
        public Vector3 direction;
    }

    [System.Serializable]
    private struct LightingKeyframe
    {
        [Range(0f, 24f)] public float hour;
        public Color lightColor;
        [Min(0f)] public float lightIntensity;
        public Color ambientColor;
        [Min(0f)] public float ambientIntensity;
    }

    private const string DefaultLitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const int MaxPointLights = 8;

    private static readonly int GlobalDirectionalLightDirectionId = Shader.PropertyToID("_GlobalDirectionalLightDirection");
    private static readonly int GlobalDirectionalLightColorId = Shader.PropertyToID("_GlobalDirectionalLightColor");
    private static readonly int GlobalDirectionalLightIntensityId = Shader.PropertyToID("_GlobalDirectionalLightIntensity");
    private static readonly int GlobalAmbientLightColorId = Shader.PropertyToID("_GlobalAmbientLightColor");
    private static readonly int GlobalAmbientLightIntensityId = Shader.PropertyToID("_GlobalAmbientLightIntensity");
    private static readonly int GlobalPointLightCountId = Shader.PropertyToID("_GlobalPointLightCount");
    private static readonly int GlobalPointLightPositionsId = Shader.PropertyToID("_GlobalPointLightPositions");
    private static readonly int GlobalPointLightColorsId = Shader.PropertyToID("_GlobalPointLightColors");
    private static readonly int GlobalPointLightParamsId = Shader.PropertyToID("_GlobalPointLightParams");
    private static readonly int GlobalPointLightShapeId = Shader.PropertyToID("_GlobalPointLightShape");
    private static readonly int GlobalObjectPointLightRangeMultiplierId = Shader.PropertyToID("_GlobalObjectPointLightRangeMultiplier");

    [Header("Directional Light")]
    [SerializeField] private Vector3 lightDirection = new Vector3(0f, 1f, 1f);
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField, Min(0f)] private float lightIntensity = 1f;

    [Header("Ambient Light")]
    [SerializeField] private Color ambientColor = Color.white;
    [SerializeField, Min(0f)] private float ambientIntensity = 0.2f;

    [Header("Material Override")]
    [SerializeField] private Material globalLitMaterial;
    [SerializeField] private bool autoApplyMaterial = false;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool ignoreShadowRenderers = true;
    [SerializeField] private string shadowNameKeyword = "Shadow";

    [Header("Point Lights")]
    [SerializeField, Range(0, MaxPointLights)] private int maxPointLights = MaxPointLights;
    [SerializeField, Min(0.1f)] private float objectPointLightRangeMultiplier = 2.5f;

    [Header("External Inputs")]
    [SerializeField, Range(0f, 24f)] private float currentHour = 12f;
    [SerializeField] private WeatherType currentWeather = WeatherType.Normal;

    [Header("Time Of Day Lighting")]
    [SerializeField] private bool useTimeOfDayKeyframes = true;
    [SerializeField] private bool debugUseManualLighting = false;
    [SerializeField] private DirectionKeyframe[] directionKeyframes =
    {
        new DirectionKeyframe { hour = 5f, direction = new Vector3(0.5f, 0.5f, 0.1f) },
        new DirectionKeyframe { hour = 12f, direction = new Vector3(0f, 0.5f, 1f) },
        new DirectionKeyframe { hour = 19f, direction = new Vector3(-0.5f, 0.5f, 0.1f) },
    };
    [SerializeField] private LightingKeyframe[] lightingKeyframes =
    {
        new LightingKeyframe
        {
            hour = 5f,
            lightColor = new Color(0.62f, 0.73f, 1f, 1f),
            lightIntensity = 0f,
            ambientColor = new Color(0.35f, 0.42f, 0.58f, 1f),
            ambientIntensity = 0.07f,
        },
        new LightingKeyframe
        {
            hour = 8f,
            lightColor = new Color(0.98f, 0.94f, 0.88f, 1f),
            lightIntensity = 0.7f,
            ambientColor = new Color(0.93f, 0.95f, 1f, 1f),
            ambientIntensity = 0.5f,
        },
        new LightingKeyframe
        {
            hour = 12f,
            lightColor = Color.white,
            lightIntensity = 0.8f,
            ambientColor = Color.white,
            ambientIntensity = 0.5f,
        },
        new LightingKeyframe
        {
            hour = 17f,
            lightColor = new Color(1f, 0.93f, 0.85f, 1f),
            lightIntensity = 0.7f,
            ambientColor = new Color(1f, 0.94f, 0.88f, 1f),
            ambientIntensity = 0.5f,
        },
        new LightingKeyframe
        {
            hour = 19f,
            lightColor = new Color(1f, 0.55f, 0.35f, 1f),
            lightIntensity = 0.7f,
            ambientColor = new Color(0.95f, 0.55f, 0.45f, 1f),
            ambientIntensity = 0.5f,
        },
        new LightingKeyframe
        {
            hour = 20f,
            lightColor = new Color(0.62f, 0.73f, 1f, 1f),
            lightIntensity = 0f,
            ambientColor = new Color(0.35f, 0.42f, 0.58f, 1f),
            ambientIntensity = 0.07f,
        },
    };

    private readonly Vector4[] pointLightPositions = new Vector4[MaxPointLights];
    private readonly Vector4[] pointLightColors = new Vector4[MaxPointLights];
    private readonly Vector4[] pointLightParams = new Vector4[MaxPointLights];
    private readonly Vector4[] pointLightShape = new Vector4[MaxPointLights];

    public float CurrentHour => currentHour;
    public WeatherType CurrentWeather => currentWeather;

    public void Initialize()
    {
        Enable();
    }
    
    private void Update()
    {
        ApplyGlobals();
    }

    private void OnValidate()
    {
        ApplyGlobals();
        TryApplyMaterialToScene();
    }

    public void Enable()
    {
        ApplyGlobals();
        TryApplyMaterialToScene();
    }

    [ContextMenu("Apply Globals")]
    public void ApplyGlobals()
    {
        Vector3 appliedDirection = lightDirection;
        Color appliedLightColor = lightColor;
        float appliedLightIntensity = lightIntensity;
        Color appliedAmbientColor = ambientColor;
        float appliedAmbientIntensity = ambientIntensity;

        if (useTimeOfDayKeyframes && !debugUseManualLighting)
        {
            appliedDirection = EvaluateDirection(currentHour);
            EvaluateLighting(
                currentHour,
                out appliedLightColor,
                out appliedLightIntensity,
                out appliedAmbientColor,
                out appliedAmbientIntensity);
        }

        Vector3 normalizedDirection = NormalizeDirection(appliedDirection);

        Shader.SetGlobalVector(GlobalDirectionalLightDirectionId, new Vector4(
            normalizedDirection.x,
            normalizedDirection.y,
            normalizedDirection.z,
            0f));
        Shader.SetGlobalColor(GlobalDirectionalLightColorId, appliedLightColor);
        Shader.SetGlobalFloat(GlobalDirectionalLightIntensityId, appliedLightIntensity);
        Shader.SetGlobalColor(GlobalAmbientLightColorId, appliedAmbientColor);
        Shader.SetGlobalFloat(GlobalAmbientLightIntensityId, appliedAmbientIntensity);
        Shader.SetGlobalFloat(GlobalObjectPointLightRangeMultiplierId, objectPointLightRangeMultiplier);

        PushPointLights();
    }

    public void SetCurrentHour(float hour)
    {
        currentHour = Mathf.Repeat(hour, 24f);
    }

    public void SetCurrentTimeNormalized(float normalizedTime)
    {
        currentHour = Mathf.Repeat(normalizedTime, 1f) * 24f;
    }

    public void SetCurrentTimePercent(float currentTimePercent)
    {
        SetCurrentTimeNormalized(currentTimePercent);
    }

    public void SetCurrentWeather(WeatherType weather)
    {
        currentWeather = weather;
    }

    public bool SetCurrentWeather(string weatherName)
    {
        if (string.IsNullOrWhiteSpace(weatherName))
        {
            return false;
        }

        if (System.Enum.TryParse(weatherName, true, out WeatherType parsedWeather))
        {
            currentWeather = parsedWeather;
            return true;
        }

        return false;
    }

    [ContextMenu("Apply Material To Scene")]
    public void ApplyMaterialToScene()
    {
        if (globalLitMaterial == null)
        {
            return;
        }

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (!ShouldReplaceMaterial(renderer))
            {
                continue;
            }

            renderer.sharedMaterial = globalLitMaterial;
        }
    }

    private void TryApplyMaterialToScene()
    {
        if (!autoApplyMaterial)
        {
            return;
        }

        ApplyMaterialToScene();
    }

    private bool ShouldReplaceMaterial(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (ignoreShadowRenderers &&
            !string.IsNullOrWhiteSpace(shadowNameKeyword) &&
            renderer.name.IndexOf(shadowNameKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        Material currentMaterial = renderer.sharedMaterial;
        if (currentMaterial == null || currentMaterial.shader == null)
        {
            return true;
        }

        string shaderName = currentMaterial.shader.name;
        return shaderName == DefaultLitShaderName || currentMaterial == globalLitMaterial;
    }

    private static Vector3 NormalizeDirection(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector3(0f, 1f, 1f).normalized;
    }

    private Vector3 EvaluateDirection(float hour)
    {
        if (directionKeyframes == null || directionKeyframes.Length == 0)
        {
            return lightDirection;
        }

        if (directionKeyframes.Length == 1)
        {
            return directionKeyframes[0].direction;
        }

        int fromIndex;
        int toIndex;
        float t;
        FindSegment(directionKeyframes, hour, out fromIndex, out toIndex, out t);

        Vector3 fromDirection = NormalizeDirection(directionKeyframes[fromIndex].direction);
        Vector3 toDirection = NormalizeDirection(directionKeyframes[toIndex].direction);
        return Vector3.Slerp(fromDirection, toDirection, t);
    }

    private void EvaluateLighting(
        float hour,
        out Color evaluatedLightColor,
        out float evaluatedLightIntensity,
        out Color evaluatedAmbientColor,
        out float evaluatedAmbientIntensity)
    {
        if (lightingKeyframes == null || lightingKeyframes.Length == 0)
        {
            evaluatedLightColor = lightColor;
            evaluatedLightIntensity = lightIntensity;
            evaluatedAmbientColor = ambientColor;
            evaluatedAmbientIntensity = ambientIntensity;
            return;
        }

        if (lightingKeyframes.Length == 1)
        {
            LightingKeyframe only = lightingKeyframes[0];
            evaluatedLightColor = only.lightColor;
            evaluatedLightIntensity = only.lightIntensity;
            evaluatedAmbientColor = only.ambientColor;
            evaluatedAmbientIntensity = only.ambientIntensity;
            return;
        }

        int fromIndex;
        int toIndex;
        float t;
        FindSegment(lightingKeyframes, hour, out fromIndex, out toIndex, out t);

        LightingKeyframe from = lightingKeyframes[fromIndex];
        LightingKeyframe to = lightingKeyframes[toIndex];

        evaluatedLightColor = Color.Lerp(from.lightColor, to.lightColor, t);
        evaluatedLightIntensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t);
        evaluatedAmbientColor = Color.Lerp(from.ambientColor, to.ambientColor, t);
        evaluatedAmbientIntensity = Mathf.Lerp(from.ambientIntensity, to.ambientIntensity, t);
    }

    private static void FindSegment(DirectionKeyframe[] keyframes, float hour, out int fromIndex, out int toIndex, out float t)
    {
        float wrappedHour = Mathf.Repeat(hour, 24f);

        for (int i = 0; i < keyframes.Length; i++)
        {
            int nextIndex = (i + 1) % keyframes.Length;
            float fromHour = keyframes[i].hour;
            float toHour = keyframes[nextIndex].hour;

            if (nextIndex == 0)
            {
                toHour += 24f;
            }

            float comparisonHour = wrappedHour;
            if (comparisonHour < fromHour)
            {
                comparisonHour += 24f;
            }

            if (comparisonHour >= fromHour && comparisonHour <= toHour)
            {
                fromIndex = i;
                toIndex = nextIndex;
                t = Mathf.Approximately(fromHour, toHour)
                    ? 0f
                    : Mathf.InverseLerp(fromHour, toHour, comparisonHour);
                return;
            }
        }

        fromIndex = keyframes.Length - 1;
        toIndex = 0;
        t = 0f;
    }

    private static void FindSegment(LightingKeyframe[] keyframes, float hour, out int fromIndex, out int toIndex, out float t)
    {
        float wrappedHour = Mathf.Repeat(hour, 24f);

        for (int i = 0; i < keyframes.Length; i++)
        {
            int nextIndex = (i + 1) % keyframes.Length;
            float fromHour = keyframes[i].hour;
            float toHour = keyframes[nextIndex].hour;

            if (nextIndex == 0)
            {
                toHour += 24f;
            }

            float comparisonHour = wrappedHour;
            if (comparisonHour < fromHour)
            {
                comparisonHour += 24f;
            }

            if (comparisonHour >= fromHour && comparisonHour <= toHour)
            {
                fromIndex = i;
                toIndex = nextIndex;
                t = Mathf.Approximately(fromHour, toHour)
                    ? 0f
                    : Mathf.InverseLerp(fromHour, toHour, comparisonHour);
                return;
            }
        }

        fromIndex = keyframes.Length - 1;
        toIndex = 0;
        t = 0f;
    }

    private void PushPointLights()
    {
        int count = 0;
        var lights = SpritePointLight2D.Lights;

        for (int i = 0; i < lights.Count && count < maxPointLights; i++)
        {
            SpritePointLight2D light = lights[i] as SpritePointLight2D;
            if (light == null || !light.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 position = light.WorldPosition;
            Color color = light.LightColor;

            pointLightPositions[count] = new Vector4(position.x, position.y, position.z, light.OuterRadius);
            pointLightColors[count] = new Vector4(color.r, color.g, color.b, color.a);
            pointLightParams[count] = new Vector4(light.Intensity, light.InnerRadiusNormalized, light.Height, 0f);
            pointLightShape[count] = new Vector4(light.EllipseYScale, light.NormalInfluence, 0f, 0f);
            count++;
        }

        for (int i = count; i < MaxPointLights; i++)
        {
            pointLightPositions[i] = Vector4.zero;
            pointLightColors[i] = Vector4.zero;
            pointLightParams[i] = Vector4.zero;
            pointLightShape[i] = Vector4.zero;
        }

        Shader.SetGlobalFloat(GlobalPointLightCountId, count);
        Shader.SetGlobalVectorArray(GlobalPointLightPositionsId, pointLightPositions);
        Shader.SetGlobalVectorArray(GlobalPointLightColorsId, pointLightColors);
        Shader.SetGlobalVectorArray(GlobalPointLightParamsId, pointLightParams);
        Shader.SetGlobalVectorArray(GlobalPointLightShapeId, pointLightShape);
    }
}
