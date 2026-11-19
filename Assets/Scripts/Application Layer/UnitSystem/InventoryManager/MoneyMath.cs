using System;

/// <summary>
/// 큰 금액 계산용. float 배율을 그대로 곱하면 1.3이 1.29999995로 들어가 큰 금액에서 몇십~몇천 원씩 어긋나고,
/// float로 담긴 1e+12는 999,999,995,904가 된다. 값을 입력한 그대로의 십진수(float 유효숫자 7자리,
/// double 15자리)로 되돌린 뒤 decimal로 곱하고 반올림해서 기획 값과 정확히 같은 금액을 만든다.
/// </summary>
public static class MoneyMath
{
    // long 최댓값(약 9.22e18)보다 넉넉히 크고, 서로 몇 번 곱해도 decimal 범위(약 7.9e28)를 거의 넘지 않는 크기
    private const double ClampAbs = 1e20;

    public static decimal ToDecimal(float _value)
    {
        if (float.IsNaN(_value)) return 0m;
        if (_value >= ClampAbs) return (decimal)ClampAbs;
        if (_value <= -ClampAbs) return -(decimal)ClampAbs;
        return (decimal)_value; // 유효숫자 7자리로 반올림 → 1.29999995f는 1.3
    }

    public static decimal ToDecimal(double _value)
    {
        if (double.IsNaN(_value)) return 0m;
        if (_value >= ClampAbs) return (decimal)ClampAbs;
        if (_value <= -ClampAbs) return -(decimal)ClampAbs;
        return (decimal)_value; // 유효숫자 15자리로 반올림
    }

    /// <summary>반올림해 long으로 바꾼다. long 범위를 넘으면 끝값으로 막는다.</summary>
    public static long RoundToLong(decimal _value, MidpointRounding _mode = MidpointRounding.ToEven)
    {
        decimal rounded = Math.Round(_value, _mode);
        if (rounded >= long.MaxValue) return long.MaxValue;
        if (rounded <= long.MinValue) return long.MinValue;
        return (long)rounded;
    }

    /// <summary>금액 × 배율. 결과가 아주 커도 예외 없이 long 끝값으로 막는다.</summary>
    public static long MultiplyRound(long _value, float _multiplier)
    {
        try
        {
            return RoundToLong(_value * ToDecimal(_multiplier));
        }
        catch (OverflowException)
        {
            return SaturatedSign(_value, _multiplier);
        }
    }

    /// <summary>금액 × 배율1 × 배율2. 결과가 아주 커도 예외 없이 long 끝값으로 막는다.</summary>
    public static long MultiplyRound(long _value, float _multiplier1, float _multiplier2)
    {
        try
        {
            return RoundToLong(_value * ToDecimal(_multiplier1) * ToDecimal(_multiplier2));
        }
        catch (OverflowException)
        {
            return SaturatedSign(_value, _multiplier1 * (double)_multiplier2);
        }
    }

    private static long SaturatedSign(long _value, double _multiplier)
    {
        bool bNegative = (_value < 0) != (_multiplier < 0);
        return bNegative ? long.MinValue : long.MaxValue;
    }
}
