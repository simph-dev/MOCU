using System;
using System.Collections.Concurrent;
using UnityEngine;


public class UnityMainThreadDispatcher : ManagedMonoBehaviour
{
    private static readonly ConcurrentQueue<Action> _executionQueue = new();

    public override void ManagedAwake() { }

    public override void ManagedStart()
    {
        CanUseUpdateMethod = true;
    }

    public override void ManagedUpdate()
    {
        while (_executionQueue.TryDequeue(out var action))
            action?.Invoke();
    }

    public override void ManagedOnDisable()
    {
        ClearQueue();
    }

    public static void Enqueue(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        _executionQueue.Enqueue(action);
    }

    public static void ClearQueue()
    {
        while (_executionQueue.TryDequeue(out _)) { }
    }
}