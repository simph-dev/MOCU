/**
 * DOF (Degrees of Freedom) Parameters.
 *
 * Used to represent displacement, velocity, or acceleration vectors
 * in the 6DOF space (Roll, Pitch, Yaw, Surge, Sway, Heave).
 *
 * Coordinate system and rotations based on the Moog 6DOF2000E Motion System:
 *
 * - 'Front' and 'Rear' orientations are consistent with the documentation and our setup (e.g., 'rear' = closer to the exit).
 * - All directions (+/-) are relative to a view from the REAR or from ABOVE (top view).
 * - All parameters are expressed in meters (linear) and radians (angular). Default values: 0.
 *
 * Typical value ranges:
 *
 * Parameter    | Normalized Displacement   | Velocity      | Acceleration      | Description
 * -------------|---------------------------|---------------|-------------------|------------------------------------------------------------------------
 * Roll         | ±21°                      | ±30°/s        | ±500°/s²          | Rotation around longitudinal axis (tilting right/left sides)
 * Pitch        | ±22°                      | ±30°/s        | ±500°/s²          | Rotation around lateral axis (tilting backward/forward, front up/down)
 * Yaw          | ±22°                      | ±40°/s        | ±400°/s²          | Rotation around vertical axis (horizontal rotation right/left)
 * Heave        | ±0.178 m                  | ±0.3 m/s      | -0.5G to +0.7G    | Vertical linear movement (down/up)
 * Surge        | +0.259 m / -0.241 m       | ±0.5 m/s      | ±0.6G             | Longitudinal linear movement (forward/backward)
 * Sway         | ±0.259 m                  | ±0.5 m/s      | ±0.6G             | Lateral linear movement (right/left)
 *
 * Tolerances:
 * - PositionTolerance (meters) and RotationTolerance (radians) are used for approximate equality comparisons (for displacement).
 */


using System;
using System.Runtime.InteropServices;


namespace MoogModule
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DofParameters : IEquatable<DofParameters>
    {
        public float Roll;
        public float Pitch;
        public float Yaw;
        public float Surge;
        public float Sway;
        public float Heave;

        // ........................................................................................

        private const float PositionTolerance = 0.0f;   // TODO: set values
        private const float RotationTolerance = 0.0f;   // TODO: set values

        // ........................................................................................

        public static DofParameters Zero => new();
        public static DofParameters NaN => new() { Roll = float.NaN, Pitch = float.NaN, Yaw = float.NaN, Surge = float.NaN, Sway = float.NaN, Heave = float.NaN };

        // ........................................................................................

        public static bool operator == (DofParameters left, DofParameters right)
        {
            return left.Equals(right);
        }

        public static bool operator != (DofParameters left, DofParameters right)
        {
            return !left.Equals(right);
        }

        public static DofParameters operator + (DofParameters left, DofParameters right)
        {
            return new DofParameters
            {
                Roll = left.Roll + right.Roll,
                Pitch = left.Pitch + right.Pitch,
                Yaw = left.Yaw + right.Yaw,
                Surge = left.Surge + right.Surge,
                Sway = left.Sway + right.Sway,
                Heave = left.Heave + right.Heave
            };
        }

        public static DofParameters operator - (DofParameters left, DofParameters right)
        {
            return new DofParameters
            {
                Roll = left.Roll - right.Roll,
                Pitch = left.Pitch - right.Pitch,
                Yaw = left.Yaw - right.Yaw,
                Surge = left.Surge - right.Surge,
                Sway = left.Sway - right.Sway,
                Heave = left.Heave - right.Heave
            };
        }

        public static DofParameters operator * (DofParameters param, float scalar)
        {
            return new DofParameters
            {
                Roll = param.Roll * scalar,
                Pitch = param.Pitch * scalar,
                Yaw = param.Yaw * scalar,
                Surge = param.Surge * scalar,
                Sway = param.Sway * scalar,
                Heave = param.Heave * scalar
            };
        }

        public static DofParameters operator / (DofParameters param, float scalar)
        {
            return new DofParameters
            {
                Roll = param.Roll / scalar,
                Pitch = param.Pitch / scalar,
                Yaw = param.Yaw / scalar,
                Surge = param.Surge / scalar,
                Sway = param.Sway / scalar,
                Heave = param.Heave / scalar
            };
        }

        public readonly bool Equals(DofParameters other)
        {
            return  ApproximatelyEqual(Roll,    other.Roll,     RotationTolerance)  &&
                    ApproximatelyEqual(Pitch,   other.Pitch,    RotationTolerance)  &&
                    ApproximatelyEqual(Yaw,     other.Yaw,      RotationTolerance)  &&
                    ApproximatelyEqual(Surge,   other.Surge,    PositionTolerance)  &&
                    ApproximatelyEqual(Sway,    other.Sway,     PositionTolerance)  &&
                    ApproximatelyEqual(Heave,   other.Heave,    PositionTolerance);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is DofParameters other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Roll, Pitch, Yaw, Surge, Sway, Heave);
        }

        public override readonly string ToString()
        {
            return $"Roll: {Roll:F5}, Pitch: {Pitch:F5}, Yaw: {Yaw:F5}, Surge: {Surge:F5}, Sway: {Sway:F5}, Heave: {Heave:F5}";
        }

        public readonly bool IsValid()
        {
            return float.IsFinite(Roll)     &&
                   float.IsFinite(Pitch)    &&
                   float.IsFinite(Yaw)      &&
                   float.IsFinite(Surge)    &&
                   float.IsFinite(Sway)     &&
                   float.IsFinite(Heave);
        }

        // ........................................................................................

        private static bool ApproximatelyEqual(float a, float b, float tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }
    }
}