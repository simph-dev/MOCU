using UnityEngine;
using System;
using System.Collections.Concurrent;

using Debug = UnityEngine.Debug;


public class UnityUtilities
{
    public static void ConsoleError(string message)
    {
        Debug.LogError(message);
    }

    public static void ConsoleWarning(string message)
    {
        Debug.LogWarning(message);
    }

    public static void ConsoleInfo(string message)
    {
        Debug.Log(message);
    }
}


    /// <summary>
    /// A flexible (but not the fastest) mechanism for calling functions in the main thread from side threads (unity simply ignores them)
    /// </summary>
    /*public class UnityMainThreadDispatcher : MonoBehaviour
    {
        /// <summary>
        /// Needed to secure adding a pair (method, parameters) to the queue
        /// so that it doesn’t turn out that some will be transmitted and some will not
        /// </summary>
        private struct Invocation
        {
            public Delegate Method;
            public object[] Parameters;

            public Invocation(Delegate method, object[] parameters)
            {
                Method = method;
                Parameters = parameters;
            }
        }

        private static readonly ConcurrentQueue<Invocation> _invocationQueue = new();

        /// <summary>
        /// Adds a tuple (method, its parameters) to the queue in order to call it in the main thread at the next frame
        /// </summary>
        public static void ExecuteOnMainThread(Delegate method, params object[] parameters)
        {
            _invocationQueue.Enqueue(new Invocation(method, parameters));
        }

        void Update()
        {
            while (_invocationQueue.TryDequeue(out var invocation))
            {
                // "DynamicInvoke" is slower than known delegate with known parameters
                invocation.Method.DynamicInvoke(invocation.Parameters);
            }
        }
    }*/