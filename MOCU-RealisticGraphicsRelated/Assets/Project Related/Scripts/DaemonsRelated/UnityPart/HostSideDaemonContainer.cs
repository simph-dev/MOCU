using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

using InterprocessCommunication;


namespace DaemonsRelated
{
    public class HostSideDaemonContainer
    {
        public IInterprocessCommunicator Communicator { get; private set; }
        public ModuleStatus Status { get; private set; }
        public DaemonType Type { get; private set; }
        public string Name { get; private set; }

        private string _fullPath;
        private bool _isHidden;
        private ProcessPriorityClass _priority;

        private Process _process;

        public HostSideDaemonContainer(DaemonType type, string fullPath, bool isHidden, ProcessPriorityClass priority = ProcessPriorityClass.Normal)
        {
            Type = type;
            _fullPath = fullPath;
            _isHidden = isHidden;
            _priority = priority;
            Name = $"{type}Daemon";
            Status = ModuleStatus.Inactive;

            var processInfo = new ProcessStartInfo()
            {
                FileName = _fullPath,
                Arguments = $"{Process.GetCurrentProcess().Id} {Name} {_isHidden}",
                UseShellExecute = !_isHidden,   // runs as independent process (release == dependent (can't see console), debug == independed (can see console))
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = _isHidden
            };

            _process = new Process() { StartInfo = processInfo };
            Communicator = new InterprocessCommunicator_UnityServer(Name);

            Communicator.ConnectionEstablished += (message) => Status = ModuleStatus.FullyOperational;
            Communicator.ConnectionBroked += (message) => Status = ModuleStatus.NotOperational;
        }

        public async void Start()
        {
            try
            {
                // 'communicator' must be before 'process'
                // (because it's on the sever side. On the client, it's the opposite)

                Status = ModuleStatus.InSetup;

                var communicator = Communicator as InterprocessCommunicator_Server;
                _ = Task.Run(() => communicator.Start());

                await communicator.WaitForServerReadyForClientConnectionAsync();
                _ = Task.Run(() =>
                {
                    _process.Start();
                    if (_priority == ProcessPriorityClass.High)
                    {
                        _process.PriorityClass = ProcessPriorityClass.High; // RealTime is dangerous
                        _process.ProcessorAffinity = (IntPtr)1; // only first core will be used (system doesn't waste time switching between processes)
                        // todo: consider also banning all other processors from using this core
                    }
                });
            }
            catch
            {
                Stop();
            }
        }

        public void Stop()
        {
            Communicator.Stop();

            if (_isHidden)
            {
                _process.Kill();
                _process.Close();
            }

            // If not -- the console does not close itself and you can read the error message(s)

            Status = ModuleStatus.NotOperational;
        }
    }
}