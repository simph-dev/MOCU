using System;
using System.Collections.Generic;

using Simph.Core.Geometry;
using Simph.Core.Motion.Compensation;

namespace Simph.Core.Platform
{
    /// <summary>
    /// Where the platform goes for a stream of commands, as its
    /// <see cref="PlatformModel"/> says: the MBC's trajectory filter on each axis,
    /// the legs it asks for, each leg's drive, and back to a pose. This is the
    /// model run forwards; <see cref="CompensatedMotion"/> runs it backwards.
    /// What it predicts can be compared with what the platform reports, after a
    /// movement or when the model is fitted.
    ///
    /// <para>Against the platform's replies of 2026-11-25 (16 degrees, 13 cm,
    /// 1 s, compensated): 69 um rms per leg, 0.31 mm at worst, and 10, 50 and 90%
    /// of the path passed within 0.8 ms of when the platform passed them. What
    /// it leaves out: the first ~12 ms in which a leg does not move at all, and
    /// friction, which holds a leg up to ~60 um from where it was sent.</para>
    /// </summary>
    public sealed class PlatformResponse
    {
        private readonly PlatformModel _model;

        public PlatformResponse(PlatformModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>
        /// One predicted pose per command, for commands one frame apart. The
        /// platform starts at rest where the first command is.
        /// </summary>
        public Pose6[] Predict(IReadOnlyList<Pose6> sent)
        {
            if (sent == null) throw new ArgumentNullException(nameof(sent));

            int count = sent.Count;
            var platform = new Pose6[count];
            if (count == 0) return platform;

            var filtered = new Pose6[count];
            foreach (var axis in Axes.All)
            {
                var input = new double[count];
                for (int n = 0; n < count; n++) input[n] = sent[n][axis];

                var output = new TrajectoryFilterModel(_model.FilterAcceleration[axis], _model.Frame).Run(input);
                for (int n = 0; n < count; n++) filtered[n] = filtered[n].With(axis, output[n]);
            }

            // Each frame a leg's lagged part moves a fraction alpha of the way to
            // what it is asked for, and the direct share arrives whole. This is
            // the discrete form CompensatedMotion inverts.
            var geometry = _model.LegGeometry;
            double alpha = _model.DriveLag > TimeSpan.Zero
                ? 1.0 - Math.Exp(-_model.Frame.TotalSeconds / _model.DriveLag.TotalSeconds)
                : 1.0;
            double share = _model.DriveDirectShare;

            var lagged = geometry.LegLengths(filtered[0]);
            var legs = new double[6];
            var wanted = new double[6];
            var pose = filtered[0];

            for (int n = 0; n < count; n++)
            {
                geometry.LegLengths(filtered[n], wanted);

                for (int i = 0; i < 6; i++)
                {
                    lagged[i] += alpha * (wanted[i] - lagged[i]);
                    legs[i] = share * wanted[i] + (1 - share) * lagged[i];
                }

                pose = geometry.PoseFromLegLengths(legs, pose);
                platform[n] = pose;
            }

            return platform;
        }
    }
}
