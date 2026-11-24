using PressingManager.Core.Common;

namespace PressingManager.Tests.Unit;

public class ReportPeriodTests
{
    [Fact]
    public void ToUtcRange_InUtc_CoversWholeLastDay()
    {
        var (fromUtc, toUtc) = ReportPeriod.ToUtcRange(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), TimeZoneInfo.Utc);

        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0), fromUtc);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0), toUtc);
    }

    [Fact]
    public void ToUtcRange_SingleDay_IsTwentyFourHours()
    {
        var day = new DateTime(2026, 11, 23, 15, 30, 0); // l'heure saisie est ignorée

        var (fromUtc, toUtc) = ReportPeriod.ToUtcRange(day, day, TimeZoneInfo.Utc);

        Assert.Equal(TimeSpan.FromHours(24), toUtc - fromUtc);
    }

    [Fact]
    public void ToUtcRange_FollowsTheTimeZoneOffset()
    {
        var utcPlusOne = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");

        var (fromUtc, toUtc) = ReportPeriod.ToUtcRange(new DateTime(2026, 11, 5), new DateTime(2026, 11, 5), utcPlusOne);

        // Minuit local (UTC+1) = 23h la veille en UTC.
        Assert.Equal(new DateTime(2026, 11, 4, 23, 0, 0), fromUtc);
        Assert.Equal(new DateTime(2026, 11, 5, 23, 0, 0), toUtc);
    }

    [Fact]
    public void ToUtcRange_EndBeforeStart_IsRefused()
    {
        Assert.Throws<BusinessRuleException>(() =>
            ReportPeriod.ToUtcRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 9), TimeZoneInfo.Utc));
    }
}
