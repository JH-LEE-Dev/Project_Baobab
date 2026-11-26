using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

internal sealed class HUD_MeteorShowerFill
{
    private const int SpriteSize = 64;
    private const float RiseDuration = 0.26f;
    private const float MeteorCueRatio = 0.84f;
    private const float MeteorCueDuration = 11 * HUD_LiquidSurface.StepInterval;
    private const int SparkCount = 5;
    private const float FlashFrequency = 12f;
    private const float FlashDutyCycle = 1f / 3f;
    private const float FlashBrightness = 1.4f;
    private static readonly int SpriteUvRect = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int FillState = Shader.PropertyToID("_FillState");
    private static readonly int BrightnessMultiplier = Shader.PropertyToID("_BrightnessMultiplier");
    private static readonly int MeteorStep = Shader.PropertyToID("_MeteorStep");
    private static readonly int SurfaceRows = Shader.PropertyToID("_SurfaceRows");
    private static readonly int Sparks = Shader.PropertyToID("_Sparks");

    private readonly Material material;
    private readonly Image image;
    private readonly int height;
    private readonly int bottom;
    private readonly float[] rows = new float[SpriteSize];
    private readonly Vector4[] sparks = new Vector4[SparkCount];
    private float shownRatio;
    private float startRatio;
    private float targetRatio;
    private float riseAge;
    private float brightness = 1f;
    private float meteorCueAge;
    private bool draining;
    private float time;
    private float stepTimer;
    private float springOffset;
    private float springVelocity;

    public HUD_MeteorShowerFill(Image _image, Material _template, int _bottom, int _height)
    {
        image = _image;
        bottom = _bottom;
        height = _height;
        material = new Material(_template);
        material.SetVector(SpriteUvRect, DataUtility.GetOuterUV(image.sprite));
        image.material = material;
        image.fillAmount = 1f;
        image.color = Color.white;
        Reset(0f);
    }

    public void Reset(float _ratio)
    {
        shownRatio = _ratio;
        startRatio = _ratio;
        targetRatio = _ratio;
        riseAge = RiseDuration;
        SetBrightness(1f);
        meteorCueAge = MeteorCueDuration;
        draining = false;
        springOffset = 0f;
        springVelocity = 0f;
        Paint();
    }

    public void Charge(float _ratio)
    {
        draining = false;
        SetBrightness(1f);

        if (_ratio <= targetRatio)
        {
            if (_ratio < targetRatio)
                Reset(_ratio);

            return;
        }

        springVelocity += Mathf.Lerp(2f, 5f, Mathf.Clamp01((_ratio - targetRatio) / 0.12f));
        springVelocity = Mathf.Min(springVelocity, 7f);
        if (targetRatio < MeteorCueRatio && _ratio >= MeteorCueRatio)
            meteorCueAge = 0f;

        startRatio = shownRatio;
        targetRatio = _ratio;
        riseAge = 0f;
    }

    public void Trigger()
    {
        Reset(1f);
        draining = true;
        SetBrightness(FlashBrightness);
        Paint();
    }

    public void Drain(float _ratio, float _elapsed)
    {
        shownRatio = _ratio;
        targetRatio = _ratio;
        float _flashPhase = Mathf.Repeat(_elapsed * FlashFrequency, 1f);
        SetBrightness(_flashPhase < FlashDutyCycle ? FlashBrightness : 1f);
    }

    private void SetBrightness(float _brightness)
    {
        if (brightness == _brightness)
            return;

        brightness = _brightness;
        material.SetFloat(BrightnessMultiplier, brightness);
    }

    public void Tick(float _dt)
    {
        if (_dt <= 0f || (targetRatio <= 0f && shownRatio <= 0f))
            return;

        time += _dt;
        if (meteorCueAge < MeteorCueDuration)
            meteorCueAge += _dt;

        if (riseAge < RiseDuration)
        {
            riseAge += _dt;
            float _remaining = 1f - Mathf.Clamp01(riseAge / RiseDuration);
            shownRatio = Mathf.Lerp(startRatio, targetRatio, 1f - _remaining * _remaining);
        }

        HUD_LiquidSurface.StepSpring(ref springOffset, ref springVelocity, 0f, _dt, _damping: 26f);
        stepTimer += _dt;

        if (stepTimer >= HUD_LiquidSurface.StepInterval)
        {
            stepTimer %= HUD_LiquidSurface.StepInterval;
            Paint();
        }
    }

    private void Paint()
    {
        image.enabled = shownRatio > 0f;
        if (!image.enabled)
            return;

        float _level = shownRatio * height;
        float _room = Mathf.Clamp01(Mathf.Min(_level, height - _level) * 0.5f);

        for (int x = 0; x < rows.Length; x++)
        {
            float _axis = Mathf.Clamp((x - 31.5f) / 11f, -1f, 1f);
            float _wave = HUD_LiquidSurface.Slosh(x, _axis, time * 0.56f, 0.65f, 0.34f, 0.42f);
            rows[x] = bottom + Mathf.Clamp(Mathf.Round(_level + (_wave + springOffset) * _room), 1f, height);
        }

        for (int i = 0; i < sparks.Length; i++)
        {
            float _orbit = time * (0.85f + 0.12f * i) + 2.39f * i;
            float _depth = 0.5f + 0.32f * Mathf.Sin(_orbit * 0.77f + 1.1f * i);
            float _width = Mathf.Lerp(3.5f, 8f, Mathf.Clamp01(_level * _depth / 6f));
            float _x = Mathf.Round(31.5f + Mathf.Cos(_orbit) * _width);
            float _y = bottom + Mathf.Round(Mathf.Max(0f, _level - 1f) * _depth);
            float _blink = Mathf.Repeat(time / (1.65f + 0.13f * i) + 0.29f * i, 1f);
            float _brightness = _blink < 0.12f || _blink >= 0.91f ? 0f : _blink < 0.43f ? 0.53f : 0.75f;
            float _cross = _blink >= 0.43f && _blink < 0.63f ? 1f : 0f;
            sparks[i] = new Vector4(_x, _y, _brightness, _cross);
        }

        float _meteorStep = meteorCueAge < MeteorCueDuration
            ? Mathf.Floor(meteorCueAge / HUD_LiquidSurface.StepInterval) : -1f;
        material.SetFloatArray(SurfaceRows, rows);
        material.SetVectorArray(Sparks, sparks);
        material.SetFloat(MeteorStep, _meteorStep);
        material.SetVector(FillState, new Vector4(bottom, time, 0f, draining ? 0f : shownRatio));
    }

    public void Dispose()
    {
        Object.Destroy(material);
    }
}
