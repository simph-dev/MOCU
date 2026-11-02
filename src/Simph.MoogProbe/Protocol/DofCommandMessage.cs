using System;
using System.Buffers.Binary;

using Simph.Core.Geometry;

namespace Simph.MoogProbe.Protocol
{
    /// <summary>
    /// Message 100, DOF Command Mode Data, HOST to MBC. CDS7330 chapter 4.
    ///
    /// Written out field by field in big-endian rather than by blitting a struct
    /// and reversing every four bytes. The layout is the thing being tested, so it
    /// should be readable here and comparable against the manual line by line.
    ///
    ///   offset 0   Packet Length            uint32   bytes, NOT including the header
    ///   offset 4   Packet Sequence Count    uint32
    ///   offset 8   Reserved                 uint32
    ///   offset 12  Message ID               uint32   = 100
    ///   offset 16  Motion Command Word      uint32
    ///   offset 20  Status Response Word     uint32
    ///   offset 24  DOF Roll                 float32  radians
    ///   offset 28  DOF Pitch                float32  radians
    ///   offset 32  DOF Yaw                  float32  radians
    ///   offset 36  DOF Longitudinal (X)     float32  metres
    ///   offset 40  DOF Lateral (Y)          float32  metres
    ///   offset 44  DOF Heave (Z)            float32  metres
    ///   offset 48  Special Effects Active   uint32
    ///
    /// Total 52 bytes; Packet Length is 36, being everything after the 16-byte
    /// header. The axis order - roll, pitch, yaw, then X, Y, Z - is from Rev. F.
    /// A comment in MOCU-MoogControl quotes a different order (roll, pitch, heave,
    /// surge, yaw, lateral); that came from CDS7237, which describes another
    /// machine, and is marked wrong there.
    /// </summary>
    public static class DofCommandMessage
    {
        public const int Size = 52;
        public const uint PayloadLength = 36;
        public const uint MessageId = 100;

        public static void Write(
            Span<byte> destination,
            uint sequenceCount,
            MotionCommand command,
            uint statusResponseWord,
            Pose6 pose)
        {
            if (destination.Length < Size)
                throw new ArgumentException($"Need {Size} bytes, got {destination.Length}.", nameof(destination));

            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(0, 4), PayloadLength);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(4, 4), sequenceCount);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(8, 4), 0);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(12, 4), MessageId);

            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(16, 4), (uint)command);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(20, 4), statusResponseWord);

            WriteSingle(destination.Slice(24, 4), pose.Roll);
            WriteSingle(destination.Slice(28, 4), pose.Pitch);
            WriteSingle(destination.Slice(32, 4), pose.Yaw);
            WriteSingle(destination.Slice(36, 4), pose.Surge);
            WriteSingle(destination.Slice(40, 4), pose.Sway);
            WriteSingle(destination.Slice(44, 4), pose.Heave);

            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(48, 4), 0);
        }

        private static void WriteSingle(Span<byte> destination, double value) =>
            BinaryPrimitives.WriteSingleBigEndian(destination, (float)value);
    }
}
