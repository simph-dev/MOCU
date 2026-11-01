using System;


namespace Simph.Core.Geometry
{
    /// <summary>
    /// A position in the platform's six degrees of freedom - or, with the same
    /// shape, a velocity or an acceleration.
    ///
    /// Translations are metres, rotations are radians, matching MoogModule's
    /// DofParameters, which this replaces on the core side. Double rather than
    /// float: the limit arithmetic divides by squared durations, and the extra
    /// headroom costs nothing here. The narrowing to float happens once, in the
    /// adapter that builds the wire packet.
    ///
    /// Immutable, so a pose can be shared between the motion loop and the render
    /// thread without a copy and without a lock.
    /// </summary>
    public readonly struct Pose6 : IEquatable<Pose6>
    {
        public double Roll { get; }
        public double Pitch { get; }
        public double Yaw { get; }
        public double Surge { get; }
        public double Sway { get; }
        public double Heave { get; }

        public Pose6(double roll, double pitch, double yaw, double surge, double sway, double heave)
        {
            Roll = roll;
            Pitch = pitch;
            Yaw = yaw;
            Surge = surge;
            Sway = sway;
            Heave = heave;
        }

        public static Pose6 Zero => default;

        /// <summary>A pose with only the three translations set.</summary>
        public static Pose6 Translation(double surge, double sway, double heave) =>
            new Pose6(0, 0, 0, surge, sway, heave);


        public double this[Axis axis]
        {
            get
            {
                switch (axis)
                {
                    case Axis.Roll: return Roll;
                    case Axis.Pitch: return Pitch;
                    case Axis.Yaw: return Yaw;
                    case Axis.Surge: return Surge;
                    case Axis.Sway: return Sway;
                    case Axis.Heave: return Heave;
                    default: throw new ArgumentOutOfRangeException(nameof(axis));
                }
            }
        }

        public Pose6 With(Axis axis, double value)
        {
            switch (axis)
            {
                case Axis.Roll: return new Pose6(value, Pitch, Yaw, Surge, Sway, Heave);
                case Axis.Pitch: return new Pose6(Roll, value, Yaw, Surge, Sway, Heave);
                case Axis.Yaw: return new Pose6(Roll, Pitch, value, Surge, Sway, Heave);
                case Axis.Surge: return new Pose6(Roll, Pitch, Yaw, value, Sway, Heave);
                case Axis.Sway: return new Pose6(Roll, Pitch, Yaw, Surge, value, Heave);
                case Axis.Heave: return new Pose6(Roll, Pitch, Yaw, Surge, Sway, value);
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }


        public static Pose6 operator +(Pose6 a, Pose6 b) => new Pose6(
            a.Roll + b.Roll, a.Pitch + b.Pitch, a.Yaw + b.Yaw,
            a.Surge + b.Surge, a.Sway + b.Sway, a.Heave + b.Heave);

        public static Pose6 operator -(Pose6 a, Pose6 b) => new Pose6(
            a.Roll - b.Roll, a.Pitch - b.Pitch, a.Yaw - b.Yaw,
            a.Surge - b.Surge, a.Sway - b.Sway, a.Heave - b.Heave);

        public static Pose6 operator *(Pose6 a, double k) => new Pose6(
            a.Roll * k, a.Pitch * k, a.Yaw * k, a.Surge * k, a.Sway * k, a.Heave * k);

        public static Pose6 operator *(double k, Pose6 a) => a * k;

        public static Pose6 operator /(Pose6 a, double k) => a * (1.0 / k);


        /// <summary>Straight line between two poses; progress outside [0,1] extrapolates.</summary>
        public static Pose6 Lerp(Pose6 from, Pose6 to, double progress) =>
            from + (to - from) * progress;

        public bool HasNonFinite()
        {
            foreach (var axis in Axes.All)
            {
                double v = this[axis];
                if (double.IsNaN(v) || double.IsInfinity(v)) return true;
            }

            return false;
        }


        public bool Equals(Pose6 other) =>
            Roll == other.Roll && Pitch == other.Pitch && Yaw == other.Yaw &&
            Surge == other.Surge && Sway == other.Sway && Heave == other.Heave;

        public override bool Equals(object obj) => obj is Pose6 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var axis in Axes.All) hash = hash * 31 + this[axis].GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"(surge {Surge:F4} sway {Sway:F4} heave {Heave:F4} | " +
            $"roll {Roll:F4} pitch {Pitch:F4} yaw {Yaw:F4})";
    }
}
