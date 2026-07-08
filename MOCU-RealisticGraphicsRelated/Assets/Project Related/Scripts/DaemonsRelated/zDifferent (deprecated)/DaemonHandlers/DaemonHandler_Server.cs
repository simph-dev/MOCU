using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using DaemonsNamespace.Common;

/// <summary>
/// DaemonHandler_Server is responsible for handling interprocess communication with a server daemon.
/// 
/// Responsibilities:
/// - Manages communication via InterprocessCommunicator_Server, sending and receiving messages.
/// - Processes incoming messages and executes commands in a separate task to avoid blocking other threads.
/// - Monitors the parent process and terminates the application if the parent process dies.
/// 
/// Threading Details:
/// - The communicator runs its own threads for reading and writing.
/// - DaemonHandler_Server manages additional tasks:
///     - _commandExecutionTask: Processes incoming command tasks from _commandQueue.
///     - _responseSendingTask: Processes outgoing messages from _outputMessageQueue.
///     Although the communicator also handles sending messages using the same technique, this task is included for code clarity and future flexibility.
///     - _parentProcessMonitoringTask: Monitors the parent process and triggers application shutdown if it exits.
/// 
/// Initialization:
/// - Requires a set of arguments for initialization, including the parent process ID and named pipe name.
/// - Accepts an IBusinessLogic_Server implementation to handle specific business logic for incoming commands.
/// 
/// Usage:
/// 1. Create an instance of DaemonHandler_Server with the required arguments and IBusinessLogic_Server implementation.
/// 2. Call StartDaemon() to begin communication and monitoring.
/// 3. Call StopDaemon() to gracefully stop all tasks and communication.
/// 
/// Documentation chart: "Documentation\Daemon Handler Server Process.pdf"
/// </summary>


namespace InterprocessCommunication
{
    public class DaemonHandler_Server
    {
        private InterprocessCommunicator_Server _communicator;
        private IBusinessLogic_Server _businessLogic;

        private BlockingCollection<Action> _commandQueue;
        private BlockingCollection<string> _outputMessageQueue;
        private Task _commandExecutionTask;
        private Task _responseSendingTask;
        private Task _parentProcessMonitoringTask;
        private CancellationTokenSource _cancellationTokenSource;

        private int _checkIntervalMs;
        private string _pipeName;
        private int _parentProcessId;
        private bool _isProcessHidden;

        public DaemonHandler_Server(string[] argsFromCaller, IBusinessLogic_Server businessLogic)
        {
            ParseAndValidateArguments(argsFromCaller);

            _communicator = new InterprocessCommunicator_Server(_pipeName);
            _communicator.MessageReceived += HandleInputMessage;

            _businessLogic = businessLogic;
            _businessLogic.SendResponse += HandleOutputMessage;

            _commandQueue = new BlockingCollection<Action>();
            _outputMessageQueue = new BlockingCollection<string>();

            _cancellationTokenSource = new CancellationTokenSource();
            _checkIntervalMs = 500;
        }


        
        public async Task StartDaemon()
        {
            /*await _communicator.Start();*/

            _commandExecutionTask = Task.Run(() => ExecuteCommands(_cancellationTokenSource.Token));
            _responseSendingTask = Task.Run(() => SendResponses(_cancellationTokenSource.Token));
            _parentProcessMonitoringTask = Task.Run(() => MonitorParentProcessAsync(_cancellationTokenSource.Token));

            Task.WaitAll(_commandExecutionTask, _responseSendingTask);
        }

        public void StopDaemon()
        {
            _cancellationTokenSource.Cancel();

            try { _communicator.Stop(); }                catch { }
            try { _commandQueue.CompleteAdding(); }         catch { }
            try { _outputMessageQueue.CompleteAdding(); }   catch { }
            try { _commandExecutionTask.Wait(); }           catch { }
            try { _responseSendingTask.Wait(); }            catch { }
            try { _parentProcessMonitoringTask.Wait(); }    catch { }
        }


        private void ParseAndValidateArguments(string[] arguments)
        {
            if (arguments == null || arguments.Length != 3)
                CloseProgram("Program / Main :: Insufficient arguments provided");

            if (!bool.TryParse(arguments[2], out bool isProcessHidden))
                CloseProgram("Program / Main :: 'isProcessHidden' argument is not valid");

            if (!int.TryParse(arguments[0], out int parentProcessId))
                CloseProgram("Program / Main :: '_parentProcessId' argument is not valid");

            if (string.IsNullOrEmpty(arguments[1]))
                CloseProgram("Program / Main :: 'namedPipeName' argument is not valid");

            _pipeName = arguments[1];
            _parentProcessId = parentProcessId;
            _isProcessHidden = isProcessHidden;

            DaemonsUtilities.ConsoleInfo($"Program name: {_pipeName}");
            DaemonsUtilities.ConsoleInfo($"Parent process id: {parentProcessId}\n");
        }

        private void HandleInputMessage(string message)
        {
            try
            {
                var command = UnityDaemonsCommon.CommonUtilities.DeserializeJson<UnifiedCommandFrom_Client>(message);
                _commandQueue.Add(() => _businessLogic.ProcessCommand(command));
            }
            catch (Exception ex)
            {
                DaemonsUtilities.ConsoleError($"Error deserializing command: {ex.Message}");
            }
        }

        private void HandleOutputMessage(UnifiedResponseFrom_Server response)
        {
            try
            {
                var message = UnityDaemonsCommon.CommonUtilities.SerializeJson(response);
                _outputMessageQueue.Add(message);
            }
            catch (Exception ex)
            {
                DaemonsUtilities.ConsoleError($"Error serializing response: {ex.Message}");
            }
        }

        private void ExecuteCommands(CancellationToken token)
        {
            foreach (var task in _commandQueue.GetConsumingEnumerable(token))
                task();
        }

        private void SendResponses(CancellationToken token)
        {
            foreach (var message in _outputMessageQueue.GetConsumingEnumerable(token))
                _communicator.SendMessage(message);
        }

        private async Task MonitorParentProcessAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !Process.GetProcessById(_parentProcessId).HasExited)
                await Task.Delay(_checkIntervalMs, token);

            if (!token.IsCancellationRequested)
                CloseProgram("Program / Main :: Parent process died");
        }

        private void CloseProgram(string reason)
        {
            DaemonsUtilities.ConsoleError(reason);
            StopDaemon();

            if (_isProcessHidden)
                Console.ReadKey();

            Environment.Exit(1);
        }
    }
}