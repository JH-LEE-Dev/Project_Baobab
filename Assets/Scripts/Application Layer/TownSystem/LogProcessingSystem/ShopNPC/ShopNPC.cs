using UnityEngine;
using System;
using UnityEngine.Rendering;

public class ShopNPC : MonoBehaviour, IShopNPC
{
    public event Action ShopMoneyChangedEvent;
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

    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject frontObject;

    private CustomSortable customSortable;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(GetComponent<SortingGroup>());

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

    public void LoadSaveData(int _money, bool _bFirstTime)
    {
        money = _money;
        bFirstTimeEarnMoney = _bFirstTime;
        ShopMoneyChangedEvent?.Invoke();
    }

    public bool GetbFirstTimeEarnMoney()
    {
        return bFirstTimeEarnMoney;
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
            frontObject.SetActive(false);
            outLineObject.SetActive(true);

            InteractStateEvent?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            frontObject.SetActive(true);
            outLineObject.SetActive(false);
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

    private void Update()
    {
        if (customSortable != null)
            customSortable.SetHeight(0f);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }
}
