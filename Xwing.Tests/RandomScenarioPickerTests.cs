using Shouldly;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Tests;

public sealed class RandomScenarioPickerTests
{
    private static readonly Random FixedRandom = new(42);

    [Fact]
    public void PickShouldRespectCheckboxExclusions()
    {
        XwaScenario?[] rounds = [XwaScenario.SalvageMission, null, null];
        var excluded = new HashSet<XwaScenario> { XwaScenario.SalvageMission, XwaScenario.ChanceEngagement };
        XwaScenario pick = RandomScenarioPicker.Pick(rounds, 2, excluded, null, FixedRandom);
        pick.ShouldNotBe(XwaScenario.SalvageMission);
        pick.ShouldNotBe(XwaScenario.ChanceEngagement);
    }

    [Fact]
    public void PickShouldExcludeScenariosFromRoundWindow()
    {
        XwaScenario?[] rounds =
        [
            XwaScenario.AssaultAtSatelliteArray,
            XwaScenario.ChanceEngagement,
            XwaScenario.SalvageMission,
            null,
        ];
        var excluded = new HashSet<XwaScenario>();
        XwaScenario pick = RandomScenarioPicker.Pick(rounds, 3, excluded, excludeFromRoundNumberOneBased: 2, FixedRandom);
        pick.ShouldNotBe(XwaScenario.ChanceEngagement);
    }

    [Fact]
    public void CollectWindowExclusionsShouldIgnoreInvalidStartRound()
    {
        XwaScenario?[] rounds = [XwaScenario.AncientKnowledge, null];
        HashSet<XwaScenario> window = RandomScenarioPicker.CollectWindowExclusions(rounds, targetRoundOneBased: 2, 5);
        window.ShouldBeEmpty();
    }

    [Fact]
    public void PickShouldFallBackToAllWhenCheckboxAndWindowBlockEverything()
    {
        XwaScenario?[] rounds =
        [
            XwaScenario.AssaultAtSatelliteArray,
            XwaScenario.ChanceEngagement,
            XwaScenario.SalvageMission,
            XwaScenario.ScrambleTheTransmissions,
            XwaScenario.AncientKnowledge,
            null,
        ];
        var excluded = new HashSet<XwaScenario>(XwaScenarios.All);
        XwaScenario pick = RandomScenarioPicker.Pick(rounds, 5, excluded, excludeFromRoundNumberOneBased: 1, FixedRandom);
        XwaScenarios.All.ShouldContain(pick);
    }
}
