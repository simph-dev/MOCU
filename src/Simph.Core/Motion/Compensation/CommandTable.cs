using System;

using Simph.Core.Geometry;

namespace Simph.Core.Motion.Compensation
{
    /// <summary>
    /// A compensated command as it goes out: one pose per frame, frame k sent at
    /// the plan's T0 plus k frames. It is worked out once, before anything is
    /// sent, so what the validator checks is exactly what is streamed, and the
    /// send loop only reads an array.
    /// </summary>
    public sealed class CommandTable
    {
        private readonly Pose6[] _frames;

        public CommandTable(CompensatedMotion motion, Pose6[] frames)
        {
            Motion = motion ?? throw new ArgumentNullException(nameof(motion));
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));

            if (frames.Length == 0)
                throw new ArgumentException("A command needs at least one frame.", nameof(frames));

            var lowest = frames[0];
            var highest = frames[0];

            foreach (var frame in frames)
            {
                foreach (var axis in Axes.All)
                {
                    lowest = lowest.With(axis, Math.Min(lowest[axis], frame[axis]));
                    highest = highest.With(axis, Math.Max(highest[axis], frame[axis]));
                }
            }

            Lowest = lowest;
            Highest = highest;
        }

        /// <summary>The movement the table was worked out from.</summary>
        public CompensatedMotion Motion { get; }

        public TimeSpan Frame => Motion.Frame;

        public int Count => _frames.Length;

        /// <summary>How long the table takes to send.</summary>
        public TimeSpan Duration => TimeSpan.FromTicks(Frame.Ticks * Count);

        /// <summary>
        /// Frame k. Before the first frame the platform is at the start point, and
        /// after the last it stays at the last frame, so both are answered too.
        /// </summary>
        public Pose6 this[int frame] =>
            frame < 0 ? Motion.Plan.From
            : frame >= _frames.Length ? _frames[_frames.Length - 1]
            : _frames[frame];

        /// <summary>The lowest value sent on each axis.</summary>
        public Pose6 Lowest { get; }

        /// <summary>The highest value sent on each axis.</summary>
        public Pose6 Highest { get; }
    }
}
