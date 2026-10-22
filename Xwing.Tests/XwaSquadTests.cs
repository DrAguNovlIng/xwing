using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

public sealed class XwaSquadTests
{
    private const string SampleXws = """
        {
          "faction": "rebelalliance",
          "pilots": [
            { "id": "lukeskywalker", "ship": "t-65-x-wing" },
            { "id": "wedgeantilles", "ship": "t-65-x-wing" }
          ],
          "vendor": {
            "yasb": {
              "url": "https://yasb.app/?f=Rebel%20Alliance&d=abc"
            }
          }
        }
        """;

    [Fact]
    public void ParseShouldReadFactionShipsPilotsAndUrl()
    {
        Result<XwaSquad> result = XwaSquad.Parse(SampleXws);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.FactionDisplayName.ShouldBe("Rebel Alliance");
        result.Value.Pilots.Count.ShouldBe(2);
        result.Value.Pilots[0].ShipId.ShouldBe("t-65-x-wing");
        result.Value.Pilots[0].PilotId.ShouldBe("lukeskywalker");
        result.Value.Url.ShouldBe("https://yasb.app/?f=Rebel%20Alliance&d=abc");
    }

    [Fact]
    public void ParseShouldAcceptLinkInsteadOfUrl()
    {
        const string json = """
            {
              "faction": "galacticempire",
              "pilots": [{ "id": "darthvader", "ship": "tie-ln-fighter" }],
              "vendor": {
                "launchbaynext": {
                  "link": "https://launchbaynext.app/list/123"
                }
              }
            }
            """;
        Result<XwaSquad> result = XwaSquad.Parse(json);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Url.ShouldBe("https://launchbaynext.app/list/123");
    }

    [Fact]
    public void ParseShouldRejectInvalidJson()
    {
        Result<XwaSquad> result = XwaSquad.Parse("{ not json");
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ParseShouldRejectMissingFaction()
    {
        const string json = """
            {
              "pilots": [{ "ship": "t-65-x-wing" }],
              "vendor": { "yasb": { "url": "https://yasb.app/" } }
            }
            """;
        Result<XwaSquad> result = XwaSquad.Parse(json);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Squad must include a faction.");
    }

    [Fact]
    public void ParseShouldRejectMissingShips()
    {
        const string json = """
            {
              "faction": "resistance",
              "pilots": [],
              "vendor": { "yasb": { "url": "https://yasb.app/" } }
            }
            """;
        Result<XwaSquad> result = XwaSquad.Parse(json);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Squad must include at least one ship.");
    }

    [Fact]
    public void ParseShouldRejectMissingBuildLink()
    {
        const string json = """
            {
              "faction": "resistance",
              "pilots": [{ "ship": "t-70-x-wing" }]
            }
            """;
        Result<XwaSquad> result = XwaSquad.Parse(json);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Squad must include a build link in vendor metadata.");
    }

    [Fact]
    public void GetShipIconSlugShouldRemoveHyphens()
    {
        XwaSquad.GetShipIconSlug("t-65-x-wing").ShouldBe("t65xwing");
    }

    [Fact]
    public void GetFactionIconSlugShouldMapKnownFactions()
    {
        XwaSquad.GetFactionIconSlug("Rebel Alliance").ShouldBe("rebel");
        XwaSquad.GetFactionIconSlug("Resistance").ShouldBe("rebel-outline");
        XwaSquad.GetFactionIconSlug("Unknown Faction").ShouldBeNull();
    }

    [Fact]
    public void ResolvePilotLabelShouldUseCatalogName()
    {
        XwsPilotCatalog.ResolvePilotLabel("lukeskywalker", "t-65-x-wing").ShouldBe("Luke Skywalker");
    }

    [Fact]
    public void ResolvePilotLabelShouldIncludeCaptionForVariantId()
    {
        XwsPilotCatalog.ResolvePilotLabel("lukeskywalker-battleofyavin", "t-65-x-wing")
            .ShouldBe("Luke Skywalker (Battle of Yavin)");
    }

    [Fact]
    public void ResolvePilotLabelShouldFallbackForUnknownPilotId()
    {
        XwsPilotCatalog.ResolvePilotLabel("unknown-pilot-id", "t-65-x-wing")
            .ShouldBe("Unknown Pilot Id");
    }

    [Fact]
    public void ResolvePilotLabelShouldUseShipNameWhenPilotMissing()
    {
        XwsPilotCatalog.ResolvePilotLabel(null, "t-65-x-wing").ShouldBe("T-65 X-wing");
    }

    [Fact]
    public void ParseSquadShipsJsonShouldReadLegacyStringArray()
    {
        IReadOnlyList<SquadShipDisplay> ships = TournamentStore.ParseSquadShipsJson("""
            ["t-65-x-wing","t-65-x-wing"]
            """);
        ships.Count.ShouldBe(2);
        ships[0].PilotLabel.ShouldBe("T-65 X-wing");
    }

    [Fact]
    public void ParseSquadShipsJsonShouldReadShipAndPilotObjects()
    {
        IReadOnlyList<SquadShipDisplay> ships = TournamentStore.ParseSquadShipsJson("""
            [{"ship":"t-65-x-wing","pilot":"lukeskywalker"}]
            """);
        ships.Single().PilotLabel.ShouldBe("Luke Skywalker");
    }
}
