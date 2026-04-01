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

    [Header("Campfire")]
    [SerializeField] private bool useCampfireFlicker = false;
    [SerializeField] private Color campfireSecondaryColor = new Color(1f, 0.35f, 0.1f, 1f);
    [SerializeField, Min(0f)] private float radiusFlickerAmplitude = 0.2f;
    [SerializeField, Min(0f)] private float radiusFlickerSpeed = 2.5f;
    [SerializeField, Min(0f)] private float colorFlickerSpeed = 1.8f;
    [SerializeField, Min(0f)] private float intensityFlickerAmplitude = 0.1f;

    public static IReadOnlyList<SpritePointLight2D> Lights => ActiveLights;

    public Vector3 WorldPosition => transform.position;
    public Color LightColor => useCampfireFlicker ? EvaluateCampfireColor() : lightColor;
    public float Intensity => useCampfireFlicker ? EvaluateCampfireIntensity() : intensity;
    public float OuterRadius => useCampfireFlicker ? EvaluateCampfireRadius() : outerRadius;
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
        radiusFlickerAmplitude = Mathf.Max(0f, radiusFlickerAmplitude);
        radiusFlickerSpeed = Mathf.Max(0f, radiusFlickerSpeed);
        colorFlickerSpeed = Mathf.Max(0f, colorFlickerSpeed);
        intensityFlickerAmplitude = Mathf.Max(0f, intensityFlickerAmplitude);
    }

    private Color EvaluateCampfireColor()
    {
        float time = GetAnimationTime();
        float seed = Mathf.Abs(GetInstanceID()) * 0.0137f;
        float blend = Mathf.PerlinNoise(seed, time * colorFlickerSpeed);
        return Color.Lerp(lightColor, campfireSecondaryColor, blend);
    }

    private float EvaluateCampfireRadius()
    {
        float time = GetAnimationTime();
        float seed = Mathf.Abs(GetInstanceID()) * 0.0319f;
        float flicker = Mathf.PerlinNoise(seed, time * radiusFlickerSpeed);
        float centered = (flicker - 0.5f) * 2f;
        float multiplier = 1f + centered * radiusFlickerAmplitude;
        return Mathf.Max(0.01f, outerRadius * multiplier);
    }

    private float EvaluateCampfireIntensity()
    {
        float time = GetAnimationTime();
        float seed = Mathf.Abs(GetInstanceID()) * 0.0473f;
        float flicker = Mathf.PerlinNoise(seed, time * (radiusFlickerSpeed + 0.8f));
        float centered = (flicker - 0.5f) * 2f;
        float multiplier = 1f + centered * intensityFlickerAmplitude;
        return Mathf.Max(0f, intensity * multiplier);
    }

    private static float GetAnimationTime()
    {
        return Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
    }
}
