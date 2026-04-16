using System;
using UnityEngine;

public interface IShopNPC
{
    public Transform npcTransform { get; }
    public int currentMoney { get; }
    public event Action ShopMoneyChangedEvent;
}
