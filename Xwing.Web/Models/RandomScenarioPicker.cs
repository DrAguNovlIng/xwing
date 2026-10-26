using System.Diagnostics.CodeAnalysis;
using Xwing.Web.Data;

namespace Xwing.Web.Models;

public static class RandomScenarioPicker
{
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Scenario pick is not security-sensitive.")]
    public static XwaScenario Pick(
        IReadOnlyList<XwaScenario?> roundScenarios,
        int targetRoundIndex,
        IReadOnlySet<XwaScenario> excludedByCheckbox,
        int? excludeFromRoundNumberOneBased,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(roundScenarios);
        ArgumentNullException.ThrowIfNull(excludedByCheckbox);
        ArgumentNullException.ThrowIfNull(random);

        IReadOnlyList<XwaScenario> all = XwaScenarios.All;
        int targetRoundOneBased = targetRoundIndex + 1;
        HashSet<XwaScenario> windowExcluded = CollectWindowExclusions(
            roundScenarios,
            targetRoundOneBased,
            excludeFromRoundNumberOneBased);

        List<XwaScenario> pool = BuildPool(all, excludedByCheckbox, windowExcluded);
        if (pool.Count == 0)
        {
            pool = BuildPool(all, excludedByCheckbox, windowExcluded: null);
            if (pool.Count == 0)
            {
                pool = all.ToList();
            }
        }

        int index = random.Next(pool.Count);
        return pool[index];
    }

    public static HashSet<XwaScenario> CollectWindowExclusions(
        IReadOnlyList<XwaScenario?> roundScenarios,
        int targetRoundOneBased,
        int? excludeFromRoundNumberOneBased)
    {
        ArgumentNullException.ThrowIfNull(roundScenarios);
        HashSet<XwaScenario> excluded = [];
        if (excludeFromRoundNumberOneBased is not int startRound)
        {
            return excluded;
        }

        if (startRound < 1 || startRound >= targetRoundOneBased)
        {
            return excluded;
        }

        int startIndex = startRound - 1;
        int endIndex = targetRoundOneBased - 2;
        for (int i = startIndex; i <= endIndex && i < roundScenarios.Count; i++)
        {
            if (roundScenarios[i] is XwaScenario scenario)
            {
                excluded.Add(scenario);
            }
        }

        return excluded;
    }

    private static List<XwaScenario> BuildPool(
        IReadOnlyList<XwaScenario> all,
        IReadOnlySet<XwaScenario> excludedByCheckbox,
        HashSet<XwaScenario>? windowExcluded)
    {
        return all
            .Where(s => !excludedByCheckbox.Contains(s) && (windowExcluded is null || !windowExcluded.Contains(s)))
            .ToList();
    }
}
