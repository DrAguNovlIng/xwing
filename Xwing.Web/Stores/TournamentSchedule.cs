using Xwing.Web.Data;

namespace Xwing.Web.Stores;

public static class TournamentSchedule
{
    public static DateTimeOffset TodayUtc(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new DateTimeOffset(timeProvider.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
    }

    public static TournamentLifecycleStatus GetStatus(Tournament tournament, DateTimeOffset todayUtc)
    {
        ArgumentNullException.ThrowIfNull(tournament);
        if (tournament.EndDateUtc < todayUtc)
        {
            return TournamentLifecycleStatus.Past;
        }
        if (tournament.StartDateUtc > todayUtc)
        {
            return TournamentLifecycleStatus.Upcoming;
        }
        return TournamentLifecycleStatus.InProgress;
    }
}
