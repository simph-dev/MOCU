using System;
using System.Globalization;
using System.Net;

using Simph.Core.Geometry;
using Simph.Core.Motion.Limits;
using Simph.Core.Platform;

using Simph.MoogProbe.Protocol;

namespace Simph.MoogProbe
{
    public enum Phase
    {
        /// <summary>
        /// Streams commands and reads the replies without ever engaging. The MBC
        /// answers every command message whatever state it is in, and ignores the
        /// pose until ENGAGED, so this cannot move the platform.
        /// </summary>
        Listen,

        /// <summary>Engages, holds the neutral pose, disengages. Moves the platform.</summary>
        Hold,

        /// <summary>Engages and runs the stimulus movement out and back. Moves the platform.</summary>
        Move
    }


    public sealed class ProbeOptions
    {
        private const double MaxDitherMetres = 0.020;
        private const double MaxDitherAcceleration = 0.2;
        private const double MinNeutralHeave = -0.30;
        private const double MaxDriveLagMilliseconds = 150.0;

        public string SetupPath { get; private set; } = RigSetup.DefaultPath;

        // From the rig setup unless given on the command line; see UseSetup.
        public IPEndPoint Host { get; private set; } = null!;
        public IPEndPoint Mbc { get; private set; } = null!;
        public Pose6 Neutral { get; private set; }
        public PlatformModel Model { get; private set; } = null!;
        public MotionEnvelope Envelope { get; private set; } = null!;

        private IPEndPoint? _hostGiven, _mbcGiven;
        private double? _neutralHeaveGiven;
        private double? _driveLagGivenMilliseconds;
        private double? _driveLeadGivenMilliseconds;
        private string? _lagActsOnGiven;

        public Phase Phase { get; private set; } = Phase.Listen;

        /// <summary>Packets a second the send loop actually emits.</summary>
        public int Rate { get; private set; } = 1000;

        /// <summary>
        /// What we declare in the Status Response Word Update Rate field. Defaults
        /// to the real rate, which is the point: MOCU-MoogControl declares 2000
        /// while sending 1000. Pass --declare 2000 to reproduce that.
        /// </summary>
        public int DeclaredRate { get; private set; }

        /// <summary>
        /// Send every command twice with the same sequence count, as CDS7330
        /// Rev. F section 4.2.1 describes. The MBC logged one
        /// "Invalid Host Sequence Count (previous = N, current = N)" per packet
        /// the one time this was tried, so it is off by default and kept only as
        /// a measurement.
        /// </summary>
        public bool Duplicate { get; private set; }

        public double Seconds { get; private set; } = 10.0;
        public double SettleSeconds { get; private set; } = 2.0;

        /// <summary>
        /// Amplitude, in metres, of a slow sine added to surge while holding.
        /// Zero by default. Its point is to make each commanded set-point
        /// distinguishable from its neighbours in the recorder, so a repeated or
        /// skipped set-point can be counted individually instead of only showing
        /// up in the row total.
        ///
        /// At the default 2 mm and 0.5 Hz the peak acceleration is 0.02 m/s^2,
        /// one percent of the trajectory filter's limit, and the step between
        /// consecutive frames is about 6 um - four hundred times the resolution
        /// of a float at this distance, so it records cleanly.
        /// </summary>
        public double DitherMetres { get; private set; }

        public double DitherHz { get; private set; } = 0.5;
        public double DwellSeconds { get; private set; } = 1.0;
        public int Repeats { get; private set; } = 1;

        // The stimulus, from tests/Simph.Core.Tests/LabSetup.cs.
        public double Distance { get; private set; } = 0.13;
        public double HeadingDegrees { get; private set; } = 16.0;
        public double Sigmas { get; private set; } = 3.0;

        /// <summary>
        /// The duration actually commanded. The default 0.8 s is what the live
        /// experiments send (their intended 1 s times a correction factor of 0.8).
        /// With --precompensate use the intended 1.0 s: compensated, 0.8 s asks
        /// the filter for more than it can deliver and is refused.
        /// </summary>
        public double CommandedDuration { get; private set; } = 0.8;

