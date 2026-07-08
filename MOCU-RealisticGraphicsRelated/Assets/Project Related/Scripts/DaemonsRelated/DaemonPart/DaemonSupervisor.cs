using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using InterprocessCommunication;
using DaemonsRelated.DaemonPart;
using System.IO;


namespace DaemonsRelated
{
    public class DaemonSupervisor
    {
        private readonly string[] _arguments;

        private int _parentProcessId;
        private string _daemonName;
        private bool _isInDebugMode;

        private readonly List<string> _terminationReasons;

        public DaemonSupervisor(string[] arguments)
        {
            _arguments = arguments;
            _terminationReasons = new List<string>();
        }

        public string DaemonName => _daemonName;

        public bool IsValid()
        {
            if (_arguments == null || _arguments.Length != 3)
            {
                _terminationReasons.Add("Insufficient _arguments provided");
                return false;
            }

            if (!bool.TryParse(_arguments[2], out var isHidden))
            {
                _terminationReasons.Add("Argument 'isProcessHidden' is not valid");
                return false;
            }

            if (!int.TryParse(_arguments[0], out _parentProcessId))
            {
                _terminationReasons.Add("Argument '_parentProcessId' is not valid");
                return false;
            }

            if (string.IsNullOrEmpty(_arguments[1]))
            {
                _terminationReasons.Add("Argument 'namedPipeName' is not valid");
                return false;
            }

            _daemonName = _arguments[1];
            _isInDebugMode = !isHidden;

            if (!_isInDebugMode)
                Console.SetOut(TextWriter.Null);    // disables console writing // todo: move to better place

            Console.WriteLine($"Is in debug mode: {_isInDebugMode}");

            return true;
        }

        public void SubscribeForParentProcessTermination()
        {
            var parentProcess = Process.GetProcessById(_parentProcessId);

            parentProcess.EnableRaisingEvents = true;
            parentProcess.Exited += (sender, e) =>
            {
                _terminationReasons.Add("Parent process terminated");
                CloseProgram();
            };
        }

        public void RunProgram(IInterprocessCommunicator communicator, IHostAPI hostAPI, IDaemonLogic daemonLogic)
        {
            communicator.ConnectionBroked += CloseProgram;
            hostAPI.TerminateDaemon += CloseProgram;
            daemonLogic.TerminateDaemon += CloseProgram;

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                Console.WriteLine("Application is exiting...");

                communicator.ConnectionBroked   -= CloseProgram;
                hostAPI.TerminateDaemon         -= CloseProgram;
                daemonLogic.TerminateDaemon     -= CloseProgram;

                daemonLogic?.DoBeforeExit();
                communicator?.Stop();
            };

            daemonLogic.Run();
            Thread.Sleep(Timeout.Infinite);
        }

        public void CloseProgram(string? reason = null)
        {
            if (reason != null)
                _terminationReasons.Add(reason);

            Console.WriteLine("Reasons for termination:");

            foreach (var terminationReason in _terminationReasons)
                Console.WriteLine($"\t{reason}");

            if (_isInDebugMode)
            {
                Console.WriteLine("Press any KEY to exit");
                Console.ReadKey();
            }

            Environment.Exit(1);
        }
    }
}