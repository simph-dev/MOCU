using System;

using Simph.Core.Geometry;
using Simph.Core.Platform;

using Xunit;

namespace Simph.Core.Tests;

/// <summary>
/// The leg geometry against the MBC's own configuration (geometry.xml), which
/// gives leg lengths the geometry has to reproduce on its own.
/// </summary>
public class HexapodGeometryTests
{
    private static readonly HexapodGeometry Geometry = HexapodGeometry.MoogMbE6Dof;

    /// <summary>Retracted Actuator Length 0.71882 m.</summary>
    [Fact]
    public void Settled_legs_are_at_the_configured_retracted_length()
    {
        foreach (double length in Geometry.LegLengths(Pose6.Translation(0, 0, 0)))
            Assert.InRange(length, 0.71882 - 0.0005, 0.71882 + 0.0005);
    }

    /// <summary>
    /// DOF Start Position -0.220775 m with Start Position Actuator Length 0.16232 m
    /// on top of the retracted 0.71882.
    /// </summary>
    [Fact]
    public void Neutral_legs_are_at_the_configured_start_length()
    {
        foreach (double length in Geometry.LegLengths(Pose6.Translation(0, 0, -0.220775)))
            Assert.InRange(length, 0.88114 - 0.0005, 0.88114 + 0.0005);
    }

    [Theory]
    [InlineData(0.13, 0.0, -0.22, 0, 0, 0)]
    [InlineData(-0.10, 0.08, -0.15, 0, 0, 0)]
    [InlineData(0.05, -0.05, -0.25, 0.05, -0.04, 0.10)]
    public void Leg_lengths_lead_back_to_the_same_pose(double surge, double sway, double heave,
        double roll, double pitch, double yaw)
    {
        var pose = new Pose6(roll, pitch, yaw, surge, sway, heave);
        var neutral = Pose6.Translation(0, 0, -0.22);

        var found = Geometry.PoseFromLegLengths(Geometry.LegLengths(pose), guess: neutral);

        foreach (var axis in Axes.All)
            Assert.True(Math.Abs(found[axis] - pose[axis]) < 1e-9, $"{axis}: {found[axis]} against {pose[axis]}");
    }

    /// <summary>
    /// The layout is symmetric about the surge axis, so a movement straight ahead
    /// changes the legs in mirrored pairs and cannot push the platform sideways.
    /// </summary>
    [Fact]
    public void Straight_ahead_leaves_the_mirrored_legs_equal()
    {
        var legs = Geometry.LegLengths(Pose6.Translation(0.13, 0, -0.22));

        Assert.Equal(legs[0], legs[1], 12);
        Assert.Equal(legs[2], legs[5], 12);
        Assert.Equal(legs[3], legs[4], 12);
    }
}
