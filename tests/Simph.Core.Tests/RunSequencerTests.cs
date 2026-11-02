using Simph.Core.Geometry;
using Simph.Core.Time;

using Simph.MoogProbe;
using Simph.MoogProbe.Protocol;

namespace Simph.Core.Tests;

/// <summary>
/// The logic that decides what goes to the platform. These run without any
/// hardware, which is the only way to exercise a stop request or a fault on
/// purpose.
/// </summary>
public class RunSequencerTests
{
    private static readonly Pose6 Neutral = Pose6.Translation(0, 0, -0.22);
    private static readonly Pose6 Away = Pose6.Translation(0.1, 0, -0.22);

    private static Timestamp Ms(double milliseconds) =>
        new Timestamp(Timestamp.ToTicks(TimeSpan.FromMilliseconds(milliseconds)));

    private static RunSequencer Sequencer(bool engages = true, double seconds = 1.0) =>
        new RunSequencer(engages, Neutral,
            new[] { new Segment("hold", TimeSpan.FromSeconds(seconds), _ => Away) },
            engageLimit: TimeSpan.FromSeconds(30),
            parkLimit: TimeSpan.FromSeconds(60),
            report: null!);

    /// <summary>Walks a sequencer up to the middle of its schedule.</summary>
    private static (RunSequencer sequencer, double now) Running()
    {
        var s = Sequencer();
        s.Next(MachineState.Ready, Ms(0), false);
        s.Next(MachineState.Ready, Ms(1), false);
        s.Next(MachineState.Engaged, Ms(2), false);
        s.Next(MachineState.Engaged, Ms(3), false);

        Assert.Equal(RunSequencer.Stage.Running, s.Current);
        return (s, 4);
    }


    /// <summary>
    /// The promise the listen phase rests on: whatever the machine reports, the
    /// probe never asks it for anything. That is why it can run with the platform
    /// parked and nobody watching.
    /// </summary>
    [Theory]
    [InlineData(MachineState.Ready)]
    [InlineData(MachineState.Engaged)]
    [InlineData(MachineState.Fault1)]
    [InlineData(MachineState.Standby)]
    public void A_listen_run_only_ever_sends_null(MachineState state)
    {
        var s = Sequencer(engages: false);

        for (int ms = 0; ms < 1500; ms++)
            Assert.Equal(MotionCommand.Null, s.Next(state, Ms(ms), stopRequested: false).Command);
    }

    [Fact]
    public void A_listen_run_stops_at_once_when_asked_because_nothing_is_engaged()
    {
        var s = Sequencer(engages: false);

        s.Next(MachineState.Ready, Ms(0), false);

        Assert.True(s.Next(MachineState.Ready, Ms(1), stopRequested: true).Finished);
    }

    [Fact]
    public void Waits_for_ready_before_it_asks_to_engage()
    {
        var s = Sequencer();

        Assert.Equal(MotionCommand.Null, s.Next(null, Ms(0), false).Command);
        Assert.Equal(MotionCommand.Null, s.Next(MachineState.Initializing, Ms(1), false).Command);
        Assert.Equal(RunSequencer.Stage.WaitingForReady, s.Current);
    }

    [Fact]
    public void Asks_to_engage_with_the_neutral_pose_only_while_the_machine_is_ready()
    {
        var s = Sequencer();

        s.Next(MachineState.Ready, Ms(0), false);
        var engage = s.Next(MachineState.Ready, Ms(1), false);
        var whileRising = s.Next(MachineState.Standby, Ms(2), false);

        Assert.Equal(MotionCommand.Engage, engage.Command);
        Assert.Equal(Neutral, engage.Pose);
        Assert.Equal(MotionCommand.Null, whileRising.Command);
        Assert.Equal(Neutral, whileRising.Pose);
    }

    [Fact]
    public void Follows_the_schedule_once_engaged_then_parks()
    {
        var (s, now) = Running();

        Assert.Equal(Away, s.Next(MachineState.Engaged, Ms(now), false).Pose);

        var afterSchedule = s.Next(MachineState.Engaged, Ms(1100), false);

        Assert.Equal(RunSequencer.Stage.Disengaging, s.Current);
        Assert.Equal(MotionCommand.Disengage, afterSchedule.Command);
    }

    /// <summary>
    /// The MBC disengages by itself after 50 packets of silence, on its terms and
    /// with an alarm. So a stop keeps the stream alive and walks the platform
    /// down: Ctrl+C used to end the loop instead.
    /// </summary>
    [Fact]
    public void A_stop_while_running_disengages_instead_of_dropping_the_stream()
    {
        var (s, now) = Running();

        var stop = s.Next(MachineState.Engaged, Ms(now), stopRequested: true);

        Assert.False(stop.Finished);
        Assert.Equal(MotionCommand.Disengage, stop.Command);
        Assert.Equal(RunSequencer.Stage.Disengaging, s.Current);
    }

