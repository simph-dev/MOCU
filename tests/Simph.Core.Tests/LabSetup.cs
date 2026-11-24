using System;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Compensation;
using Simph.Core.Motion.Limits;
using Simph.Core.Time;

namespace Simph.Core.Tests;

/// <summary>
/// The numbers the lab actually runs with, so the tests below read as statements
/// about the real experiment rather than about invented values.
///
/// Taken from MirrorExperiment/Parameters.cs on branch MirrorExperiments, and
/// from the published heading-discrimination method (Yakubovich et al. 2020:
/// 0.13 m in 1 s, peak velocity 0.31 m/s, peak acceleration 1.14 m/s^2).
/// </summary>
public static class LabSetup
{
    /// <summary>Parameters.Distance - metres travelled in one stimulus.</summary>
    public const double Distance = 0.13;

    /// <summary>Parameters.StimulusDuration - what the participant should experience.</summary>
    public static readonly TimeSpan StimulusDuration = TimeSpan.FromSeconds(1);

    /// <summary>Parameters.ProfileSigmas.</summary>
    public const double Sigmas = 3.0;

    /// <summary>
    /// Parameters.MoogMovementDurationCorrectionFactor. The commanded duration is
    /// the intended one multiplied by this, because the platform takes longer to
    /// travel than it is told to.
    /// </summary>
    public const double CorrectionFactor = 0.8;

    /// <summary>Parameters.StartHeading - the widest heading offset, in degrees.</summary>
    public const double WidestHeadingDegrees = 16.0;

    /// <summary>Parameters.MoogNeutralPosition.</summary>
    public static readonly Pose6 Neutral = Pose6.Translation(surge: 0, sway: 0, heave: -0.22);

    /// <summary>MachineSettings.DesiredFPS of 1000 - one position per millisecond.</summary>
    public static readonly TimeSpan CommandInterval = TimeSpan.FromMilliseconds(1);


    public static GaussianCdfProfile Profile() => new GaussianCdfProfile(Sigmas);

    /// <summary>
    /// The stimulus movement: forward along a heading <paramref name="headingDegrees"/>
    /// off straight ahead, starting from the neutral pose.
    /// </summary>
    public static MotionPlan Stimulus(double headingDegrees, TimeSpan commandedDuration)
    {
        double heading = headingDegrees * Math.PI / 180.0;

        var displacement = Pose6.Translation(
            surge: Distance * Math.Cos(heading),
            sway: Distance * Math.Sin(heading),
            heave: 0);

        return new MotionPlan(Neutral, Neutral + displacement, commandedDuration, Timestamp.Zero, Profile());
    }

    /// <summary>The movement as the participant should experience it.</summary>
    public static MotionPlan Intended(double headingDegrees = WidestHeadingDegrees) =>
        Stimulus(headingDegrees, StimulusDuration);

    /// <summary>The movement as the live experiments command it, shortened by the correction factor.</summary>
    public static MotionPlan Commanded(double headingDegrees = WidestHeadingDegrees) =>
        Stimulus(headingDegrees, StimulusDuration * CorrectionFactor);

    /// <summary>
    /// The drives' first-order lag after the MBC's filter, measured on the platform
    /// on 2026-11-02: 96 ms, the same on X and Y.
    /// </summary>
    public static readonly TimeSpan DriveLag = TimeSpan.FromMilliseconds(96);

    /// <summary>The plan with the filter and the drives compensated, as the new system sends it.</summary>
    public static CompensatedMotion Compensated(MotionPlan plan) =>
        new CompensatedMotion(plan, MotionEnvelope.MoogMbE6Dof, CommandInterval, DriveLag);
}
