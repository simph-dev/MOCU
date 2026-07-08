using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;


namespace InterprocessCommunication
{
    public class InterprocessCommunicator_Server : InterprocessCommunicator_Base
    {
        private const int _bufferSize = 131_072;    // larger pipe buffer -- 128 KB (default is 4 KB)
        private readonly TaskCompletionSource<bool> _serverIsReadyForClientConnection = new();
        public Task WaitForServerReadyForClientConnectionAsync() => _serverIsReadyForClientConnection.Task;

        private NamedPipeServerStream readPipe;
        private NamedPipeServerStream writePipe;

        public InterprocessCommunicator_Server(string pipeName) : base(pipeName)
        {
            readPipe = new NamedPipeServerStream(
                pipeName_clientWritesServerReads,
                PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: _bufferSize,
                outBufferSize: 0);

            writePipe = new NamedPipeServerStream(
                pipeName_serverWritesClientReads,
                PipeDirection.Out,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: _bufferSize);

            reader = new StreamReader(readPipe, new UTF8Encoding(false));
            writer = new StreamWriter(writePipe, new UTF8Encoding(false));
        }

        public override async void Start()
        {
            try
            {
                if (IsOperational)
                    throw new InvalidOperationException("The communicator is already running.");

                var readConnectionTask = readPipe.WaitForConnectionAsync();
                var writeConnectionTask = writePipe.WaitForConnectionAsync();

                // Now client side may try to connect
                _serverIsReadyForClientConnection.SetResult(true);

                await Task.WhenAll(readConnectionTask, writeConnectionTask);
                base.Start();
            }
            catch
            {
                Stop();
            }
        }

        public override void Stop(string reason = "unknown")
        {
            try { base.Stop(reason); } catch { }
            try { readPipe.Dispose(); } catch { }
            try { writePipe.Dispose(); } catch { }
        }
    }
}