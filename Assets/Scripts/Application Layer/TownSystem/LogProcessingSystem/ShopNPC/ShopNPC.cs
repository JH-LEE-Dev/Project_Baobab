using UnityEngine;
using System;

public class ShopNPC : MonoBehaviour, IShopNPC
{
    public event Action ShopMoneyChangedEvent;
    public event Action FirstTimeEarnMoneyEvent;
    public event Action<bool> InteractStateEvent;
    public event Action<int> EarnMoneyEvent;

    [SerializeField] private Transform npcTransform;

    private bool bCanInteract = false;

    private InputManager inputManager;
    private int money;

    private const string PLAYER_TAG = "Player";

    private bool bFirstTimeEarnMoney = true;

    Transform IShopNPC.npcTransform => npcTransform;

    public int currentMoney => money;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        money = 0;

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void InsertMoney(int _money)
    {
        money += _money;
        ShopMoneyChangedEvent?.Invoke();
    }

    public int GetMoney()
    {
        return money;
    }

    private void BindEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractKeyPressed;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractKeyPressed;
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bCanInteract = true;

            InteractStateEvent?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bCanInteract = false;
            InteractStateEvent?.Invoke(false);
        }
    }

    private void InteractKeyPressed()
    {
        if (money == 0 || bCanInteract == false)
            return;

        EarnMoneyEvent?.Invoke(money);

        if (bFirstTimeEarnMoney == true)
        {
            //FirstTimeEarnMoneyEvent?.Invoke();
            bFirstTimeEarnMoney = false;
        }

        money = 0;
        ShopMoneyChangedEvent?.Invoke();
    }
}
