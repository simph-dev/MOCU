using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


// Since 'reader.ReadLine' reads the message line by line,
// formatted JSON may be split into multiple messages and misinterpreted.
//
// To prevent this issue, the message is converted into a single-line format before being sent.
// After receiving it, the reverse transformation restores the original format, ensuring
// that the external user receives an unmodified message while maintaining correct internal processing.
//
// 'ProtectString' and 'UnprotectString' are executed in 'WriteLoop' and 'ReadLoop' respectively.
// Their performance impact should be minimal and should not significantly block the thread.


namespace InterprocessCommunication
{
    public abstract class InterprocessCommunicator_Base : IInterprocessCommunicator
    {
        private volatile bool _isOperational;
        public bool IsOperational
        {
            get => _isOperational;
            private set => _isOperational = value;
        }

        protected StreamWriter writer;
        protected StreamReader reader;
        protected CancellationTokenSource cancellationTokenSource;
        private TaskCompletionSource<bool> _connectionEstablished;

        protected BlockingCollection<string> receivedMessagesQueue;
        protected BlockingCollection<string> toBeSentMessagesQueue;
        protected BlockingCollection<string> sentMessagesQueue;
        protected BlockingCollection<string> debugMessagesQueue;

        private string _pipeName;
        protected string pipeName_clientWritesServerReads;
        protected string pipeName_serverWritesClientReads;

        public event Action<string> MessageReceived;
        public event Action<string> MessageSent;
        public event Action<string> ConnectionEstablished;
        public event Action<string> ConnectionBroked;
        public event Action<string> ErrorOccurred;

        private Task _readLoop;
        private Task _writeLoop;
        private Task _processReceivedMessagesLoop;
        private Task _processSentMessagesLoop;
        private Task _processDebugMessagesLoop;

        public InterprocessCommunicator_Base(string pipeName)
        {
            _pipeName = pipeName;

            receivedMessagesQueue   = new BlockingCollection<string>();
            toBeSentMessagesQueue   = new BlockingCollection<string>();
            sentMessagesQueue       = new BlockingCollection<string>();
            debugMessagesQueue      = new BlockingCollection<string>();

            pipeName_clientWritesServerReads = $"{_pipeName}C2S";
            pipeName_serverWritesClientReads = $"{_pipeName}S2C";

            cancellationTokenSource = new CancellationTokenSource();
            _connectionEstablished  = new TaskCompletionSource<bool>();
            IsOperational = false;
        }

        public virtual void Start()
        {
            _readLoop                       = Task.Run(() => ReadLoop());
            _writeLoop                      = Task.Run(() => WriteLoop());
            _processReceivedMessagesLoop    = Task.Run(() => ProcessReceivedMessagesLoop());
            _processSentMessagesLoop        = Task.Run(() => ProcessSentMessagesLoop());
            _processDebugMessagesLoop       = Task.Run(() => ProcessDebugMessagesLoop());

            IsOperational = true;

            try
            {
                ConnectionEstablished?.Invoke($"Pipe name: {_pipeName}");
            }
            catch (Exception ex)
            {
                debugMessagesQueue.Add($"Error occurred while trying to execute 'Start', handled without termination: {ex}");
            }
            
            _connectionEstablished.TrySetResult(true);
        }

        public virtual void Stop(string reason = "unknown")
        {
            if (!IsOperational)
                return;

            IsOperational = false;
            cancellationTokenSource?.Cancel();

            reader?.Dispose();
            writer?.Dispose();

            receivedMessagesQueue?.Dispose();
            toBeSentMessagesQueue?.Dispose();

            cancellationTokenSource?.Dispose();

            try
            {
                ConnectionBroked?.Invoke($"Communicator stopped. Reason: {reason}");
            }
            catch (Exception ex)
            {
                // todo: rethink that
                debugMessagesQueue.Add($"Error occurred while trying to execute 'Stop', handled without termination: {ex}");
                Console.WriteLine($"Error occurred while trying to execute 'Stop', handled without termination: {ex}");
            }
        }

        public void SendMessage(string message)
        {
            try
            {
                toBeSentMessagesQueue.Add(message);   
            }
            catch (Exception ex)
            {
                debugMessagesQueue.Add($"Error occurred while trying to execute 'SendMessage(message)', handled without termination: {ex}");
            }
        }

        private string ProtectString(string input)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }

        private string UnprotectString(string encoded)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        private async Task ReadLoop()
        {
            try
            {
                var token = cancellationTokenSource.Token;
                while (!token.IsCancellationRequested)
                {
                    var message = await reader.ReadLineAsync();

                    if (message != null)
                        receivedMessagesQueue.Add(UnprotectString(message), token);
                    else
                        throw new IOException("Got 'null' in 'ReadLoop'");
                }
            }
            catch (Exception ex)
            {
                Stop($"Error occurred while trying to read a message: {ex}");
            }
        }

        private async Task WriteLoop()
        {
            try
            {
                await _connectionEstablished.Task;  // to be able to add messages to queue even before the communicator is ready

                foreach (var message in toBeSentMessagesQueue.GetConsumingEnumerable(cancellationTokenSource.Token))
                {
                    await writer.WriteLineAsync(ProtectString(message));
                    await writer.FlushAsync();

                    sentMessagesQueue.Add(message);
                }
            }
            catch (Exception ex)
            {
                Stop($"Error occurred while trying to write a message: {ex}");
            }
        }

        private void ProcessReceivedMessagesLoop()
        {
            foreach (var message in receivedMessagesQueue.GetConsumingEnumerable(cancellationTokenSource.Token))
            {
                try
                {
                    MessageReceived?.Invoke(message);
                }
                catch (Exception ex)
                {
                    debugMessagesQueue.Add($"Error occurred while trying to execute 'MessageReceived?.Invoke(message)', handled without termination: {ex}");
                }
            }
        }

        private void ProcessSentMessagesLoop()
        {
            foreach (var message in sentMessagesQueue.GetConsumingEnumerable(cancellationTokenSource.Token))
            {
                try
                {
                    MessageSent?.Invoke(message);
                }
                catch (Exception ex)
                {
                    debugMessagesQueue.Add($"Error occurred while trying to execute 'MessageSent?.Invoke(message)', handled without termination: {ex}");
                }
            }
        }

        private void ProcessDebugMessagesLoop()
        {
            foreach (var message in debugMessagesQueue.GetConsumingEnumerable(cancellationTokenSource.Token))
            {
                try
                {
                    ErrorOccurred?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error occurred while trying to execute 'ProcessDebugMessagesLoop', handled without termination: {ex}");
                }
            }
        }
    }
}