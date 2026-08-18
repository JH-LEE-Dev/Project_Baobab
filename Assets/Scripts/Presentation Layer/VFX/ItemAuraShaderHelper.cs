using UnityEngine;

/// <summary>
/// ItemAuraEffectController 및 UI_ItemAuraEffectController에서 공통으로 사용하는 셰이더 속성 ID와 적용 헬퍼입니다.
/// </summary>
public static class ItemAuraShaderHelper
{
    public static readonly int IntensityPropertyId = Shader.PropertyToID("_Intensity");
    public static readonly int BloomMultiplierPropertyId = Shader.PropertyToID("_BloomMultiplier");
    public static readonly int PixelateEnabledPropertyId = Shader.PropertyToID("_PixelateEnabled");
    public static readonly int PixelResolutionPropertyId = Shader.PropertyToID("_PixelResolution");
    public static readonly int ColorBandingStepsPropertyId = Shader.PropertyToID("_ColorBandingSteps");
    public static readonly int BurstProgressPropertyId = Shader.PropertyToID("_BurstProgress");
    public static readonly int RotationSpeedPropertyId = Shader.PropertyToID("_RotationSpeed");
    public static readonly int SpeedVariationPropertyId = Shader.PropertyToID("_SpeedVariation");
    public static readonly int RayCountPropertyId = Shader.PropertyToID("_RayCount");
    public static readonly int AngleJitterPropertyId = Shader.PropertyToID("_AngleJitter");
    public static readonly int BeamBlurPropertyId = Shader.PropertyToID("_BeamBlur");
    public static readonly int BeamMinWidthPropertyId = Shader.PropertyToID("_BeamMinWidth");
    public static readonly int BeamMaxWidthPropertyId = Shader.PropertyToID("_BeamMaxWidth");
    public static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
    public static readonly int BeamColorPropertyId = Shader.PropertyToID("_BeamColor");
    public static readonly int OuterColorPropertyId = Shader.PropertyToID("_OuterColor");
    public static readonly int EnablePrismModePropertyId = Shader.PropertyToID("_EnablePrismMode");
    public static readonly int PrismSaturationPropertyId = Shader.PropertyToID("_PrismSaturation");
    public static readonly int PrismSpeedPropertyId = Shader.PropertyToID("_PrismSpeed");
    public static readonly int PrismHueOffsetPropertyId = Shader.PropertyToID("_PrismHueOffset");

    public static void ApplyColorSettings(MaterialPropertyBlock _block, Color _coreColor, Color _beamColor, Color _outerColor)
    {
        _block.SetColor(CoreColorPropertyId, _coreColor);
        _block.SetColor(BeamColorPropertyId, _beamColor);
        _block.SetColor(OuterColorPropertyId, _outerColor);
    }

    public static void ApplyColorSettings(Material _material, Color _coreColor, Color _beamColor, Color _outerColor)
    {
        _material.SetColor(CoreColorPropertyId, _coreColor);
        _material.SetColor(BeamColorPropertyId, _beamColor);
        _material.SetColor(OuterColorPropertyId, _outerColor);
    }

    public static void ApplyPrismSettings(MaterialPropertyBlock _block, bool _enablePrism, float _saturation, float _speed, float _hueOffset)
    {
        _block.SetFloat(EnablePrismModePropertyId, _enablePrism ? 1f : 0f);
        _block.SetFloat(PrismSaturationPropertyId, _saturation);
        _block.SetFloat(PrismSpeedPropertyId, _speed);
        _block.SetFloat(PrismHueOffsetPropertyId, _hueOffset);
    }

    public static void ApplyPrismSettings(Material _material, bool _enablePrism, float _saturation, float _speed, float _hueOffset)
    {
        _material.SetFloat(EnablePrismModePropertyId, _enablePrism ? 1f : 0f);
        _material.SetFloat(PrismSaturationPropertyId, _saturation);
        _material.SetFloat(PrismSpeedPropertyId, _speed);
        _material.SetFloat(PrismHueOffsetPropertyId, _hueOffset);
    }

    public static void ApplyPixelSettings(MaterialPropertyBlock _block, bool _enablePixelStyle, float _pixelResolution, float _colorBandingSteps)
    {
        _block.SetFloat(PixelateEnabledPropertyId, _enablePixelStyle ? 1f : 0f);
        _block.SetFloat(PixelResolutionPropertyId, _pixelResolution);
        _block.SetFloat(ColorBandingStepsPropertyId, _colorBandingSteps);
    }

    public static void ApplyPixelSettings(Material _material, bool _enablePixelStyle, float _pixelResolution, float _colorBandingSteps)
    {
        _material.SetFloat(PixelateEnabledPropertyId, _enablePixelStyle ? 1f : 0f);
        _material.SetFloat(PixelResolutionPropertyId, _pixelResolution);
        _material.SetFloat(ColorBandingStepsPropertyId, _colorBandingSteps);
    }

    public static void ApplyRayOverrides(MaterialPropertyBlock _block, float _rayCount, float _angleJitter, float _beamBlur, float _minBeamWidth, float _maxBeamWidth)
    {
        _block.SetFloat(RayCountPropertyId, _rayCount);
        _block.SetFloat(AngleJitterPropertyId, _angleJitter);
        _block.SetFloat(BeamBlurPropertyId, _beamBlur);
        _block.SetFloat(BeamMinWidthPropertyId, _minBeamWidth);
        _block.SetFloat(BeamMaxWidthPropertyId, _maxBeamWidth);
    }

    public static void ApplyRayOverrides(Material _material, float _rayCount, float _angleJitter, float _beamBlur, float _minBeamWidth, float _maxBeamWidth)
    {
        _material.SetFloat(RayCountPropertyId, _rayCount);
        _material.SetFloat(AngleJitterPropertyId, _angleJitter);
        _material.SetFloat(BeamBlurPropertyId, _beamBlur);
        _material.SetFloat(BeamMinWidthPropertyId, _minBeamWidth);
        _material.SetFloat(BeamMaxWidthPropertyId, _maxBeamWidth);
    }
}
