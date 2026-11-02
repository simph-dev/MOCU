using System.Runtime.InteropServices;

using Simph.Core.Geometry;

using Simph.MoogProbe.Protocol;

namespace Simph.Core.Tests;

/// <summary>
/// The probe has to put exactly the same bytes on the wire as the daemon the
/// experiments already run on, or a difference in the platform's behaviour tells
/// us about our encoder rather than about the platform.
///
/// The types below are copied verbatim from
/// MOCU-MoogControl/.../CommandPacket.cs and PacketSerializer.cs, rather than
/// referenced: the test project must not take a dependency on the executable
/// that the experiment builds from. If the daemon ever changes, this copy stops
/// matching it and the comparison becomes worthless - so the copy is the thing to
/// re-check when touching either side.
/// </summary>
public class MoogProbeMatchesTheLegacyDaemonTests
{
    [Theory]
    [InlineData(1u, 0u, 0.0, 0.0, -0.22)]
    [InlineData(2u, 1u, 0.1249, 0.0358, -0.22)]
    [InlineData(4242u, 2u, -0.241, 0.259, -0.178)]
    [InlineData(uint.MaxValue, 3u, 0.0, 0.0, 0.0)]
    public void The_probe_encodes_a_command_byte_for_byte_as_the_daemon_does(
        uint sequence, uint command, double surge, double sway, double heave)
    {
        var pose = Pose6.Translation(surge, sway, heave);

        var legacy = LegacySerialize(new LegacyCommandPacket(
            sequence, command, StatusResponseWord.LegacyMocu,
            new LegacyDofParameters
            {
                Roll = (float)pose.Roll,
                Pitch = (float)pose.Pitch,
                Yaw = (float)pose.Yaw,
                Surge = (float)pose.Surge,
                Sway = (float)pose.Sway,
                Heave = (float)pose.Heave
            }));

        var probe = new byte[DofCommandMessage.Size];
        DofCommandMessage.Write(probe, sequence, (MotionCommand)command,
            StatusResponseWord.LegacyMocu, pose);

        Assert.Equal(legacy, probe);
    }

    /// <summary>
    /// And the only intended difference: the declared Update Rate. Everything
    /// outside the Status Response Word stays identical, so a run with
    /// --declare 1000 differs from the daemon in exactly four bytes.
    /// </summary>
    [Fact]
    public void Declaring_a_thousand_hertz_changes_only_the_status_response_word()
    {
        var pose = Pose6.Translation(0.1249, 0.0358, -0.22);

        var asDaemon = new byte[DofCommandMessage.Size];
        var asProbe = new byte[DofCommandMessage.Size];

        DofCommandMessage.Write(asDaemon, 7, MotionCommand.Null, StatusResponseWord.LegacyMocu, pose);
        DofCommandMessage.Write(asProbe, 7, MotionCommand.Null, StatusResponseWord.Build(1000), pose);

        var differing = new List<int>();
        for (int i = 0; i < DofCommandMessage.Size; i++)
            if (asDaemon[i] != asProbe[i])
                differing.Add(i);

        // Offset 20 is the Status Response Word; only its top half carries the rate.
        Assert.Equal(new[] { 20, 21 }, differing);
    }

    [Fact]
    public void Both_encoders_agree_the_packet_is_fifty_two_bytes()
    {
        Assert.Equal(DofCommandMessage.Size, Marshal.SizeOf<LegacyCommandPacket>());
    }


    // ---------------------------------------------- verbatim from the daemon

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct LegacyDofParameters
    {
        public float Roll;
        public float Pitch;
        public float Yaw;
        public float Surge;
        public float Sway;
        public float Heave;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct LegacyCommandPacket
    {
        public readonly uint PacketLength;
        public readonly uint PacketSequenceCount;
        public readonly uint ReservedForFutureUse;
        public readonly uint MessageId;

        public readonly uint MCW;
        public readonly uint StatusResponseWord;
        public readonly LegacyDofParameters Parameters;
        public readonly uint SpecialEffectsActiveCommand;

        public LegacyCommandPacket(uint sequence, uint motionCommandWord,
            uint statusResponseWord, LegacyDofParameters dofParameters)
        {
            PacketLength = 36;
            PacketSequenceCount = sequence;
            ReservedForFutureUse = 0;
            MessageId = 100;

            MCW = motionCommandWord;
            StatusResponseWord = statusResponseWord;
            Parameters = dofParameters;
            SpecialEffectsActiveCommand = 0;
        }
    }

    private static byte[] LegacySerialize(LegacyCommandPacket value)
    {
        byte[] buffer = new byte[Marshal.SizeOf<LegacyCommandPacket>()];
        MemoryMarshal.Write(buffer, in value);

        for (int offset = 0; offset < buffer.Length; offset += 4)
            Array.Reverse(buffer, offset, 4);

        return buffer;
    }
}
