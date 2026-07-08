using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Text;


namespace InterprocessCommunication
{
    public class InterprocessCommunicator_Client : InterprocessCommunicator_Base
    {
        private int _connectionToServerTimeoutMs;

        private NamedPipeClientStream readPipe;
        private NamedPipeClientStream writePipe;

        public InterprocessCommunicator_Client(string pipeName) : base(pipeName)
        {
            _connectionToServerTimeoutMs = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;   // todo: move to config file

            readPipe = new NamedPipeClientStream(".", pipeName_serverWritesClientReads, PipeDirection.In, PipeOptions.Asynchronous);
            writePipe = new NamedPipeClientStream(".", pipeName_clientWritesServerReads, PipeDirection.Out, PipeOptions.Asynchronous);

            reader = new StreamReader(readPipe, new UTF8Encoding(false));
            writer = new StreamWriter(writePipe, new UTF8Encoding(false));
        }

        public override async void Start()
        {
            try
            {
                if (IsOperational)
                    throw new InvalidOperationException("The communicator is already running.");

                var readConnectionTask = readPipe.ConnectAsync(_connectionToServerTimeoutMs);
                var writeConnectionTask = writePipe.ConnectAsync(_connectionToServerTimeoutMs);

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