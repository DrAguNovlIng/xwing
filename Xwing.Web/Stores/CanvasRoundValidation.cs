using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

internal static class CanvasRoundValidation
{
    public static Result? ValidateInputs(IReadOnlyList<CanvasRoundInput> rounds, int graphWidthCells)
    {
        if (rounds.Count == 0)
        {
            return null;
        }

        int[] widths = rounds.Select(r => r.WidthCells).ToArray();
        Result? widthValidation = BracketRoundLayout.ValidateColumnWidths(widths, graphWidthCells);
        if (widthValidation is not null)
        {
            return widthValidation;
        }

        if (rounds.Any(round => round.Scenario is XwaScenario scenario && !XwaScenarios.IsDefined(scenario)))
        {
            return Result.Fail("Unknown scenario.");
        }

        return null;
    }
}
