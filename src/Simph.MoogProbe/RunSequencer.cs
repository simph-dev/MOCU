using System;
using System.Collections.Generic;
using System.Linq;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Time;

using Simph.MoogProbe.Protocol;

namespace Simph.MoogProbe
{
    /// <summary>
    /// One stretch of a run: how long it lasts and where the platform should be
    /// at each moment inside it.
    /// </summary>
    public sealed class Segment
    {
        public Segment(string name, TimeSpan duration, Func<TimeSpan, Pose6> pose, MotionPlan? plan = null)
        {
            Name = name;
            Duration = duration;
            Pose = pose;
            Plan = plan;
        }

        public string Name { get; }
        public TimeSpan Duration { get; }
        public Func<TimeSpan, Pose6> Pose { get; }

        /// <summary>What the platform should do here, when this stretch is a movement.</summary>
        public MotionPlan? Plan { get; }
    }


    /// <summary>What to send in one slot of the loop.</summary>
    public readonly struct SequencerStep
    {
        public SequencerStep(MotionCommand command, Pose6 pose, bool finished)
        {
            Command = command;
            Pose = pose;
            Finished = finished;
        }

        public MotionCommand Command { get; }
        public Pose6 Pose { get; }

        /// <summary>The run is over and this slot should not be sent.</summary>
        public bool Finished { get; }
    }


    /// <summary>
    /// Decides, slot by slot, what the probe sends. Everything here that can move
    /// the platform is a pure function of the machine state and the clock, so it
    /// is tested rather than trusted.
    ///
    /// The stream never pauses: the MBC disengages by itself after
    /// HostCommTimeoutDelay packets of silence, so every stage keeps sending, and
    /// a stop request walks the machine down through Disengage rather than
    /// ending the loop, which would leave the platform to time out on its own.
    /// </summary>
    public sealed class RunSequencer
    {
        public enum Stage
        {
            /// <summary>Streaming the neutral pose, waiting to see READY.</summary>
            WaitingForReady,

            /// <summary>Asking to engage, and waiting to see ENGAGED.</summary>
            Engaging,

            /// <summary>Working through the schedule.</summary>
            Running,

            /// <summary>Asking to disengage, and waiting to see READY again.</summary>
            Disengaging,

            Done
        }

        private readonly bool _engages;
        private readonly Pose6 _neutral;
        private readonly IReadOnlyList<Segment> _schedule;
        private readonly TimeSpan _total;
        private readonly TimeSpan _engageLimit;
        private readonly TimeSpan _parkLimit;
        private readonly Action<string> _report;

        private Stage _stage;
        private bool _started;
        private Timestamp _stageStarted;
        private Timestamp _scheduleStarted;
        private Pose6 _lastPose;
        private string _lastReported = string.Empty;

        /// <param name="engages">False for a listen run, which must never leave Null.</param>
        /// <param name="neutral">The pose to hold before and after the schedule.</param>
        /// <param name="schedule">The stretches to run once engaged.</param>
        /// <param name="engageLimit">How long to wait for READY, and then for ENGAGED.</param>
        /// <param name="parkLimit">
        /// How long to wait for READY after asking to disengage. The MBC gives
        /// itself 40 s (ParkingTimeoutValue in its log), so this must not be
        /// shorter or the stream would stop under a platform still parking.
        /// </param>
        /// <param name="report">Called on every change of stage or segment.</param>
        public RunSequencer(bool engages, Pose6 neutral, IReadOnlyList<Segment> schedule,
            TimeSpan engageLimit, TimeSpan parkLimit, Action<string> report)
        {
            _engages = engages;
            _neutral = neutral;
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _total = schedule.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);
            _engageLimit = engageLimit;
            _parkLimit = parkLimit;
            _report = report ?? (_ => { });

            _stage = engages ? Stage.WaitingForReady : Stage.Running;
            _lastPose = neutral;
        }

        public Stage Current => _stage;

        /// <summary>When the first segment of the schedule began.</summary>
        public Timestamp ScheduleStarted => _scheduleStarted;

        /// <summary>Time since the schedule began, for reporting.</summary>
        public TimeSpan ScheduleTime(Timestamp due) => _started ? due - _scheduleStarted : TimeSpan.Zero;

