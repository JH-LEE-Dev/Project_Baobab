using System;
using UnityEngine;

/// <summary>
/// 대시 충전 횟수. 쓸 때 1을 소모하고, 초당 rechargePerSecond만큼 차오르며, maxCharges까지 모아 둘 수 있다.
/// (기본값: 초당 0.25 = 4초에 1회, 최대 1회. 추후 최대 2회까지 늘릴 수 있게 최대치를 따로 둔다)
///
/// 값은 소수로 들고 있다. 정수 부분이 지금 쓸 수 있는 횟수이고, 소수 부분이 다음 1회가 차오른 정도다.
/// 최대 횟수와 충전 속도는 Configure로 언제든 바꿀 수 있다(특성·전리품으로 저장 횟수가 늘어나는 경우, 플레이 중 튜닝).
/// </summary>
public class DashCharge : IDashState
{
    public event Action DashStateChangedEvent;

    // 0.25씩 매 물리 틱 더하면 4초 뒤 0.99999… 처럼 1에 살짝 못 미칠 수 있어 이만큼은 찬 것으로 본다.
    private const float ChargeEpsilon = 0.0001f;

    private int maxCharges;
    private float rechargePerSecond;

    private float charge;
    private bool bUnlockedValue;

    public DashCharge(int _maxCharges, float _rechargePerSecond)
    {
        maxCharges = Mathf.Max(1, _maxCharges);
        rechargePerSecond = Mathf.Max(0f, _rechargePerSecond);
        charge = maxCharges;
    }

    /// <summary>
    /// 최대 횟수와 충전 속도를 바꾼다. 최대 횟수가 늘면 지금 횟수는 그대로 두고 늘어난 칸을 이어서 채우고,
    /// 줄면 넘치는 만큼 버린다. 최대 횟수가 바뀌면 DashStateChangedEvent를 보낸다(HUD 칸 수 갱신).
    /// </summary>
    public void Configure(int _maxCharges, float _rechargePerSecond)
    {
        rechargePerSecond = Mathf.Max(0f, _rechargePerSecond);

        int newMaxCharges = Mathf.Max(1, _maxCharges);
        if (newMaxCharges == maxCharges) return;

        maxCharges = newMaxCharges;
        if (charge > maxCharges) charge = maxCharges;

        DashStateChangedEvent?.Invoke();
    }

    public bool bUnlocked => bUnlockedValue;
    public int CurrentCharges => Mathf.Min(maxCharges, Mathf.FloorToInt(charge + ChargeEpsilon));
    public int MaxCharges => maxCharges;
    public float RechargeProgress => (CurrentCharges >= maxCharges) ? 1f : Mathf.Clamp01(charge - CurrentCharges);

    /// <summary>대시를 쓸 수 있는 횟수가 1 이상인지.</summary>
    public bool HasCharge => CurrentCharges >= 1;

    public void SetUnlocked(bool _bUnlocked)
    {
        if (bUnlockedValue == _bUnlocked) return;

        bUnlockedValue = _bUnlocked;
        DashStateChangedEvent?.Invoke();
    }

    /// <summary>전부 채운다. 새 판(씬 진입·재시작)을 시작할 때 부른다.</summary>
    public void Refill()
    {
        int before = CurrentCharges;
        charge = maxCharges;

        if (before != CurrentCharges) DashStateChangedEvent?.Invoke();
    }

    /// <summary>1회 소모한다. 남은 횟수가 없으면 아무것도 하지 않고 false.</summary>
    public bool TryConsume()
    {
        if (false == HasCharge) return false;

        charge = Mathf.Max(0f, charge - 1f);
        DashStateChangedEvent?.Invoke();
        return true;
    }

    /// <summary>시간만큼 충전한다. 물리 틱마다 부른다.</summary>
    public void Tick(float _deltaTime)
    {
        if (charge >= maxCharges) return;

        int before = CurrentCharges;
        charge = Mathf.Min(maxCharges, charge + rechargePerSecond * _deltaTime);

        if (before != CurrentCharges) DashStateChangedEvent?.Invoke();
    }
}
