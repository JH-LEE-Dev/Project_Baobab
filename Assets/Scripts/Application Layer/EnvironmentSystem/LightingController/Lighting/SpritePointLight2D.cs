using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SpritePointLight2D : MonoBehaviour
{
    private static readonly List<SpritePointLight2D> ActiveLights = new();

    [SerializeField] private Color lightColor = Color.white;
    [SerializeField, Min(0f)] private float intensity = 1f;
    [SerializeField, Min(0.01f)] private float outerRadius = 3f;
    [SerializeField, Range(0f, 1f)] private float innerRadiusNormalized = 0.15f;
    [SerializeField, Min(0.01f)] private float height = 1f;

    public static IReadOnlyList<SpritePointLight2D> Lights => ActiveLights;

    public Vector3 WorldPosition => transform.position;
    public Color LightColor => lightColor;
    public float Intensity => intensity;
    public float OuterRadius => outerRadius;
    public float InnerRadiusNormalized => innerRadiusNormalized;
    public float Height => height;

    public void Enable()
    {
        if (!ActiveLights.Contains(this))
        {
            ActiveLights.Add(this);
        }
    }

    public void Disable()
    {
        ActiveLights.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveLights.Remove(this);
    }

    private void OnValidate()
    {
        outerRadius = Mathf.Max(0.01f, outerRadius);
        height = Mathf.Max(0.01f, height);
    }
}
