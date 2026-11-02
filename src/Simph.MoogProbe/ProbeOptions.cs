using System;
using System.Globalization;
using System.Net;

using Simph.Core.Geometry;

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

        // Addresses from the MBC configuration (motionbase.xml: Host IP Address
        // 192.168.2.3, Motion Base IP Address 192.168.2.1) and matching
        // MoogHandler.MachineSettings.
        public IPEndPoint Host { get; private set; } = new IPEndPoint(IPAddress.Parse("192.168.2.3"), 16386);
        public IPEndPoint Mbc { get; private set; } = new IPEndPoint(IPAddress.Parse("192.168.2.1"), 16384);

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
        /// The duration actually commanded. The experiments send the intended 1 s
        /// multiplied by MoogMovementDurationCorrectionFactor of 0.8, because the
        /// platform takes longer to travel than it is told to.
        /// </summary>
        public double CommandedDuration { get; private set; } = 0.8;

        public Pose6 Neutral { get; private set; } = Pose6.Translation(surge: 0, sway: 0, heave: -0.22);

        /// <summary>
        /// Send the movement with the trajectory filter inverted, so that the
        /// filter's output follows the plan and the path stays straight.
        /// </summary>
        public bool Precompensate { get; private set; }

        /// <summary>
        /// With <see cref="Precompensate"/>, also lead the drives by this time
        /// constant, so that the platform itself follows the plan. Measured at
        /// 96 ms. Zero leaves the drive lag in place.
        /// </summary>
        public double DriveLagMilliseconds { get; private set; }

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
            (DriveLagMilliseconds > 0 ? "-lag" + DriveLagMilliseconds.ToString("F0", CultureInfo.InvariantCulture) : "");

        public string Header =>
            $"phase {Phase}, sending {Rate} packets/s, declaring {StatusResponseWord.Describe(StatusWord)}, " +
            $"{CopiesPerCommand} cop{(CopiesPerCommand == 1 ? "y" : "ies")} per command" +
            (Precompensate ? $", filter compensated, drive lag {DriveLagMilliseconds:F0} ms compensated" : "") +
            Environment.NewLine +
            $"host {Host}, mbc {Mbc}";


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
                        case "--drive-lag": options.DriveLagMilliseconds = Number(value); break;
                        case "--dwell": options.DwellSeconds = Number(value); break;
                        case "--repeats": options.Repeats = int.Parse(value, CultureInfo.InvariantCulture); break;
                        case "--distance": options.Distance = Number(value); break;
                        case "--heading": options.HeadingDegrees = Number(value); break;
                        case "--sigmas": options.Sigmas = Number(value); break;
                        case "--duration": options.CommandedDuration = Number(value); break;
                        case "--heave": options.Neutral = Pose6.Translation(0, 0, Number(value)); break;
                        case "--host": options.Host = ParseEndPoint(value, 16386); break;
                        case "--mbc": options.Mbc = ParseEndPoint(value, 16384); break;
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

            if (options.Neutral.Heave < MinNeutralHeave || options.Neutral.Heave > 0)
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

            if (options.DriveLagMilliseconds < 0 || options.DriveLagMilliseconds > MaxDriveLagMilliseconds)
            {
                error = $"--drive-lag must be between 0 and {MaxDriveLagMilliseconds:F0} ms.";
                return false;
            }

            if (options.DriveLagMilliseconds > 0 && !options.Precompensate)
            {
                error = "--drive-lag only means something with --precompensate.";
                return false;
            }

            return true;
        }

        private static double Number(string value) =>
            double.Parse(value, CultureInfo.InvariantCulture);

        private static Phase ParsePhase(string value) => value.ToLowerInvariant() switch
        {
            "listen" => Phase.Listen,
            "hold" => Phase.Hold,
            "move" => Phase.Move,
            _ => throw new FormatException("expected listen, hold or move")
        };

        private static IPEndPoint ParseEndPoint(string value, int defaultPort)
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
  --precompensate            invert the MBC's trajectory filter, so that the
                             path stays straight
  --drive-lag MS             with --precompensate, also lead the drives by this
                             time constant (measured: 96), so that the platform
                             itself follows the plan
  --heave M                  neutral heave (default -0.22)
  --host IP[:PORT]           default 192.168.2.3:16386
  --mbc IP[:PORT]            default 192.168.2.1:16384
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
