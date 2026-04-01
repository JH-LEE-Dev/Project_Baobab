using UnityEngine;

[ExecuteAlways]
public class GlobalSpriteDirectionalLight : MonoBehaviour
{
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

    [Header("External Inputs")]
    [SerializeField, Range(0f, 24f)] private float currentHour = 12f;
    [SerializeField] private WeatherType currentWeather = WeatherType.Normal;

    private readonly Vector4[] pointLightPositions = new Vector4[MaxPointLights];
    private readonly Vector4[] pointLightColors = new Vector4[MaxPointLights];
    private readonly Vector4[] pointLightParams = new Vector4[MaxPointLights];

    public float CurrentHour => currentHour;
    public WeatherType CurrentWeather => currentWeather;

    private void OnEnable()
    {
        ApplyGlobals();
        TryApplyMaterialToScene();
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

    [ContextMenu("Apply Globals")]
    public void ApplyGlobals()
    {
        Vector3 normalizedDirection = NormalizeDirection(lightDirection);

        Shader.SetGlobalVector(GlobalDirectionalLightDirectionId, new Vector4(
            normalizedDirection.x,
            normalizedDirection.y,
            normalizedDirection.z,
            0f));
        Shader.SetGlobalColor(GlobalDirectionalLightColorId, lightColor);
        Shader.SetGlobalFloat(GlobalDirectionalLightIntensityId, lightIntensity);
        Shader.SetGlobalColor(GlobalAmbientLightColorId, ambientColor);
        Shader.SetGlobalFloat(GlobalAmbientLightIntensityId, ambientIntensity);

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
            count++;
        }

        for (int i = count; i < MaxPointLights; i++)
        {
            pointLightPositions[i] = Vector4.zero;
            pointLightColors[i] = Vector4.zero;
            pointLightParams[i] = Vector4.zero;
        }

        Shader.SetGlobalFloat(GlobalPointLightCountId, count);
        Shader.SetGlobalVectorArray(GlobalPointLightPositionsId, pointLightPositions);
        Shader.SetGlobalVectorArray(GlobalPointLightColorsId, pointLightColors);
        Shader.SetGlobalVectorArray(GlobalPointLightParamsId, pointLightParams);
    }
}
