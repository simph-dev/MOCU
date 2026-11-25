using System;
using System.IO;

using Simph.Core.Geometry;
using Simph.Core.Motion.Limits;
using Simph.Core.Platform;
using Simph.MoogProbe;

using Xunit;

namespace Simph.Core.Tests;

/// <summary>
/// config/rig-setup.json, the file the probe (and later the daemon) reads the
/// platform from, against the values the core carries for the same machine.
/// </summary>
public class RigSetupTests
{
    private static RigSetup TheLabsSetup() => RigSetup.Load(RigSetup.DefaultPath);

    [Fact]
    public void The_setup_file_describes_the_same_machine_as_the_core()
    {
        var setup = TheLabsSetup();
        var model = PlatformModel.MoogMbE6Dof;
        var envelope = MotionEnvelope.MoogMbE6Dof;

        Assert.Equal(model.FilterAcceleration, setup.Model.FilterAcceleration);
        Assert.Equal(model.DriveLag, setup.Model.DriveLag);
        Assert.Equal(model.DriveLead, setup.Model.DriveLead);
        Assert.NotNull(setup.Model.LegGeometry);
        Assert.Equal(Pose6.Translation(0, 0, -0.22), setup.Neutral);
        Assert.Equal("192.168.2.1:16384", setup.Mbc.ToString());

        foreach (var axis in Axes.All)
        {
            Assert.Equal(envelope[axis].MinPosition, setup.Envelope[axis].MinPosition, 9);
            Assert.Equal(envelope[axis].MaxPosition, setup.Envelope[axis].MaxPosition, 9);
            Assert.Equal(envelope[axis].MaxSpeed, setup.Envelope[axis].MaxSpeed, 9);
            Assert.Equal(envelope[axis].MaxAcceleration, setup.Envelope[axis].MaxAcceleration, 9);
        }
    }

    [Fact]
    public void Its_geometry_gives_the_same_leg_lengths()
    {
        var neutral = Pose6.Translation(0.05, -0.03, -0.22);

        Assert.Equal(HexapodGeometry.MoogMbE6Dof.LegLengths(neutral), TheLabsSetup().Model.LegGeometry.LegLengths(neutral));
    }

    /// <summary>
    /// Heave was left unchecked until 2026-11-25. The bound is the data sheet's
    /// excursion about neutral; a movement that goes past it is refused.
    /// </summary>
    [Fact]
    public void Heave_is_bounded_about_neutral()
    {
        var heave = TheLabsSetup().Envelope[Axis.Heave];

        Assert.True(heave.PositionKnown);
        Assert.Equal(-0.22 - 0.178, heave.MinPosition, 9);
        Assert.Equal(-0.22 + 0.178, heave.MaxPosition, 9);
    }

    [Fact]
    public void A_bound_left_null_stays_unchecked()
    {
        string json = File.ReadAllText(RigSetup.DefaultPath)
            .Replace("\"min\": -0.398, \"max\": -0.042", "\"min\": null, \"max\": null");

        Assert.False(RigSetup.Parse(json).Envelope[Axis.Heave].PositionKnown);
    }

    [Fact]
    public void A_missing_field_is_named()
    {
        string json = File.ReadAllText(RigSetup.DefaultPath).Replace("\"driveLagMilliseconds\"", "\"driveLag\"");

        var error = Assert.Throws<FormatException>(() => RigSetup.Parse(json));

        Assert.Contains("driveLagMilliseconds", error.Message);
    }

    [Fact]
    public void A_setup_without_a_drive_lead_describes_a_pure_lag()
    {
        string json = File.ReadAllText(RigSetup.DefaultPath).Replace("\"driveLeadMilliseconds\": 9.3,", "");

        Assert.DoesNotContain("\"driveLeadMilliseconds\"", json);
        Assert.Equal(TimeSpan.Zero, RigSetup.Parse(json).Model.DriveLead);
    }

    [Fact]
    public void The_older_per_axis_form_can_still_be_chosen()
    {
        string json = File.ReadAllText(RigSetup.DefaultPath).Replace("\"driveLagActsOn\": \"legs\"", "\"driveLagActsOn\": \"axes\"");

        Assert.Null(RigSetup.Parse(json).Model.LegGeometry);
    }
}
