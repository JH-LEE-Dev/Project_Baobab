using System;
using UnityEngine;

public interface IShopNPC
{
    public Transform npcTransform { get; }
    public long currentMoney { get; }
    public event Action ShopMoneyChangedEvent;
}
