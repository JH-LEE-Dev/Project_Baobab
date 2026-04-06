using PresentationLayer.UISystem.View;

public class GameplayUICoordinator
{
    private UIView_Popup popUpUI;
    private InputManager inputManager;
    private UIView_Unit unitUI;

    private SignalHub signalHub;
    private UIView_HUD hudUI;
    private UIView_WorldPopup worldPopupUI;

    private bool bInventoryOpened = false;

    public void Initialize(SignalHub _signalHub, InputManager _inputManager, UIView_Popup _popUpUI, UIView_HUD _hudUI,
     UIView_Unit _unitUI,UIView_WorldPopup _worldPopupUI)
    {
        inputManager = _inputManager;
        popUpUI = _popUpUI;
        hudUI = _hudUI;
        signalHub = _signalHub;
        unitUI = _unitUI;
        worldPopupUI = _worldPopupUI;


        SubscribeSignals();
        BindEvents();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.Subscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
        signalHub.Subscribe<ContainerUpdatedSignal>(ContainerUpdated);
        signalHub.Subscribe<ContainerInteractStateChangedSignal>(ContainerInteractStateChanged);
        signalHub.Subscribe<CharacterEarnMoneySignal>(CharacterEarnMoney);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.UnSubscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
        signalHub.UnSubscribe<ContainerUpdatedSignal>(ContainerUpdated);
        signalHub.UnSubscribe<ContainerInteractStateChangedSignal>(ContainerInteractStateChanged);
        signalHub.UnSubscribe<CharacterEarnMoneySignal>(CharacterEarnMoney);
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;

        popUpUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        popUpUI.GoHomeButtonClickedEvent += GoHomeButtonClicked;

        popUpUI.SendDeleteItemEvent -= SendDeleteItem;
        popUpUI.SendDeleteItemEvent += SendDeleteItem;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        popUpUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        popUpUI.SendDeleteItemEvent -= SendDeleteItem;
    }

    public void Release()
    {
        UnSubscribeSignals();
        ReleaseEvents();
    }

    private void OnInventoryKeyPressed()
    {
        if (bInventoryOpened == false)
        {
            bInventoryOpened = true;
            popUpUI.Show();
        }
        else
        {
            bInventoryOpened = false;
            popUpUI.Hide();
        }
    }

    private void InventoryUpdated(InventoryUpdatedSignal inventoryUpdatedSignal)
    {
        popUpUI.InventoryShowEvent();
    }

    private void TreeGetHit(TreeGetHitSignal treeGetHitSignal)
    {
        unitUI.TreeGetHit(treeGetHitSignal.treeObj);
    }

    private void CharacterSpawned(CharacterSpawendSignal characterSpawendSignal)
    {
        hudUI.SetCharacter(characterSpawendSignal.character);
    }

    private void GoHomeButtonClicked()
    {
        signalHub.Publish(new GoHomeButtonClickedSignal());
    }

    private void SendDeleteItem(IInventorySlot _inData)
    {
        signalHub.Publish(new DeleteItemSignal(_inData));
    }

    private void ContainerUpdated(ContainerUpdatedSignal containerUpdatedSignal)
    {
        worldPopupUI.ContainerUpdated();
    }

    private void ContainerInteractStateChanged(ContainerInteractStateChangedSignal containerInteractStateChangedSignal)
    {
        worldPopupUI.LogContainerInteractStateChanged(containerInteractStateChangedSignal.state);
    }

    private void CharacterEarnMoney(CharacterEarnMoneySignal characterEarnMoneySignal)
    {
        popUpUI.CharacterEarnMoney();
    }
}
