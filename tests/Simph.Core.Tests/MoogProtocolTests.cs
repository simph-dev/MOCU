using System.Buffers.Binary;

using Simph.Core.Geometry;

using Simph.MoogProbe.Protocol;

namespace Simph.Core.Tests;

/// <summary>
/// The packet layout, checked against CDS7330 Rev. F rather than against the
/// hardware. An encoder that is wrong in a way only the platform notices costs a
/// session at the machine to find, so it is worth pinning here.
/// </summary>
public class MoogProtocolTests
{
    private static byte[] Encode(uint sequence, MotionCommand command, uint statusWord, Pose6 pose)
    {
        var buffer = new byte[DofCommandMessage.Size];
        DofCommandMessage.Write(buffer, sequence, command, statusWord, pose);
        return buffer;
    }

    private static uint Word(byte[] packet, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(offset, 4));

    private static float Real(byte[] packet, int offset) =>
        BinaryPrimitives.ReadSingleBigEndian(packet.AsSpan(offset, 4));


    /// <summary>
    /// Four header words of four bytes each, then nine payload words. Packet
    /// Length counts the payload only - "Bytes, NOT including this header".
    /// </summary>
    [Fact]
    public void A_command_is_fifty_two_bytes_declaring_a_payload_of_thirty_six()
    {
        var packet = Encode(1, MotionCommand.Null, StatusResponseWord.Build(1000), Pose6.Zero);

        Assert.Equal(52, packet.Length);
        Assert.Equal(36u, Word(packet, 0));
        Assert.Equal(52, 16 + (int)Word(packet, 0));
    }

    [Fact]
    public void The_header_carries_the_sequence_count_and_message_id_one_hundred()
    {
        var packet = Encode(4242, MotionCommand.Engage, StatusResponseWord.Build(1000), Pose6.Zero);

        Assert.Equal(4242u, Word(packet, 4));
        Assert.Equal(0u, Word(packet, 8));      // reserved
        Assert.Equal(100u, Word(packet, 12));   // DOF Command Mode Data
        Assert.Equal((uint)MotionCommand.Engage, Word(packet, 16));
    }

    /// <summary>
    /// Roll, pitch, yaw, then longitudinal, lateral, heave. A comment in
    /// MOCU-MoogControl quotes "roll, pitch, heave, surge, yaw, lateral" from
    /// CDS7237, which describes a different machine; that order would send the
    /// heave command to the yaw axis.
    /// </summary>
    [Fact]
    public void The_six_degrees_of_freedom_are_in_the_order_the_manual_gives()
    {
        var pose = new Pose6(roll: 1, pitch: 2, yaw: 3, surge: 4, sway: 5, heave: 6);

        var packet = Encode(1, MotionCommand.Null, StatusResponseWord.Build(1000), pose);

        Assert.Equal(1f, Real(packet, 24));
        Assert.Equal(2f, Real(packet, 28));
        Assert.Equal(3f, Real(packet, 32));
        Assert.Equal(4f, Real(packet, 36));
        Assert.Equal(5f, Real(packet, 40));
        Assert.Equal(6f, Real(packet, 44));
    }

