using System;
using System.Collections.Generic;
using UnityEngine;

public class LumberjackStateMachine
{
    private Dictionary<Type, LumberjackState> states = new Dictionary<Type, LumberjackState>();
    public LumberjackState CurrentState { get; private set; }

    public void AddState(LumberjackState _state)
    {
        states[_state.GetType()] = _state;
    }

    public void ChangeState<T>() where T : LumberjackState
    {
        if (CurrentState != null)
        {
            CurrentState.Exit();
        }

        if (states.TryGetValue(typeof(T), out LumberjackState nextState))
        {
            // TEMP DEBUG: 어느 NPC가 언제 어떤 상태로 전환됐는지 전부 남긴다 (멈춤 버그 추적용).
            var npcRef = nextState.Npc;
            string npcId = npcRef != null ? $"{npcRef.name}({npcRef.GetEntityId()})" : "unknown";
            LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={npcId} state: {CurrentState?.GetType().Name ?? "null"} -> {typeof(T).Name}");

            CurrentState = nextState;
            CurrentState.Enter();
        }
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }

    public void ReleaseAllState()
    {
        CurrentState = null;
        states.Clear();
    }
}
