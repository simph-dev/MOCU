using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using MoogModule;
using UnityEngine;
using UnityEngine.XR;


namespace MirrorExperiment
{
    /// A diagnostic, off unless Parameters.LogStimulusTiming is set: what the eyes
    /// were given and where the platform really was, frame by frame and packet by
    /// packet, on one clock, for every stimulus. It is what settles whether the
    /// picture comes late, without relying on how it felt.
    ///
    /// Each trial leaves three files in the folder:
    ///
    ///     trial_0012_marks.csv     named moments: start, moog, visual
    ///     trial_0012_frames.csv    one row per rendered frame: where rig and eye were
    ///     trial_0012_moog.csv      every command sent and every pose reported
    ///
    /// All times are UTC ticks of 100 ns from GetSystemTimePreciseAsFileTime, which
    /// is what the daemon's DateTime.UtcNow reads. DateTime.UtcNow here in Mono would
    /// be the coarse clock.
    ///
    /// Frames are stamped in Application.onBeforeRender, after the pose driver's own
    /// update there (its order is -30000), so the transforms are what this frame is
    /// drawn from, and the frame is still ahead of the display by the rendering and
    /// the compositor. They are read off the transforms and not taken from
    /// _cameraOffset, which reaches the rig a frame after it is computed.
    ///
    /// The rig is what the experiment asked for. The eye adds what the headset
    /// tracked, and the headset rides on the platform, so the difference between
    /// them shows whether the platform's own movement gets into the camera.
    public class StimulusTimingLog : IDisposable
    {
        [DllImport("kernel32.dll")]
        private static extern void GetSystemTimePreciseAsFileTime(out long fileTime);

        /// FILETIME counts from 1601, DateTime from year 1.
        private const long FileTimeToDateTimeTicks = 504911232000000000L;

        /// How long to record the platform for. It must outlast the slowest arrival,
        /// which is over a second after the command.
        private static readonly TimeSpan MoogWindow = TimeSpan.FromSeconds(2.5);

        private readonly string _folder;
        private readonly MoogHandler _moog;
        private readonly Transform _rig;
        private readonly Transform _eye;
        private readonly StringBuilder _frames = new();
        private readonly StringBuilder _marks = new();
        private readonly Queue<int> _awaitingFeedback = new();
        private readonly List<XRDisplaySubsystem> _displays = new();

        private int _trial = -1;

        public StimulusTimingLog(string folder, MoogHandler moog, Transform rig, Transform eye)
        {
            _folder = folder;
            _moog = moog;
            _rig = rig;
            _eye = eye;

            Directory.CreateDirectory(_folder);

            _moog.GotFeedback += OnFeedback;
            Application.onBeforeRender += OnBeforeRender;

            Debug.Log($"MirrorExperiment: logging stimulus timing to {_folder}");
        }

        public void BeginTrial(int trial, bool withMoog)
        {
            _trial = trial;

            _frames.Clear();
            _marks.Clear();
            _frames.AppendLine("utc_ticks,frame,rig_surge,rig_sway,eye_surge,eye_sway,eye_heave,delta_s,motion_to_photon_s");
            _marks.AppendLine("name,utc_ticks");

            Mark("start");

            if (withMoog)
            {
                lock (_awaitingFeedback)
                    _awaitingFeedback.Enqueue(trial);

                _moog.RecordFeedback(MoogWindow);
            }
        }

        public void Mark(string name)
        {
            _marks.Append(name).Append(',').Append(Now()).AppendLine();
        }

        public void EndTrial()
        {
            if (_trial < 0)
                return;

            File.WriteAllText(FileFor(_trial, "frames"), _frames.ToString());
            File.WriteAllText(FileFor(_trial, "marks"), _marks.ToString());

            _trial = -1;
        }

        public void Dispose()
        {
            _moog.GotFeedback -= OnFeedback;
            Application.onBeforeRender -= OnBeforeRender;
        }

        // ...........................................

        private void OnBeforeRender()
        {
            if (_trial < 0)
                return;

            Vector3 rig = _rig.position;
            Vector3 eye = _eye.position;

            _frames.Append(Now()).Append(',')
                .Append(Time.frameCount).Append(',')
                .Append(Number(rig.z)).Append(',')
                .Append(Number(rig.x)).Append(',')
                .Append(Number(eye.z)).Append(',')
                .Append(Number(eye.x)).Append(',')
                .Append(Number(eye.y)).Append(',')
                .Append(Number(Time.unscaledDeltaTime)).Append(',')
                .Append(Number(MotionToPhoton())).AppendLine();
        }

        /// Not every runtime reports it, so -1 means it was not available.
        private float MotionToPhoton()
        {
            SubsystemManager.GetSubsystems(_displays);

            return _displays.Count > 0 && _displays[0].TryGetMotionToPhoton(out float seconds) ? seconds : -1f;
        }

        private void OnFeedback(MoogFeedback feedback)
        {
            int trial;

            lock (_awaitingFeedback)
            {
                if (_awaitingFeedback.Count == 0)
                    return;

                trial = _awaitingFeedback.Dequeue();
            }

            var rows = new StringBuilder("kind,utc_ticks,surge,sway,heave\n");

            foreach (var (timestamp, position) in feedback.Commands)
                AppendPose(rows, "command", timestamp, position);

            foreach (var (timestamp, state) in feedback.Responses)
                AppendPose(rows, "response", timestamp, state.Position);

            File.WriteAllText(FileFor(trial, "moog"), rows.ToString());
        }

        private static void AppendPose(StringBuilder rows, string kind, DateTime timestamp, DofParameters pose)
        {
            rows.Append(kind).Append(',')
                .Append(timestamp.Ticks).Append(',')
                .Append(Number(pose.Surge)).Append(',')
                .Append(Number(pose.Sway)).Append(',')
                .Append(Number(pose.Heave)).Append('\n');
        }

        private string FileFor(int trial, string part) =>
            Path.Combine(_folder, $"trial_{trial:D4}_{part}.csv");

        private static long Now()
        {
            GetSystemTimePreciseAsFileTime(out long fileTime);
            return fileTime + FileTimeToDateTimeTicks;
        }

        private static string Number(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }
}
