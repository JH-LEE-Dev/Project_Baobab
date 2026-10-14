using System;

/// <summary>
/// 런 전용 골드. 카드 구매와 리롤에만 쓰는 단일 재화다(기획서 3-3).
/// 본편 소지금(InventoryManager.money)과는 완전히 별개이며 저장되지 않는다.
/// </summary>
public sealed class RunWallet
{
    /// <summary>(현재 골드, 이번 변화량)</summary>
    public event Action<long, long> GoldChangedEvent;

    public long Gold { get; private set; }

    /// <summary>이번 판에서 실제로 흡수된 골드 총합. 결과 화면 "총 획득 코인량"(기획서 3-7).</summary>
    public long TotalEarned { get; private set; }

    public void Reset()
    {
        Gold = 0;
        TotalEarned = 0;
        GoldChangedEvent?.Invoke(Gold, 0);
    }

    public void Add(long _amount)
    {
        if (_amount <= 0) return;

        Gold += _amount;
        TotalEarned += _amount;
        GoldChangedEvent?.Invoke(Gold, _amount);
    }

    public bool CanAfford(long _amount) => _amount <= Gold;

    public bool TrySpend(long _amount)
    {
        if (_amount < 0 || _amount > Gold) return false;
        if (0 == _amount) return true;

        Gold -= _amount;
        GoldChangedEvent?.Invoke(Gold, -_amount);
        return true;
    }
}
