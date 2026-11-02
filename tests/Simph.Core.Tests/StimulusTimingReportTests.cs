using System.Globalization;

using Simph.MoogProbe;

namespace Simph.Core.Tests;

/// <summary>
/// The report that settles whether the picture is late. A wrong sign or a frame's
/// offset here would read as a fact about the rig, so its traces are built by hand
/// with the answer known.
/// </summary>
public class StimulusTimingReportTests
{
    private const double Metres = 0.13;
    private const double Heading = 16 * Math.PI / 180;

    /// <summary>Smooth, so that a level's crossing has an exact answer.</summary>
    private static double Smooth(double fraction)
    {
        double t = Math.Clamp(fraction, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>When <see cref="Smooth"/> reaches a level, by bisection.</summary>
    private static double SmoothCrossing(double level)
    {
        double low = 0, high = 1;

        for (int i = 0; i < 60; i++)
        {
            double middle = (low + high) / 2;

            if (Smooth(middle) < level) low = middle;
            else high = middle;
        }

        return low;
    }

    /// <summary>Out along the heading from a delay, then back from a later time.</summary>
    private static List<TracePoint> Path(double delay, double duration, double step, double until, double? returnsAt = null, double scale = 1)
    {
        var path = new List<TracePoint>();

        for (double t = 0; t <= until; t += step)
        {
            double along = Smooth((t - delay) / duration);

            if (returnsAt is double back)
                along -= Smooth((t - back) / duration);

            path.Add(new TracePoint(t,
                scale * Metres * along * Math.Cos(Heading),
                scale * Metres * along * Math.Sin(Heading)));
        }

        return path;
    }

    private static StimulusTrace Trace(double platformDelay = 0.15, double cameraDelay = 0.24, double trackedShare = 0)
    {
        const double duration = 1.0;

        var platform = Path(platformDelay, duration, 0.001, 2.4, returnsAt: 1.8);
        var rig = Path(cameraDelay, duration, 1.0 / 90, 1.3);

        var eye = rig.Select(p =>
        {
            var carried = platform[(int)Math.Round(p.Seconds * 1000)];
            return new TracePoint(p.Seconds, p.Surge + trackedShare * carried.Surge, p.Sway + trackedShare * carried.Sway);
        }).ToList();

        return new StimulusTrace(12, hasPlatform: true, starsShown: 0.105,
            Path(0.01, duration, 0.001, 2.4, returnsAt: 1.8), platform, rig, eye,
            rig.Select(_ => -1.0).ToList());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void Reads_each_path_at_the_time_it_was_built_with(int level)
    {
        var timing = StimulusTimingReport.Measure(Trace());

        double crossing = SmoothCrossing(MovementReport.Levels[level]);

        Assert.Equal((0.01 + crossing) * 1000, timing.Command[level], 1.0);
        Assert.Equal((0.15 + crossing) * 1000, timing.Platform[level], 1.0);
        Assert.Equal((0.24 + crossing) * 1000, timing.Rig[level], 1.0);
    }

    /// <summary>
    /// The recording outlasts the stimulus, so it can hold the start of the way
    /// back. The levels still belong to the way out.
    /// </summary>
    [Fact]
    public void The_way_back_does_not_count_as_arriving()
    {
        var timing = StimulusTimingReport.Measure(Trace());

        Assert.Equal((0.15 + SmoothCrossing(0.99)) * 1000, timing.Platform[4], 1.0);
        Assert.Equal(Metres * 1000, timing.PlatformTravelMillimetres, 0.5);
    }

    [Fact]
    public void Says_how_late_the_picture_is_and_which_way()
    {
        string text = StimulusTimingReport.Describe(new[] { StimulusTimingReport.Measure(Trace()) });

        Assert.Contains("+90", text);
    }

    [Fact]
    public void A_picture_ahead_of_the_platform_comes_out_negative()
    {
        var early = Trace(platformDelay: 0.30, cameraDelay: 0.24);

        string text = StimulusTimingReport.Describe(new[] { StimulusTimingReport.Measure(early) });

        Assert.Contains("-60", text);
    }

    [Fact]
    public void The_first_frame_with_stars_is_the_one_after_they_were_shown()
    {
        var timing = StimulusTimingReport.Measure(Trace());

        Assert.True(timing.FirstFrameWithStars > 105);
        Assert.True(timing.FirstFrameWithStars <= 105 + 1000.0 / 90 + 0.001);
    }

    [Fact]
    public void Tracking_that_stands_still_leaves_the_eye_on_the_rig()
    {
        var timing = StimulusTimingReport.Measure(Trace(trackedShare: 0));

        Assert.Equal(0, timing.PeakTrackedMillimetres, 6);
    }

    /// <summary>
    /// A tracking camera fixed in the room sees the platform carry the headset, and
    /// the eye then travels the stimulus twice over. This is what the figure is for.
    /// </summary>
    [Fact]
    public void Tracking_that_follows_the_platform_shows_the_platforms_travel()
    {
        var timing = StimulusTimingReport.Measure(Trace(trackedShare: 1));

        Assert.Equal(Metres * 1000, timing.PeakTrackedMillimetres, 0.5);
    }

    [Fact]
    public void A_trial_that_never_moved_has_no_crossings_rather_than_some_from_noise()
    {
        var still = Enumerable.Range(0, 100)
            .Select(n => new TracePoint(n / 90.0, 1e-5 * Math.Sin(n), 0))
            .ToList();

        var trace = new StimulusTrace(1, false, 0, new List<TracePoint>(), new List<TracePoint>(), still, still, new double[0]);

        Assert.All(StimulusTimingReport.Measure(trace).Rig, level => Assert.True(double.IsNaN(level)));
    }

    /// <summary>
    /// The contract with StimulusTimingLog in the experiment, which cannot be
    /// compiled here: these are the columns it writes, in its number format.
    /// </summary>
    [Fact]
    public void Reads_the_files_the_experiment_writes()
    {
        const long moog = 638_000_000_000_000_000;

        long Ms(double ms) => moog + (long)(ms * 10_000);
        string Ticks(double ms) => Ms(ms).ToString(CultureInfo.InvariantCulture);

        string[] marks =
        {
            "name,utc_ticks",
            $"start,{Ticks(-1)}",
            $"moog,{Ticks(0)}",
            $"visual,{Ticks(101)}",
        };

        string[] frames =
        {
            "utc_ticks,frame,rig_surge,rig_sway,eye_surge,eye_sway,eye_heave,delta_s,motion_to_photon_s",
            $"{Ticks(100)},7000,0,0,0.5,0.25,1.2,0.0111,-1",
            $"{Ticks(111)},7001,0.01,0.002,0.51,0.252,1.2,0.0111,0.021",
            $"{Ticks(122)},7002,0.02,0.004,0.52,0.254,1.2,0.0111,0.022",
        };

        string[] rows =
        {
            "kind,utc_ticks,surge,sway,heave",
            $"command,{Ticks(2)},0,0,-0.22",
            $"command,{Ticks(3)},0.1,0,-0.22",
            $"response,{Ticks(4)},0,0,-0.22",
            $"response,{Ticks(5)},0.05,0,-0.22",
        };

        var trace = StimulusTimingReport.Parse(12, marks, frames, rows);

        Assert.True(trace.HasPlatform);
        Assert.Equal(2, trace.Command.Count);
        Assert.Equal(2, trace.Platform.Count);
        Assert.Equal(3, trace.Rig.Count);
        Assert.Equal(0.101, trace.StarsShown!.Value, 6);
        Assert.Equal(0.111, trace.Rig[1].Seconds, 6);
        Assert.Equal(0.51, trace.Eye[1].Surge, 6);
        Assert.Equal(0.252, trace.Eye[1].Sway, 6);
        Assert.Equal(0.1, trace.Command[1].Surge, 6);
        Assert.Equal(0.05, trace.Platform[1].Surge, 6);

        Assert.Equal(21, StimulusTimingReport.Measure(trace).MotionToPhoton[0], 6);
    }

    /// <summary>
    /// Two hours off is what a local time written as if it were UTC would look like.
    /// It has to stop the report, not produce a lag of two hours.
    /// </summary>
    [Fact]
    public void Refuses_rows_from_another_clock()
    {
        string[] marks = { "name,utc_ticks", "moog,638000000000000000" };
        string[] frames = { "utc_ticks,frame,rig_surge,rig_sway,eye_surge,eye_sway,eye_heave,delta_s,motion_to_photon_s" };
        string[] rows = { "kind,utc_ticks,surge,sway,heave", "response,638000072000000000,0,0,-0.22" };

        Assert.Throws<InvalidDataException>(() => StimulusTimingReport.Parse(1, marks, frames, rows));
    }

    [Fact]
    public void A_trial_whose_platform_log_never_came_still_gives_its_visual_times()
    {
        var trace = Trace();
        var without = new StimulusTrace(trace.Trial, true, trace.StarsShown,
            new List<TracePoint>(), new List<TracePoint>(), trace.Rig, trace.Eye, trace.MotionToPhoton);

        var timing = StimulusTimingReport.Measure(without);

        Assert.All(timing.Platform, level => Assert.True(double.IsNaN(level)));
        Assert.False(double.IsNaN(timing.Rig[2]));
        Assert.NotEmpty(StimulusTimingReport.Describe(new[] { timing }));
    }
}
