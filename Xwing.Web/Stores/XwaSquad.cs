using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public sealed record XwaSquad
{
    public required string FactionId { get; init; }
    public required string FactionDisplayName { get; init; }
    public required IReadOnlyList<string> ShipIds { get; init; }
    public required IReadOnlyList<string> ShipDisplayNames { get; init; }

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Parsed absolute URL stored for persistence and href binding.")]
    public required string Url { get; init; }

    public static Result<XwaSquad> Parse(string jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return Result.Fail<XwaSquad>("Squad JSON is required.");
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonText);
        }
        catch (JsonException)
        {
            return Result.Fail<XwaSquad>("Squad JSON is not valid.");
        }
        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Fail<XwaSquad>("Squad JSON must be an object.");
            }
            if (!root.TryGetProperty("faction", out JsonElement factionElement) ||
                factionElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(factionElement.GetString()))
            {
                return Result.Fail<XwaSquad>("Squad must include a faction.");
            }
            string factionId = factionElement.GetString()!.Trim();
            if (!root.TryGetProperty("pilots", out JsonElement pilotsElement) ||
                pilotsElement.ValueKind != JsonValueKind.Array)
            {
                return Result.Fail<XwaSquad>("Squad must include pilots.");
            }
            var shipIds = new List<string>();
            foreach (JsonElement pilot in pilotsElement.EnumerateArray())
            {
                if (pilot.ValueKind != JsonValueKind.Object ||
                    !pilot.TryGetProperty("ship", out JsonElement shipElement) ||
                    shipElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                string? shipId = shipElement.GetString();
                if (string.IsNullOrWhiteSpace(shipId))
                {
                    continue;
                }
                shipIds.Add(shipId.Trim());
            }
            if (shipIds.Count == 0)
            {
                return Result.Fail<XwaSquad>("Squad must include at least one ship.");
            }
            string? url = FindVendorUrl(root);
            if (url is null)
            {
                return Result.Fail<XwaSquad>("Squad must include a build link in vendor metadata.");
            }
            var shipDisplayNames = shipIds.Select(FormatShipDisplayName).ToList();
            return Result.Ok(new XwaSquad
            {
                FactionId = factionId,
                FactionDisplayName = FormatFactionDisplayName(factionId),
                ShipIds = shipIds,
                ShipDisplayNames = shipDisplayNames,
                Url = url,
            });
        }
    }

    public static string FormatShipDisplayName(string shipId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shipId);
        string[] parts = shipId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var groups = new List<string>();
        int index = 0;
        while (index < parts.Length)
        {
            string part = parts[index];
            if (index + 1 < parts.Length && parts[index + 1].All(char.IsDigit))
            {
                groups.Add($"{CapitalizeFirstCharacter(part)}-{parts[index + 1]}");
                index += 2;
                continue;
            }
            if (index + 1 < parts.Length)
            {
                groups.Add($"{CapitalizeFirstCharacter(part)}-{parts[index + 1]}");
                index += 2;
                continue;
            }
            groups.Add(CapitalizeFirstCharacter(part));
            index++;
        }
        return string.Join(' ', groups);
    }

    private static string CapitalizeFirstCharacter(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }
        if (value.Length == 1)
        {
            return value.ToUpperInvariant();
        }
        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    public static string FormatFactionDisplayName(string factionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);
        string key = factionId.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return key switch
        {
            "REBEL" or "REBELALLIANCE" => "Rebel Alliance",
            "GALACTICEMPIRE" or "EMPIRE" => "Galactic Empire",
            "SCUMANDVILLAINY" or "SCUM" => "Scum and Villainy",
            "FIRSTORDER" => "First Order",
            "RESISTANCE" => "Resistance",
            "GALACTICREPUBLIC" or "REPUBLIC" => "Galactic Republic",
            "SEPARATISTALLIANCE" or "SEPARATISTS" => "Separatist Alliance",
            _ => factionId.Trim(),
        };
    }

    public static string? GetFactionIconSlug(string factionDisplayName)
    {
        if (string.IsNullOrWhiteSpace(factionDisplayName))
        {
            return null;
        }
        return factionDisplayName.Trim() switch
        {
            "Rebel Alliance" => "rebel",
            "Galactic Empire" => "empire",
            "Scum and Villainy" => "scum",
            "Resistance" => "rebel-outline",
            "Separatist Alliance" => "separatists",
            "First Order" => "firstorder",
            "Galactic Republic" => "republic",
            _ => null,
        };
    }

    public static string GetShipIconSlug(string shipId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shipId);
        return shipId.Replace("-", string.Empty, StringComparison.Ordinal);
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
}
