/// <summary>
/// 돈(코인) 보유 상한. 캐릭터 소지금과 상점 잔액 모두 999.99Q(999,990,000,000,000,000)를 넘지 않는다.
/// 상한에 닿은 뒤 들어오는 돈은 버려진다.
/// </summary>
public static class MoneyLimit
{
    public const long Max = 999_990_000_000_000_000L;

    /// <summary>_current에 _amount를 더하되 상한을 넘지 않게 한다. 더하는 값이 아주 커도 long이 넘치지 않는다.</summary>
    public static long Add(long _current, long _amount)
    {
        if (0 >= _amount) return Clamp(_current + _amount);
        if (_current >= Max || _amount >= Max - _current) return Max;
        return _current + _amount;
    }

    public static long Clamp(long _value)
    {
        return _value > Max ? Max : _value;
    }
}