        /// <summary>
        /// Send the movement with the platform's response compensated as the rig
        /// setup describes it: the trajectory filter inverted and the drives led,
        /// so that the platform itself follows the plan.
        /// </summary>
        public bool Precompensate { get; private set; }

        /// <summary>The drive lag the compensation leads by; zero compensates the filter only.</summary>
        public double DriveLagMilliseconds => Model.DriveLag.TotalMilliseconds;

        public double DriveLeadMilliseconds => Model.DriveLead.TotalMilliseconds;

        public string DriveLagActsOn => Model.LegGeometry != null ? "legs" : "axes";

        /// <summary>Required before anything that moves the platform.</summary>
        public bool Armed { get; private set; }

        /// <summary>Skips the typed confirmation. For unattended runs only.</summary>
        public bool AssumeYes { get; private set; }

        public string LogRoot { get; private set; } = "probe-logs";

        public TimeSpan Interval => TimeSpan.FromSeconds(1.0 / Rate);

        public uint StatusWord => StatusResponseWord.Build(DeclaredRate);

        public int CopiesPerCommand => Duplicate ? 2 : 1;

        public bool MovesThePlatform => Phase != Phase.Listen;

        public Pose6 Target
        {
            get
            {
                double heading = HeadingDegrees * Math.PI / 180.0;
                return Neutral + Pose6.Translation(
                    surge: Distance * Math.Cos(heading),
                    sway: Distance * Math.Sin(heading),
                    heave: 0);
            }
        }

        public string Label =>
            $"{Phase.ToString().ToLowerInvariant()}-{Rate}send-{DeclaredRate}declared" +
            (Phase == Phase.Move ? "-" + CommandedDuration.ToString("F2", CultureInfo.InvariantCulture) + "s" : "") +
            (Duplicate ? "-duplicated" : "") +
            (Precompensate ? "-precomp" : "") +
            (Precompensate && DriveLagMilliseconds > 0
                ? "-lag" + DriveLagMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + DriveLagActsOn
                : "") +
            (Precompensate && DriveLeadMilliseconds > 0
                ? "-lead" + DriveLeadMilliseconds.ToString("F1", CultureInfo.InvariantCulture)
                : "");

        public string Header =>
            $"phase {Phase}, sending {Rate} packets/s, declaring {StatusResponseWord.Describe(StatusWord)}, " +
            $"{CopiesPerCommand} cop{(CopiesPerCommand == 1 ? "y" : "ies")} per command" +
            (Precompensate ? $", filter compensated, drive lag {DriveLagMilliseconds:F1} ms with lead {DriveLeadMilliseconds:F1} ms compensated on the {DriveLagActsOn}" : "") +
            Environment.NewLine +
            $"host {Host}, mbc {Mbc}, setup {SetupPath}";

        /// <summary>
        /// Takes everything not given on the command line from the rig setup. Has
        /// to run before the schedule is built: the neutral pose comes from here.
        /// </summary>
        public void UseSetup(RigSetup setup)
        {
            Host = _hostGiven ?? setup.Host;
            Mbc = _mbcGiven ?? setup.Mbc;
            Neutral = _neutralHeaveGiven.HasValue ? Pose6.Translation(0, 0, _neutralHeaveGiven.Value) : setup.Neutral;
            Envelope = setup.Envelope;

            var model = setup.Model;
            var lag = _driveLagGivenMilliseconds.HasValue ? TimeSpan.FromMilliseconds(_driveLagGivenMilliseconds.Value) : model.DriveLag;

            // A lag given alone describes a pure lag: keeping the setup's lead
            // with someone else's lag would mix two models. "--drive-lag 0"
            // (filter only) needs the lead at zero as well.
            var lead = _driveLeadGivenMilliseconds.HasValue ? TimeSpan.FromMilliseconds(_driveLeadGivenMilliseconds.Value)
                : _driveLagGivenMilliseconds.HasValue ? TimeSpan.Zero
                : model.DriveLead;
            var geometry = _lagActsOnGiven switch
            {
                "axes" => null,
                "legs" => model.LegGeometry ?? HexapodGeometry.MoogMbE6Dof,
                _ => model.LegGeometry
            };

            Model = new PlatformModel(model.FilterAcceleration, lag, lead, geometry, model.Frame);
        }


