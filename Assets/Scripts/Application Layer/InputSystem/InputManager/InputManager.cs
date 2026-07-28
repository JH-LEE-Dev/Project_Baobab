using System;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public InputReader inputReader { get; private set; }

    private bool bCursorHoveredOnUI = false;

    public void Initialize()
    {
        inputReader = new InputReader();

        if (inputReader == null)
        {
            Debug.Log("inputReader is null -> InputManager::Initialize");
            return;
        }

        inputReader.Initialize();
    }

    public void Release()
    {
        inputReader.Release();
    }

    public void OnDestroy()
    {
        inputReader?.Release();
    }

    public void PauseMove(bool _bPause)
    {
        inputReader.PauseMove(_bPause);
    }

    public void ClearPrevKeyboardInput()
    {
        inputReader.ClearPrevKeyboardInput();
    }

    public void SetCursorHoveredOnUI(bool _bCursorHoveredOnUI)
    {
        bCursorHoveredOnUI = _bCursorHoveredOnUI;
    }

    public bool IsCursorHoveredOnUI()
    {
        return bCursorHoveredOnUI;
    }

    public void PauseInteractKey(bool _boolean)
    {
        inputReader.PauseInteractKey(_boolean);
    }

    public void PauseESCKey(bool _boolean)
    {
        inputReader.PauseESCKey(_boolean);
    }

    // 키 리바인딩 (실제 처리는 inputReader 위임, KeyBindingsChangedEvent는 inputManager.inputReader에서 직접 구독)
    public bool IsRebinding => inputReader.IsRebinding;

    public IReadOnlyList<ERebindableAction> GetRebindableActions()
    {
        return inputReader.GetRebindableActions();
    }

    public string GetBindingDisplayString(ERebindableAction _action)
    {
        return inputReader.GetBindingDisplayString(_action);
    }

    public string GetBindingPath(ERebindableAction _action)
    {
        return inputReader.GetBindingPath(_action);
    }

    public bool IsConflicting(ERebindableAction _action)
    {
        return inputReader.IsConflicting(_action);
    }

    public bool HasAnyConflict()
    {
        return inputReader.HasAnyConflict();
    }

    public void BeginEditSession()
    {
        inputReader.BeginEditSession();
    }

    public void DiscardEditSession()
    {
        inputReader.DiscardEditSession();
    }

    public bool CommitEditSession()
    {
        return inputReader.CommitEditSession();
    }

    public void StartRebind(ERebindableAction _action, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        inputReader.StartRebind(_action, _onFinished);
    }

    public void CancelRebind()
    {
        inputReader.CancelRebind();
    }

    public void ResetBinding(ERebindableAction _action)
    {
        inputReader.ResetBinding(_action);
    }

    public void ResetAllBindings()
    {
        inputReader.ResetAllBindings();
    }
}