    [Fact]
    public void Everything_goes_out_big_endian()
    {
        var packet = Encode(1, MotionCommand.Null, StatusResponseWord.Build(1000), Pose6.Zero);

        // 36 as four big-endian bytes is 00 00 00 24.
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x24 }, packet[0..4]);
    }

    /// <summary>
    /// The neutral pose the experiments park at, written as the platform
    /// receives it: a float, not a double, so this is what the MBC acts on.
    /// </summary>
    [Fact]
    public void The_neutral_heave_survives_the_narrowing_to_float()
    {
        var packet = Encode(1, MotionCommand.Null, StatusResponseWord.Build(1000),
            Pose6.Translation(0, 0, -0.22));

        Assert.Equal(-0.22f, Real(packet, 44));
        Assert.Equal(-0.22, Real(packet, 44), 6);
    }


    // ------------------------------------------------- Status Response Word

    /// <summary>
    /// The Update Rate occupies d16..d31 with d16 as the least significant bit,
    /// so a declared 1000 Hz is 0x03E8 in the top half of the word.
    /// </summary>
    [Fact]
    public void Declaring_a_thousand_hertz_puts_it_in_the_top_half_of_the_word()
    {
        Assert.Equal(0x03E80040u, StatusResponseWord.Build(1000));
    }

    /// <summary>
    /// MOCU-MoogControl has always declared 0x07D00040, which is 2000 in that
    /// field, while its send loop emits about 989 packets a second.
    /// </summary>
    [Fact]
    public void What_mocu_has_always_sent_declares_two_thousand_hertz()
    {
        Assert.Equal(2000, StatusResponseWord.UpdateRate(StatusResponseWord.LegacyMocu));
        Assert.Equal(StatusResponseWord.FeedbackDof,
            StatusResponseWord.RequestedFeedback(StatusResponseWord.LegacyMocu));
        Assert.Equal(StatusResponseWord.LegacyMocu, StatusResponseWord.Build(2000));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void A_declared_rate_reads_back_as_itself(int rate)
    {
        Assert.Equal(rate, StatusResponseWord.UpdateRate(StatusResponseWord.Build(rate)));
    }

    [Fact]
    public void A_rate_outside_the_fields_range_is_refused_rather_than_truncated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusResponseWord.Build(2001));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusResponseWord.Build(9));
    }

    [Fact]
    public void The_replay_file_id_sits_between_the_rate_and_the_feedback_bits()
    {
        uint word = StatusResponseWord.Build(1000, StatusResponseWord.FeedbackDof, replayFile: 98);

        Assert.Equal(1000, StatusResponseWord.UpdateRate(word));
        Assert.Equal(98, StatusResponseWord.ReplayFile(word));
        Assert.Equal(StatusResponseWord.FeedbackDof, StatusResponseWord.RequestedFeedback(word));
    }


    // ------------------------------------------------------ Status Response

    [Fact]
    public void A_status_response_gives_back_the_mbc_counter_and_the_machine_state()
    {
        var packet = new byte[68];
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(0, 4), 52);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4, 4), 777);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), 200);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(16, 4), (uint)MachineState.Engaged);

        Assert.True(StatusResponse.TryParse(packet, out var response));
        Assert.Equal(777u, response.MbcPacketCount);
        Assert.Equal(MachineState.Engaged, response.State);
        Assert.False(response.HasFault);
    }

    /// <summary>
    /// A reply captured from the platform on 2026-11-02 while it sat parked, with
    /// 1000 Hz declared. The Machine Status Word came back as 0x03E80051: the
    /// state is the low nibble, and the top half is the rate we declared. Casting
    /// the whole word to the state enum - which `ResponsePacket` in the daemon
    /// does - gives 65536081, which is not a state at all.
    /// </summary>
    [Fact]
    public void The_machine_status_word_is_a_word_and_the_state_is_its_low_nibble()
    {
        var packet = new byte[68];
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(0, 4), 52);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4, 4), 29709593);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), 200);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(16, 4), 0x03E80051);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(20, 4), 0x42);

        Assert.True(StatusResponse.TryParse(packet, out var response));

        Assert.Equal(MachineState.Ready, response.State);
        Assert.Equal(1000, response.DeclaredRateEcho);
        Assert.Equal(0x03E80051u, response.MachineStatusWord);

        // Discrete I/O 0x42: base at home, not in maintenance mode, E-stop clear.
        Assert.True(response.AtHome);
        Assert.Equal(DiscreteIo.None, response.Io & DiscreteIo.MaintenanceMode);
        Assert.Equal(DiscreteIo.None, response.Io & DiscreteIo.EStopActive);
    }

    /// <summary>
    /// And the reason a reply cannot be paired with a command by sequence number:
    /// what comes back at offset 4 is the MBC's own counter. The captured value
    /// is 29709593 for a command sent as 12345.
    /// </summary>
    [Fact]
    public void The_reply_does_not_echo_the_sequence_count_we_sent()
    {
        var packet = new byte[68];
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4, 4), 29709593);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), 200);

        Assert.True(StatusResponse.TryParse(packet, out var response));
        Assert.NotEqual(12345u, response.MbcPacketCount);
    }

    [Fact]
    public void Anything_that_is_not_message_two_hundred_is_rejected()
    {
        var packet = new byte[64];
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), 100);

        Assert.False(StatusResponse.TryParse(packet, out _));
        Assert.False(StatusResponse.TryParse(new byte[8], out _));
    }

    [Fact]
    public void A_non_zero_fault_word_is_reported_without_being_decoded()
    {
        var packet = new byte[64];
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12, 4), 200);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(28, 4), 0x00000040);

        Assert.True(StatusResponse.TryParse(packet, out var response));
        Assert.True(response.HasFault);
        Assert.Equal(0x40u, response.FaultWord2);
    }


    // ------------------------------------------------- feedback blocks

    private static byte[] Words(params uint[] words)
    {
        var bytes = new byte[4 * words.Length];
        for (int i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4 * i, 4), words[i]);
        return bytes;
    }

    /// <summary>The fixed words of a reply from the parked platform, 2026-11-25.</summary>
    private static readonly uint[] Header = { 0, 0x021FA9D2, 0, 200, 0x03E800D1, 0x42, 0, 0, 0 };

    private static readonly uint[] DofBlock = { 1, 0x3801C13E, 0x36E1CD76, 0xB7AF2863, 0xB7737228, 0xB7FB132E, 0xB7CB7244 };
    private static readonly uint[] LegBlock = { 2, 0x36924C1D, 0x3603888B, 0x38690919, 0xB56989F2, 0x3874F286, 0x37FB529C };

    /// <summary>
    /// A reply asked for DOF and leg lengths (0xC0), word for word as the MBC
    /// sent it: the blocks chain, DOF first, and a zero ends the chain.
    /// </summary>
    [Fact]
    public void A_reply_with_dof_and_leg_lengths_gives_both()
    {
        var reply = Words(Header.Concat(DofBlock).Concat(LegBlock).Append(0u).ToArray());

        Assert.True(StatusResponse.TryReadDofFeedback(reply, out var pose));
        Assert.True(StatusResponse.TryReadBlock(reply, FeedbackBlock.LegLengths, out var legs));

        Assert.Equal(-1.4511e-5, pose.Surge, 9);
        Assert.Equal(3.0936e-5, pose.Roll, 9);
        Assert.Equal(6, legs.Length);
        Assert.Equal(5.556e-5, legs[2], 9);
    }

    /// <summary>
    /// Asked for leg lengths alone, the MBC puts them where the DOF block
    /// usually sits. Reading a fixed offset would take them for a pose.
    /// </summary>
    [Fact]
    public void Leg_lengths_alone_are_not_read_as_a_pose()
    {
        var reply = Words(Header.Concat(LegBlock).Append(0u).ToArray());

        Assert.False(StatusResponse.TryReadDofFeedback(reply, out _));
        Assert.True(StatusResponse.TryReadBlock(reply, FeedbackBlock.LegLengths, out _));
    }

    [Fact]
    public void Actuator_data_skips_its_count_word_and_carries_ten_values_per_actuator()
    {
        var actuators = new uint[1 + 60];
        actuators[1 + 10 * 2 + 1] = 0x3F800000;   // actuator C, second value: 1.0

        var reply = Words(Header.Concat(DofBlock).Append(3u).Concat(actuators).Append(0u).ToArray());

        Assert.True(StatusResponse.TryReadBlock(reply, FeedbackBlock.ActuatorData, out var values));
        Assert.Equal(60, values.Length);
        Assert.Equal(1.0f, values[21]);
    }

    [Fact]
    public void A_block_cut_short_is_not_read()
    {
        var reply = Words(Header.Concat(DofBlock.Take(4)).ToArray());

        Assert.False(StatusResponse.TryReadDofFeedback(reply, out _));
    }
}
