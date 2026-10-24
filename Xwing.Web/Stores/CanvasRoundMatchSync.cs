using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

internal static class CanvasRoundMatchSync
{
    public static void ApplyTemplateMatchRounds(IEnumerable<TemplateMatch> matches, IReadOnlyList<int> columnWidthCells)
    {
        foreach (TemplateMatch match in matches)
        {
            match.RoundNumber = BracketRoundLayout.GetRoundNumberForPosition(match.PositionX, columnWidthCells);
        }
    }

    public static void ApplyTournamentMatchRounds(IEnumerable<TournamentMatch> matches, IReadOnlyList<int> columnWidthCells)
    {
        foreach (TournamentMatch match in matches)
        {
            match.RoundNumber = BracketRoundLayout.GetRoundNumberForPosition(match.PositionX, columnWidthCells);
        }
    }
}
