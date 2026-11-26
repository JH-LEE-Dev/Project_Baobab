using UnityEngine;

internal static class HUD_LiquidSurface
{
    public const float StepInterval = 1f / 30f;

    public static void StepSpring(ref float _length, ref float _velocity, float _target, float _dt, float _damping = 16f)
    {
        float _step = Mathf.Min(_dt, 0.05f) * 0.25f;

        for (int i = 0; i < 4; i++)
        {
            float _acceleration = 200f * (_target - _length) - _damping * _velocity;
            _velocity += _acceleration * _step;
            _length += _velocity * _step;
        }
    }

    public static float Slosh(float _axis, float _normalizedAxis, float _time, float _phase,
        float _tiltAmplitude, float _rippleAmplitude)
    {
        float _tilt = _tiltAmplitude * Mathf.Sin(_time * 1.7f + _phase);
        float _ripple = Mathf.Sin(_axis * 0.9f - _time * 5.2f + _phase) * 0.7f
            + Mathf.Sin(_axis * 0.45f + _time * 2.3f + _phase * 1.7f) * 0.4f;
        return _tilt * _normalizedAxis + _rippleAmplitude * _ripple;
    }
}
