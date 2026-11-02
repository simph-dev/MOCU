using System;
using System.Collections.Generic;

namespace Simph.Core.Motion.Compensation
{
    /// <summary>
    /// The MBC's DOF Trajectory filter, for one axis, as measured on the lab's
    /// platform. Each frame it moves towards its input at the speed from which it
    /// could still stop in time at its configured acceleration:
    ///
    ///   e = input[n-1] - output[n-1]
    ///   wanted = sign(e) * ( sqrt((A dt/2)^2 + 2 A |e|) - A dt/2 )
    ///   v[n] = wanted, limited to within A dt of v[n-1]
    ///   output[n] = output[n-1] + v[n] dt
    ///
    /// A is ACC_LIM of that axis and dt is one frame, exactly 1 ms. Run open-loop
    /// on recorded inputs this reproduces the recorded outputs to about 0.05 um
    /// over 40000 frames, irregular streams included.
    ///
    /// The consequence is a lag that grows with speed: the output trails a moving
    /// input by v^2 / (2 A) + |v| dt / 2. Each axis is filtered on its own, so on a
    /// diagonal the faster axis falls further behind, and that is the bend in the
    /// platform's path. Nothing stops the lag at low accelerations, so staying
    /// under the limit does not avoid it.
    ///
    /// The law was measured on surge and sway. The other axes have the same
    /// configuration entries and are assumed to behave alike.
    /// </summary>
    public sealed class TrajectoryFilterModel
    {
        private readonly double _dt;
        private readonly double _halfStep;

        public TrajectoryFilterModel(double acceleration, TimeSpan frame)
        {
            if (acceleration <= 0)
                throw new ArgumentOutOfRangeException(nameof(acceleration), acceleration, "Must be positive.");

            if (frame <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "Must be positive.");

            Acceleration = acceleration;
            Frame = frame;
            _dt = frame.TotalSeconds;
            _halfStep = acceleration * _dt / 2.0;
        }

        /// <summary>ACC_LIM of the axis, in its units per second squared.</summary>
        public double Acceleration { get; }

        public TimeSpan Frame { get; }

        /// <summary>
        /// How far the input must be ahead of the output for the filter to move at
        /// this velocity: the signed inverse of the law above.
        /// </summary>
        public double Lead(double velocity) =>
            Math.Sign(velocity) * (velocity * velocity / (2.0 * Acceleration) + Math.Abs(velocity) * _dt / 2.0);

        /// <summary>The velocity the law asks for at this distance from the input.</summary>
        public double WantedVelocity(double error) =>
            Math.Sign(error) * (Math.Sqrt(_halfStep * _halfStep + 2.0 * Acceleration * Math.Abs(error)) - _halfStep);

        /// <summary>
        /// Runs a whole input series, starting at rest on its first sample. The
        /// result has one output per input.
        /// </summary>
        public double[] Run(IReadOnlyList<double> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var output = new double[input.Count];

            if (input.Count == 0)
                return output;

            double position = input[0];
            double velocity = 0.0;
            double maxChange = Acceleration * _dt;

            output[0] = position;

            for (int n = 1; n < input.Count; n++)
            {
                double wanted = WantedVelocity(input[n - 1] - position);

                velocity = Math.Min(Math.Max(wanted, velocity - maxChange), velocity + maxChange);
                position += velocity * _dt;

                output[n] = position;
            }

            return output;
        }
    }
}
