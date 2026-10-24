namespace Xwing.Web.Data;

public static class XwaScenarios
{
    public static IReadOnlyList<XwaScenario> All { get; } =
    [
        XwaScenario.AssaultAtSatelliteArray,
        XwaScenario.ChanceEngagement,
        XwaScenario.SalvageMission,
        XwaScenario.ScrambleTheTransmissions,
        XwaScenario.AncientKnowledge,
    ];

    public static string? GetDisplayLabel(XwaScenario? scenario) => scenario switch
    {
        null => null,
        XwaScenario.AssaultAtSatelliteArray => "1 - Assault at the Satellite Array",
        XwaScenario.ChanceEngagement => "2 - Chance Engagement",
        XwaScenario.SalvageMission => "3 - Salvage Mission",
        XwaScenario.ScrambleTheTransmissions => "4 - Scramble the Transmissions",
        XwaScenario.AncientKnowledge => "5 - Ancient Knowledge",
        _ => scenario.ToString(),
    };

    public static bool IsDefined(XwaScenario scenario) => All.Contains(scenario);
}
