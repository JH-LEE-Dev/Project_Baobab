using System;
using System.Collections.Generic;

public class PorterStateMachine
{
    private Dictionary<Type, PorterState> states = new Dictionary<Type, PorterState>();
    public PorterState CurrentState { get; private set; }

    public void AddState(PorterState _state)
    {
        states[_state.GetType()] = _state;
    }

    public void ChangeState<T>() where T : PorterState
    {
        if (CurrentState != null)
        {
            CurrentState.Exit();
        }

        if (states.TryGetValue(typeof(T), out PorterState nextState))
        {
            CurrentState = nextState;
            CurrentState.Enter();
        }
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public void ReleaseAllState()
    {
        CurrentState = null;
        states.Clear();
    }
}
