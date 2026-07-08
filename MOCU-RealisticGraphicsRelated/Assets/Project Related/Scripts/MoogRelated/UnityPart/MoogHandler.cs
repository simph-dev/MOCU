using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using MoogModule.Daemon;
using System.Collections;
using UnityEngine;
using ChartsModule;

// DateTime in Moog can be a problem. Better use DateTimeOffset?


namespace MoogModule
{
    public class MoogHandler : ManagedMonoBehaviour, MoogHandler_API
    {
        public event Action<MoogFeedback> GotFeedback;

        public ModuleStatusHandler<Moog_ModuleSubStatuses> stateTracker { get; private set; }

        private MoogHostSideBridge _daemon;
        private MachineSettings _connectParameters;

        // unity components
        private DaemonsHandler _daemonsHandler;
        //private TrajectoryMakerForMoogTest _controller;
        private DebugTabHandler _debugTabHandler;
        private ChartsHandler _chartsHandler;

        // ########################################################################################

        public override void ManagedAwake()
        {
            _daemonsHandler = GetComponent<DaemonsHandler>();
            //_controller = GetComponent<TrajectoryMakerForMoogTest>();
            _debugTabHandler = GetComponent<DebugTabHandler>();
            _chartsHandler = GetComponent<ChartsHandler>();

            // todo: move to config
            _connectParameters = new MachineSettings
            {
                StartPosition = new DofParameters { Heave = -0.22f },
                MaxAcceleration = 2.0,
                MinimalFPS = 500,
                DesiredFPS = 1000,
                HOST_IP = "192.168.2.3",
                HOST_PORT = "16386",
                MBC_IP = "192.168.2.1",
                MBC_PORT = "16384"
            };

            //_debugTabHandler.testBtn1Clicked += (eventObj) => Engage();     // test
            //_debugTabHandler.testBtn2Clicked += (eventObj) => TestMethod(); // test
        }

        public override void ManagedStart()
        {
            var daemonWrapper = _daemonsHandler.GetDaemon(DaemonType.Moog);
            var communicator = daemonWrapper.Communicator;

            _daemon = new MoogHostSideBridge(communicator);
            _daemon.Connect(_connectParameters);    // will be sent only after 'ConnectionEstablished' (automatically, it's in the queue)
            //_daemon.Test();

            //communicator.ConnectionEstablished += message => stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Communicator, SubStatusState.Complete);
            //communicator.ConnectionBroked += message => stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Communicator, SubStatusState.Failed);
            _daemon.Feedback += OnReceivedFeedback;
            _daemon.MachineStateChanged += OnMachineStateChanged;
            //_controller.OnPositionChanged += (coordinate) => MoveToPoint(new MoveToPointParameters { Coordinate = coordinate });    //test

            // here communicator and process start
            daemonWrapper.Start();                    // <----------
        }

        // ########################################################################################

        public void Connect(MachineSettings parameters)
        {
        }

        public void Engage()
        {
            _daemon.Engage();
        }

        public void Disengage()
        {
            _daemon.Disengage();
        }

        public void Reset()
        {
        }

        public void MoveToPoint(MoveToPointParameters parameters)
        {
            _daemon.MoveToPoint(parameters);
            /*if (stateTracker.Status == ModuleStatus.FullyOperational)
                _daemon.MoveToPoint(parameters);*/
        }

        public void MoveByTrajectory(MoveByTrajectoryParameters parameters)
        {
            _daemon.MoveByTrajectory(parameters);
        }

        public void RecordFeedback(TimeSpan timeSpan)
        {
            StartCoroutine(FeedbackCoroutine(timeSpan));
        }

        // ########################################################################################

        private void OnMachineStateChanged(MoogRealTimeState state)
        {
            /*if (state == MachineState.Ready || state == MachineState.Engaged)
                stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Machine, SubStatusState.Complete);
            else if (state == MachineState.Initializing || state == MachineState.Standby || state == MachineState.Parking)
                stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Machine, SubStatusState.StillNotSet);
            else
                stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Machine, SubStatusState.Failed);*/
        }

