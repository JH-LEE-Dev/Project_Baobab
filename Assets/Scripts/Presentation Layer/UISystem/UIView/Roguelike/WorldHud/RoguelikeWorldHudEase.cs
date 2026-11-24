using System;

/// <summary>
/// 월드 HUD 연출 이징(WorldHUD_Plan.md 10-4). world_hud_v7_motion.py의 clamp01 / ease_out_cubic / ease_in_cubic / ease_out_back / phase와 같은 식이다.
/// 시뮬(파이썬 float)과 같은 결과가 나오도록 double로 계산하고 세제곱은 Math.Pow를 쓴다.
/// </summary>
public static class RoguelikeWorldHudEase
{
    /// <summary>ease_out_back 기본 오버슈트 계수.</summary>
    public const double BackOvershoot = 2.2d;

    public static double Clamp01(double _t)
    {
        if (0d > _t) return 0d;
        if (1d < _t) return 1d;
        return _t;
    }

    public static double OutCubic(double _t)
    {
        double t = Clamp01(_t);
        return 1d - Math.Pow(1d - t, 3d);
    }

    public static double InCubic(double _t)
    {
        double t = Clamp01(_t);
        return Math.Pow(t, 3d);
    }

    public static double OutBack(double _t)
    {
        double t = Clamp01(_t) - 1d;
        return t * t * ((BackOvershoot + 1d) * t + BackOvershoot) + 1d;
    }

    /// <summary>_t가 [_a, _b]에서 0 ~ 1로 가는 비율. _b가 _a 이하이면 _t가 _b 이상일 때 1.</summary>
    public static double Phase(double _t, double _a, double _b)
    {
        if (_b > _a) return Clamp01((_t - _a) / (_b - _a));
        return _t >= _b ? 1d : 0d;
    }
}
