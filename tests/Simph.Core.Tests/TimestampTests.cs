using System;
using System.Diagnostics;

using Simph.Core.Time;

using Xunit;

namespace Simph.Core.Tests;

public class TimestampTests
{
    /// <summary>
    /// The exact answer is the reading times 10^9 over the frequency, rounded
    /// down; decimal holds that product without overflow, so it serves as the
    /// reference.
    /// </summary>
    [Theory]
    [InlineData(10_000_000, 274_167_595_389)]      // most current Windows machines; the reading taken on 2026-11-23
    [InlineData(3_579_545, 98_164_771_123)]        // the old ACPI timer frequency
    [InlineData(3_000_000_000, 82_250_278_616_701)] // a counter driven straight from a 3 GHz TSC
    public void A_counter_reading_converts_to_whole_nanoseconds_rounded_down(long frequency, long reading)
    {
        long expected = (long)decimal.Floor((decimal)reading * 1_000_000_000m / frequency);

        Assert.Equal(expected, Timestamp.FromCounter(reading, frequency).Nanoseconds);
    }

    /// <summary>
    /// Multiplying a 10 MHz counter by 10^9 in one step overflows after about
    /// 15 minutes of uptime. A month of uptime must still convert exactly.
    /// </summary>
    [Fact]
    public void A_month_of_uptime_converts_without_overflow()
    {
        const long frequency = 10_000_000;
        const long monthSeconds = 30L * 24 * 3600;
        long counter = monthSeconds * frequency + 1;

        Assert.Equal(monthSeconds * 1_000_000_000 + 100, Timestamp.FromCounter(counter, frequency).Nanoseconds);
    }

    [Fact]
    public void Adding_a_duration_and_subtracting_moments_agree()
    {
        var start = new Timestamp(27_416_760_000_000);
        var later = start + TimeSpan.FromMilliseconds(1.5);

        Assert.Equal(1_500_000, later.Nanoseconds - start.Nanoseconds);
        Assert.Equal(TimeSpan.FromMilliseconds(1.5), later - start);
        Assert.Equal(start, later - TimeSpan.FromMilliseconds(1.5));
    }

    /// <summary>
    /// In .NET, Stopwatch is the raw performance counter, so the probe and the
    /// daemon keep the timeline they had. (In Unity's Mono it is not, which is
    /// why SystemClock reads the counter itself.)
    /// </summary>
    [Fact]
    public void In_dotnet_the_system_clock_reads_the_same_counter_as_Stopwatch()
    {
        var clock = new SystemClock();

        var before = Timestamp.FromCounter(Stopwatch.GetTimestamp(), Stopwatch.Frequency);
        var now = clock.Now;
        var after = Timestamp.FromCounter(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

        Assert.Equal(Stopwatch.Frequency, clock.CounterFrequency);
        Assert.InRange(now.Nanoseconds, before.Nanoseconds, after.Nanoseconds);
    }

    [Fact]
    public void The_system_clock_never_goes_backwards()
    {
        var clock = new SystemClock();
        var previous = clock.Now;

        for (int i = 0; i < 100_000; i++)
        {
            var now = clock.Now;
            Assert.True(now >= previous);
            previous = now;
        }
    }
}
