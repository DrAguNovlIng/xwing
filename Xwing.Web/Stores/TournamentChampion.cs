using Xwing.Web.Data;

namespace Xwing.Web.Stores;

public static class TournamentChampion
{
    public static int? GetWinnerUserId(TournamentMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return match.Winner switch
        {
            TournamentMatchWinner.SlotA => match.SlotAUserId,
            TournamentMatchWinner.SlotB => match.SlotBUserId,
            _ => null,
        };
    }
}
