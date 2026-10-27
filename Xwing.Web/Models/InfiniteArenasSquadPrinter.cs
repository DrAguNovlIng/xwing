using System.Diagnostics.CodeAnalysis;

namespace Xwing.Web.Models;

public static class InfiniteArenasSquadPrinter
{
    [SuppressMessage("Sonar", "S1075:URIs should not be hardcoded", Justification = "Infinite Arenas squad printer is a fixed external tool.")]
    public const string Url = "https://infinitearenas.com/squadPrinter.php";
}
