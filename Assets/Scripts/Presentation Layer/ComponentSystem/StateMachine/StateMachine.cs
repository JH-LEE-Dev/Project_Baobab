using System;
using System.Collections.Generic;

public class StateMachine
{
    private State currentState;
    private Dictionary<Type, State> states = new Dictionary<Type, State>();

    public void AddState(State _state)
    {
        states[_state.GetType()] = _state;
    }

    public void ChangeState<T>() where T : State
    {
        var type = typeof(T);

        if (!states.ContainsKey(type))
            throw new Exception($"{type} State is not registered.");

        currentState?.Exit();
        currentState = states[type];
        currentState?.Enter();
    }

    public void Update()
    {
        currentState?.Update();
    }

    /// <summary>
    /// 현재 상태의 물리 업데이트를 실행합니다.
    /// </summary>
    public void FixedUpdate()
    {
        currentState?.FixedUpdate();
    }

    public bool IsState<T>() where T : State
    {
        return currentState != null && currentState.GetType() == typeof(T);
    }

    public T GetState<T>() where T : State
    {
        var type = typeof(T);

        if (!states.TryGetValue(type, out State instance) || instance == null)
        {
            return default(T);
        }

        return (T)instance;
    }

    public void ReleaseAllState()
    {
        foreach (var (type, state) in states)
        {
            state.Release();
        }
    }
}
