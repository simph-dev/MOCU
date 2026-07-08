/// <summary>
/// Overrides the base events for the Unity environment.
/// Those events wrap all subscribers in the `UnityMainThreadDispatcher` to ensure 
/// they are invoked on Unity's main thread. 
/// 
/// The `new` keyword is used to hide the events from the base class 
/// `InterprocessCommunicator_Base`, allowing custom handling specific to Unity.
/// 
/// Note:
/// - This approach allows Unity-specific behavior without altering the base class.
/// - Existing classes that use the base class events directly are unaffected
///   by this implementation.
/// </summary>


using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;


namespace InterprocessCommunication
{
    public class InterprocessCommunicator_UnityServer : InterprocessCommunicator_Server, IInterprocessCommunicator
    {
        public InterprocessCommunicator_UnityServer(string pipeName) : base(pipeName) { }

        public new event Action<string> MessageReceived
        {
            add
            {
                base.MessageReceived += (message) =>
                {
                    UnityMainThreadDispatcher.Enqueue(() => value?.Invoke(message));
                };
            }
            remove
            {
                base.MessageReceived -= value;
            }
        }

        public new event Action<string> MessageSent
        {
            add
            {
                base.MessageSent += (message) =>
                {
                    UnityMainThreadDispatcher.Enqueue(() => value?.Invoke(message));
                };
            }
            remove
            {
                base.MessageSent -= value;
            }
        }

        public new event Action<string> ErrorOccurred
        {
            add
            {
                base.ErrorOccurred += (message) =>
                {
                    UnityMainThreadDispatcher.Enqueue(() => value?.Invoke(message));
                };
            }
            remove
            {
                base.ErrorOccurred -= value;
            }
        }

        public new event Action<string> ConnectionEstablished
        {
            add
            {
                base.ConnectionEstablished += (message) =>
                {
                    UnityMainThreadDispatcher.Enqueue(() => value?.Invoke(message));
                };
            }
            remove
            {
                base.ConnectionEstablished -= value;
            }
        }

        public new event Action<string> ConnectionBroked
        {
            add
            {
                base.ConnectionBroked += (message) =>
                {
                    UnityMainThreadDispatcher.Enqueue(() => value?.Invoke(message));
                };
            }
            remove
            {
                base.ConnectionBroked -= value;
            }
        }
    }
}