    [Fact]
    public void A_stop_holds_the_last_pose_rather_than_stepping_back_to_neutral()
    {
        var (s, now) = Running();

        s.Next(MachineState.Engaged, Ms(now), false);

        Assert.Equal(Away, s.Next(MachineState.Engaged, Ms(now + 1), stopRequested: true).Pose);
    }

    [Fact]
    public void A_stop_while_rising_disengages_too()
    {
        var s = Sequencer();

        s.Next(MachineState.Ready, Ms(0), false);
        s.Next(MachineState.Standby, Ms(1), false);

        Assert.Equal(MotionCommand.Disengage, s.Next(MachineState.Standby, Ms(2), stopRequested: true).Command);
    }

    [Fact]
    public void A_stop_before_engaging_just_ends_because_there_is_nothing_to_park()
    {
        var s = Sequencer();

        s.Next(null, Ms(0), false);

        Assert.True(s.Next(MachineState.Ready, Ms(1), stopRequested: true).Finished);
    }

    /// <summary>
    /// Parking passes through Parking and then a second of ClassOneFault, with no
    /// fault set, before Ready. The stream has to carry on through both.
    /// </summary>
    [Fact]
    public void Keeps_streaming_through_parking_until_the_machine_is_ready_again()
    {
        var (s, now) = Running();

        s.Next(MachineState.Engaged, Ms(now), stopRequested: true);

        Assert.False(s.Next(MachineState.Parking, Ms(now + 1), true).Finished);
        Assert.False(s.Next(MachineState.Fault1, Ms(now + 7000), true).Finished);
        Assert.True(s.Next(MachineState.Ready, Ms(now + 8000), true).Finished);
    }

    [Fact]
    public void A_fault_while_running_ends_the_schedule()
    {
        var (s, now) = Running();

        s.Next(MachineState.Fault1, Ms(now), false);

        Assert.Equal(RunSequencer.Stage.Disengaging, s.Current);
    }

    [Fact]
    public void Does_not_try_to_engage_a_machine_that_starts_in_a_fault()
    {
        var s = Sequencer();

        Assert.True(s.Next(MachineState.Fault2, Ms(0), false).Finished);
    }

    [Fact]
    public void Gives_up_on_a_machine_that_never_becomes_ready()
    {
        var s = Sequencer();

        s.Next(null, Ms(0), false);

        Assert.True(s.Next(null, Ms(31000), false).Finished);
    }

    [Fact]
    public void Waits_longer_for_parking_than_the_engage_limit_allows()
    {
        var (s, now) = Running();

        s.Next(MachineState.Engaged, Ms(now), stopRequested: true);

        Assert.False(s.Next(MachineState.Parking, Ms(now + 45000), true).Finished);
        Assert.True(s.Next(MachineState.Parking, Ms(now + 61000), true).Finished);
    }


    // ----------------------------------------------------------- option bounds

    [Theory]
    [InlineData("--dither", "21")]      // past 20 mm
    [InlineData("--dither", "-1")]
    [InlineData("--heave", "-0.5")]
    [InlineData("--heave", "0.1")]
    public void Refuses_options_that_would_reach_the_platform_unvalidated(string key, string value)
    {
        Assert.False(ProbeOptions.TryParse(new[] { key, value }, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Compensation_is_refused_at_any_rate_but_the_filter_frame()
    {
        var args = new[] { "--precompensate", "--rate", "989" };

        Assert.False(ProbeOptions.TryParse(args, out _, out _));
    }

    [Fact]
    public void A_drive_lag_means_nothing_without_the_filter_compensation()
    {
        Assert.False(ProbeOptions.TryParse(new[] { "--drive-lag", "96" }, out _, out _));
        Assert.True(ProbeOptions.TryParse(new[] { "--precompensate", "--drive-lag", "96" }, out _, out _));
    }

    [Fact]
    public void Refuses_a_dither_that_is_small_but_fast()
    {
        var args = new[] { "--dither", "20", "--dither-hz", "5" };

        Assert.False(ProbeOptions.TryParse(args, out _, out _));
    }

    [Fact]
    public void Accepts_the_dither_it_was_measured_with()
    {
        var args = new[] { "--dither", "2", "--dither-hz", "0.5" };

        Assert.True(ProbeOptions.TryParse(args, out var options, out _));
        Assert.Equal(0.002, options.DitherMetres, 6);
    }
}
