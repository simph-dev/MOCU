using System;
using System.Globalization;
using System.IO;
using System.Linq;

using Simph.Core.Geometry;
using Simph.Core.Platform;

using Xunit;
using Xunit.Abstractions;

namespace Simph.Core.Tests;

/// <summary>
/// The platform model run forwards, against what the real platform did. Unlike
/// the compensation tests, which check the model against itself, these hold it
/// to one recorded movement: a change to the model that makes it describe the
/// platform worse fails here.
/// </summary>
public class PlatformResponseTests
{
    private readonly ITestOutputHelper _output;

    public PlatformResponseTests(ITestOutputHelper output) => _output = output;

    private const int MovementStart = 100;

    /// <summary>Data/movement-2026-11-25.csv: what was sent and what the platform reported, frame by frame.</summary>
    private static (Pose6[] sent, Pose6[] reported) Recorded()
    {
        var rows = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Data", "movement-2026-11-25.csv"))
            .Where(line => !line.StartsWith("#") && !line.StartsWith("sent_"))
            .Select(line => line.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray())
            .ToArray();

        Pose6 At(double[] v, int i) => new Pose6(v[i], v[i + 1], v[i + 2], v[i + 3], v[i + 4], v[i + 5]);

        return (rows.Select(v => At(v, 0)).ToArray(), rows.Select(v => At(v, 6)).ToArray());
    }

    /// <summary>Leg lengths, each less its value at rest before the movement.</summary>
    private static double[][] LegsFromRest(Pose6[] poses)
    {
        var geometry = HexapodGeometry.MoogMbE6Dof;
        var legs = poses.Select(p => geometry.LegLengths(p)).ToArray();
        var rest = Enumerable.Range(0, 6).Select(i => legs.Take(MovementStart).Average(l => l[i])).ToArray();

        return legs.Select(l => l.Select((x, i) => x - rest[i]).ToArray()).ToArray();
    }

    /// <summary>Milliseconds from the movement's start until the path first passes this fraction of its length.</summary>
    private static double Reaches(Pose6[] poses, double fraction)
    {
        var from = poses[MovementStart - 1];
        var to = poses[^1];
        double dx = to.Surge - from.Surge, dy = to.Sway - from.Sway, lengthSquared = dx * dx + dy * dy;
        double previous = 0;

        for (int n = MovementStart; n < poses.Length; n++)
        {
            double progress = ((poses[n].Surge - from.Surge) * dx + (poses[n].Sway - from.Sway) * dy) / lengthSquared;
            if (previous < fraction && progress >= fraction)
                return n - 1 + (fraction - previous) / (progress - previous) - MovementStart;
            previous = progress;
        }

        return double.NaN;
    }

    [Fact]
    public void Predicts_the_legs_of_a_recorded_movement()
    {
        var (sent, reported) = Recorded();

        var predicted = LegsFromRest(new PlatformResponse(PlatformModel.MoogMbE6Dof).Predict(sent));
        var measured = LegsFromRest(reported);

        double sum = 0, worst = 0;
        for (int n = 0; n < predicted.Length; n++)
        {
            for (int i = 0; i < 6; i++)
            {
                double error = predicted[n][i] - measured[n][i];
                sum += error * error;
                worst = Math.Max(worst, Math.Abs(error));
            }
        }

        double rms = Math.Sqrt(sum / (predicted.Length * 6));
        _output.WriteLine($"leg error: rms {rms * 1e6:F1} um, worst {worst * 1e6:F1} um");

        // 69 um when this was written.
        Assert.True(rms < 0.000085, $"rms {rms * 1e6:F1} um");
    }

    [Theory]
    [InlineData(0.10)]
    [InlineData(0.50)]
    [InlineData(0.90)]
    public void Predicts_when_a_recorded_movement_passes_along_its_path(double fraction)
    {
        var (sent, reported) = Recorded();

        double predicted = Reaches(new PlatformResponse(PlatformModel.MoogMbE6Dof).Predict(sent), fraction);
        double measured = Reaches(reported, fraction);
        _output.WriteLine($"{fraction:P0}: predicted {predicted:F1} ms, measured {measured:F1} ms");

        // Within 0.8 ms when this was written.
        Assert.True(Math.Abs(predicted - measured) < 1.5, $"predicted {predicted:F1} ms, measured {measured:F1} ms");
    }
}
