namespace Xwing.Web.Stores;

public static class XwaFactionCatalog
{
    public static IReadOnlyList<string> AllDisplayNames { get; } =
    [
        "Rebel Alliance",
        "Galactic Empire",
        "Scum and Villainy",
        "First Order",
        "Resistance",
        "Galactic Republic",
        "Separatist Alliance",
    ];

    public static bool IsKnownDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return AllDisplayNames.Contains(displayName.Trim(), StringComparer.Ordinal);
    }
}
