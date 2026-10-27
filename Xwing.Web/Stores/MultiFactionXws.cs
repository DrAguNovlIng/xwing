using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public static partial class MultiFactionXws
{
    private const string YasbBuilderName = "YASB - X-Wing 2.5 XWA";
    [SuppressMessage("Sonar", "S1075:URIs should not be hardcoded", Justification = "YASB builder base URL is fixed for XWS export.")]
    private const string YasbBuilderUrl = "https://yasb.app/";

    public static Result<ParsedXwsSquad> Parse(string jsonText, string? sourceLabel = null)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return Result.Fail<ParsedXwsSquad>("Squad JSON is required.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonText);
        }
        catch (JsonException)
        {
            return Result.Fail<ParsedXwsSquad>("Squad JSON is not valid.");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Fail<ParsedXwsSquad>("Squad JSON must be an object.");
            }

            if (!root.TryGetProperty("pilots", out JsonElement pilotsElement) ||
                pilotsElement.ValueKind != JsonValueKind.Array)
            {
                return Result.Fail<ParsedXwsSquad>("Squad must include pilots.");
            }

            string? vendorUrl = FindVendorUrl(root);
            if (vendorUrl is null)
            {
                return Result.Fail<ParsedXwsSquad>("Squad must include a build link in vendor metadata.");
            }

            Result<(int SerializationVersion, IReadOnlyList<string> ShipChunks)> serialized =
                ParseSerializedShipsFromUrl(vendorUrl);
            if (!serialized.IsSuccess)
            {
                return Result.Fail<ParsedXwsSquad>(serialized.Error!);
            }

            var pilotElements = new List<JsonElement>();
            foreach (JsonElement pilot in pilotsElement.EnumerateArray())
            {
                if (pilot.ValueKind == JsonValueKind.Object)
                {
                    pilotElements.Add(pilot.Clone());
                }
            }

            if (pilotElements.Count == 0)
            {
                return Result.Fail<ParsedXwsSquad>("Squad must include at least one ship.");
            }

            if (pilotElements.Count != serialized.Value!.ShipChunks.Count)
            {
                return Result.Fail<ParsedXwsSquad>(
                    "Squad pilots do not match the YASB build link. Re-export from YASB and try again.");
            }

            var pilots = new List<ParsedXwsPilot>();
            for (int index = 0; index < pilotElements.Count; index++)
            {
                JsonElement pilot = pilotElements[index];
                if (!pilot.TryGetProperty("ship", out JsonElement shipElement) ||
                    shipElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(shipElement.GetString()))
                {
                    return Result.Fail<ParsedXwsSquad>("Each pilot must include a ship.");
                }

                string shipId = shipElement.GetString()!.Trim();
                string? pilotId = ReadPilotId(pilot);
                int points = ReadPilotPoints(pilot);
                pilots.Add(new ParsedXwsPilot
                {
                    Key = Guid.NewGuid(),
                    ShipId = shipId,
                    PilotLabel = XwsPilotCatalog.ResolvePilotLabel(pilotId, shipId),
                    PilotJson = pilot,
                    SerializedChunk = serialized.Value.ShipChunks[index],
                    Points = points,
                });
            }

            string? ruleset = ReadOptionalString(root, "ruleset");
            string? versionTag = ReadOptionalString(root, "version");
            return Result.Ok(new ParsedXwsSquad
            {
                SourceLabel = sourceLabel,
                RawJson = jsonText.Trim(),
                SerializationVersion = serialized.Value.SerializationVersion,
                Ruleset = ruleset,
                VersionTag = versionTag,
                Pilots = pilots,
            });
        }
    }

    public static Result<string> BuildCombined(
        IReadOnlyList<ParsedXwsPilot> includedPilots,
        int serializationVersion,
        string? ruleset,
        string? versionTag,
        string squadName)
    {
        ArgumentNullException.ThrowIfNull(includedPilots);
        if (includedPilots.Count == 0)
        {
            return Result.Fail<string>("Add at least one ship.");
        }

        string trimmedName = string.IsNullOrWhiteSpace(squadName) ? "Unnamed Squadron" : squadName.Trim();
        int totalPoints = includedPilots.Sum(p => p.Points);
        string serializedPayload = string.Join(
            'Y',
            includedPilots.Select(p => p.SerializedChunk));
        string link = BuildYasbLink(serializationVersion, serializedPayload, trimmedName);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("description", string.Empty);
            writer.WriteString("name", trimmedName);
            writer.WritePropertyName("pilots");
            writer.WriteStartArray();
            foreach (ParsedXwsPilot pilot in includedPilots)
            {
                pilot.PilotJson.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteNumber("points", totalPoints);
            writer.WritePropertyName("vendor");
            writer.WriteStartObject();
            writer.WritePropertyName("yasb");
            writer.WriteStartObject();
            writer.WriteString("builder", YasbBuilderName);
            writer.WriteString("builder_url", YasbBuilderUrl);
            writer.WriteString("link", link);
            writer.WriteEndObject();
            writer.WriteEndObject();
            if (!string.IsNullOrWhiteSpace(versionTag))
            {
                writer.WriteString("version", versionTag);
            }

            if (!string.IsNullOrWhiteSpace(ruleset))
            {
                writer.WriteString("ruleset", ruleset);
            }

            writer.WriteEndObject();
        }

        return Result.Ok(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string BuildYasbLink(int serializationVersion, string serializedPayload, string squadName)
    {
        string d = "v" + serializationVersion.ToString(CultureInfo.InvariantCulture) + "ZeZ100Z" + serializedPayload;
        return YasbBuilderUrl + "?f=All&d=" + Uri.EscapeDataString(d) + "&sn=" + Uri.EscapeDataString(squadName) + "&obs=";
    }

    private static Result<(int SerializationVersion, IReadOnlyList<string> ShipChunks)> ParseSerializedShipsFromUrl(
        string vendorUrl)
    {
        if (!Uri.TryCreate(vendorUrl, UriKind.Absolute, out Uri? uri))
        {
            return Result.Fail<(int, IReadOnlyList<string>)>("Build link is not a valid URL.");
        }

        string? d = GetQueryParameter(uri, "d");
        if (string.IsNullOrWhiteSpace(d))
        {
            return Result.Fail<(int, IReadOnlyList<string>)>("Build link must include squad data.");
        }

        Match match = SerializedParameterPattern().Match(d.Trim());
        if (!match.Success)
        {
            return Result.Fail<(int, IReadOnlyList<string>)>("Build link uses an unsupported squad format.");
        }

        if (!int.TryParse(match.Groups["version"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
        {
            return Result.Fail<(int, IReadOnlyList<string>)>("Build link uses an unsupported squad format.");
        }

        string shipsPart = match.Groups["ships"].Value;
        IReadOnlyList<string> chunks = shipsPart.Length == 0
            ? []
            : shipsPart.Split('Y', StringSplitOptions.RemoveEmptyEntries);
        return Result.Ok((version, chunks));
    }

    private static string? GetQueryParameter(Uri uri, string name)
    {
        string query = uri.Query;
        if (query.Length <= 1)
        {
            return null;
        }

        foreach (string segment in query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equalsIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex <= 0)
            {
                continue;
            }

            string key = Uri.UnescapeDataString(segment[..equalsIndex]);
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(segment[(equalsIndex + 1)..]);
        }

        return null;
    }

    private static int ReadPilotPoints(JsonElement pilot)
    {
        if (pilot.TryGetProperty("points", out JsonElement pointsElement) &&
            pointsElement.ValueKind == JsonValueKind.Number &&
            pointsElement.TryGetInt32(out int points))
        {
            return points;
        }

        return 0;
    }

    private static string? ReadPilotId(JsonElement pilot)
    {
        if (pilot.TryGetProperty("id", out JsonElement idElement) &&
            idElement.ValueKind == JsonValueKind.String)
        {
            string? id = idElement.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        if (pilot.TryGetProperty("name", out JsonElement nameElement) &&
            nameElement.ValueKind == JsonValueKind.String)
        {
            string? name = nameElement.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return null;
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out JsonElement element) &&
            element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static string? FindVendorUrl(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("vendor", out JsonElement vendorElement))
            {
                string? fromVendor = FindUrlInVendorObject(vendorElement);
                if (fromVendor is not null)
                {
                    return fromVendor;
                }
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals("vendor"))
                {
                    continue;
                }

                string? nested = FindVendorUrl(property.Value);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                string? nested = FindVendorUrl(item);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? FindUrlInVendorObject(JsonElement vendorElement)
    {
        if (vendorElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonElement vendorData in vendorElement.EnumerateObject().Select(property => property.Value))
        {
            if (vendorData.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? url = ReadHttpUrl(vendorData, "url");
            if (url is not null)
            {
                return url;
            }

            url = ReadHttpUrl(vendorData, "link");
            if (url is not null)
            {
                return url;
            }
        }

        return null;
    }

    private static string? ReadHttpUrl(JsonElement vendorData, string propertyName)
    {
        if (!vendorData.TryGetProperty(propertyName, out JsonElement urlElement) ||
            urlElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? value = urlElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return trimmed;
    }

    [GeneratedRegex(@"^v(?<version>\d+)Z[^Z]*Z[^Z]*Z(?<ships>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SerializedParameterPattern();
}

public sealed record ParsedXwsSquad
{
    public string? SourceLabel { get; init; }
    public required string RawJson { get; init; }
    public required int SerializationVersion { get; init; }
    public string? Ruleset { get; init; }
    public string? VersionTag { get; init; }
    public required IReadOnlyList<ParsedXwsPilot> Pilots { get; init; }
}

public sealed record ParsedXwsPilot
{
    public required Guid Key { get; init; }
    public required string ShipId { get; init; }
    public required string PilotLabel { get; init; }
    public required JsonElement PilotJson { get; init; }
    public required string SerializedChunk { get; init; }
    public int Points { get; init; }
}
