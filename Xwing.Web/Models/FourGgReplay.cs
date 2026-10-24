using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Xwing.Web.Models;

public static partial class FourGgReplay
{
    [SuppressMessage("Sonar", "S1075:URIs should not be hardcoded", Justification = "4GG watch URL is a fixed product endpoint.")]
    public const string WatchBaseUrl = "https://4gg.dev/";

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public static Result? ValidateAndNormalize(string? input, out string? storedCode)
    {
        storedCode = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string trimmed = input.Trim();
        if (TryExtractReplayCode(trimmed, out string? extracted))
        {
            storedCode = extracted;
            return null;
        }

        if (CodePattern().IsMatch(trimmed))
        {
            storedCode = trimmed;
            return null;
        }

        return Result.Fail("Enter a valid 4GG replay code.");
    }

    public static string? GetCodeForDisplay(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        string trimmed = storedValue.Trim();
        if (TryExtractReplayCode(trimmed, out string? code))
        {
            return code;
        }

        if (CodePattern().IsMatch(trimmed))
        {
            return trimmed;
        }

        return null;
    }

    [SuppressMessage("Design", "CA1055:URI-like return types should not be strings", Justification = "Blazor links and persistence use string URLs.")]
    public static string? ToWatchUrl(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        string trimmed = storedValue.Trim();
        if (TryExtractReplayCode(trimmed, out string? code))
        {
            return BuildWatchUrl(code!);
        }

        if (CodePattern().IsMatch(trimmed))
        {
            return BuildWatchUrl(trimmed);
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return trimmed;
        }

        return null;
    }

    [SuppressMessage("Design", "CA1055:URI-like return types should not be strings", Justification = "Blazor links use string hrefs.")]
    public static string BuildWatchUrl(string code) =>
        QueryHelpers.AddQueryString(WatchBaseUrl, "replay", code);

    private static bool TryExtractReplayCode(string input, out string? code)
    {
        code = null;
        if (CodePattern().IsMatch(input))
        {
            code = input;
            return true;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!uri.Host.Equals("4gg.dev", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("replay", out StringValues replayValues) || replayValues.Count == 0)
        {
            return false;
        }

        string replayCode = replayValues[0]!.Trim();
        if (string.IsNullOrWhiteSpace(replayCode))
        {
            return false;
        }
        if (!CodePattern().IsMatch(replayCode))
        {
            return false;
        }

        code = replayCode;
        return true;
    }
}
