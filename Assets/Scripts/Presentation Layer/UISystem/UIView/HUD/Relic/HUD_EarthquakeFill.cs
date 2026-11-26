using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

internal sealed class HUD_EarthquakeFill
{
    private const float RiseDuration = 0.16f;
    private const float DropDuration = 0.57f;
    private const int FlashCount = 3;
    private const float FlashFrequency = 12f;
    private const float ReadyPulsePeriod = 1f;
    private static readonly int SpriteUvRect = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int LiquidState = Shader.PropertyToID("_LiquidState");

    private readonly Image image;
    private readonly Material material;
    private readonly int bottom;
    private readonly int height;
    private float shownRatio;
    private float startRatio;
    private float targetRatio;
    private float riseAge;
    private float time;
    private float collapseAge = -1f;
    private float readyAge = -1f;

    public HUD_EarthquakeFill(Image _image, Material _template, int _bottom, int _height)
    {
        image = _image;
        bottom = _bottom;
        height = _height;
        material = new Material(_template);
        material.SetVector(SpriteUvRect, DataUtility.GetOuterUV(image.sprite));
        image.material = material;
        image.color = Color.white;
        image.fillAmount = 1f;
        Reset(0f);
    }

    public void Reset(float _ratio)
    {
        shownRatio = startRatio = targetRatio = _ratio;
        riseAge = RiseDuration;
        collapseAge = -1f;
        readyAge = -1f;
        Paint();
    }

    public void Charge(float _ratio)
    {
        if (_ratio == targetRatio)
            return;

        startRatio = shownRatio;
        targetRatio = _ratio;
        riseAge = 0f;
    }

    public void Trigger()
    {
        shownRatio = 1f;
        startRatio = targetRatio = 0f;
        riseAge = 0f;
        collapseAge = 0f;
        readyAge = -1f;
        Paint();
    }

    public void Tick(float _dt, bool _cooldownActive)
    {
        if (_dt <= 0f || (collapseAge < 0f && shownRatio <= 0f && targetRatio <= 0f))
            return;

        time += _dt;
        if (collapseAge >= 0f)
        {
            collapseAge += _dt;
            float _progress = Mathf.Clamp01(collapseAge / DropDuration);
            shownRatio = 1f - Mathf.SmoothStep(0f, 1f, _progress);
            if (collapseAge >= DropDuration)
            {
                collapseAge = -1f;
                startRatio = 0f;
                riseAge = 0f;
            }
        }
        else if (riseAge < RiseDuration)
        {
            riseAge += _dt;
            float _remaining = 1f - Mathf.Clamp01(riseAge / RiseDuration);
            shownRatio = _remaining <= 0f ? targetRatio : Mathf.Lerp(startRatio, targetRatio, 1f - _remaining * _remaining);
        }

        bool _readyDuringCooldown = _cooldownActive && collapseAge < 0f && targetRatio >= 1f && shownRatio >= 1f;
        if (_readyDuringCooldown)
            readyAge = readyAge < 0f ? 0f : Mathf.Repeat(readyAge + _dt, ReadyPulsePeriod);
        else
            readyAge = -1f;
        Paint();
    }

    private void Paint()
    {
        int _level = Mathf.RoundToInt(shownRatio * height);
        image.enabled = _level > 0;
        if (!image.enabled)
            return;

        // Surface motion runs on whole 30 fps steps. Flash timing stays independent.
        float _dotTime = Mathf.Floor(time / HUD_LiquidSurface.StepInterval) * HUD_LiquidSurface.StepInterval;
        float _flashProgress = collapseAge * FlashFrequency;
        bool _triggerFlash = _flashProgress >= 0f && _flashProgress < FlashCount && Mathf.Repeat(_flashProgress, 1f) < 1f / 3f;
        float _brightness = _triggerFlash ? 1.4f : 1f;
        if (readyAge >= 0f)
            _brightness = 1.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * readyAge / ReadyPulsePeriod);

        material.SetVector(LiquidState, new Vector4(bottom, _dotTime, _level, _brightness));
    }

    public void Dispose()
    {
        Object.Destroy(material);
    }
}
