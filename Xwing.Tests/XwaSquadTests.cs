using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

public sealed class XwaSquadTests
{
    private const string SampleXws = """
        {
          "faction": "rebelalliance",
          "pilots": [
            { "ship": "t-65-x-wing" },
            { "ship": "t-65-x-wing" }
          ],
          "vendor": {
            "yasb": {
              "url": "https://yasb.app/?f=Rebel%20Alliance&d=abc"
            }
          }
        }
        """;

    [Fact]
    public void ParseShouldReadFactionShipsAndUrl()
    {
        Result<XwaSquad> result = XwaSquad.Parse(SampleXws);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.FactionDisplayName.ShouldBe("Rebel Alliance");
        result.Value.ShipIds.Count.ShouldBe(2);
        result.Value.ShipDisplayNames[0].ShouldBe("T-65 X-wing");
        result.Value.Url.ShouldBe("https://yasb.app/?f=Rebel%20Alliance&d=abc");
    }

    [Fact]
    public void ParseShouldAcceptLinkInsteadOfUrl()
    {
        const string json = """
            {
              "faction": "galacticempire",
              "pilots": [{ "ship": "tie-ln-fighter" }],
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
}
