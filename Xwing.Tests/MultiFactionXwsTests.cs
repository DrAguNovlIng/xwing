using System.Text.Json;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

public sealed class MultiFactionXwsTests
{
    private const string SampleMultiFactionXws = """
        {
          "description": "",
          "name": "Unnamed Squadron",
          "pilots": [
            {
              "id": "bluesquadronpilot",
              "name": "bluesquadronpilot",
              "points": 10,
              "ship": "asf01bwing",
              "upgrades": {
                "torpedo": ["advprotontorpedoes"],
                "configuration": ["stabilizedsfoils"]
              }
            },
            {
              "id": "caithrenalli-btanr2ywing",
              "name": "caithrenalli-btanr2ywing",
              "points": 7,
              "ship": "btanr2ywing",
              "upgrades": { "turret": ["dorsalturret"] }
            }
          ],
          "points": 17,
          "vendor": {
            "yasb": {
              "builder": "YASB - X-Wing 2.5 XWA",
              "builder_url": "https://yasb.app/",
              "link": "https://yasb.app/?f=All&d=v10ZeZ100Z76XWWW134WWW313WY484XWW137WWWWW&sn=Unnamed%20Squadron&obs="
            }
          },
          "version": "50P-2.1.1",
          "ruleset": "XWA"
        }
        """;

    [Fact]
    public void ParseShouldReadPilotsAndSerializationChunks()
    {
        Result<ParsedXwsSquad> result = MultiFactionXws.Parse(SampleMultiFactionXws);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.SerializationVersion.ShouldBe(10);
        result.Value.Pilots.Count.ShouldBe(2);
        result.Value.Pilots[0].SerializedChunk.ShouldBe("76XWWW134WWW313W");
        result.Value.Pilots[1].SerializedChunk.ShouldBe("484XWW137WWWWW");
        result.Value.Ruleset.ShouldBe("XWA");
    }

    [Fact]
    public void BuildCombinedShouldMatchMultiFactionShape()
    {
        Result<ParsedXwsSquad> parsed = MultiFactionXws.Parse(SampleMultiFactionXws);
        parsed.IsSuccess.ShouldBeTrue();
        Result<string> built = MultiFactionXws.BuildCombined(
            parsed.Value!.Pilots,
            parsed.Value.Ruleset,
            parsed.Value.VersionTag,
            "Unnamed Squadron");
        built.IsSuccess.ShouldBeTrue();
        using JsonDocument document = JsonDocument.Parse(built.Value!);
        JsonElement root = document.RootElement;
        root.TryGetProperty("faction", out _).ShouldBeFalse();
        root.TryGetProperty("obstacles", out _).ShouldBeFalse();
        root.TryGetProperty("vendor", out _).ShouldBeFalse();
        root.GetProperty("points").GetInt32().ShouldBe(17);
        root.GetProperty("ruleset").GetString().ShouldBe("XWA");
        root.GetProperty("pilots").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void BuildCombinedShouldDropRemovedShip()
    {
        Result<ParsedXwsSquad> parsed = MultiFactionXws.Parse(SampleMultiFactionXws);
        parsed.IsSuccess.ShouldBeTrue();
        ParsedXwsPilot kept = parsed.Value!.Pilots[0];
        Result<string> built = MultiFactionXws.BuildCombined(
            [kept],
            parsed.Value.Ruleset,
            parsed.Value.VersionTag,
            "Unnamed Squadron");
        built.IsSuccess.ShouldBeTrue();
        using JsonDocument document = JsonDocument.Parse(built.Value!);
        document.RootElement.GetProperty("pilots").GetArrayLength().ShouldBe(1);
        document.RootElement.GetProperty("points").GetInt32().ShouldBe(10);
    }

    [Fact]
    public void ParseShouldRejectPilotCountMismatch()
    {
        const string json = """
            {
              "pilots": [{ "id": "a", "ship": "asf01bwing", "points": 1 }],
              "vendor": {
                "yasb": {
                  "link": "https://yasb.app/?d=v10ZeZ100Z76XWWW134WWW313WY484XWW137WWWWW"
                }
              }
            }
            """;
        Result<ParsedXwsSquad> result = MultiFactionXws.Parse(json);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("match");
    }
}