        // todo: rename
        private IEnumerator FeedbackCoroutine(TimeSpan timeSpan)
        {
            _daemon.StartReceivingFeedback();
            yield return new WaitForSeconds((float)timeSpan.TotalSeconds);
            _daemon.StopReceivingFeedback();
        }

        private void OnReceivedFeedback(MoogFeedback feedback)
        {
            GotFeedback?.Invoke(feedback);
            //_chartsHandler.InteractiveChart(feedback);
        }

        private void TestMethod()
        {
            var centerPoint = new DofParameters { Heave = -0.22f };
            var destinationPoint_1 = new DofParameters { Heave = -0.22f, Surge = 0.01f };
            var destinationPoint_2 = new DofParameters { Heave = -0.22f, Surge = 0.13f };
            var trajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } };
            var trajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } };
            var trajectoryType = TrajectoryType.Linear;
            var trajectoryProfile = TrajectoryProfile.CDF;
            var delayHandling = DelayCompensationStrategy.Ignore;


            var firstTrajectorySettings = new MoveByTrajectoryParameters
            {
                StartPoint = centerPoint,
                EndPoint = destinationPoint_1,
                MovementDuration = TimeSpan.FromSeconds(1),
                TrajectoryTypeSettings = trajectoryTypeSettings,
                TrajectoryProfileSettings = trajectoryProfileSettings,
                DelayHandling = delayHandling,
                TrajectoryType = trajectoryType,
                TrajectoryProfile = trajectoryProfile
            };

            var secondTrajectorySettings = new MoveByTrajectoryParameters
            {
                StartPoint = destinationPoint_1,
                EndPoint = centerPoint,
                MovementDuration = TimeSpan.FromSeconds(2.5),
                TrajectoryTypeSettings = trajectoryTypeSettings,
                TrajectoryProfileSettings = trajectoryProfileSettings,
                DelayHandling = delayHandling,
                TrajectoryType = trajectoryType,
                TrajectoryProfile = trajectoryProfile
            };

            var thirdTrajectorySettings = new MoveByTrajectoryParameters
            {
                StartPoint = centerPoint,
                EndPoint = destinationPoint_2,
                MovementDuration = TimeSpan.FromSeconds(1),
                TrajectoryTypeSettings = trajectoryTypeSettings,
                TrajectoryProfileSettings = trajectoryProfileSettings,
                DelayHandling = delayHandling,
                TrajectoryType = trajectoryType,
                TrajectoryProfile = trajectoryProfile
            };

            var fourthTrajectorySettings = new MoveByTrajectoryParameters
            {
                StartPoint = destinationPoint_2,
                EndPoint = centerPoint,
                MovementDuration = TimeSpan.FromSeconds(2.5),
                TrajectoryTypeSettings = trajectoryTypeSettings,
                TrajectoryProfileSettings = trajectoryProfileSettings,
                DelayHandling = delayHandling,
                TrajectoryType = trajectoryType,
                TrajectoryProfile = trajectoryProfile
            };


            IEnumerator TestCoroutine()
            {
                // forward 1 (short)
                _daemon.StartReceivingFeedback();
                _daemon.MoveByTrajectory(firstTrajectorySettings);
                yield return new WaitForSeconds(1f + 0.5f);
                _daemon.StopReceivingFeedback();
                yield return new WaitForSeconds(1f);    // на всякий случай

                // backward 1
                _daemon.MoveByTrajectory(secondTrajectorySettings);
                yield return new WaitForSeconds(2.5f + 2f);

                /*// forward 2 (long)
                _daemon.StartReceivingFeedback();
                _daemon.MoveByTrajectory(thirdTrajectorySettings);
                yield return new WaitForSeconds(1f + 0.5f);
                _daemon.StopReceivingFeedback();
                yield return new WaitForSeconds(1f);    // на всякий случай

                // backward 2
                _daemon.MoveByTrajectory(fourthTrajectorySettings);
                yield return new WaitForSeconds(2.5f + 2f);*/

                // stop
                _daemon.Disengage();
            }


            StartCoroutine(TestCoroutine());
        }
    }
}