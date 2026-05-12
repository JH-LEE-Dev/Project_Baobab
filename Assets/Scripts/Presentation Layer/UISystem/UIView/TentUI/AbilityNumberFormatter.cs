using System;
using System.Globalization;
using UnityEngine;

public static class AbilityNumberFormatter
{
    private static readonly long[] CompactDivisors =
    {
        1_000_000_000_000_000L,
        1_000_000_000_000L,
        1_000_000_000L,
        1_000_000L,
        1_000L,
    };

    private static readonly string[] CompactSuffixes =
    {
        "Q",
        "T",
        "B",
        "M",
        "K",
    };

    // 재화 수치를 툴팁용 축약 표기로 바꾼다. 예: 1000 -> 1K, 1100 -> 1.1K
    public static string FormatCompact(long _value)
    {
        long absValue = _value == long.MinValue ? long.MaxValue : Math.Abs(_value);

        for (int i = 0; i < CompactDivisors.Length; i++)
        {
            long divisor = CompactDivisors[i];
            if (absValue < divisor)
                continue;

            double scaledValue = _value / (double)divisor;
            string numberText = scaledValue.ToString("0.#", CultureInfo.InvariantCulture);
            return numberText + CompactSuffixes[i];
        }

        return _value.ToString(CultureInfo.InvariantCulture);
    }

    // 공식 계산 결과를 재화 정수값으로 해석한다.
    public static long RoundToLong(float _value)
    {
        if (float.IsNaN(_value) || float.IsInfinity(_value))
            return 0L;

        return (long)Math.Round(_value, MidpointRounding.AwayFromZero);
    }
}
