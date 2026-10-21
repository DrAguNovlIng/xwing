using Microsoft.JSInterop;
using Xwing.Web.Auth;

namespace Xwing.Web.Theme;

public static class ThemePreferenceCookie
{
    public const string CookieName = "xwing-theme";

    public static ThemePreference Read(HttpContext? context)
    {
        if (context?.Request.Cookies.TryGetValue(CookieName, out string? value) != true)
        {
            return ThemePreference.System;
        }
        return Parse(value);
    }

    public static string ToCookieValue(ThemePreference preference) =>
        preference switch
        {
            ThemePreference.Light => "light",
            ThemePreference.Dark => "dark",
            _ => "system",
        };

    public static async Task WriteAsync(IJSRuntime jsRuntime, ThemePreference preference)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        int maxAgeSeconds = AuthCookieSettings.PersistentCookieDays * 24 * 60 * 60;
        await jsRuntime.InvokeVoidAsync(
            "xwingTheme.setPreference",
            ToCookieValue(preference),
            maxAgeSeconds);
    }

    private static ThemePreference Parse(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "LIGHT" => ThemePreference.Light,
            "DARK" => ThemePreference.Dark,
            _ => ThemePreference.System,
        };
}
