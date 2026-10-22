using Xwing.Web.Data;
using Xwing.Web.Stores;

namespace Xwing.Tests;
public sealed class TournamentScheduleTests
{
    private static readonly DateTimeOffset DefaultEnd = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    [Fact]
    public void FormatTimeLeftToJoinAtFortyEightHoursShouldUseDays()
    {
        var tournament = new Tournament
        {
            StartDateUtc = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = DefaultEnd,
        };
        DateTimeOffset now = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        string? text = TournamentSchedule.FormatTimeLeftToJoin(tournament, now);
        text.ShouldBe("Registration and squad changes closes in 2 days");
    }

    [Fact]
    public void FormatTimeLeftToJoinJustUnderFortyEightHoursShouldUseHoursAndMinutes()
    {
        var tournament = new Tournament
        {
            StartDateUtc = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = DefaultEnd,
        };
        DateTimeOffset now = tournament.StartDateUtc - TimeSpan.FromDays(2) + TimeSpan.FromSeconds(1);
        string? text = TournamentSchedule.FormatTimeLeftToJoin(tournament, now);
        text.ShouldBe("Registration and squad changes closes in 47 hours 59 minutes");
    }

    [Fact]
    public void FormatTimeLeftToJoinWhenClosedShouldReturnNull()
    {
        var tournament = new Tournament
        {
            StartDateUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = DefaultEnd,
        };
        DateTimeOffset now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        string? text = TournamentSchedule.FormatTimeLeftToJoin(tournament, now);
        text.ShouldBeNull();
    }

    [Fact]
    public void TimeLeftToJoinUsesMinuteRefreshShouldBeTrueUnderTwoDays()
    {
        var tournament = new Tournament
        {
            StartDateUtc = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = DefaultEnd,
        };
        DateTimeOffset now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        TournamentSchedule.TimeLeftToJoinUsesMinuteRefresh(tournament, now).ShouldBeTrue();
    }

    [Fact]
    public void TimeLeftToJoinUsesMinuteRefreshShouldBeFalseAtTwoDaysOrMore()
    {
        var tournament = new Tournament
        {
            StartDateUtc = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = DefaultEnd,
        };
        DateTimeOffset now = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        TournamentSchedule.TimeLeftToJoinUsesMinuteRefresh(tournament, now).ShouldBeFalse();
    }
}
