using UnityEngine;

[ExecuteAlways]
public class GlobalSpriteDirectionalLight : MonoBehaviour
{
    private const string DefaultLitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";

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

        Shader.SetGlobalVector("_GlobalDirectionalLightDirection", new Vector4(
            normalizedDirection.x,
            normalizedDirection.y,
            normalizedDirection.z,
            0f));
        Shader.SetGlobalColor("_GlobalDirectionalLightColor", lightColor);
        Shader.SetGlobalFloat("_GlobalDirectionalLightIntensity", lightIntensity);
        Shader.SetGlobalColor("_GlobalAmbientLightColor", ambientColor);
        Shader.SetGlobalFloat("_GlobalAmbientLightIntensity", ambientIntensity);
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
}
