using System;
using System.Buffers.Binary;

using Simph.Core.Geometry;

namespace Simph.MoogProbe.Protocol
{
    /// <summary>
    /// Machine state, CDS7330 section 4.3.1. The MBC accepts set-points only in
    /// <see cref="Engaged"/>; before that it answers our packets but ignores
    /// their pose, so a probe run that never engages cannot move the platform.
    /// </summary>
    public enum MachineState : uint
    {
        Initializing = 0x0,
        Ready        = 0x1,
        Standby      = 0x2,
        Engaged      = 0x3,
        Parking      = 0x4,
        Fault1       = 0x8,
        Fault2       = 0x9,
        Fault2Hold   = 0xA,
        Disabled     = 0xB,
        Inhibited    = 0xC,
        Frozen       = 0xD
    }


    /// <summary>
    /// Discrete I/O Word, CDS7330 section 4.3.2.
    /// </summary>
    [Flags]
    public enum DiscreteIo : uint
    {
        None = 0,
        MaintenanceMode = 1u << 0,
        BaseAtHome      = 1u << 1,
        EStopActive     = 1u << 2,
        UserInput1      = 1u << 3,
        UserInput2      = 1u << 4
    }


    /// <summary>
    /// Message 200, Motion Base Status Response, MBC to HOST.
    ///
    /// Measured against the real platform on 2026-11-02, 68 bytes:
    ///
    ///   offset 0   Packet Length         uint32   52, header excluded
    ///   offset 4   Packet Sequence Count uint32   the MBC's own counter
    ///   offset 8   Reserved              uint32
    ///   offset 12  Message ID            uint32   = 200
    ///   offset 16  Machine Status Word   uint32   note 4.4-3
    ///   offset 20  Discrete I/O Word     uint32   section 4.3.2
    ///   offset 24  Latched Fault Word 1  uint32
    ///   offset 28  Latched Fault Word 2  uint32
    ///   offset 32  Latched Fault Word 3  uint32
    ///   offset 36  Optional Status Data  uint32   = 1
    ///   offset 40  DOF feedback          6x float roll, pitch, yaw, X, Y, Z
    ///   offset 64  trailing word         uint32
    ///
    /// Two things here are not what MOCU-MoogControl assumes, and both were
    /// found by dumping a real reply rather than by reading:
    ///
    /// **The sequence count is not an echo.** We sent 12345 and got 29709593
    /// back. The MBC puts its own packet counter there, so a reply cannot be
    /// matched to a command by sequence number. Pairing by order of arrival is
    /// only approximate: it occasionally gives a negative round trip.
    ///
    /// **The Machine Status Word is a word, not a state.** The state is its low
    /// nibble; the top half carries back the Update Rate we declared. Casting
    /// the whole 32 bits to the state enum, which `ResponsePacket` does, yields
    /// values like 0x03E80051 that match nothing. In the existing code that word
    /// only ever reaches a debug chart label, so it has been harmless - but any
    /// engage handshake that waits for READY by comparing the whole word would
    /// wait forever.
    /// </summary>
    public readonly struct StatusResponse
    {
        public const uint MessageId = 200;
        public const int MinimumSize = 40;

        /// <summary>
        /// The MBC's own packet counter, NOT an echo of ours. It counts packets
        /// the MBC accepted and does not free-run: two seconds of silence advance
        /// it by exactly one packet. So a step of 1 between replies means nothing
        /// was lost, and it says nothing about the MBC's frame rate.
        /// </summary>
        public uint MbcPacketCount { get; }

        /// <summary>The whole Machine Status Word, kept so nothing is lost.</summary>
        public uint MachineStatusWord { get; }

        public DiscreteIo Io { get; }
        public uint FaultWord1 { get; }
        public uint FaultWord2 { get; }
        public uint FaultWord3 { get; }

        private StatusResponse(uint mbcPacketCount, uint machineStatusWord, uint io,
            uint fault1, uint fault2, uint fault3)
        {
            MbcPacketCount = mbcPacketCount;
            MachineStatusWord = machineStatusWord;
            Io = (DiscreteIo)io;
            FaultWord1 = fault1;
            FaultWord2 = fault2;
            FaultWord3 = fault3;
        }

        /// <summary>The state, from the low nibble of the Machine Status Word.</summary>
        public MachineState State => (MachineState)(MachineStatusWord & 0xF);

        /// <summary>
        /// The Update Rate the MBC believes we declared, from the top half of the
        /// status word. It comes back verbatim for every value tried, 100 through
        /// 2000, so it confirms the round trip but says nothing about whether the
        /// rate was accepted or clamped.
        /// </summary>
        public int DeclaredRateEcho => (int)(MachineStatusWord >> 16);

        public bool HasFault => FaultWord1 != 0 || FaultWord2 != 0 || FaultWord3 != 0;

        public bool AtHome => (Io & DiscreteIo.BaseAtHome) != 0;

        public static bool TryParse(ReadOnlySpan<byte> source, out StatusResponse response)
        {
            response = default;

            if (source.Length < MinimumSize)
                return false;

            if (BinaryPrimitives.ReadUInt32BigEndian(source.Slice(12, 4)) != MessageId)
                return false;

            response = new StatusResponse(
                mbcPacketCount: BinaryPrimitives.ReadUInt32BigEndian(source.Slice(4, 4)),
                machineStatusWord: BinaryPrimitives.ReadUInt32BigEndian(source.Slice(16, 4)),
                io: BinaryPrimitives.ReadUInt32BigEndian(source.Slice(20, 4)),
                fault1: BinaryPrimitives.ReadUInt32BigEndian(source.Slice(24, 4)),
                fault2: BinaryPrimitives.ReadUInt32BigEndian(source.Slice(28, 4)),
                fault3: BinaryPrimitives.ReadUInt32BigEndian(source.Slice(32, 4)));

            return true;
        }

        /// <summary>
        /// The reported pose, when the Status Response Word asked for DOF
        /// feedback and the packet is long enough to carry it.
        /// </summary>
        public static bool TryReadDofFeedback(ReadOnlySpan<byte> source, out Pose6 pose)
        {
            pose = default;

            if (source.Length < 64)
                return false;

            pose = new Pose6(
                roll:  BinaryPrimitives.ReadSingleBigEndian(source.Slice(40, 4)),
                pitch: BinaryPrimitives.ReadSingleBigEndian(source.Slice(44, 4)),
                yaw:   BinaryPrimitives.ReadSingleBigEndian(source.Slice(48, 4)),
                surge: BinaryPrimitives.ReadSingleBigEndian(source.Slice(52, 4)),
                sway:  BinaryPrimitives.ReadSingleBigEndian(source.Slice(56, 4)),
                heave: BinaryPrimitives.ReadSingleBigEndian(source.Slice(60, 4)));

            return true;
        }
    }
}
