using PressingManager.Core.Common;

namespace PressingManager.Tests.Unit;

public class ReportTrendTests
{
    private static DateTime Utc(int month, int day, int hour = 12) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildTrend_ShortPeriod_GivesOnePointPerDay_WithZeroForEmptyDays()
    {
        var points = ReportCalculator.BuildTrend(
            new[] { (Utc(11, 2), 500m), (Utc(11, 2, 18), 250m) },
            new[] { (Utc(11, 3), 100m) },
            Utc(11, 1, 0),
            Utc(11, 4, 0),
            TimeZoneInfo.Utc);

        Assert.Equal(3, points.Count);
        Assert.All(points, p => Assert.False(p.IsMonthly));
        Assert.Equal(new DateTime(2026, 11, 1), points[0].Period);
        Assert.Equal((0m, 0m), (points[0].Collected, points[0].Expenses));
        Assert.Equal((750m, 0m), (points[1].Collected, points[1].Expenses));
        Assert.Equal((0m, 100m), (points[2].Collected, points[2].Expenses));
    }

    [Fact]
    public void BuildTrend_LongPeriod_GroupsByMonth()
    {
        var points = ReportCalculator.BuildTrend(
            new[] { (Utc(9, 5), 100m), (Utc(9, 20), 200m), (Utc(11, 2), 400m) },
            new[] { (Utc(10, 10), 50m) },
            Utc(9, 1, 0),
            Utc(12, 1, 0),
            TimeZoneInfo.Utc);

        Assert.Equal(3, points.Count);
        Assert.All(points, p => Assert.True(p.IsMonthly));
        Assert.Equal(new DateTime(2026, 9, 1), points[0].Period);
        Assert.Equal(300m, points[0].Collected);
        Assert.Equal(50m, points[1].Expenses);
        Assert.Equal(400m, points[2].Collected);
    }

    [Fact]
    public void BuildTrend_UsesTheLocalDay_NotTheUtcDay()
    {
        var plusOne = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");

        // 23h30 UTC le 1er novembre = 00h30 le 2 novembre a UTC+1.
        var points = ReportCalculator.BuildTrend(
            new[] { (new DateTime(2026, 11, 1, 23, 30, 0, DateTimeKind.Utc), 900m) },
            Array.Empty<(DateTime, decimal)>(),
            new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 3, 23, 0, 0, DateTimeKind.Utc),
            plusOne);

        Assert.Equal(3, points.Count);
        Assert.Equal(new DateTime(2026, 11, 1), points[0].Period);
        Assert.Equal(0m, points[0].Collected);
        Assert.Equal(900m, points[1].Collected);
    }
}