        public static bool TryParse(string[] args, out ProbeOptions options, out string? error)
        {
            options = new ProbeOptions();
            error = null;

            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];

                switch (key)
                {
                    case "--arm": options.Armed = true; continue;
                    case "--yes": options.AssumeYes = true; continue;
                    case "--duplicate": options.Duplicate = true; continue;
                    case "--precompensate": options.Precompensate = true; continue;
                }

                if (i + 1 >= args.Length)
                {
                    error = $"{key} needs a value.";
                    return false;
                }

                string value = args[++i];

                try
                {
                    switch (key)
                    {
                        case "--phase": options.Phase = ParsePhase(value); break;
                        case "--rate": options.Rate = int.Parse(value, CultureInfo.InvariantCulture); break;
                        case "--declare": options.DeclaredRate = int.Parse(value, CultureInfo.InvariantCulture); break;
                        case "--seconds": options.Seconds = Number(value); break;
                        case "--settle": options.SettleSeconds = Number(value); break;
                        case "--dither": options.DitherMetres = Number(value) / 1000.0; break;
                        case "--dither-hz": options.DitherHz = Number(value); break;
                        case "--drive-lag": options._driveLagGivenMilliseconds = Number(value); break;
                        case "--lag-in": options._lagActsOnGiven = ParseLagActsOn(value); break;
                        case "--drive-lead": options._driveLeadGivenMilliseconds = Number(value); break;
                        case "--setup": options.SetupPath = value; break;
                        case "--dwell": options.DwellSeconds = Number(value); break;
                        case "--repeats": options.Repeats = int.Parse(value, CultureInfo.InvariantCulture); break;
                        case "--distance": options.Distance = Number(value); break;
                        case "--heading": options.HeadingDegrees = Number(value); break;
                        case "--sigmas": options.Sigmas = Number(value); break;
                        case "--duration": options.CommandedDuration = Number(value); break;
                        case "--heave": options._neutralHeaveGiven = Number(value); break;
                        case "--host": options._hostGiven = ParseEndPoint(value, 16386); break;
                        case "--mbc": options._mbcGiven = ParseEndPoint(value, 16384); break;
                        case "--logs": options.LogRoot = value; break;
                        default:
                            error = $"Unknown option {key}.";
                            return false;
                    }
                }
                catch (Exception exception)
                {
                    error = $"{key}: {exception.Message}";
                    return false;
                }
            }

            if (options.DeclaredRate == 0)
                options.DeclaredRate = options.Rate;

            if (options.Rate < 1 || options.Rate > 2000)
            {
                error = "--rate must be between 1 and 2000.";
                return false;
            }

            if (options.Repeats < 1)
            {
                error = "--repeats must be at least 1.";
                return false;
            }

            // The dither is added to an engaged pose and no MotionPlan describes
            // it, so the validator never sees it. It has its own bounds here.
            double omega = 2.0 * Math.PI * options.DitherHz;

            if (options.DitherMetres < 0 || options.DitherMetres > MaxDitherMetres ||
                options.DitherHz <= 0 ||
                options.DitherMetres * omega * omega > MaxDitherAcceleration)
            {
                error = $"--dither must stay within {MaxDitherMetres * 1000:F0} mm and " +
                    $"{MaxDitherAcceleration:F1} m/s^2 of peak acceleration.";
                return false;
            }

            if (options._neutralHeaveGiven is double heave && (heave < MinNeutralHeave || heave > 0))
            {
                error = $"--heave must be between {MinNeutralHeave:F2} and 0.";
                return false;
            }

            // The filter's law is per frame, and frames follow the stream only at
            // this rate. Anything else would be compensating a different filter.
            if (options.Precompensate && options.Rate != 1000)
            {
                error = "--precompensate needs --rate 1000.";
                return false;
            }

            if (options._driveLagGivenMilliseconds is double lag && (lag < 0 || lag > MaxDriveLagMilliseconds))
            {
                error = $"--drive-lag must be between 0 and {MaxDriveLagMilliseconds:F0} ms.";
                return false;
            }

