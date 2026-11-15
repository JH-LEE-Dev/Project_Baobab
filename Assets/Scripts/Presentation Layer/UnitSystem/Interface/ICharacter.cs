using System;
using UnityEngine;

public interface ICharacter
{
    public IPHealthComponent pHealthComponent { get; }
    public Transform GetTransform();
    public IStatComponent statComponent { get; }
    public IArmComponent armComponent { get; }
    public IOverheatComponent overheatState { get; }
    public IDashState dashState { get; }
    public bool bDead { get; }
    public bool bRide { get; }
    public bool bAxeDeposited { get; }
    // 도끼를 모루에 맡겼을 때 true, 돌려받았을 때 false(화면 도끼 HUD가 내려가고 올라오는 데 쓴다).
    public event Action<bool> AxeDepositStateChangedEvent;
}
