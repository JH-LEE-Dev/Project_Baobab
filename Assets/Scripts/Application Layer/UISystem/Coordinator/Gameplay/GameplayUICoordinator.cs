using System;
using System.Collections.Generic;
using UnityEngine;

public class GameplayUICoordinator
{
    private UIView_Inventory inventoryUI;
    private InputManager inputManager;

    private bool bInventoryOpened = false;

    public void Initialize(InputManager _inputManager, UIView_Inventory _inventoryUI)
    {
        inputManager = _inputManager;
        inventoryUI = _inventoryUI;

        BindEvents();
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void OnInventoryKeyPressed()
    {
        if (bInventoryOpened == false)
        {
            bInventoryOpened = true;
            inventoryUI.Show();
        }
        else
        {
            bInventoryOpened = false;
            inventoryUI.Hide();
        }
    }
}
