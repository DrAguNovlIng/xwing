using System.Globalization;
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

    public static string? FormatTimeLeftToJoin(Tournament tournament, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(tournament);
        TimeSpan remaining = tournament.StartDateUtc - utcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }
        if (remaining >= TimeSpan.FromDays(2))
        {
            int days = (int)Math.Floor(remaining.TotalDays);
            return days == 1
                ? "1 day left to join"
                : $"{days.ToString(CultureInfo.InvariantCulture)} days left to join";
        }
        if (remaining < TimeSpan.FromMinutes(1))
        {
            return "Less than 1 minute left to join";
        }
        int totalMinutes = (int)Math.Floor(remaining.TotalMinutes);
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        string hourPart = hours switch
        {
            0 => string.Empty,
            1 => "1 hour",
            _ => $"{hours.ToString(CultureInfo.InvariantCulture)} hours",
        };
        string minutePart = minutes switch
        {
            1 => "1 minute",
            _ => $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes",
        };
        string combined;
        if (hours == 0)
        {
            combined = minutePart;
        }
        else if (minutes == 0)
        {
            combined = hourPart;
        }
        else
        {
            combined = $"{hourPart} {minutePart}";
        }
        return $"{combined} left to join";
    }

    public static bool TimeLeftToJoinUsesMinuteRefresh(Tournament tournament, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(tournament);
        TimeSpan remaining = tournament.StartDateUtc - utcNow;
        return remaining > TimeSpan.Zero && remaining < TimeSpan.FromDays(2);
    }
}
