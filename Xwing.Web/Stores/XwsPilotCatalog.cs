using System.Reflection;
using System.Text.Json;

namespace Xwing.Web.Stores;
public static class XwsPilotCatalog
{
    private static readonly Lazy<Dictionary<string, string>> DisplayNames = new(LoadDisplayNames);
    public static string ResolvePilotLabel(string? pilotId, string shipId)
    {
        if (string.IsNullOrWhiteSpace(pilotId))
        {
            return XwaSquad.FormatShipDisplayName(shipId);
        }

        string key = pilotId.Trim();
        if (DisplayNames.Value.TryGetValue(key, out string? displayName))
        {
            return displayName;
        }

        return FormatPilotIdFallback(key);
    }

    private static string FormatPilotIdFallback(string pilotId)
    {
        string[] parts = pilotId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return pilotId;
        }

        return string.Join(' ', parts.Select(CapitalizeFirstCharacter));
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

    private static Dictionary<string, string> LoadDisplayNames()
    {
        Assembly assembly = typeof(XwsPilotCatalog).Assembly;
        const string resourceName = "Xwing.Web.Data.xws-pilot-names.json";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        }

        Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return parsed ?? new Dictionary<string, string>();
    }
}