            if (options._driveLeadGivenMilliseconds is double lead && (lead < 0 || lead > (options._driveLagGivenMilliseconds ?? MaxDriveLagMilliseconds)))
            {
                error = "--drive-lead must be between 0 and the drive lag.";
                return false;
            }

            if ((options._driveLagGivenMilliseconds.HasValue || options._driveLeadGivenMilliseconds.HasValue ||
                 options._lagActsOnGiven != null) && !options.Precompensate)
            {
                error = "--drive-lag, --drive-lead and --lag-in only mean something with --precompensate.";
                return false;
            }

            return true;
        }

        private static string ParseLagActsOn(string value) => value.ToLowerInvariant() switch
        {
            "legs" => "legs",
            "axes" => "axes",
            _ => throw new FormatException("expected legs or axes")
        };

        private static double Number(string value) =>
            double.Parse(value, CultureInfo.InvariantCulture);

        private static Phase ParsePhase(string value) => value.ToLowerInvariant() switch
        {
            "listen" => Phase.Listen,
            "hold" => Phase.Hold,
            "move" => Phase.Move,
            _ => throw new FormatException("expected listen, hold or move")
        };

        internal static IPEndPoint ParseEndPoint(string value, int defaultPort)
        {
            int colon = value.LastIndexOf(':');

            if (colon < 0)
                return new IPEndPoint(IPAddress.Parse(value), defaultPort);

            return new IPEndPoint(
                IPAddress.Parse(value.Substring(0, colon)),
                int.Parse(value.Substring(colon + 1), CultureInfo.InvariantCulture));
        }


        public static string Usage =>
@"Simph.MoogProbe - instruments the command stream to the Moog MB-E-6DOF.

  --phase listen|hold|move   listen (default) never engages and cannot move the
                             platform; hold and move both do, and need --arm
  --rate N                   packets a second to send (default 1000)
  --declare N                Update Rate to declare in the Status Response Word
                             (default: the same as --rate; 2000 reproduces what
                             MOCU-MoogControl has always sent)
  --duplicate                send each command twice with the same sequence
                             count, per CDS7330 Rev. F 4.2.1
  --seconds S                length of a listen or hold run (default 10)
  --settle S                 hold at neutral before moving, in move (default 2)
  --dither MM                amplitude in mm of a slow sine on surge while
                             holding, so each set-point is distinguishable in
                             the recorder (default 0, off)
  --dither-hz F              its frequency (default 0.5)
  --dwell S                  hold at each end of a movement (default 1)
  --repeats N                out-and-back cycles (default 1)
  --distance M               metres travelled (default 0.13)
  --heading DEG              heading off straight ahead (default 16)
  --duration S               commanded duration (default 0.8)
  --sigmas N                 Gaussian CDF width (default 3)
  --precompensate            compensate the platform's response as the rig setup
                             describes it (filter inverted, drives led), so that
                             the platform itself follows the plan
  --drive-lag MS             with --precompensate, override the setup's drive
                             lag; 0 compensates the filter only
  --drive-lead MS            with --precompensate, how much earlier than the lag
                             the drives answer (setup: 0; 0 also when only
                             --drive-lag is given)
  --lag-in legs|axes         with --precompensate, override where the drive lag
                             acts (setup: legs)
  --setup FILE               rig setup (default rig-setup.json next to the exe,
                             copied from config/rig-setup.json)
  --heave M                  neutral heave (default from the setup, -0.22)
  --host IP[:PORT]           default from the setup, 192.168.2.3:16386
  --mbc IP[:PORT]            default from the setup, 192.168.2.1:16384
  --logs DIR                 where to write the run folder (default probe-logs)
  --arm                      permit a phase that moves the platform
  --yes                      skip the typed confirmation

Simph.MoogProbe timing FOLDER reads a session folder written by the experiment's
StimulusTimingLog and lays the picture and the platform on one clock.

Start with the phase that cannot move anything:

  Simph.MoogProbe --phase listen --rate 1000 --declare 1000 --seconds 20
  Simph.MoogProbe --phase listen --rate 1000 --declare 2000 --seconds 20
  Simph.MoogProbe --phase listen --rate 1000 --declare 2000 --duplicate --seconds 20
";
    }
}