        public SequencerStep Next(MachineState? state, Timestamp due, bool stopRequested)
        {
            if (!_started)
            {
                _started = true;
                _stageStarted = due;
                _scheduleStarted = due;
            }

            var command = MotionCommand.Null;

            switch (_stage)
            {
                case Stage.WaitingForReady:
                    if (stopRequested)
                    {
                        // Nothing was engaged, so there is nothing to park.
                        Enter(Stage.Done, due, "stopped before engaging");
                    }
                    else if (state == MachineState.Ready)
                    {
                        Enter(Stage.Engaging, due, "Engaging");
                    }
                    else if (state != null && IsFault(state.Value))
                    {
                        Enter(Stage.Done, due, $"machine is in {state}; clear it from the MBC interface first");
                    }
                    else if (due - _stageStarted > _engageLimit)
                    {
                        Enter(Stage.Done, due, $"gave up waiting for READY (last state {Describe(state)})");
                    }
                    break;

                case Stage.Engaging:
                    if (stopRequested)
                    {
                        Enter(Stage.Disengaging, due, "stop requested while engaging");
                    }
                    else if (state == MachineState.Engaged)
                    {
                        Enter(Stage.Running, due, "Running");
                        _scheduleStarted = due;
                    }
                    else if (state == MachineState.Ready)
                    {
                        // Engage is valid only in READY and carries the start pose
                        // in its own packet, so it goes out with the neutral pose
                        // until the state moves on.
                        command = MotionCommand.Engage;
                    }
                    else if (state != null && IsFault(state.Value))
                    {
                        Enter(Stage.Disengaging, due, $"machine faulted while engaging: {state}");
                    }
                    else if (due - _stageStarted > _engageLimit)
                    {
                        Enter(Stage.Disengaging, due, $"gave up waiting for ENGAGED (last state {Describe(state)})");
                    }
                    break;

                case Stage.Running:
                    if (stopRequested)
                    {
                        Enter(_engages ? Stage.Disengaging : Stage.Done, due, "stop requested");
                        break;
                    }

                    if (_engages && state != null && IsFault(state.Value))
                    {
                        Enter(Stage.Disengaging, due, $"machine faulted while running: {state}");
                        break;
                    }

                    var elapsed = due - _scheduleStarted;

                    if (elapsed >= _total)
                    {
                        Enter(_engages ? Stage.Disengaging : Stage.Done, due, _engages ? "Disengaging" : "Done");
                        break;
                    }

                    var segment = Locate(elapsed, out var inSegment);
                    _lastPose = segment.Pose(inSegment);
                    Report(segment.Name);
                    break;
            }

            // Falls through from Running or Engaging on the slot that decided to
            // stop, so the first Disengage goes out at once.
            if (_stage == Stage.Disengaging)
            {
                if (state == MachineState.Engaged || state == MachineState.Frozen || state == MachineState.Standby)
                {
                    // Valid in STANDBY and ENGAGED. The pose is held rather than
                    // sent back to neutral: a stop mid-movement should not add a
                    // step of its own to whatever the MBC does next.
                    command = MotionCommand.Disengage;
                }
                else if (state == MachineState.Ready)
                {
                    Enter(Stage.Done, due, "Done");
                }
                else if (due - _stageStarted > _parkLimit)
                {
                    Enter(Stage.Done, due, $"gave up waiting to park (last state {Describe(state)})");
                }
            }

            if (_stage == Stage.Done)
                return new SequencerStep(MotionCommand.Null, _lastPose, finished: true);

            return new SequencerStep(command, _lastPose, finished: false);
        }

        private void Enter(Stage next, Timestamp at, string message)
        {
            _stage = next;
            _stageStarted = at;
            Report(message);
        }

        private void Report(string what)
        {
            if (what == _lastReported) return;

            _lastReported = what;
            _report(what);
        }

        private Segment Locate(TimeSpan elapsed, out TimeSpan inSegment)
        {
            var offset = elapsed;

            foreach (var segment in _schedule)
            {
                if (offset < segment.Duration)
                {
                    inSegment = offset;
                    return segment;
                }

                offset -= segment.Duration;
            }

            inSegment = _schedule[_schedule.Count - 1].Duration;
            return _schedule[_schedule.Count - 1];
        }

        private static bool IsFault(MachineState state) =>
            state == MachineState.Fault1 || state == MachineState.Fault2 ||
            state == MachineState.Fault2Hold;

        private static string Describe(MachineState? state) =>
            state?.ToString() ?? "no reply yet";
    }
}
