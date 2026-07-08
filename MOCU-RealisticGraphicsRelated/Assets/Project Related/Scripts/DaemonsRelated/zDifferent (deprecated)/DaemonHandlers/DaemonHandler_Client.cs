using System.Collections.Concurrent;
using System.Threading;
using System;
using System.Threading.Tasks;

using System.Diagnostics;
using System.IO;


namespace InterprocessCommunication
{
    public class DaemonHandler_Client
    {
        // those 2 event are for 'DaemonsHandler' to log incoming/outgoing messages
        public event Action<string, string> MessageSended;      // <daemon name, command name>
        public event Action<string, string> MessageReceived;    // <daemon name, response name>

        private InterprocessCommunicator_Client _communicator;
        private IBusinessLogic_Client _businessLogic;

        private BlockingCollection<Action> _responseQueue;
        private BlockingCollection<string> _outputMessageQueue;
        private Task _commandSendingTask;
        private CancellationTokenSource _cancellationTokenSource;

        // todo: not sure where to place it
        private Process _daemonProcess;
        private string _daemonPath;
        private string _daemonName;
        private bool _isDaemonHidden;
        public bool isProcessOk;
        public bool isConnectionOk;

        public DaemonHandler_Client(string exeFullFilePath, bool isDaemonHidden, IBusinessLogic_Client businessLogic)
        {
            _daemonPath = exeFullFilePath;
            _daemonName = Path.GetFileNameWithoutExtension(_daemonPath);
            _isDaemonHidden = isDaemonHidden;

            _businessLogic = businessLogic;

            _responseQueue = new BlockingCollection<Action>();
            _outputMessageQueue = new BlockingCollection<string>();

            _cancellationTokenSource = new CancellationTokenSource();
        }



        public async Task StartDaemon()
        {
            StartDaemonProcess();

            _communicator = new InterprocessCommunicator_Client(_daemonName);
            _communicator.MessageReceived += HandleInputMessage;
            _communicator.Start();
            /*isConnectionOk = true;

            _commandSendingTask = Task.Run(() => SendCommands(_cancellationTokenSource.Token));*/
        }

        public void StopDaemon()
        {
            _cancellationTokenSource.Cancel();

            try { _communicator.Stop(); }                catch { }
            try { _responseQueue.CompleteAdding(); }        catch { }
            try { _outputMessageQueue.CompleteAdding(); }   catch { }
            try { _commandSendingTask.Wait(); }             catch { }
            try { _daemonProcess.Kill(); }                  catch { }
        }

        public void SendCommand(UnifiedCommandFrom_Client command)
        {
            HandleOutputMessage(command);
        }



        private void HandleInputMessage(string message)
        {
            try
            {
                var response = UnityDaemonsCommon.CommonUtilities.DeserializeJson<UnifiedResponseFrom_Server>(message);
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    _businessLogic.ProcessResponse(response);
                    MessageReceived?.Invoke(_daemonName, response.name);
                });
            }
            catch (Exception ex)
            {
                UnityUtilities.ConsoleError($"Error deserializing response: {ex.Message}");
            }
        }

        private void HandleOutputMessage(UnifiedCommandFrom_Client command)
        {
            try
            {
                var message = UnityDaemonsCommon.CommonUtilities.SerializeJson(command);
                _outputMessageQueue.Add(message);

                // 'ThreadDispatcher' is used so as not to interfere messages sending
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    MessageSended?.Invoke(_daemonName, command.name);
                });
            }
            catch (Exception ex)
            {
                UnityUtilities.ConsoleError($"Error serializing command: {ex.Message}");
            }
        }

        private void SendCommands(CancellationToken token)
        {
            foreach (var message in _outputMessageQueue.GetConsumingEnumerable(token))
                _communicator.SendMessage(message);
        }

        private void StartDaemonProcess()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = _daemonPath,
                    Arguments = $"{Process.GetCurrentProcess().Id} {_daemonName} {_isDaemonHidden}", // takes string where spaces separate arguments
                    UseShellExecute = !_isDaemonHidden,
                    RedirectStandardOutput = false,
                    CreateNoWindow = _isDaemonHidden
                };

                _daemonProcess = new Process() { StartInfo = startInfo };
                _daemonProcess.Start();

                isProcessOk = true;
            }
            catch (Exception ex)
            {
                isProcessOk = false;
                throw new Exception(ex.Message);
            }
        }
    }
}