using UnityEditor;
using UnityEngine;

public static class HighResolutionBloomMaterialMenu
{
    private const string ShaderName = "ProjectBaobab/Rendering/HighResolutionBloom";
    private const string MaterialPath = "Assets/Shaders/Rendering/HighResolutionBloom.mat";

    [MenuItem("Assets/Create/Project Baobab/Rendering/High Resolution Bloom Material")]
    public static void CreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader not found: {ShaderName}");
            return;
        }

        Material material = new Material(shader)
        {
            name = "HighResolutionBloom"
        };

        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(MaterialPath);
        AssetDatabase.CreateAsset(material, uniquePath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = material;
    }
}
