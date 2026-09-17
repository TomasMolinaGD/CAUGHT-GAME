using System;
using System.Collections.Generic;
using UnityEngine;

public enum PoliceState
{
    idle,
    Patrol,
    Pursuit,
    Attack,
    Gather
}

public class StateMachine
{
    public State CurrentState { get; private set; }
    public Enum CurrentStateKey { get; private set; }

    private readonly Dictionary<Enum, State> states = new Dictionary<Enum, State>();

    public void RegisterState(Enum key, State state)
    {
        states[key] = state;
    }

    public bool ChangeState(Enum key)
    {
        if (!states.TryGetValue(key, out State newState))
        {
            Debug.LogWarning($"FSM state '{key}' is not registered.");
            return false;
        }

        if (newState == CurrentState)
        {
            return false;
        }

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentStateKey = key;
        CurrentState.Enter();
        return true;
    }

    public bool HasState(Enum key)
    {
        return states.ContainsKey(key);
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public void Stop()
    {
        CurrentState?.Exit();
        CurrentState = null;
        CurrentStateKey = null;
    }
}
